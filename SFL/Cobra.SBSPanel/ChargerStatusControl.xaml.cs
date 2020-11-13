using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Cobra.SBSPanel
{
    /// <summary>
    /// Interaction logic for ChargerStatusControl.xaml
    /// </summary>
    public partial class ChargerStatusControl : UserControl
    {
        BitmapImage myBitmapImage = new BitmapImage();
        public ChargerStatusControl()
        {
            InitializeComponent();
            Initial();
        }

        public void Initial()
        {
            lamp1.Source = new BitmapImage(new Uri("Image/Off.png", UriKind.Relative));
            lamp2.Source = new BitmapImage(new Uri("Image/Off.png", UriKind.Relative));
            lamp3.Source = new BitmapImage(new Uri("Image/Off.png", UriKind.Relative));
            lamp4.Source = new BitmapImage(new Uri("Image/Off.png", UriKind.Relative));
            lamp5.Source = new BitmapImage(new Uri("Image/Off.png", UriKind.Relative));
        }

        public void Update(byte bdata)
        {            
            if((bdata & 0x01)!=0)
                lamp1.Source = new BitmapImage(new Uri("Image/On.png",UriKind.Relative));
            else
                lamp1.Source = new BitmapImage(new Uri("Image/Off.png", UriKind.Relative));

            if((bdata & 0x02)!=0)
                lamp2.Source = new BitmapImage(new Uri("Image/On.png", UriKind.Relative));
            else
                lamp2.Source = new BitmapImage(new Uri("Image/Off.png", UriKind.Relative));

            if ((bdata & 0x04) != 0)
                lamp3.Source = new BitmapImage(new Uri("Image/On.png",UriKind.Relative));
            else
                lamp3.Source = new BitmapImage(new Uri("Image/Off.png", UriKind.Relative));
            
            if ((bdata & 0x08) != 0)
                lamp4.Source = new BitmapImage(new Uri("Image/On.png", UriKind.Relative));
            else
                lamp4.Source = new BitmapImage(new Uri("Image/Off.png", UriKind.Relative));

            if ((bdata & 0x10) != 0)
                lamp5.Source = new BitmapImage(new Uri("Image/On.png", UriKind.Relative));
            else
                lamp5.Source = new BitmapImage(new Uri("Image/Off.png", UriKind.Relative));

        }
    }
}
