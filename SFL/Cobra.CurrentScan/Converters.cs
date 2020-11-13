using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Cobra.Common;

namespace Cobra.CurrentScan
{
    class EnableConverter1 : IMultiValueConverter
    {
        public object Convert(object[] value, Type typetarget, object param, CultureInfo culture)
        {
            int Count = (int)value[0];
            bool isChecked = (bool)value[1];
            if (isChecked)
                return false;
            return (Count == 1);
        }
        public object[] ConvertBack(object value, Type[] typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
}
