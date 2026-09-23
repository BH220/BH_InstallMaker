using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BH_InstallerMaker.ViewModels;

namespace BH_InstallerMaker.Views
{
    //메이커 창. 입력값은 모두 뷰모델에 바인딩되고(PFX 암호는 PasswordBoxHelper), 최대화 시 모양은 XAML 트리거가 바꾼다.
    //여기에는 창 자체에 관한 처리만 둔다.
    public partial class MainView : Window
    {
        public MainView(MainViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }

        //로그가 추가되면 끝으로 스크롤한다
        private void LogBox_TextChanged(object sender, TextChangedEventArgs e) => ((TextBox)sender).ScrollToEnd();

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
                return;
            }
            if (e.ButtonState == MouseButtonState.Pressed && WindowState == WindowState.Normal)
                DragMove();
        }

        private void ToggleMaximize() =>
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        private void BtnMaximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
