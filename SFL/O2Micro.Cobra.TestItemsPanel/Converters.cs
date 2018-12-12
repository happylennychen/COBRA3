using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Data;
using System.Globalization;

namespace O2Micro.Cobra.TestItemsPanel
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
            bool cfgbtn = (bool)value[0];
            bool cfgbox = (bool)value[1];
            bool boardbtn = (bool)value[2];
            bool boardbox = (bool)value[3];
            System.Windows.Visibility boardvisi = (System.Windows.Visibility)value[4];
            bool testonly = (bool)value[5];

            if (boardvisi == System.Windows.Visibility.Visible)
            {
                return !(cfgbtn ^ cfgbox) & boardbtn & boardbox;
            }
            else
            {
                if (testonly)
                    return true;
                else
                    return cfgbtn & cfgbox;
            }
        }
        public object[] ConvertBack(object value, Type[] typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
}
