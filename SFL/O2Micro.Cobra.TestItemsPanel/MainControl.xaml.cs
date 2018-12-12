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
using O2Micro.Cobra.EM;
using O2Micro.Cobra.Common;
using O2Micro.Cobra.ControlLibrary;
using System.Xml;
using System.Security.Cryptography;
using System.IO;
using System.Collections;

namespace O2Micro.Cobra.TestItemsPanel
{
    /// <summary>
    /// Interaction logic for MainControl.xaml
    /// </summary>
    public partial class MainControl
    {
        #region variable defination
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

        private TASKMessage m_Msg = new TASKMessage();
        public TASKMessage msg
        {
            get { return m_Msg; }
            set { m_Msg = value; }
        }

        private ViewModel m_Viewmodel = new ViewModel();
        public ViewModel Viewmodel
        {
            get { return m_Viewmodel; }
            set { m_Viewmodel = value; }
        }

        public List<ProcessItem> ProcessItems = new List<ProcessItem>();

        private GeneralMessage gm = new GeneralMessage("TestItems SFL", "", 0);
        AsyncObservableCollection<Parameter> scanlist = new AsyncObservableCollection<Parameter>();

        private string ParametersFileName = "";
        private string BoardFileName = "";
        private string MPTFileName = "";
        #endregion

        private string GetHashTableValueByKey(string str, Hashtable htable)
        {
            /*foreach (DictionaryEntry de in htable)
            {
                if (de.Key.ToString().Equals(str))
                    return de.Value.ToString();
            }
            return "NoSuchKey";*/
            /*if (htable.ContainsKey(str))  //之所以不能这样用，是因为这个htable在创建的时候，Key的类型为XName而非string
                return htable[str].ToString();
            else
                return "NoSuchKey";*/
            if (htable.ContainsKey(str))
                return htable[str].ToString();
            else
                return "NoSuchKey";
        }
        private void ShowBoardConfigInput(bool b)
        {
            if (b)
                BoardLabel.Visibility = System.Windows.Visibility.Visible;
            else
                BoardLabel.Visibility = System.Windows.Visibility.Collapsed;
        }

        public MainControl(object pParent, string name)
        {
            InitializeComponent();

            #region 相关初始化
            parent = (Device)pParent;
            if (parent == null) return;

            sflname = name;
            if (String.IsNullOrEmpty(sflname)) return;
            #endregion

            #region 解析XML获取Items
            #region 解析ExtensionDescriptor
            /*
            string str_option = String.Empty;
            XmlNodeList nodelist = parent.GetUINodeList(sflname);
            foreach (XmlNode node in nodelist)
            {
                str_option = node.Name;
                switch (str_option)
                {
                    case "Items":
                        {
                            foreach (XmlElement sub in node)
                            {
                                if (sub.Name != "Item")
                                    continue;
                                TestItem ti = new TestItem();
                                string temp = sub.GetAttribute("Name");
                                if (temp != "")
                                    ti.Name = temp;
                                temp = sub.GetAttribute("Unit");
                                if (temp != "")
                                    ti.Unit = temp;
                                temp = sub.GetAttribute("MinValue");
                                if (temp != "")
                                    ti.MinValue = Convert.ToDouble(temp);
                                temp = sub.GetAttribute("MaxValue");
                                if (temp != "")
                                    ti.MaxValue = Convert.ToDouble(temp);
                                temp = sub.GetAttribute("EnableTolerance");
                                if (temp != "")
                                    ti.EnableTolerance = Convert.ToBoolean(temp);

                                foreach (XmlNode guidnode in sub)
                                {
                                    temp = guidnode.InnerText;
                                    if (temp != "")
                                        ti.GUIDs.Add(temp);
                                }
                                Viewmodel.TestItems.Add(ti);
                            }
                            break;
                        }
                }
            }
            */
            #endregion
            #region 解析DeviceDescriptor
            scanlist = parent.GetParamLists(sflname).parameterlist;
            string temp;
            try
            {
                foreach (Parameter param in scanlist)  //从XML对应的parameter中提取私有属性，给viewmodel赋初值，并将parameter归入读取容器，以便Run时使用
                {
                    temp = GetHashTableValueByKey("Name", param.sfllist[sflname].nodetable);
                    if (temp == "NoSuchKey")
                        continue;
                    TestItem ti = new TestItem();
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

                    ti.GUID="0x" + param.guid.ToString("X8");
                    
                    Viewmodel.TestItems.Add(ti);
                }
            }
            catch (System.Exception e)
            {
                MessageBox.Show(e.Message);
            }

            #endregion
            #endregion

            foreach (var btn in EMExtensionManage.m_EM_DevicesManage.btnPanelList)  //根据ExtensionDescriptor中是否包含BoardConfig SFL来决定是否显示输入项
            {
                if (btn.btnlabel == "BoardConfig")
                    ShowBoardConfigInput(true);
            }

            TestItemsUI.DataContext = Viewmodel.TestItems;
        }


        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            ;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            /*if (!(bool)DownloadEnable.IsChecked && !(bool)TestEnable.IsChecked)   //没有选择
            {
                MessageBox.Show("Please enable Test/Download process first!");
                return;
            }
            if ((bool)DownloadEnable.IsChecked)
            {
                if (!(bool)WithoutPowerControl.IsChecked && !(bool)WithPowerControl.IsChecked)   //没有选择
                {
                    MessageBox.Show("Please select with/without power control first!");
                    return;
                }
            }*/

            string fullpath = "";
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            saveFileDialog.FileName = "default";
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
                    SaveFile(fullpath);
                }
            }
        }

        internal void SaveFile(string fullpath)
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
                    XmlNodeList nodelist = parent.GetUINodeList(sflname);
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

                            ProcessItem pi = new ProcessItem();
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
                    foreach (ProcessItem pi in ProcessItems)
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
                    foreach (TestItem ti in Viewmodel.TestItems)
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
                /*if (BoardFilePath.Text == String.Empty)   //We don't check board file because some projects do not have a *.board file
                {
                    MessageBox.Show("Please choose board config file!");
                    return;
                }*/
                string m_temp_folder = System.IO.Path.Combine(FolderMap.m_currentproj_folder, "TempFolder\\");
                if (Directory.Exists(m_temp_folder))
                    Directory.Delete(m_temp_folder, true);
                if (!Directory.Exists(m_temp_folder))
                    Directory.CreateDirectory(m_temp_folder);

                if (BoardFilePath.Text != String.Empty)
                    File.Copy(BoardFilePath.Text, m_temp_folder + BoardFileName);

                if ((bool)DownloadOnly.IsChecked || (bool)DownloadAndTest.IsChecked)
                {
                    if (ParametersFilePath.Text != String.Empty)
                        File.Copy(ParametersFilePath.Text, m_temp_folder + ParametersFileName);
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
        private bool CheckFile(string fullpath)
        {
            string tmp;

            XmlDocument doc = new XmlDocument();
            try
            {
                doc.Load(fullpath);
            }
            catch
            {
                return false;
            }
            XmlElement root = doc.DocumentElement;
            StringBuilder sb = new StringBuilder();
            string hash = "";
            try
            {
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
            }
            catch
            {
                return false;
            }
            using (MD5 md5Hash = MD5.Create())
            {
                if (VerifyMd5Hash(md5Hash, sb.ToString(), hash))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
        private void LoadParametersFileButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Title = "Load Parameters Config File";
            openFileDialog.Filter = "Parameters Config file (*.cfg)|*.cfg||";
            openFileDialog.DefaultExt = "cfg";
            openFileDialog.FileName = "default";
            openFileDialog.FilterIndex = 1;
            openFileDialog.RestoreDirectory = true;
            openFileDialog.InitialDirectory = FolderMap.m_currentproj_folder;
            if (openFileDialog.ShowDialog() == true)
            {
                if (CheckFile(openFileDialog.FileName))
                {
                    ParametersFilePath.Text = openFileDialog.FileName;
                    ParametersFilePath.IsEnabled = true;
                    ParametersFileName = openFileDialog.SafeFileName;
                }
                else
                {
                    MessageBox.Show("File illegal.");
                }
            }
        }

        private void LoadBoardFileButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Title = "Load Board Config File";
            openFileDialog.Filter = "Board Config file (*.board)|*.board||";
            openFileDialog.DefaultExt = "board";
            openFileDialog.FileName = "default";
            openFileDialog.FilterIndex = 1;
            openFileDialog.RestoreDirectory = true;
            openFileDialog.InitialDirectory = FolderMap.m_currentproj_folder;
            if (openFileDialog.ShowDialog() == true)
            {
                if (CheckFile(openFileDialog.FileName))
                {
                    BoardFilePath.Text = openFileDialog.FileName;
                    BoardFilePath.IsEnabled = true;
                    BoardFileName = openFileDialog.SafeFileName;
                }
                else
                {
                    MessageBox.Show("File illegal.");
                }
            }
        }

        private void PackButton_Click(object sender, RoutedEventArgs e)
        {
            string fullpath = "";
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            saveFileDialog.FileName = "default";
            saveFileDialog.FilterIndex = 1;
            saveFileDialog.RestoreDirectory = true;
            saveFileDialog.Title = "Make package File";
            saveFileDialog.Filter = "Package file (*.pack)|*.pack||";
            saveFileDialog.DefaultExt = "pack";
            saveFileDialog.InitialDirectory = FolderMap.m_currentproj_folder;
            if (saveFileDialog.ShowDialog() == true)
            {
                fullpath = saveFileDialog.FileName;
                PackFile(fullpath);
                //PackedFilePath.Text = fullpath;
            }
        }

        private void PackFile(string fullpath)
        {
            if (BoardFilePath.Text == String.Empty)
            {
                MessageBox.Show("Please choose board config file!");
                return;
            }
            string m_temp_folder = System.IO.Path.Combine(FolderMap.m_currentproj_folder, "TempFolder\\");
            if (Directory.Exists(m_temp_folder))
                Directory.Delete(m_temp_folder, true);
            if (!Directory.Exists(m_temp_folder))
                Directory.CreateDirectory(m_temp_folder);

            if (ParametersFilePath.Text != String.Empty)
                File.Copy(ParametersFilePath.Text, m_temp_folder + ParametersFileName);
            if (BoardFilePath.Text != String.Empty)
                File.Copy(BoardFilePath.Text, m_temp_folder + BoardFileName);
            //if (MPTFilePath.Text != String.Empty)
                //File.Copy(MPTFilePath.Text, m_temp_folder + MPTFileName);
            string filename = System.IO.Path.GetFileName(fullpath);
            string path = System.IO.Path.GetDirectoryName(fullpath);
            GZip.Compress(m_temp_folder, "*.*", SearchOption.TopDirectoryOnly, path, filename, true);

            Directory.Delete(m_temp_folder, true);
        }
        #endregion

        private void TestOnly_Checked(object sender, RoutedEventArgs e)
        {
            ParametersFilePath.Text = "";
            ParametersFilePath.IsEnabled = false;
        }
    }
}
