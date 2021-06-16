using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Data;
using System.ComponentModel;
using System.Windows.Media.Imaging;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using System.Windows.Controls.Ribbon;
using Cobra.EM;
using Cobra.Common;

namespace Cobra.Shell
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : RibbonWindow
    {
        private StartUpWindow m_startup;
        public StartUpWindow startup
        {
            set { m_startup = value; }
            get { return m_startup; }
        }

        #region Process
        private Process m_upgrade;
        public Process upgrade
        {
            set { m_upgrade = value; }
            get { return m_upgrade; }
        }
        #endregion

        /// <summary>
        /// List of Employee Class 
        /// </summary>
        public EMExtensionManage m_EM_Lib = new EMExtensionManage();
        private List<SFLTabControl> m_sfltabcontrol_list = new List<SFLTabControl>();

        public GeneralMessage gm = new GeneralMessage();
        private AsyncObservableCollection<GeneralMessage> m_generalmessage_list = new AsyncObservableCollection<GeneralMessage>();

        private BackgroundWorker m_Upgrade_BackgroundWorker;// 申明后台对象
        public UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
        public MainWindow()
        {
            try
            {
                InitializeComponent();
                #region startup Dialog
                System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
                Thread newWindowThread = new Thread(new ThreadStart(CreateCounterWindowThread));
                newWindowThread.SetApartmentState(ApartmentState.STA);
                newWindowThread.Start();

                gm.PropertyChanged += new PropertyChangedEventHandler(User_DoSomeOperations);
                gm.controls = "Shell";
                gm.message = "start up";
                gm.bupdate = true;
                InformList.ItemsSource = m_generalmessage_list;

                ret = FolderMap.InitFolders();
                if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                {
                    CatchSystemException(LibErrorCode.GetErrorDescription(ret));
                    return;
                }
                LibInfor.Init();
                LibInfor.AssemblyRegister(Assembly.GetExecutingAssembly(), ASSEMBLY_TYPE.SHELL);
                #endregion

                #region EM初始化操作
                Registry.LoadRegistryFile();  //784
                uint r = m_EM_Lib.MonitorExtension(Registry.GetCurExtensionFileName());
                if (r != LibErrorCode.IDS_ERR_SUCCESSFUL)
                {
                    Registry.SaveCurExtensionFileName("");
                }
                if (Registry.GetCurExtensionFileName() != "")
                {
                    Title = Registry.GetCurExtensionFileName();
                    COBRA_GLOBAL.CurrentOCEName = Registry.GetCurExtensionFileName();   //Issue 1577 Leon
                    m_EM_Lib.GetExtensionToken();
                }
                m_EM_Lib.Init();
                m_EM_Lib.gm.PropertyChanged += new PropertyChangedEventHandler(User_DoSomeOperations);

                BuildDeviceConnectSettingControlsGroup();
                UpdateWorkSpacePanel();
                UpdateDeviceInformation();
                InitBWork();
                #endregion
#if O2MICRO
                createShortCut();
#endif
                do
                {
                    Thread.Sleep(10);
                } while (startup == null);
                startup.Dispatcher.BeginInvoke(new Action(() =>
                {
                    startup.Close();
                }));
            }
            catch (System.Exception ex)
            {
                CatchSystemException(ex.Message);
            }
        }

        public void InitBWork()
        {
            //Upgrade worker
            m_Upgrade_BackgroundWorker = new BackgroundWorker(); // 实例化后台对象

            m_Upgrade_BackgroundWorker.WorkerReportsProgress = false; // 设置可以通告进度
            m_Upgrade_BackgroundWorker.WorkerSupportsCancellation = true; // 设置可以取消

            m_Upgrade_BackgroundWorker.DoWork += new DoWorkEventHandler(DoWork);
            m_Upgrade_BackgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(CompletedWork);
        }

        private void CatchSystemException(string str)
        {
            do
            {
                Thread.Sleep(10);
            } while (startup == null);
            startup.Dispatcher.Invoke(new Action(() =>
            {
                startup.Close();
            }));

            ControlLibrary.SelectWindow sw = new ControlLibrary.SelectWindow();
            gm.message = string.Format("{0},Quit or Continue!", str);
            gm.level = 2;
            gm.bupdate = true;
            sw.ShowDialog(gm);
            if (!sw.m_result)
                App.Current.Shutdown();
        }

        private void CreateCounterWindowThread()
        {
            startup = new StartUpWindow();
            startup.ShowDialog();
        }

        //切换工程时清除所有容器
        private void ClearContainer()
        {
            //清除Feature Button List
            FeatureBtnList.ItemsSource = null;
            FeatureBtnList.Items.Clear();

            //清除SFL Panel之Tab List
            m_sfltabcontrol_list.Clear();
        }

        private void UpdateWorkSpacePanel()
        {
            for (int i = 0; i < EMExtensionManage.m_EM_DevicesManage.btnPanelList.Count; i++)
            {
                SFLTabControl obj = new SFLTabControl(i);
                m_sfltabcontrol_list.Add(obj);
            }

            FeatureBtnList.ItemsSource = EMExtensionManage.m_EM_DevicesManage.btnPanelList;
            FeatureBtnList.SelectedIndex = 0;
        }

        private void UpdateDeviceInformation()
        {
            DeviceStatusList.DataContext = EMExtensionManage.m_EM_DevicesManage.deviceinforlist;
        }

        private void AdjustWorkSpacePanel(bool badjust, string devicename)
        {
            int num = 0;
            SFLTabControl tab = null;
            if (badjust)//添加设备
            {
                int index = Registry.GetBusOptionsByName(devicename).DeviceIndex;
                for (; num < m_sfltabcontrol_list.Count; num++)
                {
                    tab = m_sfltabcontrol_list[num];
                    tab.InsertTab(num, devicename);
                }
            }
            else//删除设备
            {
                for (; num < m_sfltabcontrol_list.Count; num++)
                {
                    tab = m_sfltabcontrol_list[num];
                    for (int i = 0; i < tab.tabcontrol.Items.Count; i++)
                    {
                        TabItem item = (TabItem)tab.tabcontrol.Items[i];
                        if (item.Name.Equals(devicename))
                            tab.tabcontrol.Items.Remove(item);
                    }
                }
            }
        }

        private void BuildDeviceConnectSettingControlsGroup()
        {
            DeviceRibbonControlGroup.Children.Clear();
            for (int i = 0; i < Registry.devicenum; i++)
            {
                RibbonCheckBox devicecheckbox = new RibbonCheckBox();
                devicecheckbox.Margin = new Thickness(1, 20, 1, 18);
                devicecheckbox.FontSize = 14;
                devicecheckbox.FontFamily = new FontFamily("Arial");
                devicecheckbox.Label = Registry.busoptionslist[i].Name;
                devicecheckbox.Name = Registry.busoptionslist[i].Name;
                //RegisterName(devicecheckbox.Name, devicecheckbox);
                devicecheckbox.Click += checkDevice_Click;

                Binding DeviceCheckBinding = new Binding();
                DeviceCheckBinding.Source = Registry.busoptionslist[i];
                DeviceCheckBinding.Path = new PropertyPath("DeviceIsCheck");
                DeviceCheckBinding.Mode = BindingMode.TwoWay;
                devicecheckbox.SetBinding(RibbonCheckBox.IsCheckedProperty, DeviceCheckBinding);

                DeviceRibbonControlGroup.Children.Add(devicecheckbox);
            }

            RibbonButton devicebtn = new RibbonButton();
            /*devicebtn.Margin = new Thickness(10, 10, 5, 10);
            devicebtn.FontSize = 12;
            devicebtn.FontFamily = new FontFamily("Arial");*/
            devicebtn.Name = "DeviceBtn";
            devicebtn.Label = "Bus Setting";
            /*devicebtn.Width = 100;
            devicebtn.Height = 25;*/
            devicebtn.Click += DeviceOptionsBtn_Click;
            DeviceRibbonControlGroup.Children.Add(devicebtn);
        }

        private void DeviceOptionsBtn_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // 在此处添加事件处理程序实现。
            if (Registry.busoptionslistview.Count == 0)
            {
                MessageBox.Show("There is no device to configuration,Pleasee check!");
                return;
            }

            RibbonButton btn = (RibbonButton)sender;
            gm.controls = btn.Name;
            gm.message = "Enter Bus Options Windows";
            BusOptionsWindow busoptionswindow = new BusOptionsWindow(this);

            busoptionswindow.Owner = this;
            busoptionswindow.ShowDialog();
        }

        private void checkDevice_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            this.Cursor = Cursors.Wait;
            // 在此处添加事件处理程序实现。
            RibbonCheckBox o = (RibbonCheckBox)sender;
            o.IsEnabled = false;
            if (o.IsChecked == false)
            {
                //检查当前工程是否正在运行，如果正在运行报警
                if (m_EM_Lib.CheckDeviceRun(o.Label))
                {
                    o.IsEnabled = true;
                    this.Cursor = Cursors.Arrow;
                    o.IsChecked = true;
                    gm.message = o.Label + " is running,Please stop firstly!";
                    gm.level = 1;
                    gm.bupdate = true;
                    CallWarningControl(gm);
                    return;
                }

                if (Registry.busoptionslistview.Count == 1)
                {
                    o.IsEnabled = true;
                    this.Cursor = Cursors.Arrow;
                    o.IsChecked = true;
                    gm.message = "You should keep one device on system!";
                    CallWarningControl(gm);
                    return;
                }

                Registry.busoptionslistview.Remove(Registry.GetBusOptionsByName(o.Name));
                m_EM_Lib.AdjustDevice(false, o.Name);
                AdjustWorkSpacePanel(false, o.Name);
                gm.controls = o.Name;
                gm.message = "Uncheck";
            }
            else
            {
                gm.controls = o.Name;
                gm.message = "Checked";
                if (Registry.CheckBusOptionsByNameInListView(o.Name))
                {
                    o.IsEnabled = true;
                    this.Cursor = Cursors.Arrow;
                    return;
                }

                BusOptions device = Registry.GetBusOptionsByName(o.Name);
                Registry.busoptionslistview.Add(device);
                Registry.busoptionslistview.Sort(x => x.DeviceIndex);
                m_EM_Lib.AdjustDevice(true, o.Name);
                AdjustWorkSpacePanel(true, o.Name);
            }
            o.IsEnabled = true;
            this.Cursor = Cursors.Arrow;
        }

        private void Ribbon_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // 在此处添加事件处理程序实现。
            Ribbon tab = (Ribbon)sender;
            RibbonTab tb = (RibbonTab)tab.SelectedItem;

            gm.controls = tb.Name;
            gm.message = "Ribbon Tab Switch";
        }

        private void btnExtensionManager_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            ExtensionManager em = new ExtensionManager();
            em.Owner = this;
            if (em.ShowDialog() == true)
            {
                if (em.SelectedFileName != "")
                {
                    string newprojname = String.Empty;
                    string oldprojname = String.Empty;
                    UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;

                    ControlMessage cmg = new ControlMessage();
                    cmg.message = "Please wait....";
                    cmg.percent = 0;
                    cmg.bshow = true;
                    CallWaitControl(cmg);

                    // 在此处添加事件处理程序实现。
                    RibbonButton btn = (RibbonButton)sender;
                    gm.controls = btn.Name;
                    oldprojname = Registry.GetCurExtensionFileName();

                    //检查当前工程是否正在运行，如果正在运行报警
                    if (m_EM_Lib.CheckDevicesRun())
                    {
                        cmg.bshow = false;
                        CallWaitControl(cmg);
                        gm.message = "Some devices is running,Please stop firstly before switching project!";
                        gm.level = 1;
                        gm.bupdate = true;
                        CallWarningControl(gm);

                        return;
                    }

                    ret = m_EM_Lib.MonitorExtension(em.SelectedFileName); //Add ID:592
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    {
                        cmg.bshow = false;
                        CallWaitControl(cmg);
                        gm.message = LibErrorCode.GetErrorDescription(ret);
                        gm.level = 2;
                        gm.bupdate = true;
                        CallWarningControl(gm);
                        return;
                    }

                    ClearContainer();
                    newprojname = em.SelectedFileName;
                    Registry.SaveCurExtensionFileName(newprojname);
                    FolderMap.m_curextensionfile_name = newprojname;

                    if (!m_EM_Lib.Init())
                    {
                        cmg.bshow = false;
                        CallWaitControl(cmg);
                        gm.message = "Version number does not match between Shell and OCE, please load the correct OCE!";
                        gm.level = 1;
                        gm.bupdate = true;
                        CallWarningControl(gm);
                        return;
                    }
                    BuildDeviceConnectSettingControlsGroup();
                    UpdateWorkSpacePanel();
                    Title = newprojname;
                    COBRA_GLOBAL.CurrentOCEName = newprojname;   //Issue 1577 Leon
                    m_EM_Lib.GetExtensionToken();

                    cmg.bshow = false;
                    CallWaitControl(cmg);
                    gm.message = "Selecte " + newprojname + " project";
                    gm.bupdate = true;
                    return;
                }
            }
        }

        private void btnImport_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory); ;
            openFileDialog.Filter = "OCE File(*.oce)|*.oce";
            openFileDialog.FilterIndex = 1;
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == true)
            {
                string destPath = Path.Combine(FolderMap.m_extensions_folder, Path.GetFileName(openFileDialog.FileName));
                File.Copy(openFileDialog.FileName, destPath, true);
            }
        }

        private void btnSaveAs_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // 在此处添加事件处理程序实现。
            RibbonButton btn = (RibbonButton)sender;

            string fullpath = "";
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            saveFileDialog.Title = "Save General Message";
            saveFileDialog.Filter = "General Message files (*.info)|*.info||";
            saveFileDialog.FileName = "gm";
            saveFileDialog.FilterIndex = 1;
            saveFileDialog.RestoreDirectory = true;
            saveFileDialog.DefaultExt = "info";
            saveFileDialog.InitialDirectory = FolderMap.m_currentproj_folder;
            if (saveFileDialog.ShowDialog() == true)
            {
                fullpath = saveFileDialog.FileName;
                SaveFile(fullpath);
            }
        }

        private void btnAbout_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // 在此处添加事件处理程序实现。
            RibbonButton btn = (RibbonButton)sender;

            gm.controls = btn.Name;
            gm.message = "Enter About Windows";
            AboutWindow aboutwindow = new AboutWindow(this);

            aboutwindow.Owner = this;
            aboutwindow.ShowDialog();
        }

        private void SaveFile(string path)
        {
            string str;
            FileStream fs = new FileStream(@path, FileMode.Create);
            StreamWriter sw = new StreamWriter(fs, Encoding.Default);

            sw.Write("------------------General Message Record------------------");
            foreach (GeneralMessage gm in m_generalmessage_list)
            {
                str = String.Empty + "\r\n";
                str += gm.time + ":" + " ";
                str += gm.controls + ":" + " ";
                str += gm.message + ":" + " ";
                sw.Write(str);
            }
            sw.Write("\r\n" + "------------------End------------------");

            sw.Close();
            fs.Close();
        }

        private void btnClear_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            RibbonButton btn = (RibbonButton)sender;
            m_generalmessage_list.Clear();
        }

        private void FeatureBtnList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int index = FeatureBtnList.SelectedIndex;
            ExtensionStatusPanel.Children.Clear();

            if (index == -1) return; //当切换工程时会引发该事件
            if (m_sfltabcontrol_list.Count == 0) return;
            ExtensionStatusPanel.Children.Add(m_sfltabcontrol_list[index]);
        }

        private void User_DoSomeOperations(object sender, PropertyChangedEventArgs e)
        {
            GeneralMessage gm = sender as GeneralMessage;
            if (String.IsNullOrEmpty(gm.message)) return;

            GeneralMessage info = new GeneralMessage { controls = gm.controls, message = gm.message };
            m_generalmessage_list.Add(info);
        }

        #region 通用控件消息响应
        public void CallWarningControl(GeneralMessage message)
        {
            WarningPopControl.Dispatcher.Invoke(new Action(() =>
            {
                WarningPopControl.ShowDialog(message);
            }));
        }

        public void CallWaitControl(ControlMessage msg)
        {
            WaitPopControl.Dispatcher.Invoke(new Action(() =>
            {
                WaitPopControl.IsBusy = msg.bshow;
                WaitPopControl.Text = msg.message;
                WaitPopControl.Percent = String.Format("{0}%", msg.percent);
            }));
        }
        #endregion
        private void SystemBar_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            MenuItem item = sender as MenuItem;
            if (item.IsChecked)
                BtnStatusPanel.Visibility = Visibility.Collapsed;
            else
                BtnStatusPanel.Visibility = Visibility.Visible;
        }

        private void MiniRibbonBar_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            MenuItem item = sender as MenuItem;

            if (item.IsChecked)
                Ribbon.IsMinimized = true;
            else
                Ribbon.IsMinimized = false;
        }

        private void createShortCut()
        {
            string deskTop = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
            string dirPath = System.Environment.CurrentDirectory;
            string exePath = Assembly.GetExecutingAssembly().Location;
            System.Diagnostics.FileVersionInfo exeInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath);
            if (System.IO.File.Exists(string.Format(@"{0}\COBRA.lnk", deskTop)))
                System.IO.File.Delete(string.Format(@"{0}\COBRA.lnk", deskTop));//删除原来的桌面快捷键方式

            IWshRuntimeLibrary.WshShell shell = new IWshRuntimeLibrary.WshShell();
            IWshRuntimeLibrary.IWshShortcut shortcut = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + "\\" + "COBRA.lnk");
            shortcut.TargetPath = @exePath;         //目标文件
            shortcut.WorkingDirectory = dirPath;    //目标文件夹
            shortcut.WindowStyle = 1;               //目标应用程序的窗口状态分为普通、最大化、最小化【1,3,7】
            shortcut.IconLocation = string.Format(@"{0}\Images\Cobra.ico", dirPath);  //快捷方式图标
            shortcut.Save();
        }

        #region Upgrade异步线程
        private void btnUpgrade_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            btnUpgrade.IsEnabled = false;
            RibbonButton btn = (RibbonButton)sender;
            if (LibInfor.m_bUpgrade)
            {
                gm.message = "Upgrade process is running!";
                gm.level = 2;
                gm.bupdate = true;
                CallWarningControl(gm);
                btnUpgrade.IsEnabled = true;
                return;
            }

            if (ChannelServices.GetChannel("IPC_Server") == null)
            {
                IpcServerChannel channel = new IpcServerChannel("IPC_Server", "CobraServerChannel");
                ChannelServices.RegisterChannel(channel, false);
            }
            m_Upgrade_BackgroundWorker.RunWorkerAsync();
        }

        void DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker bw = sender as BackgroundWorker;
            DirectoryInfo dir = new DirectoryInfo(Path.Combine(FolderMap.m_center_folder, "COBRA", "Upgrade"));
            if (dir.Exists)
            {
                SharedFormula.DeleteFolder(FolderMap.m_upgrade_folder);
                SharedFormula.DirectoryCopy(dir.FullName, FolderMap.m_upgrade_folder, true);
            }
            m_upgrade = Process.Start(FolderMap.m_upgrade_folder + FolderMap.m_upgrade_file + FolderMap.m_upgrade_ext);
            RemotingConfiguration.RegisterWellKnownServiceType(typeof(LibInfor), "LibInfor", WellKnownObjectMode.SingleCall);
            e.Cancel = true;
        }

        void CompletedWork(object sender, RunWorkerCompletedEventArgs e)
        {
            btnUpgrade.IsEnabled = true;
        }
        #endregion

        private void RibbonWindow_Closed(object sender, EventArgs e) //ID:592
        {
            if (!Directory.Exists(FolderMap.m_dem_library_folder)) return;
            Directory.Delete(FolderMap.m_dem_library_folder, true);
        }
    }
}
