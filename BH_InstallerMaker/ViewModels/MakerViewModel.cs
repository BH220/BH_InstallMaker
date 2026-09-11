using System.Collections.ObjectModel;
using System.IO;
using BH_Install.Core;
using BH_InstallerMaker.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BH_InstallerMaker.ViewModels
{
    public partial class MakerViewModel : ObservableObject
    {
        private readonly IFileDialogService _dialogs;
        private readonly ProjectService _projects;
        private readonly SettingsService _settings;

        private string? _projectDir;
        private string? _mainExeName;

        //설정 로드 중에는 변경 알림이 다시 저장을 부르지 않게 한다
        private bool _loadingSettings;

        public MakerViewModel(IFileDialogService dialogs, ProjectService projects, SettingsService settings)
        {
            _dialogs = dialogs;
            _projects = projects;
            _settings = settings;

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

        // ===== 설치 경로 =====
        [ObservableProperty]
        private string rootPath = "";

        [ObservableProperty]
        private string dataRootPath = "";

        [ObservableProperty]
        private string registryKey = "";

        // ===== 옵션 =====
        [ObservableProperty]
        private bool useWindowsService;

        [ObservableProperty]
        private string windowsServiceName = "";

        [ObservableProperty]
        private string windowsServiceDescription = "";

        [ObservableProperty]
        private bool useLicense;

        [ObservableProperty]
        private string programIdText = "";

        // ===== 코드 서명 =====
        //게시 결과물(exe/dll)에 Authenticode 서명을 붙인다
        [ObservableProperty]
        private bool useCodeSigning;

        //코드 서명 인증서 PFX 경로 (설정에 저장)
        [ObservableProperty]
        private string pfxPath = "";

        //PFX 암호. 실행 중 메모리에만 두고 어디에도 저장하지 않는다
        [ObservableProperty]
        private string pfxPassword = "";

        //RFC 3161 타임스탬프 서버 (설정에 저장)
        [ObservableProperty]
        private string timestampUrl = CodeSigner.DefaultTimestampUrl;

        //PFX 검사 결과 안내 문구
        [ObservableProperty]
        private string certInfoText = "PFX 파일을 선택하고 암호를 입력하면 인증서를 확인합니다.";

        //PFX 와 암호가 맞고 인증서가 만료되지 않았다
        [ObservableProperty]
        private bool isCertValid;

        private PfxCheckResult? _pfxCheck;

        // ===== 상태 =====
        [ObservableProperty]
        private string statusText = "배포할 프로젝트(.csproj)를 선택하고 정보를 입력하세요.";

        [ObservableProperty]
        private string buildButtonText = "설치 파일 빌드";

        public ObservableCollection<string> Logs { get; } = new();

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
                var info = _projects.Read(path);
                Name = ToDisplayName(info.Stem);
                Version = info.Version;
                _mainExeName = info.MainExeName;

                // 기본 경로: <루트>\{제작자}\{이름}
                RootPath = $@"C:\Program Files\{Publisher}\{Name}";
                DataRootPath = $@"C:\ProgramData\{Publisher}\{Name}";
                RegistryKey = $@"SOFTWARE\{Publisher}\{Name}";

                ProjectSummary = $"프로젝트: {info.Stem}  ·  {info.TargetFramework}  ·  메인 파일: {info.MainExeName}";
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
        private void ClearLog() => Logs.Clear();

        [RelayCommand]
        private async Task BuildAsync()
        {
            var model = CollectModel();
            if (model is null)
                return;

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
            }
        }

        // ===== 코드 서명 설정 변경 처리 =====

        partial void OnUseCodeSigningChanged(bool value) => SaveSettings();

        partial void OnPfxPathChanged(string value)
        {
            ValidatePfx();
            SaveSettings();
        }

        partial void OnPfxPasswordChanged(string value) => ValidatePfx();

        partial void OnTimestampUrlChanged(string value) => SaveSettings();

        //PFX 와 암호를 즉시 검사해 안내 문구를 갱신한다
        private void ValidatePfx()
        {
            _pfxCheck = null;
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

            _pfxCheck = result;

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

        private void LoadSettings()
        {
            _loadingSettings = true;
            try
            {
                MakerSettings s = _settings.Load();
                UseCodeSigning = s.UseCodeSigning;
                PfxPath = s.PfxPath ?? "";
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
                UseCodeSigning = UseCodeSigning,
                PfxPath = PfxPath,
                TimestampUrl = TimestampUrl,
            });
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

        private void Log(string message) => Logs.Add($"[{DateTime.Now:HH:mm:ss}]  {message}");

        //선택된 프로젝트를 dotnet publish로 빌드하고 산출물 위치를 확인한 뒤, 옵션에 따라 서명한다
        private async Task RunBuildAsync(ProgramModel model)
        {
            Log("========== 빌드 시작 ==========");
            Log($"프로그램: {model.Name} v{model.Version} ({model.Publisher})");
            Log($"메인 실행 파일: {_mainExeName}");

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

            string mainExe = Path.Combine(outDir, _mainExeName ?? "");
            if (File.Exists(mainExe))
                Log($"메인 실행 파일 확인: {_mainExeName}");
            else
                Log($"경고: 산출물에서 메인 실행 파일({_mainExeName})을 찾을 수 없습니다.");

            //코드 서명
            if (UseCodeSigning)
                await SignOutputAsync(outDir, model);
            else
                Log("코드 서명: 사용 안 함");

            Log("========== 빌드 완료 (다음 단계: 압축/패키징 예정) ==========");
        }

        //게시 산출물의 exe/dll 에 서명한다. 실패한 파일이 있으면 빌드를 오류로 끝낸다.
        private async Task SignOutputAsync(string outDir, ProgramModel model)
        {
            Log("========== 코드 서명 ==========");

            var options = new SignOptions
            {
                PfxPath = PfxPath,
                PfxPassword = PfxPassword,
                TimestampUrl = TimestampUrl.Trim(),
                Description = model.Name,
            };

            SignReport report = await CodeSigner.SignAsync(new[] { outDir }, options, Log);

            if (!report.IsSuccess)
                throw new InvalidOperationException($"코드 서명에 실패한 파일이 {report.Failed.Count}개 있습니다. 산출물을 배포하지 마세요.");
        }

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
            if (UseWindowsService && string.IsNullOrWhiteSpace(WindowsServiceName))
            {
                StatusText = "서비스 이름을 입력하세요.";
                return null;
            }
            if (UseLicense && !int.TryParse(ProgramIdText, out _))
            {
                StatusText = "프로그램 ID는 숫자로 입력하세요.";
                return null;
            }
            if (UseCodeSigning && !IsCertValid)
            {
                StatusText = "코드 서명 인증서를 확인하세요: " + CertInfoText.Split('\n')[0];
                return null;
            }

            return new ProgramModel
            {
                Name = Name.Trim(),
                Publisher = Publisher.Trim(),
                Version = Version.Trim(),
                Description = Description.Trim(),
                RootPath = RootPath.Trim(),
                DataRootPath = DataRootPath.Trim(),
                RegistryKey = RegistryKey.Trim(),
                UseWindowsService = UseWindowsService,
                WindowsServiceName = WindowsServiceName.Trim(),
                WindowsServiceDescription = WindowsServiceDescription.Trim(),
                UseLicense = UseLicense,
                ProgramId = int.TryParse(ProgramIdText, out var id) ? id : 0,
            };
        }
    }
}