using System.Windows;
using System.Windows.Input;
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