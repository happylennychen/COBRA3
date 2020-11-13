using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Windows;
using Microsoft.Research.DynamicDataDisplay.ViewportRestrictions;
using Microsoft.Research.DynamicDataDisplay;

namespace Cobra.ScanPanel
{
    public class OscilloscopeRestriction : ViewportRestrictionBase
    {
        public double Top = -10;
        public double Bottom = 10;
        public double Width = 100;

        private MainControl m_parent;
        public MainControl parent
        {
            get { return m_parent; }
            set { m_parent = value; }
        }

        public override Rect Apply(Rect oldVisible, Rect newVisible, Viewport2D viewport)
        {
            //newVisible.X = newVisible.Right - oldVisible.Right + oldVisible.X;
            DataTable table = parent.logUIdata.logbuf;
            int last = table.Rows.Count - 1;
            if (last != -1)
            {
                DataRow lastRow = table.Rows[last];
                //((DateTime)row["Time"] - (DateTime)table.Rows[0]["Time"]).TotalSeconds
                double w = ((DateTime)lastRow["Time"] - (DateTime)table.Rows[0]["Time"]).TotalSeconds;
                if (w > Width * 0.98)
                {
                    newVisible.X = w - (Width * 0.98);
                }
                else
                    newVisible.X = -(Width*0.02);

                newVisible.Width = Width;
                newVisible.Y = Top;
                newVisible.Height = Bottom - Top;
                return newVisible;
            }

            newVisible.X = 0;
            newVisible.Width = Width;
            newVisible.Y = Top;
            newVisible.Height = Bottom - Top;
            return newVisible;
        }

        public OscilloscopeRestriction(double t, double b, double w, MainControl p)
        {
            Top = t;
            Bottom = b;
            Width = w;
            parent = p;
        }
        public event EventHandler Changed;
    }
}
