using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Shapes;
using System.Linq;
using System.IO;
using System.Reflection;
using System.ComponentModel;
using System.Xml;
using System.Threading;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Cobra.Common;
using Cobra.EM;
using Excel = Microsoft.Office.Interop.Excel;
using System.Data;

namespace Cobra.DeviceConfigurationPanel
{
    enum editortype
    {
        TextBox_EditType = 0,
        ComboBox_EditType = 1,
        CheckBox_EditType = 2
    }

    public static class ConstantSettings	//Leon: 在DeviceConfig这边用到的一些常量
    {
        public readonly static string OCE_NAME_NODE = "OCE_NAME";
        public readonly static string CFG_VERSION_NODE = "CFG_VERSION";
        public readonly static string OCE_TOKEN_NODE = "OCE_TOKEN";
        public readonly static string DLL_TOKEN_NODE = "DLL_TOKEN";
        public readonly static string PARAM_TOKEN_NODE = "PARAM_TOKEN";
        public readonly static string BOARD_TOKEN_NODE = "BOARD_TOKEN";
        public readonly static string MD5_NODE = "MD5";
        public readonly static string PRODUCT_FAMILY_NODE = "PRODUCT_FAMILY";
        public readonly static string BIN_MD5_NODE = "BIN_MD5";
        public readonly static int CFG_VERSION_INT = 0;
        public static readonly string BOARD_NODE = "Board";
        public static readonly string CFG_NODE = "CFG";
    }

    /// <summary>
    /// MainControl.xaml 的交互逻辑
    /// </summary>
    public partial class MainControl
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

        private bool m_bVerify = false;
        public bool bVerify
        {
            get { return m_bVerify; }
            set { m_bVerify = value; }
        }

        private TASKMessage m_Msg = new TASKMessage();
        public TASKMessage msg
        {
            get { return m_Msg; }
            set { m_Msg = value; }
        }

        public SFLViewModel cfgViewModel { get; set; }	//原来的viewmode改名用以区分

        public SFLViewModel boardViewModel { get; set; }	//board部分的vm
        public List<SFLModel> TotalList
        {
            get
            {
                var list = cfgViewModel.sfl_parameterlist.ToList();
                list.AddRange(boardViewModel.sfl_parameterlist.ToList());
                return list;
            }
        }
        private ControlMessage m_CtrlMg = new ControlMessage();

        public bool border = false; //是否采用Order排序模式
        public GeneralMessage gm = new GeneralMessage("Device Configuration SFL", "", 0);

        private UIConfig m_UI_Config = new UIConfig();
        public UIConfig ui_config
        {
            get { return m_UI_Config; }
            set { m_UI_Config = value; }
        }

        private ListCollectionView GroupedCustomers = null;
        #region C-HFile
        private List<string> lineList = new List<string>();
        #endregion
        private Dictionary<string, string> BCImg = new System.Collections.Generic.Dictionary<string, string>();//Issue 1426 Leon


        public ushort m_readsubtask = 0;	//Issue1363 Leon
        public ushort ReadSubTask
        {
            set { m_readsubtask = value; }
            get { return m_readsubtask; }
        }
        public ushort m_writesubtask = 0;	//Issue1363 Leon
        public ushort WriteSubTask
        {
            set { m_writesubtask = value; }
            get { return m_writesubtask; }
        }
        public ushort SaveHexSubTask	//Issue1513 Leon
        {
            set;
            get;
        } = 0;
        public ushort GetMaxValueSubTask { get; set; }	//Issue1593 Leon
        public ushort GetMinValueSubTask { get; set; }	//Issue1593 Leon
        public string ProductFamily { get; set; } = string.Empty;
        public string BoardConfigLabel { get; set; } = string.Empty;

        public event EventHandler BoardConfigChanged;//Issue1593 Leon

        protected virtual void OnRasieBoardConfigChangedEvent()//Issue1593 Leon
        {
            EventHandler handler = BoardConfigChanged;

            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }
        private Dictionary<string, string> Verify_Dic = new Dictionary<string, string>();

        public MainControl(object pParent, string name)
        {
            this.InitializeComponent();
            #region 相关初始化
            bool bval = false;
            parent = (Device)pParent;
            if (parent == null) return;

            sflname = name;
            if (String.IsNullOrEmpty(sflname)) return;

            #region 初始化SubTask	//Issue1363 Leon
            string str_option = String.Empty;
            XmlNodeList nodelist = parent.GetUINodeList(sflname);
            foreach (XmlNode node in nodelist)
            {
                str_option = node.Name;
                switch (str_option)
                {
                    case "SubTask":
                        {
                            foreach (XmlNode sub in node)
                            {
                                if (sub.Name == "Read")
                                    ReadSubTask = Convert.ToUInt16(sub.InnerText);
                                else if (sub.Name == "Write")
                                    WriteSubTask = Convert.ToUInt16(sub.InnerText);
                                else if (sub.Name == "SaveHex")		//Issue1513 Leon
                                    SaveHexSubTask = Convert.ToUInt16(sub.InnerText);
                                else if (sub.Name == "GetMax")		//Issue1593 Leon
                                    GetMaxValueSubTask = Convert.ToUInt16(sub.InnerText);
                                else if (sub.Name == "GetMin")		//Issue1593 Leon
                                    GetMinValueSubTask = Convert.ToUInt16(sub.InnerText);
                            }
                            break;
                        }
                    case "BoardConfigLabel":
                        BoardConfigLabel = node.InnerText;
                        break;
                }
            }
            ProductFamily = SharedAPI.GetProductFamilyFromExtension();
            #endregion

            InitalUI();
            LibInfor.AssemblyRegister(Assembly.GetExecutingAssembly(), ASSEMBLY_TYPE.SFL);
            gm.PropertyChanged += new PropertyChangedEventHandler(gm_PropertyChanged);
            msg.PropertyChanged += new PropertyChangedEventHandler(msg_PropertyChanged);
            msg.gm.PropertyChanged += new PropertyChangedEventHandler(gm_PropertyChanged);

            cfgViewModel = new SFLViewModel(pParent, this, sflname);
            boardViewModel = new SFLViewModel(pParent, this, BoardConfigLabel);

            SaveConfigToInternalMemory();//Issue1378 Leon

            PasswordPopControl.SetParent(mDataGrid);
            WarningPopControl.SetParent(mDataGrid);
            WaitPopControl.SetParent(mDataGrid);
            #endregion

            #region 初始化NoMapping
            XmlElement root = EMExtensionManage.m_extDescrip_xmlDoc.DocumentElement;
            XmlNode xn = root.SelectSingleNode("descendant::Button[@Label = '" + sflname + "']");
            XmlElement xe = (XmlElement)xn;

            if (Boolean.TryParse(xe.GetAttribute("NoMapping").Trim(), out bval))
                NoMapping = bval;
            else
                NoMapping = false;
            if (Boolean.TryParse(xe.GetAttribute("bVerify").Trim(), out bval))
                bVerify = bval;
            else
                bVerify = false;

            #endregion

            if (border)
            {
                var queryResults = cfgViewModel.sfl_parameterlist.OrderBy(model => model.order).ThenBy(model => model.guid).Select(model => model);
                IEnumerable<IGrouping<string, SFLModel>> groups = cfgViewModel.sfl_parameterlist.GroupBy(model => model.catalog);
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
                GroupedCustomers = new ListCollectionView(cfgViewModel.sfl_parameterlist);
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

                                if (sub.Attributes["Recontent"] != null)	//Guo
                                    btCtrl.btn_content = sub.Attributes["Recontent"].Value.ToString();

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
            foreach (UIElement uiE in BottomPanel.Children)	//Guo
            {
                if (!(uiE is System.Windows.Controls.Button)) continue;
                System.Windows.Controls.Button btn = (uiE as System.Windows.Controls.Button);
                if (ui_config.GetBtnControlByName(btn.Name) != null)
                {
                    if (ui_config.GetBtnControlByName(btn.Name).btn_content != null)
                        continue;
                }

                switch (btn.Name)
                {
                    case "LoadBtn":
                        btn.Content = "Load From File";
                        break;
                    case "SaveBtn":
                        btn.Content = "Save To File";
                        break;
                    case "ReadBtn":
                        btn.Content = "Read From Device";
                        break;
                    case "WriteBtn":
                        btn.Content = "Write To Device";
                        break;
                    case "EraseBtn":
                        btn.Content = "Erase";
                        break;
                }
            }
            if (ui_config.GetBtnControlByName("VerifyBtn") == null)
            {
                VerifyBtn.Visibility = Visibility.Collapsed;    //默认隐藏
            }
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
        private bool isBoardConfig()
        {
            return sflname == COBRA_GLOBAL.Constant.OldBoardConfigName || sflname == COBRA_GLOBAL.Constant.NewBoardConfigName;//support them both in COBRA2.00.15, so all old and new OCEs will work fine.//Issue 1426 Leon
        }
        #region Save
        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            ret = ParameterValidityCheck();
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                var m = new GeneralMessage();
                m.message = LibErrorCode.GetErrorDescription(ret);
                m.level = 2;
                CallWarningControl(m);
                return;
            }
            string OCEName = GetOCEName();
            if (OCEName == string.Empty)
            {
                msg.gm.message = "The CFG file will not contain OCEName info.\n" +
                    "Proceed?";
                CallSelectControl(msg.gm);
                if (!msg.controlmsg.bcancel)
                    return;
            }

            string fullpath = "";
            string chipname = GetChipName();    //Issue1373

            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            saveFileDialog.FilterIndex = 1;
            saveFileDialog.RestoreDirectory = true;

            saveFileDialog.FileName = chipname;// + "-" + DateTime.Now.ToString("yyyyMMddHHmmss");//Issue1373 Leon
            saveFileDialog.Title = "Save File";       //Issue1513 Leon
            saveFileDialog.Filter = "Device Configuration file (*.cfg)|*.cfg|c file (*.c)|*.c|h file (*.h)|*.h||";
            saveFileDialog.DefaultExt = "cfg";

            saveFileDialog.InitialDirectory = FolderMap.m_currentproj_folder;
            if (saveFileDialog.ShowDialog() == true)
            {
                if (parent == null) return;
                else
                {
                    fullpath = saveFileDialog.FileName;
                    SaveFile(ref fullpath);
                    Registry.SaveConfigFilePath(fullpath);//Issue1378 Leon
                }
            }
            else return;

            StatusLabel.Content = fullpath;
        }

        private UInt32 ParameterValidityCheck()   //Issue1607 Leon  check parameter validity before save
        {
            foreach (SFLModel model in cfgViewModel.sfl_parameterlist)
            {
                if (model.berror
                    || (model.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)        //Leon: 与改成或，任何一个错了都拦下来
                    )
                    return LibErrorCode.IDS_ERR_SECTION_DEVICECONFSFL_PARAM_INVALID;
            }
            return LibErrorCode.IDS_ERR_SUCCESSFUL;
        }
        private string GetChipName()//Issue1373 Leon Get chip name to form file name before save（考虑放到初始化中）
        {
            XmlElement root = EMExtensionManage.m_extDescrip_xmlDoc.DocumentElement;
            return root.GetAttribute("chip");
        }
        private string GetOCEName()
        {
            XmlElement root = EMExtensionManage.m_extDescrip_xmlDoc.DocumentElement;
            return root.GetAttribute("OCEName");
        }
        internal void SaveFile(ref string fullpath)
        {
            int index = fullpath.LastIndexOf('.');
            string suffix = fullpath.Substring(index + 1);
            switch (suffix.ToLower())
            {
                case "c":
                    SaveCFile(fullpath);
                    break;
                case "h":
                    SaveHFile(fullpath);
                    break;
                default:    //*.cfg + *.hex + *.bin
                    SaveFilePackage(ref fullpath);
                    break;
            }
        }
        private void SaveFilePackage(ref string cfgfullpath)
        {
            if (!string.IsNullOrEmpty(BoardConfigLabel))
            {
                //string boardMD5 = string.Empty;
                //boardMD5 = GetBoardMD5();

                gm.message = "Hex and bin file are to be generated, please make sure the Board Config is configured correctly!";
                msg.gm.level = 0;
                CallSelectControl(gm);
                if (msg.controlmsg.bcancel != true)
                {
                    return;
                }
            }

            cfgViewModel.UpdateAllModels();

            string originalfilename = System.IO.Path.GetFileNameWithoutExtension(cfgfullpath);
            string originalfolder = System.IO.Path.GetDirectoryName(cfgfullpath);
            string newfolder = System.IO.Path.Combine(originalfolder, originalfilename + "-" + DateTime.Now.ToString("yyyyMMddHHmmss"));
            if (!Directory.Exists(newfolder))
                Directory.CreateDirectory(newfolder);
            string filename = System.IO.Path.GetFileName(cfgfullpath);
            string BIN_MD5_STR = string.Empty;
            if (SaveHexSubTask != 0)
            {
                string hexfullpath = System.IO.Path.Combine(newfolder, originalfilename + ".hex");
                BIN_MD5_STR = SaveHexFile(hexfullpath);                                      //呼叫DEM API，P2H之后保存二进制文件。如果OCE的xml里面没有指定SaveHex命令的话，这里相当于skip掉。
            }
            else
            {
                //对于Register Config来说，不产生hex文件，也没有BIN_MD5
            }

            msg.task_parameterlist = cfgViewModel.dm_parameterlist;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_CONVERT_PHYSICALTOHEX;                                             //P2H。为防止上一步被skip掉，这里要确保流程完整，所以必须加上这一步
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();

            msg.task_parameterlist = cfgViewModel.dm_parameterlist;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_CONVERT_HEXTOPHYSICAL;                                             //H2P。这样一来保存的内容就是实际计算出来的值而非UI值。
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();

            cfgfullpath = System.IO.Path.Combine(newfolder, filename); //cfg file name;
            SaveConfigFile(cfgfullpath, BIN_MD5_STR);
        }

        private void SaveConfigFile(string cfgfullpath, string BIN_MD5_STR)
        {
            FileStream file = new FileStream(cfgfullpath, FileMode.Create);
            StreamWriter sw = new StreamWriter(file);
            sw.WriteLine("<?xml version=\"1.0\"?>");
            sw.WriteLine("<root>");
            sw.WriteLine("</root>");
            sw.Close();
            file.Close();
            XmlDocument doc = new XmlDocument();
            doc.Load(cfgfullpath);
            XmlElement root = doc.DocumentElement;

            var OCEName = GetOCEName();
            if (!OCEName.Equals(string.Empty))
                SharedAPI.XmlAddOneNode(doc, root, ConstantSettings.OCE_NAME_NODE, OCEName);

            SharedAPI.XmlAddOneNode(doc, root, ConstantSettings.CFG_VERSION_NODE, ConstantSettings.CFG_VERSION_INT.ToString());

            SharedAPI.XmlAddOneNode(doc, root, ConstantSettings.OCE_TOKEN_NODE, COBRA_GLOBAL.CurrentOCEToken);

            SharedAPI.XmlAddOneNode(doc, root, ConstantSettings.DLL_TOKEN_NODE, COBRA_GLOBAL.CurrentDLLToken);

            SharedAPI.XmlAddOneNode(doc, root, ConstantSettings.PARAM_TOKEN_NODE, COBRA_GLOBAL.CurrentParamToken);

            SharedAPI.XmlAddOneNode(doc, root, ConstantSettings.BOARD_TOKEN_NODE, COBRA_GLOBAL.CurrentBoardToken);

            SharedAPI.XmlAddOneNode(doc, root, ConstantSettings.PRODUCT_FAMILY_NODE, ProductFamily);

            SharedAPI.XmlAddOneNode(doc, root, ConstantSettings.BIN_MD5_NODE, BIN_MD5_STR);

            var cfgList = cfgViewModel.sfl_parameterlist.ToList();
            CreateSubNodes(doc, root, ConstantSettings.CFG_NODE, cfgList);
            var boardList = boardViewModel.sfl_parameterlist.ToList();
            CreateSubNodes(doc, root, ConstantSettings.BOARD_NODE, boardList);

            string hash;
            //hash = GetUIMD5Code(TotalList, BIN_MD5_STR);
            hash = GetXMLMD5Code(root);
            SharedAPI.XmlAddOneNode(doc, root, ConstantSettings.MD5_NODE, hash);

            doc.Save(cfgfullpath);
        }

        private void CreateSubNodes(XmlDocument doc, XmlElement entry, string nodeName, List<SFLModel> list)
        {
            var cfgentry = SharedAPI.XmlAddOneNode(doc, entry, nodeName);
            foreach (SFLModel model in list)
            {
                if (model == null) continue;
                string strval = string.Empty;
                model.GetStringValue(ref strval);
                var dic = new Dictionary<string, string>();
                dic.Add("Name", model.nickname);
                SharedAPI.XmlAddOneNode(doc, cfgentry, "item", strval, dic);
            }
        }

        private string SaveHexFile(string fullpath)	//Issue1513 Leon
        {
            msg.sm.efusebindata.Clear();

            msg.task_parameterlist = cfgViewModel.dm_parameterlist;
            msg.gm.sflname = sflname;
            msg.sub_task = SaveHexSubTask;
            msg.sub_task_json = fullpath;
            msg.task = TM.TM_COMMAND;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();

            return GetMd5Hash(msg.sm.efusebindata.ToArray());
        }

        private void SaveCFile(string fullpath)
        {
            int nline = 0;

            cfgViewModel.UpdateAllModels();
            msg.task_parameterlist = cfgViewModel.dm_parameterlist;
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
                            typedefDic.Add(str.Substring(0, str.IndexOf('[')), nline);
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
                        mode = (md as SFLModel);
                        if (mode != null)
                        {
                            stB.Append('\r');
                            stB.Append('\n');
                            cfgViewModel.FWHexTostr(mode);
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
                sw.Write(string.Format("{0}{1}", line, "\r\n"));
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
                sLine = Regex.Replace(sLine, "[\r\n\t]", " ", RegexOptions.Compiled);
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
                        mode = (md as SFLModel);
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
                sw.Write(string.Format("{0}{1}", line, "\r\n"));
            sr.Close();
            rf.Close();
            sw.Close();
            wf.Close();
        }
        #endregion
        #region Load
        private void LoadBtn_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (isBoardConfig())    //Load CSV file
            {
                string fullpath = "";
                Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();

                openFileDialog.Title = "Load CSV File";
                openFileDialog.Filter = "Device Configuration file (*.csv)|*.csv||";
                openFileDialog.DefaultExt = "csv";
                openFileDialog.FileName = "default";
                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;
                openFileDialog.InitialDirectory = FolderMap.m_currentproj_folder;
                if (openFileDialog.ShowDialog() == true)
                {
                    if (parent == null) return;
                    {
                        bool ret = true;
                        fullpath = openFileDialog.FileName;
                        ret = LoadBoardConfigFromCSV(fullpath);
                        if (ret == true)
                        {
                            StatusLabel.Content = fullpath;
                        }
                        else
                        {
                            gm.message = "Load failed! Please check the file format.";
                            CallWarningControl(gm);
                        }
                    }
                }
                else
                    return;
            }
            else// Load CFG file
            {
                string fullpath = "";
                Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();

                openFileDialog.Title = "Load Configuration File";
                openFileDialog.Filter = "Device Configuration file (*.cfg)|*.cfg||";
                openFileDialog.DefaultExt = "cfg";

                openFileDialog.FileName = "default";
                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;
                //openFileDialog.InitialDirectory = FolderMap.m_currentproj_folder;
                if (openFileDialog.ShowDialog() == true)
                {
                    if (parent == null) return;
                    {
                        bool ret = true;
                        fullpath = openFileDialog.FileName;

                        ret = LoadFile(fullpath);

                        if (ret == true)
                        {
                            StatusLabel.Content = fullpath;
                        }
                        Registry.SaveConfigFilePath(fullpath);//Issue1378 Leon

                    }
                }
                else
                    return;
            }
        }
        internal bool LoadFile(string fullpath)
        {
            string warning = string.Empty;
            #region File format check
            XmlDocument doc = new XmlDocument();
            try
            {
                doc.Load(fullpath);
            }
            catch
            {
                gm.message = "File format error!";
                CallWarningControl(gm);
                return false;
            }
            #endregion
            XmlElement root = doc.DocumentElement;

            #region MD5 Check
            string hashofxml = GetXMLMD5Code(root);
            string hashinxml = GetValueFromXML(root, ConstantSettings.MD5_NODE);
            if (string.IsNullOrEmpty(hashinxml))
            {
                msg.gm.message = "Cannot get MD5 in file!\nProceed?";
                CallSelectControl(msg.gm);
                if (!msg.controlmsg.bcancel)
                    return false;
                else
                    warning = string.Empty;
            }
            else if (hashofxml != hashinxml)
            {
                //warning = $"MD5 in file: {hashinxml}\n";
                //warning += $"MD5 of file: {hashofxml}\n";
                warning += "File has been changed!\nProceed?";
                msg.gm.message = warning;
                CallSelectControl(msg.gm);
                if (!msg.controlmsg.bcancel)
                    return false;
                else
                    warning = string.Empty;
            }
            #endregion
            #region CFG Version Check
            string CFGVersionInXML = GetValueFromXML(root, ConstantSettings.CFG_VERSION_NODE);
            if (string.IsNullOrEmpty(CFGVersionInXML))
            {
                msg.gm.message = "No CFG Version in XML!\nProceed?";
                CallSelectControl(msg.gm);
                if (!msg.controlmsg.bcancel)
                    return false;
                else
                    warning = string.Empty;
            }
            else if (CFGVersionInXML != ConstantSettings.CFG_VERSION_INT.ToString())
            {
                //warning = $"CFG Version in file: {CFGVersionInXML}\n";
                //warning += $"CFG Version you are using: {ConstantSettings.CFG_VERSION_INT.ToString()}\n";
                warning += "CFG Version Mismatch!\nProceed?";
                msg.gm.message = warning;
                CallSelectControl(msg.gm);
                if (!msg.controlmsg.bcancel)
                    return false;
                else
                    warning = string.Empty;
            }

            #endregion
            #region Token Check
            warning = string.Empty;
            #region OCETokenMD5 Check
            string OCETokenMD5FromRuntime = string.Empty;
            OCETokenMD5FromRuntime = COBRA_GLOBAL.CurrentOCEToken;
            string OCETokenInXML = GetValueFromXML(root, ConstantSettings.OCE_TOKEN_NODE);
            if (string.IsNullOrEmpty(OCETokenInXML))
            {
                warning += "No OCE Token in XML!\n";
            }
            else if (OCETokenInXML != OCETokenMD5FromRuntime)
            {
                //warning += $"OCE Token in file: {OCETokenInXML}\n"; 
                //warning += $"OCE Token in OCE: {OCETokenMD5FromRuntime}\n";
                warning += "CFG file is not made by current OCE!\n";
            }
            #endregion
            #region DLLToken Check
            string DLLTokenFromRuntime = string.Empty;
            DLLTokenFromRuntime = COBRA_GLOBAL.CurrentDLLToken;
            string DLLTokenInXML = GetValueFromXML(root, ConstantSettings.DLL_TOKEN_NODE);
            if (string.IsNullOrEmpty(DLLTokenInXML))
            {
                warning += "No DLL Token in XML!\n";
            }
            else if (DLLTokenInXML != DLLTokenFromRuntime)
            {
                //warning += $"DLL in file: {DLLTokenInXML}\n";
                //warning += $"DLL in OCE: {DLLTokenFromRuntime}\n";
                warning += "Converting algorithm may be different!\n";
            }
            #endregion
            #region ParamToken Check
            string ParamTokenFromRuntime = string.Empty;
            ParamTokenFromRuntime = COBRA_GLOBAL.CurrentParamToken;
            string ParamTokenInXML = GetValueFromXML(root, ConstantSettings.PARAM_TOKEN_NODE);
            if (string.IsNullOrEmpty(ParamTokenInXML))
            {
                warning += "No Param Token in XML!\n";
            }
            else if (ParamTokenInXML != ParamTokenFromRuntime)
            {
                //warning += $"Param in file: {ParamTokenInXML}\n";
                //warning += $"Param Token in OCE: {ParamTokenFromRuntime}\n";
                warning += "Parameters are different! Paramenters in CFG file may be insufficient, superfluous or invalid.\n";
            }
            #endregion
            #region BoardToken Check
            string BoardTokenFromRuntime = string.Empty;
            BoardTokenFromRuntime = COBRA_GLOBAL.CurrentBoardToken;
            string BoardTokenInXML = GetValueFromXML(root, ConstantSettings.BOARD_TOKEN_NODE);
            if (string.IsNullOrEmpty(BoardTokenInXML))
            {
                warning += "No Board Token in XML!\n";
            }
            else if (BoardTokenInXML != BoardTokenFromRuntime)
            {
                //warning += $"Board in file: {BoardTokenInXML}\n";
                //warning += $"Board Token in OCE: {BoardTokenFromRuntime}\n";
                warning += "Board settings are different! Board settings in CFG file may be insufficient, superfluous or invalid.\n\n";
            }
            #endregion
            if (warning != string.Empty)
            {
                msg.gm.message = warning + "Proceed?";
                CallSelectControl(msg.gm);
                if (!msg.controlmsg.bcancel) return false;
            }
            #endregion
            #region Product Family Check
            string ProductFamilyRuntime = string.Empty;
            ProductFamilyRuntime = ProductFamily;
            if (ProductFamilyRuntime == string.Empty)   //有些OCE可能暂时没有Product Family
            {
                ;
            }
            string ProductFamilyInXML = GetValueFromXML(root, ConstantSettings.PRODUCT_FAMILY_NODE);
            if (string.IsNullOrEmpty(ProductFamilyInXML))   //所以对应的可能cfg文件中也没有Product Family
            {
                ;
            }
            if (ProductFamilyInXML != ProductFamilyRuntime) //能否加载cfg文件，只看CFG Version, OCEToken和MD5
            {
                //string warning = "Product Family in file: " + ProductFamilyInXML;
                //warning += "\nProduct Family you are using: " + ProductFamilyRuntime;
                //warning += "\nProduct Family Mismatch! Load failed!";
                //gm.message = warning;
                //gm.level = 2;
                //CallWarningControl(gm);
                //return false;
            }

            #endregion
            //No need to check BIN_MD5 here

            foreach (XmlNode xn in root.ChildNodes)
            {
                if (xn.Name == ConstantSettings.CFG_NODE)
                {
                    UpdateModelWithNodes(xn, cfgViewModel.sfl_parameterlist.ToList());
                }
                else if (xn.Name == ConstantSettings.BOARD_NODE)
                {
                    UpdateModelWithNodes(xn, boardViewModel.sfl_parameterlist.ToList());
                }
            }
            cfgViewModel.UpdateAllModels();     //加载完之后，直接让cfg settings生效
            boardViewModel.UpdateAllModels();   //加载完之后，直接让board settings生效
            return true;
        }

        private void UpdateModelWithNodes(XmlNode xn, List<SFLModel> list)
        {
            string tmp;
            SFLModel model;
            foreach (XmlElement xe in xn.ChildNodes)
            {
                string name = xe.GetAttribute("Name");
                //model = cfgViewModel.GetParameterByName(name);
                model = list.SingleOrDefault(o => o.nickname == name);
                if (model == null) continue;

                tmp = xe.InnerText;

                model.UpdateFromStringValue(tmp);
            }
        }

        private string GetValueFromXML(XmlElement xe, string name)
        {
            XmlNode xn = xe.SelectSingleNode(name);
            if (xn != null)
                return xn.InnerText;
            else
                return string.Empty;
        }
        #endregion
        #region MD5
        private string GetXMLMD5Code(XmlElement root)
        {
            StringBuilder sb = new StringBuilder();
            foreach (XmlNode xn in root.ChildNodes)
            {
                string nodeName = xn.LocalName;
                if (nodeName == ConstantSettings.MD5_NODE)
                {
                    continue;
                }
                else if (nodeName == ConstantSettings.CFG_NODE || nodeName == ConstantSettings.BOARD_NODE)
                {
                    foreach (XmlElement subxn in xn.ChildNodes)
                    {
                        sb.Append(subxn.GetAttribute("Name"));
                        sb.Append(subxn.InnerText);
                    }
                }
                else
                {
                    sb.Append(nodeName);
                    sb.Append(xn.InnerText);
                }
            }
            string hash = GetMd5Hash(sb.ToString());
            return hash;
        }

        private string GetMd5Hash(string input)
        {
            byte[] data;
            using (MD5 md5Hash = MD5.Create())
            {
                // Convert the input string to a byte array and compute the hash.
                data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));
            }
            return GetStringFromBytes(data.ToList());
        }
        private string GetMd5Hash(byte[] binData)
        {
            byte[] data;
            using (MD5 md5Hash = MD5.Create())
            {
                // Convert the input string to a byte array and compute the hash.
                data = md5Hash.ComputeHash(binData);
            }
            return GetStringFromBytes(data.ToList());
        }

        private string GetStringFromBytes(List<byte> data)
        {
            // Create a new Stringbuilder to collect the bytes
            // and create a string.
            StringBuilder sBuilder = new StringBuilder();

            // Loop through each byte of the hashed data 
            // and format each one as a hexadecimal string.
            foreach (var d in data)
            {
                sBuilder.Append(d.ToString("x2"));
            }

            // Return the hexadecimal string.
            return sBuilder.ToString();
        }
        #endregion

        #region Borad Config Related
        private void SaveConfigToInternalMemory()//Issue1378 Leon
        {
            foreach (SFLModel model in TotalList)
            {
                string name = model.nickname;
                string strval = string.Empty;
                model.GetStringValue(ref strval);
                BCImg.Add(name, strval);
            }
        }

        public bool LoadConfigFromInternalMemory()//Issue1378 Leon
        {
            foreach (SFLModel model in TotalList)
            {
                if (BCImg.ContainsKey(model.nickname))
                {
                    var str = BCImg[model.nickname];
                    model.UpdateFromStringValue(str);
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        public void Preload(string fullpath)
        {
            if (LoadFile(fullpath))
                StatusLabel.Content = fullpath;
        }

        private bool LoadBoardConfigFromCSV(string fullpath)
        {
            if (!CSVFileCheck(fullpath))
                return false;
            var dic = LoadCSVToDic(fullpath);
            foreach (var row in dic)
            {
                var model = cfgViewModel.GetParameterByName(row.Key);
                if (model == null)
                    continue;
                else
                {
                    model.berror = false;
                    model.data = row.Value * 1000;
                }
            }
            return true;
        }

        private bool CSVFileCheck(string fullpath)
        {
            var output = false;
            if (!File.Exists(fullpath))
                return output;
            FileStream file = new FileStream(fullpath, FileMode.Open);
            StreamReader sr = new StreamReader(file);
            List<string> strlist;
            string strlin;
            while ((strlin = sr.ReadLine()) != null)
            {
                strlist = GetCSVStrList(strlin);
                if (strlist.Count != 2)
                    return output;
            }
            sr.Close();
            file.Close();
            return true;
        }

        public Dictionary<string, double> LoadCSVToDic(string filePath)//从csv读取数据返回table
        {
            var output = new Dictionary<string, double>();
            FileStream file = new FileStream(filePath, FileMode.Open);
            StreamReader sr = new StreamReader(file);
            List<string> strlist;
            string strlin;
            while ((strlin = sr.ReadLine()) != null)
            {
                strlist = GetCSVStrList(strlin);
                output.Add(strlist[0], Convert.ToDouble(strlist[1]));
            }
            sr.Close();
            file.Close();
            return output;
        }
        private List<string> GetCSVStrList(string strline)  //Load cvs file line to string list
        {
            string[] strArray = strline.Split(',');
            List<string> strlist = new List<string>();
            foreach (string str in strArray)
                strlist.Add(str);
            return strlist;
        }
        #endregion


        #region Read
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
                    cfgViewModel.BuildPartParameterList(cm.PlacementTarget.Uid);
                    msg.gm.controls = "Read One parameter";
                    msg.task_parameterlist = cfgViewModel.dm_part_parameterlist;
                    break;
                case "Button":
                    msg.gm.controls = ((System.Windows.Controls.Button)sender).Content.ToString();
                    msg.task_parameterlist = cfgViewModel.dm_parameterlist;

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
            if (isBoardConfig())//support them both in COBRA2.00.15, so all old and new OCEs will work fine.//Issue 1426 Leon
                Reset();//Issue1381 Leon
            else if (ReadSubTask != 0)     //当前oce支持subtask特性 Issue1363 Leon
                ReadCommand(ReadSubTask);
            else
                Read();
        }
        private void Reset()//Issue1373 Leon
        {
            LoadConfigFromInternalMemory();
        }
        private void ReadCommand(ushort subtask)	//Issue1363 Leon    Let DEM deal with the process, not SFL
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
            msg.task = TM.TM_COMMAND;
            msg.sub_task = subtask;
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
            return;
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
            return;
        }
        #endregion
        #region Write
        private void WriteBtn_Click(object sender, RoutedEventArgs e)
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            ret = ParameterValidityCheck();
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                var m = new GeneralMessage();
                m.message = LibErrorCode.GetErrorDescription(ret);
                m.level = 2;
                CallWarningControl(m);
                return;
            }
            msg.owner = this;
            msg.gm.sflname = sflname;
            btnControl btn_ctrl = null;
            switch (sender.GetType().Name)
            {
                case "MenuItem":
                    var cl = sender as System.Windows.Controls.MenuItem;
                    var cm = cl.Parent as System.Windows.Controls.ContextMenu;
                    cfgViewModel.BuildPartParameterList(cm.PlacementTarget.Uid);
                    msg.gm.controls = "Write One parameter";
                    msg.task_parameterlist = cfgViewModel.dm_part_parameterlist;
                    break;
                case "Button":
                    if (!isBoardConfig())
                    {
                        msg.gm.message = "you are ready to write entirely area,please be care!";
                        msg.controlreq = COMMON_CONTROL.COMMON_CONTROL_SELECT;
                        if (!msg.controlmsg.bcancel) return;
                    }

                    msg.gm.controls = ((System.Windows.Controls.Button)sender).Content.ToString();
                    msg.task_parameterlist = cfgViewModel.dm_parameterlist;

                    System.Windows.Controls.Button btn = sender as System.Windows.Controls.Button;
                    btn_ctrl = ui_config.GetBtnControlByName(btn.Name);
                    if (btn_ctrl == null) break;
                    if (btn_ctrl.btn_menu_control.Count == 0) break;

                    btn_ctrl.btn_cm.PlacementTarget = btn;
                    btn_ctrl.btn_cm.IsOpen = true;
                    return;
            }
            if (WriteSubTask != 0)		//Issue1363 Leon
                WriteCommand(WriteSubTask);
            else
                write();
        }
        private void WriteCommand(ushort subtask)	//Issue1363 Leon
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

            ret = cfgViewModel.UpdateAllModels();
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
            msg.task = TM.TM_COMMAND;
            msg.sub_task = WriteSubTask;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                if (msg.errorcode == LibErrorCode.IDS_ERR_SECTION_DEVICECONFSFL_PARAM_VERIFY)
                {
                    UInt32 guid = 0x00;
                    StringBuilder sb = new StringBuilder();
                    var Verify_Dic = SharedAPI.DeserializeStringToDictionary<string, string>(m_Msg.sub_task_json);
                    foreach (KeyValuePair<string, string> str in Verify_Dic)
                    {
                        if (!UInt32.TryParse(str.Key, out guid)) continue;
                        SFLModel model = cfgViewModel.GetParameterByGuid(guid);
                        if (model == null) continue;
                        sb.Append(string.Format("{0} {1}.\n", model.nickname, str.Value));
                    }
                    gm.level = 2;
                    gm.message = sb.ToString();
                    CallWarningControl(gm);
                    parent.bBusy = false;
                    return;
                }
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
            UInt32 guid = 0x00;
            StringBuilder sb = new StringBuilder();
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

            ret = cfgViewModel.UpdateAllModels();
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

            if (bVerify)
            {
                msg.percent = 60;
                msg.task = TM.TM_SPEICAL_VERIFICATION;
                parent.AccessDevice(ref m_Msg);
                while (msg.bgworker.IsBusy)
                    System.Windows.Forms.Application.DoEvents();
                if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                {
                    gm.level = 2;
                    Verify_Dic = SharedAPI.DeserializeStringToDictionary<string, string>(m_Msg.sub_task_json);
                    foreach (KeyValuePair<string, string> str in Verify_Dic)
                    {
                        if (!UInt32.TryParse(str.Key, out guid)) continue;
                        SFLModel model = cfgViewModel.GetParameterByGuid(guid);
                        if (model == null) continue;
                        sb.Append(string.Format("{0} {1}.\n", model.nickname, str.Value));
                    }
                    gm.message = sb.ToString();
                    CallWarningControl(gm);
                    parent.bBusy = false;
                    return;
                }
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
        #endregion
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
            msg.task_parameterlist = cfgViewModel.dm_parameterlist;
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
            }

            parent.bBusy = false;
        }

        private void VerifyBtn_Click(object sender, RoutedEventArgs e)
        {
            //MessageBox.Show("Please make sure Board Settings are correct first!");
            string ExcelFilePath = string.Empty;
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Title = "Load Excel File";
            openFileDialog.Filter = "Excel file (*.xlsx)|*.xlsx||";
            openFileDialog.DefaultExt = "xlsx";
            openFileDialog.FileName = "default";
            openFileDialog.FilterIndex = 1;
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == true)
            {
                ExcelFilePath = openFileDialog.FileName;
            }
            else
            { return; }

            //cfgviewmodel.WriteDevice();

            var excelApp = new Excel.Application();
            Excel.Workbook excelWKB = null;
            Excel._Worksheet excelSHEET = null;
            try
            {
                excelWKB = excelApp.Workbooks.Open(ExcelFilePath);
            }
            catch (Exception c)
            {
                System.Windows.MessageBox.Show(c.ToString());
            }
            foreach (var p in cfgViewModel.sfl_parameterlist)
            {
                string relatedname = "";
                excelSHEET = null;	//Issue1549
                foreach (Excel._Worksheet st in excelWKB.Sheets)
                {
                    string fulltargetname = st.Name.Replace(' ', '/');
                    string targetname = "";
                    if (fulltargetname.StartsWith("("))
                    {
                        targetname = fulltargetname.Remove(0, fulltargetname.IndexOf(')') + 1);
                        if (targetname == p.nickname)
                        {
                            excelSHEET = st;
                            relatedname = fulltargetname.Substring(fulltargetname.IndexOf('(') + 1, fulltargetname.IndexOf(')') - fulltargetname.IndexOf('(') - 1);
                            break;
                        }
                    }
                    else
                    {
                        if (fulltargetname == p.nickname)
                        {
                            excelSHEET = st;
                            break;
                        }
                    }
                }
                if (excelSHEET == null)	//Issue1549
                    continue;
                try
                {
                    if (excelSHEET != null)
                    {
                        int colcnt = excelSHEET.UsedRange.Columns.Count;
                        int rowcnt = excelSHEET.UsedRange.Rows.Count;
                        if (relatedname == "") //普通参数
                        {
                            for (int row = 2; row <= rowcnt; row++)
                            {
                                #region excel cell中的数据转到SFLViewModel中去
                                string tmp = ((Excel.Range)excelSHEET.Cells[row, 1]).Text.ToString();
                                if (tmp == "")
                                    break;
                                double dval = 0.0;
                                if (p.brange)//为正常录入浮点数
                                {
                                    switch (p.format)
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
                                                catch (Exception ex)
                                                {
                                                    dval = 0.0;
                                                    break;
                                                }
                                                break;
                                            }
                                        default:
                                            break;
                                    }
                                    p.data = dval;
                                }
                                else
                                    p.sphydata = tmp;
                                #endregion

                                //WriteDevice(ref p);
                                #region WriteDevice SFLViewModel转到Parameter中去
                                if (p.berror && (p.errorcode & LibErrorCode.IDS_ERR_SECTION_DEVICECONFSFL) == LibErrorCode.IDS_ERR_SECTION_DEVICECONFSFL)
                                    return;

                                p.IsWriteCalled = true;

                                Parameter param = p.parent;
                                if (p.brange)
                                    param.phydata = p.data;
                                else
                                    param.sphydata = p.sphydata;

                                p.IsWriteCalled = false;
                                #endregion


                                #region 调用DEM API
                                msg.owner = this;
                                msg.gm.sflname = sflname;
                                msg.funName = "Verify";
                                var list = new AsyncObservableCollection<Parameter>();
                                list.Add(p.parent);
                                msg.task_parameterlist.parameterlist = list;
                                msg.task = TM.TM_CONVERT_PHYSICALTOHEX;
                                parent.AccessDevice(ref m_Msg);
                                while (msg.bgworker.IsBusy)
                                    System.Windows.Forms.Application.DoEvents();
                                if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                                    return;
                                #endregion

                                #region 新建一列放入计算值
                                excelSHEET.Cells[row, 6] = p.parent.hexdata;
                                #endregion

                                #region 新建一列放入比较值
                                string strAnswer = ((Excel.Range)excelSHEET.Cells[row, 2]).Text.ToString();
                                UInt16 answer = Convert.ToUInt16(strAnswer, 2);
                                excelSHEET.Cells[row, 7] = p.parent.hexdata - answer;
                                #endregion
                            }
                        }
                        else    //依赖参数
                        {
                            for (int row = 2; row <= rowcnt; row++)
                            {
                                #region 第一列
                                //获取TH参数的Name
                                string p1name = relatedname;
                                //根据Name获取参数
                                var p1 = cfgViewModel.GetParameterByName(p1name);
                                #region excel cell中的数据转到SFLViewModel中去
                                string tmp = ((Excel.Range)excelSHEET.Cells[row, 2]).Text.ToString();
                                double dval = 0.0;
                                if (p1.brange)//为正常录入浮点数
                                {
                                    switch (p1.format)
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
                                                catch (Exception ex)
                                                {
                                                    dval = 0.0;
                                                    break;
                                                }
                                                break;
                                            }
                                        default:
                                            break;
                                    }
                                    p1.data = dval;
                                }
                                else
                                    p1.sphydata = tmp;
                                #endregion

                                //WriteDevice(ref p1);
                                #region WriteDevice SFLViewModel转到Parameter中去
                                if (p1.berror && (p1.errorcode & LibErrorCode.IDS_ERR_SECTION_DEVICECONFSFL) == LibErrorCode.IDS_ERR_SECTION_DEVICECONFSFL)
                                    return;

                                p1.IsWriteCalled = true;

                                Parameter param1 = p1.parent;
                                if (p1.brange)
                                    param1.phydata = p1.data;
                                else
                                    param1.sphydata = p1.sphydata;

                                p1.IsWriteCalled = false;
                                #endregion
                                #endregion
                                #region 第二列
                                #region excel cell中的数据转到SFLViewModel中去
                                tmp = ((Excel.Range)excelSHEET.Cells[row, 1]).Text.ToString();
                                dval = 0.0;
                                if (p.brange)//为正常录入浮点数
                                {
                                    switch (p.format)
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
                                                catch (Exception ex)
                                                {
                                                    dval = 0.0;
                                                    break;
                                                }
                                                break;
                                            }
                                        default:
                                            break;
                                    }
                                    p.data = dval;
                                }
                                else
                                    p.sphydata = tmp;
                                #endregion

                                //WriteDevice(ref p);
                                #region WriteDevice SFLViewModel转到Parameter中去
                                if (p.berror && (p.errorcode & LibErrorCode.IDS_ERR_SECTION_DEVICECONFSFL) == LibErrorCode.IDS_ERR_SECTION_DEVICECONFSFL)
                                    return;

                                p.IsWriteCalled = true;

                                Parameter param = p.parent;
                                if (p.brange)
                                    param.phydata = p.data;
                                else
                                    param.sphydata = p.sphydata;

                                p.IsWriteCalled = false;
                                #endregion
                                #endregion


                                #region 调用DEM API
                                msg.owner = this;
                                msg.gm.sflname = sflname;
                                var list = new AsyncObservableCollection<Parameter>();
                                list.Add(param1);
                                list.Add(param);
                                msg.task_parameterlist.parameterlist = list;
                                msg.task = TM.TM_CONVERT_PHYSICALTOHEX;
                                parent.AccessDevice(ref m_Msg);
                                while (msg.bgworker.IsBusy)
                                    System.Windows.Forms.Application.DoEvents();
                                if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                                    return;
                                #endregion

                                #region 新建一列放入计算值
                                excelSHEET.Cells[row, 6] = p.parent.hexdata;
                                #endregion

                                #region 新建一列放入比较值
                                string strAnswer = ((Excel.Range)excelSHEET.Cells[row, 3]).Text.ToString();
                                UInt16 answer = Convert.ToUInt16(strAnswer, 2);
                                excelSHEET.Cells[row, 7] = p.parent.hexdata - answer;
                                #endregion
                            }
                        }
                    }
                }
                catch (Exception c)
                {
                    System.Windows.MessageBox.Show(c.ToString());
                }
                finally
                {
                    //excelWKB.Close(true);
                    //System.Runtime.InteropServices.Marshal.ReleaseComObject(excelWKB);
                    //excelWKB = null;
                }
            }
            excelApp.Workbooks.Close();
            excelApp.Quit();
            System.Runtime.InteropServices.Marshal.ReleaseComObject(excelApp);
            excelApp = null;
            System.Windows.MessageBox.Show("Done!");
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

        #region 计算边界值   //Issue1593 Leon

        public double GetMaxValue(Parameter param)
        {
            TASKMessage msg = new TASKMessage();
            msg.task = TM.TM_COMMAND;
            msg.sub_task = GetMaxValueSubTask;
            msg.task_parameterlist.parameterlist.Add(param);
            parent.AccessDevice(ref msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode == LibErrorCode.IDS_ERR_SUCCESSFUL)
                return param.dbPhyMax;
            else
                return 0;
        }

        public double GetMinValue(Parameter param)
        {
            TASKMessage msg = new TASKMessage();
            msg.task = TM.TM_COMMAND;
            msg.sub_task = GetMinValueSubTask;
            msg.task_parameterlist.parameterlist.Add(param);
            parent.AccessDevice(ref msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode == LibErrorCode.IDS_ERR_SUCCESSFUL)
                return param.dbPhyMin;
            else
                return 0;
        }
        #endregion

    }
}