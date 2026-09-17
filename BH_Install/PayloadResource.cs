using System.IO;
using System.Reflection;

namespace BH_Install
{
    //메이커가 설치 exe 에 임베드한 런처·언인스톨 exe.
    //BH_Install.csproj 가 -p:BH_LauncherExe / -p:BH_UninstallExe 로 받은 파일을 아래 LogicalName 으로 넣는다.
    //설치 단계에서 설치 폴더로 꺼낸다. F5 실행처럼 임베드가 없으면 Extract 가 false 를 돌려준다.
    internal static class PayloadResource
    {
        public const string Launcher = "BH.payload.BH_Launcher.exe";
        public const string Uninstall = "BH.payload.BH_Uninstall.exe";

        public static bool Extract(string resourceName, string destinationPath)
        {
            Assembly assembly = typeof(PayloadResource).Assembly;
            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null) return false;

            string? dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            using FileStream file = File.Create(destinationPath);
            stream.CopyTo(file);
            return true;
        }
    }
}
