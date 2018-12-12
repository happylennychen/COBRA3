//#define debug
using System;
using System.Collections.Generic;
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
using System.Linq;
using System.Xml.Linq;
using System.Xml;
using System.IO;
using System.Data;
using System.ComponentModel;
using O2Micro.Cobra.EM;
using O2Micro.Cobra.Common;
using System.Windows.Controls.Primitives;
using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection;
//using System.Windows.Threading;
//using System.Threading;
using System.Security.Cryptography;
using System.Windows.Forms;
using System.Threading;
using System.Runtime.InteropServices;

namespace O2Micro.Cobra.ProductionPanel
{
    /// <summary>
    /// MainControl.xaml 的交互逻辑
    /// </summary>
    public partial class MainControl
    {
        #region 变量定义
        private string ProductionSFLName = "";
        private static class ProductionRecord
        {
            public static Dictionary<string, string> DBRecord = new Dictionary<string, string>();
            public static void Init(Dictionary<string, DBManager.DataType> columns)
            {
                foreach (string key in columns.Keys)
                {
                    DBRecord[key] = "";
                }
            }
            public static void Reset()
            {
                string[] keys = DBRecord.Keys.ToArray();
                foreach (string key in keys)
                {
                    DBRecord[key] = "";
                }
            }
            public static void Save(string module_name)
            {
                int ret = DBManager.NewRow(module_name, DBRecord);
                if (ret == -1)
                {
                    System.Windows.MessageBox.Show("New Row Failed!");
                }
            }
        }

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

        private string CFGFileName = string.Empty;
        private string BOARDFileName = string.Empty;
        private string MPTFileName = string.Empty;

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

        public enum ErrorCode
        {
            Success,
            FileFormatError,
            FileParsingError,
            FileIntegrityError,
        }
        private Dictionary<ErrorCode, string> ErrorMessage = new Dictionary<ErrorCode, string>()
        {
            {ErrorCode.Success, "Successful"},
            {ErrorCode.FileFormatError, "File format error!"},
            {ErrorCode.FileParsingError, "File parsing error!"},
            {ErrorCode.FileIntegrityError, "File integrity error!"},
        };

        public DataTable Records = new DataTable();
        bool isReentrant = false;   //控制Operation button的重入问题
        #endregion

        #region 函数定义

        [DllImport("kernel32.dll")]
        public static extern bool Beep(int freq, int duration);
        private void Alarm()
        {
            for(byte i=0; i<5; i++)
            {
                Beep(800, 300);
                Beep(500, 300);
            }
        }

        public MainControl(object pParent, string name)
        {
            this.InitializeComponent();
            #region 相关初始化
            parent = (Device)pParent;
            if (parent == null) return;

            ProductionSFLName = name;
            ProductionSFLDBName = "Production";
            if (String.IsNullOrEmpty(ProductionSFLDBName)) return;

            string EFsflname = "";
            string BDsflname = "";
            foreach (var btn in EMExtensionManage.m_EM_DevicesManage.btnPanelList)
            {
                if (btn.btnlabel == "BoardConfig" || btn.btnlabel == "Board Config")
                {
                    BDsflname = btn.btnlabel;
                }
                else if (btn.btnlabel == "EfuseConfig" || btn.btnlabel == "EFUSE Config")
                {
                    EFsflname = btn.btnlabel;
                }
            }

            cfgviewmodel = new SFLViewModel(pParent, this, EFsflname);
            boardviewmodel = new SFLViewModel(pParent, this, BDsflname);
            #endregion

            #region 初始化Password
            string str_option = String.Empty;
            XmlNodeList nodelist = parent.GetUINodeList(ProductionSFLName);
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

            #region CreateTableN

            if (DBManager.supportdb == true)
            {
                Dictionary<string, DBManager.DataType> columns = new Dictionary<string, DBManager.DataType>();
                columns.Add("ProcessResult", DBManager.DataType.TEXT);
                columns.Add("TestResult", DBManager.DataType.TEXT);
                columns.Add("Time", DBManager.DataType.TEXT);
                ProductionRecord.Init(columns);
                int ret = DBManager.CreateTableN(ProductionSFLDBName, columns);
                if (ret != 0)
                    System.Windows.MessageBox.Show("Create Production Table Failed!");
            }
            #endregion

            InitialUI();

            #region Hide or Show Configuration Tab
            XmlElement root = EMExtensionManage.m_extDescrip_xmlDoc.DocumentElement;
            if (root == null) return;
            XmlNode ProductionNode = root.SelectSingleNode("descendant::Button[@DBModuleName = '"+ProductionSFLDBName+"']");
            XmlElement xe = (XmlElement)ProductionNode;
            string ShowConfig = xe.GetAttribute("ShowConfig");
            if (ShowConfig.ToUpper() == "FALSE")
                CFGContainer.Visibility = System.Windows.Visibility.Collapsed;
            else
                CFGTab.Init(this, ProductionSFLName);

            string ShowVerify = xe.GetAttribute("ShowVerify");
            if (ShowVerify.ToUpper() == "FALSE")
                VerificationContainer.Visibility = System.Windows.Visibility.Collapsed;
            #endregion
        }

        private void InitialUI()
        {
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

        private void ShowWarning(string main, string sub)
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
            openFileDialog.Filter = "Config file (*.pack)|*.pack|(*.cfg)|*.cfg||";
            openFileDialog.DefaultExt = "pack";
            openFileDialog.FileName = "default";
            openFileDialog.FilterIndex = 1;
            openFileDialog.RestoreDirectory = true;
            openFileDialog.InitialDirectory = FolderMap.m_currentproj_folder;
            if (openFileDialog.ShowDialog() == true)
            {
                string filename = openFileDialog.SafeFileName;
                FileInfo fi = new FileInfo(filename);
                if (fi.Extension == ".cfg")
                {
                    fullpath = openFileDialog.FileName;
                    ret = LoadFile(fullpath, ViewModelTypy.CFG);
                    if (ret == ErrorCode.Success)
                    {
                        FilePath.Content = fullpath;

                        InitOperationUI("Download", true);
                    }
                    else
                    {
                        ShowWarning("Load Failed!", ErrorMessage[ret]);
                    }
                }
                else if (fi.Extension == ".pack")
                {
                    bool hasBOARD = false;
                    bool hasMPT = false;

                    bool needDownload = false;
                    bool needTest = false;

                    #region Unzip
                    string tempfolder = System.IO.Path.Combine(FolderMap.m_currentproj_folder, "TempFolder\\");

                    if (Directory.Exists(tempfolder))
                        Directory.Delete(tempfolder, true);

                    string folderpath = System.IO.Path.GetDirectoryName(openFileDialog.FileName);
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
                        if (x.Extension == ".cfg")
                        {
                            needDownload = true;
                            CFGFileName = fn;
                        }
                        else if (x.Extension == ".board")
                        {
                            hasBOARD = true;
                            BOARDFileName = fn;
                        }
                        else if (x.Extension == ".mpt")
                        {
                            hasMPT = true;
                            MPTFileName = fn;
                        }
                    }

                    if (
                            (boardviewmodel.dm_parameterlist!=null && 
                            boardviewmodel.dm_parameterlist.parameterlist!= null && 
                            boardviewmodel.dm_parameterlist.parameterlist.Count!=0 && 
                            !hasBOARD) 
                            || 
                            !hasMPT
                        )
                    {
                        ShowWarning("Load Failed!", "Pack file is illegal.");
                        return;
                    }

                    FilePath.Content = openFileDialog.FileName; //Issue 950
                    #endregion


                    #region load files
                    if (hasBOARD)
                    {
                        ret = LoadFile(BOARDFileName, ViewModelTypy.BOARD);
                        if (ret != ErrorCode.Success)
                        {
                            ShowWarning("Load Failed!", ErrorMessage[ret]);
                            return;
                        }
                    }

                    ret = LoadMPTFile(MPTFileName, ref needTest);
                    if (ret != ErrorCode.Success)
                    {
                        ShowWarning("Load Failed!", ErrorMessage[ret]);
                        return;
                    }
                    if (needDownload)
                    {
                        ret = LoadFile(CFGFileName, ViewModelTypy.CFG);
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
            }
        }
        public enum ViewModelTypy
        {
            CFG,
            BOARD
        }
        public ErrorCode LoadFile(string fullpath, ViewModelTypy vmt)
        {
            double dval = 0.0;
            string tmp;
            SFLParameterModel model = null;

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
            StringBuilder sb = new StringBuilder();
            string hash = "";
            try
            {
                for (XmlNode xn = root.FirstChild; xn is XmlNode; xn = xn.NextSibling)
                {
                    //tmp = xn.Name.Replace("H","0x");
                    //selfid = Convert.ToUInt32(tmp, 16);
                    string name = xn.Attributes[0].Value;
                    if (name == "MD5")
                    {
                        hash = xn.InnerText;
                        continue;
                    }
                    //if (sflname == "BoardConfig")
                    //if (name.Contains("NEG"))
                    //name = name.Replace("NEG", "-");      //neg for negative
                    sb.Append(name);
                    if(vmt == ViewModelTypy.CFG)
                        model = cfgviewmodel.GetParameterByName(name);
                    else if (vmt == ViewModelTypy.BOARD)
                        model = boardviewmodel.GetParameterByName(name);
                    if (model == null) continue;

                    model.berror = false;

                    tmp = xn.InnerText;
                    sb.Append(tmp);
                    if (model.brange)//为正常录入浮点数
                    {
                        switch (model.format)
                        {
                            case 0: //Int     
                            case 1: //float1
                            case 2: //float2
                            case 3: //float3
                            case 4: //float4
                                {
                                    if (!Double.TryParse(tmp, out dval))
                                        dval = 0.0;
                                    break;
                                }
                            case 5: //Hex
                            case 6: //Word
                                {
                                    try
                                    {
                                        dval = (Double)Convert.ToInt32(tmp, 16);
                                    }
                                    catch (Exception e)
                                    {
                                        dval = 0.0;
                                        break;
                                    }
                                    break;
                                }
                            default:
                                break;
                        }
                        model.data = dval;
                    }
                    else
                        model.sphydata = tmp;
                }
            }
            catch
            {
                return ErrorCode.FileParsingError;
            }
            using (MD5 md5Hash = MD5.Create())
            {
                if (VerifyMd5Hash(md5Hash, sb.ToString(), hash))
                {
                    return ErrorCode.Success;
                }
                else
                {
                    return ErrorCode.FileIntegrityError;
                }
            }
            //StatusLabel.Content = fullpath;
        }
        private string GetMd5Hash(MD5 md5Hash, string input)
        {

            // Convert the input string to a byte array and compute the hash.
            byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));

            // Create a new Stringbuilder to collect the bytes
            // and create a string.
            StringBuilder sBuilder = new StringBuilder();

            // Loop through each byte of the hashed data 
            // and format each one as a hexadecimal string.
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            // Return the hexadecimal string.
            return sBuilder.ToString();
        }
        // Verify a hash against a string.
        private bool VerifyMd5Hash(MD5 md5Hash, string input, string hash)
        {
            // Hash the input.
            string hashOfInput = GetMd5Hash(md5Hash, input);

            // Create a StringComparer an compare the hashes.
            StringComparer comparer = StringComparer.OrdinalIgnoreCase;

            if (0 == comparer.Compare(hashOfInput, hash))
            {
                return true;
            }
            else
            {
                return false;
            }
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
            XmlNode TestNode = root.SelectSingleNode("Test");
            XmlNode ProcessNode = root.SelectSingleNode("Process");

            #region Load ProcessItems
            ProcessItems.Clear();
            if (ProcessNode != null)
            {
                try
                {
                    ProcessItem pi = new ProcessItem();
                    pi.Name = "Prepare Download Data";
                    pi.Color = Brushes.Gray;
                    pi.callback = new ProcessItem.CallBack(PrepareDownloadData);
                    ProcessItems.Add(pi);
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
        }

        private UInt32 PrepareDownloadData(ushort sub_task)
        {
            msg.task_parameterlist.parameterlist = cfgviewmodel.dm_parameterlist.parameterlist;
            msg.owner = this;
            msg.gm.sflname = ProductionSFLName;
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;

            #region Database New Log
            if (DBManager.supportdb == true)
            {
                string timestamp = DateTime.Now.ToString();
                int log_id = -1;
                int r = DBManager.NewLog(ProductionSFLDBName, "Production Log", timestamp, ref log_id);
                if (r != 0)
                {
                    System.Windows.MessageBox.Show("New Production Log Failed!");
                    return ret;
                }
            }
            #endregion

            ret = boardviewmodel.WriteDevice();
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                return ret;
            }
            ret = cfgviewmodel.WriteDevice();
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                return ret;
            }
            msg.brw = false;
            msg.percent = 10;
            msg.task = TM.TM_SPEICAL_GETDEVICEINFOR;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                return msg.errorcode;
            }

            msg.task = TM.TM_SPEICAL_GETREGISTEINFOR;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                return msg.errorcode;
            }

            msg.task = TM.TM_READ;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                return msg.errorcode;
            }

            msg.task = TM.TM_CONVERT_PHYSICALTOHEX;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                return msg.errorcode;
            }
            return ret;
        }
        private UInt32 Mapping(ushort sub_task)
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            msg.owner = this;
            msg.gm.sflname = ProductionSFLName;
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
            msg.gm.sflname = ProductionSFLName;
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
            ret = boardviewmodel.WriteDevice();
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                return ret;
            }

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
            scanlist = parent.GetParamLists(ProductionSFLName).parameterlist;
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

            string filename = filepath + DateTime.Now.ToString("yyyyMMddHHmmssfff") +result+ ".csv";
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

                #region Database New Log
                if (DBManager.supportdb == true)
                {
                    string timestamp = DateTime.Now.ToString();
                    int log_id = -1;
                    int r = DBManager.NewLog(ProductionSFLDBName, "Production Log", timestamp, ref log_id);
                    if (r != 0)
                        System.Windows.MessageBox.Show("New Production Log Failed!");
                }
                #endregion

                ProductionRecord.Reset();
                ProductionRecord.DBRecord["ProcessResult"] = prLstring;
                ProductionRecord.DBRecord["TestResult"] = trLstring;
                ProductionRecord.DBRecord["Time"] = DateTime.Now.ToString("yyyy-MM-dd:hh-mm-ss");
                ProductionRecord.Save(ProductionSFLDBName);
                #endregion
                #endregion
                #region Show Success Message
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
                #endregion
                parent.bBusy = false;

                isReentrant = false;
            }
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
                WarningPopControl.ShowDialog(message,level);
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
        #endregion

        #region DM提供的API
#if false
        public uint Command(ParamContainer pc, ushort subtask)
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_COMMAND;
            msg.sub_task = subtask;
            msg.task_parameterlist = pc;
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            return m_Msg.errorcode;
        }
        public uint Read(ParamContainer pc)
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_READ;
            msg.task_parameterlist = pc;
            //msg.bupdate = false;            //需要从chip读数据
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            /*if (m_Msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                msg.bgworker.Dispose();
                msg.bgworker.CancelAsync();
            }*/
            return m_Msg.errorcode;
        }
        public uint Write(ParamContainer pc)
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_WRITE;
            msg.task_parameterlist = pc;
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            return m_Msg.errorcode;
        }
        public uint ConvertHexToPhysical(ParamContainer pc)
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_CONVERT_HEXTOPHYSICAL;
            msg.task_parameterlist = pc;
            //msg.bupdate = true;         //不用从chip读，只从img读
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            return m_Msg.errorcode;
        }
        public uint GetSysInfo()
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_SPEICAL_GETSYSTEMINFOR;
            //msg.bupdate = false;            //需要从chip读数据
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            return m_Msg.errorcode;
        }
        public uint GetDevInfo()
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_SPEICAL_GETDEVICEINFOR;
            //msg.bupdate = false;            //需要从chip读数据
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            return m_Msg.errorcode;
        }
        public uint ClearBit(ParamContainer pc)
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_BITOPERATION;
            msg.task_parameterlist = pc;
            //msg.bupdate = false;            //需要从chip读数据
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            return m_Msg.errorcode;
        }
#endif
        #endregion

        #endregion

    }
}