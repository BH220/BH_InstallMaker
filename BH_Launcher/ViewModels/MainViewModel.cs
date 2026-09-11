using System.Diagnostics;
using System.IO;
using BH_Install.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BH_Launcher.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        // 메이커가 임베드한 매니페스트. 없으면(F5 디버깅) 더미 화면.
        private readonly ProgramModel? _model;

        //더미 업데이트 파일 목록 (이름, 크기 MB). 업데이트 서버 연동 전까지의 자리표시자다.
        private sealed record DummyFile(string Name, double SizeMb);

        private static readonly DummyFile[] Files =
        {
            new("core.dll", 8.4),
            new("resources.pak", 32.1),
            new("BH_Program.exe", 12.7),
        };

        private readonly Random _rand = new();
        private readonly double _totalMb = Files.Sum(f => f.SizeMb);
        private bool _started;

        [ObservableProperty]
        private string programName = "BH Sample Program";

        [ObservableProperty]
        private string versionText = "v1.2.0";

        [ObservableProperty]
        private string statusText = "서버에서 업데이트 확인 중...";

        [ObservableProperty]
        private string detailText = "-";

        [ObservableProperty]
        private double percent;

        [ObservableProperty]
        private string percentText = "0%";

        public MainViewModel()
        {
            _model = ProgramManifest.LoadEmbedded();
            if (_model is not null)
            {
                ProgramName = _model.Name;
                VersionText = $"v{_model.Version}";
            }
        }

        //업데이트 완료 후 실제 프로그램 실행 요청
        public event EventHandler? LaunchRequested;

        public async Task RunUpdateAsync()
        {
            if (_started)
                return;
            _started = true;

            // 1) 업데이트 확인 (더미 - 업데이트 리스트 연동 예정)
            for (int i = 0; i < 20; i++)
            {
                StatusText = "서버에서 업데이트 확인 중" + new string('.', i % 3 + 1);
                await Task.Delay(80);
            }

            // 2) 다운로드 (더미)
            for (int fi = 0; fi < Files.Length; fi++)
            {
                var file = Files[fi];
                double fileProgress = 0;
                while (fileProgress < 100)
                {
                    fileProgress = Math.Min(100, fileProgress + _rand.NextDouble() * 6 + 2);
                    StatusText = $"다운로드 중 ({fi + 1}/{Files.Length}): {file.Name}";

                    double doneMb = Files.Take(fi).Sum(f => f.SizeMb) + file.SizeMb * fileProgress / 100;
                    Percent = doneMb / _totalMb * 100;
                    PercentText = $"{(int)Percent}%";
                    DetailText = $"{doneMb:0.0} / {_totalMb:0.0} MB · {8 + _rand.NextDouble() * 7:0.0} MB/s";
                    await Task.Delay(80);
                }
            }

            // 3) 완료 후 실행
            Percent = 100;
            PercentText = "100%";
            DetailText = $"{_totalMb:0.0} / {_totalMb:0.0} MB";
            StatusText = "최신 버전입니다. 잠시 후 프로그램을 시작합니다...";
            VersionText = _model is null ? "v1.3.1" : $"v{_model.Version}";

            await Task.Delay(1500);
            LaunchRequested?.Invoke(this, EventArgs.Empty);
        }

        // 매니페스트의 MainExe 를 런처 폴더 기준으로 실행한다.
        // 실패하면 false 와 사용자에게 보여줄 문구를 돌려준다.
        public bool TryLaunchProgram(out string message)
        {
            if (_model is null)
            {
                message = "매니페스트가 없습니다. 여기서 실제 프로그램을 실행합니다. (미리보기)";
                return false;
            }

            if (string.IsNullOrWhiteSpace(_model.MainExe))
            {
                message = "실행할 메인 프로그램이 지정되지 않았습니다.";
                return false;
            }

            string exe = Path.Combine(AppContext.BaseDirectory, _model.MainExe);
            if (!File.Exists(exe))
            {
                message = $"프로그램 파일이 없습니다.\n{exe}\n\n업데이트 서버 연동 후에는 여기서 자동으로 내려받습니다.";
                return false;
            }

            try
            {
                Process.Start(new ProcessStartInfo(exe)
                {
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(exe),
                });
                message = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                message = $"프로그램을 실행할 수 없습니다.\n{ex.Message}";
                return false;
            }
        }
    }
}