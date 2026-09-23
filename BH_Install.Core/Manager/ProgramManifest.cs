using BH_Install.Core.Common;
using Newtonsoft.Json;
using System.IO;
using System.Reflection; 

namespace BH_Install.Core.Manager
{
    public class ProgramManifest
    {
        //포함 리소스 이름(csproj 의 LogicalName)과, SaveToModel() 이 덮어쓰는 소스 파일 경로(프로젝트 폴더 기준)
        private const string ResourceName = "ProgramModel.json";
        private const string ModelFile = @"Resources\ProgramModel.json";

        //대상 프로그램 아이콘. SaveToModel() 이 MainIcon 파일을 IconFile 로 복사해 두면 WPF 리소스로 dll 에 들어간다.
        //모듈은 IconResourceLoader.LoadBestFrame(IconResourcePath, 크기) 로 읽는다.
        private const string IconFile = @"Resources\ProgramIcon.ico";
        private const string DefaultIconFile = @"Resources\ProgramIcon.default.ico";
        public const string IconResourcePath = "BH_Install.Core;component/Resources/ProgramIcon.ico";

        public ProgramModel ProgramModel { get; private set; }

        //매니페스트가 채워져 있는지. 메이커는 MainExe 를 항상 넣으므로 비어 있으면 빌드를 거치지 않은 것(F5, 빈 ProgramModel.json)이다.
        //모듈은 이 값이 false 면 화면만 보여주는 미리보기 모드로 동작한다.
        public bool IsLoaded => !string.IsNullOrWhiteSpace(ProgramModel.MainExe);

        //모듈 exe 이름: {대상 exe 이름}.Install.exe / .Launcher.exe / .Uninstall.exe
        //메이커가 산출물 이름을 정할 때와 설치 프로그램이 설치 폴더에 놓을 때 같은 규칙을 쓴다.
        //ProgramModel 에서 매번 계산하므로 SaveToModel() 뒤에도 새 값이 나온다.
        public string InstallFileName => $"{ExeStem}.Install.exe";
        public string LauncherFileName => $"{ExeStem}.Launcher.exe";
        public string UninstallFileName => $"{ExeStem}.Uninstall.exe";

        private string ExeStem
        {
            get
            {
                string stem = Path.GetFileNameWithoutExtension(ProgramModel.MainExe ?? string.Empty);
                return string.IsNullOrWhiteSpace(stem) ? "BH" : stem;
            }
        }

        private static ProgramManifest instance = null;
        public static ProgramManifest Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new ProgramManifest();
                    instance.InitLoad();
                }
                return instance;
            }
        }

        private void InitLoad()
        {
            Assembly assembly = typeof(ProgramManifest).Assembly;   // BH_Install.Core
            using Stream? stream = assembly.GetManifestResourceStream(ResourceName);
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                string json = reader.ReadToEnd();
                if (string.IsNullOrWhiteSpace(json))
                    ProgramModel = new ProgramModel();
                else
                    ProgramModel = JsonConvert.DeserializeObject<ProgramModel>(json) ?? new ProgramModel();
            }
            else
            {
                ProgramModel = new ProgramModel();
            }
        }


        public void SaveToModel(ProgramModel model)
        {
            string dir = FindCoreProjectDir()
                ?? throw new DirectoryNotFoundException(
                    $"{CoreProjectName} 프로젝트 폴더를 찾을 수 없습니다. 메이커는 이 저장소 안에서 실행해야 합니다.");

            string path = Path.Combine(dir, ModelFile);
            string json = JsonConvert.SerializeObject(model, Formatting.Indented);
            File.WriteAllText(path, json, new System.Text.UTF8Encoding(false));

            //아이콘: MainIcon(메이커 PC 의 .ico 절대 경로)은 설치 대상 PC 에 없으므로 파일 자체를 리소스로 넣는다.
            //대상 프로젝트에 아이콘이 없으면 기본(BH) 아이콘으로 되돌려 이전 빌드의 아이콘이 남지 않게 한다.
            string iconSource = !string.IsNullOrWhiteSpace(model.MainIcon) && File.Exists(model.MainIcon)
                ? model.MainIcon
                : Path.Combine(dir, DefaultIconFile);
            string iconPath = Path.Combine(dir, IconFile);
            File.Copy(iconSource, iconPath, overwrite: true);
            //File.Copy 는 원본의 수정 시각을 그대로 옮기므로, 그대로 두면 MSBuild 증분 빌드가 "바뀐 게 없다"고 보고 이전 아이콘을 다시 넣는다
            File.SetLastWriteTimeUtc(iconPath, DateTime.UtcNow);

            ProgramModel = model;
        }

        private const string CoreProjectName = "BH_Install.Core";
         
        public static string? FindCoreProjectDir()
        {
            for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, CoreProjectName);
                if (File.Exists(Path.Combine(candidate, CoreProjectName + ".csproj")))
                    return candidate;
            }
            return null;
        }

    }
}