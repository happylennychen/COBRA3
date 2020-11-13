using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Cobra.Common;

namespace Cobra.CCControlPanel
{
    class IndexConverter : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object param, CultureInfo culture)
        {
            double index = (int)value + 1;
            return index;
        }
        public object ConvertBack(object value, Type typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
}
