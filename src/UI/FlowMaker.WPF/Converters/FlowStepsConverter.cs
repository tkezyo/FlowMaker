using FlowMaker.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;

namespace FlowMaker.Converters
{
    public class FlowStepsConverter : IMultiValueConverter
    {
        public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[1] is IEnumerable<FlowStepViewModel> s && values[0] is FlowStepViewModel step)
            {
                return (step, s);
            }
            return null;
        }


        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
