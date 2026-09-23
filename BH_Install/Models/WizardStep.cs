using CommunityToolkit.Mvvm.ComponentModel;

namespace BH_Install.Models
{
    //사이드바 단계 목록의 한 항목.
    //뷰모델이 CurrentStep 에 맞춰 IsCurrent/IsDone 을 갱신하고, 화면은 DataTrigger 로 강조·체크 표시를 그린다.
    public sealed partial class WizardStep : ObservableObject
    {
        public int Index { get; }
        public string Title { get; }

        //칩에 보이는 번호 (1부터). 완료된 단계는 화면에서 ✓ 로 바꾼다.
        public string Number => (Index + 1).ToString();

        [ObservableProperty]
        private bool isCurrent;

        [ObservableProperty]
        private bool isDone;

        public WizardStep(int index, string title)
        {
            Index = index;
            Title = title;
        }
    }
}
