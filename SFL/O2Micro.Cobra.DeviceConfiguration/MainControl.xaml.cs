using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using Microsoft.Win32;
using System.Linq;
using System.Xml.Linq;
using System.IO;
using System.Reflection;
using System.ComponentModel;
using System.Globalization;
using System.Xml;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using O2Micro.Cobra.EM;
using O2Micro.Cobra.Common;
using O2Micro.Cobra.AutoMationTest;

namespace O2Micro.Cobra.DeviceConfigurationPanel
{
    enum editortype
    {
        TextBox_EditType = 0,
        ComboBox_EditType = 1,
        CheckBox_EditType = 2
    }

    /// <summary>
    /// MainControl.xaml 的交互逻辑
    /// </summary>
    public partial class MainControl : ISFLLib//SFLBaseClass
    {
        //父对象保存
        private Device m_parent;
        public Device parent
        {
            get { return m_parent; }
            set { m_parent = value; }
        }

        private string m_SFLname;
        public string sflname
        {
            get { return m_SFLname; }
            set { m_SFLname = value; }
        }

        private bool m_NoMapping = false;
        public bool NoMapping
        {
            get { return m_NoMapping; }
            set { m_NoMapping = value; }
        }

        private TASKMessage m_Msg = new TASKMessage();
        public TASKMessage msg
        {
            get { return m_Msg; }
            set { m_Msg = value; }
        }

        private SFLViewMode m_viewmode;
        public SFLViewMode viewmode
        {
            get { return m_viewmode; }
            set { m_viewmode = value; }
        }

        private BackgroundWorker m_BackgroundWorker;// 申明后台对象
        private ControlMessage m_CtrlMg = new ControlMessage();

        public bool border = false; //是否采用Order排序模式
        public GeneralMessage gm = new GeneralMessage("Device Configuration SFL", "", 0);

        private UIConfig m_UI_Config = new UIConfig();
        public  UIConfig ui_config
        {
            get { return m_UI_Config; }
            set { m_UI_Config = value; }
        }

        private ListCollectionView GroupedCustomers = null;
        #region C-HFile
        private List<string> lineList = new List<string>();
        #endregion
        private Dictionary<string, string> BCImg = new System.Collections.Generic.Dictionary<string, string>();

        public MainControl(object pParent, string name)
        {
            this.InitializeComponent();
            #region 相关初始化
            parent = (Device)pParent;
            if (parent == null) return;

            sflname = name;
            if (String.IsNullOrEmpty(sflname)) return;

            InitalUI();
            LibInfor.AssemblyRegister(Assembly.GetExecutingAssembly(), ASSEMBLY_TYPE.SFL);
            InitBWork();
            gm.PropertyChanged += new PropertyChangedEventHandler(gm_PropertyChanged);
            msg.PropertyChanged += new PropertyChangedEventHandler(msg_PropertyChanged);
            msg.gm.PropertyChanged += new PropertyChangedEventHandler(gm_PropertyChanged);

            viewmode = new SFLViewMode(pParent, this);

            if (sflname == "BoardConfig" || sflname == "Board Config")//support them both in COBRA2.00.15, so all old and new OCEs will work fine.
            {
                SaveBoardConfigToInternalMemory();
            }

            PasswordPopControl.SetParent(mDataGrid);
            WarningPopControl.SetParent(mDataGrid);
            WaitPopControl.SetParent(mDataGrid);
            #endregion

            #region 初始化NoMapping
            XmlElement root = EMExtensionManage.m_extDescrip_xmlDoc.DocumentElement;
            XmlNode xn = root.SelectSingleNode("descendant::Button[@Label = '" + sflname + "']");
            XmlElement xe = (XmlElement)xn;
            string nomapping = xe.GetAttribute("NoMapping").ToUpper();
            if (nomapping == "TRUE")
                NoMapping = true;
            #endregion

            if (border)
            {
                var queryResults = viewmode.sfl_parameterlist.OrderBy(model => model.order).ThenBy(model => model.guid).Select(model => model);
                IEnumerable<IGrouping<string, SFLModel>> groups = viewmode.sfl_parameterlist.GroupBy(model => model.catalog);
                foreach (IGrouping<string, SFLModel> grp in groups)
                {
                    grp.OrderBy(model => model.order);
                }
                AsyncObservableCollection<SFLModel> newsfllist = new AsyncObservableCollection<SFLModel>();
                foreach (var n in queryResults)
                {
                    newsfllist.Add(n);
                }
                GroupedCustomers = new ListCollectionView(newsfllist);
            }
            else
                GroupedCustomers = new ListCollectionView(viewmode.sfl_parameterlist);
            GroupedCustomers.GroupDescriptions.Add(new PropertyGroupDescription("catalog"));
            mDataGrid.ItemsSource = GroupedCustomers;
        }

        public void InitalUI()
        {
            string name = String.Empty;
            bool bdata = false;
            UInt16 wdata = 0;
            XmlNodeList nodelist = parent.GetUINodeList(sflname);
            if (nodelist == null) return;

            foreach (XmlNode node in nodelist)
            {
                if (node.Attributes["Name"] == null) continue;
                name = node.Attributes["Name"].Value.ToString();
                switch (name)
                {
                    case "layout":
                        {
                            if (node.Attributes["bOrder"] != null)
                            {
                                if (bool.TryParse(node.Attributes["bOrder"].Value.ToString(), out bdata))
                                    border = bdata;
                                else
                                    border = false;
                            }

                            foreach (XmlNode sub in node)
                            {
                                if (sub.Attributes["Name"] == null) continue;
                                if (sub.Attributes["IsEnable"] == null) continue;
                                btnControl btCtrl = new btnControl();
                                btCtrl.btn_name = sub.Attributes["Name"].Value.ToString();
                                if (Boolean.TryParse(sub.Attributes["IsEnable"].Value.ToString(), out bdata))
                                    btCtrl.benable = bdata;
                                else
                                    btCtrl.benable = true;

                                //Leon add Visibility Property control here
                                if (sub.Attributes["Visibility"] != null)
                                {
                                    switch (sub.Attributes["Visibility"].Value)
                                    {
                                        case "Collapsed":
                                            btCtrl.visi = Visibility.Collapsed; 
                                            break;
                                        case "Hidden":
                                            btCtrl.visi = Visibility.Hidden; 
                                            break;
                                        case "Visible":
                                            btCtrl.visi = Visibility.Visible;
                                            break;
                                        default:
                                            btCtrl.visi = Visibility.Visible; 
                                            break;
                                    }
                                }
                                else
                                    btCtrl.visi = Visibility.Visible;
                                //Leon add Visibility Property control here

                                foreach (XmlNode subxn in sub.ChildNodes)
                                {
                                    XmlElement xe = (XmlElement)subxn;
                                    subMenu sm = new subMenu();
                                    System.Windows.Controls.MenuItem btn_cm_mi = new System.Windows.Controls.MenuItem();

                                    sm.header = xe.GetAttribute("Name");
                                    if (UInt16.TryParse(xe.GetAttribute("SubTask"), System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out wdata))
                                        sm.subTask = wdata;
                                    else
                                        sm.subTask = 0;

                                    btCtrl.btn_menu_control.Add(sm);
                                    btn_cm_mi.Header = sm.header;
                                    btn_cm_mi.CommandParameter = sm.subTask;
                                    btn_cm_mi.Click += MenuItem_Click;
                                    btCtrl.btn_cm.Items.Add(btn_cm_mi);
                                }
                                System.Windows.Controls.Button btn = BottomPanel.FindName(btCtrl.btn_name) as System.Windows.Controls.Button;
                                if (btn != null) btn.DataContext = btCtrl;

                                ui_config.btn_controls.Add(btCtrl);
                            }
                            break;
                        }
                }
            }
            if (sflname == "BoardConfig" || sflname == "Board Config")//support them both in COBRA2.00.15, so all old and new OCEs will work fine.	//Issue686
            {
                WriteBtn.Content = "Apply";
                ReadBtn.Content = "Reset";
            }
        }

        public void InitBWork()
        {
            m_BackgroundWorker = new BackgroundWorker(); // 实例化后台对象

            m_BackgroundWorker.WorkerReportsProgress = true; // 设置可以通告进度
            m_BackgroundWorker.WorkerSupportsCancellation = true; // 设置可以取消

            m_BackgroundWorker.DoWork += new DoWorkEventHandler(DoWork);
            m_BackgroundWorker.ProgressChanged += new ProgressChangedEventHandler(UpdateProgress);
            m_BackgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(CompletedWork);
        }

        private void gm_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            parent.gm = (GeneralMessage)sender;
        }

        private void msg_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            TASKMessage msg = sender as TASKMessage;
            switch (e.PropertyName)
            {
                case "controlreq":
                    switch (msg.controlreq)
                    {
                        case COMMON_CONTROL.COMMON_CONTROL_PASSWORD:
                            {
                                CallPasswordControl(msg.controlmsg);
                                break;
                            }
                        case COMMON_CONTROL.COMMON_CONTROL_SELECT:
                            {
                                CallSelectControl(msg.gm);
                                break;
                            }
                        case COMMON_CONTROL.COMMON_CONTROL_WARNING:
                            {
                                CallWarningControl(msg.gm);
                                break;
                            }

                        case COMMON_CONTROL.COMMON_CONTROL_WAITTING:
                            {
                                CallWaitControl(msg.controlmsg);
                                break;
                            }
                    }
                    break;
            }
        }

        private void LoadBtn_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            string fullpath = "";
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            if (sflname == "BoardConfig" || sflname == "Board Config")//support them both in COBRA2.00.15, so all old and new OCEs will work fine.
            {
                openFileDialog.Title = "Load Board Config file";			//Support Production SFL, Leon
                openFileDialog.Filter = "Board Config file (*.board)|*.board||";
                openFileDialog.DefaultExt = "board";
            }
            else
            {
                openFileDialog.Title = "Load Configuration File";
                openFileDialog.Filter = "Device Configuration file (*.cfg)|*.cfg||";
                openFileDialog.DefaultExt = "cfg";
            }
            openFileDialog.FileName = "default";
            openFileDialog.FilterIndex = 1;
            openFileDialog.RestoreDirectory = true;
            openFileDialog.InitialDirectory = FolderMap.m_currentproj_folder;
            if (openFileDialog.ShowDialog() == true)
            {
                if (parent == null) return;
                {
                    fullpath = openFileDialog.FileName;
                    LoadFile(fullpath);
                }
            }
            else
                return;

            if (sflname == "BoardConfig" || sflname == "Board Config")//support them both in COBRA2.00.15, so all old and new OCEs will work fine.    //Issue1373
            {
                SaveBoardConfigFilePath(fullpath);
            }
        }

        private string GetChipName()
        { 
            XmlElement root = EMExtensionManage.m_extDescrip_xmlDoc.DocumentElement;
            return root.GetAttribute("chip");
        }

        private string GetMD5Code()
        {

            StringBuilder sb = new StringBuilder();
            foreach (SFLModel model in viewmode.sfl_parameterlist)
            {
                if (model == null) continue;
                string name = model.nickname;
                sb.Append(name);
                string strval;
                switch (model.editortype)
                {
                    case 0:
                        {
                            strval = model.sphydata;
                            break;
                        }
                    case 1:
                    case 2:
                        {
                            strval = String.Format("{0:F1}", model.data);
                            break;
                        }
                    default:
                        strval = model.sphydata;
                        break; ;
                }
                sb.Append(strval);
            }
            string hash;
            using (MD5 md5Hash = MD5.Create())
            {
                hash = GetMd5Hash(md5Hash, sb.ToString());
            }
            return hash.Substring(hash.Length - 5);
        }

        public void SaveBoardConfigFilePath(string fullpath)
        {
            string settingfilepath = System.IO.Path.Combine(FolderMap.m_currentproj_folder,"settings.xml");
            FileStream file = new FileStream(settingfilepath, FileMode.Create);
            StreamWriter sw = new StreamWriter(file);
            sw.WriteLine("<?xml version=\"1.0\"?>");
            sw.WriteLine("<root>");
            sw.WriteLine("</root>");
            sw.Close();
            file.Close();
            XmlDocument doc = new XmlDocument();
            doc.Load(settingfilepath);
            XmlElement root = doc.DocumentElement;

            XmlElement item = doc.CreateElement("BoardConfigFileName");
            XmlText filepath = doc.CreateTextNode(fullpath);
            root.AppendChild(item);
            item.AppendChild(filepath);

            doc.Save(settingfilepath);
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            string fullpath = "";
            string chipname = GetChipName();    //Issue1373
            string MD5Code = GetMD5Code();
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            saveFileDialog.FilterIndex = 1;
            saveFileDialog.RestoreDirectory = true;
            if (sflname == "BoardConfig" || sflname == "Board Config")//support them both in COBRA2.00.15, so all old and new OCEs will work fine.
            {
                saveFileDialog.FileName = chipname+"-"+MD5Code;
                saveFileDialog.Title = "Save Board Config file";
                saveFileDialog.Filter = "Board Config file (*.board)|*.board||";
                saveFileDialog.DefaultExt = "board";
            }
            else
            {
                saveFileDialog.FileName = chipname + "-" + MD5Code;
                saveFileDialog.Title = "Save Configuration File";
                saveFileDialog.Filter = "Device Configuration file (*.cfg)|*.cfg|c file (*.c)|*.c|h file (*.h)|*.h||";
                saveFileDialog.DefaultExt = "cfg";
            }
            saveFileDialog.InitialDirectory = FolderMap.m_currentproj_folder;
            if (saveFileDialog.ShowDialog() == true)
            {
                if (parent == null) return;
                else
                {
                    fullpath = saveFileDialog.FileName;
                    SaveFile(fullpath);
                }
            }
            else return;

            StatusLabel.Content = fullpath;
            if (sflname == "BoardConfig" || sflname == "Board Config")//support them both in COBRA2.00.15, so all old and new OCEs will work fine.    //Issue1373
            {
                SaveBoardConfigFilePath(fullpath);
            }
        }
        private void SaveBoardConfigToInternalMemory()
        {
            foreach (SFLModel model in viewmode.sfl_parameterlist)
            {
                if (model == null) continue;
                string name = model.nickname;
                string strval;
                switch (model.editortype)
                {
                    case 0:
                        {
                            strval = model.sphydata;
                            break;
                        }
                    case 1:
                    case 2:
                        {
                            strval = String.Format("{0:F1}", model.data);
                            break;
                        }
                    default:
                        strval = model.sphydata;
                        break; ;
                }
                BCImg.Add(name, strval);
            }
        }

        public void LoadBoardConfigFromInternalMemory()
        {
            double dval = 0.0;
            string tmp;
            SFLModel model;
            foreach (var item in BCImg)
            {
                string name = item.Key;
                model = viewmode.GetParameterByName(name);
                if (model == null) continue;

                model.berror = false;

                tmp = item.Value;

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
                        case 7: //Dword
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
                        case 8: //Date
                            {
                                try
                                {
                                    dval = SharedFormula.DateToUInt32(tmp);
                                }
                                catch (Exception e)
                                {
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

        public void BoardConfigAutoLoadFile(string fullpath)
        {
            double dval = 0.0;
            string tmp;
            SFLModel model;
            if (!File.Exists(fullpath))
            {
                //gm.message = "The previously saved file path is invalid! The default values will be used in Board Settings.";
                //CallWarningControl(gm);
                throw new NotImplementedException("The previously saved file path is invalid! ");
            }
            XmlDocument doc = new XmlDocument();
            try
            {
                doc.Load(fullpath);
            }
            catch
            {
                //gm.message = "File format error! The default values will be used in Board Settings.";
                //CallWarningControl(gm);
                throw new NotImplementedException("File format error! ");
            }

            XmlElement root = doc.DocumentElement;

            StringBuilder sb = new StringBuilder();
            string hash = "";
            for (XmlNode xn = root.FirstChild; xn is XmlNode; xn = xn.NextSibling)
            {
                string name = xn.Attributes[0].Value;
                if (name == "MD5")
                {
                    hash = xn.InnerText;
                    continue;
                }
                sb.Append(name);
                tmp = xn.InnerText;
                sb.Append(tmp);
            }
            if (hash == "")         //没有MD5
            {
                gm.message = "Warning, this configuration file dosen't have MD5 verification code. You can still use it but we suggest you upgrade it by save to another file.";
                CallWarningControl(gm);
            }
            else
            {
                using (MD5 md5Hash = MD5.Create())
                {
                    if (VerifyMd5Hash(md5Hash, sb.ToString(), hash))
                    {
                        ;
                    }
                    else
                    {
                        throw new NotImplementedException("File illegal, MD5 check failed. ");
                    }
                }
            }

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
                model = viewmode.GetParameterByName(name);
                if (model == null) continue;

                model.berror = false;

                tmp = xn.InnerText;
                
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
                        case 7: //Dword
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
                        case 8: //Date
                            {
                                try
                                {
                                    dval = SharedFormula.DateToUInt32(tmp);
                                }
                                catch (Exception e)
                                {
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
            StatusLabel.Content = fullpath;
        }


        internal void LoadFile(string fullpath)
        {
            double dval = 0.0;
            string tmp;
            SFLModel model;

            XmlDocument doc = new XmlDocument();
            try
            {
                doc.Load(fullpath);
            }
            catch
            {
                gm.message = "File format error!";
                CallWarningControl(gm);
                return;
            }

            XmlElement root = doc.DocumentElement;

            StringBuilder sb = new StringBuilder();
            string hash = "";
            for (XmlNode xn = root.FirstChild; xn is XmlNode; xn = xn.NextSibling)
            {
                string name = xn.Attributes[0].Value;
                if (name == "MD5")
                {
                    hash = xn.InnerText;
                    continue;
                }
                sb.Append(name);
                tmp = xn.InnerText;
                sb.Append(tmp);
            }
            if (hash == "")         //没有MD5
            {
                gm.message = "Warning, this configuration file dosen't have MD5 verification code. You can still use it but we suggest you upgrade it by save to another file.";
                CallWarningControl(gm);
            }
            else
            {
                using (MD5 md5Hash = MD5.Create())
                {
                    if (VerifyMd5Hash(md5Hash, sb.ToString(), hash))
                    {
                        ;
                    }
                    else
                    {
                        gm.message = "File illegal, MD5 check failed";
                        CallWarningControl(gm);
                        return;
                    }
                }
            }

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
                model = viewmode.GetParameterByName(name);
                if (model == null) continue;

                model.berror = false;

                tmp = xn.InnerText;

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
                        case 7: //Dword
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
                        case 8: //Date
                            {
                                try
                                {
                                    dval = SharedFormula.DateToUInt32(tmp);
                                }
                                catch (Exception e)
                                {
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
            StatusLabel.Content = fullpath;
        }
        internal void SaveFile(string fullpath)
        {
            int index = fullpath.LastIndexOf('.');
            string suffix = fullpath.Substring(index+1);
            switch (suffix.ToLower())
            {
                case "c":
                    SaveCFile(fullpath);
                    break;
                case "h":
                    SaveHFile(fullpath);
                    break;
                default:
                    SaveCfgFile(fullpath);
                    break;
            }
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

        private void ReadBtn_Click(object sender, RoutedEventArgs e)
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            btnControl btn_ctrl = null;
            switch (sender.GetType().Name)
            {
                case "MenuItem":
                    var cl = sender as System.Windows.Controls.MenuItem;
                    var cm = cl.Parent as System.Windows.Controls.ContextMenu;
                    viewmode.BuildPartParameterList(cm.PlacementTarget.Uid);
                    msg.gm.controls = "Read One parameter";
                    msg.task_parameterlist = viewmode.dm_part_parameterlist;
                    break;
                case "Button":
                    msg.gm.controls = ((System.Windows.Controls.Button)sender).Content.ToString();
                    msg.task_parameterlist = viewmode.dm_parameterlist;

                    System.Windows.Controls.Button btn = sender as System.Windows.Controls.Button;
                    btn_ctrl = ui_config.GetBtnControlByName(btn.Name);
                    if (btn_ctrl == null) break;
                    if (btn_ctrl.btn_menu_control.Count == 0) break;

                    btn_ctrl.btn_cm.PlacementTarget = btn;
                    btn_ctrl.btn_cm.IsOpen = true;
                    return;
                default:
                    break;
            }
            //string timestamp = DateTime.Now.ToString();
            //int log_id = -1;
            //DBManager.NewLog("Com", "Com Log", timestamp, ref log_id);
            if (sflname == "BoardConfig" || sflname == "Board Config")//support them both in COBRA2.00.15, so all old and new OCEs will work fine.
                Reset();
            else
                Read();
        }
        private void Reset()
        {
            StatusLabel.Content = "";
            if (parent.bBusy)
            {
                gm.level = 1;
                gm.controls = "Read From Device button";
                gm.message = LibErrorCode.GetErrorDescription(LibErrorCode.IDS_ERR_EM_THREAD_BKWORKER_BUSY);
                gm.bupdate = true;
                CallWarningControl(gm);
                return;
            }
            else
                parent.bBusy = true;

            msg.percent = 40;
            msg.task = TM.TM_READ;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                gm.level = 2;
                gm.message = LibErrorCode.GetErrorDescription(msg.errorcode);
                CallWarningControl(gm);
                parent.bBusy = false;
                return;
            }

            msg.percent = 80;
            msg.task = TM.TM_CONVERT_HEXTOPHYSICAL;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                gm.level = 2;
                gm.message = LibErrorCode.GetErrorDescription(msg.errorcode);
                CallWarningControl(gm);
                parent.bBusy = false;
                return;
            }

            parent.bBusy = false;
        }
        private void Read()
        {
            //if(parent.bSimulation)

            msg.funName = MethodBase.GetCurrentMethod().Name;

            StatusLabel.Content = "Device Content";
            if (parent.bBusy)
            {
                gm.level = 1;
                gm.controls = "Read From Device button";
                gm.message = LibErrorCode.GetErrorDescription(LibErrorCode.IDS_ERR_EM_THREAD_BKWORKER_BUSY);
                gm.bupdate = true;
                CallWarningControl(gm);
                return;
            }
            else
                parent.bBusy = true;

            msg.brw = true;
            msg.percent = 10;
            msg.task = TM.TM_SPEICAL_GETDEVICEINFOR;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                gm.level = 2;
                gm.message = LibErrorCode.GetErrorDescription(msg.errorcode);
                CallWarningControl(gm);
                parent.bBusy = false;
                return;
            }

            msg.percent = 20;
            msg.task = TM.TM_SPEICAL_GETREGISTEINFOR;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                gm.level = 2;
                gm.message = LibErrorCode.GetErrorDescription(msg.errorcode);
                CallWarningControl(gm);
                parent.bBusy = false;
                return;
            }

            msg.percent = 40;
            msg.task = TM.TM_READ;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                gm.level = 2;
                gm.message = LibErrorCode.GetErrorDescription(msg.errorcode);
                CallWarningControl(gm);
                parent.bBusy = false;
                return;
            }

            if (!NoMapping)
            {
	            msg.percent = 60;
	            msg.task = TM.TM_BLOCK_MAP;
	            parent.AccessDevice(ref m_Msg);
	            while (msg.bgworker.IsBusy)
	                System.Windows.Forms.Application.DoEvents();
	            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
	            {
	                gm.level = 2;
	                gm.message = LibErrorCode.GetErrorDescription(msg.errorcode);
	                CallWarningControl(gm);
	                parent.bBusy = false;
	                return;
                }
            }

            msg.percent = 80;
            msg.task = TM.TM_CONVERT_HEXTOPHYSICAL;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                gm.level = 2;
                gm.message = LibErrorCode.GetErrorDescription(msg.errorcode);
                CallWarningControl(gm);
                parent.bBusy = false;
                return;
            }

            parent.bBusy = false;

            try
            {
                //if (O2Micro.Cobra.EM.Registry.busoptionslist[0].GetATMElementbyGuid(AutomationElement.GUIDATMTestStart).dbValue > 0 ? false : true)//not test mode
                if (parent.bATMTestStart)
                {
                    AutomationTestLog.CompleteLogFile();
                    CommunicationLog.CompleteComLogFile();
                    //(A170119)Francis, saving log to database
                    if (DBManager.supportdb == true)
                    {
                        CommunicationDBLog.CompleteComDBLogFile();
                    }
                    //(E170119)
                    parent.bOnce = false;
                }
            }
            catch (System.Exception ex)
            {
                //System.Windows.MessageBox.Show(ex.Message);
            }
            return;
        }

        private void WriteBtn_Click(object sender, RoutedEventArgs e)
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            btnControl btn_ctrl = null;
            switch (sender.GetType().Name)
            {
                case "MenuItem":
                    var cl = sender as System.Windows.Controls.MenuItem;
                    var cm = cl.Parent as System.Windows.Controls.ContextMenu;
                    viewmode.BuildPartParameterList(cm.PlacementTarget.Uid);
                    msg.gm.controls = "Write One parameter";
                    msg.task_parameterlist = viewmode.dm_part_parameterlist;
                    break;
                case "Button":
                    if (sflname == "BoardConfig" || sflname == "Board Config")//support them both in COBRA2.00.15, so all old and new OCEs will work fine.
                    {
                    }
                    else
                    {
                        msg.gm.message = "you are ready to write entirely area,please be care!";
                        msg.controlreq = COMMON_CONTROL.COMMON_CONTROL_SELECT;
                        if (!msg.controlmsg.bcancel) return; 
                    }

                    msg.gm.controls = ((System.Windows.Controls.Button)sender).Content.ToString();
                    msg.task_parameterlist = viewmode.dm_parameterlist;

                    System.Windows.Controls.Button btn = sender as System.Windows.Controls.Button;
                    btn_ctrl = ui_config.GetBtnControlByName(btn.Name);
                    if (btn_ctrl == null) break;
                    if (btn_ctrl.btn_menu_control.Count == 0) break;

                    btn_ctrl.btn_cm.PlacementTarget = btn;
                    btn_ctrl.btn_cm.IsOpen = true;
                    return;
            }
            if (sflname == "BoardConfig" || sflname == "Board Config")//support them both in COBRA2.00.15, so all old and new OCEs will work fine.
                Apply();
            else
                write();
        }
        private void Apply()
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            if (parent.bBusy)
            {
                gm.level = 1;
                gm.controls = "Write To Device button!";
                gm.message = LibErrorCode.GetErrorDescription(LibErrorCode.IDS_ERR_EM_THREAD_BKWORKER_BUSY);
                gm.bupdate = true;
                CallWarningControl(gm);
                return;
            }
            else
                parent.bBusy = true;

            ret = viewmode.WriteDevice();
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                gm.level = 2;
                gm.message = LibErrorCode.GetErrorDescription(ret);
                CallWarningControl(gm);
                parent.bBusy = false;
                return;
            }
            StatusLabel.Content = "";

            msg.percent = 30;
            msg.task = TM.TM_CONVERT_PHYSICALTOHEX;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();

            msg.percent = 70;
            msg.task = TM.TM_WRITE;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                gm.level = 2;
                gm.message = LibErrorCode.GetErrorDescription(msg.errorcode);
                CallWarningControl(gm);
                parent.bBusy = false;
                return;
            }

            parent.bBusy = false;
        }
        private void write()
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            if (parent.bBusy)
            {
                gm.level = 1;
                gm.controls = "Write To Device button!";
                gm.message = LibErrorCode.GetErrorDescription(LibErrorCode.IDS_ERR_EM_THREAD_BKWORKER_BUSY);
                gm.bupdate = true;
                CallWarningControl(gm);
                return;
            }
            else
                parent.bBusy = true;

            ret = viewmode.WriteDevice();
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                gm.level = 2;
                gm.message = LibErrorCode.GetErrorDescription(ret);
                CallWarningControl(gm);
                parent.bBusy = false;
                return;
            }
            StatusLabel.Content = "Device Content";

            msg.brw = false;
            msg.percent = 10;
            msg.task = TM.TM_SPEICAL_GETDEVICEINFOR;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                gm.level = 2;
                gm.message = LibErrorCode.GetErrorDescription(msg.errorcode);
                CallWarningControl(gm);
                parent.bBusy = false;
                return;
            }

            msg.percent = 20;
            msg.task = TM.TM_SPEICAL_GETREGISTEINFOR;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                gm.level = 2;
                gm.message = LibErrorCode.GetErrorDescription(msg.errorcode);
                CallWarningControl(gm);
                parent.bBusy = false;
                return;
            }

            msg.percent = 30;
            msg.task = TM.TM_READ;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                gm.level = 2;
                gm.message = LibErrorCode.GetErrorDescription(msg.errorcode);
                CallWarningControl(gm);
                parent.bBusy = false;
                return;
            }

            msg.percent = 40;
            msg.task = TM.TM_CONVERT_PHYSICALTOHEX;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();

            msg.percent = 50;
            msg.task = TM.TM_WRITE;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                gm.level = 2;
                gm.message = LibErrorCode.GetErrorDescription(msg.errorcode);
                CallWarningControl(gm);
                parent.bBusy = false;
                return;
            }

            msg.percent = 70;
            msg.task = TM.TM_READ;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                gm.level = 2;
                gm.message = LibErrorCode.GetErrorDescription(msg.errorcode);
                CallWarningControl(gm);
                parent.bBusy = false;
                return;
            }

            msg.percent = 80;
            msg.task = TM.TM_CONVERT_HEXTOPHYSICAL;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();

            if (!NoMapping)
            {
	            msg.percent = 90;
	            msg.task = TM.TM_BLOCK_MAP;
	            parent.AccessDevice(ref m_Msg);
	            while (msg.bgworker.IsBusy)
	                System.Windows.Forms.Application.DoEvents();
	            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
	            {
	                gm.level = 2;
	                gm.message = LibErrorCode.GetErrorDescription(msg.errorcode);
	                CallWarningControl(gm);
	                parent.bBusy = false;
	                return;
                }
            }

            parent.bBusy = false;
        }

        private void EraseBtn_Click(object sender, RoutedEventArgs e)
        {
            if (parent.bBusy)
            {
                gm.level = 1;
                gm.controls = "Erase Device button";
                gm.message = LibErrorCode.GetErrorDescription(LibErrorCode.IDS_ERR_EM_THREAD_BKWORKER_BUSY);
                gm.bupdate = true;
                CallWarningControl(gm);
                return;
            }
            else
                parent.bBusy = true;

            StatusLabel.Content = "EEPROM Content";

            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.gm.controls = ((System.Windows.Controls.Button)sender).Content.ToString();

            msg.percent = 10;
            msg.task = TM.TM_SPEICAL_GETDEVICEINFOR;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                gm.level = 2;
                gm.message = LibErrorCode.GetErrorDescription(msg.errorcode);
                CallWarningControl(gm);
                parent.bBusy = false;
                return;
            }

            msg.percent = 20;
            msg.task = TM.TM_BLOCK_ERASE;
            msg.task_parameterlist = viewmode.dm_parameterlist;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                gm.level = 2;
                gm.message = LibErrorCode.GetErrorDescription(msg.errorcode);
                CallWarningControl(gm);
                parent.bBusy = false;
                return;
            }

            msg.percent = 50;
            msg.task = TM.TM_BLOCK_MAP;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                gm.level = 2;
                gm.message = LibErrorCode.GetErrorDescription(msg.errorcode);
                CallWarningControl(gm);
                parent.bBusy = false;
                return;
            }

            msg.percent = 60;
            msg.task = TM.TM_SPEICAL_GETDEVICEINFOR;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                gm.level = 2;
                gm.message = LibErrorCode.GetErrorDescription(msg.errorcode);
                CallWarningControl(gm);
                parent.bBusy = false;
                return;
            }

            msg.percent = 70;
            msg.task = TM.TM_READ;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                gm.level = 2;
                gm.message = LibErrorCode.GetErrorDescription(msg.errorcode);
                CallWarningControl(gm);
                parent.bBusy = false;
                return;
            }

            msg.percent = 80;
            msg.task = TM.TM_CONVERT_HEXTOPHYSICAL;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                gm.level = 2;
                gm.message = LibErrorCode.GetErrorDescription(msg.errorcode);
                CallWarningControl(gm);
                parent.bBusy = false;
                return;
            }

            parent.bBusy = false;
        }

        private void MenuItem_Click(object sender, EventArgs e)
        {
            UInt16 udata = 0;
            var mi = sender as System.Windows.Controls.MenuItem;
            var cm = mi.Parent as System.Windows.Controls.ContextMenu;
            System.Windows.Controls.Button btn = cm.PlacementTarget as System.Windows.Controls.Button;
            if (UInt16.TryParse(mi.CommandParameter.ToString(), out udata))
                msg.sub_task = udata;
            else
                msg.sub_task = 0;
            switch (btn.Name)
            {
                case "ReadBtn":
                    Read();
                    break;
                case "WriteBtn":
                    write();
                    break;
            }
        }

        #region 通用控件消息响应
        public void CallWarningControl(GeneralMessage message)
        {
            WarningPopControl.Dispatcher.Invoke(new Action(() =>
            {
                WarningPopControl.ShowDialog(message);
            }));
        }

        public void CallPasswordControl(ControlMessage msg)
        {
            PasswordPopControl.Dispatcher.Invoke(new Action(() =>
            {
                msg.bcancel = PasswordPopControl.ShowDialog();
                msg.password = PasswordPopControl.password;
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

        public void CallSelectControl(GeneralMessage message)
        {
            SelectPopControl.Dispatcher.Invoke(new Action(() =>
            {
                msg.controlmsg.bcancel = SelectPopControl.ShowDialog(message);
            }));
        }
        #endregion

        #region Simulation模式
        public void Simulation()
        {
            parent.bControl = true;
            m_BackgroundWorker.RunWorkerAsync();
        }

        void DoWork(object sender, DoWorkEventArgs e)
        {
            while (parent.bBusy) ;
            BackgroundWorker bw = sender as BackgroundWorker;

            int i = 1;
            MethodInfo dynMethod = this.GetType().GetMethod(msg.funName, BindingFlags.NonPublic | BindingFlags.Instance);
            while (i <= parent.iATMTestRepeatTimes)
            {
                if (bw.CancellationPending)
                {
                    e.Cancel = true;
                    break;
                }
                this.Dispatcher.Invoke(new Action(() =>
                {
                    dynMethod.Invoke(this, null);
                }));
                bw.ReportProgress(i++);
                Thread.Sleep(1000);
            }

            e.Cancel = true;
        }

        void UpdateProgress(object sender, ProgressChangedEventArgs e)
        {
            int progress = e.ProgressPercentage;
            m_CtrlMg.message = string.Format("The {0} time simulation", progress);
            m_CtrlMg.percent = progress;
            m_CtrlMg.bshow = true;
            CallWaitControl(m_CtrlMg);
            //label1.Content = string.Format("{0}", progress);
        }

        void CompletedWork(object sender, RunWorkerCompletedEventArgs e)
        {
            parent.bControl = false;
            if (e.Error != null)
            {
                // MessageBox.Show("Error");
            }
            else if (e.Cancelled)
            {
                //  MessageBox.Show("Canceled");
            }
            else
            {
                //  MessageBox.Show("Completed");
            }
            AutomationTestLog.CompleteLogFile();
            /*GlobalData.lvp.summaryInfo.GPEC = AutoMationTest.AutoMationTest.wErrPECCounter.ToString();
            GlobalData.lvp.summaryInfo.GCRC = AutoMationTest.AutoMationTest.wErrCRCCounter.ToString();
            GlobalData.lvp.summaryInfo.GoMax = AutoMationTest.AutoMationTest.wErrOutMaxCounter.ToString();
            GlobalData.lvp.summaryInfo.GoMin = AutoMationTest.AutoMationTest.wErrOutMinCounter.ToString();
            GlobalData.lvp.summaryInfo.GTol = AutoMationTest.AutoMationTest.wErrSummary.ToString();
            GlobalData.lvp.summaryInfo.CPEC = AutoMationTest.AutoMationTest.PECCtr.ToString();
            GlobalData.lvp.summaryInfo.CCRC = AutoMationTest.AutoMationTest.CRCCtr.ToString();
            GlobalData.lvp.summaryInfo.CoMax = AutoMationTest.AutoMationTest.OMaxCtr.ToString();
            GlobalData.lvp.summaryInfo.CoMin = AutoMationTest.AutoMationTest.OMinCtr.ToString();
            GlobalData.lvp.summaryInfo.CTol = AutoMationTest.AutoMationTest.ErrSummary.ToString();
            GlobalData.lvp.summaryInfo.Rate = (((float)AutoMationTest.AutoMationTest.ErrSummary / (float)AutoMationTest.AutoMationTest.wErrSummary) * 100).ToString("F2") + "%";*/
            //AutoMationTest.AutoMationTest.AssignInfo();
            CommunicationLog.CompleteComLogFile();
            //(A170119)Francis, saving log to database
            if (DBManager.supportdb == true)
            {
                CommunicationDBLog.CompleteComDBLogFile();
            }
            //(E170119)
            parent.bOnce = false;
            m_CtrlMg.bshow = false;
            CallWaitControl(m_CtrlMg);
            gm.level = 1;
            gm.controls = "Simulation";
            gm.message = LibErrorCode.GetErrorDescription(LibErrorCode.IDS_ERR_SECTION_SIMULATION_COMPLETE);
            gm.bupdate = true;
            CallWarningControl(gm);
        }
        #endregion

        #region 其他函数
        private void SaveCfgFile(string fullpath)
        {
            FileStream file = new FileStream(fullpath, FileMode.Create);
            StreamWriter sw = new StreamWriter(file);
            sw.WriteLine("<?xml version=\"1.0\"?>");
            sw.WriteLine("<root>");
            sw.WriteLine("</root>");
            sw.Close();
            file.Close();
            XmlDocument doc = new XmlDocument();
            doc.Load(fullpath);
            XmlElement root = doc.DocumentElement;

            StringBuilder sb = new StringBuilder();
            foreach (SFLModel model in viewmode.sfl_parameterlist)
            {
                if (model == null) continue;
                string name = model.nickname;
                sb.Append(name);
                XmlElement newitem = doc.CreateElement("item");
                XmlAttribute A = doc.CreateAttribute("Name");
                XmlText T = doc.CreateTextNode(name);
                string strval;
                switch (model.editortype)
                {
                    case 0:
                        {
                            strval = model.sphydata;
                            break;
                        }
                    case 1:
                    case 2:
                        {
                            strval = String.Format("{0:F1}", model.data);
                            break;
                        }
                    default:
                        strval = model.sphydata;
                        break; ;
                }
                sb.Append(strval);
                XmlText v = doc.CreateTextNode(strval);
                root.AppendChild(newitem);
                newitem.SetAttributeNode(A);
                A.AppendChild(T);
                newitem.AppendChild(v);
            }
            string hash;
            using (MD5 md5Hash = MD5.Create())
            {
                hash = GetMd5Hash(md5Hash, sb.ToString());
            }
            XmlElement item = doc.CreateElement("item");
            XmlAttribute Aname = doc.CreateAttribute("Name");
            XmlText Tname = doc.CreateTextNode("MD5");
            XmlText Tvalue = doc.CreateTextNode(hash);
            root.AppendChild(item);
            item.SetAttributeNode(Aname);
            Aname.AppendChild(Tname);
            item.AppendChild(Tvalue);

            doc.Save(fullpath);
        }

        private void SaveCFile(string fullpath)
        {
            int nline = 0;

            viewmode.WriteDevice(); 
            msg.task_parameterlist = viewmode.dm_parameterlist;
            msg.task = TM.TM_CONVERT_PHYSICALTOHEX;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();

            string sFilePath = System.IO.Path.Combine(FolderMap.m_extension_work_folder, "Parameter.c");
            if (!File.Exists(sFilePath)) return;

            string sLine = string.Empty;
            FileStream wf = new FileStream(fullpath, FileMode.Create);
            StreamWriter sw = new StreamWriter(wf);

            FileStream rf = new FileStream(sFilePath, FileMode.Open);
            StreamReader sr = new StreamReader(rf, UnicodeEncoding.Default);

            bool btypedef = false;
            string stmp = string.Empty;
            SFLModel mode = null;

            Dictionary<string, int> typedefDic = new Dictionary<string, int>();
            StringBuilder stB = new StringBuilder();
            string[] sArray = null;
            string sFirstStr = string.Empty;

            lineList.Clear();
            while ((sLine = sr.ReadLine()) != null)
            {
                lineList.Add(sLine);
                //sLine = Regex.Replace(sLine, "[\r\n\t]"," " ,RegexOptions.Compiled);
                if (sLine.IndexOf("volatile") != -1)
                {
                    sArray = sLine.Split(' ');
                    foreach (string str in sArray)
                    {
                        if (str.ToString().Contains("["))
                        {
                            typedefDic.Add(str.Substring(0,str.IndexOf('[')), nline);
                            btypedef = false;
                        }
                    }
                }
                nline++;
            }
            foreach (CollectionViewGroup group in GroupedCustomers.Groups)
            {
                stB.Clear();
                if (typedefDic.ContainsKey(((string)group.Name).Trim().ToLower()))
                {
                    stB.Append(lineList[typedefDic[((string)group.Name).Trim().ToLower()]]);
                    foreach (object md in group.Items)
                    {
                        mode = (md as  SFLModel);
                        if (mode != null)
                        {
                            stB.Append('\r');
                            stB.Append('\n');
                            viewmode.FWHexTostr(mode);
                            stB.Append(mode.sphydata);
                            stB.Append(',');
                            stB.Append(@"           \\");
                            stB.Append(mode.name);
                            stB.Append(',');
                        }
                    }
                    lineList[typedefDic[((string)group.Name).Trim().ToLower()]] = stB.ToString();
                }
            }
            foreach (string line in lineList)
                sw.Write(string.Format("{0}{1}",line,"\r\n"));
            sr.Close();
            rf.Close();
            sw.Close();
            wf.Close();
        }

        private void SaveHFile(string fullpath)
        {
            int nline = 0;
            int fline = 0;

            string sFilePath = System.IO.Path.Combine(FolderMap.m_extension_work_folder, "Parameter.h");
            if (!File.Exists(sFilePath)) return;

            string sLine = string.Empty;
            FileStream wf = new FileStream(fullpath, FileMode.Create);
            StreamWriter sw = new StreamWriter(wf);

            FileStream rf = new FileStream(sFilePath, FileMode.Open);
            StreamReader sr = new StreamReader(rf, UnicodeEncoding.Default);

            bool btypedef = false;
            string stmp = string.Empty;
            SFLModel mode = null;

            Dictionary<string, int> typedefDic = new Dictionary<string, int>();
            StringBuilder stB = new StringBuilder();
            string sFirstStr = string.Empty;

            lineList.Clear();
            while ((sLine = sr.ReadLine()) != null)
            {
                lineList.Add(sLine);
                sLine = Regex.Replace(sLine, "[\r\n\t]"," " ,RegexOptions.Compiled);
                if (sLine.IndexOf("typedef") != -1)
                {
                    btypedef = true;
                    stB.Clear();
                }
                if (btypedef)
                {
                    if (sLine.Contains("{")) fline = nline;
                    if (sLine.Contains("}") || sLine.Contains(";"))
                        stB.Append(sLine);
                    if (stB.ToString().Contains("}") && stB.ToString().Contains(";"))
                    {
                        stmp = stB.ToString();
                        typedefDic.Add(stmp.Substring(stmp.IndexOf('}') + 1, stmp.IndexOf(';') - 1).Trim().ToLower(), fline);
                        btypedef = false;
                    }
                }
                nline++;
            }
            foreach (CollectionViewGroup group in GroupedCustomers.Groups)
            {
                stB.Clear();
                if (typedefDic.ContainsKey(((string)group.Name).Trim().ToLower()))
                {
                    stB.Append(lineList[typedefDic[((string)group.Name).Trim().ToLower()]]);
                    foreach (object md in group.Items)
                    {
                        mode = (md as  SFLModel);
                        if (mode != null)
                        {
                            stB.Append('\r');
                            stB.Append('\n');
                            stB.Append(mode.name);
                            stB.Append(',');
                        }
                    }
                    lineList[typedefDic[((string)group.Name).Trim().ToLower()]] = stB.ToString();
                }
            }
            foreach (string line in lineList)
                sw.Write(string.Format("{0}{1}",line,"\r\n"));
            sr.Close();
            rf.Close();
            sw.Close();
            wf.Close();
        }
        #endregion
    }
}