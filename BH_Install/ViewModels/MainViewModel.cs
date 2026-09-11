using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using BH_Install.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BH_Install.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        public const int StepWelcome = 0;
        public const int StepLicense = 1;
        public const int StepProgress = 2;
        public const int StepDone = 3;

        // 메이커가 게시할 때 임베드한 매니페스트. 없으면(F5 디버깅) 더미 값으로 화면만 보여주고
        // 파일 복사·레지스트리 같은 실제 작업은 하지 않는다(미리보기 모드).
        private readonly ProgramModel _model;
        private readonly bool _isReal;

        private readonly Random _rand = new();

        // 루트 인증서 설치 결과. 완료 화면 문구에 쓴다.
        private CertInstallReport? _certReport;

        // 설치 중 오류
        private bool _failed;
        private string _failReason = string.Empty;

        private string LauncherPath => Path.Combine(_model.RootPath, ProgramManifest.LauncherFileName(_model));
        private string UninstallerPath => Path.Combine(_model.RootPath, ProgramManifest.UninstallFileName(_model));

        public MainViewModel()
        {
            ProgramModel? embedded = ProgramManifest.LoadEmbedded();
            _isReal = embedded is not null;
            _model = embedded ?? CreateDummyModel();
        }

        private static ProgramModel CreateDummyModel() => new()
        {
            Name = "BH Sample Program",
            Publisher = "BH Soft",
            Version = "1.3.1",
            Description = "웹 업데이트 기반 런처를 통해 배포되는 샘플 프로그램입니다.",
            RootPath = @"C:\Program Files\BH Soft\BH Sample Program",
            DataRootPath = @"C:\ProgramData\BH Soft\BH Sample Program",
            RegistryKey = @"SOFTWARE\BH Soft\BH Sample Program",
            MainExe = "BH_Program.exe",
            ProgramId = 1,
        };

        // 설치 단계 정의. Action 이 null 인 단계는 표시만 한다.
        private sealed record InstallStage(double At, string Text, Func<MainViewModel, Task>? Action);

        // (진행률 임계값, 상태 문구, 실제 작업) 설치 시나리오
        private static readonly InstallStage[] Stages =
        {
            new(0,  "설치 준비 중...",                          vm => vm.PrepareAsync()),
            new(6,  "루트 인증서 설치 중...",                    vm => vm.InstallRootCertificateAsync()),
            new(18, "런처 복사 중...",                           vm => vm.ExtractPayloadAsync(ProgramManifest.LauncherPayloadName, vm.LauncherPath)),
            new(34, "제거 프로그램 복사 중...",                   vm => vm.ExtractPayloadAsync(ProgramManifest.UninstallPayloadName, vm.UninstallerPath)),
            new(50, "레지스트리 등록 중...",                     vm => vm.RegisterAsync()),
            new(64, "시작 메뉴 바로 가기 생성 중...",             vm => vm.CreateShortcutAsync(ShortcutHelper.StartMenuDirFor(vm._model))),
            new(76, "바탕화면 아이콘 생성 중...",                 vm => vm.CreateShortcutAsync(ShortcutHelper.DesktopDir)),
            new(92, "설치 마무리 중...",                         null),
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
        public string RunNowLabel => $"지금 {_model.Name} 실행";

        public string DoneTitle => _failed ? "설치를 완료하지 못했습니다" : "설치가 완료되었습니다";

        public string DoneSubText => _failed
            ? $"설치 중 오류가 발생했습니다.\n{_failReason}"
            : $"{_model.Name}이(가) 성공적으로 설치되었습니다.";

        // 완료 화면에 보여줄 루트 인증서 설치 결과 문구
        public string CertStatusText
        {
            get
            {
                CertInstallReport? report = _certReport;
                if (report is null) return string.Empty;

                return report.Status switch
                {
                    CertInstallStatus.Installed        => "BH Soft 루트 인증서를 설치했습니다.",
                    CertInstallStatus.AlreadyInstalled => "BH Soft 루트 인증서가 이미 설치되어 있습니다.",
                    _                                  => $"루트 인증서 설치 실패: {report.Message}"
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
                    if (RunNow && !_failed)
                        LaunchProgram();
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
                    NextLabel = _failed ? "닫기" : "마침";
                    IsNextEnabled = true;
                    IsBackVisible = false;
                    IsCancelVisible = false;
                    break;
            }
        }

        // 설치 진행. 단계마다 실제 작업을 기다리며, 오류가 나면 거기서 멈추고 완료 화면에 사유를 보여준다.
        private async Task RunInstallAsync()
        {
            Percent = 0;
            Logs.Clear();
            _certReport = null;
            _failed = false;
            _failReason = string.Empty;
            int stageIndex = -1;

            if (!_isReal)
                AddLog("매니페스트가 없어 미리보기 모드로 동작합니다. 실제 파일·레지스트리 작업은 하지 않습니다.");

            while (Percent < 100 && !_failed)
            {
                // 임계값을 넘긴 단계로 진입하며, 실제 작업이 있으면 끝날 때까지 기다린다.
                while (!_failed && stageIndex + 1 < Stages.Length && Percent >= Stages[stageIndex + 1].At)
                {
                    stageIndex++;
                    InstallStage stage = Stages[stageIndex];

                    StageText = stage.Text;
                    AddLog(stage.Text);

                    if (stage.Action is null) continue;

                    try
                    {
                        await stage.Action(this);
                    }
                    catch (Exception ex)
                    {
                        _failed = true;
                        _failReason = ex.Message;
                        AddLog($"오류: {ex.Message}");
                    }
                }

                if (_failed) break;

                Percent = Math.Min(100, Percent + _rand.NextDouble() * 1.8 + 0.3);
                PercentText = $"{(int)Percent}%";

                await Task.Delay(60);
            }

            if (_failed)
            {
                StageText = "설치 실패";
                AddLog("설치가 중단되었습니다.");
            }
            else
            {
                AddLog("설치 완료");
            }

            OnPropertyChanged(nameof(DoneTitle));
            OnPropertyChanged(nameof(DoneSubText));
            GoTo(StepDone);
        }

        // ===== 설치 단계 =====

        // 실제 모드면 백그라운드에서 작업을 수행하고, 미리보기 모드면 잠깐 기다리기만 한다.
        private Task RunReal(Action work)
        {
            if (!_isReal)
                return Task.Delay(150);

            return Task.Run(work);
        }

        private Task PrepareAsync() => RunReal(() =>
        {
            if (string.IsNullOrWhiteSpace(_model.RootPath))
                throw new InvalidOperationException("설치 경로가 지정되지 않았습니다.");

            Directory.CreateDirectory(_model.RootPath);
            if (!string.IsNullOrWhiteSpace(_model.DataRootPath))
                Directory.CreateDirectory(_model.DataRootPath);

            string? previous = RegisterHelper.ReadProgramValue(_model.RegistryKey, "Version");
            AddLog(previous is null
                ? $"설치 위치: {_model.RootPath}"
                : $"기존 설치(v{previous})를 발견했습니다. 덮어써서 업그레이드합니다.");
        });

        // BH Soft 루트 CA 인증서를 신뢰할 수 있는 루트 저장소에 설치한다.
        // 이 인증서가 없으면 설치되는 런처와 이후 업데이트 파일의 코드 서명이 "알 수 없는 게시자"로 표시된다.
        private async Task InstallRootCertificateAsync()
        {
            CertInstallReport report = await Task.Run(() => CertificateInstaller.InstallRoot(AddLog));

            _certReport = report;
            OnPropertyChanged(nameof(CertStatusText));

            if (!report.IsSuccess)
            {
                // 인증서 설치 실패가 설치 전체를 막지는 않는다.
                AddLog("서명 검증 없이 설치를 계속합니다.");
            }
        }

        private Task ExtractPayloadAsync(string resourceName, string destination) => RunReal(() =>
        {
            if (!ProgramManifest.ExtractPayload(resourceName, destination))
                throw new InvalidOperationException($"설치 파일에 {Path.GetFileName(destination)} 이(가) 포함되어 있지 않습니다.");

            AddLog($"복사됨: {destination}");
        });

        private Task RegisterAsync() => RunReal(() =>
        {
            long bytes = new DirectoryInfo(_model.RootPath)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(f => f.Length);

            RegisterHelper.WriteProgramInfo(_model, LauncherPath);
            RegisterHelper.WriteUninstallEntry(_model, UninstallerPath, LauncherPath, bytes);

            if (!string.IsNullOrWhiteSpace(_model.RegistryKey))
                AddLog($"레지스트리: HKLM\\{_model.RegistryKey}");
            AddLog("앱 및 기능 목록에 등록했습니다.");
        });

        private Task CreateShortcutAsync(string directory) => RunReal(() =>
        {
            string lnk = Path.Combine(directory, _model.Name + ".lnk");
            ShortcutHelper.Create(lnk, LauncherPath, _model.Description, _model.RootPath);
            AddLog($"바로 가기: {lnk}");
        });


        // 완료 화면의 [지금 실행] 이 켜져 있으면 런처를 띄운다.
        private void LaunchProgram()
        {
            if (!_isReal || !File.Exists(LauncherPath))
                return;

            try
            {
                Process.Start(new ProcessStartInfo(LauncherPath)
                {
                    UseShellExecute = true,
                    WorkingDirectory = _model.RootPath,
                });
            }
            catch (Exception ex)
            {
                AddLog($"런처 실행 실패: {ex.Message}");
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