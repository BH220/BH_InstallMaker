using System.IO;
using System.Text.RegularExpressions;
using BH_Install.Core;
using BH_Install.Core.Common;
using BH_Install.Core.Manager;

namespace BH_InstallerMaker.Services
{
    //설치 프로그램 산출물을 만든다.
    //  런처 게시·서명 → 언인스톨 게시·서명 → (둘을 페이로드로 넣어) 설치 게시·서명 → Setup exe
    //
    //세 모듈은 이 저장소의 프로젝트를 dotnet publish 로 다시 빌드한다.
    //매니페스트는 게시 직전에 ProgramManifest.SaveToModel() 로 BH_Install.Core\Resources\ProgramModel.json 에 써 두어
    //각 모듈이 참조하는 BH_Install.Core.dll 의 포함 리소스로 들어가고,
    //런처·언인스톨 exe 는 -p:BH_LauncherExe / -p:BH_UninstallExe 로 설치 프로그램에 임베드된다(BH_Install.csproj).
    public sealed class ModuleBuilder
    {
        public const string SolutionFileName = "BH_InstallerMaker.sln";

        private const string LauncherProject = "BH_Launcher";
        private const string UninstallProject = "BH_Uninstall";
        private const string InstallProject = "BH_Install";

        private readonly ProjectService _projects;

        public ModuleBuilder(ProjectService projects) => _projects = projects;

        public sealed record BuildResult(string SetupPath, string LauncherPath, string UninstallerPath);

        //모듈 소스 루트(.sln 이 있는 폴더). 메이커는 이 저장소 안에서 실행되므로 실행 위치에서 위로 올라가며 찾는다.
        public static string? FindModuleSourceRoot()
        {
            DirectoryInfo? dir = new(AppContext.BaseDirectory);
            for (int i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, SolutionFileName)))
                    return dir.FullName;
            }
            return null;
        }

        public async Task<BuildResult> BuildAsync(ProgramModel model, string workDir, string sourceRoot, SignOptions sign, Action<string> log, CancellationToken ct = default)
        {
            //작업 폴더를 비운 상태로 시작
            if (Directory.Exists(workDir))
                Directory.Delete(workDir, recursive: true);
            Directory.CreateDirectory(workDir);

            ProgramManifest.Instance.SaveToModel(model);

            //모듈 exe 의 파일 버전에 프로그램 버전을 찍는다 (숫자.숫자 형식일 때만)
            string? version = IsStampableVersion(model.Version) ? model.Version : null;
            if (version is null)
                log($"버전 '{model.Version}' 은 exe 버전 정보로 쓸 수 없는 형식이라 모듈 버전 표기를 건너뜁니다.");

            //1) 런처. exe 아이콘은 SaveToModel() 이 Core 에 복사해 둔 ProgramIcon.ico(대상 아이콘, 없으면 BH 기본)를 쓴다.
            //   대상 .ico 를 직접 넘기지 않는 이유: 그 파일은 수정 시각이 오래되어 증분 빌드가 바뀐 걸 모르고 옛 아이콘을 남긴다.
            //   Core 의 사본은 SaveToModel() 이 수정 시각을 갱신하므로 매번 새로 들어간다.
            string launcherIcon = Path.Combine(sourceRoot, "BH_Install.Core", "Resources", "ProgramIcon.ico");
            var launcherProps = new Dictionary<string, string> { ["BH_LauncherIcon"] = launcherIcon };
            log(string.IsNullOrWhiteSpace(model.MainIcon)
                ? "대상 프로젝트에 ApplicationIcon 이 없어 런처는 기본 아이콘을 씁니다."
                : $"런처 아이콘: {model.MainIcon}");
            string launcherExe = await PublishModuleAsync(sourceRoot, LauncherProject, Path.Combine(workDir, "launcher"),
                version, launcherProps, log, ct);
            await SignAsync(sign, launcherExe, $"{model.Name} 런처", log, ct);

            //2) 언인스톨
            string uninstallExe = await PublishModuleAsync(sourceRoot, UninstallProject, Path.Combine(workDir, "uninstall"),
                version, null, log, ct);
            await SignAsync(sign, uninstallExe, $"{model.Name} 제거", log, ct);

            //3) 설치 (서명된 런처·언인스톨을 페이로드로 포함)
            var payload = new Dictionary<string, string>
            {
                ["BH_LauncherExe"] = launcherExe,
                ["BH_UninstallExe"] = uninstallExe,
            };
            string installExe = await PublishModuleAsync(sourceRoot, InstallProject, Path.Combine(workDir, "install"),
                version, payload, log, ct);
            await SignAsync(sign, installExe, $"{model.Name} 설치", log, ct);

            //4) 최종 파일명
            string setupName = $"{SafeFileName(model.Name)}_Setup_{SafeFileName(model.Version)}.exe";
            string setupPath = Path.Combine(workDir, setupName);
            File.Copy(installExe, setupPath, overwrite: true);

            log($"설치 프로그램: {setupPath} ({new FileInfo(setupPath).Length / 1024.0 / 1024.0:0.0} MB)");
            return new BuildResult(setupPath, launcherExe, uninstallExe);
        }

        private async Task<string> PublishModuleAsync(
            string sourceRoot, string project, string outDir, string? version,
            IReadOnlyDictionary<string, string>? extraProps, Action<string> log, CancellationToken ct)
        {
            string csproj = Path.Combine(sourceRoot, project, project + ".csproj");
            if (!File.Exists(csproj))
                throw new FileNotFoundException($"모듈 프로젝트를 찾을 수 없습니다: {csproj}");

            log($"---------- {project} 게시 ----------");

            var args = new List<string>
            {
                "-p:PublishProfile=FolderProfile",     //단일 파일 설정은 각 모듈의 pubxml 에 있다
                "-p:BH_SignAfterPublish=false",         //VS 게시용 서명 훅은 끄고 메이커가 직접 서명한다
            };
            if (version is not null)
                args.Add($"-p:Version={version}");
            if (extraProps is not null)
                foreach ((string key, string value) in extraProps)
                    args.Add($"-p:{key}={value}");

            int code = await _projects.PublishAsync(csproj, Path.GetDirectoryName(csproj)!, outDir, log, args, ct);
            if (code != 0)
                throw new InvalidOperationException($"{project} 게시에 실패했습니다 (종료 코드 {code}).");

            string exe = Path.Combine(outDir, project + ".exe");
            if (!File.Exists(exe))
                throw new FileNotFoundException($"{project} 게시 결과에 exe 가 없습니다: {exe}");

            string[] others = Directory.GetFiles(outDir)
                .Where(f => !string.Equals(f, exe, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (others.Length > 0)
                log($"경고: {project} 게시 결과가 단일 파일이 아닙니다: {string.Join(", ", others.Select(Path.GetFileName))}");

            //산출물 이름을 {대상 exe 이름}.Launcher.exe 형식으로 바꾼다.
            //설치 프로그램이 설치 폴더에 놓는 이름(ProgramManifest.*FileName)과 같은 규칙이다.
            string newName = project switch
            {
                LauncherProject  => ProgramManifest.Instance.LauncherFileName,
                UninstallProject => ProgramManifest.Instance.UninstallFileName,
                _                => ProgramManifest.Instance.InstallFileName,
            };
            string newExe = Path.Combine(outDir, newName);
            File.Move(exe, newExe, overwrite: true);
            log($"{newName}  {new FileInfo(newExe).Length / 1024} KB");
            return newExe;
        }

        //모듈 exe 서명. 서명은 필수이므로 건너뛰는 경로가 없다.
        private static async Task SignAsync(SignOptions sign, string exe, string description, Action<string> log, CancellationToken ct)
        {
            var options = new SignOptions
            {
                PfxPath = sign.PfxPath,
                PfxPassword = sign.PfxPassword,
                TimestampUrl = sign.TimestampUrl,
                Description = description,
                Force = sign.Force,
            };

            SignReport report = await CodeSigner.SignAsync(new[] { exe }, options, log, ct);
            if (!report.IsSuccess)
                throw new InvalidOperationException($"{Path.GetFileName(exe)} 서명에 실패했습니다.");
        }

        private static bool IsStampableVersion(string? version) =>
            !string.IsNullOrWhiteSpace(version) && Regex.IsMatch(version, @"^\d+(\.\d+){1,3}$");

        //파일 이름에 쓸 수 없는 문자와 공백을 _ 로 바꾼다
        private static string SafeFileName(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            var chars = name.Trim().Select(c => invalid.Contains(c) || char.IsWhiteSpace(c) ? '_' : c).ToArray();
            string result = new string(chars).Trim('_');
            return result.Length == 0 ? "Program" : result;
        }
    }
}