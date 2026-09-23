using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BH_Install.Core.Helper
{
    //정수 값이 ConverterParameter 와 같으면 Visible, 아니면 Collapsed.
    //위저드처럼 "현재 단계 번호" 하나로 여러 패널 중 하나만 보이게 할 때 쓴다.
    //  Visibility="{Binding CurrentStep, Converter={StaticResource StepVis}, ConverterParameter=2}"
    public sealed class IndexToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool match = value is int current
                && int.TryParse(parameter?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int index)
                && current == index;

            return match ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
