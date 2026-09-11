using Microsoft.Win32;

namespace BH_Install.Core
{
    //설치 정보를 레지스트리(HKLM)에 기록·삭제한다. 관리자 권한이 필요하다.
    //  - HKLM\{RegistryKey}                          : 프로그램 자체 키. 설치 경로·버전 등. 런처·언인스톨러가 읽는다.
    //  - HKLM\...\CurrentVersion\Uninstall\{Code}    : Windows "앱 및 기능" 목록 항목. 언인스톨러를 연결한다.
    public static class RegisterHelper
    {
        private const string UninstallRoot = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

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
            key.SetValue("Launcher", launcherPath);
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

        //설치 때 만든 키를 모두 지운다. 없으면 조용히 넘어간다.
        public static void Remove(ProgramModel m)
        {
            if (!string.IsNullOrWhiteSpace(m.RegistryKey))
            {
                EnsureSafeKey(m.RegistryKey);
                Registry.LocalMachine.DeleteSubKeyTree(m.RegistryKey, throwOnMissingSubKey: false);
            }
            Registry.LocalMachine.DeleteSubKeyTree($@"{UninstallRoot}\{UninstallKeyName(m)}", throwOnMissingSubKey: false);
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