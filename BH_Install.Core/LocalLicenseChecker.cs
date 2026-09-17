using Microsoft.Win32;

namespace BH_Install.Core
{
    //설치 프로그램이 레지스트리에 남기고 런처가 확인하는 로컬 라이선스 표식.
    //  값 위치 : HKLM\{registryKey}\license
    //  값 내용 : ProgramId 번호를 BhsCrypto(AES-256-GCM, 키 bhsoft) 로 암호화한 것에서 "v1:" 접두어를 뗀 base64.
    //  런처는 "v1:" 을 다시 붙여 복호화하고 매니페스트의 ProgramId 와 맞춰본다.
    public class LocalLicenseChecker
    {
        public const string ValueName = "license";
        private const string AesKey = "bhsoft";
        private static readonly string Prefix = BhsCrypto.Version + ":";

        private static LocalLicenseChecker? _instance;

        public static LocalLicenseChecker Instance => _instance ??= new LocalLicenseChecker();

        private LocalLicenseChecker() { }

        //레지스트리의 license 값이 프로그램 ID 와 맞는지 확인한다. 값이 없거나 복호화 실패·불일치면 false.
        public bool IsLicenseValid(int bhProgramId, string registryKey)
            => IsLicenseValid(bhProgramId, registryKey, out _);

        //틀리면 false 와 사용자에게 보여줄 문구.
        public bool IsLicenseValid(int bhProgramId, string registryKey, out string message)
        {
            string? stored = RegisterHelper.ReadProgramValue(registryKey, ValueName);
            if (string.IsNullOrWhiteSpace(stored))
            {
                message = "라이선스 정보가 없습니다.\n설치 프로그램으로 다시 설치해 주세요.";
                return false;
            }

            if (!BhsCrypto.TryDecrypt(Prefix + stored.Trim(), AesKey, out string plain)
                || plain != ((int)bhProgramId).ToString())
            {
                message = "라이선스 정보가 올바르지 않습니다.\n정상적으로 설치된 프로그램이 아니거나 설치 정보가 변경되었습니다.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        //레지스트리에 기록할 값을 만든다 ("v1:" 없음). 매번 다른 값이 나온다.
        public string CreateToken(BhProgramId bhProgramId)
            => BhsCrypto.Encrypt(((int)bhProgramId).ToString(), AesKey)[Prefix.Length..];

        //설치 시 HKLM\{registryKey}\license 에 표식을 기록한다. 관리자 권한이 필요하다.
        public void LocalLicenseWrite(BhProgramId bhProgramId, string registryKey)
        {
            if (string.IsNullOrWhiteSpace(registryKey))
                throw new InvalidOperationException("레지스트리 키가 비어 있어 라이선스를 기록할 수 없습니다.");

            using RegistryKey key = Registry.LocalMachine.CreateSubKey(registryKey, writable: true)
                ?? throw new InvalidOperationException($"레지스트리 키를 만들 수 없습니다: HKLM\\{registryKey}");
            key.SetValue(ValueName, CreateToken(bhProgramId));
        }
    }
}
