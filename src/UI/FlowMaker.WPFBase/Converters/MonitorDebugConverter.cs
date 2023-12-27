using FlowMaker.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace FlowMaker.Converters
{
    public class MonitorDebugConverter : IMultiValueConverter
    {
        public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
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
