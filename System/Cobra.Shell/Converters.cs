using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Globalization;
using Cobra.Common;

namespace Cobra.Shell
{
    public class ContentToPathConverter : IValueConverter
    {
        readonly static ContentToPathConverter value = new ContentToPathConverter();
        public static ContentToPathConverter Value
        {
            get { return value; }
        }

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            ContentPresenter cp = (ContentPresenter)value;
            double h = cp.ActualHeight > 10 ? 1.4 * cp.ActualHeight : 10;
            double w = cp.ActualWidth > 10 ? 1.25 * cp.ActualWidth : 10;
            PathSegmentCollection ps = new PathSegmentCollection(4);
            ps.Add(new LineSegment(new Point(1, 0.7 * h), true));
            ps.Add(new BezierSegment(new Point(1, 0.9 * h), new Point(0.1 * h, h), new Point(0.3 * h, h), true));
            ps.Add(new LineSegment(new Point(w, h), true));
            ps.Add(new BezierSegment(new Point(w + 0.6 * h, h), new Point(w + h, 0), new Point(w + h * 1.3, 0), true));
            //return ps; // Fix

            // Fix
            PathFigure figure = new PathFigure(new Point(1, 0), ps, false);
            PathGeometry geometry = new PathGeometry();
            geometry.Figures.Add(figure);

            return geometry;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ContentToMarginConverter : IValueConverter
    {
        readonly static ContentToMarginConverter value = new ContentToMarginConverter();
        public static ContentToMarginConverter Value
        {
            get { return value; }
        }

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return new Thickness(0, 0, -((ContentPresenter)value).ActualHeight, 0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DeviceInforConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            string temp = null;
            switch ((string)parameter)
            {
                case "Status":
                    {        
                        if ((int)value == 0)
                            temp = "Y";
                        else
                            temp = "N";
                    }
                    break;
                case "HWVersion":
                    {
                        if ((string)value == String.Empty)
                            temp = "NA";
                        else
                            temp = (string)value;
                    }
                    break;
                case "FWVersion":
                    {
                        if ((string)value == String.Empty)
                            temp = "NA";
                        else
                            temp = (string)value;
                    }
                    break;
                case "ATEVersion":
                    {
                        if ((int)value == -1)
                            temp = "NA";
                        else
                            temp = String.Format("{0}", value);
                    }
                    break;
                case "Type":
                    {
                        if ((int)value == -1)
                            temp = "NA";
                        else
                            temp = String.Format("{0:x2}", value);
                    }
                    break;
            }
            return temp;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class UInt2BrushConverter : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            bool bval = (bool)value;
            Brush brush = null;
            switch (bval)
            {
                case false:
                    brush = new SolidColorBrush(Color.FromRgb(154, 218, 158));
                    break;
                case true:
                    brush = new SolidColorBrush(Color.FromRgb(225, 85, 85));
                    break;
                default:
                    break;
            }
            return brush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    [ValueConversion(typeof(bool), typeof(Brush))]
    public class Error2BrushConverter : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            bool boolval = (bool)value;
            Brush brush = null;
            switch (boolval)
            {
                case true:
                    brush = new SolidColorBrush(Color.FromRgb(255, 0, 0));
                    break;
                case false:
                    brush = new SolidColorBrush(Color.FromRgb(0, 0, 0));
                    break;
                default:
                    break;
            }
            return brush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    class RightConverter : IValueConverter
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
            double p;
            if (param is string)
                p = double.Parse(param as string);
            else
                p = (double)(param);
            double width = (double)value + p;
            return width;
        }
    }

    class ExpanderConverter : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object param, CultureInfo culture)
        {
            bool isExpanded = (bool)value;
            var style = Application.Current.Resources["expander-shell"] as Style;
            var style1 = Application.Current.Resources["expander-shell1"] as Style;

            if (isExpanded)
                return style1;
            else
                return style;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ItemDataTempSelector : DataTemplateSelector  //ID：784
    {
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            FrameworkElement element = container as FrameworkElement;
            if ((element != null) && (item != null) && (item is DeviceInfor))
            {
                DeviceInfor deviceInfor = item as DeviceInfor;
                if (deviceInfor.mode == 0x00)
                {
                    return element.FindResource("DeviceStatusTemp") as DataTemplate;
                }
                else
                {
                    return element.FindResource("FWDeviceStatusTemp") as DataTemplate;
                }
            }
            return base.SelectTemplate(item, container);
        }
    }


    public class EnableConverter : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            int count = (int)value;
            int type = int.Parse((string)parameter);
            bool IsEnable = false;
            if (type == 0)  //select button
            {
                if (count == 1)
                    IsEnable = true;
                else
                    IsEnable = false;
            }
            else if (type == 1) //delet button
            {
                if (count > 0)
                    IsEnable = true;
                else
                    IsEnable = false;
            }
            return IsEnable;
        }
        public object ConvertBack(object value, Type typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }

    public class LegalConverter : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            bool isHighLighted = (bool)value;	//Issue1289 Leon
            if (isHighLighted)
                return Brushes.Red;
            else
                return Brushes.Black;
        }
        public object ConvertBack(object value, Type typetarget, object param, CultureInfo culture)
        {
            return null;
        }
    }
}
