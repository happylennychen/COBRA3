using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Collections;
using System.Timers;
using Cobra.Common;

namespace Cobra.Common
{
    public class DBManage
    {        //父对象保存
        private BusOptions m_parent;
        public BusOptions parent
        {
            get { return m_parent; }
            set { m_parent = value; }
        }
        public enum DataType
        {
            INTERGER,
            FLOAT,
            TEXT,
        }

        private int device_index = 0;
        private object DB_Lock = new object();
        private const string DeviceTableName = "DEVICE_TABLE";
        private const string DataTableName = "DATA_TABLE";
        private const string SessionTableName = "SESSION_TABLE";
        private string Project_Name
        {
            get { return COBRA_GLOBAL.CurrentOCEName; }
        }
        private byte idle_cnt = 0;
        private const int FlushInterval = 15000;
        private const int FlushIdleCount = 3;
        private Timer flush_timer = new Timer();
        private List<string> sqls_buffer = new List<string>();

        public DBManage(object parent)
        {
            m_parent = parent as BusOptions;
        }
        public void Init()
        {
            lock (DB_Lock)
            {
                device_index = m_parent.DeviceIndex;
                sqls_buffer.Clear();
                if (!Directory.Exists(SQLiteDriver.DB_Path))
                    Directory.CreateDirectory(SQLiteDriver.DB_Path);
                flush_timer.Elapsed += new ElapsedEventHandler(tFlushDB_Elapsed);

                if (!File.Exists(Path.Combine(SQLiteDriver.DB_Path, SQLiteDriver.DB_Name)))
                {
                    sqls_buffer.Add("CREATE TABLE IF NOT EXISTS " + DeviceTableName + " (project_id INTEGER PRIMARY KEY,device_index VARCHAR(10),project_name VARCHAR(30) NOT NULL,UNIQUE(device_index,project_name));");//Issue1406 Leon
                    sqls_buffer.Add("CREATE TABLE IF NOT EXISTS " + SessionTableName + " (session_id INTEGER PRIMARY KEY,project_id INTEGER NOT NULL, module_name VARCHAR(30) NOT NULL,row_number VARCHAR(10) DEFAULT 0,session_establish_time VARCHAR(17) NOT NULL, UNIQUE(project_id,module_name,session_establish_time));");//Issue1406 Leon
                    sqls_buffer.Add("CREATE TABLE IF NOT EXISTS " + DataTableName + " (data_id INTEGER PRIMARY KEY, session_id INTEGER NOT NULL, data_set VARCHAR(500) NOT NULL);");
                    SQLiteDriver.ExecuteNonQueryTransaction(sqls_buffer);
                    sqls_buffer.Clear();
                }
            }
        }

        private void tFlushDB_Elapsed(object sender, EventArgs e)
        {
            try
            {
                lock (DB_Lock)
                {
                    if (sqls_buffer.Count == 0)
                    {
                        if (idle_cnt >= FlushIdleCount)
                        {
                            flush_timer.Stop();
                            idle_cnt = 0;
                        }
                        else
                            idle_cnt++;
                    }
                    else
                    {
                        SQLiteDriver.ExecuteNonQueryTransaction(sqls_buffer);
                        sqls_buffer.Clear();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Flush DB failed\n", ex);
            }
        }
        private void GetProjectIDFromDeviceTable(string project_name, ref int project_id)
        {
            List<string> datacolumns = new List<string>();
            datacolumns.Add("project_id");

            Dictionary<string, string> conditions = new Dictionary<string, string>();
            conditions.Add("project_name", Project_Name);
            conditions.Add("device_index", device_index.ToString());
            DataTable dt = new DataTable();
            int row = -1;
            SQLiteDriver.DBSelect(DeviceTableName, conditions, datacolumns, ref dt, ref row);
            if (dt.Rows.Count == 0)
            {
                project_id = -1;
            }
            else
            {
                project_id = Convert.ToInt32(dt.Rows[0]["project_id"]);
            }
        }
        private void GetSessionIDFromSessionTable(int project_id, string module_name, ref int session_id, string session_establish_time = "")
        {
            List<string> datacolumns = new List<string>();
            datacolumns.Add("session_id");

            Dictionary<string, string> conditions = new Dictionary<string, string>();
            conditions.Add("project_id", project_id.ToString());
            conditions.Add("module_name", module_name);
            if (session_establish_time != "")
                conditions.Add("session_establish_time", session_establish_time);
            DataTable dt = new DataTable();
            int row = -1;
            SQLiteDriver.DBSelect(SessionTableName, conditions, datacolumns, ref dt, ref row);
            if (dt.Rows.Count == 0)
            {
                session_id = -1;
                throw new Exception("Get Session ID failed!\n");
            }
            else
            {
                session_id = Convert.ToInt32(dt.Rows[0]["session_id"]);
            }
        }

        #region API
        public void NewSession(string module_name, ref int session_id, string session_establish_time = "")
        {
            int row = -1;
            Dictionary<string, string> record = new Dictionary<string, string>();
            try
            {
                lock (DB_Lock)
                {
                    int project_id = -1;
                    record.Add("device_index", device_index.ToString());
                    record.Add("project_name", Project_Name);
                    SQLiteDriver.DBInsertInto(DeviceTableName, record, ref row);
                    GetProjectIDFromDeviceTable(Project_Name, ref project_id);
                    if (project_id == -1) return;
                    record.Clear();
                    record.Add("project_id", project_id.ToString());
                    record.Add("module_name", module_name);
                    record.Add("session_establish_time", session_establish_time);
                    SQLiteDriver.DBInsertInto(SessionTableName, record, ref row);
                    GetSessionIDFromSessionTable(project_id, module_name, ref session_id, session_establish_time);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("New session failed\n", ex);
            }
        }
        public void BeginNewRow(int session_id, Dictionary<string, string> data_dictionary)
        {
            string sql = "";
            StringBuilder strBuilder = new StringBuilder();
            Dictionary<string, string> record = new Dictionary<string, string>();
            try
            {
                lock (DB_Lock)
                {
                    if (!flush_timer.Enabled)
                    {
                        flush_timer.Interval = FlushInterval;
                        flush_timer.Start();
                    }
                    record.Add("session_id", session_id.ToString());
                    foreach (string key in data_dictionary.Keys)
                        strBuilder.Append(key + "|" + data_dictionary[key] + ",");
                    record.Add("data_set", strBuilder.ToString());
                    sql = SQLiteDriver.SQLInsertInto(DataTableName, record);
                    sqls_buffer.Add(sql);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("New Row failed\n", ex);
            }
        }
        public void BeginNewRow(int session_id, string data_normal)
        {
            Dictionary<string, string> record = new Dictionary<string, string>();
            try
            {
                lock (DB_Lock)
                {
                    if (!flush_timer.Enabled)
                    {
                        flush_timer.Interval = 1000;
                        flush_timer.Start();
                    }
                    record.Add("session_id", session_id.ToString());
                    record.Add("data_set", data_normal);

                    string sql = SQLiteDriver.SQLInsertInto(DataTableName, record);
                    sqls_buffer.Add(sql);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("New Row failed\n", ex);
            }
        }
        public void UpdateSessionSize(int session_id, ulong session_row_number)
        {
            try
            {
                lock (DB_Lock)
                {
                    if (sqls_buffer.Count != 0)
                    {
                        SQLiteDriver.ExecuteNonQueryTransaction(sqls_buffer);
                        sqls_buffer.Clear();
                    }
                    string sql = "UPDATE " + SessionTableName + " SET row_number = " + session_row_number.ToString() + " WHERE session_id = " + session_id.ToString() + ";";
                    int row = -1;
                    SQLiteDriver.ExecuteNonQuery(sql, ref row);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Scan SFL Get Sessions Infor failed\n", ex);
                //MessageBox.Show("Scan SFL Get Sessions Infor failed\n" + ex.Message);
            }
        }
        public void GetSessionsInfor(string module_name, ref List<List<string>> records)
        {
            try
            {
                lock (DB_Lock)
                {
                    int project_id = -1;
                    if (sqls_buffer.Count != 0)
                    {
                        SQLiteDriver.ExecuteNonQueryTransaction(sqls_buffer);
                        sqls_buffer.Clear();
                    }

                    GetProjectIDFromDeviceTable(Project_Name, ref project_id);
                    if (project_id == -1) return;
                    Dictionary<string, string> conditions = new Dictionary<string, string>();
                    conditions.Add("project_id", project_id.ToString());
                    conditions.Add("module_name", module_name);
                    List<string> datacolumns = new List<string>();
                    datacolumns.Add("session_id");
                    datacolumns.Add("session_establish_time");
                    datacolumns.Add("row_number");

                    int row = -1;
                    List<List<string>> datavalues = new List<List<string>>();
                    SQLiteDriver.DBSelect(SessionTableName, conditions, datacolumns, ref datavalues, ref row);
                    foreach (var datavalue in datavalues)
                    {
                        int session_id = Convert.ToInt32(datavalue[0]);
                        string timestamp = datavalue[1];
                        string session_size = datavalue[2];
                        List<string> item = new List<string>();
                        item.Add(timestamp);
                        item.Add(session_size);
                        item.Add(device_index.ToString());
                        records.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Scan SFL Get Sessions Infor failed\n", ex);
                //MessageBox.Show("Scan SFL Get Sessions Infor failed\n" + ex.Message);
            }
        }
        public void DeleteOneSession(string module_name, string session_establish_time)
        {
            try
            {
                Dictionary<string, string> record = new Dictionary<string, string>();
                lock (DB_Lock)
                {
                    int session_id = -1;
                    int project_id = -1;
                    GetProjectIDFromDeviceTable(Project_Name, ref project_id);
                    if (project_id == -1) return;
                    GetSessionIDFromSessionTable(project_id, module_name, ref session_id, session_establish_time);

                    Dictionary<string, string> conditions = new Dictionary<string, string>();
                    conditions.Add("session_id", session_id.ToString());
                    int row = -1;
                    SQLiteDriver.DBDelete(DataTableName, conditions, ref row);
                    SQLiteDriver.DBDelete(SessionTableName, conditions, ref row);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Scan SFL Delete Session failed\n", ex);
                //MessageBox.Show("Scan SFL Delete Session failed\n" + ex.Message);
            }
        }
        public void GetOneSession(string module_name, string session_establish_time, ref DataTable dt)
        {
            try
            {
                Dictionary<string, string> record = new Dictionary<string, string>();
                lock (DB_Lock)
                {
                    int session_id = -1;
                    int project_id = -1;
                    GetProjectIDFromDeviceTable(Project_Name, ref project_id);
                    if (project_id == -1) return;
                    GetSessionIDFromSessionTable(project_id, module_name, ref session_id, session_establish_time);

                    Dictionary<string, string> conditions = new Dictionary<string, string>();
                    conditions.Add("session_id", session_id.ToString());

                    List<string> datacolumns = new List<string>();
                    datacolumns.Add("data_set");
                    int row = -1;
                    DataTable dtTemp = new DataTable();
                    SQLiteDriver.DBSelect(DataTableName, conditions, datacolumns, ref dtTemp, ref row);
                    string dr0string = dtTemp.Rows[0]["data_set"].ToString();
                    string[] dr0items = dr0string.Split(',');
                    foreach (var dr0item in dr0items)
                    {
                        if (dr0item != "")
                        {
                            string[] s = dr0item.Split('|');
                            string col = s[0];
                            dt.Columns.Add(col);
                        }
                    }
                    foreach (DataRow dr in dtTemp.Rows)
                    {
                        string drstring = dr["data_set"].ToString();
                        string[] dritems = drstring.Split(',');
                        DataRow newdr = dt.NewRow();
                        foreach (var dritem in dritems)
                        {
                            if (dritem != "")
                            {
                                string[] s = dritem.Split('|');
                                string col = s[0];
                                newdr[s[0]] = s[1];
                            }
                        }
                        dt.Rows.Add(newdr);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Scan SFL Get One Session failed\n", ex);
                //MessageBox.Show("Scan SFL Get One Session failed\n" + ex.Message);
            }
        }
        #endregion
    }
}
