using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Globalization;
using Cobra.Common;

namespace Cobra.Update
{
    [ValueConversion(typeof(bool), typeof(string))]
    public class Bool2ContentConverter : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            bool boolval = (bool)value;
            string sContent = string.Empty;
            switch (boolval)
            {
                case true:
                    sContent = "Download";
                    break;
                case false:
                    sContent = "Cancel";
                    break;
                default:
                    break;
            }
            return sContent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class Bool2BrushConverter : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            bool bval = (bool)value;
            Brush brush = null;
            switch (bval)
            {
                case false:
                    brush = new SolidColorBrush(Color.FromRgb(225, 0, 0));
                    break;
                case true:
                    brush = new SolidColorBrush(Color.FromRgb(0, 0, 0));
                    break;
                default:
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