using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using O2Micro.Cobra.Common;

namespace O2Micro.Cobra.MonitorPanel
{
    class BarConverter : IMultiValueConverter
    {
        public object Convert(object[] value, Type typetarget, object param, CultureInfo culture)
        {
            double a, b, y;
            a = 80 / ((double)value[0] - (double)value[1]);
            b = 10 - a * (double)value[1];
            y = a * (double)value[2] + b;
            if (y < 0)
                y = 1;
            else if (y > 100)
                y = 100;
            return y;
        }
        public object[] ConvertBack(object value, Type[] typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
    class LineConverter : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            double length;
            length = (int)value * 18 -7;
            return length;
        }
        public object ConvertBack(object value, Type typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
    class ColorConverter : IMultiValueConverter
    {
        public object Convert(object[] value, Type typetarget, object param, CultureInfo culture)
        {
            Brush b;
            if ((double)value[0] < (double)value[2] || (double)value[1] > (double)value[2])
            {
                b = Brushes.Red;
            }
            else
                b = Brushes.LightGray;
            return b;
        }
        public object[] ConvertBack(object value, Type[] typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
    class TotalConverter : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            AsyncObservableCollection<CellVoltage> voltageList = new AsyncObservableCollection<CellVoltage>();
            voltageList = (AsyncObservableCollection < CellVoltage > )value;
            double toltalVoltage = 0;
            foreach (CellVoltage c in voltageList)
            {
                toltalVoltage += c.pValue;
            }
            string str = "TotalVoltage : "+toltalVoltage.ToString();
            return str;
        }
        public object ConvertBack(object value, Type typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
}
