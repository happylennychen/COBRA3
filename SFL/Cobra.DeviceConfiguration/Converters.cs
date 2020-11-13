using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Globalization;
using Cobra.Common;

namespace Cobra.DeviceConfigurationPanel
{
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class Bool2VisibilityConverter : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            bool boolval = (bool)value;
            Visibility bvisib = Visibility.Visible;
            switch (boolval)
            {
                case true:
                    bvisib = Visibility.Visible;
                    break;
                case false:
                    bvisib = Visibility.Collapsed;
                    break;
                default:
                    break;
            }
            return bvisib;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

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
                    brush = new SolidColorBrush(Color.FromRgb(210, 210, 210));
                    break;
                case false:
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

    [ValueConversion(typeof(bool), typeof(Brush))]
    public class Error2BrushConverter : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            bool boolval = (bool)value;
            Brush brush = null;
            switch (boolval)
            {
                case true:
                    brush = new SolidColorBrush(Color.FromRgb(255, 0, 0));
                    break;
                case false:
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


    //[ValueConversion(typeof(bool), typeof(Brush))]
    public class SliderDigitConverter : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            /*bool boolval = (bool)value;
            Brush brush = null;
            switch (boolval)
            {
                case true:
                    brush = new SolidColorBrush(Color.FromRgb(255, 0, 0));
                    break;
                case false:
                    brush = new SolidColorBrush(Color.FromRgb(0, 0, 0));
                    break;
                default:
                    break;
            }
            return brush;*/
            if (value == null || value == "")
                return value;
            double d = System.Convert.ToDouble(value);
            string str = d.ToString("F1");
            return System.Convert.ToDouble(str);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || value == "")
                return value;
            double d = System.Convert.ToDouble(value);
            string str = d.ToString("F2");
            return System.Convert.ToDouble(str);
        }
    }
}
