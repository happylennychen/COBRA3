using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Globalization;

namespace O2Micro.Cobra.ControlLibrary
{
    public class ForegroundConverter : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            int Ival = (int)value;
            Brush brush = null;
            switch (Ival)
            {
                case 0://正常模式下字体颜色
                    brush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    break;
                case 1://设置数据范围越界等
                    brush = new SolidColorBrush(Color.FromRgb(43, 204, 213));
                    break;
                case 2://校验，通讯问题等警告字体颜色
                    brush = new SolidColorBrush(Color.FromRgb(255, 0, 0));
                    break;
                default:
                    break;
            }
            return brush;
        }

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class TitleConverter : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            int Ival = (int)value;
            string strTitle="Warning";
            switch (Ival)
            {
                case 0://正常模式下字体颜色
                    strTitle = "Message";
                    break;
                case 1://设置数据范围越界等
                    strTitle = "Error";
                    break;
                case 2://校验，通讯问题等警告字体颜色
                    strTitle = "Warning";
                    break;
                default:
                    break;
            }
            return strTitle;
        }

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
