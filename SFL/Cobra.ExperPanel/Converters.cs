using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Globalization;
using Cobra.Common;

namespace Cobra.ExperPanel
{
    [ValueConversion(typeof(byte), typeof(Visibility))]
    public class byte2VisibilityConvert : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            byte yVal = (byte)value;
			Visibility vTgt = Visibility.Visible;
			if (yVal == 0)
            {
				vTgt = Visibility.Collapsed;
            }
            return vTgt;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

	public class Multi32Bits2Width : IMultiValueConverter
	{
		public Object Convert(Object[] value, Type targetType, Object parameter, CultureInfo culture)
		{
			byte yVal = (byte)value[0];
			double dbActual = (double)value[1] / 32;
			double dbWidth = 0;
			if (dbActual == 0)
			{
			}
			else
			{
				if (yVal == 0)
				{
				}
				else
				{
					dbWidth = dbActual * yVal;
				}
			}
			return dbWidth;
		}

		public object[] ConvertBack(object value, Type[] targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
	//[ValueConversion(typeof(byte), typeof(UInt16))]
	public class Multi16Bits2Width : IMultiValueConverter
	{
		public Object Convert(Object[] value, Type targetType, Object parameter, CultureInfo culture)
		{
			byte yVal = (byte)value[0];
			double dbActual = (double)value[1] / 16;
			double dbWidth = 0;
			if (dbActual == 0)
			{
			}
			else
			{
				if (yVal == 0)
				{
				}
				else
				{
					dbWidth = dbActual * yVal;
				}
			}
			return dbWidth;
		}

		public object[] ConvertBack(object value, Type[] targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}

	public class Multi08Bits2Width : IMultiValueConverter
	{
		public Object Convert(Object[] value, Type targetType, Object parameter, CultureInfo culture)
		{
			byte yVal = (byte)value[0];
			double dbActual = 0;
			double dbWidth = 0;
			if (value[1] == null)
			{
			}
			else
			{
				dbActual = (double)value[1] / 8;
				if ((yVal == 0) || (dbActual == 0))
				{
				}
				else
				{
					dbWidth = dbActual * yVal;
				}
			}
			return dbWidth;
		}

		public object[] ConvertBack(object value, Type[] targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}

	public class DataComponentSelector : DataTemplateSelector
	{
		public override DataTemplate SelectTemplate(object item, DependencyObject container)
		{
			FrameworkElement fwelement = container as FrameworkElement;
			if ((fwelement != null) && (item != null) && (item is ExperModel))
			{
				ExperModel myExp = item as ExperModel;
				if (myExp.yRegLength == 0x20)
				{
					return fwelement.FindResource("DtTmpltReg32Bits") as DataTemplate;
				}
				else if (myExp.yRegLength == 0x10)
				{
					return fwelement.FindResource("DtTmpltReg16Bits") as DataTemplate;
				}
				else
				{
					return fwelement.FindResource("DtTmpltReg08Bits") as DataTemplate;
				}
			}
			return base.SelectTemplate(item, container);
		}
	}
	
	public class Bool2BrushConverter : IValueConverter
	{
		public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
		{
			bool boolval = (bool)value;
			Brush brush = null;
			if (boolval)
			{
				brush = new SolidColorBrush(Color.FromRgb(255, 0, 0));
			}
			else
			{
				brush = new SolidColorBrush(Color.FromRgb(0, 0, 0));
			}

			return brush;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}

	public class Bool2BrushBackground : IValueConverter
	{
		public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
		{
			bool boolval = (bool)value;
			Brush brush = null;
			if (boolval)
			{
				brush = new SolidColorBrush(Colors.LightSlateGray);
			}
			else
			{
				brush = new SolidColorBrush(Colors.WhiteSmoke);
			}

			return brush;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
