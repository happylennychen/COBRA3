﻿using System;
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
using O2Micro.Cobra.EM;
using O2Micro.Cobra.Common;
using Excel = Microsoft.Office.Interop.Excel;

namespace O2Micro.Cobra.DeviceConfigurationPanel
{
    enum editortype
    {
        TextBox_EditType = 0,
        ComboBox_EditType = 1,
        CheckBox_EditType = 2
    }

    public static class ConstantSettings
    {
        public readonly static string CFG_VERSION_NODE = "CFG_VERSION";
        public readonly static string OCE_TOKEN_NODE = "OCE_TOKEN";
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

        public SFLViewModel cfgViewModel { get; set; }

        public SFLViewModel boardViewModel { get; set; }

        private BackgroundWorker m_BackgroundWorker;// 申明后台对象
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

        public MainControl(object pParent, string name)
        {
            this.InitializeComponent();
            #region 相关初始化
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
            GetProductFamily();
            #endregion

            InitalUI();
            LibInfor.AssemblyRegister(Assembly.GetExecutingAssembly(), ASSEMBLY_TYPE.SFL);
            InitBWork();
            gm.PropertyChanged += new PropertyChangedEventHandler(gm_PropertyChanged);
            msg.PropertyChanged += new PropertyChangedEventHandler(msg_PropertyChanged);
            msg.gm.PropertyChanged += new PropertyChangedEventHandler(gm_PropertyChanged);

            cfgViewModel = new SFLViewModel(pParent, this, sflname);
            boardViewModel = new SFLViewModel(pParent, this, BoardConfigLabel);

            if (sflname == CobraGlobal.Constant.OldBoardConfigName || sflname == CobraGlobal.Constant.NewBoardConfigName)//support them both in COBRA2.00.15, so all old and new OCEs will work fine.//Issue 1426 Leon
            {
                SaveBoardConfigToInternalMemory();//Issue1378 Leon
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

        private void GetProductFamily()
        {
            string xmlfilepath = FolderMap.m_extension_work_folder + FolderMap.m_dev_descrip_xml_name + FolderMap.m_extension_work_ext;
            XmlDocument doc = new XmlDocument();
            doc.Load(xmlfilepath);
            ProductFamily = doc.DocumentElement.GetAttribute(ConstantSettings.PRODUCT_FAMILY_NODE);
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
            if (sflname == CobraGlobal.Constant.OldBoardConfigName || sflname == CobraGlobal.Constant.NewBoardConfigName)//support them both in COBRA2.00.15, so all old and new OCEs will work fine.	//Issue686//Issue 1426 Leon
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

            string fullpath = "";
            string chipname = GetChipName();    //Issue1373
                                                //string MD5ShortCode = GetShortCode(GetUIMD5Code(viewmode.sfl_parameterlist.ToList()));
                                                //if (sflname == CobraGlobal.Constant.OldBoardConfigName || sflname == CobraGlobal.Constant.NewBoardConfigName)//support them both in COBRA2.00.15, so all old and new OCEs will work fine.//Issue 1426 Leon
                                                //{
                                                //    Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
                                                //    saveFileDialog.FilterIndex = 1;
                                                //    saveFileDialog.RestoreDirectory = true;

            //    saveFileDialog.FileName = chipname + "-" + MD5ShortCode;//Issue1373 Leon
            //    saveFileDialog.Title = "Save Board Config file";
            //    saveFileDialog.Filter = "Board Config file (*.board)|*.board||";
            //    saveFileDialog.DefaultExt = "board";

            //    saveFileDialog.InitialDirectory = FolderMap.m_currentproj_folder;
            //    if (saveFileDialog.ShowDialog() == true)
            //    {
            //        if (parent == null) return;
            //        else
            //        {
            //            fullpath = saveFileDialog.FileName;
            //            SaveBoardFile(fullpath);
            //        }
            //    }
            //    else return;

            //    StatusLabel.Content = fullpath;

            //    SaveBoardConfigFilePath(fullpath);
            //}
            //else
            //{
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            saveFileDialog.FilterIndex = 1;
            saveFileDialog.RestoreDirectory = true;

            saveFileDialog.FileName = chipname;// + "-" + DateTime.Now.ToString("yyyyMMddHHmmss");//Issue1373 Leon
                                               //saveFileDialog.Title = "Save Configuration File";
                                               //saveFileDialog.Filter = "Device Configuration file (*.cfg)|*.cfg|c file (*.c)|*.c|h file (*.h)|*.h||";
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
                }
            }
            else return;

            StatusLabel.Content = fullpath;
            //}
        }

        private UInt32 ParameterValidityCheck()   //Issue1607 Leon  check parameter validity before save
        {
            foreach (SFLModel model in cfgViewModel.sfl_parameterlist)
            {
                if (model.berror && (model.errorcode & LibErrorCode.IDS_ERR_SECTION_DEVICECONFSFL) == LibErrorCode.IDS_ERR_SECTION_DEVICECONFSFL)
                    return LibErrorCode.IDS_ERR_SECTION_DEVICECONFSFL_PARAM_INVALID;
            }
            return LibErrorCode.IDS_ERR_SUCCESSFUL;
        }
        private string GetChipName()//Issue1373 Leon Get chip name to form file name before save（考虑放到初始化中）
        {
            XmlElement root = EMExtensionManage.m_extDescrip_xmlDoc.DocumentElement;
            return root.GetAttribute("chip");
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

            string originalfilename = System.IO.Path.GetFileNameWithoutExtension(cfgfullpath);
            string originalfolder = System.IO.Path.GetDirectoryName(cfgfullpath);
            string newfolder = System.IO.Path.Combine(originalfolder, originalfilename + "-" + DateTime.Now.ToString("yyyyMMddhhmmss"));
            if (!Directory.Exists(newfolder))
                Directory.CreateDirectory(newfolder);
            string filename = System.IO.Path.GetFileName(cfgfullpath);

            string hexfullpath = System.IO.Path.Combine(newfolder, originalfilename + ".hex");
            string BIN_MD5_STR = SaveHexFile(hexfullpath);
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

            string SOCETokenMD5 = CobraGlobal.CurrentOCETokenMD5;
            string SCFGVersion = ConstantSettings.CFG_VERSION_INT.ToString();
            XmlAddOneNode(doc, root, ConstantSettings.CFG_VERSION_NODE, SCFGVersion);

            XmlAddOneNode(doc, root, ConstantSettings.OCE_TOKEN_NODE, SOCETokenMD5);

            XmlAddOneNode(doc, root, ConstantSettings.PRODUCT_FAMILY_NODE, ProductFamily);

            XmlAddOneNode(doc, root, ConstantSettings.BIN_MD5_NODE, BIN_MD5_STR);

            var cfgList = cfgViewModel.sfl_parameterlist.ToList();
            CreateSubNodes(doc, root, ConstantSettings.CFG_NODE, cfgList);
            var boardList = boardViewModel.sfl_parameterlist.ToList();
            CreateSubNodes(doc, root, ConstantSettings.BOARD_NODE, boardList);

            List<SFLModel> totalList = new List<SFLModel>(cfgList);
            totalList.AddRange(boardList);

            string hash;
            hash = GetUIMD5Code(totalList, BIN_MD5_STR);
            XmlAddOneNode(doc, root, ConstantSettings.MD5_NODE, hash);

            doc.Save(cfgfullpath);
        }

        private void CreateSubNodes(XmlDocument doc, XmlElement entry, string nodeName, List<SFLModel> list)
        {
            var cfgentry = XmlAddOneNode(doc, entry, nodeName);
            foreach (SFLModel model in list)
            {
                if (model == null) continue;
                string strval;
                switch (model.editortype)
                {
                    case 0:
                        {
                            strval = model.sphydata;
                            break;
                        }
                    case 1: //ComboBox
                        {
                            strval = model.itemlist[model.listindex];
                            break;
                        }
                    case 2:
                        {
                            strval = String.Format("{0:F1}", model.data);
                            break;
                        }
                    default:
                        strval = model.sphydata;
                        break; ;
                }
                var dic = new Dictionary<string, string>();
                dic.Add("Name", model.nickname);
                XmlAddOneNode(doc, cfgentry, "item", strval, dic);
            }
        }

        private string SaveHexFile(string fullpath)	//Issue1513 Leon
        {
            msg.sm.efusebindata.Clear();
            if (SaveHexSubTask != 0)
            {
                cfgViewModel.UpdateAllModels();
                msg.task_parameterlist = cfgViewModel.dm_parameterlist;
                msg.sub_task = SaveHexSubTask;
                msg.sub_task_json = fullpath;
                msg.task = TM.TM_COMMAND;
                parent.AccessDevice(ref m_Msg);
                while (msg.bgworker.IsBusy)
                    System.Windows.Forms.Application.DoEvents();

                return GetMd5Hash(GetStringFromBytes(msg.sm.efusebindata));
            }
            else
                return string.Empty;        //对于Register Config来说，不产生hex文件，也没有BIN_MD5
        }

        private XmlElement XmlAddOneNode(XmlDocument doc, XmlElement entry, string nodeName, string nodeInnerText = "", Dictionary<string, string> attributes = null)
        {
            XmlElement xe = doc.CreateElement(nodeName);

            if (attributes != null)
            {
                foreach (var attr in attributes)
                {
                    XmlAttribute xa = doc.CreateAttribute(attr.Key);
                    XmlText value = doc.CreateTextNode(attr.Value);
                    xa.AppendChild(value);
                    xe.SetAttributeNode(xa);
                }
            }
            if (nodeInnerText != string.Empty)
            {
                XmlText content = doc.CreateTextNode(nodeInnerText);
                xe.AppendChild(content);
            }

            entry.AppendChild(xe);
            return xe;
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
            string fullpath = "";
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            //if (sflname == CobraGlobal.Constant.OldBoardConfigName || sflname == CobraGlobal.Constant.NewBoardConfigName)//support them both in COBRA2.00.15, so all old and new OCEs will work fine.//Issue 1426 Leon
            //{
            //    openFileDialog.Title = "Load Board Config file";			//Support Production SFL, Leon
            //    openFileDialog.Filter = "Board Config file (*.board)|*.board|Excel file (*.xlsx)|*.xlsx||";
            //    openFileDialog.DefaultExt = "board";
            //}
            //else
            //{
            openFileDialog.Title = "Load Configuration File";
            openFileDialog.Filter = "Device Configuration file (*.cfg)|*.cfg||";
            openFileDialog.DefaultExt = "cfg";
            //}
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
                    int index = fullpath.LastIndexOf('.');
                    string suffix = fullpath.Substring(index + 1).ToLower();
                    if (suffix == "board" || suffix == "cfg")
                        ret = LoadFile(fullpath);
                    else if (suffix == "xlsx")
                    {
                        ret &= LoadBoardConfigFromExcel(fullpath);
                    }
                    if (ret == true /*&& (sflname == CobraGlobal.Constant.OldBoardConfigName || sflname == CobraGlobal.Constant.NewBoardConfigName)*/)
                    {
                        StatusLabel.Content = fullpath;
                        //gm.message = "Board Setting will only take effect after Apply button is clicked!";
                        //CallWarningControl(gm);
                    }

                }
            }
            else
                return;

            if (sflname == CobraGlobal.Constant.OldBoardConfigName || sflname == CobraGlobal.Constant.NewBoardConfigName)//support them both in COBRA2.00.15, so all old and new OCEs will work fine.    //Issue1373//Issue 1426 Leon
            {
                SaveBoardConfigFilePath(fullpath);//Issue1378 Leon
            }
        }
        internal bool LoadFile(string fullpath)
        {
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

            #region CFG Version Check
            string CFGVersionInXML = GetValueFromXML(root, ConstantSettings.CFG_VERSION_NODE);
            if (string.IsNullOrEmpty(CFGVersionInXML))
            {
                gm.message = "No CFG Version in XML!";
                gm.level = 2;
                CallWarningControl(gm);
                return false;
            }
            if (CFGVersionInXML != ConstantSettings.CFG_VERSION_INT.ToString())
            {
                string warning = "Cobra Version in file: " + CFGVersionInXML;
                warning += "\nCobra you are using: " + ConstantSettings.CFG_VERSION_INT.ToString();
                warning += "\nCobra Version Mismatch! Load failed!";
                gm.message = warning;
                gm.level = 2;
                CallWarningControl(gm);
                return false;
            }

            #endregion
            #region OCEToken Check
            string OCETokenMD5FromRuntime = string.Empty;
            OCETokenMD5FromRuntime = CobraGlobal.CurrentOCETokenMD5;
            if (OCETokenMD5FromRuntime == string.Empty)
            {
                gm.message = "Cannot get OCE Token MD5! Load failed!";
                gm.level = 2;
                CallWarningControl(gm);
                return false;
            }
            string OCETokenInXML = GetValueFromXML(root, ConstantSettings.OCE_TOKEN_NODE);
            if (string.IsNullOrEmpty(OCETokenInXML))
            {
                gm.message = "No OCE Token in XML!";
                gm.level = 2;
                CallWarningControl(gm);
                return false;
            }
            if (OCETokenInXML != OCETokenMD5FromRuntime)
            {
                string warning = "OCE Token in file: " + OCETokenInXML;
                warning += "\nOCE Token in OCE: " + OCETokenMD5FromRuntime;
                warning += "\nOCE Mismatch!";
                gm.message = warning;
                gm.level = 2;
                CallWarningControl(gm);
                return false;
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
            if (ProductFamilyInXML != ProductFamilyRuntime)
            {
                string warning = "Product Family in file: " + ProductFamilyInXML;
                warning += "\nProduct Family you are using: " + ProductFamilyRuntime;
                warning += "\nProduct Family Mismatch! Load failed!";
                gm.message = warning;
                gm.level = 2;
                CallWarningControl(gm);
                return false;
            }

            #endregion
            //No need to check BIN_MD5 here
            #region MD5 Check
            string hashofxml = GetFileMD5Code(root);
            if (string.IsNullOrEmpty(hashofxml))
            {
                gm.message = "Cannot get MD5! Load failed!";
                gm.level = 2;
                CallWarningControl(gm);
                return false;
            }
            string hashinxml = GetValueFromXML(root, ConstantSettings.MD5_NODE);
            if (string.IsNullOrEmpty(hashinxml))
            {
                gm.message = "Cannot get MD5 in file! Load failed!";
                gm.level = 2;
                CallWarningControl(gm);
                return false;
            }
            if (hashofxml != hashinxml)
            {
                string warning = "MD5 in file: " + hashinxml;
                warning += "\nMD5 of file: " + hashofxml;
                warning += "\nMD5 Mismatch! Load failed!";
                gm.message = warning;
                gm.level = 2;
                CallWarningControl(gm);
                return false;
            }
            #endregion

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
            double dval = 0.0;
            string tmp;
            SFLModel model;
            foreach (XmlElement xe in xn.ChildNodes)
            {
                string name = xe.GetAttribute("Name");
                model = cfgViewModel.GetParameterByName(name);
                model = list.SingleOrDefault(o => o.nickname == name);
                if (model == null) continue;

                model.berror = false;
                model.errorcode = LibErrorCode.IDS_ERR_SUCCESSFUL;

                tmp = xe.InnerText;

                if (model.brange)//为正常录入浮点数
                {
                    if (model.editortype == 1)//combobox
                    {
                        dval = model.itemlist.IndexOf(tmp);     //tmp本身不是index，而是index对应的item。而model的值是index
                    }
                    else//editbox
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
                    }
                    model.data = dval;
                }
                else
                    model.sphydata = tmp;
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

        private string GetUIMD5Code(List<SFLModel> models, string BIN_MD5_STR)
        {

            StringBuilder sb = new StringBuilder();


            string SOCETokenMD5 = CobraGlobal.CurrentOCETokenMD5;
            string SCFGVersion = ConstantSettings.CFG_VERSION_INT.ToString();
            sb.Append(ConstantSettings.CFG_VERSION_NODE);
            sb.Append(SCFGVersion);
            sb.Append(ConstantSettings.OCE_TOKEN_NODE);
            sb.Append(SOCETokenMD5);
            sb.Append(ConstantSettings.PRODUCT_FAMILY_NODE);
            sb.Append(ProductFamily);
            sb.Append(ConstantSettings.BIN_MD5_NODE);
            sb.Append(BIN_MD5_STR);

            foreach (SFLModel model in models)
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
                        {
                            strval = model.itemlist[model.listindex];
                            break;
                        }
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
            string hash = GetMd5Hash(sb.ToString());
            return hash;
        }

        private string GetFileMD5Code(XmlElement root)
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

        private string GetShortCode(string fullcode)
        {
            return fullcode.Substring(fullcode.Length - 5);
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

        private string GetStringFromBytes(List<byte> data)
        {
            // Create a new Stringbuilder to collect the bytes
            // and create a string.
            StringBuilder sBuilder = new StringBuilder();

            // Loop through each byte of the hashed data 
            // and format each one as a hexadecimal string.
            foreach(var d in data)
            {
                sBuilder.Append(d.ToString("x2"));
            }

            // Return the hexadecimal string.
            return sBuilder.ToString();
        }
        #endregion

        #region Borad Config Related
        public void SaveBoardConfigFilePath(string fullpath)
        {
            string settingfilepath = System.IO.Path.Combine(FolderMap.m_currentproj_folder, "settings.xml");
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

        private void SaveBoardConfigToInternalMemory()//Issue1378 Leon
        {
            foreach (SFLModel model in cfgViewModel.sfl_parameterlist)
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

        public void LoadBoardConfigFromInternalMemory()//Issue1378 Leon
        {
            double dval = 0.0;
            string tmp;
            SFLModel model;
            foreach (var item in BCImg)
            {
                string name = item.Key;
                model = cfgViewModel.GetParameterByName(name);
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
            Apply();
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
            string hashinXML = "";
            for (XmlNode xn = root.FirstChild; xn is XmlNode; xn = xn.NextSibling)
            {
                string name = xn.Attributes[0].Value;
                if (name == "MD5")
                {
                    hashinXML = xn.InnerText;
                    continue;
                }
                sb.Append(name);
                tmp = xn.InnerText;
                sb.Append(tmp);
            }
            if (hashinXML == "")         //没有MD5
            {
                gm.message = "Warning, this configuration file dosen't have MD5 verification code. You can still use it but we suggest you upgrade it by save to another file.";
                CallWarningControl(gm);
            }
            else
            {
                string hashOfXML = GetMd5Hash(sb.ToString());
                if (hashOfXML == hashinXML)
                {
                    ;
                }
                else
                {
                    throw new NotImplementedException("File illegal, MD5 check failed. ");
                }
            }

            for (XmlNode xn = root.FirstChild; xn is XmlNode; xn = xn.NextSibling)
            {
                //tmp = xn.Name.Replace("H","0x");
                //selfid = Convert.ToUInt32(tmp, 16);
                string name = xn.Attributes[0].Value;
                if (name == "MD5")
                {
                    hashinXML = xn.InnerText;
                    continue;
                }
                model = cfgViewModel.GetParameterByName(name);
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

        // Verify a hash against a string.
        private bool VerifyMd5Hash(string input, string hash)
        {
            // Hash the input.
            string hashOfInput = GetMd5Hash(input);

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

        private bool LoadBoardConfigFromExcel(string fullpath)
        {
            bool ret = true;
            var excelApp = new Excel.Application();
            try
            {
                Excel.Workbook excelWKB = null;
                Excel._Worksheet excelSHEET = null;
                excelWKB = excelApp.Workbooks.Open(fullpath);
                excelSHEET = excelWKB.Sheets[1];

                for (int row = 4; row <= 200; row++)
                {
                    string tmp = ((Excel.Range)excelSHEET.Cells[row, 2]).Text.ToString();
                    var model = cfgViewModel.GetParameterByName(tmp);
                    if (model == null)
                        break;
                    else
                    {
                        model.berror = false;

                        tmp = ((Excel.Range)excelSHEET.Cells[row, 3]).Text.ToString();

                        double dval = 0.0;
                        if (!Double.TryParse(tmp, out dval))
                            dval = 0.0;
                        model.data = dval * 1000;
                    }
                }
            }
            catch (Exception c)
            {
                System.Windows.MessageBox.Show(c.ToString());
                ret = false;
            }
            finally
            {
                StatusLabel.Content = fullpath;
                excelApp.Workbooks.Close();
                excelApp.Quit();
                System.Runtime.InteropServices.Marshal.ReleaseComObject(excelApp);
                excelApp = null;
            }
            return ret;
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
            if (sflname == CobraGlobal.Constant.OldBoardConfigName || sflname == CobraGlobal.Constant.NewBoardConfigName)//support them both in COBRA2.00.15, so all old and new OCEs will work fine.//Issue 1426 Leon
                Reset();//Issue1381 Leon
            else if (ReadSubTask != 0)     //当前oce支持subtask特性 Issue1363 Leon
                ReadCommand(ReadSubTask);
            else
                Read();
        }
        private void Reset()//Issue1373 Leon
        {
            LoadBoardConfigFromInternalMemory();
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
        #endregion
        #region Write
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
                    cfgViewModel.BuildPartParameterList(cm.PlacementTarget.Uid);
                    msg.gm.controls = "Write One parameter";
                    msg.task_parameterlist = cfgViewModel.dm_part_parameterlist;
                    break;
                case "Button":
                    if (sflname == CobraGlobal.Constant.OldBoardConfigName || sflname == CobraGlobal.Constant.NewBoardConfigName)//support them both in COBRA2.00.15, so all old and new OCEs will work fine.//Issue 1426 Leon
                    {//Issue1381 Leon
                    }
                    else
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
            if (sflname == CobraGlobal.Constant.OldBoardConfigName || sflname == CobraGlobal.Constant.NewBoardConfigName)//support them both in COBRA2.00.15, so all old and new OCEs will work fine.//Issue 1426 Leon
                Apply();//Issue1381 Leon
            else if (WriteSubTask != 0)		//Issue1363 Leon
                WriteCommand(WriteSubTask);
            else
                write();
        }
        private void Apply()//Issue1381 Leon
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

            if (msg.errorcode == LibErrorCode.IDS_ERR_SUCCESSFUL)   //Issue1826 Leon
            {
                gm.level = 0;
                gm.message = "Board Parameters Saved!";
                CallWarningControl(gm);
                parent.bBusy = false;
                return;
            }

            OnRasieBoardConfigChangedEvent();//Issue1593 Leon
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