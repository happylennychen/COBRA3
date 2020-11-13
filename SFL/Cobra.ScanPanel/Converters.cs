using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Cobra.Common;

namespace Cobra.ScanPanel
{
    class BarConverter : IMultiValueConverter
    {
        public object Convert(object[] value, Type typetarget, object param, CultureInfo culture)
        {
            //string str = value[3].ToString();
            if (Double.IsNaN((Double)value[3]))
            {
                return null;
            }
            /*if (value[0] == null || value[1] == null || value[2] == null)   //如果值不存在，则下限设为0，上限设为5000
            {
                value[0] = (double)5000;
                value[1] = (double)0;
                value[2] = (double)0;
            }*/
            double res;
            if (!Double.TryParse(value[0].ToString(), out res))
                value[0] = (double)5000;
            if (!Double.TryParse(value[1].ToString(), out res))
                value[1] = (double)0;
            if (!Double.TryParse(value[2].ToString(), out res))
                value[2] = (double)0;
            double a, b, y;
            a = ((double)value[3] * 0.80) / ((double)value[0] - (double)value[1]);
            b = ((double)value[3] * 0.1) - a * (double)value[1];
            y = a * (double)value[2] + b;
            if (y < 0)
                y = 0;
            else if (y > (double)value[3])
                y = (double)value[3];
            return y;
        }
        public object[] ConvertBack(object value, Type[] typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
    class CurrentBarWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] value, Type typetarget, object param, CultureInfo culture)
        {
            if (value[0] == null || value[1] == null || value[2] == null)   //如果值不存在，则下限设为0，上限设为5000
            {
                value[0] = (double)5000;
                value[1] = (double)0;
                value[2] = (double)0;
            }
            double a, b, y, r;
            a = ((double)value[3] * 0.80) / ((double)value[0] - (double)value[1]);
            b = ((double)value[3] * 0.1) - a * (double)value[1];

            y = Math.Abs(a * (double)value[2]);


            if (y < 0)
                y = 1;
            else
            {
                r = (double)value[3] - b;
                if ((double)value[2] >= 0)
                {
                    if (y > r)
                        y = r;
                }
                else
                {
                    if (y > b)
                        y = b;
                }
            }
            return y;
        }
        public object[] ConvertBack(object value, Type[] typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
    class CurrentBarLeftConverter : IMultiValueConverter
    {
        public object Convert(object[] value, Type typetarget, object param, CultureInfo culture)
        {
            if (value[0] == null || value[1] == null || value[2] == null)   //如果值不存在，则下限设为0，上限设为5000
            {
                value[0] = (double)5000;
                value[1] = (double)0;
                value[2] = (double)0;
            }
            double a, b, y;
            a = ((double)value[3] * 0.80) / ((double)value[0] - (double)value[1]);
            b = ((double)value[3] * 0.1) - a * (double)value[1];

            if ((double)value[2] >= 0)
            {
                y = b;
            }
            else
            {
                y = a * (double)value[2] + b;
            }

            if (y < 0)
                y = 0;
            else if (y > (double)value[3])
                y = (double)value[3];
            return y;
        }
        public object[] ConvertBack(object value, Type[] typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
    class ColorConverter : IMultiValueConverter
    {
        public object Convert(object[] value, Type typetarget, object param, CultureInfo culture)
        {
            if (value[0] == null || value[1] == null || value[2] == null)   //如果值不存在，则直接返回，返回值已不重要，因为后续不会处理
                return Brushes.LightGray;
            Brush b;
            if ((double)value[0] < (double)value[2] || (double)value[1] > (double)value[2])
            {
                b = Brushes.Red;
            }
            else
                b = Brushes.LightGray;
            if(value[3] != null)
                if ((bool)value[3])
                    b = Brushes.LightGray;
            return b;
        }
        public object[] ConvertBack(object value, Type[] typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
    class TotalVoltageConverter : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            AsyncObservableCollection<CellVoltage> voltageList = new AsyncObservableCollection<CellVoltage>();
            voltageList = (AsyncObservableCollection<CellVoltage>)value;
            double toltalVoltage = 0;
            foreach (CellVoltage c in voltageList)
            {
                if(c.pUsability == false)
                    toltalVoltage += c.pValue;
            }
            string str = toltalVoltage.ToString();
            return str;
        }
        public object ConvertBack(object value, Type typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
    class DeltaVoltageConverter : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            AsyncObservableCollection<CellVoltage> voltageList = new AsyncObservableCollection<CellVoltage>();
            voltageList = (AsyncObservableCollection<CellVoltage>)value;
            double max=0, min = 9999;
            foreach (CellVoltage c in voltageList)
            {
                if (c.pUsability == false)
                {
                    if (max < c.pValue)
                        max = c.pValue;
                    if (min > c.pValue)
                        min = c.pValue;
                }
            }
            string str = (max-min).ToString();
            return str;
        }
        public object ConvertBack(object value, Type typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
    class WidthConverter : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            if (value == null)  //如果值不存在，则直接返回，返回值已不重要，因为后续不会处理
                return "";
            string str;
            decimal num = new decimal((double)value);
            if (num == -999999)
                str = "No Value";
            else
                str = Decimal.Round(num, 1).ToString();
            return str;
        }
        public object ConvertBack(object value, Type typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
    class WidthConverter2 : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object param, CultureInfo culture)
        {
            //double width = (double)value - Int32.Parse(param as string);
            double p;
            if (param is string)
                p = double.Parse(param as string);
            else
                p = (double)(param);
            double width = (double)value - p;
            return width;
        }
        public object ConvertBack(object value, Type typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
    class MidConverter : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object param, CultureInfo culture)
        {
            double width = (double)value / 2 - 3;
            return width;
        }
        public object ConvertBack(object value, Type typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }

    class VoltageTextConverter : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            if (value == null)  //如果值不存在，则直接返回，返回值已不重要，因为后续不会处理
                return "";
            string str;
            decimal num = new decimal((double)value);
            if (num == -999999)
                str = "No Value";
            else
                str = Decimal.Round(num, 1).ToString();
            char[] cs = str.ToArray();
            //c.Join();
            str="";
            foreach (char c in cs)
            {
                str += c + "\n";
            }
            return str;
        }
        public object ConvertBack(object value, Type typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
    class MainWidthConverter : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object param, CultureInfo culture)
        {
            double width = (double)value * Int32.Parse(param as string) / 100;
            return width;
        }
        public object ConvertBack(object value, Type typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
    class HeightConverter : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object param, CultureInfo culture)
        {
            double height = ((int)value + 1) * ((Double)param + 4) - 4;
            return height;
        }
        public object ConvertBack(object value, Type typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
    class CanvasHeightConverter : IMultiValueConverter
    {
        public object Convert(object[] value, Type typetarget, object param, CultureInfo culture)
        {
            double height;
            height = ((double)value[1] + 4) * ((int)value[0] + 0);
            return height;
        }
        public object[] ConvertBack(object value, Type[] typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
    class VolBarWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] value, Type typetarget, object param, CultureInfo culture)
        {
            double height;
            height = 1 * ((double)value[1] - 20 - 10) / ((int)value[0]) - 4; //第一个20是TH的宽度，第二个10是为右边留的一点空间（不留的话数字不能完全显示），4是margin的宽度
            if (height < 20)
                height = 20;
            return height;
        }
        public object[] ConvertBack(object value, Type[] typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
    class TempBarWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] value, Type typetarget, object param, CultureInfo culture)
        {
            double width;
            width = ((double)value[2] - (double)param * 2) / ((int)value[0] + (int)value[1]) - 4; //20是TH的宽度，4是margin的宽度
            if (width < 30)
                width = 30;
            return width;
        }
        public object[] ConvertBack(object value, Type[] typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
    class GlobalMargintConverter : IMultiValueConverter
    {
        public object Convert(object[] value, Type typetarget, object param, CultureInfo culture)
        {
            double height;
            height = 0.2 * (double)value[0] / ((int)value[1] + 3);
            return height;
        }
        public object[] ConvertBack(object value, Type[] typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
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
    class TimerConverter : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object param, CultureInfo culture)
        {
            Visibility v = (value == null) ? Visibility.Hidden : Visibility.Visible;
            return v;
        }
        public object ConvertBack(object value, Type typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
    class FDColorConverter : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object param, CultureInfo culture)
        {
            if (value == null)
                return SystemColors.ControlBrush;
            Brush br = ((bool)value == true) ? SystemColors.GradientActiveCaptionBrush : SystemColors.ControlBrush;
            return br;
        }
        public object ConvertBack(object value, Type typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
    class BleedingConverter : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            Brush br;
            if ((bool?)value == null)
                br = Brushes.Black;
            else if ((bool?)value == false)
                br = Brushes.Black;
            else
                br = Brushes.LightGreen;

            return br;
        }
        public object ConvertBack(object value, Type typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
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
    class EnableConverter2 : IMultiValueConverter
    {
        public object Convert(object[] value, Type typetarget, object param, CultureInfo culture)
        {
            int Count = (int)value[0];
            bool isChecked = (bool)value[1];
            if (isChecked)
                return false;
            return (Count >= 1);
        }
        public object[] ConvertBack(object value, Type[] typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
    class PositionConverter : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object param, CultureInfo culture)
        {
            return ((int)value % 2) + 2;
        }
        public object ConvertBack(object value, Type typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }

    class HeaderConverter : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            AsyncObservableCollection<CellVoltage> voltageList = new AsyncObservableCollection<CellVoltage>();
            voltageList = (AsyncObservableCollection<CellVoltage>)value;
            double toltalVoltage = 0, max = 0, min = 9999;
            foreach (CellVoltage c in voltageList)
            {
                if (c.pUsability == false)
                {
                    toltalVoltage += c.pValue;
                    if (max < c.pValue)
                        max = c.pValue;
                    if (min > c.pValue)
                        min = c.pValue;
                }
            }
            string str = "Voltage Group(mV)                      " + "Total :" + toltalVoltage.ToString() + "   Delta :" + (max - min).ToString();
            return str;
        }
        public object ConvertBack(object value, Type typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
    class ShiftConverter : IMultiValueConverter
    {
        public object Convert(object[] value, Type typetarget, object param, CultureInfo culture)
        {
            double shift;
            shift = ((double)value[1] - (double)value[0]) / 2;
            return shift;
        }
        public object[] ConvertBack(object value, Type[] typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
    class WidthRatioConverter : IMultiValueConverter
    {
        public object Convert(object[] value, Type typetarget, object param, CultureInfo culture)
        {
            double ratio; 
            ratio = (int)value[0] * ((double)value[1] + 4) + (double)param;
            return ratio;
        }
        public object[] ConvertBack(object value, Type[] typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
    class DataColorConverter: IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            SolidColorBrush output;
            if((bool)value)
            {
                output = Brushes.Green;
            }
            else
            {
                output = Brushes.Black;
            }
            return output;
        }
        public object ConvertBack(object value, Type typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
}
