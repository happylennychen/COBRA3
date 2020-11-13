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
using System.Windows.Shapes;
using System.IO;
using Cobra.Common;
using System.Windows.Resources;

namespace Cobra.Shell
{
    /// <summary>
    /// Interaction logic for EULAWindow.xaml
    /// </summary>
    public partial class EULAWindow : Window
    {
        public EULAWindow()
        {
            InitializeComponent();
        }

       
        private void CancelBtn_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Hide();
            Close();
        }

        private void LoadRtfFile()
        {
#if O2MICRO
            string imagePath = "pack://application:,,,/Cobra.Images;component/Images/O2Micro/SOFTWARE LICENSE AGREEMENT.txt";
#else
            string imagePath = "pack://application:,,,/Cobra.Images;component/Images/BGMoment/SOFTWARE LICENSE AGREEMENT.txt";
#endif
            StreamResourceInfo imageInfo = Application.GetResourceStream(new Uri(imagePath));
            TextRange textRange = new TextRange(EULATextBox.Document.ContentStart, EULATextBox.Document.ContentEnd);
            textRange.Load(imageInfo.Stream, DataFormats.Text);//DataFormats.Rtf);

            imageInfo.Stream.Close();
        }

        private void LayoutRoot_Loaded(object sender, RoutedEventArgs e)
        {
            LoadRtfFile();
        }  
    }
}
