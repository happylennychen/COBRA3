using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Data;
using System.Globalization;
using System.Windows;

namespace Cobra.TableMaker
{
    class EnableConverter : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            int Count = (int)value;
            return (Count >= 1);
        }
        public object ConvertBack(object value, Type typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
    class VisibilityConverter : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            string str = (string)value;
            if (str == "" || str == null)
                return Visibility.Hidden;
            else
                return Visibility.Visible;
        }
        public object ConvertBack(object value, Type typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
    class EnableConverter2 : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            return !(bool)value;
        }
        public object ConvertBack(object value, Type typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
}
