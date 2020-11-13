using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.Composition;
using System.Windows;
using System.Reflection;
using Cobra.Common;

namespace Cobra.DeviceConfigurationPanel
{
    [Export(typeof(IServices))]
    [Serializable]
    public class Services : IServices
    {
        public UIElement Insert(object pParent, string name)
        {
            return new MainControl(pParent, name);
        }
    }
}
