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
using System.ComponentModel;
using System.IO;
using Cobra.Common;
using System.Xml;
using Microsoft.VisualBasic.FileIO;
using System.Windows.Controls.Primitives;

namespace Cobra.Shell
{
    /// <summary>
    /// Interaction logic for ExtensionManager.xaml
    /// </summary>
    public partial class ExtensionManager : Window
    {
        private AsyncObservableCollection<ExtensionFile> ExtensionFiles = new AsyncObservableCollection<ExtensionFile>();
        public string SelectedFileName
        {
            set;
            get;
        }
        private bool IsInDebugMode
        {
            set;
            get;
        }
        public ExtensionManager()
        {
            InitializeComponent();
            InitializeViewModel();
            ExManager.ItemsSource = ExtensionFiles;
            SelectedFileName = "";
            IsInDebugMode = false;
        }

        private void InitializeViewModel()
        {
            DirectoryInfo directory = new DirectoryInfo(FolderMap.m_extensions_folder);
            if (!directory.Exists) return;
            else
            {
                string fullname = FolderMap.m_extension_common_name + FolderMap.m_extension_ext;
                foreach (FileInfo file in directory.GetFiles(fullname))
                {
                    ExtensionFile ef = new ExtensionFile(); //Issue1289
                    UInt32 ret = ef.Init(System.IO.Path.GetFileName(file.Name));
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    {
                        ef.IsLegal = false;
                        ef.Info = LibErrorCode.GetErrorDescription(ret);
                    }
                    else
                    {
                        ef.IsLegal = true;
                    }
                    ExtensionFiles.Add(ef);
                }
                UpdateExtensionsLegality();
            }
        }

        private void UpdateExtensionsLegality()
        {
            int number = ExtensionFiles.Count;  //Issue1289
            for (int i = 0; i < number; i++)
            {
                if (IsInDebugMode)
                    ExtensionFiles[i].IsHighLighted = false;
                else if (ExtensionFiles[i].IsLegal == false)
                    ExtensionFiles[i].IsHighLighted = true;
                else
                    ExtensionFiles[i].IsHighLighted = false;
            }
        }

        private bool IsEqual(ExtensionFile a, ExtensionFile b)
        {
            if ((a.Chip == b.Chip) && (a.Version == b.Version) && (a.Type == b.Type) && (a.Date == b.Date))
                return true;
            else
                return false;
        }

        private void SelectBtn_Click(object sender, RoutedEventArgs e)
        {
            ExtensionFile ef = (ExtensionFile)ExManager.SelectedItem;
            if (ef.IsLegal == false && IsInDebugMode == false)	//Issue1289 Leon
            {
                MessageBox.Show("This OCE is illegal and cannot be loaded!");
                return;
            }
            SelectedFileName = System.IO.Path.GetFileNameWithoutExtension(ef.FileName);
            this.DialogResult = true;
            this.Close();
        }

        private void AddBtn_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            //openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory); ;
            openFileDialog.Filter = "OCE File(*.oce)|*.oce";
            openFileDialog.FilterIndex = 1;
            openFileDialog.Multiselect = true;
            openFileDialog.RestoreDirectory = true;

            List<string> filenames = new List<string>();
            foreach (ExtensionFile ef in ExtensionFiles)
            {
                string filename = ef.FileName;
                filenames.Add(filename);
            }
            if (openFileDialog.ShowDialog() == true)
            {
                List<string> dupfiles = new List<string>();
                foreach (string filename in openFileDialog.FileNames)
                {
                    if (System.IO.Path.GetDirectoryName(filename) == System.IO.Path.GetDirectoryName(FolderMap.m_extensions_folder))
                    {
                        MessageBox.Show("The files in this folder have already been loaded, please choose another folder.");
                        return;
                    }
                    if (filenames.Contains(System.IO.Path.GetFileName(filename)))
                    {
                        dupfiles.Add(filename);
                    }
                    else
                    {
                        string destPath = System.IO.Path.Combine(FolderMap.m_extensions_folder, System.IO.Path.GetFileName(filename));
                        File.Copy(filename, destPath, true);
                        ExtensionFile ef = new ExtensionFile();
                        UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
                        ret = ef.Init(System.IO.Path.GetFileName(filename));
                        if (ret == LibErrorCode.IDS_ERR_SUCCESSFUL) //Issue1289
                        {
                            ef.IsLegal = true;
                        }
                        else
                        {
                            ef.IsLegal = false;
                            ef.Info = LibErrorCode.GetErrorDescription(ret);
                        }
                        ExtensionFiles.Add(ef);
                    }
                } 
                UpdateExtensionsLegality();
                if (dupfiles.Count != 0)
                {
                    string msg = "";
                    foreach (string filename in dupfiles)
                    {
                        msg += System.IO.Path.GetFileName(filename) + ", ";
                    }
                    msg = msg.Remove(msg.Length - 2);
                    if (dupfiles.Count == 1)
                        msg += " is";
                    else
                        msg += " are";
                    msg += " already exist. Are you sure to replace";
                    if (dupfiles.Count == 1)
                        msg += " it?";
                    else
                        msg += " them?";
                    if (MessageBox.Show(msg, "Warning!", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    {
                        foreach (string filename in dupfiles)
                        {
                            string destPath = System.IO.Path.Combine(FolderMap.m_extensions_folder, System.IO.Path.GetFileName(filename));
                            File.Copy(filename, destPath, true);
                        }
                    }
                }
            }
        }

        private void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Do you want to delete the item(s)?", "Warning!", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                List<ExtensionFile> efs = new List<ExtensionFile>();
                foreach (ExtensionFile ef in ExManager.SelectedItems)
                {
                    //File.Delete(System.IO.Path.Combine(FolderMap.m_extensions_folder, ef.FileName));
                    string fn = System.IO.Path.Combine(FolderMap.m_extensions_folder, ef.FileName);
                    FileSystem.DeleteFile(fn, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                    efs.Add(ef);
                }
                foreach (ExtensionFile ef in efs)
                {
                    ExtensionFiles.Remove(ef);
                }
                //UpdateExtensionsLegality();
            }
        }

        private void ExManager_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            string headername = e.Column.Header.ToString();

            //Cancel the column you don't want to generate
            if (headername == "FileName")
            {
                //e.Cancel = true;
                e.Column.Width = new DataGridLength(50, DataGridLengthUnitType.Star);
            }
            if (headername == "Chip")
            {
                e.Cancel = true;
                //e.Column.Width = new DataGridLength(30, DataGridLengthUnitType.Star);
            }
            if (headername == "Version")
            {
                e.Cancel = true;
                //e.Column.Width = new DataGridLength(30, DataGridLengthUnitType.Star);
            }
            if (headername == "Type")
            {
                e.Cancel = true;
                //e.Column.Width = new DataGridLength(10, DataGridLengthUnitType.Star);
            }
            if (headername == "Date")
            {
                e.Cancel = true;
                //e.Column.Width = new DataGridLength(20, DataGridLengthUnitType.Star);
            }
            if (headername == "IsLegal")
            {
                e.Cancel = true;
            }
            if (headername == "Info")
            {
                e.Column.Width = new DataGridLength(50, DataGridLengthUnitType.Star);
            }
            if (headername == "IsHighLighted")
            {
                e.Cancel = true;
            }
        }

        private void DebugOnOff_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton btn = sender as ToggleButton;
            if ((bool)btn.IsChecked)
            {
                if (MessageBox.Show("Are you sure to enter Debug Mode? Debug Mode is used for internal test. In this mode, Cobra may have the chance to work abnormally.", "Warning!", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    IsInDebugMode = true;
                    UpdateExtensionsLegality();
                    btn.Content = "Debug On";
                }
            }
            else
            {
                IsInDebugMode = false;
                UpdateExtensionsLegality();
                btn.Content = "Debug Off";
            }
        }
    }

    public class ExtensionFile : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }


        private bool m_IsLegal = true;
        public bool IsLegal
        {
            get { return m_IsLegal; }
            set
            {
                m_IsLegal = value;
                OnPropertyChanged("IsLegal");
            }
        }

        private bool m_IsHighLighted = false;
        public bool IsHighLighted
        {
            get { return m_IsHighLighted; }
            set
            {
                m_IsHighLighted = value;
                OnPropertyChanged("IsHighLighted");
            }
        }

        private string m_FileName;
        public string FileName
        {
            get { return m_FileName; }
            set
            {
                m_FileName = value;
                OnPropertyChanged("FileName");
            }
        }

        private string m_Chip;
        public string Chip
        {
            get { return m_Chip; }
            set
            {
                m_Chip = value;
                OnPropertyChanged("Chip");
            }
        }

        private string m_Version;
        public string Version
        {
            get { return m_Version; }
            set
            {
                m_Version = value;
                OnPropertyChanged("RecordNumber");
            }
        }

        private string m_Type;
        public string Type
        {
            get { return m_Type; }
            set
            {
                m_Type = value;
                OnPropertyChanged("Type");
            }
        }
        private string m_Date;
        public string Date
        {
            get { return m_Date; }
            set
            {
                m_Date = value;
                OnPropertyChanged("Date");
            }
        }

        private string m_Info;
        public string Info
        {
            get { return m_Info; }
            set
            {
                m_Info = value;
                OnPropertyChanged("Info");
            }
        }

        public UInt32 Init(string filename)
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            ClearMonitorTemp();
            FileName = filename;
            if (filename.Remove(0, filename.Length - 4) != ".oce")
            {
                return LibErrorCode.IDS_ERR_SECTION_OCE_NOT_LOWER;
            }
            GZipResult res = GZip.Decompress(FolderMap.m_extensions_folder, FolderMap.m_extension_monitor_folder, filename);
            if (res.Errors == true)
                return LibErrorCode.IDS_ERR_SECTION_OCE_UNZIP;
            string extxmlfullname1 = FolderMap.m_extension_monitor_folder + FolderMap.m_ext_descrip_xml_name + FolderMap.m_extension_work_ext;
            string extxmlfullname2 = FolderMap.m_extension_monitor_folder + FolderMap.m_dev_descrip_xml_name + FolderMap.m_extension_work_ext;
            if (!File.Exists(extxmlfullname1) || !File.Exists(extxmlfullname2))
            {
                return LibErrorCode.IDS_ERR_SECTION_OCE_LOSE_FILE;
            }
            ret = LoadExtensionInfo(extxmlfullname1);
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                return ret;
            ret = LoadExtensionInfo(extxmlfullname2);
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                return ret;
            return ret;
        }

        private void ClearMonitorTemp()
        {
            //Clear temp folder
            DirectoryInfo directory = new DirectoryInfo(FolderMap.m_extension_monitor_folder);
            if (!directory.Exists)
                directory.Create();
            else
                foreach (FileInfo files in directory.GetFiles(FolderMap.m_extension_common_name))
                    files.Delete();
        }

        private UInt32 LoadExtensionInfo(string filenmae)
        {
            string[] names = new string[2];
            string[] versions = new string[2];
            try
            {
                XmlDocument m_extDescrip_xmlDoc = new XmlDocument();
                m_extDescrip_xmlDoc.Load(filenmae);
                XmlElement root = m_extDescrip_xmlDoc.DocumentElement;
                if (root == null) return LibErrorCode.IDS_ERR_SECTION_OCE_DIS_FILE_ATTRIBUTE;
            }
            catch (System.Exception ex)
            {
                return LibErrorCode.IDS_ERR_SECTION_OCE_DIS_FILE_ATTRIBUTE;
            }
            return LibErrorCode.IDS_ERR_SUCCESSFUL;
        }

    }
}
