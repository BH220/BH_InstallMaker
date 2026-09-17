using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using BH_Install.Core;
using BH_InstallerMaker.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BH_InstallerMaker.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        //값이 바뀌면 대상 프로젝트 설정 파일(<프로젝트>.bhinstaller.json)에 저장하는 속성들
        private static readonly HashSet<string> ProjectFields = new()
        {
            nameof(Name), nameof(Version), nameof(Publisher), nameof(Description),
            nameof(RootPath), nameof(DataRootPath), nameof(RegistryKey),
            nameof(MainExe), nameof(UpdateUrl), nameof(SelectedProgram),
        };

        private readonly IFileDialogService _dialogs;
        private readonly ProjectService _projects;
        private readonly SettingsService _settings;
        private readonly ModuleBuilder _modules;

        private string? _projectDir;

        //대상 프로그램의 exe 아이콘(.ico) 경로. 런처가 같은 아이콘을 쓴다.
        private string? _iconPath;

        //로드 중에는 변경 알림이 다시 저장을 부르지 않게 한다
        private bool _loadingSettings;
        private bool _loadingProject;

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

                _loadingProject = true;
                try
                {
                     ApplyDefaults(info);

                    //버전과 메인 실행 파일은 항상 csproj 에서 정한다
                    Version = info.Version;
                    MainExe = info.MainExeName;
                }
                finally
                {
                    _loadingProject = false;
                } 
                 
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

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e); 
        }

        partial void OnPfxPathChanged(string value)
        {
            ValidatePfx();
            SaveSettings();
        }

        partial void OnPfxPasswordChanged(string value)
        {
            ValidatePfx();
            SaveSettings();
        }

        partial void OnTimestampUrlChanged(string value) => SaveSettings();

        //PFX 와 암호를 즉시 검사해 안내 문구를 갱신한다
        private void ValidatePfx()
        {
            IsCertValid = false;

            if (string.IsNullOrWhiteSpace(PfxPath))
            {
                CertInfoText = "PFX 파일을 선택하세요.";
                return;
            }
            if (!File.Exists(PfxPath))
            {
                CertInfoText = "PFX 파일을 찾을 수 없습니다.";
                return;
            }
            if (string.IsNullOrEmpty(PfxPassword))
            {
                CertInfoText = "PFX 암호를 입력하세요.";
                return;
            }

            PfxCheckResult result = CodeSigner.CheckPfx(PfxPath, PfxPassword);
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

        //저장된 설정을 화면에 적용
        private void ApplyModel(ProgramModel m)
        {
            Name = m.Name;
            Publisher = string.IsNullOrWhiteSpace(m.Publisher) ? "BH Soft" : m.Publisher;
            Description = m.Description;
            MainExe = m.MainExe;
            RootPath = m.RootPath;
            DataRootPath = m.DataRootPath;
            RegistryKey = m.RegistryKey;
            UpdateUrl = m.UpdateUrl;
            SelectedProgram = BhProgramCatalog.Find((int)m.ProgramId);
        }

        //처음 선택한 프로젝트의 기본값
        private void ApplyDefaults(ProjectInfo info)
        {
            Name = ToDisplayName(info.Stem);
            Description = "";
            MainExe = info.MainExeName;

            // 기본 경로: <루트>\{제작자}\{이름}
            RootPath = $@"C:\Program Files\{Publisher}\{Name}";
            DataRootPath = $@"C:\ProgramData\{Publisher}\{Name}";
            RegistryKey = $@"SOFTWARE\{Publisher}\{Name}";

            UpdateUrl = "";
            SelectedProgram = null;
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
        }

        //1) 대상 프로그램 게시 → 2) 서명 → 3) 설치 프로그램(런처·언인스톨 포함) 생성
        private async Task RunBuildAsync(ProgramModel model)
        {
            Log("========== 빌드 시작 ==========");
            Log($"프로그램: {model.Name} v{model.Version} ({model.Publisher})");
            Log($"메인 실행 파일: {model.MainExe}");

            //산출물 출력 폴더: <프로젝트>\bin\bh_publish
            string outDir = Path.Combine(_projectDir!, "bin", "bh_publish");
            if (Directory.Exists(outDir))
            {
                Log("이전 게시 출력 정리 중...");
                Directory.Delete(outDir, true);
            }

            Log("게시 실행: dotnet publish -c Release");
            int exitCode = await _projects.PublishAsync(ProjectPath, _projectDir!, outDir, Log);
            if (exitCode != 0)
            {
                Log($"게시 실패 (종료 코드 {exitCode})");
                throw new InvalidOperationException("프로젝트 게시(dotnet publish)에 실패했습니다.");
            }
            Log("게시 성공");

            //산출물 확인
            var files = Directory.GetFiles(outDir, "*", SearchOption.AllDirectories);
            double totalMb = files.Sum(f => new FileInfo(f).Length) / 1024.0 / 1024.0;
            Log($"산출물 위치: {outDir}");
            Log($"산출물 파일 {files.Length}개, 총 {totalMb:0.0} MB");

            string mainExePath = Path.Combine(outDir, model.MainExe);
            if (File.Exists(mainExePath))
                Log($"메인 실행 파일 확인: {model.MainExe}");
            else
                Log($"경고: 산출물에서 메인 실행 파일({model.MainExe})을 찾을 수 없습니다.");

            //코드 서명 (필수)
            var sign = new SignOptions
            {
                PfxPath = PfxPath,
                PfxPassword = PfxPassword,
                TimestampUrl = TimestampUrl.Trim(),
                Description = model.Name,
            };
            await SignOutputAsync(outDir, sign);

            //설치 프로그램 생성
            string setupPath = await BuildInstallerAsync(model, sign);

            Log("========== 빌드 완료 (다음 단계: 업데이트 리스트 생성·압축·업로드 예정) ==========");

            //결과 폴더를 탐색기로 열고 설치 파일을 선택해 둔다
            OpenInExplorer(setupPath);
        }

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

        //게시 산출물의 exe/dll 에 서명한다. 실패한 파일이 있으면 빌드를 오류로 끝낸다.
        private async Task SignOutputAsync(string outDir, SignOptions sign)
        {
            Log("========== 코드 서명 ==========");

            SignReport report = await CodeSigner.SignAsync(new[] { outDir }, sign, Log);

            if (!report.IsSuccess)
                throw new InvalidOperationException($"코드 서명에 실패한 파일이 {report.Failed.Count}개 있습니다. 산출물을 배포하지 마세요.");
        }

        //런처·언인스톨·설치 모듈을 매니페스트와 함께 게시·서명해 Setup exe 를 만든다.
        private async Task<string> BuildInstallerAsync(ProgramModel model, SignOptions sign)
        {
            Log("========== 설치 프로그램 생성 ==========");

            string? sourceRoot = ModuleBuilder.FindModuleSourceRoot();
            if (sourceRoot is null)
                throw new InvalidOperationException(
                    $"메이커 실행 위치의 상위 폴더에서 {ModuleBuilder.SolutionFileName} 을 찾을 수 없습니다. 메이커는 이 저장소 안에서 실행해야 합니다.");
            Log($"모듈 소스: {sourceRoot}");

            //산출물: <프로젝트>\bin\bh_installer
            string workDir = Path.Combine(_projectDir!, "bin", "bh_installer");

            ModuleBuilder.BuildResult result = await _modules.BuildAsync(model, workDir, sourceRoot, sign, Log, _iconPath);
            Log($"설치 프로그램 생성 완료: {result.SetupPath}");
            return result.SetupPath;
        }

        //화면 값으로 모델을 만든다 (검증 없음). 설정 파일 저장용.
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
            ProgramId = SelectedProgram != null ? 0 : 0,
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