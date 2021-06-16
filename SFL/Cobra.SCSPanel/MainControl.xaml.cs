using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using System.Windows.Controls.Primitives;
using System.Threading;
using System.IO;
using System.Xml;
using System.Data;
using System.Reflection;
using Cobra.EM;
using Cobra.Common;

namespace Cobra.SCSPanel
{
    /// <summary>
    /// Interaction logic for MainControl.xaml
    /// </summary>
    public partial class MainControl : UserControl
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

        private TASKMessage m_Msg = new TASKMessage();
        public TASKMessage msg
        {
            get { return m_Msg; }
            set { m_Msg = value; }
        }

        private ViewMode m_viewmode;
        public ViewMode viewmode
        {
            get { return m_viewmode; }
            set { m_viewmode = value; }
        }

        private UInt16 subTask = 0x31;
        private BackgroundWorker m_bgWorker;// 申明后台对象

        private bool bsubTask = false;
        private UInt16 intvalTime = 0;
        private UInt32 totalTime = 1000;
        private DateTime startTime = DateTime.Now;
        private GeneralMessage gm = new GeneralMessage("SCS SFL", "", 0);

        private AsyncObservableCollection<DataBaseRecord> m_DataBaseRecords = new AsyncObservableCollection<DataBaseRecord>();
        public AsyncObservableCollection<DataBaseRecord> dataBaseRecords    //LogData的集合
        {
            get { return m_DataBaseRecords; }
            set
            {
                m_DataBaseRecords = value;
            }
        }
        public int session_id = -1;
        public ulong session_row_number = 0;

        public MainControl(object pParent, string name)
        {
            this.InitializeComponent();
            #region 相关初始化
            parent = (Device)pParent;
            if (parent == null) return;

            sflname = name;
            if (String.IsNullOrEmpty(sflname)) return;
            #endregion
            bsubTask = false;
            viewmode = new ViewMode(pParent, this); 
            InitUI();
            InitBWork();
        }

        public void InitUI()
        {
            parent.db_Manager.NewSession(sflname, ref session_id, DateTime.Now.ToString());
            XmlNodeList nodelist = parent.GetUINodeList(sflname);
            foreach (XmlNode node in nodelist)
            {
                switch (node.Name.ToLower())
                {
                    case "layout":
                        {
                            foreach (XmlNode sub in node)
                            {
                                if (sub.Attributes["SubTask"] == null) continue;
                                bsubTask = true;
                                subTask = Convert.ToUInt16(sub.Attributes["SubTask"].Value.ToString(), 16);
                            }
                            break;
                        }
                }
            }
            UInt16.TryParse(intervalTb.Text.Trim(), out intvalTime);
            UInt32.TryParse(totalTb.Text.Trim(), out totalTime);

            ChannelSelector.ItemsSource = viewmode.sfl_parameterlist;
            ChannelSelector.SelectedIndex = 0;
            ChannelSelector.SelectionChanged += new SelectionChangedEventHandler(ChannelSelector_SelectionChanged);
            Model mo = ChannelSelector.SelectedItem as Model;
            if (mo == null) return;
            viewmode.scan_parameterlist.parameterlist.Clear();
            viewmode.scan_parameterlist.parameterlist.Add(mo.parent);
            UpdateDBRecordList();
            dbRecordDataGrid.ItemsSource = dataBaseRecords;
        }

        void ChannelSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Model mo = ChannelSelector.SelectedItem as Model;
            if (mo == null) return;
            viewmode.scan_parameterlist.parameterlist.Clear();
            viewmode.scan_parameterlist.parameterlist.Add(mo.parent);
        }

        public void InitBWork()
        {
            m_bgWorker = new BackgroundWorker(); // 实例化后台对象
            m_bgWorker.WorkerReportsProgress = true; // 设置可以通告进度
            m_bgWorker.WorkerSupportsCancellation = true; // 设置可以取消
            m_bgWorker.DoWork += new DoWorkEventHandler(DoWork);
            m_bgWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(CompletedWork);
        }

        void DoWork(object sender, DoWorkEventArgs e)
        {
            startTime = DateTime.Now;
            BackgroundWorker bw = sender as BackgroundWorker;
            while (true)
            {
                if (bw.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }

                if (totalTime != 0)
                {
                    if ((DateTime.Now - startTime).TotalMilliseconds > totalTime)
                    {
                        e.Cancel = true;
                        return;
                    }
                }

                if (Access() != LibErrorCode.IDS_ERR_SUCCESSFUL)
                {
                    e.Cancel = true;
                    return;
                }
                Thread.Sleep(intvalTime);
            }
        }

        void CompletedWork(object sender, RunWorkerCompletedEventArgs e)
        {
            runBtn.Content = "Start";
            runBtn.IsChecked = false;

            m_bgWorker.CancelAsync();
            while (m_bgWorker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            UpdateDBRecordList();
        }

        #region 通用控件消息响应
        public void CallWarningControl(GeneralMessage message)
        {
            WarningPopControl.Dispatcher.Invoke(new Action(() =>
            {
                WarningPopControl.ShowDialog(message);
            }));
        }
        #endregion

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton btn = sender as ToggleButton;
            UInt16.TryParse(intervalTb.Text.Trim(), out intvalTime);
            UInt32.TryParse(totalTb.Text.Trim(), out totalTime);

            if (runBtn.IsChecked == true)
            {
                runBtn.Content = "Stop";
                m_bgWorker.RunWorkerAsync();
            }
            else
            {
                runBtn.Content = "Start";
                runBtn.IsChecked = false;
                m_bgWorker.CancelAsync();
                while (m_bgWorker.IsBusy)
                    System.Windows.Forms.Application.DoEvents();
            }
        }

        private UInt32 Access()
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;


            if (bsubTask)
            {
                msg.owner = this;
                msg.gm.sflname = sflname;
                msg.task = TM.TM_COMMAND;
                msg.sub_task = subTask;
                msg.task_parameterlist = viewmode.scan_parameterlist;
                parent.AccessDevice(ref m_Msg);
                while (msg.bgworker.IsBusy)
                    System.Windows.Forms.Application.DoEvents();
                if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                {
                    gm.level = 2;
                    gm.message = LibErrorCode.GetErrorDescription(msg.errorcode);
                    CallWarningControl(gm);
                    parent.bBusy = false;
                    ret = msg.errorcode;
                    return ret;
                }
            }
            else
            {
                ret = Read();
                if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                {
                    gm.message = LibErrorCode.GetErrorDescription(ret);
                    gm.bupdate = true;
                    CallWarningControl(gm);
                    return ret;
                }

                ret = ConvertHexToPhysical();
                if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                {
                    gm.message = LibErrorCode.GetErrorDescription(ret);
                    gm.bupdate = true;
                    CallWarningControl(gm);
                    return ret;
                }
            }


            Dictionary<string, string> records = new Dictionary<string, string>();
            foreach (Parameter param in viewmode.scan_parameterlist.parameterlist)
            {
                Model mo = viewmode.GetParameterByGuid(param.guid);
                records[mo.nickname] = string.Format("{0:D}", param.hexdata);
            }
            records.Add("Time", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss-fff"));
            parent.db_Manager.BeginNewRow(session_id, records);
            return ret;
        }

        public UInt32 Read()
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            msg.task_parameterlist = viewmode.scan_parameterlist;

            msg.task = TM.TM_READ;
            ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                gm.level = 2;
                gm.message = LibErrorCode.GetErrorDescription(msg.errorcode);
                CallWarningControl(gm);
                parent.bBusy = false;
                ret = msg.errorcode;
                return ret;
            }
            return ret;
        }

        public UInt32 ConvertHexToPhysical()
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;

            msg.task = TM.TM_CONVERT_HEXTOPHYSICAL;
            msg.task_parameterlist = viewmode.scan_parameterlist;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                gm.level = 2;
                gm.message = LibErrorCode.GetErrorDescription(msg.errorcode);
                CallWarningControl(gm);
                parent.bBusy = false;
                ret = msg.errorcode;
                return ret;
            }
            return ret;
        }

        #region DB Operation
        public void UpdateDBRecordList()
        {
            List<List<String>> records = new List<List<string>>();
            if (session_id != 0 && session_row_number != 0)
                parent.db_Manager.UpdateSessionSize(session_id, session_row_number);
            parent.db_Manager.GetSessionsInfor(sflname, ref records);
            dataBaseRecords.Clear();
            foreach (var record in records)
            {
                DataBaseRecord ld = new DataBaseRecord();
                ld.Timestamp = record[0];
                ld.RecordNumber = Convert.ToInt64(record[1]);
                dataBaseRecords.Add(ld);
            }
        }

        private void ExportBtn_Click(object sender, RoutedEventArgs e)
        {
            string fullpath = "";
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            DataBaseRecord dbRecord = (sender as Button).DataContext as DataBaseRecord;
            string tmp = dbRecord.Timestamp;
            char[] skip = { ' ', '/', ':' };
            foreach (var s in skip)
            {
                tmp = tmp.Replace(s, '_');
            }
            tmp = "Scan_" + tmp;
            saveFileDialog.FileName = tmp;
            saveFileDialog.FilterIndex = 1;
            saveFileDialog.RestoreDirectory = true;
            saveFileDialog.Title = "Export DB data to csv file";
            saveFileDialog.Filter = "CSV file (*.csv)|*.csv||";
            saveFileDialog.DefaultExt = "csv";
            saveFileDialog.InitialDirectory = FolderMap.m_logs_folder;
            if (saveFileDialog.ShowDialog() == true)
            {
                DataTable dt = new DataTable();
                parent.db_Manager.GetOneSession(sflname, dbRecord.Timestamp, ref dt);
                fullpath = saveFileDialog.FileName;
                ExportDB(fullpath, dt);
            }
        }

        public bool ExportDB(string fullpath, DataTable dt) //Save buffer content to hard disk as temperary file, then clear buffer
        {
            FileStream file = new FileStream(fullpath, FileMode.Create);
            StreamWriter sw = new StreamWriter(file);
            int length;
            string str = "";
            foreach (DataColumn col in dt.Columns)
            {
                str += col.ColumnName + ",";
            }
            length = str.Length;
            str = str.Remove(length - 1);
            sw.WriteLine(str);

            foreach (DataRow row in dt.Rows)
            {
                str = "";
                foreach (DataColumn col in dt.Columns)
                {
                    str += row[col.ColumnName] + ",";
                }
                length = str.Length;
                str = str.Remove(length - 1);
                sw.WriteLine(str);
            }
            sw.Close();
            file.Close();
            return true;
        }

        private void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            DataBaseRecord dbRecord = (sender as Button).DataContext as DataBaseRecord;

            DataTable dt = new DataTable();
            parent.db_Manager.DeleteOneSession(sflname, dbRecord.Timestamp);
            UpdateDBRecordList();
        }
        #endregion
    }
}
