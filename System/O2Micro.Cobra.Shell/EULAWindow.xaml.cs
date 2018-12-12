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
using O2Micro.Cobra.Common;

namespace O2Micro.Cobra.Shell
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
            string path = FolderMap.m_EULA_file;
            FileStream fs = File.Open(path, FileMode.Open);

            TextRange textRange = new TextRange(EULATextBox.Document.ContentStart, EULATextBox.Document.ContentEnd);
            textRange.Load(fs, DataFormats.Rtf);

            fs.Close();
        }

        private void LayoutRoot_Loaded(object sender, RoutedEventArgs e)
        {
            LoadRtfFile();
        }  
    }
}
