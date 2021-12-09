using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Linq;
using System.Xml;
using System.IO;
using System.Data;
using System.ComponentModel;
using Cobra.EM;
using Cobra.Common;
using System.Windows.Threading;
using System.Threading;
using System.Runtime.InteropServices;

namespace Cobra.ProductionPanel
{
    public enum ErrorCode
    {
        Success,
        FileFormatError,
        FileParsingError,
        FileIntegrityError,
        TokenMismatch,
    }
    /// <summary>
    /// MainControl.xaml 的交互逻辑
    /// </summary>
    public partial class MainControl
    {
        #region 变量定义
        public static int session_id = -1;
        private string ProductionSFLName = "";//Issue1426 Leon

        private Device m_parent;
        public Device parent
        {
            get { return m_parent; }
            set { m_parent = value; }
        }

        private string m_SFLname;
        public string ProductionSFLDBName
        {
            get { return m_SFLname; }
            set { m_SFLname = value; }
        }

        private TASKMessage m_Msg = new TASKMessage();
        public TASKMessage msg
        {
            get { return m_Msg; }
            set { m_Msg = value; }
        }

        public GeneralMessage gm = new GeneralMessage("Production SFL", "", 0);

        private string Password = string.Empty;

        private string BinFileName = string.Empty;
        private string MPTFileName = string.Empty;

        public Dictionary<string, string> DBRecord = new Dictionary<string, string>();
        private AsyncObservableCollection<TestGroup> m_TestGroups = new AsyncObservableCollection<TestGroup>();
        public AsyncObservableCollection<TestGroup> TestGroups
        {
            get { return m_TestGroups; }
            set { m_TestGroups = value; }
        }
        private bool TestResult = true;

        private AsyncObservableCollection<TestItem> m_TestItems = new AsyncObservableCollection<TestItem>();
        public AsyncObservableCollection<TestItem> TestItems
        {
            get { return m_TestItems; }
            set
            {
                m_TestItems = value;
            }
        }

        public AsyncObservableCollection<ProcessItem> ProcessItems = new AsyncObservableCollection<ProcessItem>();

        AsyncObservableCollection<Parameter> scanlist = new AsyncObservableCollection<Parameter>();

        public Dictionary<ErrorCode, string> ErrorMessage = new Dictionary<ErrorCode, string>()
        {
            {ErrorCode.Success, "Successful"},
            {ErrorCode.FileFormatError, "File format error!"},
            {ErrorCode.FileParsingError, "File parsing error!"},
            {ErrorCode.FileIntegrityError, "File integrity error!"},
            {ErrorCode.TokenMismatch, "Token mismatch!"},
        };

        public DataTable Records = new DataTable();
        bool isReentrant = false;   //控制Operation button的重入问题

        private UInt16 VerificationTaskID = 0;

        public int TotalCount { get; set; } = 0;
        public int PassedCount { get; set; } = 0;
        public int FailedCount { get; set; } = 0;
        #endregion

        #region 函数定义

        [DllImport("kernel32.dll")]
        public static extern bool Beep(int freq, int duration);
        private void Alarm()
        {
            for (byte i = 0; i < 5; i++)
            {
                Beep(800, 300);
                Beep(500, 300);
            }
        }

        public void Reset()
        {
            string[] keys = DBRecord.Keys.ToArray();
            foreach (string key in keys)
            {
                DBRecord[key] = "";
            }
        }

        public MainControl(object pParent, string name)
        {
            this.InitializeComponent();
            #region 相关初始化
            parent = (Device)pParent;
            if (parent == null) return;

            ProductionSFLName = name;//Issue1426 Leon
            ProductionSFLDBName = "Production";
            if (String.IsNullOrEmpty(ProductionSFLDBName)) return;

            #region Get SFL Names
            #endregion

            #endregion

            #region 初始化Password
            string str_option = String.Empty;
            XmlNodeList nodelist = parent.GetUINodeList(ProductionSFLName);//Issue1426 Leon
            string password = String.Empty;
            foreach (XmlNode node in nodelist)
            {
                str_option = node.Name;
                switch (str_option)
                {
                    case "Password":
                        {
                            Password = node.InnerText;
                            break;
                        }
                }
            }
            #endregion

            msg.PropertyChanged += new PropertyChangedEventHandler(msg_PropertyChanged);
            TestUI.DataContext = TestItems;
            ProcessUI.DataContext = ProcessItems;
            RecordsDataGrid.DataContext = Records;
            InitialUI();
            UpdateUIWithXML();
            PreloadPackFile();      //Issue1828
        }

        private void PreloadPackFile()
        {
            DispatcherTimer t = new DispatcherTimer();  //初始化的时候，LibInfor.m_assembly_list里面的值还没准备好，所以得等几秒钟再来加载
            t.Interval = TimeSpan.FromSeconds(3);
            t.Tick += (sender, e) =>
            {
                t.Stop();
                string packfilepath = "";
                if (GetPackFilePath(ref packfilepath))
                {
                    string filename = Path.GetFileName(packfilepath);
                    LoadPackFile(filename, packfilepath);
                }
            };
            t.Start();
        }
        private bool GetPackFilePath(ref string path)
        {
            string directory = Path.Combine(FolderMap.m_root_folder, "Settings");
            foreach (string p in Directory.GetFiles(directory))
            {
                if (Path.GetExtension(p) == ".pack")
                {
                    path = p;
                    return true;    //若有多个*.pack，实际上只加载第一个找到的
                }
            }
            return false;
        }
        private void UpdateUIWithXML()
        {
            #region Hide or Show Configuration Tab//Issue1272 Leon
            XmlElement root = EMExtensionManage.m_extDescrip_xmlDoc.DocumentElement;
            if (root == null) return;
            XmlNode ProductionNode = root.SelectSingleNode("descendant::Button[@DBModuleName = '" + ProductionSFLDBName + "']");
            XmlElement xe = (XmlElement)ProductionNode;
            string ShowConfig = xe.GetAttribute("ShowConfig");
            if (ShowConfig.ToUpper() == "FALSE")
                CFGContainer.Visibility = System.Windows.Visibility.Collapsed;
            else
            {
                CFGTab.Init(this, ProductionSFLName);
            }
            #endregion

            #region 初始化Verification Button
            string str_option = String.Empty;
            XmlNodeList nodelist = parent.GetUINodeList(ProductionSFLDBName);
            string password = String.Empty;
            foreach (XmlNode node in nodelist)
            {
                str_option = node.Name;
                switch (str_option)
                {
                    case "Verification":
                        {
                            ReadBackCheckButton.Content = node.InnerText;
                            ReadBackCheckButton.Visibility = System.Windows.Visibility.Visible;
                            XmlElement xe1 = (XmlElement)(node);
                            this.VerificationTaskID = Convert.ToUInt16(xe1.GetAttribute("SubTaskID"));
                            break;
                        }
                }
            }
            #endregion
        }

        private void InitialUI()
        {
            parent.db_Manager.NewSession(ProductionSFLName, ref session_id, DateTime.Now.ToString());
            UpdateStatus("Ready", Brushes.GreenYellow, "Please click the load button to proceed.", Brushes.White);
            StatusInitText.Visibility = System.Windows.Visibility.Visible;
            TestInitText.Visibility = System.Windows.Visibility.Visible;
        }
        private void UpdateStatus(string main, Brush mcolor, string sub, Brush scolor)
        {
            MainStatus.Text = main;
            MainStatus.Foreground = mcolor;
            SubStatus.Text = sub;
            SubStatus.Foreground = scolor;
        }
        private void UpdateStatus(string main, string sub)
        {
            MainStatus.Text = main;
            MainStatus.Foreground = Brushes.GreenYellow;
            SubStatus.Text = sub;
            SubStatus.Foreground = Brushes.White;
        }

        public void ShowWarning(string main, string sub)
        {
            Thread t = new Thread(Alarm);
            t.Start();
            UpdateStatus(main, Brushes.Red, sub, Brushes.White);
            PromptWarning(sub);
        }
        private void ShowMessage(string main, string sub)
        {
            UpdateStatus(main, sub);
            PromptMessage(main);
        }

        private void Load_Click(object sender, RoutedEventArgs e)
        {
            ErrorCode ret = ErrorCode.Success;
            #region password
            if (Password != String.Empty)
            {
                PasswordDialog pd = new PasswordDialog();
                if (pd.ShowDialog() == true)
                {
                    if (pd.PasswordBox.Password != Password)
                    {
                        System.Windows.MessageBox.Show("Wrong Password!");
                        return;
                    }
                }
                else
                    return;
            }
            #endregion

            string fullpath = "";
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Title = "Load File";
            openFileDialog.Filter = "Config file (*.pack)|*.pack||";    //Leon Issue1544
            openFileDialog.DefaultExt = "pack";
            openFileDialog.FileName = "default";
            openFileDialog.FilterIndex = 1;
            openFileDialog.RestoreDirectory = true;
            openFileDialog.InitialDirectory = FolderMap.m_currentproj_folder;
            if (openFileDialog.ShowDialog() == true)
            {
                string filename = openFileDialog.SafeFileName;
                FileInfo fi = new FileInfo(filename);
                if (fi.Extension == ".pack")
                {
                    string fullname = openFileDialog.FileName;
                    LoadPackFile(filename, fullname);
                }
            }
        }

        private void LoadPackFile(string filename, string fullname)
        {
            ErrorCode ret = ErrorCode.Success;
            bool hasMPT = false;

            bool needDownload = false;
            bool needTest = false;

            #region Unzip
            string tempfolder = System.IO.Path.Combine(FolderMap.m_currentproj_folder, "TempFolder\\");

            if (Directory.Exists(tempfolder))
                Directory.Delete(tempfolder, true);

            string folderpath = System.IO.Path.GetDirectoryName(fullname);
            GZipResult gret = GZip.Decompress(folderpath, tempfolder, filename);
            if (gret.Errors)
            {
                ShowWarning("Load Failed!", "Unzip package failed.");
                return;
            }
            #endregion

            #region check package
            string[] filenames = Directory.GetFiles(tempfolder);
            foreach (string fn in filenames)
            {
                FileInfo x = new FileInfo(fn);
                if (x.Extension == ".bin")
                {
                    needDownload = true;
                    BinFileName = fn;
                }
                else if (x.Extension == ".mpt")
                {
                    hasMPT = true;
                    MPTFileName = fn;
                }
            }

            FilePath.Content = fullname; //Issue 950
            #endregion


            #region load files
            ret = LoadMPTFile(MPTFileName, ref needTest);
            if (ret != ErrorCode.Success)
            {
                ShowWarning("Load Failed!", ErrorMessage[ret]);
                return;
            }
            if (needDownload)
            {
                //ret = LoadFile(BinFileName, ViewModelTypy.CFG);
                ret = PrepareDownloadData(BinFileName);
                if (ret != ErrorCode.Success)
                {
                    ShowWarning("Load Failed!", ErrorMessage[ret]);
                    return;
                }
            }
            #endregion

            #region UpdateUI

            InitOperationUI("", false);
            InitTestUI(false);

            if (needDownload & needTest)
            {
                InitOperationUI("Download and Test", true);
                InitTestUI(true);
            }
            else if (!needDownload & needTest)
            {
                InitOperationUI("Test", true);
                InitTestUI(true);
            }
            else if (needDownload & !needTest)
            {
                InitOperationUI("Download", true);
                TestInitText.Visibility = System.Windows.Visibility.Visible;
            }

            StatusInitText.Visibility = System.Windows.Visibility.Hidden;
            #endregion

            #region remove temp folder
            try
            {
                Directory.Delete(tempfolder, true);
            }
            catch
            {
                ShowWarning("Load Failed!", "Remove temp folder failed.");
                return;
            }
            #endregion

            #region build Records column
            RecordsDataGrid.DataContext = null;
            Records.Clear();
            Records.Columns.Clear();
            DataColumn col;
            foreach (var pi in ProcessItems)
            {
                col = new DataColumn();
                col.DataType = System.Type.GetType("System.String");
                col.ColumnName = pi.Name;
                col.AutoIncrement = false;
                col.ReadOnly = true;
                col.Unique = false;
                Records.Columns.Add(col);
            }
            foreach (var ti in TestItems)
            {
                col = new DataColumn();
                col.DataType = System.Type.GetType("System.String");
                col.ColumnName = ti.Name;
                col.AutoIncrement = false;
                col.ReadOnly = true;
                col.Unique = false;
                Records.Columns.Add(col);
            }
            RecordsDataGrid.DataContext = Records;
            #endregion

            ShowMessage("Package file loaded.", "Please click " + OperationButtonName.Text + " button to proceed.");
        }

        private ErrorCode LoadMPTFile(string fullpath, ref bool needTest)
        {
            XmlDocument doc = new XmlDocument();
            try
            {
                doc.Load(fullpath);
            }
            catch
            {
                return ErrorCode.FileFormatError;
            }
            XmlElement root = doc.DocumentElement;

            #region Check Chip Name
            var node = root.SelectSingleNode(COBRA_GLOBAL.Constant.CHIP_NAME_NODE);     //Issue 1906
            if (node == null)
                return ErrorCode.FileIntegrityError;
            if (node.InnerText != SharedAPI.GetChipNameFromExtension())
                return ErrorCode.TokenMismatch;
            #endregion
            #region Load ProcessItems
            XmlNode ProcessNode = root.SelectSingleNode("Process");
            ProcessItems.Clear();
            if (ProcessNode != null)
            {
                try
                {
                    ProcessItem pi;
                    foreach (XmlElement xe in ProcessNode.ChildNodes)
                    {
                        pi = new ProcessItem();
                        pi.SubTaskID = Convert.ToByte(xe.GetAttribute("SubTaskID"));
                        pi.Name = xe.InnerText;
                        pi.Color = Brushes.Gray;
                        pi.callback = new ProcessItem.CallBack(Command);
                        ProcessItems.Add(pi);
                    }
                    pi = new ProcessItem();
                    pi.Name = "Mapping";
                    pi.Color = Brushes.Gray;
                    pi.callback = new ProcessItem.CallBack(Mapping);
                    ProcessItems.Add(pi);
                }
                catch
                {
                    return ErrorCode.FileParsingError;
                }
            }
            #endregion

            #region Load TestItems
            XmlNode TestNode = root.SelectSingleNode("Test");
            TestGroups.Clear();
            TestItems.Clear();
            if (TestNode != null)
            {
                try
                {
                    List<byte> GroupIDs = new List<byte>();
                    foreach (XmlElement xe in TestNode.ChildNodes)
                    {
                        TestItem ti = new TestItem();
                        ti.Name = xe.GetAttribute("Name");
                        ti.StandardValue = Convert.ToDouble(xe.GetAttribute("StandardValue"));
                        if (xe.GetAttribute("Tolerance") != string.Empty)
                            ti.Tolerance = Convert.ToDouble(xe.GetAttribute("Tolerance"));
                        ti.GUID = xe.GetAttribute("GUID");
                        ti.Group = Convert.ToByte(xe.GetAttribute("Group"));
                        if (!GroupIDs.Contains(ti.Group))
                            GroupIDs.Add(ti.Group);
                        TestItems.Add(ti);
                    }

                    foreach (var id in GroupIDs)        //创建空TestGroups，其中TestGroup的ID被赋值，其他值无效
                    {
                        TestGroup tg = new TestGroup();
                        tg.GroupID = id;
                        TestGroups.Add(tg);
                    }
                    foreach (var ti in TestItems)       //将TestItems中的项目按Group分类放入TestGroup
                    {
                        TestGroup tmp = GetTestGroupByID(ti.Group);
                        if (tmp != null)
                            tmp.TestItems.Add(ti);
                    }
                }
                catch
                {
                    return ErrorCode.FileParsingError;
                }
            }
            #endregion

            if (TestItems.Count != 0)
            {
                needTest = true;
                ProcessItem pi = new ProcessItem();
                pi.Name = "Test";
                pi.Color = Brushes.Gray;
                pi.callback = new ProcessItem.CallBack(ConductTest);
                ProcessItems.Add(pi);
            }

            return ErrorCode.Success;
        }

        private TestGroup GetTestGroupByID(byte GroupID)
        {
            foreach (TestGroup tg in TestGroups)
            {
                if (tg.GroupID == GroupID)
                {
                    return tg;
                }
            }
            return null;
        }
        private void InitTestUI(bool isEnabled)
        {
            if (isEnabled)
            {
                //foreach (TestGroup tg in TestGroups)
                //{
                //tg.Color = Brushes.Gray;
                foreach (TestItem ti in /*tg.*/TestItems)
                {
                    ti.Color = Brushes.Gray;
                }
                //}
            }
            TestUI.IsEnabled = isEnabled;
            TestInitText.Visibility = System.Windows.Visibility.Hidden;
        }
        private void InitOperationUI(string buttonname, bool isEnabled)
        {

            OperationButtonName.Text = buttonname;
            OperationButton.IsEnabled = isEnabled;
            ReadBackCheckButton.IsEnabled = isEnabled;
        }

        private ErrorCode PrepareDownloadData(string binFileName)
        {
            ErrorCode ret = ErrorCode.Success;
            msg.sm.efusebindata.Clear();
            msg.sm.efusebindata = SharedAPI.LoadBinFileToList(binFileName);
            if(msg.sm.efusebindata.Count == 0)
                ret = ErrorCode.FileParsingError;
            return ret;
        }
        private UInt32 Mapping(ushort sub_task)
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            msg.owner = this;
            msg.gm.sflname = ProductionSFLName;//Issue1426 Leon
            msg.task = TM.TM_BLOCK_MAP;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                return msg.errorcode;
            }
            return ret;
        }
        private UInt32 Command(ushort sub_task)
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            msg.owner = this;
            msg.gm.sflname = ProductionSFLName;//Issue1426 Leon
            msg.task = TM.TM_COMMAND;
            msg.sub_task = sub_task;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                return msg.errorcode;
            }
            return ret;
        }

        private UInt32 ConductTest(ushort sub_task)
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            //ret = boardviewmodel.WriteDevice();
            //if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
            //{
            //    return ret;
            //}
            //MessageBox.Show("Please make sure Board Config are set properly.");
            PromptMessage("Please make sure Board Config are set properly.");       //Issue 2631

            for (byte i = 0; i < 8; i++)
            {
                Thread.Sleep(200);
                ret = SendTestCommand();
                if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                {
                    return ret;
                }
                GetTestResult();
                SaveTestReport();
                if (TestResult)
                    break;
            }
            UpdateTestUI();
            #region move the test report
            string sourcepath = System.IO.Path.Combine(FolderMap.m_currentproj_folder, "TestReport\\Temp\\");
            string targetpath;
            string targetroot;
            if (TestResult)
            {
                targetroot = System.IO.Path.Combine(FolderMap.m_currentproj_folder, "TestReport\\Passed\\");
                targetpath = System.IO.Path.Combine(FolderMap.m_currentproj_folder, "TestReport\\Passed\\" + DateTime.Now.ToString("yyyyMMddHHmmss"));
            }
            else
            {
                targetroot = System.IO.Path.Combine(FolderMap.m_currentproj_folder, "TestReport\\Failed\\");
                targetpath = System.IO.Path.Combine(FolderMap.m_currentproj_folder, "TestReport\\Failed\\" + DateTime.Now.ToString("yyyyMMddHHmmss"));
            }
            if (Directory.Exists(targetpath))
                Directory.Delete(targetpath);
            Directory.CreateDirectory(targetroot);
            Directory.Move(sourcepath, targetpath);
            #endregion

            return ret;
        }
        private UInt32 SendTestCommand()
        {
            scanlist = parent.GetParamLists(ProductionSFLName).parameterlist;//Issue1426 Leon
            msg.task_parameterlist.parameterlist = scanlist;
            msg.task = TM.TM_READ;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                return msg.errorcode;


            msg.task = TM.TM_CONVERT_HEXTOPHYSICAL;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                return msg.errorcode;
            return msg.errorcode;
        }
        private void GetTestResult()
        {
            #region reset ti
            foreach (TestItem ti in TestItems)
            {
                ti.FailedDetail = string.Empty;
                ti.isPassed = null;
                ti.ReadResult = 0;
            }
            #endregion
            TestResult = true;
            foreach (Parameter p in scanlist)
            {
                string GUID = "0x" + p.guid.ToString("X8");
                //foreach (TestGroup tg in TestGroups)
                //{
                //tg.isPassed = true;
                foreach (TestItem ti in /*tg.*/TestItems)
                {
                    if (ti.GUID.Equals(GUID))
                    {
                        ti.ReadResult = p.phydata;
                        if (ti.Tolerance != 0)
                        {
                            if ((ti.StandardValue - ti.Tolerance) < ti.ReadResult && ti.ReadResult < (ti.StandardValue + ti.Tolerance))
                            {
                                ti.isPassed = true;
                            }
                            else
                            {
                                ti.isPassed = false;
                                //tg.isPassed = false;
                                TestResult = false;
                                ti.FailedDetail = ti.ReadResult.ToString();
                            }
                        }
                        else
                        {
                            if (ti.StandardValue == ti.ReadResult)
                            {
                                ti.isPassed = true;
                            }
                            else
                            {
                                ti.isPassed = false;
                                //tg.isPassed = false;
                                TestResult = false;
                                ti.FailedDetail = ti.ReadResult.ToString();
                            }
                        }
                        break;
                    }
                }
                //}
            }
        }
        private void ResetDynamicUI()
        {
            Thread.Sleep(1000);
            this.Dispatcher.Invoke(new Action(() =>
            {
                //foreach (TestGroup tg in TestGroups)
                //{
                foreach (TestItem ti in /*tg.*/TestItems)
                {
                    if (ti != null)
                    {
                        ti.Color = Brushes.Gray;
                    }
                }
                //tg.Color = Brushes.Gray;
                //}
                foreach (var pi in ProcessItems)
                {
                    if (pi != null)
                    {
                        pi.Color = Brushes.Gray;
                    }
                }
                UpdateStatus("Ready", Brushes.GreenYellow, "Please click the " + OperationButtonName.Text + " button to proceed.", Brushes.White);
                OperationGrid.Visibility = System.Windows.Visibility.Visible;
            }));
        }
        private void UpdateTestUI()
        {
            //foreach (TestGroup tg in TestGroups)
            //{
            foreach (TestItem ti in /*tg.*/TestItems)
            {
                if (ti.isPassed == null)
                    ti.Color = Brushes.Gray;
                else if (ti.isPassed == true)
                    ti.Color = Brushes.Green;
                else if (ti.isPassed == false)
                    ti.Color = Brushes.Red;
            }
            /*if (tg.isPassed == null)
                tg.Color = Brushes.Gray;
            else if (tg.isPassed == true)
                tg.Color = Brushes.Green;
            else if (tg.isPassed == false)
                tg.Color = Brushes.Red;*/
            //}
        }
        private void SaveTestReport()
        {
            string filepath = string.Empty;
            string result = string.Empty;
            if (TestResult)
                result = "Passed";
            else
                result = "Failed";
            /*if (TestResult)
                filepath = System.IO.Path.Combine(FolderMap.m_currentproj_folder, "TestReport\\Pass\\");
            else
                filepath = System.IO.Path.Combine(FolderMap.m_currentproj_folder, "TestReport\\Failed\\");*/

            filepath = System.IO.Path.Combine(FolderMap.m_currentproj_folder, "TestReport\\Temp\\");

            if (!Directory.Exists(filepath))
                Directory.CreateDirectory(filepath);

            string filename = filepath + DateTime.Now.ToString("yyyyMMddHHmmssfff") + result + ".csv";
            FileStream file = new FileStream(filename, FileMode.Create);
            StreamWriter sw = new StreamWriter(file);
            string str = "";
            str = "Group,Test Item,GUID,Standard Value,Tolerance,Actual Value,Test Result";
            sw.WriteLine(str);

            foreach (TestGroup tg in TestGroups)
            {
                foreach (TestItem ti in tg.TestItems)
                {
                    str = tg.GroupID.ToString();
                    string testResult = "";
                    if (ti.isPassed == null)
                        testResult = "NA";
                    else if (ti.isPassed == true)
                        testResult = "Pass";
                    else if (ti.isPassed == false)
                        testResult = "Failed";
                    str += "," + ti.Name + "," + ti.GUID + "," + ti.StandardValue.ToString() + "," + ti.Tolerance.ToString() + "," + ti.ReadResult.ToString() + "," + testResult;
                    sw.WriteLine(str);
                }
            }

            sw.Close();
            file.Close();
        }

        private void OperationButton_Click(object sender, RoutedEventArgs e)
        {
            if (isReentrant == false)   //此次点击并没有重入
            {
                isReentrant = true;

                #region initialize UI to inform user that the operation button was clicked and operation is started
                OperationGrid.Visibility = System.Windows.Visibility.Hidden;
                #endregion

                if (parent.bBusy)
                {
                    ShowWarning("Failed!", LibErrorCode.GetErrorDescription(LibErrorCode.IDS_ERR_EM_THREAD_BKWORKER_BUSY));
                    return;
                }
                parent.bBusy = true;

                #region reset pi
                foreach (var pi in ProcessItems)
                {
                    pi.FailedDetail = string.Empty;
                    pi.IsSuccessed = null;
                    pi.Time = string.Empty;
                }
                #endregion

                System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
                stopwatch.Start();
                foreach (var pi in ProcessItems)
                {
                    if (pi.Name == "Test")
                    {
                        UInt32 ret = pi.callback(pi.SubTaskID);
                        pi.Time = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 0).ToString() + "mS";
                        stopwatch.Restart();
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        {
                            pi.IsSuccessed = false;
                            pi.Color = Brushes.Red;
                            pi.FailedDetail = LibErrorCode.GetErrorDescription(ret);
                            ShowWarning(pi.Name + "Failed!", pi.FailedDetail);
                            break;
                        }
                        if (TestResult)
                        {
                            pi.IsSuccessed = true;
                            pi.Color = Brushes.Green;
                            UpdateStatus("Test Passed", "All " + TestItems.Count.ToString() + " test items passed");
                        }
                        else
                        {
                            pi.IsSuccessed = false;
                            pi.Color = Brushes.Red;
                            UInt16 PassedCount = 0;
                            UInt16 FailedCount = 0;
                            UInt16 UntestCount = 0;
                            foreach (var ti in TestItems)
                            {
                                if (ti.isPassed == null)
                                    UntestCount++;
                                else if (ti.isPassed == true)
                                    PassedCount++;
                                else
                                    FailedCount++;
                            }
                            pi.FailedDetail = "Pass: " + PassedCount.ToString() + "\nFail: " + FailedCount.ToString() + "\nUntested: " + UntestCount.ToString();
                            ShowWarning(pi.Name + " Failed!", pi.FailedDetail);
                            break;
                        }
                    }
                    else
                    {
                        UInt32 ret = pi.callback(pi.SubTaskID);
                        pi.Time = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 0).ToString() + "mS";
                        stopwatch.Restart();
                        if (ret == LibErrorCode.IDS_ERR_SUCCESSFUL)
                        {
                            pi.IsSuccessed = true;
                            pi.Color = Brushes.Green;
                            UpdateStatus(pi.Name + "Passed!", "");
                        }
                        else
                        {
                            pi.IsSuccessed = false;
                            pi.Color = Brushes.Red;
                            pi.FailedDetail = LibErrorCode.GetErrorDescription(ret);
                            ShowWarning(pi.Name + " Failed!", pi.FailedDetail);
                            break;
                        }
                    }
                }
                stopwatch.Stop();

                Thread t = new Thread(ResetDynamicUI);
                t.Start();

                #region save record
                #region save to DataGrid
                DataRow row = Records.NewRow();
                foreach (var pi in ProcessItems)
                {
                    string processresult = string.Empty;
                    if (pi.IsSuccessed == null)
                        processresult = "UnReached";
                    else if (pi.IsSuccessed == true)
                        processresult = "PASS " + pi.Time;
                    else if (pi.IsSuccessed == false)
                        processresult = pi.FailedDetail;
                    row[pi.Name] = processresult;
                }
                foreach (var ti in TestItems)
                {
                    string testresult = string.Empty;
                    if (ti.isPassed == null)
                        testresult = "UnTested";
                    else if (ti.isPassed == true)
                        testresult = "PASS";
                    else if (ti.isPassed == false)
                        testresult = ti.FailedDetail;
                    row[ti.Name] = testresult;
                }
                Records.Rows.Add(row);
                #endregion
                #region save to DataBase
                string prLstring = string.Empty;
                foreach (var pi in ProcessItems)
                {
                    string prstring = string.Empty;
                    prstring = pi.Name + ":";
                    if (pi.IsSuccessed == null)
                        prstring += "UnReached";
                    else if (pi.IsSuccessed == true)
                        prstring += "PASS " + pi.Time;
                    else if (pi.IsSuccessed == false)
                        prstring += pi.FailedDetail;
                    prstring += ";";
                    prLstring += prstring;
                }
                string trLstring = string.Empty;
                foreach (var ti in TestItems)
                {
                    string trstring = string.Empty;
                    trstring = ti.Name + ":";
                    if (ti.isPassed == null)
                        trstring += "UnTested";
                    else if (ti.isPassed == true)
                        trstring += "PASS";
                    else if (ti.isPassed == false)
                        trstring += ti.FailedDetail;
                    trstring += ";";
                    trLstring += trstring;
                }
                Reset();
                DBRecord["ProcessResult"] = prLstring;
                DBRecord["TestResult"] = trLstring;
                DBRecord["Time"] = DateTime.Now.ToString("yyyy-MM-dd:hh-mm-ss");
                parent.db_Manager.BeginNewRow(session_id, ProductionSFLDBName);
                #endregion
                #endregion
                #region Show Success Message and update count
                bool ProcessResult = true;
                foreach (var pi in ProcessItems)
                {
                    if (pi.IsSuccessed == false)
                    {
                        ProcessResult = false;
                        break;
                    }
                }
                if (ProcessResult == true)
                {
                    ShowMessage(OperationButtonName.Text + " Complete!", "All " + ProcessItems.Count.ToString() + " processes complete!");
                }

                if (ProcessResult == true)
                {
                    PassedCount += 1;
                    PassedCountLabel.Content = PassedCount;
                }
                else
                {
                    FailedCount += 1;
                    FailedCountLabel.Content = FailedCount;
                }
                TotalCount += 1;
                TotalCountLabel.Content = TotalCount;
                #endregion
                parent.bBusy = false;

                isReentrant = false;
            }
        }


        private void ReadBackCheckButton_Click(object sender, RoutedEventArgs e)
        {
            msg.owner = this;
            msg.gm.sflname = ProductionSFLName;//Issue1426 Leon
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;

            ret = Command(VerificationTaskID);
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                PromptWarning(LibErrorCode.GetErrorDescription(ret));
            }
            else
                PromptMessage("Verification Passed!");  //Issue 1825
        }
        #region 通用控件消息响应
        private void msg_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            TASKMessage msg = sender as TASKMessage;
            switch (e.PropertyName)
            {
                case "controlreq":
                    switch (msg.controlreq)
                    {
                        case COMMON_CONTROL.COMMON_CONTROL_WARNING:
                            {
                                DisplayDEMMessage(msg.gm);
                                break;
                            }
                    }
                    break;
            }
        }
        public void DisplayDEMMessage(GeneralMessage message)     //处理底层传回的信息，GeneralMessage中的message和level要准备好
        {
            WarningPopControl.Dispatcher.Invoke(new Action(() =>
            {
                if (!(bool)WarningCheckBox.IsChecked && message.level < 1)
                    return;
                WarningPopControl.ShowDialog(message.message, message.level);
            }));
        }
        private void DisplaySFLMessage(string message, int level)     //处理SFL的信息
        {
            //WarningPopControl.Dispatcher.Invoke(new Action(() =>
            //{
            if (!(bool)WarningCheckBox.IsChecked && level < 1)
                return;
            WarningPopControl.ShowDialog(message, level);
            //}));
        }
        private void PromptWarning(string message)
        {
            DisplaySFLMessage(message, 2);
        }
        private void PromptMessage(string message)
        {
            DisplaySFLMessage(message, 0);      //Issue951
        }

        public void UnMaskWarning(string warning)
        {
            WarningPopControl.ShowDialog(warning, 2);
        }
        #endregion

        #endregion

    }
}