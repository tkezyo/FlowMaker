using Avalonia.Data.Converters;
using FlowMaker.ViewModels;
using System.Globalization;

namespace FlowMaker.Converters
{
    public class MonitorDebugConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values[0] is MonitorInfoViewModel info && values[1] is MonitorStepInfoViewModel stepInfo)
            {
                return (info, stepInfo);
            }
            return null;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return [];
        }
    }
}
