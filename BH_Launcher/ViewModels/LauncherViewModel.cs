using CommunityToolkit.Mvvm.ComponentModel;

namespace BH_Launcher.ViewModels
{
    public partial class LauncherViewModel : ObservableObject
    {
        //더미 업데이트 파일 목록 (이름, 크기 MB)
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

        //업데이트 완료 후 실제 프로그램 실행 요청
        public event EventHandler? LaunchRequested;

        public async Task RunUpdateAsync()
        {
            if (_started)
                return;
            _started = true;

            // 1) 업데이트 확인 (더미)
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
            VersionText = "v1.3.1";

            await Task.Delay(1500);
            LaunchRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
