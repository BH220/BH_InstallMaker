using BH_Install.Core.Common;
using BH_Install.Core.Manager;
using Microsoft.Win32;

namespace BH_Install.Core.Helper
{
    //설치 정보를 레지스트리(HKLM)에 기록·삭제한다. 관리자 권한이 필요하다.
    //  - HKLM\{RegistryKey}                             : 프로그램 자체 키. 설치 경로·버전 등. 런처·언인스톨러가 읽는다.
    //  - HKLM\...\CurrentVersion\Uninstall\{Name}       : Windows "앱 및 기능" 목록 항목. 언인스톨러를 연결한다.
    //  - HKLM\...\CurrentVersion\App Paths\{Cmd}.exe    : 실행창(Win+R)에서 ExeCommand 만 입력해도 런처가 뜨게 하는 단축 명령.
    public static class RegisterHelper
    {
        private const string UninstallRoot = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        private const string AppPathsRoot = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";

        //App Paths 의 키 이름은 exe 이름이어야 하므로 {ExeCommand}.exe 로 만든다. ExeCommand 가 비어 있으면 null (등록하지 않음).
        private static string? AppPathKeyName(ProgramModel m)
        {
            string command = (m.ExeCommand ?? string.Empty).Trim();
            if (command.Length == 0) return null;
            return command.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? command : command + ".exe";
        }

        //Uninstall 하위 키 이름. 프로그램 이름을 그대로 쓴다 (버전이 바뀌어도 같아야 업그레이드로 처리된다).
        private static string UninstallKeyName(ProgramModel m) => m.Name.Trim();

        //SOFTWARE 처럼 너무 짧은 키를 지우는 사고를 막는다. 최소 "SOFTWARE\제작자\프로그램" 깊이를 요구한다.
        private static void EnsureSafeKey(string registryKey)
        {
            if (string.IsNullOrWhiteSpace(registryKey) || registryKey.Trim('\\').Split('\\').Length < 3)
                throw new InvalidOperationException($"레지스트리 키가 너무 짧습니다. SOFTWARE\\제작자\\프로그램 형식이어야 합니다: {registryKey}");
        }

        //프로그램 자체 키에 설치 정보를 기록한다.
        public static void WriteProgramInfo(ProgramModel m, string launcherPath)
        {
            if (string.IsNullOrWhiteSpace(m.RegistryKey)) return;
            EnsureSafeKey(m.RegistryKey);

            using RegistryKey key = Registry.LocalMachine.CreateSubKey(m.RegistryKey, writable: true)
                ?? throw new InvalidOperationException($"레지스트리 키를 만들 수 없습니다: HKLM\\{m.RegistryKey}");

            key.SetValue("DisplayName", m.Name);
            key.SetValue("Publisher", m.Publisher);
            key.SetValue("Version", m.Version);
            key.SetValue("InstallPath", m.RootPath);
            key.SetValue("DataPath", m.DataRootPath);
            key.SetValue("MainExe", m.MainExe);
            key.SetValue("UpdateUrl", m.UpdateUrl);
            key.SetValue("Launcher", launcherPath);
            key.SetValue("license", LocalLicenseChecker.Instance.CreateToken(m.ProgramId));
            key.SetValue("InstallDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        //"앱 및 기능" 목록에 표시되도록 Uninstall 항목을 등록한다.
        public static void WriteUninstallEntry(ProgramModel m, string uninstallerPath, string launcherPath, long installedBytes)
        {
            using RegistryKey key = Registry.LocalMachine.CreateSubKey($@"{UninstallRoot}\{UninstallKeyName(m)}", writable: true)
                ?? throw new InvalidOperationException("Uninstall 레지스트리 키를 만들 수 없습니다.");

            key.SetValue("DisplayName", m.Name);
            key.SetValue("DisplayVersion", m.Version);
            key.SetValue("Publisher", m.Publisher);
            key.SetValue("Comments", m.Description);
            key.SetValue("InstallLocation", m.RootPath);
            key.SetValue("DisplayIcon", launcherPath);
            key.SetValue("UninstallString", $"\"{uninstallerPath}\"");
            key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
            key.SetValue("EstimatedSize", (int)Math.Min(int.MaxValue, installedBytes / 1024), RegistryValueKind.DWord);
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }

        //실행창(Win+R)·명령 프롬프트에서 ExeCommand 만 입력해도 런처가 실행되도록 App Paths 에 등록한다.
        //Windows 는 입력한 이름에 .exe 를 붙여 App Paths 에서 찾고, 그 키의 기본값(전체 경로)을 실행한다. ExeCommand 가 비어 있으면 건너뛴다.
        public static void WriteAppPath(ProgramModel m, string launcherPath)
        {
            string? keyName = AppPathKeyName(m);
            if (keyName is null) return;

            using RegistryKey key = Registry.LocalMachine.CreateSubKey($@"{AppPathsRoot}\{keyName}", writable: true)
                ?? throw new InvalidOperationException($"App Paths 레지스트리 키를 만들 수 없습니다: {keyName}");

            key.SetValue(string.Empty, launcherPath);
            key.SetValue("Path", m.RootPath);
        }

        //설치 때 만든 키를 모두 지운다. 없으면 조용히 넘어간다.
        public static void Remove(ProgramModel m)
        {
            if (!string.IsNullOrWhiteSpace(m.RegistryKey))
            {
                EnsureSafeKey(m.RegistryKey);
                Registry.LocalMachine.DeleteSubKeyTree(m.RegistryKey, throwOnMissingSubKey: false);
            }
            Registry.LocalMachine.DeleteSubKeyTree($@"{UninstallRoot}\{UninstallKeyName(m)}", throwOnMissingSubKey: false);

            if (AppPathKeyName(m) is { } appPathKey)
                Registry.LocalMachine.DeleteSubKeyTree($@"{AppPathsRoot}\{appPathKey}", throwOnMissingSubKey: false);
        }

        //프로그램 자체 키의 값을 읽는다. 없으면 null.
        public static string? ReadProgramValue(string registryKey, string valueName)
        {
            if (string.IsNullOrWhiteSpace(registryKey)) return null;
            try
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(registryKey);
                return key?.GetValue(valueName)?.ToString();
            }
            catch
            {
                return null;
            }
        }
    }
}