using System.Collections.Specialized;
using System.Windows;
using System.Windows.Input;
using BH_Install.Core;
using BH_Install.Core.Helper;
using BH_Install.Core.Manager;
using BH_Install.ViewModels;

namespace BH_Install.Views
{
    //설치 위저드 창. 화면 상태(단계 전환·사이드바·애니메이션)는 XAML 바인딩과 트리거가 그리고,
    //라이선스 키 입력 규칙은 LicenseKeyInput 첨부 동작이 맡는다. 여기에는 창 자체에 관한 처리만 둔다.
    public partial class MainView : Window
    {
        public MainView(MainViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;

            //좌상단 로고: 매니페스트(ProgramModel.MainIcon)와 함께 Core 에 들어간 대상 프로그램 아이콘. 못 읽으면 BH 글자
            LogoImage.Source = IconResourceLoader.LoadBestFrame(ProgramManifest.IconResourcePath, 48);
            if (LogoImage.Source is null) LogoText.Visibility = Visibility.Visible;

            //뷰모델이 닫기를 요청하면(마침·취소) 창을 닫는다
            vm.CloseRequested += (_, _) => Close();

            //로그가 추가되면 끝으로 스크롤한다
            ((INotifyCollectionChanged)vm.Logs).CollectionChanged += (_, _) => LogScroll.ScrollToEnd();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
