using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using O2Micro.Cobra.Common;
using System.Windows;

namespace O2Micro.Cobra.Common
{

    public class CobraLog
    {
        private ushort m_logbuflen = new ushort();      //0~65536
        public ushort logbuflen
        {
            get { return m_logbuflen; }
            set { m_logbuflen = value; }
        }       //指定每个LogData zhong de LogBuffer（内存中）能够储存的最大数据量，超过此值就需要将数据存到disk

        private string m_folder;
        public string folder
        {
            get { return m_folder; }
            set { m_folder = value; }
        }

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

        public CobraLog()  
        {
        }
        public CobraLog(string str, ushort len)  //指定foldername 和 logbuflen的构造函数
        {
            folder = str;
            logbuflen = len;
        }

        public void SyncLogData()  //同步文件夹中的log和logdatalist，在程序启动时调用
        {
            logdatalist.Clear();

            AsyncObservableCollection<FileInfo> filelist = new AsyncObservableCollection<FileInfo>();
            string[] strArray = Directory.GetFiles(folder, "*.csv");
            foreach (string str in strArray)
            {
                FileInfo fs = new FileInfo(str);
                filelist.Add(fs);
            }
            strArray = Directory.GetFiles(folder, "*.tmp");
            foreach (string str in strArray)
            {
                FileInfo fs = new FileInfo(str);
                filelist.Add(fs);
            }
            filelist.Sort(x => x.LastWriteTime);

            foreach (FileInfo fi in filelist)
            {
                LogData ld = new LogData(fi.Name, this);
                ld.logsize = fi.Length;
                logdatalist.Add(ld);
            }
        }

        private object Log_Lock = new object();

        public void NewRow(Dictionary<string, string> records)
        {
            lock (Log_Lock)
            {
                #region save data to buffer
                LogData ld = logdatalist[logdatalist.Count - 1];
                DataTable table = ld.logbuf;
                DataRow row = table.NewRow();

                List<string> columns = records.Keys.ToList<string>();
                List<string> values = records.Values.ToList<string>();
                for (int i = 0; i < records.Count; i++)
                {
                    records[columns[0]] = values[0];
                    row[columns[i]] = values[i];
                }

                table.Rows.Add(row);
                #endregion
                #region transfer buffer to harddisk
                if (table.Rows.Count >= logbuflen)
                    ld.Save2Temp();
                #endregion
            }
        }
    }

    public class LogDataBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private DataTable m_logbuf = new DataTable();
        public DataTable logbuf
        {
            get { return m_logbuf; }
            set { m_logbuf = value; }
        }

        public void BuildColumn(List<string> strlist, bool isWithTime)
        {
            DataColumn col;
            foreach (string str in strlist)
            {
                col = new DataColumn();
                if (str == "Time")
                    col.DataType = System.Type.GetType("System.DateTime");
                else
                    col.DataType = System.Type.GetType("System.String");
                col.ColumnName = str;
                col.AutoIncrement = false;
                col.Caption = str;
                col.ReadOnly = false;
                col.Unique = false;
                logbuf.Columns.Add(col);
            }
            if (isWithTime)
            {
                col = new DataColumn();
                col.DataType = System.Type.GetType("System.DateTime");
                col.ColumnName = "Time";
                col.AutoIncrement = false;
                col.Caption = "Time";
                col.ReadOnly = false;
                col.Unique = false;
                logbuf.Columns.Add(col);
            }
        }
    }

    public class LogData : LogDataBase
    {
        private CobraLog m_parent;
        public CobraLog parent
        {
            get { return m_parent; }
            set { m_parent = value; }
        }

        private string m_logname;
        public string logname
        {
            get { return m_logname; }
            set
            {
                m_logname = value;
                OnPropertyChanged("logname");
            }
        }

        private long m_logsize;
        public long logsize
        {
            get { return m_logsize; }
            set
            {
                m_logsize = value;
                OnPropertyChanged("logsize");
            }
        }

        private bool m_haveheader = false;
        public bool haveheader
        {
            get { return m_haveheader; }
            set { m_haveheader = value; }
        }

        private bool m_complete = false;
        private bool isCompleted
        {
            get { return m_complete; }
            set
            {
                m_complete = value;
                OnPropertyChanged("complete");
            }
        }
        public LogData(string str, CobraLog p)
        {
            logname = str;
            parent = p;
        }

        private object logfile_lock = new object();

        public bool Save2Temp() //Save buffer content to hard disk as temperary file, then clear buffer
        {
            lock (logfile_lock)
            {
                //logbuf.Clear();
                //return true;
                FileStream file = new FileStream(parent.folder + logname, FileMode.Append);
                StreamWriter sw = new StreamWriter(file);
                int length;
                string str = "";
                if (!haveheader)
                {
                    foreach (DataColumn col in logbuf.Columns)
                    {
                        str += col.ColumnName + ",";
                    }
                    length = str.Length;
                    str = str.Remove(length - 1);
                    sw.WriteLine(str);
                    haveheader = true;
                }

                foreach (DataRow row in logbuf.Rows)
                {
                    str = "";
                    foreach (DataColumn col in logbuf.Columns)
                    {
                        if (col.ColumnName == "Time")
                        {
                            DateTime dt = (DateTime)row["Time"];
                            str += dt.ToString("HH:mm:ss:fff") + ",";
                        }
                        else
                            str += row[col.ColumnName] + ",";
                    }
                    length = str.Length;
                    str = str.Remove(length - 1);
                    sw.WriteLine(str);
                }
                sw.Close();
                file.Close();
                logbuf.Clear();
                FileInfo fi = new FileInfo(parent.folder + logname);
                logsize = fi.Length;
                return true;
            }
        }


        public void Complete()  //将logdata打上正常完成标记，且转化成最终格式
        {
            //file = new FileStream(filename, FileMode.Open);
            //string str = logname;
            //str = str.Remove(str.Length - 4) + ".bak";
            string str = logname.Remove(logname.Length - 4);
            File.Move(parent.folder + logname, parent.folder + str);
            logname = str;
            isCompleted = true;
        }

        public void Delete()  //将logdata删除
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            ret = SharedAPI.FileIsOpen(parent.folder + logname);
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                string str = LibErrorCode.GetErrorDescription(ret);
                MessageBox.Show(str);
                return;
            }

            File.Delete(parent.folder + logname);
            parent.logdatalist.Remove(this);
        }
    }

    public class LogUIData : LogDataBase
    {
        protected List<string> GetCSVStrList(string strline)  //Load cvs file line to string list
        {
            string[] strArray = strline.Split(',');
            List<string> strlist = new List<string>();
            foreach (string str in strArray)
                strlist.Add(str);
            return strlist;
        }

        protected void AddRow(List<string> strlist)   //transfer string list to DataRow, add row to DataTable
        {
            DataTable table = logbuf;
            DataRow row = table.NewRow();
            int i = 0;
            foreach (DataColumn col in logbuf.Columns)
            {
                if (col.ColumnName == "Time")   //HH:mm:ss:fff
                {
                    row["Time"] = DateTime.ParseExact(strlist[i], "HH:mm:ss:fff", null);
                }
                else
                    row[col.ColumnName] = strlist[i];
                i++;
            }
            table.Rows.Add(row);
        }
        public void LoadFromFile(string filename)   //Load cvs file to data table
        {
            logbuf.Clear();
            logbuf.Columns.Clear();

            FileStream file = new FileStream(filename, FileMode.Open);
            if (file.Length < 1000)
            {
                StreamReader sr = new StreamReader(file);
                List<string> strlist = new List<string>();
                strlist = GetCSVStrList(sr.ReadLine());
                BuildColumn(strlist, false);
                string strlin;
                while ((strlin = sr.ReadLine()) != null)
                {
                    strlist = GetCSVStrList(strlin);
                    AddRow(strlist);
                }
                sr.Close();
                file.Close();
            }
            else if (file.Length < 2000)
            {
                StreamReader sr = new StreamReader(file);
                List<string> strlist = new List<string>();
                strlist = GetCSVStrList(sr.ReadLine());
                BuildColumn(strlist, false);
                string buffer = sr.ReadToEnd();
                string[] linelist = buffer.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in linelist)
                {
                    if (line == "")
                        continue;
                    strlist = GetCSVStrList(line);
                    AddRow(strlist);
                }
                sr.Close();
                file.Close();
            }
            else
            {
                file.Close();
                List<string> strlist = new List<string>();
                IEnumerable<string> linelist = File.ReadAllLines(filename);
                bool isFirst = true;
                foreach (string line in linelist)
                {
                    strlist = GetCSVStrList(line);
                    if (isFirst)
                    {
                        BuildColumn(strlist, false);
                        isFirst = false;
                        continue;
                    }
                    AddRow(strlist);
                }
            }
        }
    }

    public enum AutomationTestLogHeader 
    {
        CADDR,
        VALUE,
        ERRORTYPE,
        CID,
        GUID,
        NICKNAME,
        ERRORCODE,
        DESCRIPTION,
        API,
        REGADDR,
        PVALUE,
        HVALUE,
    }

    public class AutomationTestLog
    {
        static public string[] LogHeaders = { "CADDR", "VALUE", "ERRORTYPE", "CID", "GUID", "NICKNAME", "ERRORCODE", "DESCRIPTION", "API", "REGADDR", "PVALUE", "HVALUE" };

        static private Dictionary<string, string> m_newrow = new Dictionary<string, string>();
        static public Dictionary<string, string> newrow
        {
            get { return m_newrow; }
            set
            {
                m_newrow = value;
            }
        }

        private void Init()  //指定foldername 和 logbuflen的构造函数
        {
            foreach (string log in AutomationTestLog.LogHeaders)
            {
                newrow[log] = "";
            }
        }

        static public CobraLog cl;

        static private LogUIData m_logUIdata = new LogUIData();
        static public LogUIData logUIdata
        {
            get { return m_logUIdata; }
            set { m_logUIdata = value; }
        }

        static public void CreateLogFile(string str)
        {
            #region new logdata
            LogData ld = new LogData(str, cl);
            //根据dynamicdatalist中的数据来初始化DataTable的column
            List<string> strlist = new List<string>();
            foreach (string logHeader in LogHeaders)
            {
                strlist.Add(logHeader);
            }
            ld.BuildColumn(strlist, true);
            cl.logdatalist.Add(ld);
            //logfilelist.ScrollIntoView(logfilelist.Items[logfilelist.Items.Count - 1]); //scroll to the last item
            #endregion 

            #region 根据TestLog.LogHeaders中的数据来初始化DataTable的column以及isDisplay
            /*strlist = new List<string>();
            foreach (string logHeader in AutomationTestLog.LogHeaders)
            {
                strlist.Add(logHeader);
            }*/
            logUIdata.logbuf.Clear();   //Clear all the history data before the scan start.
            logUIdata.logbuf.Columns.Clear();
            logUIdata.BuildColumn(strlist, true);
            #endregion
        }
        static public void CompleteLogFile()
        {                 
            #region 储存剩余数据到logdata, logdatalist中的最后一个，即为当前的logdata
            LogData lg = cl.logdatalist[cl.logdatalist.Count - 1];
            lg.Save2Temp();
            lg.Complete();
            #endregion
        }
        static public void AddOneRow()
        {
            #region save data to buffer
            LogData ld = cl.logdatalist[cl.logdatalist.Count - 1];
            DataTable table = ld.logbuf;

            DataRow row = table.NewRow();
            foreach (string lh in LogHeaders)
            {
                row[lh] = newrow[lh];
            }
            row["Time"] = DateTime.Now;
            table.Rows.Add(row);
            #endregion
            #region transfer buffer to harddisk
            if (table.Rows.Count >= cl.logbuflen)
                ld.Save2Temp();
            #endregion

            row = logUIdata.logbuf.NewRow();
            foreach (string lh in LogHeaders)
            {
                row[lh] = newrow[lh];
            }
            row["Time"] = DateTime.Now;
            if (logUIdata.logbuf.Rows.Count >= 10000)
                logUIdata.logbuf.Rows.RemoveAt(0);
            logUIdata.logbuf.Rows.Add(row);
        }
    }

    public class CommunicationLog
    {
        public static string[] strColHeader = { "DayTime", "CID", "ErrCode", "R/W", "I2CAddr", "RegIndex", "Data1", "Data2", "Data3", "ErrComments" };
        private static List<string> lststrComLogCol = new List<string>();
        public static string strFileStamp;
        public static string strFullLogFoler;
        public static CobraLog clCommLog = null;
        public static UInt32 uCounting;

        public static void Init()
        {
            lststrComLogCol.Clear();
            foreach (string strHead in strColHeader)
            {
                lststrComLogCol.Add(strHead);
            }
            uCounting = 0;
        }

        //will be used in Device.cs
        public static bool CreateComLogFile(string strIn = null)
        {
            bool bReturn = true;
            DateTime dtimeFile = DateTime.Now;
            string strTmpToWrite = string.Empty;

            //strFullLogFoler = Path.Combine(FolderMap.m_logs_folder, "CommLay\\");
            strFullLogFoler = Path.Combine(FolderMap.m_currentproj_folder, "Communication\\");
            /*
            if (!Directory.Exists(strFullLogFoler))
                Directory.CreateDirectory(strFullLogFoler);
             */
            bReturn = FolderMap.CreateFolder(strFullLogFoler);
            if (!bReturn) return bReturn;
            if (clCommLog == null)
            clCommLog = new CobraLog(strFullLogFoler, 0);
            else
                clCommLog.folder = strFullLogFoler;
            clCommLog.SyncLogData();        //convert *.tmp to *.csv

            if (strIn != null)
            {
                //strTmpToWrite = string.Format("ATMlog{0}.csv", dtimeFile.ToString("s"));
                strTmpToWrite = strIn;
                strTmpToWrite = strTmpToWrite.Replace("Log", "COM");
            }
            else
            {
                strTmpToWrite = string.Format("CommunicationDatalog{0}.csv", dtimeFile.ToString("s"));
                strTmpToWrite = strTmpToWrite.Replace(':', '-');
                strTmpToWrite = strTmpToWrite + ".tmp";
            }
            strFileStamp = strTmpToWrite;

            //build column
            //strFileStamp = strTmpToWrite;
            LogData ldTmp = new LogData(strFileStamp, clCommLog);

            ldTmp.BuildColumn(lststrComLogCol, false);
            clCommLog.logdatalist.Add(ldTmp);
            return bReturn;
        }

        public static void CompleteComLogFile()
        {
            if ((clCommLog != null) && (clCommLog.logdatalist.Count > 0))
            {
                LogData ldTmp = clCommLog.logdatalist[clCommLog.logdatalist.Count - 1];
                ldTmp.Save2Temp();
                ldTmp.Complete();
            }
        }

    }
}
