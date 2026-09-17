using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using BH_Install.Core;
using BH_Install.ViewModels;

namespace BH_Install.Views
{
    public partial class MainView : Window
    {
        private readonly MainViewModel _vm;
        private FrameworkElement[] _steps = null!;
        private (Border Chip, TextBlock Num)[] _stepChips = null!;

        public MainView(MainViewModel vm)
        {
            InitializeComponent();

            _vm = vm;
            DataContext = _vm;

            _steps = new FrameworkElement[] { Step0, Step1, Step2, Step3, Step4 };
            _stepChips = new[]
            {
                (StepChip0, StepNum0),
                (StepChip1, StepNum1),
                (StepChip2, StepNum2),
                (StepChip3, StepNum3),
                (StepChip4, StepNum4),
            };

            //좌상단 로고: 대상 프로그램 아이콘. 못 읽으면 BH 글자로 대체
            LogoImage.Source = IconResourceLoader.LoadBestFrame("BH_Install;component/program_icon.ico", 48)
                            ?? IconResourceLoader.LoadBestFrame("BH_Install;component/main_icon.ico", 48);
            if (LogoImage.Source is null) LogoText.Visibility = Visibility.Visible;

            _vm.CloseRequested += (_, _) => Close();
            _vm.PropertyChanged += OnViewModelPropertyChanged;
            ((INotifyCollectionChanged)_vm.Logs).CollectionChanged += (_, _) => LogScroll.ScrollToEnd();

            ShowStep(_vm.CurrentStep, animate: false);
        }

        //단계 전환의 시각 효과(패널 전환/애니메이션/사이드바)는 뷰에서 처리
        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.CurrentStep))
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

        // ===== 라이선스 키 입력: 영문·숫자만 받고 4자마다 하이픈을 자동으로 넣는다 (하이픈은 표시용) =====

        private const int LicenseGroupSize = 4;
        //라이선스 키는 영문·숫자 12자. 화면에는 하이픈 2개를 더해 14자(XXXX-XXXX-XXXX)로 보인다. MaxLength=14
        private const int LicenseMaxChars = MainViewModel.LicenseKeyLength;

        private bool _formattingLicenseKey;

        //영문·숫자만 남기고 대문자로
        private static string RawLicenseChars(string text) =>
            new(text.Where(char.IsAsciiLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

        //4자마다 하이픈: ABCDEFGHI → ABCD-EFGH-I
        private static string FormatLicenseKey(string raw)
        {
            var groups = new List<string>();
            for (int i = 0; i < raw.Length; i += LicenseGroupSize)
                groups.Add(raw.Substring(i, Math.Min(LicenseGroupSize, raw.Length - i)));
            return string.Join("-", groups);
        }

        //입력·삭제·붙여넣기 뒤에 항상 XXXX-XXXX 형식으로 다시 맞추고 커서 위치를 보존한다
        private void LicenseKeyBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_formattingLicenseKey || sender is not TextBox box)
                return;

            string text = box.Text;
            int rawBeforeCaret = text.Take(box.CaretIndex).Count(char.IsAsciiLetterOrDigit);

            string raw = RawLicenseChars(text);
            if (raw.Length > LicenseMaxChars)
            {
                raw = raw[..LicenseMaxChars];
                rawBeforeCaret = Math.Min(rawBeforeCaret, LicenseMaxChars);
            }

            string formatted = FormatLicenseKey(raw);
            if (formatted == text)
                return;

            _formattingLicenseKey = true;
            try
            {
                box.Text = formatted;   //바인딩으로 뷰모델에도 반영된다
                int caret = rawBeforeCaret + (rawBeforeCaret > 0 ? (rawBeforeCaret - 1) / LicenseGroupSize : 0);
                box.CaretIndex = Math.Min(caret, formatted.Length);
            }
            finally
            {
                _formattingLicenseKey = false;
            }
        }

        //키 입력은 영문·숫자만. 하이픈·특수문자는 막고 안내를 띄운다
        private void LicenseKeyBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!e.Text.All(char.IsAsciiLetterOrDigit))
            {
                e.Handled = true;
                _vm.ReportLicenseKeyFormatError();
            }
        }

        //띄어쓰기는 막고, 하이픈 앞뒤에서의 Backspace/Delete 는 하이픈을 건너뛰어 글자를 지운다
        private void LicenseKeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox box)
                return;

            if (e.Key == Key.Space)
            {
                e.Handled = true;
                _vm.ReportLicenseKeyFormatError();
                return;
            }

            if (box.SelectionLength > 0)
                return;

            int caret = box.CaretIndex;
            if (e.Key == Key.Back && caret > 0 && box.Text[caret - 1] == '-')
                box.CaretIndex = caret - 1;
            else if (e.Key == Key.Delete && caret < box.Text.Length && box.Text[caret] == '-')
                box.CaretIndex = caret + 1;
        }

        //붙여넣기: 영문·숫자만 남겨 넣는다. 걸러낸 문자가 있으면 안내를 띄운다
        private void LicenseKeyBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            e.CancelCommand();

            if (!e.DataObject.GetDataPresent(DataFormats.Text) || sender is not TextBox box)
                return;

            string text = (string)e.DataObject.GetData(DataFormats.Text);
            string filtered = RawLicenseChars(text);

            //하이픈은 자동으로 들어가므로 붙여넣은 하이픈은 걸러도 안내하지 않는다
            if (filtered.Length != text.Count(c => c != '-'))
                _vm.ReportLicenseKeyFormatError();

            if (filtered.Length == 0)
                return;

            int start = box.SelectionStart;
            box.SelectedText = filtered;
            box.SelectionStart = Math.Min(start + filtered.Length, box.Text.Length);
            box.SelectionLength = 0;
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
