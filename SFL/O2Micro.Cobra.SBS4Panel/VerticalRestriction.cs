using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Windows;
using Microsoft.Research.DynamicDataDisplay.ViewportRestrictions;
using Microsoft.Research.DynamicDataDisplay;

namespace O2Micro.Cobra.SBS4Panel
{
    public class VerticalRestriction : ViewportRestrictionBase
    {
        public double Top = -10;
        public double Bottom = 10;

        public override Rect Apply(Rect oldVisible, Rect newVisible, Viewport2D viewport)
        {
            newVisible.Y = Top;
            newVisible.Height = Bottom - Top;
            return newVisible;
        }

        public VerticalRestriction(double t, double b)
        {
            Top = t;
            Bottom = b;
        }
        public event EventHandler Changed;
    }
}
