using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Globalization;
using O2Micro.Cobra.Common;

namespace O2Micro.Cobra.SBS4Panel
{
    [ValueConversion(typeof(bool), typeof(Brush))]
    public class Bool2BrushConverter : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            double uval;
            try
            {
                uval = (double)value;
            }
            catch (Exception e)
            {
                uval = 0;
            }
            Brush brush = null;
            switch ((UInt16)uval)
            {
                case 1:
                    brush = Brushes.Red;
                    break;
                case 0:
                    brush = Brushes.Gray;
                    break;
                default:
                    brush = Brushes.Gray;
                    break;
            }
            return brush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class RadioBoolToIntConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double index;
            try
            {
                index = (double)value;
            }
            catch (Exception e)
            {
                index = 0;
            }

            if ((UInt16)index == UInt16.Parse(parameter.ToString()))
                return true;
            else
                return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return null;

            bool usevalue = (bool)value;
            if (usevalue)
                return parameter.ToString();
            return null;

        }
    }
}
