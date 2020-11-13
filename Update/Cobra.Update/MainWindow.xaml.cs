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
using System.Diagnostics;
using System.Net;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Threading;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using Cobra.Common;

namespace Cobra.Update
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private LibInfor _talk = null;

        private ViewMode m_viewmode;
        public ViewMode viewmode
        {
            get { return m_viewmode; }
            set { m_viewmode = value; }
        }

        private string m_mainprocess_name;
        public string mainprocess_name
        {
            get { return m_mainprocess_name; }
            set { m_mainprocess_name = value; }
        }

        internal ObservableCollection<FileModel> m_oceList = new ObservableCollection<FileModel>();
        internal ObservableCollection<FileModel> m_sysList = new ObservableCollection<FileModel>();
        public GeneralMessage gm = new GeneralMessage("Cobra Upgrade Window", "", 0);

        private IpcClientChannel channel = new IpcClientChannel();
        private const long MAX_INTVAL = 30000;
        public MainWindow()
        {
            InitializeComponent();
            viewmode = new ViewMode(); 
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ChannelServices.RegisterChannel(channel, false);

            try
            {
                _talk = (LibInfor)Activator.GetObject(typeof(LibInfor), "ipc://CobraServerChannel/LibInfor");
                FolderMap.InitFolders(_talk.GetAssemblyByType(ASSEMBLY_TYPE.SHELL).Assembly_Path);
                m_mainprocess_name = _talk.GetCurrentProcess().ProcessName;
                _talk.upgradeIsRun(true);
            }
            catch (Exception e1)
            {
                FolderMap.InitFolders();
                m_mainprocess_name = FolderMap.m_root_folder + "\\output\\Cobra.Shell.exe";
            }
            Registry.LoadRegistryFile();
            DeleteFolder(FolderMap.m_center_folder);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            ChannelServices.UnregisterChannel(channel);
            _talk.upgradeIsRun(false);
            this.Cursor = Cursors.Arrow;
        }

        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            ServerConnectWindow serverConwindow = new ServerConnectWindow(this);
            serverConwindow.dtgrdSetting.ItemsSource = viewmode.connectSetList;
            serverConwindow.Owner = this;
            serverConwindow.ShowDialog();
        }

        private void btnFileDownload_Click(object sender, RoutedEventArgs e)
        {
            string urlAddress = string.Empty;
            Button btntmp = sender as Button;
            FileModel releasefileSingle = btntmp.DataContext as FileModel;
            if (releasefileSingle.webClient.IsBusy)
            {
                releasefileSingle.webClient.CancelAsync();
                releasefileSingle.bcancel = true;
                return;
            }
            releasefileSingle.vsiProgressbar = Visibility.Visible;
            releasefileSingle.strProgressText = "0%";
            releasefileSingle.iProgressValue = 0;
            releasefileSingle.vsiDescription = Visibility.Hidden;
            releasefileSingle.bcancel = false;

            string downloadFile = System.IO.Path.Combine(FolderMap.m_center_folder, releasefileSingle.strFullName);
            if (m_viewmode.strServerPort.Length != 0)
                urlAddress = m_viewmode.strServerIPAddr + ":" + m_viewmode.strServerPort + releasefileSingle.m_downloadCenter.dwnfilelink.url;
            else
                urlAddress = m_viewmode.strServerIPAddr + releasefileSingle.m_downloadCenter.dwnfilelink.url;
            Uri URL = urlAddress.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? new Uri(urlAddress) : new Uri("https://" + urlAddress);

            //releasefileSingle.webClient.DownloadFileAsync(URL, downloadFile);
            releasefileSingle.webClient.DownloadFileAsyncWithTimeout(URL, downloadFile, null);
        }

        private void btnReStart_Click(object sender, RoutedEventArgs e)
        {
            m_oceList.Clear();
            m_sysList.Clear();
            try
            {
                foreach (FileModel fm in viewmode.myFileList)
                {
                    if (!fm.bDownloadable) continue; //不能下载文件判定
                    if (!fm.bcancel) //正在下载文件判定
                    {
                        gm.message = "Please wait to restart,File is downloading!";
                        gm.level = 2;
                        gm.bupdate = true;
                        CallWarningControl(gm);
                        return;
                    }
                    if (!fm.bdownload) continue;//没有下载文件判定
                    switch (fm.versionInfo.Assembly_Type)
                    {
                        case ASSEMBLY_TYPE.OCE:
                            m_oceList.Add(fm);
                            break;
                        case ASSEMBLY_TYPE.SHELL:
                            m_sysList.Add(fm);
                            break;
                    }
                }

                if ((m_oceList.Count == 0) && (m_sysList.Count == 0))
                {
                    gm.message = "There is no file to be upgraded!";
                    gm.level = 2;
                    gm.bupdate = true;
                    CallWarningControl(gm);
                    return;
                }

                if (m_oceList.Count != 0)
                {
                    foreach (FileModel fm in m_oceList)
                    {
                        if (fm.versionInfo.Assembly_Path == string.Empty) break;
                        File.Copy(System.IO.Path.Combine(FolderMap.m_center_folder, fm.strFullName), System.IO.Path.Combine(fm.versionInfo.Assembly_Path, fm.strFullName), true);
                    }
                }
                //顺序请勿颠倒
                if (m_sysList.Count != 0)
                {
                    foreach (FileModel fm in m_sysList)
                    {
                        ZipHelper.UnZip(System.IO.Path.Combine(FolderMap.m_center_folder, fm.strFullName), FolderMap.m_center_folder);
                        CloseMainProcess();
                        DeleteFolder(FolderMap.m_parent_folder, Registry.skipDeleteFolderPath_List);
                        DirectoryCopy(System.IO.Path.Combine(FolderMap.m_center_folder,fm.strFileName, "COBRA"), FolderMap.m_parent_folder, true);
                        StartMainProcess();
                    }
                }
                else
                {
                    CloseMainProcess();
                    StartMainProcess();
                }
            }
            catch (System.Exception ex1)
            {
                FolderMap.WriteFile(ex1.Message);
            }
        }

        #region 本地升级函数
        public void Connect()
        {
            bool bRet = true;
            m_viewmode.initializeAutoUpdateList();
            try
            {
                foreach (VersionInfo vi in _talk.GetAssemblyList())
                {
                    if ((vi.Assembly_Type == ASSEMBLY_TYPE.SHELL) | (vi.Assembly_Type == ASSEMBLY_TYPE.OCE))
                    {
                        m_viewmode.addModuleNameVersionToList(vi);
                    }
                }
            }
            catch (System.Exception ex)
            {
                m_viewmode.addModuleNameVersionToList(new VersionInfo("COBRA System", "SWUCOBRA", new Version("2.0.0.0"), ASSEMBLY_TYPE.SHELL));
                m_viewmode.addModuleNameVersionToList(new VersionInfo("OCKL10", "OCEKL10Y", new Version("1.9.0.0"), ASSEMBLY_TYPE.OCE));
            }

            bRet = m_viewmode.checkServerWithList();
            if (bRet)
                lstboxFileAutoUpdate.ItemsSource = m_viewmode.myFileList;
        }

        private void CloseMainProcess()
        {
            //1.关闭COBRA系统
            try
            {
                Process[] p = Process.GetProcessesByName(m_mainprocess_name);
                foreach (Process ps in p)
                {
                    ps.CloseMainWindow();
                    if (ps.HasExited) continue;
                    ps.Kill();
                    ps.WaitForExit(1000);
                }
            }
            catch (Exception e)
            {
                FolderMap.WriteFile(e.Message);
            }
        }

        private void StartMainProcess()
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo(System.IO.Path.Combine(FolderMap.m_parent_folder, mainprocess_name + ".exe")); 
                Process.Start(info);
                Process.GetCurrentProcess().CloseMainWindow();
                if (Process.GetCurrentProcess().HasExited) return;
                Process.GetCurrentProcess().Kill();
                Process.GetCurrentProcess().WaitForExit(1000);
            }
            catch (System.Exception ex)
            {
                FolderMap.WriteFile(ex.Message);
            }
        }
        #endregion

        #region 通用控件消息响应
        public void CallWarningControl(GeneralMessage message)
        {
            WarningPopControl.Dispatcher.Invoke(new Action(() =>
            {
                WarningPopControl.ShowDialog(message);
            }));
        }

        public bool CallSelectControl(GeneralMessage message)
        {
            bool bcancel = false;
            SelectPopControl.Dispatcher.Invoke(new Action(() =>
            {
                bcancel = SelectPopControl.ShowDialog(message);
            }));
            return bcancel;
        }
        #endregion

        #region 通用算法
        /// <summary>
        /// 删除文件夹及其内容
        /// </summary>
        /// <param name="dir"></param>
        public void DeleteFolder(string dir, ObservableCollection<string> skpdir = null)
        {
            foreach (string d in Directory.GetFileSystemEntries(dir))
            {
                if (File.Exists(d))
                {
                    FileInfo fi = new FileInfo(d);
                    if (fi.Attributes.ToString().IndexOf("ReadOnly") != -1)
                        fi.Attributes = FileAttributes.Normal;
                    File.Delete(d);//直接删除其中的文件  
                }
                else
                {
                    DirectoryInfo d1 = new DirectoryInfo(d);
                    if (skpdir != null)
                    {
                        if (skpdir.IndexOf(d1.FullName) != -1)
                            continue;
                    }
                    if (d1.GetFiles().Length + d1.GetDirectories().Length != 0)
                    {
                        DeleteFolder(d1.FullName, skpdir);////递归删除子文件夹
                    }
                    Directory.Delete(d);
                }
            }
        }

        public void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs, ObservableCollection<string> skpdir = null)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);
            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException("Source directory does not exist or could not be found: " + sourceDirName);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = System.IO.Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, true);
            }

            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    if (skpdir != null)
                    {
                        if (skpdir.IndexOf(subdir.FullName) != -1)
                            continue;
                    }
                    string temppath = System.IO.Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, temppath, copySubDirs,skpdir);
                }
            }
        }
        #endregion
    }
}
