using BH_Install.Core;
using BH_Install.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace BH_Install.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        public const int StepWelcome = 0;
        public const int StepEula = 1;
        public const int StepLicense = 2;
        public const int StepProgress = 3;
        public const int StepDone = 4;
         

        private readonly Random _rand = new();

        // 루트 인증서 설치 결과. 완료 화면 문구에 쓴다.
        private CertInstallReport? _certReport;

        // 설치 중 오류
        private bool _failed;
        private string _failReason = string.Empty;

        //메이커가 빌드 직전에 BH_Install.Core 의 ProgramModel.json 에 저장한 매니페스트.
        //비어 있으면(F5, 빈 ProgramModel.json) 미리보기 모드로 화면만 보여주고 실제 파일·레지스트리 작업은 하지 않는다.
        private readonly ProgramModel _model = ProgramManifest.Instance.ProgramModel;
        private readonly bool _isReal = ProgramManifest.Instance.IsLoaded;

        private string LauncherPath => Path.Combine(_model.RootPath, ProgramManifest.Instance.LauncherFileName);
        private string UninstallerPath => Path.Combine(_model.RootPath, ProgramManifest.Instance.UninstallFileName);

        // 설치 단계 정의. Action 이 null 인 단계는 표시만 한다.
        private sealed record InstallStage(double At, string Text, Func<MainViewModel, Task>? Action);

        // (진행률 임계값, 상태 문구, 실제 작업) 설치 시나리오
        private static readonly InstallStage[] Stages =
        {
            new(0,  "설치 준비 중...",                          vm => vm.PrepareAsync()),
            new(6,  "루트 인증서 설치 중...",                    vm => vm.InstallRootCertificateAsync()),
            new(18, "런처 복사 중...",                           vm => vm.ExtractPayloadAsync(PayloadResource.Launcher, vm.LauncherPath)),
            new(34, "제거 프로그램 복사 중...",                   vm => vm.ExtractPayloadAsync(PayloadResource.Uninstall, vm.UninstallerPath)),
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

        //사용권 계약 동의. 동의해야 [다음] 이 켜진다
        [ObservableProperty]
        private bool eulaAccepted;

        //사용권 계약 본문
        public string EulaText => Eula.Build(_model.Name, _model.Publisher);

        partial void OnEulaAcceptedChanged(bool value)
        {
            if (CurrentStep == StepEula)
                IsNextEnabled = value;
        }

        [ObservableProperty]
        private string licenseKey = "";

        //라이선스 키 형식 오류 안내. 비어 있으면 화면에서 숨긴다.
        [ObservableProperty]
        private string licenseKeyError = "";

        public const string LicenseKeyFormatMessage = "영문과 숫자만 입력할 수 있습니다. 하이픈은 자동으로 들어갑니다.";

        //라이선스 키 길이 (하이픈 제외). 붙여넣기도 이 길이에서 잘린다.
        public const int LicenseKeyLength = 12;
        public static readonly string LicenseKeyLengthMessage = $"라이선스 키는 영문·숫자 {LicenseKeyLength}자입니다.";

        //실제 처리에 쓰는 값. 화면 표시용 하이픈을 뺀 영문·숫자만 (대문자)
        public string LicenseKeyRaw => LicenseKey.Replace("-", string.Empty).ToUpperInvariant();

        private static readonly Regex LicenseKeyPattern = new("^[A-Za-z0-9-]*$", RegexOptions.Compiled);

        //영문·숫자·하이픈으로만 되어 있는지
        public static bool IsValidLicenseKeyChars(string text) => LicenseKeyPattern.IsMatch(text);

        //허용되지 않는 문자를 입력하려 했을 때 뷰가 호출한다
        public void ReportLicenseKeyFormatError() => LicenseKeyError = LicenseKeyFormatMessage;

        //올바른 문자가 입력되면 안내를 지운다
        partial void OnLicenseKeyChanged(string value) =>
            LicenseKeyError = IsValidLicenseKeyChars(value) ? string.Empty : LicenseKeyFormatMessage;

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
                    GoTo(StepEula);
                    break;
                case StepEula:
                    if (EulaAccepted)
                        GoTo(StepLicense);
                    break;
                case StepLicense:
                    //공인 IP 조회 등 네트워크를 타므로 비동기로 확인하고 통과하면 설치 단계로 간다
                    _ = CheckLicenseThenContinueAsync();
                    break;
                case StepDone:
                    if (RunNow && !_failed)
                        LaunchProgram();
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }

        // 라이선스 확인 중에는 버튼을 잠그고, 통과하면 설치로, 실패하면 사유를 보여주고 이 단계에 머문다.
        private async Task CheckLicenseThenContinueAsync()
        {
            if (!IsValidLicenseKeyChars(LicenseKey))
            {
                ReportLicenseKeyFormatError();
                return;
            }
            if (LicenseKeyRaw.Length != LicenseKeyLength)
            {
                LicenseKeyError = LicenseKeyLengthMessage;
                return;
            }

            IsNextEnabled = false;
            IsBackVisible = false;
            NextLabel = "라이선스 확인 중...";

            (bool ok, string message) = await CheckLicenseAsync();

            if (ok)
            {
                GoTo(StepProgress);
                return;
            }

            NextLabel = "설치";
            IsNextEnabled = true;
            IsBackVisible = true;
            MessageBox.Show(message, "라이선스 확인", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // 라이선스 서버에 활성 요청을 보낸다.
        // 요청 값: 프로그램 ID, 요청 종류(120001 활성), 공인 IP(사설망 IP 아님), MAC, PC 이름, 사용자 이름, 입력한 라이선스 키
        private async Task<(bool Ok, string Message)> CheckLicenseAsync()
        {
            if(string.IsNullOrEmpty(licenseKey))
                return (false, "라이선스가 입력되지 않았습니다.");

            if(licenseKey.Trim().Replace(" ", "").Replace("-", "").Length != 12)
                return (false, "라이선스가 올바르지 않습니다.");

            LicenseRequest request;
            try
            {
                request = await LicenseRequest.CreateAsync((int)_model.ProgramId, LicenseRequestType.Activate, LicenseKeyRaw);
            }
            catch (Exception ex)
            {
                return (false, $"PC 정보를 확인할 수 없습니다.\n{ex.Message}");
            }

            AddLog($"라이선스 요청: programId={request.ProgramId} type={request.RequestType} ip={request.Ip} mac={request.Mac} pc={request.PcName} user={request.UserName}");

            if (string.IsNullOrEmpty(request.Ip))
                return (false, "공인 IP 를 확인할 수 없습니다. 인터넷 연결을 확인한 뒤 다시 시도하세요.");

            if (string.IsNullOrEmpty(request.Mac))
                return (false, "네트워크 어댑터의 MAC 주소를 확인할 수 없습니다.");

            ResLicense res = await GetLicenseActivateResult(request);

            return (res.result == 1, res.msg);
        }

        private async Task<ResLicense> GetLicenseActivateResult(LicenseRequest reqLicense)
        {
            ResLicense result = new ResLicense();
            result.result = 0;
            try
            {
                HttpClientHandler handler = new HttpClientHandler()
                {
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                };
                HttpClient client = new HttpClient();
                string _baseUrl = "https://www.bhsoft.co.kr";
#if DEBUG
                _baseUrl = "http://localhost:8169";
#endif
                string apiUrl = $"{_baseUrl}/license/register";

                Dictionary<string, string> dic = new Dictionary<string, string>();
                dic.Add("license_key", reqLicense.LicenseKey);
                dic.Add("program_num", reqLicense.ProgramId);
                dic.Add("request_type", reqLicense.RequestType);
                dic.Add("ip", reqLicense.ProgramId);
                dic.Add("mac", reqLicense.Mac);
                dic.Add("pc_name", reqLicense.PcName);
                dic.Add("user_name", reqLicense.UserName);
                FormUrlEncodedContent content = new FormUrlEncodedContent(dic);
                HttpResponseMessage response = await client.PostAsync(apiUrl, content);
                string str = await response.Content.ReadAsStringAsync();
                result = JsonConvert.DeserializeObject<ResLicense>(str) ?? result;
                //result.result = 1;
            }
            catch (Exception ex)
            {
                result.msg = ex.Message;
            }
            return result;
        }

        [RelayCommand]
        private void Back()
        {
            if (CurrentStep is StepEula)
                GoTo(StepWelcome);
            else if (CurrentStep is StepLicense)
                GoTo(StepEula);
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
                case StepEula:
                    NextLabel = "다음  〉";
                    IsNextEnabled = EulaAccepted;
                    IsBackVisible = true;
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
            if (!PayloadResource.Extract(resourceName, destination))
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