using System.Windows;
using System.Windows.Input;
using BH_Install.Core;
using BH_Launcher.ViewModels;

namespace BH_Launcher.Views
{
    public partial class MainView : Window
    {
        private readonly MainViewModel _vm;

        public MainView(MainViewModel vm)
        {
            InitializeComponent();

            _vm = vm;
            DataContext = _vm;

            //좌상단 로고: 대상 프로그램 아이콘. 못 읽으면 BH 글자로 대체
            LogoImage.Source = IconResourceLoader.LoadBestFrame("BH_Launcher;component/main_icon.ico", 32);
            if (LogoImage.Source is null) LogoText.Visibility = Visibility.Visible;

            _vm.LaunchRequested += (_, _) =>
            {
                if (!_vm.TryLaunchProgram(out string message))
                    MessageBox.Show(this, message, "런처", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            };

            Loaded += async (_, _) => await _vm.RunUpdateAsync();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}