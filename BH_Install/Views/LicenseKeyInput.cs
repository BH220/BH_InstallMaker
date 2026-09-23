using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BH_Install.Views
{
    //라이선스 키 TextBox 의 입력 규칙 (첨부 동작).
    //  - 영문·숫자만 받고 4자마다 하이픈을 자동으로 넣어 XXXX-XXXX-XXXX 로 보여준다.
    //  - 하이픈은 표시용이다. 바인딩 값에는 그대로 들어가고 뷰모델이 LicenseKeyRaw 로 걷어낸다.
    //  - 허용되지 않는 입력(특수문자·공백·붙여넣은 잡문자)은 막고 InvalidInputCommand 를 실행해 안내를 띄운다.
    //  - 하이픈 앞뒤에서의 Backspace/Delete 는 하이픈을 건너뛰어 글자를 지운다.
    //
    //  <TextBox local:LicenseKeyInput.IsEnabled="True"
    //           local:LicenseKeyInput.RawLength="12"
    //           local:LicenseKeyInput.InvalidInputCommand="{Binding ReportLicenseKeyFormatErrorCommand}" />
    public static class LicenseKeyInput
    {
        private const int GroupSize = 4;

        // ----- 첨부 속성 -----

        public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
            "IsEnabled", typeof(bool), typeof(LicenseKeyInput), new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject d) => (bool)d.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(DependencyObject d, bool value) => d.SetValue(IsEnabledProperty, value);

        //하이픈을 뺀 영문·숫자 글자 수 상한
        public static readonly DependencyProperty RawLengthProperty = DependencyProperty.RegisterAttached(
            "RawLength", typeof(int), typeof(LicenseKeyInput), new PropertyMetadata(12));

        public static int GetRawLength(DependencyObject d) => (int)d.GetValue(RawLengthProperty);
        public static void SetRawLength(DependencyObject d, int value) => d.SetValue(RawLengthProperty, value);

        //허용되지 않는 입력을 걸러냈을 때 실행할 커맨드 (뷰모델의 안내 문구 표시)
        public static readonly DependencyProperty InvalidInputCommandProperty = DependencyProperty.RegisterAttached(
            "InvalidInputCommand", typeof(ICommand), typeof(LicenseKeyInput), new PropertyMetadata(null));

        public static ICommand? GetInvalidInputCommand(DependencyObject d) => (ICommand?)d.GetValue(InvalidInputCommandProperty);
        public static void SetInvalidInputCommand(DependencyObject d, ICommand? value) => d.SetValue(InvalidInputCommandProperty, value);

        //TextChanged 안에서 Text 를 고칠 때 재진입을 막는 플래그 (TextBox 별로 따로 둔다)
        private static readonly DependencyProperty IsFormattingProperty = DependencyProperty.RegisterAttached(
            "IsFormatting", typeof(bool), typeof(LicenseKeyInput), new PropertyMetadata(false));

        // ----- 연결 -----

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBox box)
                return;

            if ((bool)e.NewValue)
            {
                box.TextChanged += OnTextChanged;
                box.PreviewTextInput += OnPreviewTextInput;
                box.PreviewKeyDown += OnPreviewKeyDown;
                DataObject.AddPastingHandler(box, OnPasting);
            }
            else
            {
                box.TextChanged -= OnTextChanged;
                box.PreviewTextInput -= OnPreviewTextInput;
                box.PreviewKeyDown -= OnPreviewKeyDown;
                DataObject.RemovePastingHandler(box, OnPasting);
            }
        }

        // ----- 입력 처리 -----

        //입력·삭제·붙여넣기 뒤에 항상 XXXX-XXXX 형식으로 다시 맞추고 커서 위치를 보존한다
        private static void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox box || (bool)box.GetValue(IsFormattingProperty))
                return;

            string text = box.Text;
            int maxRaw = GetRawLength(box);
            int rawBeforeCaret = text.Take(box.CaretIndex).Count(char.IsAsciiLetterOrDigit);

            string raw = RawChars(text);
            if (raw.Length > maxRaw)
            {
                raw = raw[..maxRaw];
                rawBeforeCaret = Math.Min(rawBeforeCaret, maxRaw);
            }

            string formatted = Format(raw);
            if (formatted == text)
                return;

            box.SetValue(IsFormattingProperty, true);
            try
            {
                box.Text = formatted;   //바인딩으로 뷰모델에도 반영된다
                int caret = rawBeforeCaret + (rawBeforeCaret > 0 ? (rawBeforeCaret - 1) / GroupSize : 0);
                box.CaretIndex = Math.Min(caret, formatted.Length);
            }
            finally
            {
                box.SetValue(IsFormattingProperty, false);
            }
        }

        //키 입력은 영문·숫자만. 하이픈·특수문자는 막고 안내를 띄운다
        private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is TextBox box && !e.Text.All(char.IsAsciiLetterOrDigit))
            {
                e.Handled = true;
                ReportInvalid(box);
            }
        }

        //띄어쓰기는 막고, 하이픈 앞뒤에서의 Backspace/Delete 는 하이픈을 건너뛰어 글자를 지운다
        private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox box)
                return;

            if (e.Key == Key.Space)
            {
                e.Handled = true;
                ReportInvalid(box);
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
        private static void OnPasting(object sender, DataObjectPastingEventArgs e)
        {
            e.CancelCommand();

            if (sender is not TextBox box || !e.DataObject.GetDataPresent(DataFormats.Text))
                return;

            string text = (string)e.DataObject.GetData(DataFormats.Text);
            string filtered = RawChars(text);

            //하이픈은 자동으로 들어가므로 붙여넣은 하이픈은 걸러도 안내하지 않는다
            if (filtered.Length != text.Count(c => c != '-'))
                ReportInvalid(box);

            if (filtered.Length == 0)
                return;

            int start = box.SelectionStart;
            box.SelectedText = filtered;
            box.SelectionStart = Math.Min(start + filtered.Length, box.Text.Length);
            box.SelectionLength = 0;
        }

        // ----- 도우미 -----

        //영문·숫자만 남기고 대문자로
        private static string RawChars(string text) =>
            new(text.Where(char.IsAsciiLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

        //4자마다 하이픈: ABCDEFGHI → ABCD-EFGH-I
        private static string Format(string raw)
        {
            var groups = new List<string>();
            for (int i = 0; i < raw.Length; i += GroupSize)
                groups.Add(raw.Substring(i, Math.Min(GroupSize, raw.Length - i)));
            return string.Join("-", groups);
        }

        private static void ReportInvalid(TextBox box)
        {
            ICommand? command = GetInvalidInputCommand(box);
            if (command?.CanExecute(null) == true)
                command.Execute(null);
        }
    }
}
