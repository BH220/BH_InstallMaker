using System.IO;
using System.Reflection;
using System.Text.Json;

namespace BH_Install.Core
{
    //program.json 매니페스트와 페이로드(런처/언인스톨 exe)를 다룬다.
    //
    //메이커가 각 모듈을 게시할 때 -p:BH_ProgramJson / -p:BH_LauncherExe / -p:BH_UninstallExe 로 넘긴 파일이
    //아래 LogicalName 으로 모듈의 진입 어셈블리에 임베드된다 (BH.Publish.targets, BH_Install.csproj 참고).
    //단일 파일 exe 안에 들어가므로 코드 서명으로 함께 봉인되고, 설치 뒤에 바꿀 수 없다.
    public static class ProgramManifest
    {
        public const string ResourceName = "BH.program.json";
        public const string LauncherPayloadName = "BH.payload.BH_Launcher.exe";
        public const string UninstallPayloadName = "BH.payload.BH_Uninstall.exe";

        //설치 폴더에 놓이는 파일 이름. 메이커가 만드는 exe 이름과 같은 규칙이다: {대상 exe 이름}.Launcher.exe
        public static string LauncherFileName(ProgramModel m) => $"{ExeStem(m)}.Launcher.exe";
        public static string UninstallFileName(ProgramModel m) => $"{ExeStem(m)}.Uninstall.exe";
        public static string InstallFileName(ProgramModel m) => $"{ExeStem(m)}.Install.exe";

        private static string ExeStem(ProgramModel m)
        {
            string stem = Path.GetFileNameWithoutExtension(m.MainExe ?? string.Empty);
            return string.IsNullOrWhiteSpace(stem) ? "BH" : stem;
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public static string ToJson(ProgramModel model) => JsonSerializer.Serialize(model, JsonOptions);

        public static ProgramModel? FromJson(string json) => JsonSerializer.Deserialize<ProgramModel>(json, JsonOptions);

        //진입 어셈블리에 임베드된 매니페스트를 읽는다. 없으면(F5 디버깅 등) null.
        public static ProgramModel? LoadEmbedded(Assembly? assembly = null)
        {
            assembly ??= Assembly.GetEntryAssembly();
            if (assembly is null) return null;

            using Stream? stream = assembly.GetManifestResourceStream(ResourceName);
            if (stream is null) return null;

            using var reader = new StreamReader(stream);
            return FromJson(reader.ReadToEnd());
        }

        //임베드된 페이로드가 있는지
        public static bool HasPayload(string resourceName, Assembly? assembly = null)
        {
            assembly ??= Assembly.GetEntryAssembly();
            return assembly?.GetManifestResourceInfo(resourceName) is not null;
        }

        //임베드된 페이로드를 파일로 꺼낸다. 없으면 false.
        public static bool ExtractPayload(string resourceName, string destinationPath, Assembly? assembly = null)
        {
            assembly ??= Assembly.GetEntryAssembly();
            using Stream? stream = assembly?.GetManifestResourceStream(resourceName);
            if (stream is null) return false;

            string? dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            using FileStream file = File.Create(destinationPath);
            stream.CopyTo(file);
            return true;
        }
    }
}