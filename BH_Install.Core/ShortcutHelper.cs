using System.IO;

namespace BH_Install.Core
{
    //바로 가기(.lnk) 생성·삭제. WScript.Shell COM 을 늦은 바인딩(dynamic)으로 쓴다.
    public static class ShortcutHelper
    {
        //모든 사용자 시작 메뉴 > 프로그램
        public static string StartMenuDir => Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);

        //모든 사용자 바탕화면
        public static string DesktopDir => Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);

        //시작 메뉴 > 프로그램 > {제작자} 폴더. 설치되는 프로그램은 모두 이 폴더 아래에 놓인다.
        public static string StartMenuDirFor(ProgramModel m)
        {
            string publisher = string.IsNullOrWhiteSpace(m.Publisher) ? "BH Soft" : m.Publisher.Trim();
            foreach (char c in Path.GetInvalidFileNameChars()) publisher = publisher.Replace(c, '_');
            return Path.Combine(StartMenuDir, publisher);
        }

        //폴더가 비어 있으면 지운다 (제거 후 제작자 폴더 정리용)
        public static void DeleteDirectoryIfEmpty(string dir)
        {
            try
            {
                if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                    Directory.Delete(dir);
            }
            catch
            {
                //정리 실패는 무시
            }
        }

        public static void Create(string lnkPath, string targetPath, string? description = null, string? workingDir = null)
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell")
                ?? throw new InvalidOperationException("WScript.Shell 을 사용할 수 없습니다.");

            string? dir = Path.GetDirectoryName(lnkPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic link = shell.CreateShortcut(lnkPath);
            link.TargetPath = targetPath;
            link.WorkingDirectory = workingDir ?? Path.GetDirectoryName(targetPath) ?? string.Empty;
            link.Description = description ?? string.Empty;
            link.IconLocation = targetPath + ",0";
            link.Save();
        }

        //있으면 지우고 true, 없으면 false
        public static bool DeleteIfExists(string lnkPath)
        {
            if (!File.Exists(lnkPath)) return false;
            File.Delete(lnkPath);
            return true;
        }
    }
}