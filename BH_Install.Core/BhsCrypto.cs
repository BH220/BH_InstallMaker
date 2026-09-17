using System.Security.Cryptography;
using System.Text;

namespace BH_Install.Core
{
    //BHS_Api 의 Crypto(PHP) 와 같은 형식의 AES-256-GCM 암복호화.
    //  저장 형식 : "v1:base64( iv(12) + tag(16) + ciphertext )"
    //  인증 데이터: 버전 문자열("v1"). 서버와 똑같이 묶어야 복호화된다.
    //  키        : base64 로 풀어 32바이트면 그대로, 아니면 문자열을 SHA-256 으로 파생 (bhs_crypto_tool 과 동일)
    public static class BhsCrypto
    {
        public const string Version = "v1";
        private const int IvLen = 12;
        private const int TagLen = 16;

        //암호화. 매번 다른 값이 나온다.
        public static string Encrypt(string plain, string keyText)
        {
            byte[] key = MakeKey(keyText);
            byte[] iv = RandomNumberGenerator.GetBytes(IvLen);
            byte[] data = Encoding.UTF8.GetBytes(plain);
            byte[] cipher = new byte[data.Length];
            byte[] tag = new byte[TagLen];

            using var aes = new AesGcm(key, TagLen);
            aes.Encrypt(iv, data, cipher, tag, Encoding.UTF8.GetBytes(Version));

            byte[] packed = new byte[IvLen + TagLen + cipher.Length];
            Buffer.BlockCopy(iv, 0, packed, 0, IvLen);
            Buffer.BlockCopy(tag, 0, packed, IvLen, TagLen);
            Buffer.BlockCopy(cipher, 0, packed, IvLen + TagLen, cipher.Length);
            return $"{Version}:{Convert.ToBase64String(packed)}";
        }

        //복호화. 형식이 다르거나 키가 맞지 않거나 변조됐으면 false.
        public static bool TryDecrypt(string? stored, string keyText, out string plain)
        {
            plain = string.Empty;
            if (string.IsNullOrWhiteSpace(stored))
                return false;

            int sep = stored.IndexOf(':');
            if (sep <= 0)
                return false;

            try
            {
                string version = stored[..sep];
                byte[] raw = Convert.FromBase64String(stored[(sep + 1)..].Trim());
                if (raw.Length < IvLen + TagLen)
                    return false;

                byte[] iv = raw[..IvLen];
                byte[] tag = raw[IvLen..(IvLen + TagLen)];
                byte[] cipher = raw[(IvLen + TagLen)..];
                byte[] data = new byte[cipher.Length];

                using var aes = new AesGcm(MakeKey(keyText), TagLen);
                aes.Decrypt(iv, cipher, tag, data, Encoding.UTF8.GetBytes(version));
                plain = Encoding.UTF8.GetString(data);
                return true;
            }
            catch
            {
                return false;
            }
        }

        //키 문자열 → 32바이트 원시 키
        private static byte[] MakeKey(string keyText)
        {
            try
            {
                byte[] raw = Convert.FromBase64String(keyText);
                if (raw.Length == 32)
                    return raw;
            }
            catch (FormatException)
            {
            }
            return SHA256.HashData(Encoding.UTF8.GetBytes(keyText));
        }
    }
}
