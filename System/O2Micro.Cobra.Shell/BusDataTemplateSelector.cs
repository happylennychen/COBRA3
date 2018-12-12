using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Reflection;
using O2Micro.Cobra.Common;
using O2Micro.Cobra.EM;

namespace O2Micro.Cobra.Shell
{
    public class BusDataTemplateSelector : DataTemplateSelector
    {
        public DataTemplate SPIBusTemplate { get; set; }
        public DataTemplate I2CBusTemplate { get; set; }
        public DataTemplate I2C2BusTemplate { get; set; }
        public DataTemplate SVIDBusTemplate { get; set; }
        public DataTemplate RS232BusTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item != null)
            {
                switch (Registry.m_BusType)
                {
                    case BUS_TYPE.BUS_TYPE_I2C:
                        return I2CBusTemplate;
                    case BUS_TYPE.BUS_TYPE_I2C2:
                        return I2C2BusTemplate;
                    case BUS_TYPE.BUS_TYPE_SPI:
                        return SPIBusTemplate;
					case BUS_TYPE.BUS_TYPE_SVID:
						return SVIDBusTemplate;
                    case BUS_TYPE.BUS_TYPE_RS232:
                        return RS232BusTemplate;
                }
            }
            return I2CBusTemplate;
        }
    }
}
