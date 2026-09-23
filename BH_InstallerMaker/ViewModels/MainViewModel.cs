using BH_Install.Core;
using BH_Install.Core.Common;
using BH_Install.Core.Helper;
using BH_Install.Core.Manager;
using BH_InstallerMaker.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows.Controls;
using System.Xml.Linq;

namespace BH_InstallerMaker.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IFileDialogService _dialogs;
        private readonly ProjectService _projects;
        private readonly SettingsService _settings;
        private readonly ModuleBuilder _modules;

        private string? _projectDir;

        //대상 프로그램의 exe 아이콘(.ico) 경로. 런처가 같은 아이콘을 쓴다.
        private string? _iconPath;

        //설정 로드 중에는 변경 알림이 다시 저장을 부르지 않게 한다
        private bool _loadingSettings;

        public MainViewModel(IFileDialogService dialogs, ProjectService projects, SettingsService settings, ModuleBuilder modules)
        {
            _dialogs = dialogs;
            _projects = projects;
            _settings = settings;
            _modules = modules;

            LoadSettings();
        }

        // ===== 배포 소스 =====
        [ObservableProperty]
        private string projectPath = "";

        [ObservableProperty]
        private string projectSummary = "프로젝트를 선택하면 빌드·게시 후 그 출력 전체가 배포 대상이 됩니다.";


        // ===== 프로그램 정보 =====
        [ObservableProperty]
        private string name = "";

        [ObservableProperty]
        private string version = "";

        [ObservableProperty]
        private string publisher = "BH Soft";

        [ObservableProperty]
        private string description = "";

        //런처가 실행할 대상 프로그램의 exe. csproj 의 AssemblyName 에서 자동으로 정해지며 화면에 노출하지 않는다.
        [ObservableProperty]
        private string mainExe = "";

        //런처가 업데이트 목록·파일을 조회하는 서버 주소 (프로그램마다 다름, 설정 파일에 저장)
        [ObservableProperty]
        private string updateUrl = "";

        // ===== 설치 경로 =====
        [ObservableProperty]
        private string rootPath = "";

        [ObservableProperty]
        private string dataRootPath = "";

        [ObservableProperty]
        private string registryKey = "";


        // ===== 프로그램 선택 =====
        //라이선스 DB 의 프로그램 (필수). 배포 소스 카드의 목록에서 고른다.
        [ObservableProperty]
        private BhProgramInfo? selectedProgram;

        //선택 목록 (DB 의 program_num 을 하드코딩한 카탈로그)
        public IReadOnlyList<BhProgramInfo> ProgramOptions => BhProgramCatalog.All;

        // ===== 코드 서명 (필수) =====
        [ObservableProperty]
        private string pfxPath = "";

        //PFX 암호. 설정 파일에 AES 암호화(SecretProtector)해서 저장한다
        [ObservableProperty]
        private string pfxPassword = "";

        [ObservableProperty]
        private string timestampUrl = CodeSigner.DefaultTimestampUrl;

        [ObservableProperty]
        private string certInfoText = "PFX 파일을 선택하고 암호를 입력하면 인증서를 확인합니다.";

        [ObservableProperty]
        private bool isCertValid;

        // ===== 상태 =====
        [ObservableProperty]
        private string statusText = "배포할 프로젝트(.csproj)를 선택하고 정보를 입력하세요.";

        [ObservableProperty]
        private string buildButtonText = "설치 파일 빌드";

        [ObservableProperty]
        private bool isBuilding;

        public ObservableCollection<string> Logs { get; } = new();

        //로그 전체 텍스트 (읽기 전용 TextBox 바인딩용)
        [ObservableProperty]
        private string logText = "";

        private readonly System.Text.StringBuilder _logBuilder = new();

        // ===== 커맨드 =====
        [RelayCommand]
        private void BrowseProject()
        {
            if (_dialogs.OpenFile("배포할 프로젝트 파일 선택", "C# 프로젝트 (*.csproj)|*.csproj") is not { } path)
                return;

            ProjectPath = path;
            _projectDir = Path.GetDirectoryName(path);

            try
            {
                ProjectInfo info = _projects.Read(path);
                _iconPath = info.IconPath;

                ApplyDefaults(info);

                //버전과 메인 실행 파일은 항상 csproj 에서 정한다
                Version = info.Version;
                MainExe = info.MainExeName; 

                string iconNote = info.IconPath is null ? "아이콘: 없음(런처 기본 아이콘)" : $"아이콘: {Path.GetFileName(info.IconPath)}";
                ProjectSummary = $"프로젝트: {info.Stem}  ·  {info.TargetFramework}  ·  메인 파일: {info.MainExeName}  ·  {iconNote}";
                StatusText = "배포할 프로젝트가 준비되었습니다.";
            }
            catch (Exception ex)
            {
                ProjectSummary = $"프로젝트 파일을 읽을 수 없습니다: {ex.Message}";
            }
        }

        [RelayCommand]
        private void BrowsePfx()
        {
            if (_dialogs.OpenFile("코드 서명 인증서 선택", "PFX 인증서 (*.pfx;*.p12)|*.pfx;*.p12|모든 파일 (*.*)|*.*") is { } path)
                PfxPath = path;
        }

        [RelayCommand]
        private void ClearLog()
        {
            Logs.Clear();
            _logBuilder.Clear();
            LogText = "";
        }

        [RelayCommand]
        private async Task BuildAsync()
        {
            if (IsBuilding)
                return;

            var model = CollectModel();
            if (model is null)
                return;

            ProgramManifest.Instance.SaveToModel(model);

            IsBuilding = true;
            BuildButtonText = "빌드 중...";
            StatusText = "빌드를 진행하고 있습니다.";

            try
            {
                await RunBuildAsync(model);
                StatusText = "빌드가 완료되었습니다.";
            }
            catch (Exception ex)
            {
                Log($"오류: {ex.Message}");
                StatusText = "빌드 중 오류가 발생했습니다.";
            }
            finally
            {
                BuildButtonText = "설치 파일 빌드";
                IsBuilding = false;
            }
        }

        // ===== 변경 처리 =====

        //설정 로드 중에는 경로·암호가 연달아 들어오므로 여기서 검사하지 않고 LoadSettings() 끝에서 한 번만 한다.
        partial void OnPfxPathChanged(string value)
        {
            if (_loadingSettings) return;
            ValidatePfx();
            SaveSettings();
        }

        partial void OnPfxPasswordChanged(string value)
        {
            if (_loadingSettings) return;
            ValidatePfx();
            SaveSettings();
        }

        partial void OnTimestampUrlChanged(string value) => SaveSettings();

        //진행 중인 PFX 검사. 경로·암호가 연달아 바뀌면 이전 검사를 취소하고 마지막 요청만 반영한다.
        private CancellationTokenSource? _pfxCheckCts;

        //PFX 와 암호를 검사해 안내 문구를 갱신한다.
        //파일 읽기·키 가져오기는 PFX 가 있는 디스크 상태(HDD 절전 등)에 따라 수 초 걸릴 수 있으므로 UI 스레드에서 하지 않는다.
        //창은 먼저 뜨고 "확인 중" 문구를 보이다가 결과가 나오면 바뀐다.
        private void ValidatePfx()
        {
            _pfxCheckCts?.Cancel();
            CancellationTokenSource cts = _pfxCheckCts = new CancellationTokenSource();

            IsCertValid = false;

            string path = PfxPath;
            string password = PfxPassword;

            if (string.IsNullOrWhiteSpace(path))
            {
                CertInfoText = "PFX 파일을 선택하세요.";
                return;
            }
            if (string.IsNullOrEmpty(password))
            {
                CertInfoText = "PFX 암호를 입력하세요.";
                return;
            }

            CertInfoText = "인증서 확인 중...";
            _ = CheckPfxInBackgroundAsync(path, password, cts);
        }

        private async Task CheckPfxInBackgroundAsync(string path, string password, CancellationTokenSource cts)
        {
            PfxCheckResult result;
            try
            {
                result = await Task.Run(() =>
                {
                    if (!File.Exists(path))
                        return new PfxCheckResult { Message = "PFX 파일을 찾을 수 없습니다." };
                    return CodeSigner.CheckPfx(path, password);
                }, cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                result = new PfxCheckResult { Message = $"PFX 를 확인할 수 없습니다: {ex.Message}" };
            }

            //검사 중에 경로나 암호가 또 바뀌었으면 이 결과는 버린다
            if (cts.IsCancellationRequested)
                return;

            ApplyPfxResult(result);
        }

        private void ApplyPfxResult(PfxCheckResult result)
        {
            if (!result.IsValid)
            {
                CertInfoText = result.Message;
                return;
            }

            if (result.IsExpired)
            {
                CertInfoText = $"만료된 인증서입니다 ({result.NotAfter:yyyy-MM-dd}). 재발급이 필요합니다.";
                return;
            }

            IsCertValid = true;
            CertInfoText = $"{result.SubjectName}  ·  발급: {result.IssuerName}  ·  만료: {result.NotAfter:yyyy-MM-dd} ({result.DaysLeft}일 남음)"
                         + $"\n지문: {result.Thumbprint}";

            if (result.DaysLeft <= 30)
                CertInfoText += "\n만료가 가깝습니다. New-CodeSigningCert.ps1 로 재발급하세요.";
        }

        // ===== 설정 저장/로드 =====

        private void LoadSettings()
        {
            _loadingSettings = true;
            try
            {
                MakerSettings s = _settings.Load();
                PfxPath = s.PfxPath ?? "";
                PfxPassword = SecretProtector.Unprotect(s.PfxPasswordEnc ?? "");
                TimestampUrl = string.IsNullOrWhiteSpace(s.TimestampUrl) ? CodeSigner.DefaultTimestampUrl : s.TimestampUrl;
            }
            finally
            {
                _loadingSettings = false;
            }

            //로드한 경로·암호로 한 번만 검사한다 (백그라운드)
            ValidatePfx();
        }

        private void SaveSettings()
        {
            if (_loadingSettings)
                return;

            _settings.Save(new MakerSettings
            {
                PfxPath = PfxPath,
                PfxPasswordEnc = SecretProtector.Protect(PfxPassword),
                TimestampUrl = TimestampUrl,
            });
        }

        //선택한 프로젝트의 기본값
        private void ApplyDefaults(ProjectInfo info)
        {
            Name = ToDisplayName(info.Stem);
            Description = "";
            MainExe = info.MainExeName;

            // 기본 경로: <루트>\{제작자}\{이름}
            RootPath = $@"C:\Program Files\{Publisher}\{Name}";
            DataRootPath = $@"C:\ProgramData\{Publisher}\{Name}";
            RegistryKey = $@"SOFTWARE\{Publisher}\{Name}";

            //업데이트 서버 주소: 대상 저장소의 docs\CNAME(GitHub Pages 도메인)이 있으면 https://{도메인}/ 로 채운다
            UpdateUrl = ResolveUpdateUrlFromCname(_projectDir);
            SelectedProgram = null;
        }

        //대상 프로젝트 상위의 .sln 폴더에서 docs\CNAME 을 읽어 업데이트 서버 주소를 만든다. 없으면 빈 값.
        //PublishToDocsAsync 가 산출물을 올리는 곳이 그 docs(GitHub Pages)이므로 런처는 https://{CNAME}/ 에서 내려받는다.
        private static string ResolveUpdateUrlFromCname(string? projectDir)
        {
            if (string.IsNullOrEmpty(projectDir))
                return string.Empty;

            string? slnDir = FindSolutionDir(projectDir);
            if (slnDir is null)
                return string.Empty;

            string cnamePath = Path.Combine(slnDir, "docs", "CNAME");
            if (!File.Exists(cnamePath))
                return string.Empty;

            string host = File.ReadAllText(cnamePath).Trim();
            if (host.Length == 0)
                return string.Empty;

            //CNAME 에는 보통 호스트 이름만 있다. 스킴이 이미 있으면 그대로 둔다.
            string url = host.Contains("://") ? host : $"https://{host}";
            return url.EndsWith('/') ? url : url + "/";
        }


        // ===== 내부 로직 =====

        //프로젝트명 → 표시 이름 변환
        //  _ 는 띄어쓰기로, 소문자/숫자 뒤의 대문자 앞에는 띄어쓰기 삽입 (중복 공백 방지)
        //  예: BH_VpnBrowser → BH Vpn Browser
        private static string ToDisplayName(string stem)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < stem.Length; i++)
            {
                char c = stem[i];
                if (c == '_')
                {
                    if (sb.Length > 0 && sb[^1] != ' ')
                        sb.Append(' ');
                    continue;
                }
                if (char.IsUpper(c) && i > 0
                    && (char.IsLower(stem[i - 1]) || char.IsDigit(stem[i - 1]))
                    && sb.Length > 0 && sb[^1] != ' ')
                {
                    sb.Append(' ');
                }
                sb.Append(c);
            }
            return sb.ToString().Trim();
        }

        private void Log(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}]  {message}";
            Logs.Add(line);
            _logBuilder.AppendLine(line);
            LogText = _logBuilder.ToString();
            Console.WriteLine(line);
        }

        //[설치 파일 빌드] 파이프라인
        //  대상 프로그램 : 1) 게시 → 2) pdb·xml 정리 → 3) 산출물 집계 → 4) 코드 서명 → 5) update.json·index.json → 6) 파일별 압축
        //  배포 저장소   : 7) sln 옆 docs 폴더 → 8) git pull → 9) docs 비우기(보존 파일 제외) → 10) 산출물 이동 → 11) git 커밋·푸시
        //  설치 프로그램 : 런처·언인스톨·설치 모듈 게시·서명 → Setup exe
        private async Task RunBuildAsync(ProgramModel model)
        {
            Log("========== 빌드 시작 ==========");
            Log($"프로그램: {model.Name} v{model.Version} ({model.Publisher})");
            Log($"메인 실행 파일: {model.MainExe}");

            string projectDir = _projectDir ?? throw new InvalidOperationException("배포할 프로젝트가 선택되지 않았습니다.");

            var sign = new SignOptions
            {
                PfxPath = PfxPath,
                PfxPassword = PfxPassword,
                TimestampUrl = TimestampUrl.Trim(),
                Description = model.Name,
            };

            // ----- 대상 프로그램 -----
            string outDir = ResolvePublishDir(projectDir);
            await PublishTargetAsync(outDir);
            RemoveDebugArtifacts(outDir);
            double totalMb = LogPublishSummary(outDir, model.MainExe);
            await SignOutputAsync(outDir, sign);
            Dictionary<string, string> updateFiles = await WriteUpdateManifestAsync(outDir, model.Version, totalMb);
            await ZipUpdateFilesAsync(outDir, updateFiles);

            // ----- 배포 저장소 (docs) -----
            string slnDir = await PrePublishToDocsAsync(outDir, projectDir);

            //BH_Install, BH_Launcher, BH_Uninstall 3가지의 프로젝트를 빌드하고,
            //BH_install 의 최종 결과물을 docs 폴더의 install.exe 로 복사 한다
            string setupPath = await BuildInstallerAsync(model, sign);

            Log("========== 빌드 완료 ==========");

            await PublishToDocsAsync(slnDir);

            //결과 폴더를 탐색기로 열고 설치 파일을 선택해 둔다
            OpenInExplorer(setupPath);
        }

        // ===== 대상 프로그램 =====

        //게시 출력 폴더. 대상 프로젝트의 Properties\PublishProfiles\FolderProfile.pubxml 에 적힌 PublishDir 을 쓴다.
        //이전 산출물이 남아 있으면 비우고 시작한다.
        private string ResolvePublishDir(string projectDir)
        {
            string pubxml = Path.Combine(projectDir, "Properties", "PublishProfiles", "FolderProfile.pubxml");
            if (!File.Exists(pubxml))
                throw new InvalidOperationException($"게시 프로필이 없습니다: {pubxml}");

            XDocument doc = XDocument.Load(pubxml);
            XNamespace ns = doc.Root?.Name.Namespace ?? XNamespace.None;
            string? publishDir = doc.Root?
                .Elements(ns + "PropertyGroup")
                .Elements(ns + "PublishDir")
                .FirstOrDefault()?.Value;
            if (string.IsNullOrWhiteSpace(publishDir))
                throw new InvalidOperationException($"게시 프로필에 PublishDir 이 없습니다: {pubxml}");

            string outDir = Path.GetFullPath(Path.Combine(projectDir, publishDir));
            if (Directory.Exists(outDir))
            {
                Log("이전 게시 출력 정리 중...");
                Directory.Delete(outDir, recursive: true);
            }
            return outDir;
        }

        //1) dotnet publish
        private async Task PublishTargetAsync(string outDir)
        {
            Log("게시 실행: dotnet publish -c Release");
            int exitCode = await _projects.PublishAsync(ProjectPath, _projectDir!, outDir, Log);
            if (exitCode != 0)
            {
                Log($"게시 실패 (종료 코드 {exitCode})");
                throw new InvalidOperationException("프로젝트 게시(dotnet publish)에 실패했습니다.");
            }
            Log("게시 성공");
        }

        //2) 배포에 필요 없는 pdb 와, 짝이 되는 dll 이 있는 xml 문서 파일을 지운다
        private void RemoveDebugArtifacts(string outDir)
        {
            Log("산출물 정리: pdb·xml 문서 제거");

            int removed = 0;
            foreach (string file in Directory.GetFiles(outDir, "*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                bool isXmlDoc = ext == ".xml" && File.Exists(Path.ChangeExtension(file, ".dll"));
                if (ext != ".pdb" && !isXmlDoc)
                    continue;

                File.Delete(file);
                removed++;
            }

            Log($"산출물 정리 완료: {removed}개 삭제");
        }

        //3) 산출물 요약을 로그에 남기고 총 크기(MB)를 돌려준다
        private double LogPublishSummary(string outDir, string mainExe)
        {
            string[] files = Directory.GetFiles(outDir, "*", SearchOption.AllDirectories);
            double totalMb = files.Sum(f => new FileInfo(f).Length) / 1024.0 / 1024.0;

            Log($"산출물 위치: {outDir}");
            Log($"산출물 파일 {files.Length}개, 총 {totalMb:0.0} MB");

            if (File.Exists(Path.Combine(outDir, mainExe)))
                Log($"메인 실행 파일 확인: {mainExe}");
            else
                Log($"경고: 산출물에서 메인 실행 파일({mainExe})을 찾을 수 없습니다.");

            return totalMb;
        }

        //4) 게시 산출물의 exe/dll 에 서명한다. 실패한 파일이 있으면 빌드를 오류로 끝낸다.
        private async Task SignOutputAsync(string outDir, SignOptions sign)
        {
            Log("========== 코드 서명 ==========");

            SignReport report = await CodeSigner.SignAsync(new[] { outDir }, sign, Log);

            if (!report.IsSuccess)
                throw new InvalidOperationException($"코드 서명에 실패한 파일이 {report.Failed.Count}개 있습니다. 산출물을 배포하지 마세요.");
        }

        //5) 업데이트 목록. update.json = { 상대 경로: SHA-256 }, index.json = { ver, vol }
        //   서명이 끝난 뒤에 해시를 구해야 런처가 받은 파일과 값이 맞는다. 목록에는 이 두 파일 자체는 들어가지 않는다.
        private async Task<Dictionary<string, string>> WriteUpdateManifestAsync(string outDir, string version, double totalMb)
        {
            Log("업데이트 목록 생성 중...");

            Dictionary<string, string> updateFiles = Directory
                .GetFiles(outDir, "*", SearchOption.AllDirectories)
                .ToDictionary(f => Path.GetRelativePath(outDir, f), f => f.ToHashEx());

            await File.WriteAllTextAsync(Path.Combine(outDir, "update.json"), JsonConvert.SerializeObject(updateFiles));
            await File.WriteAllTextAsync(Path.Combine(outDir, "index.json"), JsonConvert.SerializeObject(new { ver = version, vol = $"{totalMb:0.0}MB" }));

            Log($"업데이트 목록 생성 완료: {updateFiles.Count}개");
            return updateFiles;
        }

        //6) 파일별 압축. 원본은 지우고 {상대 경로}.zip 만 남긴다 (런처가 파일 단위로 내려받는다)
        private async Task ZipUpdateFilesAsync(string outDir, Dictionary<string, string> updateFiles)
        {
            Log("업데이트 압축 파일 생성 중...");

            foreach (string relativePath in updateFiles.Keys)
            {
                string source = Path.Combine(outDir, relativePath);
                string zipPath = source + ".zip";

                await Task.Run(() =>
                {
                    using ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
                    archive.CreateEntryFromFile(source, Path.GetFileName(source), CompressionLevel.Optimal);
                });
                File.Delete(source);
            }

            Log($"업데이트 압축 파일 생성 완료: {updateFiles.Count}개");
        }

        // ===== 배포 저장소 (docs) =====

        //docs 를 비울 때 남겨 두는 파일 (GitHub Pages 설정·정적 페이지)
        private static readonly HashSet<string> DocsKeepFiles = new(StringComparer.OrdinalIgnoreCase)
        {
            "CNAME",
            "docs.msbuildproj",
            "index.html",
        };

        //7)~11) 대상 프로젝트 저장소의 docs 폴더(GitHub Pages)에 산출물을 올릴 준비를 한다
        private async Task<string> PrePublishToDocsAsync(string outDir, string projectDir)
        {
            Log("========== 배포 저장소 반영 ==========");

            //7) 대상 프로젝트 상위에서 .sln 이 있는 폴더가 저장소 루트, 그 아래 docs 가 배포 폴더
            string slnDir = FindSolutionDir(projectDir)
                ?? throw new InvalidOperationException("대상 프로젝트 상위에서 .sln 을 찾을 수 없어 배포 폴더(docs)를 정할 수 없습니다.");
            if (!Directory.Exists(Path.Combine(slnDir, ".git")))
                throw new InvalidOperationException($"Git 저장소가 아닙니다: {slnDir}");

            string docsDir = Path.Combine(slnDir, "docs");
            Directory.CreateDirectory(docsDir);
            Log($"배포 폴더: {docsDir}");

            //8) main 을 최신으로
            await RunGitAsync(slnDir, "checkout main");
            await RunGitAsync(slnDir, "pull origin main");

            //9) 보존 파일 외 전부 삭제
            ClearDirectory(docsDir, DocsKeepFiles);
            Log("docs 폴더 정리 완료");

            //10) 산출물 이동
            MoveDirectoryContents(outDir, docsDir);
            Log("산출물 이동 완료");
            return slnDir;
        }

        private async Task PublishToDocsAsync(string slnDir)
        {

            //11) docs 만 스테이징해 커밋·푸시. 대상 저장소에 있을지 모르는 다른 변경은 건드리지 않는다.
            await RunGitAsync(slnDir, "add -A docs");

            string message = $"{DateTime.Now:yyMMdd_HHmmss}_인스톨러 자동커밋";
            int commitCode = await RunGitAsync(slnDir, $"commit -m \"{message}\"", throwOnError: false);
            if (commitCode != 0)
            {
                Log("커밋할 변경이 없어 푸시를 건너뜁니다.");
                return;
            }

            await RunGitAsync(slnDir, "push origin main");
            Log($"푸시 완료: {message}");
        }

        //dir 에서 위로 올라가며 .sln 이 있는 폴더를 찾는다. 없으면 null.
        private static string? FindSolutionDir(string dir)
        {
            for (DirectoryInfo? current = new(dir); current is not null; current = current.Parent)
            {
                if (current.EnumerateFiles("*.sln").Any())
                    return current.FullName;
            }
            return null;
        }

        //dir 바로 아래의 파일(keepFiles 제외)과 하위 폴더를 모두 지운다
        private static void ClearDirectory(string dir, ISet<string> keepFiles)
        {
            foreach (string file in Directory.GetFiles(dir))
            {
                if (!keepFiles.Contains(Path.GetFileName(file)))
                    File.Delete(file);
            }
            foreach (string sub in Directory.GetDirectories(dir))
                Directory.Delete(sub, recursive: true);
        }

        //source 아래 파일을 상대 경로를 유지한 채 target 으로 옮긴다
        private static void MoveDirectoryContents(string source, string target)
        {
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string dest = Path.Combine(target, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Move(file, dest, overwrite: true);
            }
        }

        //git 명령을 실행하고 출력을 로그로 흘린다. 종료 코드를 돌려주며, throwOnError 면 0 이 아닐 때 예외를 낸다.
        //출력 이벤트는 작업 스레드에서 오므로 UI 스레드로 넘겨 기록한다.
        private async Task<int> RunGitAsync(string workingDir, string arguments, bool throwOnError = true)
        {
            Log($"[git] {arguments}");

            SynchronizationContext? context = SynchronizationContext.Current;
            void Post(string text)
            {
                if (context is null) Log(text);
                else context.Post(_ => Log(text), null);
            }

            var psi = new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            using var process = new Process { StartInfo = psi };
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) Post($"[git] {e.Data}"); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) Post($"[git] {e.Data}"); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0 && throwOnError)
                throw new InvalidOperationException($"git {arguments} 실패 (종료 코드 {process.ExitCode})");

            return process.ExitCode;
        }

        // ===== 설치 프로그램 =====

        private void OpenInExplorer(string filePath)
        {
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{filePath}\"") { UseShellExecute = false });
            }
            catch (Exception ex)
            {
                Log($"결과 폴더를 열지 못했습니다: {ex.Message}");
            }
        }

        //런처·언인스톨·설치 모듈을 게시·서명해 Setup exe 를 만든다.
        private async Task<string> BuildInstallerAsync(ProgramModel model, SignOptions sign)
        {
            //런처, 언인스톨, 인스톨 순서로 게시 후 서명 절차를 반복한다.
            //인스톨은 런처와 언인스톨게시 결과를 포함한다. 
            Log("========== 설치 프로그램 생성 ==========");

            string? sourceRoot = ModuleBuilder.FindModuleSourceRoot();
            if (sourceRoot is null)
                throw new InvalidOperationException(
                    $"메이커 실행 위치의 상위 폴더에서 {ModuleBuilder.SolutionFileName} 을 찾을 수 없습니다. 메이커는 이 저장소 안에서 실행해야 합니다.");
            Log($"모듈 소스: {sourceRoot}");

            //산출물: <프로젝트>\bin\bh_installer
            string workDir = Path.Combine(_projectDir!, "bin", "bh_installer");

            ModuleBuilder.BuildResult result = await _modules.BuildAsync(model, workDir, sourceRoot, sign, Log);
            Log($"설치 프로그램 생성 완료: {result.SetupPath}");
            return result.SetupPath;
        }

        //화면 값으로 모델을 만든다 (검증 없음). CollectModel() 이 검증 뒤에 호출한다.
        private ProgramModel BuildModel() => new()
        {
            Name = Name.Trim(),
            Publisher = Publisher.Trim(),
            Version = Version.Trim(),
            Description = Description.Trim(),
            RootPath = RootPath.Trim(),
            DataRootPath = DataRootPath.Trim(),
            RegistryKey = RegistryKey.Trim(),
            MainExe = MainExe.Trim(),
            UpdateUrl = UpdateUrl.Trim(),
            MainIcon = _iconPath ?? "",
            ProgramId = SelectedProgram?.Number ?? 0,
        };

        //빌드 전 검증. 문제가 있으면 상태 문구를 바꾸고 null.
        private ProgramModel? CollectModel()
        {
            if (string.IsNullOrWhiteSpace(ProjectPath))
            {
                StatusText = "배포할 프로젝트(.csproj)를 먼저 선택하세요.";
                return null;
            }
            if (string.IsNullOrWhiteSpace(Name))
            {
                StatusText = "프로그램 이름을 입력하세요.";
                return null;
            }
            if (string.IsNullOrWhiteSpace(MainExe))
            {
                StatusText = "프로젝트에서 실행 파일 이름을 읽지 못했습니다. csproj 를 다시 선택하세요.";
                return null;
            }
            if (string.IsNullOrWhiteSpace(RootPath))
            {
                StatusText = "프로그램 설치 경로를 입력하세요.";
                return null;
            }
            if (!string.IsNullOrWhiteSpace(RegistryKey) && RegistryKey.Trim('\\').Split('\\').Length < 3)
            {
                StatusText = @"레지스트리 키는 SOFTWARE\제작자\프로그램 형식이어야 합니다.";
                return null;
            }

            if (SelectedProgram is null)
            {
                StatusText = "배포 소스에서 프로그램(라이선스 DB)을 선택하세요.";
                return null;
            }
            if (!Uri.TryCreate(UpdateUrl.Trim(), UriKind.Absolute, out Uri? updateUri)
                || (updateUri.Scheme != Uri.UriSchemeHttp && updateUri.Scheme != Uri.UriSchemeHttps))
            {
                StatusText = "업데이트 서버 주소를 http:// 또는 https:// 로 시작하는 전체 주소로 입력하세요.";
                return null;
            }
            if (!IsCertValid)
            {
                StatusText = "코드 서명은 필수입니다. " + CertInfoText.Split('\n')[0];
                return null;
            }

            return BuildModel();
        }
    }
}