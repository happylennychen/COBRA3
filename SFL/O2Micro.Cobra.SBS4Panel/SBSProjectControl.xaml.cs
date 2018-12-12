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
using O2Micro.Cobra.Common;

namespace O2Micro.Cobra.SBS4Panel
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
            uint temp = 0;
			UInt32 uErrCode = LibErrorCode.IDS_ERR_SUCCESSFUL;

            if (parent.viewmode.folder_path == "")
            {
                MessageBox.Show("Please choose project folder first!");
                return;
            }
            //if (System.IO.Directory.GetFiles(parent.viewmode.folder_path).Length < 7)
            if (System.IO.Directory.GetFiles(parent.viewmode.folder_path).Length < (int)SettingFlag.FileNum)    //(M151002)Francis, use defined enum in Project.cs, instead of digi-number
            {
                MessageBox.Show("Missing Files!");
                return;
            }
            string[] pathlist = System.IO.Directory.GetFiles(parent.viewmode.folder_path);
            foreach(string path in pathlist)
            {
                if (path.Contains("Project"))
                {
                    parent.viewmode.path_parameterlist[0].path = path;
                    temp |= 0x01;
                }
                else if (path.Contains("OCVbyTSOC") || path.Contains("OCVbySOC"))
                {
                    parent.viewmode.path_parameterlist[1].path = path;
                    temp |= 0x02;
                }
                else if (path.Contains("SOCbyOCV") || path.Contains("TSOCbyOCV"))
                {
                    parent.viewmode.path_parameterlist[2].path = path;
                    temp |= 0x04;
                }
                else if (path.Contains("RC"))
                {
                    parent.viewmode.path_parameterlist[3].path = path;
                    temp |= 0x08;
                }
                else if (path.Contains("Thermal"))
                {
                    parent.viewmode.path_parameterlist[4].path = path;
                    temp |= 0x10;
                }
                else if (path.Contains("Selfdis"))
                {
                    parent.viewmode.path_parameterlist[5].path = path;
                    temp |= 0x20;
                }
                else if (path.Contains("Reoc") || path.Contains("Roec"))
                {
                    parent.viewmode.path_parameterlist[6].path = path;
                    temp |= 0x40;
                }
				else if (path.Contains("Charge") || path.Contains("charge"))
				{
					parent.viewmode.path_parameterlist[7].path = path;
					//temp |= 0x80;
				}
				if ((temp == 0x7f) ||(temp == 0x7E))
                    break;
            }
            if ((temp != 0x7f) && (temp != 0x7E))
            {
                MessageBox.Show("Missing Files!");
                return;
            }

			//(M150902)Francis, assign path_list, call GasGauge static function to check table avaliable
            parent.path_list.Clear();
            foreach (PathModel spath in parent.viewmode.path_parameterlist)
            {
                if (spath.path != String.Empty)
                    parent.path_list.Add(spath.path);
            }

			if (GasGaugeProject.CheckTableFiles(ref uErrCode, parent.path_list, false, true))
			{
				Visibility = Visibility.Hidden;
				parent.wavectrl_init();
			}
			else
			{
				MessageBox.Show(LibErrorCode.GetErrorDescription(uErrCode));
			}
			//(E150902)
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            parent.viewmode.folder_path = "";
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
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            //openFileDialog.InitialDirectory = "D:\\Cobra code\\SW_Cobra-V20150512\\SW_Cobra\\output\\tables";
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

        private void BrowseBtn_Click(object sender, RoutedEventArgs e)
        {
            //string fullpath = "";
            System.Windows.Forms.FolderBrowserDialog fbDialog = new System.Windows.Forms.FolderBrowserDialog();
            fbDialog.Description = "Choose Project Files' Folder";
            fbDialog.RootFolder = Environment.SpecialFolder.DesktopDirectory;
            if (fbDialog.ShowDialog() == System.Windows.Forms.DialogResult.Cancel)
                return;

            parent.viewmode.folder_path = fbDialog.SelectedPath;

            //if (System.IO.Directory.GetFiles(parent.viewmode.folder_path).Length < 7)
            if (System.IO.Directory.GetFiles(parent.viewmode.folder_path).Length < (int)SettingFlag.FileNum)    //(M151002)Francis, use defined enum in Project.cs, instead of digi-number
            {
                MessageBox.Show("Missing Files!");
                return;
            }
            prjfolder.Text = parent.viewmode.folder_path;
            //pVM.OutPutfolder = fbDialog.SelectedPath;
            /*openFileDialog.Title = "Load File";
            openFileDialog.Filter = "Project Configuration files (*.*)|*.*||";
            openFileDialog.FileName = "default";
            openFileDialog.FilterIndex = 1;
            openFileDialog.RestoreDirectory = true;
            openFileDialog.DefaultExt = "";
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            //openFileDialog.InitialDirectory = "D:\\Cobra code\\SW_Cobra-V20150512\\SW_Cobra\\output\\tables";
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
            }    */      

        }
    }
}
