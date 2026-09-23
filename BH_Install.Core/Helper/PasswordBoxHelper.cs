using System.Windows;
using System.Windows.Controls;

namespace BH_Install.Core.Helper
{
    //PasswordBox.Password 는 의존 속성이 아니라 바인딩이 안 된다. 첨부 속성으로 양방향 바인딩을 흉내낸다.
    //  <PasswordBox core:PasswordBoxHelper.Attach="True"
    //               core:PasswordBoxHelper.BoundPassword="{Binding PfxPassword, UpdateSourceTrigger=PropertyChanged}" />
    //암호가 문자열로 메모리에 남지만, 뷰모델이 이미 string 으로 다루므로 보호 수준은 같다.
    public static class PasswordBoxHelper
    {
        //true 로 두면 PasswordChanged 를 구독해 BoundPassword 에 반영한다
        public static readonly DependencyProperty AttachProperty = DependencyProperty.RegisterAttached(
            "Attach", typeof(bool), typeof(PasswordBoxHelper), new PropertyMetadata(false, OnAttachChanged));

        public static bool GetAttach(DependencyObject d) => (bool)d.GetValue(AttachProperty);
        public static void SetAttach(DependencyObject d, bool value) => d.SetValue(AttachProperty, value);

        //뷰모델과 양방향으로 묶이는 암호 값
        public static readonly DependencyProperty BoundPasswordProperty = DependencyProperty.RegisterAttached(
            "BoundPassword", typeof(string), typeof(PasswordBoxHelper),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundPasswordChanged));

        public static string GetBoundPassword(DependencyObject d) => (string)d.GetValue(BoundPasswordProperty);
        public static void SetBoundPassword(DependencyObject d, string value) => d.SetValue(BoundPasswordProperty, value);

        //PasswordChanged 안에서 BoundPassword 를 갱신할 때 되돌아오는 갱신을 막는다
        private static readonly DependencyProperty IsUpdatingProperty = DependencyProperty.RegisterAttached(
            "IsUpdating", typeof(bool), typeof(PasswordBoxHelper), new PropertyMetadata(false));

        private static void OnAttachChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not PasswordBox box)
                return;

            if ((bool)e.NewValue)
            {
                box.PasswordChanged += OnPasswordChanged;

                //바인딩이 먼저 적용되어 있으면 그 값을 넣는다
                string bound = GetBoundPassword(box);
                if (!string.IsNullOrEmpty(bound))
                    box.Password = bound;
            }
            else
            {
                box.PasswordChanged -= OnPasswordChanged;
            }
        }

        //뷰모델 → PasswordBox
        private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not PasswordBox box || (bool)box.GetValue(IsUpdatingProperty))
                return;

            string value = (string?)e.NewValue ?? string.Empty;
            if (box.Password != value)
                box.Password = value;
        }

        //PasswordBox → 뷰모델
        private static void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            var box = (PasswordBox)sender;
            box.SetValue(IsUpdatingProperty, true);
            try
            {
                SetBoundPassword(box, box.Password);
            }
            finally
            {
                box.SetValue(IsUpdatingProperty, false);
            }
        }
    }
}
