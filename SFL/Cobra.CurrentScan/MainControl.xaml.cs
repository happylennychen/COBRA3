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
using Cobra.Common;
using Cobra.EM;
using System.Collections;
using System.Reflection;
using System.Windows.Controls.Primitives;
using System.ComponentModel;
using System.Threading;
using System.Data;
using System.IO;

namespace Cobra.CurrentScan
{
    /// <summary>
    /// Interaction logic for MainControl.xaml
    /// </summary>
    public partial class MainControl
    {
        #region 变量定义
        bool isReentrant_Run = false;   //控制Run button的重入
        //父对象保存
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

        private TASKMessage m_Msg = new TASKMessage();
        public TASKMessage msg
        {
            get { return m_Msg; }
            set { m_Msg = value; }
        }

        DataTable dt = new DataTable();
        private GeneralMessage gm = new GeneralMessage("CURRENT SCAN SFL", "", 0);


        Parameter PackC = new Parameter();
        Parameter CADC = new Parameter();

        Thread IOThread;

        private AsyncObservableCollection<LogData> m_logdatalist = new AsyncObservableCollection<LogData>();
        public AsyncObservableCollection<LogData> logdatalist    //LogData的集合
        {
            get { return m_logdatalist; }
            set
            {
                m_logdatalist = value;
                //OnPropertyChanged("logdatalist");
            }
        }

        public int session_id = -1;
        public ulong session_row_number = 0;
        #endregion

        #region Internal Function

        private string GetHashTableValueByKey(string str, Hashtable htable)
        {
            /*foreach (DictionaryEntry de in htable)
            {
                if (de.Key.ToString().Equals(str))
                    return de.Value.ToString();
            }
            return "NoSuchKey";*/
            if (htable.ContainsKey(str))  //之所以不能这样用，是因为这个htable在创建的时候，Key的类型为XName而非string
                return htable[str].ToString();
            else
                return "NoSuchKey";
        }
        private delegate void UpdateLogUIDelegate();
        private void UpdateLogUI()
        {
            DataRow row = dt.NewRow();

            decimal packc = new decimal((double)PackC.phydata);
            decimal cadc = new decimal((double)CADC.phydata);
            row["PackC"] = Decimal.Round(packc, 1).ToString();
            row["CADC"] = Decimal.Round(cadc, 1).ToString();
            row["Time"] = DateTime.Now;
            if (dt.Rows.Count >= 1000)
                dt.Rows.RemoveAt(0);
            dt.Rows.Add(row);
            CurrentDataGrid.ScrollIntoView(CurrentDataGrid.Items[CurrentDataGrid.Items.Count - 1]); //scroll to the last item
        }

        #endregion
        public MainControl(object pParent, string name)
        {
            InitializeComponent();

            #region 相关初始化
            parent = (Device)pParent;
            if (parent == null) return;

            sflname = name;
            if (String.IsNullOrEmpty(sflname)) return;

            WarningPopControl.SetParent(CurrentScanUI);
            #endregion

            DataColumn col = new DataColumn();
            col.DataType = System.Type.GetType("System.String");
            col.ColumnName = "PackC";
            col.AutoIncrement = false;
            col.Caption = "PackC";
            col.ReadOnly = false;
            col.Unique = false;
            dt.Columns.Add(col);

            col = new DataColumn();
            col.DataType = System.Type.GetType("System.String");
            col.ColumnName = "CADC";
            col.AutoIncrement = false;
            col.Caption = "CADC";
            col.ReadOnly = false;
            col.Unique = false;
            dt.Columns.Add(col);

            col = new DataColumn();
            col.DataType = System.Type.GetType("System.DateTime");
            col.ColumnName = "Time";
            col.AutoIncrement = false;
            col.Caption = "Time";
            col.ReadOnly = false;
            col.Unique = false;
            dt.Columns.Add(col);

            CurrentDataGrid.DataContext = dt;
            loglist.ItemsSource = logdatalist;

            foreach (var p in parent.GetParamLists("Scan").parameterlist)
            {
                if (p.subtype == 4)
                {
                    PackC = p;
                    break;
                }
                else if (p.subtype == 5)
                {
                    CADC = p;
                    break;
                }
            }
            LibInfor.AssemblyRegister(Assembly.GetExecutingAssembly(), ASSEMBLY_TYPE.SFL);
            UpdateLogDataList();
        }
        #region event handler

        public void CallWarningControl(uint errorcode)
        {
            gm.message = LibErrorCode.GetErrorDescription(errorcode);

            if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                WarningPopControl.Dispatcher.Invoke(new Action(() =>
                {
                    WarningPopControl.ShowDialog(gm);
                }));
            }
        }

        public void CallWarningControl(GeneralMessage gm)
        {
            WarningPopControl.Dispatcher.Invoke(new Action(() =>
            {
                WarningPopControl.ShowDialog(gm);
            }));
        }

        private void EnterStartState()
        {
            gm.controls = "Run button";
            gm.message = "Read Device";
            gm.bupdate = false;  //??

            runBtn.Content = "Stop";
            SubTask.IsEnabled = false;
            IOThread = new Thread(IO_Callback);
            int i = 0;
            switch (SubTask.SelectedIndex)
            {
                case 0: i = 15; break;
                case 1: i = 13; break;
                case 2: i = 14; break;
            }
            IOThread.Start(i);
        }

        private void ResetContext()
        {
            parent.bBusy = false;
            runBtn.Content = "Run";
            runBtn.IsChecked = false;
            SubTask.IsEnabled = true;
            isReentrant_Run = false;
        }


        private delegate void EnterStopStateDelegate(UInt32 errorcode);
        private void EnterStopState(UInt32 errorcode)
        {
            IOThread.Abort();

            gm.controls = "Stop button";
            gm.message = LibErrorCode.GetErrorDescription(errorcode);
            gm.bupdate = true;

            ResetContext();
            CallWarningControl(errorcode);
        }


        public void PreRead(ref uint errorcode)
        {
            //uint errorcode = (uint)obj;
            #region 预读数据
            errorcode = GetSysInfo();                                           //读GPIO信息 
            if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                return;
            }
            errorcode = GetDevInfo();                                           //读设备信息
            if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                return;
            }
            #endregion
        }

        private void UpdateLogDataList()
        {
            List<List<String>> records = new List<List<string>>();
            if (session_id != 0 && session_row_number != 0)
                parent.db_Manager.UpdateSessionSize(session_id, session_row_number);
            parent.db_Manager.GetSessionsInfor(sflname, ref records);

            logdatalist.Clear();
            foreach (var record in records)
            {
                LogData ld = new LogData();
                ld.Timestamp = record[0];
                ld.RecordNumber = Convert.ToInt64(record[1]);
                logdatalist.Add(ld);
            }
        }

        private void runBtn_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton btn = sender as ToggleButton;
            uint errorcode = 0;
            if (isReentrant_Run == false)   //此次点击并没有重入
            {
                isReentrant_Run = true;
                if ((bool)btn.IsChecked)    //点了Run
                {
                    if (parent.bBusy)       //Scan功能是否被其他SFL占用
                    {
                        errorcode = LibErrorCode.IDS_ERR_EM_THREAD_BKWORKER_BUSY;
                        ResetContext();
                        CallWarningControl(errorcode);
                        return;
                    }
                    else
                        parent.bBusy = true;

                    if (msg.bgworker.IsBusy == true) //bus是否正忙
                    {
                        errorcode = LibErrorCode.IDS_ERR_EM_THREAD_BKWORKER_BUSY;
                        ResetContext();
                        CallWarningControl(errorcode);
                        return;
                    }
                    //一切正常，可以开始scan

                    PreRead(ref errorcode);
                    if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    {
                        CallWarningControl(errorcode);
                        ResetContext();
                        return;
                    }

                    parent.db_Manager.NewSession(sflname, ref session_id, DateTime.Now.ToString());
                    dt.Clear();
                    EnterStartState();
                }
                else    //点了stop
                {
                    EnterStopState(errorcode);
                    UpdateLogDataList();
                }

                isReentrant_Run = false;
            }
            else  //重入了，需要将IsChecked属性还原
            {
                runBtn.IsChecked = !runBtn.IsChecked;
            }
        }

        private void IO_Callback(object st)
        {
            while(true)
            {
                uint errorcode = LibErrorCode.IDS_ERR_SUCCESSFUL;
                if (msg.bgworker.IsBusy == true) //bus是否正忙
                {
                    errorcode = LibErrorCode.IDS_ERR_EM_THREAD_BKWORKER_BUSY;
                    //if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    //EnterStopState(errorcode);
                }
                else
                {
                    ParamContainer pc = new ParamContainer();
                    pc.parameterlist.Add(PackC);
                    pc.parameterlist.Add(CADC);
                    errorcode = Command(pc, Convert.ToUInt16(st));

                    if (errorcode == LibErrorCode.IDS_ERR_SUCCESSFUL)
                    {
                        errorcode = ConvertHexToPhysical(pc);
                        //if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        //EnterStopState(errorcode);
                    }
                    if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    {
                        this.Dispatcher.Invoke(new EnterStopStateDelegate(EnterStopState), errorcode);
                    }
                    else
                    {
                        Dictionary<string, string> records = new Dictionary<string, string>();   //取出快照

                        records.Add("PackC", PackC.phydata.ToString());
                        records.Add("CADC", CADC.phydata.ToString());
                        records.Add("Time", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss-fff"));
                        parent.db_Manager.BeginNewRow(session_id, records);
                        this.Dispatcher.Invoke(new UpdateLogUIDelegate(UpdateLogUI));
                    }
                }
            }
        }

        private void ExportBtn_Click(object sender, RoutedEventArgs e)
        {
            string fullpath = "";
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            LogData ld = (LogData)loglist.SelectedItem;
            //records.Add("Time", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss-fff"));
            string tmp = ld.Timestamp;
            char[] skip = { ' ', '/', ':' };
            foreach (var s in skip)
            {
                tmp = tmp.Replace(s, '_');
            }
            tmp = "CurrentScan_" + tmp;
            //tmp.Remove(tmp.Length - 4);
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
                parent.db_Manager.GetOneSession(sflname, ld.Timestamp, ref dt);
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
            //dt.Clear();
            //FileInfo fi = new FileInfo(parent.folder + logname);
            return true;
        }


        void gm_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            parent.gm = (GeneralMessage)sender;
        }

        void msg_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            TASKMessage msg = sender as TASKMessage;
            switch (e.PropertyName)
            {
                case "controlreq":
                    switch (msg.controlreq)
                    {
                        case COMMON_CONTROL.COMMON_CONTROL_WARNING:
                            {
                                CallWarningControl(msg.gm);
                                /*t.Stop();
                                runBtn.IsChecked = false;
                                runBtn.Content = "Run";*/

                                break;
                            }
                    }
                    break;
            }
        }

        private void loglist_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            string headername = e.Column.Header.ToString();

            if (headername == "Timestamp")
            {
                e.Column.Width = new DataGridLength(50, DataGridLengthUnitType.Star);
                e.Column.Header = "Time";
            }
            if (headername == "RecordNumber")
            {
                e.Column.Width = new DataGridLength(50, DataGridLengthUnitType.Star);
                e.Column.Header = "Rows";
            }
        }

        private void CurrentDataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            string headername = e.Column.Header.ToString();

            if (headername == "PackC")
            {
                e.Column.Width = new DataGridLength(50, DataGridLengthUnitType.Star);
            }
            if (headername == "CADC")
            {
                e.Column.Width = new DataGridLength(50, DataGridLengthUnitType.Star);
            }
            if (headername == "Time")
            {
                e.Column.Width = new DataGridLength(50, DataGridLengthUnitType.Star);
            }
        }
        #endregion


        #region DM提供的API
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
        #endregion
    }


    public class LogData : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private string m_Timestamp;
        public string Timestamp
        {
            get { return m_Timestamp; }
            set
            {
                m_Timestamp = value;
                OnPropertyChanged("Timestamp");
            }
        }

        private long m_RecordNumber;
        public long RecordNumber
        {
            get { return m_RecordNumber; }
            set
            {
                m_RecordNumber = value;
                OnPropertyChanged("RecordNumber");
            }
        }
    }
}
