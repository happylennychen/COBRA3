using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using Cobra.Common;

namespace Cobra.SBSPanel
{
    /// <summary>
    /// Interaction logic for SBSProjectControl.xaml
    /// </summary>
    public partial class SBSProjectControl : UserControl
    {
        private MainControl m_Parent;
        public MainControl parent { get; set; }

        public SBSProjectControl()
        {
            InitializeComponent(); 
            Visibility = Visibility.Visible;
        }

        public void ShowDialog(Boolean bshow)
        {
            if(bshow)
                Visibility = Visibility.Visible;
            else
                Visibility = Visibility.Hidden;
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            Visibility = Visibility.Hidden;
            parent.wavectrl_init();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Visibility = Visibility.Hidden;
        }

        private void PathBtn_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            string fullpath = "";
            string command = btn.CommandParameter.ToString();
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Title = "Load File";
            openFileDialog.Filter = "Project Configuration files (*.*)|*.*||";
            openFileDialog.FileName = "default";
            openFileDialog.FilterIndex = 1;
            openFileDialog.RestoreDirectory = true;
            openFileDialog.DefaultExt = "";
            openFileDialog.InitialDirectory = FolderMap.m_currentproj_folder;
            openFileDialog.InitialDirectory = "D:\\Cobra code\\SW_Cobra-V20150408\\SW_Cobra\\output\\tables";
            if (openFileDialog.ShowDialog() == true)
            {
                fullpath = openFileDialog.FileName;
                var pmode = from path in parent.viewmode.path_parameterlist where path.btncommand == command select path;
                foreach (PathModel p in pmode)
                {
                    if (p != null)
                    {
                        p.path = fullpath;
                    }
                }
            }            
        }
    }
}
