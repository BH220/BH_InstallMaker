using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BH_InstallerMaker.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace BH_InstallerMaker
{
    public partial class MainWindow : Window
    {
        private readonly MakerViewModel _vm;

        public MainWindow()
        {
            InitializeComponent();

            _vm = Ioc.Default.GetRequiredService<MakerViewModel>();
            DataContext = _vm;

            ((INotifyCollectionChanged)_vm.Logs).CollectionChanged += (_, _) => LogScroll.ScrollToEnd();

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
            BtnMaximize.Content = maximized ? "" : "";
        }

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