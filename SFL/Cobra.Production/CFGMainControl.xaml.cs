using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using Cobra.EM;
using Cobra.Common;
using System.Xml;
using System.Security.Cryptography;
using System.IO;
using System.Collections;

namespace Cobra.ProductionPanel
{
    /// <summary>
    /// Interaction logic for MainControl.xaml
    /// </summary>
    public partial class CFGMainControl
    {
        #region variable defination
        //父对象保存
        private MainControl m_parent;
        public MainControl parent
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

        private TASKMessage m_Msg = new TASKMessage();
        public TASKMessage msg
        {
            get { return m_Msg; }
            set { m_Msg = value; }
        }

        private CFGViewModel m_Viewmodel = new CFGViewModel();
        public CFGViewModel Viewmodel
        {
            get { return m_Viewmodel; }
            set { m_Viewmodel = value; }
        }

        public List<CFGProcessItem> ProcessItems = new List<CFGProcessItem>();

        private GeneralMessage gm = new GeneralMessage("TestItems SFL", "", 0);
        AsyncObservableCollection<Parameter> scanlist = new AsyncObservableCollection<Parameter>();

        private string BinFileName = "";
        private string MPTFileName = "";

        private UInt16 CheckBinFileTaskID = 0;
        #endregion

        private string GetHashTableValueByKey(string str, Hashtable htable)
        {
            if (htable.ContainsKey(str))
                return htable[str].ToString();
            else
                return "NoSuchKey";
        }

        public CFGMainControl()
        {
            InitializeComponent();
        }

        public void Init(object pParent, string name)
        {
            #region 相关初始化
            parent = (MainControl)pParent;
            if (parent == null) return;

            sflname = name;
            if (String.IsNullOrEmpty(sflname)) return;
            msg = parent.msg;
            #endregion
            #region 解析XML获取Items
            #region 解析DeviceDescriptor
            scanlist = parent.parent.GetParamLists(sflname).parameterlist;
            string temp;
            try
            {
                foreach (Parameter param in scanlist)  //从XML对应的parameter中提取私有属性，给viewmodel赋初值，并将parameter归入读取容器，以便Run时使用
                {
                    temp = GetHashTableValueByKey("Name", param.sfllist[sflname].nodetable);
                    if (temp == "NoSuchKey")
                        continue;
                    CFGTestItem ti = new CFGTestItem();
                    ti.Name = temp;


                    temp = GetHashTableValueByKey("Unit", param.sfllist[sflname].nodetable);
                    if (temp != "NoSuchKey")
                        ti.Unit = temp;

                    temp = GetHashTableValueByKey("MinValue", param.sfllist[sflname].nodetable);
                    if (temp != "NoSuchKey")
                        ti.MinValue = Convert.ToDouble(temp);

                    temp = GetHashTableValueByKey("MaxValue", param.sfllist[sflname].nodetable);
                    if (temp != "NoSuchKey")
                        ti.MaxValue = Convert.ToDouble(temp);

                    temp = GetHashTableValueByKey("EnableTolerance", param.sfllist[sflname].nodetable);
                    if (temp != "NoSuchKey")
                        ti.EnableTolerance = Convert.ToBoolean(temp);

                    ti.GUID = "0x" + param.guid.ToString("X8");

                    Viewmodel.TestItems.Add(ti);
                }
            }
            catch (System.Exception e)
            {
                MessageBox.Show(e.Message);
            }

            #endregion
            #region 初始化 BinFileCheck SubTaskID
            string str_option = String.Empty;
            XmlNodeList nodelist = parent.parent.GetUINodeList(parent.ProductionSFLDBName);
            foreach (XmlNode node in nodelist)
            {
                str_option = node.Name;
                switch (str_option)
                {
                    case "BinFileCheck":
                        {
                            XmlElement xe1 = (XmlElement)(node);
                            this.CheckBinFileTaskID = Convert.ToUInt16(xe1.GetAttribute("SubTaskID"));
                            break;
                        }
                }
            }
            #endregion
            #endregion

            TestItemsUI.DataContext = Viewmodel.TestItems;
        }


        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            ;
        }

        private string GetChipName()
        {
            XmlElement root = EMExtensionManage.m_extDescrip_xmlDoc.DocumentElement;
            return root.GetAttribute("chip");
        }

        private void SavePackButton_Click(object sender, RoutedEventArgs e)
        {
            string fullpath = "";
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            string chip = GetChipName();
            string filename = chip + "-" + DateTime.Now.ToString("yyyyMMddHHmmss");
            saveFileDialog.FileName = filename;
            saveFileDialog.FilterIndex = 1;
            saveFileDialog.RestoreDirectory = true;
            saveFileDialog.Title = "Save pack File";
            saveFileDialog.Filter = "pack file (*.pack)|*.pack||";
            saveFileDialog.DefaultExt = "pack";
            saveFileDialog.InitialDirectory = FolderMap.m_currentproj_folder;
            if (saveFileDialog.ShowDialog() == true)
            {
                if (parent == null) return;
                else
                {
                    fullpath = saveFileDialog.FileName;
                    SavePackFile(fullpath);
                }
            }
        }

        internal void SavePackFile(string fullpath)
        {
            string MPTFilePath;
            #region Save *.mpt
            try
            {
                MPTFilePath = System.IO.Path.ChangeExtension(fullpath, "mpt");
                FileStream file = new FileStream(MPTFilePath, FileMode.Create);
                StreamWriter sw = new StreamWriter(file);
                sw.WriteLine("<?xml version=\"1.0\"?>");
                sw.WriteLine("<root>");
                sw.WriteLine("</root>");
                sw.Close();
                file.Close();
                XmlDocument doc = new XmlDocument();
                doc.Load(MPTFilePath);
                XmlElement root = doc.DocumentElement;
                SharedAPI.XmlAddOneNode(doc, doc.DocumentElement, COBRA_GLOBAL.Constant.CHIP_NAME_NODE, SharedAPI.GetChipNameFromExtension());//Issue1906
                #region Download Part
                if ((bool)DownloadOnly.IsChecked || (bool)DownloadAndTest.IsChecked)
                {
                    byte RadioOption = 0;
                    if ((bool)WithPowerControl.IsChecked)
                    {
                        RadioOption = 0;
                    }
                    else if ((bool)WithoutPowerControl.IsChecked)
                    {
                        RadioOption = 1;
                    }
                    else
                    {
                        MessageBox.Show("Choose Power Control!");
                        return;
                    }

                    #region 解析ExtensionDescriptor
                    ProcessItems.Clear();
                    string str_option = String.Empty;
                    XmlNodeList nodelist = parent.parent.GetUINodeList(sflname);
                    foreach (XmlNode node in nodelist)
                    {
                        if (node.Name != "Process")
                            continue;
                        foreach (XmlElement sub in node)
                        {
                            if (sub.Name != "Item")
                                continue;

                            string temp = sub.GetAttribute("RadioOption");
                            if (temp != "")
                            {
                                if (temp != RadioOption.ToString())
                                    continue;
                            }

                            CFGProcessItem pi = new CFGProcessItem();
                            temp = sub.GetAttribute("SubTaskID");
                            if (temp != "")
                                pi.SubTaskID = temp;
                            temp = sub.InnerText;
                            if (temp != "")
                                pi.Name = temp;
                            ProcessItems.Add(pi);
                        }
                    }
                    #endregion

                    XmlElement processnode = doc.CreateElement("Process");
                    root.AppendChild(processnode);
                    foreach (CFGProcessItem pi in ProcessItems)
                    {
                        if (pi == null) continue;
                        XmlElement newitem = doc.CreateElement("Item");
                        processnode.AppendChild(newitem);

                        string temp = pi.SubTaskID;
                        XmlAttribute AID = doc.CreateAttribute("SubTaskID");
                        XmlText TID = doc.CreateTextNode(temp);
                        newitem.SetAttributeNode(AID);
                        AID.AppendChild(TID);

                        temp = pi.Name;
                        XmlText TName = doc.CreateTextNode(temp);
                        newitem.AppendChild(TName);
                    }
                }
                #endregion

                #region Test Part
                if ((bool)DownloadAndTest.IsChecked || (bool)TestOnly.IsChecked)
                {
                    XmlElement testnode = doc.CreateElement("Test");
                    root.AppendChild(testnode);
                    foreach (CFGTestItem ti in Viewmodel.TestItems)
                    {
                        if (ti == null) continue;
                        if (!ti.IsEnable) continue;
                        XmlElement newitem = doc.CreateElement("Item");
                        testnode.AppendChild(newitem);

                        string temp = ti.Name;
                        XmlAttribute AName = doc.CreateAttribute("Name");
                        XmlText TName = doc.CreateTextNode(temp);
                        newitem.SetAttributeNode(AName);
                        AName.AppendChild(TName);

                        temp = ti.StandardValue.ToString();
                        XmlAttribute AData = doc.CreateAttribute("StandardValue");
                        XmlText TData = doc.CreateTextNode(temp);
                        newitem.SetAttributeNode(AData);
                        AData.AppendChild(TData);

                        temp = ti.Tolerance.ToString();
                        if (temp != "0")
                        {
                            XmlAttribute ATolerance = doc.CreateAttribute("Tolerance");
                            XmlText TTolerance = doc.CreateTextNode(temp);
                            newitem.SetAttributeNode(ATolerance);
                            ATolerance.AppendChild(TTolerance);
                        }

                        temp = ti.GUID;
                        if (temp != "0")
                        {
                            XmlAttribute AGUID = doc.CreateAttribute("GUID");
                            XmlText TGUID = doc.CreateTextNode(temp);
                            newitem.SetAttributeNode(AGUID);
                            AGUID.AppendChild(TGUID);
                        }

                        temp = ti.Group.ToString();
                        XmlAttribute AGroup = doc.CreateAttribute("Group");
                        XmlText TGroup = doc.CreateTextNode(temp);
                        newitem.SetAttributeNode(AGroup);
                        AGroup.AppendChild(TGroup);
                    }
                }
                #endregion

                doc.Save(MPTFilePath);
                MPTFileName = System.IO.Path.GetFileName(MPTFilePath);
            }
            catch
            {
                MessageBox.Show("Save MPT file failed!");
                return;
            }
            #endregion

            #region Pack files into *.pack
            try
            {
                string m_temp_folder = System.IO.Path.Combine(FolderMap.m_currentproj_folder, "TempFolder\\");
                if (Directory.Exists(m_temp_folder))
                    Directory.Delete(m_temp_folder, true);
                if (!Directory.Exists(m_temp_folder))
                    Directory.CreateDirectory(m_temp_folder);

                if ((bool)DownloadOnly.IsChecked || (bool)DownloadAndTest.IsChecked)
                {
                    if (BinFilePath.Text != String.Empty)
                        File.Copy(BinFilePath.Text, m_temp_folder + BinFileName);
                }
                if (MPTFilePath != String.Empty)
                    File.Copy(MPTFilePath, m_temp_folder + MPTFileName);

                string filename = System.IO.Path.GetFileName(fullpath);
                string path = System.IO.Path.GetDirectoryName(fullpath);
                GZip.Compress(m_temp_folder, "*.*", SearchOption.TopDirectoryOnly, path, filename, true);

                Directory.Delete(m_temp_folder, true);
            }
            catch
            {
                MessageBox.Show("Save pack file failed!");
                return;
            }
            #endregion

        }

        #region Pack
        // Verify a hash against a string.
        private void ChooseBinFileButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Title = "Choose Bin File";
            openFileDialog.Filter = "Bin file (*.bin)|*.bin||";
            openFileDialog.DefaultExt = "bin";
            //openFileDialog.FileName = "default";
            openFileDialog.FilterIndex = 1;
            openFileDialog.RestoreDirectory = true;
            openFileDialog.InitialDirectory = FolderMap.m_currentproj_folder;
            if (openFileDialog.ShowDialog() == true)
            {
                var ret = CheckBinFile(CheckBinFileTaskID, openFileDialog.FileName);
                if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                {
                    string errstring = LibErrorCode.GetErrorDescription(ret);
                    parent.UnMaskWarning(errstring);
                }
                else
                {
                    BinFilePath.Text = openFileDialog.FileName;
                    BinFilePath.IsEnabled = true;
                    BinFileName = openFileDialog.SafeFileName;
                }
            }
        }

        private UInt32 CheckBinFile(ushort checkBinFileTaskID, string fileName)
        {
            msg.task = TM.TM_COMMAND;
            msg.sub_task = checkBinFileTaskID;
            msg.sub_task_json = fileName;
            parent.parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                return msg.errorcode;
            return LibErrorCode.IDS_ERR_SUCCESSFUL;
        }
        #endregion

        private void TestOnly_Checked(object sender, RoutedEventArgs e)
        {
            BinFilePath.Text = "";
            BinFilePath.IsEnabled = false;
        }
    }
}
