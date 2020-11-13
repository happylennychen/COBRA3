using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace Cobra.Common
{
    public interface IServices
    {
        UIElement Insert(object pParent, string name);
    }
}
