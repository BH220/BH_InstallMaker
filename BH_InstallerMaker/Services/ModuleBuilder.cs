using System.IO;
using System.Text.RegularExpressions;
using BH_Install.Core;

namespace BH_InstallerMaker.Services
{
    //설치 프로그램 산출물을 만든다.
    //  런처 게시·서명 → 언인스톨 게시·서명 → (둘을 페이로드로 넣어) 설치 게시·서명 → Setup exe
    //
    //세 모듈은 이 저장소의 프로젝트를 dotnet publish 로 다시 빌드한다.
    //매니페스트(program.json)는 -p:BH_ProgramJson 으로 넘겨 각 모듈의 임베디드 리소스가 되고(BH.Publish.targets),
    //런처·언인스톨 exe 는 -p:BH_LauncherExe / -p:BH_UninstallExe 로 설치 프로그램에 임베드된다(BH_Install.csproj).
    public sealed class ModuleBuilder
    {
        public const string SolutionFileName = "BH_InstallerMaker.sln";

        private const string LauncherProject = "BH_Launcher";
        private const string UninstallProject = "BH_Uninstall";
        private const string InstallProject = "BH_Install";

        private readonly ProjectService _projects;

        public ModuleBuilder(ProjectService projects) => _projects = projects;

        public sealed record BuildResult(string SetupPath, string LauncherPath, string UninstallerPath, string ManifestPath);

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

        public async Task<BuildResult> BuildAsync(
            ProgramModel model, string workDir, string sourceRoot, SignOptions sign,
            Action<string> log, string? launcherIconPath = null, CancellationToken ct = default)
        {
            //작업 폴더를 비운 상태로 시작
            if (Directory.Exists(workDir))
                Directory.Delete(workDir, recursive: true);
            Directory.CreateDirectory(workDir);

            //매니페스트
            string manifestPath = Path.Combine(workDir, "program.json");
            File.WriteAllText(manifestPath, ProgramManifest.ToJson(model));
            log($"매니페스트: {manifestPath}");

            //모듈 exe 의 파일 버전에 프로그램 버전을 찍는다 (숫자.숫자 형식일 때만)
            string? version = IsStampableVersion(model.Version) ? model.Version : null;
            if (version is null)
                log($"버전 '{model.Version}' 은 exe 버전 정보로 쓸 수 없는 형식이라 모듈 버전 표기를 건너뜁니다.");

            //1) 런처 (대상 프로그램의 exe 아이콘을 그대로 쓴다)
            Dictionary<string, string>? launcherProps = null;
            if (!string.IsNullOrWhiteSpace(launcherIconPath) && File.Exists(launcherIconPath))
            {
                launcherProps = new Dictionary<string, string> { ["BH_LauncherIcon"] = launcherIconPath };
                log($"런처 아이콘: {launcherIconPath}");
            }
            else
            {
                log("대상 프로젝트에 ApplicationIcon 이 없어 런처는 기본 아이콘을 씁니다.");
            }
            string launcherExe = await PublishModuleAsync(model, sourceRoot, LauncherProject, Path.Combine(workDir, "launcher"),
                manifestPath, version, launcherProps, log, ct);
            await SignAsync(sign, launcherExe, $"{model.Name} 런처", log, ct);

            //2) 언인스톨
            string uninstallExe = await PublishModuleAsync(model, sourceRoot, UninstallProject, Path.Combine(workDir, "uninstall"),
                manifestPath, version, null, log, ct);
            await SignAsync(sign, uninstallExe, $"{model.Name} 제거", log, ct);

            //3) 설치 (서명된 런처·언인스톨을 페이로드로 포함)
            var payload = new Dictionary<string, string>
            {
                ["BH_LauncherExe"] = launcherExe,
                ["BH_UninstallExe"] = uninstallExe,
            };
            string installExe = await PublishModuleAsync(model, sourceRoot, InstallProject, Path.Combine(workDir, "install"),
                manifestPath, version, payload, log, ct);
            await SignAsync(sign, installExe, $"{model.Name} 설치", log, ct);

            //4) 최종 파일명
            string setupName = $"{SafeFileName(model.Name)}_Setup_{SafeFileName(model.Version)}.exe";
            string setupPath = Path.Combine(workDir, setupName);
            File.Copy(installExe, setupPath, overwrite: true);

            log($"설치 프로그램: {setupPath} ({new FileInfo(setupPath).Length / 1024.0 / 1024.0:0.0} MB)");
            return new BuildResult(setupPath, launcherExe, uninstallExe, manifestPath);
        }

        private async Task<string> PublishModuleAsync(ProgramModel model,
            string sourceRoot, string project, string outDir, string manifestPath, string? version,
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
                $"-p:BH_ProgramJson={manifestPath}",
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
                LauncherProject  => ProgramManifest.LauncherFileName(model),
                UninstallProject => ProgramManifest.UninstallFileName(model),
                _                => ProgramManifest.InstallFileName(model),
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