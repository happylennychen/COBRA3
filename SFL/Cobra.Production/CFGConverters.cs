using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Data;
using System.Globalization;

namespace Cobra.ProductionPanel
{
    class OrLogicEnableConverter : IMultiValueConverter
    {
        public object Convert(object[] value, Type typetarget, object param, CultureInfo culture)
        {
            foreach (var v in value)
            {
                if ((bool)v)
                    return true;
            }
            return false;
        }
        public object[] ConvertBack(object value, Type[] typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
    class SaveButtonEnableConverter : IMultiValueConverter
    {
        public object Convert(object[] value, Type typetarget, object param, CultureInfo culture)
        {
            bool binbtn = (bool)value[0];
            bool binbox = (bool)value[1];
            bool testonly = (bool)value[2];
            if (testonly)
                return true;
            else
                return binbtn & binbox;
        }
        public object[] ConvertBack(object value, Type[] typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
}