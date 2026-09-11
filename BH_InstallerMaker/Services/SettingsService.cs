using System.IO;
using System.Text.Json;
using BH_Install.Core;

namespace BH_InstallerMaker.Services
{
    //앱 재시작 후에도 유지할 사용자 설정.
    //코드 서명은 필수라 켜고 끄는 값이 없고, 모듈 소스 위치는 메이커 실행 위치에서 자동으로 찾는다.
    public sealed class MakerSettings
    {
        public string PfxPath { get; set; } = "";
        //PFX 암호. SecretProtector 로 AES 암호화한 Base64 문자열
        public string PfxPasswordEnc { get; set; } = "";
        public string TimestampUrl { get; set; } = CodeSigner.DefaultTimestampUrl;
    }

    //%LOCALAPPDATA%\BH Soft\BH_InstallerMaker\settings.json 읽기/쓰기
    public sealed class SettingsService
    {
        private static readonly string Dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BH Soft", "BH_InstallerMaker");

        private static readonly string FilePath = Path.Combine(Dir, "settings.json");

        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public MakerSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonSerializer.Deserialize<MakerSettings>(File.ReadAllText(FilePath)) ?? new MakerSettings();
            }
            catch
            {
                //손상된 설정 파일은 무시하고 기본값으로 시작
            }
            return new MakerSettings();
        }

        public void Save(MakerSettings settings)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, JsonOptions));
            }
            catch
            {
                //설정 저장 실패는 동작을 막지 않는다
            }
        }
    }
}