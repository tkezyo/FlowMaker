//using System;
//using System.Globalization;
//using System.Linq;
//using System.Windows.Data;
//using Volo.Abp.UI.Navigation;

//namespace Ty.Converters
//{
//    public class MenuConverter : IMultiValueConverter
//    {
//        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
//        {
//            if (values[0] is ApplicationMenuItem menuItem && values[1] is not null)
//            {
//                string pageName = values[1].ToString();
//                if (menuItem.Name == pageName)
//                {
//                    return true;
//                }
//                if (menuItem.Items.Any(c => c.Name == pageName))
//                {
//                    return true;
//                }
//            }

//            return false;
//        }

//        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
//        {
//            throw new NotImplementedException();
//        }
//    }
//}
