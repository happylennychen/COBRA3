using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Globalization;
using Cobra.Common;

namespace Cobra.SBSPanel
{
    [ValueConversion(typeof(bool), typeof(Brush))]
    public class Bool2BrushConverter : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            bool boolval = (bool)value;
            Brush brush = null;
            switch (boolval)
            {
                case true:
                    brush = Brushes.Red;
                    break;
                case false:
                    brush = Brushes.Black;
                    break;
                default:
                    brush = Brushes.Black;
                    break;
            }
            return brush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
