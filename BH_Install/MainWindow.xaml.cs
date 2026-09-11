using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using BH_Install.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace BH_Install
{
    public partial class MainWindow : Window
    {
        private readonly InstallViewModel _vm;
        private FrameworkElement[] _steps = null!;
        private (Border Chip, TextBlock Num)[] _stepChips = null!;

        public MainWindow()
        {
            InitializeComponent();

            _vm = Ioc.Default.GetRequiredService<InstallViewModel>();
            DataContext = _vm;

            _steps = new FrameworkElement[] { Step0, Step1, Step2, Step3 };
            _stepChips = new[]
            {
                (StepChip0, StepNum0),
                (StepChip1, StepNum1),
                (StepChip2, StepNum2),
                (StepChip3, StepNum3),
            };

            _vm.CloseRequested += (_, _) => Close();
            _vm.PropertyChanged += OnViewModelPropertyChanged;
            ((INotifyCollectionChanged)_vm.Logs).CollectionChanged += (_, _) => LogScroll.ScrollToEnd();

            ShowStep(_vm.CurrentStep, animate: false);
        }

        //단계 전환의 시각 효과(패널 전환/애니메이션/사이드바)는 뷰에서 처리
        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(InstallViewModel.CurrentStep))
                ShowStep(_vm.CurrentStep, animate: true);
        }

        private void ShowStep(int index, bool animate)
        {
            for (int i = 0; i < _steps.Length; i++)
                _steps[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;

            UpdateSidebar(index);

            if (animate)
                PlayEnterAnimation(_steps[index]);
        }

        private void UpdateSidebar(int current)
        {
            for (int i = 0; i < _stepChips.Length; i++)
            {
                var (chip, num) = _stepChips[i];
                if (i == current)
                {
                    chip.Background = new SolidColorBrush(Color.FromArgb(0x2A, 0xFF, 0xFF, 0xFF));
                    chip.Opacity = 1.0;
                }
                else
                {
                    chip.Background = Brushes.Transparent;
                    chip.Opacity = i < current ? 0.85 : 0.55;
                }
                num.Text = i < current ? "✓" : (i + 1).ToString();
            }
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
