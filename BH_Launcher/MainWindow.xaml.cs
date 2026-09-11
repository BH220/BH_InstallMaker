using System.Windows;
using System.Windows.Input;
using BH_Launcher.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace BH_Launcher
{
    public partial class MainWindow : Window
    {
        private readonly LauncherViewModel _vm;

        public MainWindow()
        {
            InitializeComponent();

            _vm = Ioc.Default.GetRequiredService<LauncherViewModel>();
            DataContext = _vm;

            _vm.LaunchRequested += (_, _) =>
            {
                MessageBox.Show("여기서 실제 프로그램을 실행합니다. (더미)", "런처",
                    MessageBoxButton.OK, MessageBoxImage.Information);
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
