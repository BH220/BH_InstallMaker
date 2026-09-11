using System.Collections.ObjectModel;
using BH_Install.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BH_Uninstall.ViewModels
{
    public partial class UninstallViewModel : ObservableObject
    {
        public const int ScreenConfirm = 0;
        public const int ScreenProgress = 1;
        public const int ScreenDone = 2;

        // 더미 데이터 - 추후 설치 정보 로딩으로 교체
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
        };

        private readonly Random _rand = new();

        private static readonly (double At, string Text)[] Stages =
        {
            (0,  "제거 준비 중..."),
            (8,  "Windows 서비스 중지 및 삭제 중..."),
            (24, "바탕화면 아이콘 삭제 중..."),
            (36, "시작 메뉴 항목 삭제 중..."),
            (50, "레지스트리 항목 삭제 중..."),
            (64, "다운로드된 업데이트 파일 삭제 중..."),
            (82, "프로그램 파일 삭제 중..."),
            (95, "제거 마무리 중..."),
        };

        [ObservableProperty]
        private int screenIndex = ScreenConfirm;

        [ObservableProperty]
        private string actionLabel = "제거";

        [ObservableProperty]
        private bool isActionEnabled = true;

        [ObservableProperty]
        private bool isCancelVisible = true;

        [ObservableProperty]
        private string stageText = "제거 준비 중...";

        [ObservableProperty]
        private double percent;

        [ObservableProperty]
        private string percentText = "0%";

        public ObservableCollection<string> Logs { get; } = new();

        public string ProgramName => _model.Name;
        public string ProgramSub => $"버전 {_model.Version} · {_model.Publisher}";
        public string ConfirmText => $"이 컴퓨터에서 {_model.Name}을(를) 제거합니다.";
        public string DoneSubText => $"{_model.Name}이(가) 컴퓨터에서 제거되었습니다.";

        public event EventHandler? CloseRequested;

        [RelayCommand]
        private async Task ActionAsync()
        {
            switch (ScreenIndex)
            {
                case ScreenConfirm:
                    ScreenIndex = ScreenProgress;
                    ActionLabel = "제거 중...";
                    IsActionEnabled = false;

                    await RunDummyRemoveAsync();

                    ScreenIndex = ScreenDone;
                    ActionLabel = "닫기";
                    IsActionEnabled = true;
                    IsCancelVisible = false;
                    break;

                case ScreenDone:
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }

        [RelayCommand]
        private void Cancel() => CloseRequested?.Invoke(this, EventArgs.Empty);

        // 더미 제거 시뮬레이션 - 추후 실제 제거 로직으로 교체
        private async Task RunDummyRemoveAsync()
        {
            Percent = 0;
            Logs.Clear();
            int stageIndex = -1;

            while (Percent < 100)
            {
                Percent = Math.Min(100, Percent + _rand.NextDouble() * 2.0 + 0.3);
                PercentText = $"{(int)Percent}%";

                while (stageIndex + 1 < Stages.Length && Percent >= Stages[stageIndex + 1].At)
                {
                    stageIndex++;
                    StageText = Stages[stageIndex].Text;
                    Logs.Add($"[{DateTime.Now:HH:mm:ss}]  {Stages[stageIndex].Text}");
                }

                await Task.Delay(60);
            }

            Logs.Add($"[{DateTime.Now:HH:mm:ss}]  제거 완료");
        }
    }
}
