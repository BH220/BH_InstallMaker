using System.Windows;
using System.Windows.Input;
using BH_Install.Core;
using BH_Install.Core.Helper;
using BH_Install.Core.Manager;
using BH_Launcher.ViewModels;

namespace BH_Launcher.Views
{
    //런처 창. 업데이트 진행과 프로그램 실행은 뷰모델이 맡고, 여기에는 창 자체에 관한 처리만 둔다.
    public partial class MainView : Window
    {
        public MainView(MainViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;

            //좌상단 로고: 매니페스트(ProgramModel.MainIcon)와 함께 Core 에 들어간 대상 프로그램 아이콘.
            //없으면 런처 자체 아이콘(main_icon.ico), 그것도 못 읽으면 BH 글자
            LogoImage.Source = IconResourceLoader.LoadBestFrame(ProgramManifest.IconResourcePath, 32)
                            ?? IconResourceLoader.LoadBestFrame("BH_Launcher;component/main_icon.ico", 32);
            if (LogoImage.Source is null) LogoText.Visibility = Visibility.Visible;

            //뷰모델이 닫기를 요청하면(프로그램을 띄운 뒤) 창을 닫는다
            vm.CloseRequested += (_, _) => Close();

            //창이 뜨면 업데이트 확인을 시작한다
            Loaded += async (_, _) => await vm.RunUpdateAsync();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
