using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using BH_Install.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BH_Install.ViewModels
{
    public partial class InstallViewModel : ObservableObject
    {
        public const int StepWelcome = 0;
        public const int StepLicense = 1;
        public const int StepProgress = 2;
        public const int StepDone = 3;

        // 더미 데이터 - 추후 실제 ProgramModel 로딩으로 교체
        private readonly ProgramModel _model = new()
        {
            Name = "BH Sample Program",
            Publisher = "BH Soft",
            Version = "1.3.1",
            Description = "웹 업데이트 기반 런처를 통해 배포되는 샘플 프로그램입니다.",
            RootPath = @"C:\Program Files\BH Sample Program",
            DataRootPath = @"C:\ProgramData\BH Sample Program",
            RegistryKey = @"SOFTWARE\BHSoft\BHSampleProgram",
            UseWindowsService = true,
            WindowsServiceName = "BHSampleService",
            WindowsServiceDescription = "BH 샘플 백그라운드 서비스",
            UseLicense = true,
        };

        private readonly Random _rand = new();

        // 루트 인증서 설치 결과. 완료 화면 문구에 쓴다.
        private CertInstallReport? _certReport;

        // 설치 단계 정의. Action 이 null 인 단계는 아직 더미 표시만 한다.
        private sealed record InstallStage(double At, string Text, Func<InstallViewModel, Task>? Action);

        // (진행률 임계값, 상태 문구, 실제 작업) 설치 시나리오
        private static readonly InstallStage[] Stages =
        {
            new(0,  "설치 준비 중...",                null),
            new(5,  "루트 인증서 설치 중...",          vm => vm.InstallRootCertificateAsync()),
            new(16, "파일 복사 중: BH_Launcher.exe",   null),
            new(32, "파일 복사 중: BH_Updater.dll",    null),
            new(48, "레지스트리 등록 중...",           null),
            new(62, "시작 메뉴 바로 가기 생성 중...",   null),
            new(74, "바탕화면 아이콘 생성 중...",       null),
            new(84, "Windows 서비스 등록 중...",       null),
            new(94, "설치 마무리 중...",               null),
        };

        [ObservableProperty]
        private int currentStep = StepWelcome;

        [ObservableProperty]
        private string nextLabel = "다음  〉";

        [ObservableProperty]
        private bool isNextEnabled = true;

        [ObservableProperty]
        private bool isBackVisible;

        [ObservableProperty]
        private bool isCancelVisible = true;

        [ObservableProperty]
        private string licenseKey = "";

        [ObservableProperty]
        private string stageText = "설치 준비 중...";

        [ObservableProperty]
        private double percent;

        [ObservableProperty]
        private string percentText = "0%";

        [ObservableProperty]
        private bool runNow = true;

        public ObservableCollection<string> Logs { get; } = new();

        // 표시 텍스트
        public string ProgramName => _model.Name;
        public string VersionChip => $"v{_model.Version}";
        public string PublisherFooter => $"© {_model.Publisher}";
        public string WelcomeText => $"{_model.Name}을(를) 이 컴퓨터에 설치합니다. 계속하려면 [다음]을 누르세요.";
        public string InfoName => _model.Name;
        public string InfoVersion => _model.Version;
        public string InfoPublisher => _model.Publisher;
        public string InfoRoot => _model.RootPath;
        public string InfoData => _model.DataRootPath;
        public string InfoDescription => _model.Description;
        public string DoneSubText => $"{_model.Name}이(가) 성공적으로 설치되었습니다.";
        public string RunNowLabel => $"지금 {_model.Name} 실행";

        // 완료 화면에 보여줄 루트 인증서 설치 결과 문구
        public string CertStatusText
        {
            get
            {
                CertInstallReport? report = _certReport;
                if (report is null) return string.Empty;

                return report.Status switch
                {
                    CertInstallStatus.Installed
                        => "BH Soft 루트 인증서를 설치했습니다.",
                    CertInstallStatus.AlreadyInstalled
                        => "BH Soft 루트 인증서가 이미 설치되어 있습니다.",
                    _   => $"루트 인증서 설치 실패: {report.Message}"
                };
            }
        }

        public event EventHandler? CloseRequested;

        [RelayCommand]
        private void Next()
        {
            switch (CurrentStep)
            {
                case StepWelcome:
                    GoTo(StepLicense);
                    break;
                case StepLicense:
                    GoTo(StepProgress);
                    break;
                case StepDone:
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }

        [RelayCommand]
        private void Back()
        {
            if (CurrentStep is StepLicense)
                GoTo(StepWelcome);
        }

        [RelayCommand]
        private void Cancel() => CloseRequested?.Invoke(this, EventArgs.Empty);

        private void GoTo(int step)
        {
            CurrentStep = step;

            switch (step)
            {
                case StepWelcome:
                    NextLabel = "다음  〉";
                    IsNextEnabled = true;
                    IsBackVisible = false;
                    IsCancelVisible = true;
                    break;
                case StepLicense:
                    NextLabel = "설치";
                    IsNextEnabled = true;
                    IsBackVisible = true;
                    IsCancelVisible = true;
                    break;
                case StepProgress:
                    NextLabel = "설치 중...";
                    IsNextEnabled = false;
                    IsBackVisible = false;
                    IsCancelVisible = true;
                    _ = RunInstallAsync();
                    break;
                case StepDone:
                    NextLabel = "마침";
                    IsNextEnabled = true;
                    IsBackVisible = false;
                    IsCancelVisible = false;
                    break;
            }
        }

        // 3단계 설치 진행. 인증서 설치는 실제 동작이고 나머지는 아직 더미다.
        private async Task RunInstallAsync()
        {
            Percent = 0;
            Logs.Clear();
            _certReport = null;
            int stageIndex = -1;

            while (Percent < 100)
            {
                // 임계값을 넘긴 단계로 진입하며, 실제 작업이 있으면 끝날 때까지 기다린다.
                while (stageIndex + 1 < Stages.Length && Percent >= Stages[stageIndex + 1].At)
                {
                    stageIndex++;
                    InstallStage stage = Stages[stageIndex];

                    StageText = stage.Text;
                    AddLog(stage.Text);

                    if (stage.Action is not null)
                        await stage.Action(this);
                }

                Percent = Math.Min(100, Percent + _rand.NextDouble() * 1.8 + 0.3);
                PercentText = $"{(int)Percent}%";

                await Task.Delay(60);
            }

            AddLog("설치 완료");
            GoTo(StepDone);
        }

        // BH Soft 루트 CA 인증서를 신뢰할 수 있는 루트 저장소에 설치한다.
        // 이 인증서가 없으면 설치되는 런처와 이후 업데이트 파일의 코드 서명이
        // "알 수 없는 게시자"로 표시된다.
        private async Task InstallRootCertificateAsync()
        {
            // 인증서 저장소 접근은 UI 스레드를 막을 수 있어 백그라운드로 돌린다.
            CertInstallReport report = await Task.Run(() => CertificateInstaller.InstallRoot(AddLog));

            _certReport = report;
            OnPropertyChanged(nameof(CertStatusText));

            if (!report.IsSuccess)
            {
                // 인증서 설치 실패가 설치 전체를 막지는 않는다.
                // 프로그램은 정상 동작하고 서명 검증만 통과하지 않는다.
                AddLog("서명 검증 없이 설치를 계속합니다.");
            }
        }

        // 백그라운드 스레드에서도 호출되므로 UI 스레드로 넘긴다.
        private void AddLog(string text)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}]  {text}";

            Dispatcher? dispatcher = Application.Current?.Dispatcher;

            if (dispatcher is null || dispatcher.CheckAccess())
                Logs.Add(line);
            else
                dispatcher.Invoke(() => Logs.Add(line));
        }
    }
}
