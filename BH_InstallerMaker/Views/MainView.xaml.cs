using BH_InstallerMaker.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace BH_InstallerMaker.Views
{
    /// <summary>
    /// MainView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainView : Window
    {
        private readonly MainViewModel _vm;

        public MainView(MainViewModel mainViewModel)
        {
            InitializeComponent();
            _vm = mainViewModel;
            this.DataContext = mainViewModel;

            //PasswordBox 는 바인딩이 안 되므로 설정에서 복원한 암호를 직접 넣는다
            PfxPasswordBox.Password = mainViewModel.PfxPassword;



            // 최대화 시 작업 표시줄을 가리지 않도록 제한
            MaxWidth = SystemParameters.WorkArea.Width;
            MaxHeight = SystemParameters.WorkArea.Height;
            StateChanged += OnWindowStateChanged;
        }


        private void OnWindowStateChanged(object? sender, EventArgs e)
        {
            bool maximized = WindowState == WindowState.Maximized;
            RootBorder.Margin = maximized ? new Thickness(0) : new Thickness(12);
            RootBorder.CornerRadius = maximized ? new CornerRadius(0) : new CornerRadius(12);
            //Segoe MDL2 Assets: E923 = 복원, E922 = 최대화. 리터럴 글리프는 인코딩 변환에서 사라질 수 있어 이스케이프로 쓴다.
            BtnMaximize.Content = maximized ? "\uE923" : "\uE922";
        }

        //로그가 추가되면 끝으로 스크롤한다
        private void LogBox_TextChanged(object sender, TextChangedEventArgs e) => LogBox.ScrollToEnd();

        //PasswordBox.Password 는 바인딩이 안 되므로 코드에서 뷰모델로 넘긴다
        private void PfxPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            _vm.PfxPassword = ((PasswordBox)sender).Password;
        }

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
