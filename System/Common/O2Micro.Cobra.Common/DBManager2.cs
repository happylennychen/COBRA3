using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace O2Micro.Cobra.Common
{
    public static class DBManager2
    {
        public enum DataType
        {
            INTERGER,
            FLOAT,
            TEXT,
        }

        public static bool DebugMode = false;       //为true时，每次启动都会在CobraDocument/Database/DebugDB创建新的临时DB
        private static string DBName = "CobraDBv2.0.db3";
        private static string TableName = "COBRA_TABLE";
        private static string DBpath = "";
        private static object DB_Lock = new object();
        //From Guo.zhu
        private static Dictionary<string, string> m_record = new Dictionary<string, string>();

        #region 数据库中与当前工程或进程关联的某系数据
        private static string Project_Name = "";
        #endregion

        #region 内存缓冲区
        private static List<string> sqls = new List<string>();
        private static System.Timers.Timer t = new System.Timers.Timer();
        private static bool isTimerBooked = false;
        private static byte idle_cnt = 0;
        #endregion

        private static void t_Elapsed(object sender, EventArgs e)
        {
            lock (DB_Lock)
            {
                if (sqls.Count == 0)
                {
                    if (idle_cnt >= 3)
                    {
                        t.Stop();
                        idle_cnt = 0;
                    }
                    else
                        idle_cnt++;
                }
                else
                {
                    SQLiteResult sret = SQLiteDriver.ExecuteNonQueryTransaction(sqls);
                    if (sret.I == -1)
                    {
                        //todo: add warning here
                        MessageBox.Show(sret.Str);
                    }
                    else
                        sqls.Clear();
                }
            }
        }

        #region API
        //return: succuss 0, failed -1
        public static Int32 CobraDBInit(string folder)
        {
            lock (DB_Lock)
            {
                if (!isTimerBooked)
                {
                    t.Elapsed += new System.Timers.ElapsedEventHandler(t_Elapsed);
                    isTimerBooked = true;
                }
                //UInt32 errorcode = LibErrorCode.IDS_ERR_SUCCESSFUL;
                if (!Directory.Exists(folder + "Database"))
                    Directory.CreateDirectory(folder + "Database");
                if (DebugMode)
                {
                    if (!Directory.Exists(folder + "Database\\DebugDB"))
                        Directory.CreateDirectory(folder + "Database\\DebugDB");
                    string[] strlist = DBName.Split('.');
                    string DebugDBName = strlist[0] + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + "." + strlist[1];

                    DBpath = folder + "Database\\DebugDB\\";   //DebugDB folder, name with timestamp
                    DBName = DebugDBName;
                    //MessageBox.Show("DebugMode for DB is enabled! Use DebugDB folder to save db files, add timestamp to file name.");
                }
                else
                {
                    DBpath = folder + "Database\\";
                }

                SQLiteDriver.DB_Name = DBName;
                SQLiteDriver.DB_Path = DBpath;
                //List<string> sqls = new List<string>();
                //sqls.Add("CREATE TABLE IF NOT EXISTS Products(product_id INTEGER PRIMARY KEY, name VARCHAR(30) NOT NULL, version VARCHAR(30) NOT NULL, UNIQUE(name, version));");
                //sqls.Add("CREATE TABLE IF NOT EXISTS NameAlias(orig_name VARCHAR(30) NOT NULL, name_alias VARCHAR(30) NOT NULL, UNIQUE(orig_name, name_alias));");
                //sqls.Add("CREATE TABLE IF NOT EXISTS VersionAlias(product_id INTEGER NOT NULL, version_alias VARCHAR(30) NOT NULL, UNIQUE(product_id, version_alias));");
                //sqls.Add("CREATE TABLE IF NOT EXISTS Projects(project_id INTEGER PRIMARY KEY, product_id INTEGER NOT NULL, user_type TEXT NOT NULL, date TEXT NOT NULL, bus_type TEXT NOT NULL, UNIQUE(product_id, user_type, date));");
                //sqls.Add("CREATE TABLE IF NOT EXISTS Modules(module_id INTEGER PRIMARY KEY, module_name VARCHAR(30) NOT NULL, UNIQUE(module_name));");
                //sqls.Add("CREATE TABLE IF NOT EXISTS TableTypes(table_type INTEGER PRIMARY KEY, project_id INTEGER NOT NULL, module_id INTEGER NOT NULL, UNIQUE(project_id, module_id));");
                //sqls.Add("CREATE TABLE IF NOT EXISTS Logs(log_id INTEGER PRIMARY KEY, table_type INTEGER NOT NULL, log_info VARCHAR(30), timestamp VARCHAR(17) NOT NULL, device_num VARCHAR(10));");//Issue1406 Leon
                //sqls.Add("CREATE TABLE IF NOT EXISTS Bus_I2C(project_id INTEGER, device_id INTEGER, frequency INTEGER NOT NULL, address INTEGER NOT NULL, pec_enable BOOLEAN NOT NULL, UNIQUE(project_id, device_id));");
                string sql = ("CREATE TABLE IF NOT EXISTS " + TableName + "(project_name VARCHAR(30) NOT NULL, module_name VARCHAR(30) NOT NULL, session_establish_time VARCHAR(17), device_index VARCHAR(10), data_set VARCHAR(500) NOT NULL);");
                //todo: Bus_SPI Bus_I2C2 Bus_???
                int row = -1;
                SQLiteResult sret = SQLiteDriver.ExecuteNonQuery(sql, ref row);
                if (sret.I != 0)
                {
                    //todo: add warning here
                    //MessageBox.Show(sret.Str);
                }
                return sret.I;
            }
        }
        public static void ExtensionRegister(string project_name)
        {
            lock (DB_Lock)
            {
                Project_Name = project_name;
            }
        }
        /*public static Int32 NewRow(string module_name, Dictionary<string, string> data_dictionary, string device_index = "", string session_establish_time = "")
        {
            lock (DB_Lock)
            {
                Dictionary<string, string> record = new Dictionary<string, string>();
                record.Add("project_name", Project_Name);
                record.Add("module_name", module_name);
                if (device_index != "")
                    record.Add("device_index", device_index);
                if (session_establish_time != "")
                    record.Add("session_establish_time", session_establish_time);

                string data_dictionary_string = "";
                foreach (string key in data_dictionary.Keys)
                {
                    data_dictionary_string += (key +"|"+ data_dictionary[key]+",");
                }
                record.Add("data_set", data_dictionary_string);
                int row = -1;
                SQLiteResult sret = SQLiteDriver.DBInsertInto(TableName, record, ref row);
                if (sret.I != 0)
                {
                    //todo: add warning here
                    //MessageBox.Show(sret.Str);
                }
                return sret.I;
            }
        }*/
        public static Int32 BeginNewRow(string module_name, Dictionary<string, string> data_dictionary, string device_index = "", string session_establish_time = "")
        {
            lock (DB_Lock)
            {
                    if (!t.Enabled)
                    {
                        t.Interval = 1000;
                        t.Start();
                    }

                    Dictionary<string, string> record = new Dictionary<string, string>();
                    record.Add("project_name", Project_Name);
                    record.Add("module_name", module_name);
                    if (device_index != "")
                        record.Add("device_index", device_index);
                    if (session_establish_time != "")
                        record.Add("session_establish_time", session_establish_time);

                    string data_dictionary_string = "";
                    foreach (string key in data_dictionary.Keys)
                    {
                        data_dictionary_string += (key + "|" + data_dictionary[key] + ",");
                    }
                    record.Add("data_set", data_dictionary_string);

                    string sql = SQLiteDriver.SQLInsertInto(TableName, record);
                    sqls.Add(sql);
                    return 0;
            }
        }
        public static Int32 BeginNewRow(string module_name, string data_normal, string device_index = "", string session_establish_time = "")
        {
            lock (DB_Lock)
            {
                if (!t.Enabled)
                {
                    t.Interval = 1000;
                    t.Start();
                }

                Dictionary<string, string> record = new Dictionary<string, string>();
                record.Add("project_name", Project_Name);
                record.Add("module_name", module_name);
                if (device_index != "")
                    record.Add("device_index", device_index);
                if (session_establish_time != "")
                    record.Add("session_establish_time", session_establish_time);

                record.Add("data_set", data_normal);

                string sql = SQLiteDriver.SQLInsertInto(TableName, record);
                sqls.Add(sql);
                return 0;
            }
        }
        public static Int32 ExecuteQuery(string sql, ref DataTable dt, ref int row)
        {
            lock (DB_Lock)
            {
                SQLiteResult sret = SQLiteDriver.ExecuteSelect(sql, ref dt, ref row);
                return sret.I;
            }
        }
        public static Int32 GetRows(string module_name, ref DataTable dt, string session_establish_time = "", string device_index = "")
        {
            lock (DB_Lock)
            {
                SQLiteResult sret;

                Dictionary<string, string> conditions = new Dictionary<string, string>();
                conditions.Add("module_name", module_name);
                if (session_establish_time != "")
                    conditions.Add("session_establish_time", session_establish_time);
                if (device_index != "")
                    conditions.Add("device_index", device_index);

                int row = -1;
                sret = SQLiteDriver.DBSelect(TableName, conditions, null, ref dt, ref row);
                return sret.I;
            }
        }
        
#region For Scan SFL
        
        public static Int32 ScanSFLGetSessionsInfor(string module_name, ref List<List<string>> records)
        {
            lock (DB_Lock)
            {
                SQLiteResult sret;
                string sql = "select device_index, session_establish_time, count(*) from COBRA_TABLE where module_name = '" + module_name + "' and project_name = '"+Project_Name+"' group by session_establish_time ;";
                DataTable dt = new DataTable();
                int row = -1;
                sret = SQLiteDriver.ExecuteSelect(sql, ref dt, ref row);
                if (sret.I == -1)
                    return sret.I;
                foreach (DataRow r in dt.Rows)
                {
                    List<string> item = new List<string>();
                    item.Add(r["session_establish_time"].ToString());
                    item.Add(r["count(*)"].ToString());
                    item.Add(r["device_index"].ToString());
                    records.Add(item);
                }
                return sret.I;
            }
        }
        public static Int32 ScanSFLDeleteOneSession(string module_name, string session_establish_time)
        {
            lock (DB_Lock)
            {
                SQLiteResult sret;

                Dictionary<string, string> conditions = new Dictionary<string, string>();
                conditions.Add("project_name", Project_Name);
                conditions.Add("module_name", module_name);
                conditions.Add("session_establish_time", session_establish_time);
                int row = -1;
                sret = SQLiteDriver.DBDelete(TableName, conditions, ref row);
                return sret.I;
            }
        }
        public static Int32 ScanSFLGetOneSession(string module_name, string session_establish_time, ref DataTable dt)
        {
            lock (DB_Lock)
            {
                SQLiteResult sret;

                Dictionary<string, string> conditions = new Dictionary<string, string>();
                conditions.Add("project_name", Project_Name);
                conditions.Add("module_name", module_name);
                conditions.Add("session_establish_time", session_establish_time);

                List<string> datacolumns = new List<string>();
                datacolumns.Add("data_set");
                int row = -1;
                DataTable dtTemp = new DataTable();
                sret = SQLiteDriver.DBSelect(TableName, conditions, datacolumns, ref dtTemp, ref row);
                if(sret.I!=0)
                    return sret.I;
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
                return 0;
            }
        }
#endregion

        #endregion
    }
}
