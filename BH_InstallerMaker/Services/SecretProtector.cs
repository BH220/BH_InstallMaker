using System.Security.Cryptography;
using System.Text;

namespace BH_InstallerMaker.Services
{
    //설정 파일에 저장하는 비밀값(PFX 암호)을 AES-256-GCM 으로 암호화/복호화한다.
    //키는 코드에 고정되어 있으므로 파일을 그대로 읽히지 않게 하는 수준이다. 실행 파일을 가진 사람은 복호화할 수 있다.
    //저장 형식: Base64( nonce(12) + tag(16) + ciphertext )
    public static class SecretProtector
    {
        private static readonly byte[] Key =
        {
            0x9D, 0x2D, 0xB3, 0xDC, 0xDB, 0xEC, 0xD0, 0x95, 0x5D, 0x43, 0x63, 0x13, 0xB8, 0x46, 0x5E, 0x6E, 0xEB, 0x8E, 0x3D, 0xE8, 0x7E, 0x61, 0x18, 0x8F, 0x70, 0x0D, 0x51, 0x43, 0x80, 0x38, 0x22, 0x55
        };

        private const int NonceSize = 12;
        private const int TagSize = 16;

        public static string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain))
                return string.Empty;

            byte[] data = Encoding.UTF8.GetBytes(plain);
            byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
            byte[] cipher = new byte[data.Length];
            byte[] tag = new byte[TagSize];

            using var aes = new AesGcm(Key, TagSize);
            aes.Encrypt(nonce, data, cipher, tag);

            byte[] packed = new byte[NonceSize + TagSize + cipher.Length];
            Buffer.BlockCopy(nonce, 0, packed, 0, NonceSize);
            Buffer.BlockCopy(tag, 0, packed, NonceSize, TagSize);
            Buffer.BlockCopy(cipher, 0, packed, NonceSize + TagSize, cipher.Length);
            return Convert.ToBase64String(packed);
        }

        //복호화. 형식이 다르거나 키가 맞지 않으면(다른 빌드로 저장된 값 등) 빈 문자열.
        public static string Unprotect(string protectedText)
        {
            if (string.IsNullOrWhiteSpace(protectedText))
                return string.Empty;

            try
            {
                byte[] packed = Convert.FromBase64String(protectedText);
                if (packed.Length < NonceSize + TagSize)
                    return string.Empty;

                byte[] nonce = packed[..NonceSize];
                byte[] tag = packed[NonceSize..(NonceSize + TagSize)];
                byte[] cipher = packed[(NonceSize + TagSize)..];
                byte[] plain = new byte[cipher.Length];

                using var aes = new AesGcm(Key, TagSize);
                aes.Decrypt(nonce, cipher, tag, plain);
                return Encoding.UTF8.GetString(plain);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}