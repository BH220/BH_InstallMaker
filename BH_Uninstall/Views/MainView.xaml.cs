using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using BH_Uninstall.ViewModels;

namespace BH_Uninstall.Views
{
    public partial class MainView : Window
    {
        private readonly MainViewModel _vm;

        public MainView(MainViewModel vm)
        {
            InitializeComponent();

            _vm = vm;
            DataContext = _vm;

            _vm.CloseRequested += (_, _) => Close();
            _vm.PropertyChanged += OnViewModelPropertyChanged;
            ((INotifyCollectionChanged)_vm.Logs).CollectionChanged += (_, _) => LogScroll.ScrollToEnd();

            //제거가 끝난 뒤 창을 닫으면 자기 exe 삭제를 예약한다
            Closing += (_, _) => _vm.OnWindowClosing();
        }

        //화면 전환은 순수 시각 효과이므로 뷰에서 처리
        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(MainViewModel.ScreenIndex))
                return;

            PanelConfirm.Visibility = _vm.ScreenIndex == MainViewModel.ScreenConfirm ? Visibility.Visible : Visibility.Collapsed;
            PanelProgress.Visibility = _vm.ScreenIndex == MainViewModel.ScreenProgress ? Visibility.Visible : Visibility.Collapsed;
            PanelDone.Visibility = _vm.ScreenIndex == MainViewModel.ScreenDone ? Visibility.Visible : Visibility.Collapsed;

            var target = _vm.ScreenIndex switch
            {
                MainViewModel.ScreenConfirm => PanelConfirm,
                MainViewModel.ScreenProgress => PanelProgress,
                _ => PanelDone,
            };
            PlayEnterAnimation(target);

            //완료 화면에서는 닫기 버튼을 기본 스타일로
            if (_vm.ScreenIndex == MainViewModel.ScreenDone)
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
