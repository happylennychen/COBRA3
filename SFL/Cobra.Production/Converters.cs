using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Cobra.Common;

namespace Cobra.ProductionPanel
{
    class WidthConverter : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            if (value == null)  //如果值不存在，则直接返回，返回值已不重要，因为后续不会处理
                return "";
            double width = ((double)value - 5 * 6) / 3;
            return width;
        }
        public object ConvertBack(object value, Type typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
}
