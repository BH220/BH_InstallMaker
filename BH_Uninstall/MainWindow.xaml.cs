using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using BH_Uninstall.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace BH_Uninstall
{
    public partial class MainWindow : Window
    {
        private readonly UninstallViewModel _vm;

        public MainWindow()
        {
            InitializeComponent();

            _vm = Ioc.Default.GetRequiredService<UninstallViewModel>();
            DataContext = _vm;

            _vm.CloseRequested += (_, _) => Close();
            _vm.PropertyChanged += OnViewModelPropertyChanged;
            ((INotifyCollectionChanged)_vm.Logs).CollectionChanged += (_, _) => LogScroll.ScrollToEnd();
        }

        //화면 전환은 순수 시각 효과이므로 뷰에서 처리
        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(UninstallViewModel.ScreenIndex))
                return;

            PanelConfirm.Visibility = _vm.ScreenIndex == UninstallViewModel.ScreenConfirm ? Visibility.Visible : Visibility.Collapsed;
            PanelProgress.Visibility = _vm.ScreenIndex == UninstallViewModel.ScreenProgress ? Visibility.Visible : Visibility.Collapsed;
            PanelDone.Visibility = _vm.ScreenIndex == UninstallViewModel.ScreenDone ? Visibility.Visible : Visibility.Collapsed;

            var target = _vm.ScreenIndex switch
            {
                UninstallViewModel.ScreenConfirm => PanelConfirm,
                UninstallViewModel.ScreenProgress => PanelProgress,
                _ => PanelDone,
            };
            PlayEnterAnimation(target);

            //완료 화면에서는 닫기 버튼을 기본 스타일로
            if (_vm.ScreenIndex == UninstallViewModel.ScreenDone)
                BtnAction.Style = (Style)FindResource("Btn.Primary");
        }

        private static void PlayEnterAnimation(FrameworkElement element)
        {
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            element.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(240)));
            if (element.RenderTransform is TranslateTransform tt)
                tt.BeginAnimation(TranslateTransform.YProperty,
                    new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(240)) { EasingFunction = ease });
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
