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
using System.Collections;
using System.ComponentModel;
using System.Windows.Controls.Primitives;
using System.Threading;
using System.IO;
using System.Text.RegularExpressions;
using System.Reflection;

namespace O2Micro.Cobra.TableMaker
{
    /// <summary>
    /// Interaction logic for MainControl.xaml
    /// </summary>
    public partial class MainControl : UserControl
    {
        #region variable defination

        private Device m_parent;
        public Device parent
        {
            get { return m_parent; }
            set { m_parent = value; }
        }
        private string m_sflname;
        public string sflname
        {
            get { return m_sflname; }
            set { m_sflname = value; }
        }

        private string m_loadrawpath;
        public string loadrawpath
        {
            get { return m_loadrawpath; }
            set { m_loadrawpath = value; }
        }
        private string m_makesrcpath;
        public string makesrcpath
        {
            get { return m_makesrcpath; }
            set { m_makesrcpath = value; }
        }
        private TASKMessage m_Msg = new TASKMessage();
        public TASKMessage msg
        {
            get { return m_Msg; }
            set { m_Msg = value; }
        }

        private ViewModel mVM;
        public ViewModel pVM
        {
            get { return mVM; }
            set { mVM = value; }
        }

        private GeneralMessage gm = new GeneralMessage("TableMaker SFL", "", 0);
        AsyncObservableCollection<Parameter> scanlist = new AsyncObservableCollection<Parameter>();

        private TableSample myTable = new TableSample();

        private OldTable m_oldTableObj;
        public OldTable oldTableObj
        {
            get { return m_oldTableObj; }
            set { m_oldTableObj = value; }
        }
        #endregion

        #region Function defination
        public string GetHashTableValueByKey(string str, Hashtable htable)
        {
            foreach (DictionaryEntry de in htable)
            {
                if (de.Key.ToString().Equals(str))
                    return de.Value.ToString();
            }
            return "NoSuchKey";
        }
        public MainControl(object pParent, string name)
        {
            InitializeComponent();

            #region folder初始化
            //Get folder name
            string[] folderstr = { "", "", "", "" };
            folderstr[0] = System.IO.Path.Combine(FolderMap.m_currentproj_folder, "Raw\\");
            if (!Directory.Exists(folderstr[0]))
                Directory.CreateDirectory(folderstr[0]);
            folderstr[1] = System.IO.Path.Combine(FolderMap.m_currentproj_folder, "Source\\");
            if (!Directory.Exists(folderstr[1]))
                Directory.CreateDirectory(folderstr[1]);
            folderstr[2] = System.IO.Path.Combine(FolderMap.m_currentproj_folder, "CFG\\");
            if (!Directory.Exists(folderstr[2]))
                Directory.CreateDirectory(folderstr[2]);
            folderstr[3] = System.IO.Path.Combine(FolderMap.m_currentproj_folder, "Output\\");
            if (!Directory.Exists(folderstr[3]))
                Directory.CreateDirectory(folderstr[3]);
            pVM = new ViewModel(folderstr[0], folderstr[1], folderstr[2], folderstr[3]);
            #endregion

            #region 相关初始化
            parent = (Device)pParent;
            if (parent == null) return;

            sflname = name;
            if (String.IsNullOrEmpty(sflname)) return;
            #endregion
            MainGrid.DataContext = pVM;
            SourceFileList.ItemsSource = pVM.sourcelist;
            VoltageList.ItemsSource = pVM.voltagelist;
            CurrentList.ItemsSource = pVM.currentlist;
            //(A170313)Francis, initial oldTable and UI
            oldTableObj = new OldTable(myTable, this);
            dgdCHFileList.ItemsSource = oldTableObj.OldTableFiles;
            txbBrowseFolderFly.Text = pVM.OutPutfolder;
            txbDateFalconLY.Text = DateTime.Now.Year.ToString() + "." + DateTime.Now.Month.ToString() + "." + DateTime.Now.Day.ToString();
            //(E170313)

            #region 加入一个空行
            VoltagePoint vp = new VoltagePoint();
            vp.Voltage = "";
            pVM.voltagelist.Add(vp);
            #endregion

            #region 加入一个空行
            CurrentPoint cp = new CurrentPoint();
            cp.Current = "";
            pVM.currentlist.Add(cp);
            #endregion

            strDate.Text = DateTime.Now.Year.ToString() + "." + DateTime.Now.Month.ToString() + "." + DateTime.Now.Day.ToString();
            Header.ItemsSource = pVM.header;

            scanlist = parent.GetParamLists(sflname).parameterlist;
            foreach (Parameter param in scanlist)
            {
                HeaderItem hi = new HeaderItem();
                hi.Item = GetHashTableValueByKey("Name", param.sfllist[sflname].nodetable);
                hi.Type = Convert.ToUInt16(GetHashTableValueByKey("Type", param.sfllist[sflname].nodetable));
                if (hi.Type == 1)
                {
                    hi.itemlist = param.itemlist;
                }
                pVM.header.Add(hi);
            }
            LibInfor.AssemblyRegister(Assembly.GetExecutingAssembly(), ASSEMBLY_TYPE.SFL);
        }

        #region 通用控件消息响应
        #endregion

        #region EventHandler

        private void ViewFolder_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("explorer.exe", FolderMap.m_currentproj_folder);
        }

        #region Raw2SourcePanel
        private void RawLoad_Click(object sender, RoutedEventArgs e)
        {
            string fullpath = "";
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Title = "Raw Data File";
            openFileDialog.Filter = "(*.csv)|*.csv||";
            openFileDialog.FileName = "default";
            openFileDialog.FilterIndex = 1;
            //openFileDialog.RestoreDirectory = true;
            openFileDialog.DefaultExt = "csv";
            if (loadrawpath == "" || loadrawpath == null)
                openFileDialog.InitialDirectory = pVM.Rawfolder;
            else
                openFileDialog.InitialDirectory = loadrawpath;
            if (openFileDialog.ShowDialog() == true)
            {
                //if (parent == null) return;
                {
                    fullpath = openFileDialog.FileName;
                    //RawFileName.Text =  System.IO.Path.GetFileName(fullpath);
                    //RawFileName.ToolTip = fullpath;
                    loadrawpath = System.IO.Path.GetDirectoryName(fullpath);
                    pVM.RawFileName = fullpath;
                    //LoadFile(fullpath);
                }
            }
        }
        private void SourceMake_Click(object sender, RoutedEventArgs e)
        {
            string fullpath = "";
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            saveFileDialog.Title = "Save Source Data File";
            saveFileDialog.Filter = "Source Data Files (*.csv)|*.csv||";
            saveFileDialog.FileName = "default";
            saveFileDialog.FilterIndex = 1;
            //saveFileDialog.RestoreDirectory = true;
            saveFileDialog.DefaultExt = "csv";
            if (makesrcpath == "" || makesrcpath == null)
                saveFileDialog.InitialDirectory = pVM.Sourcefolder;
            else
                saveFileDialog.InitialDirectory = makesrcpath;
            if (saveFileDialog.ShowDialog() == true)
            {
                //if (parent == null) return;
                //else
                {
                    UInt32 uErrorCode = 0;
                    fullpath = saveFileDialog.FileName;
                    makesrcpath = System.IO.Path.GetDirectoryName(fullpath);
                    SaveSourceFile(fullpath);
                    if (!myTable.CheckRawFile(fullpath, ref uErrorCode))
                    {
                        MessageBox.Show(LibErrorCode.GetErrorDescription(uErrorCode));
                        //File.Delete(fullpath);
                    }
                    pVM.RawFileName = "";
                    MakeSrcPanel.Visibility = Visibility.Hidden;
                    hdrCommitBtn.IsChecked = false;
                    //Header.IsEnabled = true;
                }
            }
        }
        private void SaveSourceFile(string fullpath)
        {
            FileStream file = new FileStream(pVM.RawFileName, FileMode.Open);
            StreamReader sr = new StreamReader(file, Encoding.UTF8);
            string filecontent = sr.ReadToEnd();
            sr.Close();
            file.Close();

            file = new FileStream(fullpath, FileMode.Create);
            StreamWriter sw = new StreamWriter(file, Encoding.UTF8);

            int i = 0;
            foreach (HeaderItem hi in pVM.header)
            {
                sw.WriteLine(hi.Item + ": ," + hi.Value);
                i++;
            }
            for (; i < 25; i++)     //一共25行，补齐空行
            {
                sw.WriteLine("");
            }
            sw.Write(filecontent);
            sw.Close();
            file.Close();
        }
        private void hdrCommit_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton tb = sender as ToggleButton;
            if (tb.IsChecked == true)
            {
                Int32 max = 0, min = 0;
                foreach (HeaderItem hi in pVM.header)
                {
                    if (hi.Type == 1)   //如果是combobox，则value为combobox中的选定值
                    {
                        hi.Value = hi.itemlist[hi.listindex];
                    }
                    if (hi.Value == "" || hi.Value == null) //输入内容不能为空
                    {
                        MessageBox.Show("头信息不能为空!");
                        tb.IsChecked = false;
                        return;
                    }
                    //int errortype=0;
                    switch (hi.Item)
                    {
                        case "Test Time":
                            //if (!Regex.IsMatch(hi.Value, @"^\d{4}-\d{1,2}-\d{1,2}$"))
                            if (!Regex.IsMatch(hi.Value, @"^201[4-9]-(1[0-2]|0?[1-9])-(3[01]|[1-2][0-9]|0?[1-9])$"))
                            {
                                MessageBox.Show("无效日期!");
                                tb.IsChecked = false;
                                return;
                            }
                            break;
                        case "Current(mA)":
                            if (Convert.ToInt32(hi.Value) <= 0)
                            {
                                MessageBox.Show("无效电流!");
                                tb.IsChecked = false;
                                return;
                            }
                            break;
                        case "Absolute Max Capacity(mAh)":
                            if (Convert.ToInt32(hi.Value) <= 0)
                            {
                                MessageBox.Show("无效 Absolute Max Capacity!");
                                tb.IsChecked = false;
                                return;
                            }
                            break;
                        case "Limited Charge Voltage(mV)":
                            if (Convert.ToInt32(hi.Value) <= 0)
                            {
                                MessageBox.Show("无效 Limited Charge Voltage!");
                                tb.IsChecked = false;
                                return;
                            }
                            max = Convert.ToInt32(hi.Value);
                            break;
                        case "Cut-off Discharge Voltage(mV)":
                            if (Convert.ToInt32(hi.Value) <= 0)
                            {
                                MessageBox.Show("无效 Cut-off Discharge Voltage!");
                                tb.IsChecked = false;
                                return;
                            }
                            min = Convert.ToInt32(hi.Value);
                            break;
                        default: break;
                    }
                }
                if (max <= min)
                {
                    MessageBox.Show("Limited Charge Voltage 要比 Cut-off Discharge Voltage 大!");
                    tb.IsChecked = false;
                    return;
                }
                MakeSrcPanel.Visibility = Visibility.Visible;
                //Header.IsEnabled = false;
            }
            else
            {
                MakeSrcPanel.Visibility = Visibility.Hidden;
                //Header.IsEnabled = true;
            }
        }

        private void hdrLoad_Click(object sender, RoutedEventArgs e)
        {
            string fullpath = "";
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Title = "Load Header File";
            openFileDialog.Filter = "Header files (*.hdr)|*.hdr||";
            openFileDialog.FileName = "default";
            openFileDialog.FilterIndex = 1;
            openFileDialog.RestoreDirectory = true;
            openFileDialog.DefaultExt = "hdr";
            openFileDialog.InitialDirectory = pVM.CFGfolder;
            if (openFileDialog.ShowDialog() == true)
            {
                pVM.header.Clear();
                //if (parent == null) return;
                {
                    fullpath = openFileDialog.FileName;
                    LoadHDRFile(fullpath);
                }
            }
        }


        private void LoadHDRFile(string fullpath)
        {
            FileStream file = new FileStream(fullpath, FileMode.Open);
            StreamReader sr = new StreamReader(file);
            /*foreach (TrimItem ti in ptrimViewModel.TrimList)
            {
                ti.pVolt = sr.ReadLine();
            }*/
            string line;
            while ((line = sr.ReadLine()) != null)  //"Item: ,Value"
            {
                HeaderItem hi = new HeaderItem();
                string[] strArray = line.Split(',');
                hi.Item = strArray[0];
                hi.Value = strArray[1];
                pVM.header.Add(hi);
            }
            sr.Close();
            file.Close();
        }

        private void hdrSave_Click(object sender, RoutedEventArgs e)
        {

            string fullpath = "";
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            saveFileDialog.Title = "Save Header File";
            saveFileDialog.Filter = "Header Files (*.hdr)|*.hdr||";
            saveFileDialog.FileName = "default";
            saveFileDialog.FilterIndex = 1;
            saveFileDialog.RestoreDirectory = true;
            saveFileDialog.DefaultExt = "hdr";
            saveFileDialog.InitialDirectory = pVM.CFGfolder;
            if (saveFileDialog.ShowDialog() == true)
            {
                //if (parent == null) return;
                //else
                {
                    fullpath = saveFileDialog.FileName;
                    SaveHDRFile(fullpath);
                }
            }
        }

        private void SaveHDRFile(string fullpath)
        {
            FileStream file = new FileStream(fullpath, FileMode.Create);
            StreamWriter sw = new StreamWriter(file);
            foreach (HeaderItem hi in pVM.header)
            {
                if (hi.Type == 1)
                    sw.WriteLine(hi.Item + "," + hi.itemlist[hi.listindex]);
                else
                    sw.WriteLine(hi.Item + "," + hi.Value);
            }
            sw.Close();
            file.Close();
        }
        #endregion

        #region Source2OutputPanel

        #region Step1
        private void Header_AutoGeneratedColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            string headername = e.Column.Header.ToString();
            //Cancel the column you don't want to generate
            if (headername == "parent")
            {
                e.Cancel = true;
            }
            if (headername == "Value")
            {
                e.Column.IsReadOnly = false;
            }
            if (headername == "Item")
            {
                e.Column.IsReadOnly = true;
            }
        }        

        private void SourceLoad_Click(object sender, RoutedEventArgs e)
        {
            //string fullpath = "";
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Title = "Load Source File";
            openFileDialog.Filter = "(*.csv)|*.csv|(*.xlsx)|*.xlsx||";
            openFileDialog.FileName = "default";
            openFileDialog.FilterIndex = 1;
            openFileDialog.RestoreDirectory = true;
            openFileDialog.DefaultExt = "csv";
            openFileDialog.InitialDirectory = pVM.Sourcefolder;
            openFileDialog.Multiselect = true;
            if (openFileDialog.ShowDialog() == true)
            {
                //pVM.sourcelist.Clear();
                //SDlist.Clear();
                //myTable.ClearFiles();
                foreach (string str in openFileDialog.FileNames)
                {
                    FileInfo fi = new FileInfo(str);
                    SourceFile sf = new SourceFile(str, pVM);
                    sf.filesize = fi.Length;
                    pVM.sourcelist.Add(sf);
                }
            }
        }
        private void SourceClear_Click(object sender, RoutedEventArgs e)
        {
            if (srcCommitBtn.IsChecked != true)
            {
                pVM.sourcelist.Clear();
                myTable.ClearFiles();
            }
        }
        private void SourceFileList_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            string headername = e.Column.Header.ToString();

            //Cancel the column you don't want to generate
            if (headername == "parent")
            {
                e.Cancel = true;
            }
        }
        private void srcCommit_Click(object sender, RoutedEventArgs e)
        {
            Hashtable ht = new Hashtable();
            TypeEnum te = TypeEnum.RCRawType;
            if (srcCommitBtn.IsChecked == true)
            {
                //if (pVM.sourcelist.Count > 0) //sourcelist.Count > 0 时Commit Button才会Enable
                {
                    UInt32 uErrorCode = 0;
                    foreach (SourceFile sf in pVM.sourcelist)
                    {
                        //SourceDataSample TempSample = new SourceDataSample(sf.filename);
                        if (myTable.AddSourceFile(sf.filename, ref uErrorCode, out te))
                        {
                            pVM.SrcFileType = te; //什么都不用做
                        }
                        else    //如果AddSourceFile过程中产生错误，则置起hasError标志位//弹出对话框让用户选择如何继续-不在此处
                        { 
                            List<TableError> tmpErrorlog = new List<TableError>();
                            myTable.GetErrorLog(ref tmpErrorlog);
                            foreach (TableError tberr in tmpErrorlog)
                            {
                                string ec = "0x" + tberr.uiErrorCode.ToString("X");
                                if (ht.Contains(ec))
                                {
                                    ht[ec] = (int)(ht[ec]) + (int)1;
                                }
                                else
                                    ht.Add(ec, (int)1);
                            }
                        }
                    }

                    if (ht.Count>0)
                    {
                        string wrnmsg = "";
                        foreach (DictionaryEntry de in ht)
                        {
                            UInt32 x = Convert.ToUInt32(de.Key.ToString(), 16);
                            wrnmsg += LibErrorCode.GetErrorDescription(x,false)+"\t发生了 "+de.Value.ToString()+" 次\n\n";
                        }
                        wrnmsg += "请查看Log文件夹下的log文件以了解详细错误信息\n";
                        wrnmsg += "点击 'OK' 忽略警告，点击'Cancel'取消操作.";

                        MessageBoxResult res = MessageBox.Show(wrnmsg, "警告", MessageBoxButton.OKCancel);
                        if (res == MessageBoxResult.Cancel)
                        {
                            srcCommitBtn.IsChecked = false;
                            return;
                        }
                    }

                    pVM.SrcFileType = te;
                    if (pVM.SrcFileType == TypeEnum.OCVRawType)
                    {
                        RCcfgPanel.Visibility = Visibility.Hidden;
                        CcfgPanel.Visibility = Visibility.Hidden;
                        OCVcfgPanel.Visibility = Visibility.Visible;
                        Makepanel.Visibility = Visibility.Visible;
                    }
                    else if (pVM.SrcFileType == TypeEnum.RCRawType)
                    {
                        RCcfgPanel.Visibility = Visibility.Visible;
                        CcfgPanel.Visibility = Visibility.Hidden;
                        OCVcfgPanel.Visibility = Visibility.Hidden;
                        Makepanel.Visibility = Visibility.Hidden;
                    }
                    else if (pVM.SrcFileType == TypeEnum.ChargeRawType)
                    {
                        RCcfgPanel.Visibility = Visibility.Hidden;
                        CcfgPanel.Visibility = Visibility.Visible;
                        OCVcfgPanel.Visibility = Visibility.Hidden;
                        Makepanel.Visibility = Visibility.Hidden;
                    }

                    /*UInt16 ulo = 0, uhi = 0;
                    if (myTable.GetVoltageBoundry(ref ulo, ref uhi))
                    {
                        MessageBox.Show("Minimum Voltage = " + ulo.ToString() + "mV, and Maximum Voltage = " + uhi.ToString() + "mV");
                    }*/
                }
                /*else
                {
                    MessageBox.Show("No file loaded!");
                    srcCommitBtn.IsChecked = false;
                }*/
            }
            else
            {
                RCcfgPanel.Visibility = Visibility.Hidden;
                CcfgPanel.Visibility = Visibility.Hidden;
                OCVcfgPanel.Visibility = Visibility.Hidden;
                Makepanel.Visibility = Visibility.Hidden;
            }
        }
        #endregion

        #region Step2
        private void VoltageList_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            string headername = e.Column.Header.ToString();

            //Cancel the column you don't want to generate
            if (headername == "parent")
            {
                e.Cancel = true;
            }
        }

        private void VCFGLoad_Click(object sender, RoutedEventArgs e)
        {
            string fullpath = "";
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Title = "Voltage Point Configuration File";
            openFileDialog.Filter = "Voltage Point Configuration files (*.vcfg)|*.vcfg||";
            openFileDialog.FileName = "default";
            openFileDialog.FilterIndex = 1;
            openFileDialog.RestoreDirectory = true;
            openFileDialog.DefaultExt = "vcfg";
            openFileDialog.InitialDirectory = pVM.CFGfolder;
            if (openFileDialog.ShowDialog() == true)
            {
                pVM.voltagelist.Clear();
                //if (parent == null) return;
                {
                    fullpath = openFileDialog.FileName;
                    LoadVCFGFile(fullpath);
                }
            }
        }
        private void LoadVCFGFile(string fullpath)
        {
            FileStream file = new FileStream(fullpath, FileMode.Open);
            StreamReader sr = new StreamReader(file);
            /*foreach (TrimItem ti in ptrimViewModel.TrimList)
            {
                ti.pVolt = sr.ReadLine();
            }*/
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                VoltagePoint vp = new VoltagePoint();
                vp.Voltage = line;
                pVM.voltagelist.Add(vp);
            }
            sr.Close();
            file.Close();
        }
        private void VSaveCFG_Click(object sender, RoutedEventArgs e)
        {
            string fullpath = "";
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            saveFileDialog.Title = "Voltage Point Configuration File";
            saveFileDialog.Filter = "Voltage Point Configuration Files (*.vcfg)|*.vcfg||";
            saveFileDialog.FileName = "default";
            saveFileDialog.FilterIndex = 1;
            saveFileDialog.RestoreDirectory = true;
            saveFileDialog.DefaultExt = "vcfg";
            saveFileDialog.InitialDirectory = pVM.CFGfolder;
            if (saveFileDialog.ShowDialog() == true)
            {
                //if (parent == null) return;
                //else
                {
                    fullpath = saveFileDialog.FileName;
                    SaveVCFGFile(fullpath);
                }
            }
        }
        private void SaveVCFGFile(string fullpath)
        {
            FileStream file = new FileStream(fullpath, FileMode.Create);
            StreamWriter sw = new StreamWriter(file);
            foreach (VoltagePoint vp in pVM.voltagelist)
            {
                sw.WriteLine(Convert.ToString(vp.Voltage));
            }
            sw.Close();
            file.Close();
        }

        private void CCFGLoad_Click(object sender, RoutedEventArgs e)
        {
            string fullpath = "";
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Title = "Current Point Configuration File";
            openFileDialog.Filter = "Current Point Configuration files (*.ccfg)|*.ccfg||";
            openFileDialog.FileName = "default";
            openFileDialog.FilterIndex = 1;
            openFileDialog.RestoreDirectory = true;
            openFileDialog.DefaultExt = "ccfg";
            openFileDialog.InitialDirectory = pVM.CFGfolder;
            if (openFileDialog.ShowDialog() == true)
            {
                pVM.currentlist.Clear();
                //if (parent == null) return;
                {
                    fullpath = openFileDialog.FileName;
                    LoadCCFGFile(fullpath);
                }
            }
        }
        private void LoadCCFGFile(string fullpath)
        {
            FileStream file = new FileStream(fullpath, FileMode.Open);
            StreamReader sr = new StreamReader(file);
            /*foreach (TrimItem ti in ptrimViewModel.TrimList)
            {
                ti.pVolt = sr.ReadLine();
            }*/
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                CurrentPoint cp = new CurrentPoint();
                cp.Current = line;
                pVM.currentlist.Add(cp);
            }
            sr.Close();
            file.Close();
        }
        private void CSaveCFG_Click(object sender, RoutedEventArgs e)
        {
            string fullpath = "";
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            saveFileDialog.Title = "Current Point Configuration File";
            saveFileDialog.Filter = "Current Point Configuration Files (*.ccfg)|*.ccfg||";
            saveFileDialog.FileName = "default";
            saveFileDialog.FilterIndex = 1;
            saveFileDialog.RestoreDirectory = true;
            saveFileDialog.DefaultExt = "ccfg";
            saveFileDialog.InitialDirectory = pVM.CFGfolder;
            if (saveFileDialog.ShowDialog() == true)
            {
                //if (parent == null) return;
                //else
                {
                    fullpath = saveFileDialog.FileName;
                    SaveCCFGFile(fullpath);
                }
            }
        }
        private void SaveCCFGFile(string fullpath)
        {
            FileStream file = new FileStream(fullpath, FileMode.Create);
            StreamWriter sw = new StreamWriter(file);
            foreach (CurrentPoint cp in pVM.currentlist)
            {
                sw.WriteLine(Convert.ToString(cp.Current));
            }
            sw.Close();
            file.Close();
        }

        private void vcfgCommit_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton tb = sender as ToggleButton;
            if (tb.IsChecked == true)
            {
                if (pVM.voltagelist.Count == 0)
                {
                    MessageBox.Show("配置为空!");
                    tb.IsChecked = false;
                    return;
                }
                foreach (VoltagePoint vp in pVM.voltagelist)
                {
                    if (vp.Voltage == "" || vp.Voltage == null)
                    {
                        MessageBox.Show("配置为空!");
                        tb.IsChecked = false;
                        return;
                    }
                }
                Makepanel.Visibility = Visibility.Visible;
                //Header.IsEnabled = false;
            }
            else
            {
                Makepanel.Visibility = Visibility.Hidden;
                //Header.IsEnabled = true;
            }
        }
        private void ocvCommitBtn_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton tb = sender as ToggleButton;
            if (tb.IsChecked == true)
            {
                if (ocvLow.Text == null || ocvLow.Text == "" || ocvHigh.Text == null || ocvHigh.Text == "")
                {
                    MessageBox.Show("配置为空!");
                    tb.IsChecked = false;
                    return;
                }
                UInt32 low = Convert.ToUInt32(ocvLow.Text);
                UInt32 high = Convert.ToUInt32(ocvHigh.Text);
                if (low > high)
                {
                    MessageBox.Show("配置出错!");
                    tb.IsChecked = false;
                    return;
                }
                pVM.OCVhigh = high;
                pVM.OCVlow = low;
                Makepanel.Visibility = Visibility.Visible;
                //Header.IsEnabled = false;
            }
            else
            {
                Makepanel.Visibility = Visibility.Hidden;
                //Header.IsEnabled = true;
            }
        }
        private void ccfgCommit_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton tb = sender as ToggleButton;
            if (tb.IsChecked == true)
            {
                if (pVM.currentlist.Count == 0)
                {
                    MessageBox.Show("配置为空!");
                    tb.IsChecked = false;
                    return;
                }
                foreach (CurrentPoint cp in pVM.currentlist)
                {
                    if (cp.Current == "" || cp.Current == null)
                    {
                        MessageBox.Show("配置为空!");
                        tb.IsChecked = false;
                        return;
                    }
                }
                Makepanel.Visibility = Visibility.Visible;
                //Header.IsEnabled = false;
            }
            else
            {
                Makepanel.Visibility = Visibility.Hidden;
                //Header.IsEnabled = true;
            }
        }
        #endregion

        #region Step3
        private void BrowseBtn_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog dlg = new System.Windows.Forms.FolderBrowserDialog();
            dlg.Description = "Choose an empty folder to save all the output files";
            dlg.RootFolder = Environment.SpecialFolder.Desktop;
            dlg.ShowNewFolderButton = true;
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.Cancel)
                return;

            if (Directory.GetFiles(dlg.SelectedPath).Length != 0)
            {
                MessageBox.Show("此目录不是空目录, 请选择一个空目录!");
                return;
            }

            pVM.OutPutfolder = dlg.SelectedPath;
        }
        private void OutputMake_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(pVM.OutPutfolder))
                Directory.CreateDirectory(pVM.OutPutfolder);
            else
            {
                if (Directory.GetFiles(pVM.OutPutfolder).Length != 0)
                {
                    MessageBox.Show("此目录不是空目录, 请选择一个空目录!");
                    return;
                }
            }

            if (!Regex.IsMatch(strVersion.Text, @"^[0-9]?[0-9]$"))
            {
                MessageBox.Show("无效版本号!");
                return;
            }
            if (!Regex.IsMatch(strDate.Text, @"^201[4-9].(1[0-2]|0?[1-9]).(3[01]|[1-2][0-9]|0?[1-9])$"))
            {
                MessageBox.Show("无效日期!");
                return;
            }

            UInt32 uErrorCode = 0;
            Hashtable ht = new Hashtable();
            List<UInt32> samVolt = new List<UInt32>();
            //(A170308)Francis, 
            bool bChecked = chkVTRTable.IsChecked.Value;
            //(E170308)
            if (pVM.SrcFileType == TypeEnum.OCVRawType)
            {
                samVolt.Add(Convert.ToUInt32(pVM.OCVlow));
                samVolt.Add(Convert.ToUInt32(pVM.OCVhigh));
                //OCVTSOCSample myOCVTable = new OCVTSOCSample(SDlist[0], pVM.OCVlow, pVM.OCVhigh);
                /*if (myOCVTable.BuildTable(ref uErrorCode, pVM.OutPutfolder))
                {
                    MessageBox.Show("Build OCV successfully.");
                    OCVcfgPanel.Visibility = Visibility.Hidden;
                    Makepanel.Visibility = Visibility.Hidden;
                    pVM.sourcelist.Clear();
                }
                else
                {
                    MessageBox.Show("Build Error");
                }*/
            }
            else if (pVM.SrcFileType == TypeEnum.RCRawType)
            {
                //List<UInt16> samVolt = new List<UInt16>();
                foreach (VoltagePoint vp in pVM.voltagelist)
                {
                    samVolt.Add(Convert.ToUInt32(vp.Voltage));
                }
                /*RCSample myRCtable = new RCSample(SDlist, samVolt);
                if (myRCtable.BuildTable(ref uErrorCode, pVM.OutPutfolder))
                {
                    MessageBox.Show("Build RC successfully.");
                    RCcfgPanel.Visibility = Visibility.Hidden;
                    Makepanel.Visibility = Visibility.Hidden;
                    pVM.sourcelist.Clear();
                }
                else
                {
                    MessageBox.Show("Build Error");
                }*/
            }
            else if (pVM.SrcFileType == TypeEnum.ChargeRawType)
            {
                foreach (CurrentPoint cp in pVM.currentlist)
                {
                    samVolt.Add(Convert.ToUInt32(cp.Current));
                }
            }
            List<TableError> tmpErrorlog = new List<TableError>();
            myTable.GetErrorLog(ref tmpErrorlog);

            List<string> mkParamString = new List<string>();
            mkParamString.Add(strVersion.Text);
            mkParamString.Add(strDate.Text);
            mkParamString.Add(strComment.Text);
            if (myTable.MakeTable(samVolt, ref uErrorCode, pVM.OutPutfolder, mkParamString, bChecked))
            {
                //MessageBox.Show(LibErrorCode.GetErrorDescription(uErrorCode));
                //tmpErrorlog.Clear();
                myTable.GetErrorLog(ref tmpErrorlog);


            }
            else
            {
                myTable.GetErrorLog(ref tmpErrorlog);
                foreach (TableError tberr in tmpErrorlog)
                {
                    string ec = "0x" + tberr.uiErrorCode.ToString("X");
                    if (ht.Contains(ec))
                    {
                        ht[ec] = (int)(ht[ec]) + (int)1;
                    }
                    else
                        ht.Add(ec, (int)1);
                }
                //if (ht.Count > 0)
                {
                    string wrnmsg = "";
                    foreach (DictionaryEntry de in ht)
                    {
                        UInt32 x = Convert.ToUInt32(de.Key.ToString(), 16);
                        wrnmsg += LibErrorCode.GetErrorDescription(x, false) + "\t发生了 " + de.Value.ToString() + " 次\n\n";
                    }
                    wrnmsg += "请查看Output文件夹下的log文件以了解详细错误信息\n";
                    wrnmsg += "点击 'OK' 忽略警告，点击'Cancel'取消操作.";

                    MessageBoxResult res = MessageBox.Show(wrnmsg, "警告", MessageBoxButton.OKCancel);
                    if (res == MessageBoxResult.Cancel)
                    {
                        srcCommitBtn.IsChecked = false;
                        return;
                    }
                }
            }
            if (myTable.GenerateFile(ref uErrorCode))
            {
                MessageBox.Show("完成！");
                RCcfgPanel.Visibility = Visibility.Hidden;
                CcfgPanel.Visibility = Visibility.Hidden;
                OCVcfgPanel.Visibility = Visibility.Hidden;
                Makepanel.Visibility = Visibility.Hidden;
                srcCommitBtn.IsChecked = false;
                ocvCommitBtn.IsChecked = false;
                vcfgCommitBtn.IsChecked = false;
                ccfgCommitBtn.IsChecked = false;
                pVM.sourcelist.Clear();
                myTable.ClearFiles();
            }
            else {
                MessageBox.Show(LibErrorCode.GetErrorDescription(uErrorCode));
            }
        }
        #endregion

        #endregion

        #region Old table to FalconLY table
        private void btnOCVFileOpen_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Title = "Load OCV txt File";
            openFileDialog.Filter = "(*.txt)|*.txt||";
            openFileDialog.FileName = "ocv";
            openFileDialog.FilterIndex = 1;
            openFileDialog.RestoreDirectory = true;
            openFileDialog.DefaultExt = "txt";
            openFileDialog.InitialDirectory = pVM.OutPutfolder;
            openFileDialog.Multiselect = false;
            if (openFileDialog.ShowDialog() == true)
            {
                oldTableObj.strOCVTXTFileFullPath = openFileDialog.FileName;
                //txbOCVFileOpen.Text = oldTableObj.strOCVTXTFileFullPath;
                if (!oldTableObj.readOCVtxtFileContent())
                {
                    MessageBox.Show(oldTableObj.getLastErrorDescription());
                }
            }
            //btnCommitFiles.IsEnabled = oldTableObj.checkFilesValid();
        }

        private void btnRCFileOpen_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Title = "Load OCV RC File";
            openFileDialog.Filter = "(*.txt)|*.txt||";
            openFileDialog.FileName = "rc";
            openFileDialog.FilterIndex = 1;
            openFileDialog.RestoreDirectory = true;
            openFileDialog.DefaultExt = "txt";
            openFileDialog.InitialDirectory = pVM.OutPutfolder;
            openFileDialog.Multiselect = false;
            if (openFileDialog.ShowDialog() == true)
            {
                oldTableObj.strRCTXTFileFullPath = openFileDialog.FileName;
                //txbRCFileOpen.Text = oldTableObj.strRCTXTFileFullPath;
                if(!oldTableObj.readRCtxtFileContent())
                {
                    //Open & read file error, from viewmodel.cs
                    MessageBox.Show(oldTableObj.getLastErrorDescription());
                }
            }
            //btnCommitFiles.IsEnabled = oldTableObj.checkFilesValid();
        }

        private void btnCHFileOpen_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Title = "Load Android driver c/h File";
            openFileDialog.Filter = "(*.c)(*.h)|*.c;*.h||";
            openFileDialog.FileName = "driver";
            openFileDialog.FilterIndex = 1;
            openFileDialog.RestoreDirectory = true;
            openFileDialog.DefaultExt = "txt";
            openFileDialog.InitialDirectory = pVM.OutPutfolder;
            openFileDialog.Multiselect = true;
            if (openFileDialog.ShowDialog() == true)
            {
                foreach (string strfile in openFileDialog.FileNames)
                {
                    FileInfo fi = new FileInfo(strfile);
                    SourceFile sf = new SourceFile(strfile);
                    sf.filesize = fi.Length;
                    oldTableObj.OldTableFiles.Add(sf);
                }
                if(oldTableObj.OldTableFiles.Count == 2)
                {
                    if(!oldTableObj.readCHFilesContent())
                    {
                        //Open & read file error, from viewmodel.cs
                        MessageBox.Show(oldTableObj.getLastErrorDescription());
                    }
                }
                else 
                {
                    //Message Error
                    MessageBox.Show("必須選擇兩個文件");
                }
            }
            //btnCommitFiles.IsEnabled = oldTableObj.checkFilesValid();
        }

        private void btnClearFiles_Click(object sender, RoutedEventArgs e)
        {
            oldTableObj.clearAllFiles();
            grpMakeTable.Visibility = Visibility.Hidden;
            //btnCommitFiles.IsEnabled = oldTableObj.checkFilesValid();
        }

        private void btnCommitFiles_Click(object sender, RoutedEventArgs e)
        {
            if(oldTableObj.checkFilesValid())
            {
                grpMakeTable.Visibility = Visibility.Visible;
            }
        }

        private void btnBrowseFly_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog dlg = new System.Windows.Forms.FolderBrowserDialog();
            dlg.Description = "Choose an empty folder to save all the output files";
            dlg.RootFolder = Environment.SpecialFolder.Desktop;
            dlg.ShowNewFolderButton = true;
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.Cancel)
                return;

            if (Directory.GetFiles(dlg.SelectedPath).Length != 0)
            {
                MessageBox.Show("此目录不是空目录, 请选择一个空目录!");
                return;
            }

            //pVM.OutPutfolder = dlg.SelectedPath;
            txbBrowseFolderFly.Text = dlg.SelectedPath;

        }

        private void btnMakeFalconLY_Click(object sender, RoutedEventArgs e)
        {
            List<string> lstrUsersinput = new List<string>();

            if (!Directory.Exists(txbBrowseFolderFly.Text))
                Directory.CreateDirectory(txbBrowseFolderFly.Text);
            else
            {
                if (Directory.GetFiles(txbBrowseFolderFly.Text).Length != 0)
                {
                    MessageBox.Show("此目录不是空目录, 请选择一个空目录!");
                    return;
                }
            }

            if (!Regex.IsMatch(txbVersonFalconLY.Text, @"^[0-9]?[0-9]$"))
            {
                MessageBox.Show("无效版本号!");
                return;
            }
            if (!Regex.IsMatch(txbDateFalconLY.Text, @"^201[4-9].(1[0-2]|0?[1-9]).(3[01]|[1-2][0-9]|0?[1-9])$"))
            {
                MessageBox.Show("无效日期!");
                return;
            }

            lstrUsersinput.Add(txbVersonFalconLY.Text);
            lstrUsersinput.Add(txbDateFalconLY.Text);
            lstrUsersinput.Add(txbCommentFalconLY.Text);
            if (oldTableObj.convertToNewTables(txbBrowseFolderFly.Text, lstrUsersinput))
            {
                MessageBox.Show("完成！");
                oldTableObj.clearAllFiles();
                grpMakeTable.Visibility = Visibility.Hidden;
            }
            else
            {
                MessageBox.Show(oldTableObj.getLastErrorDescription());
            }
        }

        #endregion

        #endregion

        #endregion
    }
}
