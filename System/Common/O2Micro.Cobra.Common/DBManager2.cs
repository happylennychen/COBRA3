﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Threading;

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
        private static string DBName = "CobraDBv2.db3";
        private const string DataTableName = "DATA_TABLE";
        private const string SessionTableName = "SESSION_TABLE";
        private const int FlushInterval = 1000;
        private const int FlushIdleCount = 3;
        private static string DBpath = "";
        private static object DB_Lock = new object();

        #region 数据库中与当前工程或进程关联的某系数据
        private static string Project_Name = "";
        #endregion

        #region 内存缓冲区
        private static List<string> sqls_buffer = new List<string>();
        private static System.Timers.Timer flush_timer = new System.Timers.Timer();
        private static bool isTimerBooked = false;
        private static byte idle_cnt = 0;
        #endregion

        private static void tFlushDB_Elapsed(object sender, EventArgs e)
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
                        SQLiteDriver2.ExecuteNonQueryTransaction(sqls_buffer);
                        sqls_buffer.Clear();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Flush DB failed\n", ex);
                //MessageBox.Show("Flush DB failed\n" + ex.Message);
            }
        }

        private static void GetSessionIDFromSessionTable(string module_name, ref int session_id, string device_index = "", string session_establish_time = "")
        {
            List<string> datacolumns = new List<string>();
            datacolumns.Add("session_id");

            Dictionary<string, string> conditions = new Dictionary<string, string>();
            conditions.Add("project_name", Project_Name);
            conditions.Add("module_name", module_name);
            if (session_establish_time != "")
                conditions.Add("session_establish_time", session_establish_time);
            if (device_index != "")
                conditions.Add("device_index", device_index);
            DataTable dt = new DataTable();
            int row = -1;
            SQLiteDriver2.DBSelect(SessionTableName, conditions, datacolumns, ref dt, ref row);
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
        private static void GetSessionSize(int session_id, ref int session_size)
        {
            string sql = "select count(*) from " + DataTableName + " where session_id = " + session_id.ToString() + ";";
            DataTable dt = new DataTable();
            int row = -1;
            SQLiteDriver2.ExecuteSelect(sql, ref dt, ref row);
            session_size = Convert.ToInt32(dt.Rows[0]["count(*)"]);
        }

        #region API
        public static void CobraDBInit(string folder)
        {
            try
            {
                lock (DB_Lock)
                {
                    if (!isTimerBooked)
                    {
                        flush_timer.Elapsed += new System.Timers.ElapsedEventHandler(tFlushDB_Elapsed);
                        isTimerBooked = true;
                    }
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

                    SQLiteDriver2.DB_Name = DBName;
                    SQLiteDriver2.DB_Path = DBpath;
                    if (!File.Exists(SQLiteDriver2.DB_Path + SQLiteDriver2.DB_Name))
                    {
                        List<string> sqls = new List<string>();
                        sqls.Add("CREATE TABLE IF NOT EXISTS " + SessionTableName + " (session_id INTEGER PRIMARY KEY, project_name VARCHAR(30) NOT NULL, module_name VARCHAR(30) NOT NULL, session_establish_time VARCHAR(17) NOT NULL, device_index VARCHAR(10), UNIQUE(project_name, module_name, session_establish_time));");//Issue1406 Leon
                        sqls.Add("CREATE TABLE IF NOT EXISTS " + DataTableName + " (data_id INTEGER PRIMARY KEY, session_id INTEGER NOT NULL, data_set VARCHAR(500) NOT NULL);");
                        //todo: Bus_SPI Bus_I2C2 Bus_???
                        //int row = -1;
                        SQLiteDriver2.ExecuteNonQueryTransaction(sqls);
                        sqls.Clear();
                    }
                    string OLD_DB_NAME = "CobraDB.db3";
                    string NEW_DB_NAME = "CobraDBv2.db3";
                    if (File.Exists(SQLiteDriver.DB_Path + OLD_DB_NAME))
                    {
                        FileInfo ofi = new FileInfo(SQLiteDriver.DB_Path + OLD_DB_NAME);
                        FolderMap.WriteFile("Transportation started.");
                        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
                        stopwatch.Start();

                        TransportDB();
                        FolderMap.WriteFile("Transportation Finished in " + Math.Round(stopwatch.Elapsed.TotalMilliseconds, 0).ToString() + "mS");
                        FileInfo nfi = new FileInfo(SQLiteDriver.DB_Path + NEW_DB_NAME);
                        FolderMap.WriteFile("Old DB size: " + (ofi.Length / 1024).ToString() + "KB, New DB size: " + (nfi.Length / 1024).ToString() + "KB");
                        File.Move(SQLiteDriver.DB_Path + OLD_DB_NAME, SQLiteDriver.DB_Path + "CobraDB_exported.db3");
                        FolderMap.WriteFile("CobraDB.db3 renamed to CobraDB_exported.db3");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Create CobraDB failed\n", ex);
                //MessageBox.Show("Create CobraDB failed\n" + ex.Message);
            }
        }
        private static void TransportDB()
        {
            string OLD_DB_NAME = "CobraDB.db3";
            string NEW_DB_NAME = "CobraDBv2.db3";
            List<string> sqls = new List<string>();
            #region 填充SESSION_TABLE
            int row = -1;
            string sql = @"	select Logs3.log_id, Products.name || '_' || Products.version || '_' || Logs3.user_type || '_' || Logs3.date, Logs3.module_name, Logs3.timestamp, ''
	from	
		(select Logs2.log_id, Projects.product_id, Projects.user_type, Projects.date, Modules.module_name, Logs2.timestamp 
		from
			(select Logs.log_id, TableTypes.project_id, TableTypes.module_id, Logs.timestamp 
			From Logs, TableTypes 
			where Logs.table_type = TableTypes.table_type) as Logs2,
			Projects, Modules
		where Logs2.project_id = Projects.project_id and Logs2.module_id = Modules.module_id) as Logs3,
		Products
	where Logs3.product_id = Products.product_id";
            SQLiteDriver2.DB_Name = OLD_DB_NAME;
            DataTable session_dt = new DataTable();
            SQLiteDriver2.ExecuteSelect(sql, ref session_dt, ref row);
            SQLiteDriver2.DB_Name = NEW_DB_NAME;
            SQLiteDriver2.DBMultipleInsertInto(SessionTableName, session_dt);
            FolderMap.WriteFile(row.ToString() + " sessions were added into SESSION_TABLE");
            #endregion

            #region 填充DATA_TABLE
            #region approach 1
            /*List<string> datacolumns = new List<string>();
            datacolumns.Add("log_id");
            datacolumns.Add("table_type");

            row = -1;
            List<List<string>> datavalues = new List<List<string>>();
            SQLiteDriver2.DB_Name = "CobraDB.db3";
            SQLiteDriver2.DBSelect("Logs", null, datacolumns, ref datavalues, ref row);
            SQLiteDriver2.DB_Name = "CobraDBv2.db3";
            sqls.Clear();
            foreach (var datavalue in datavalues)
            {
                try
                {
                    int log_id = Convert.ToInt32(datavalue[0]);
                    int table_type = Convert.ToInt32(datavalue[1]);
                    string TableName = "Table" + table_type.ToString();
                    #region get column name
                    List<string> columns = new List<string>();

                    sql = "PRAGMA table_info(" + TableName + ");";
                    DataTable dt = new DataTable();
                    SQLiteDriver2.DB_Name = "CobraDB.db3";
                    SQLiteDriver2.ExecuteSelect(sql, ref dt, ref row);
                    SQLiteDriver2.DB_Name = "CobraDBv2.db3";
                    foreach (DataRow dr in dt.Rows)
                    {
                        if (dr["name"].ToString() != "log_id")
                            columns.Add(dr["name"].ToString());
                    }
                    #endregion
                    #region get data dictionary
                    DataTable temp_dt = new DataTable();
                    Dictionary<string, string> conditions = new Dictionary<string, string>();
                    conditions.Add("log_id", log_id.ToString());
                    SQLiteDriver2.DB_Name = "CobraDB.db3";
                    SQLiteDriver2.DBSelect(TableName, conditions, null, ref temp_dt, ref row);
                    SQLiteDriver2.DB_Name = "CobraDBv2.db3";
                    foreach (DataRow dr in temp_dt.Rows)
                    {
                        string data_set = "";
                        foreach (string column in columns)
                        {
                            data_set += column + "|" + dr[column].ToString() + ",";
                        }
                        sql = "INSERT OR IGNORE INTO " + DataTableName + "(session_id, data_set) VALUES ('" + log_id.ToString() + "', '" + data_set + "')";
                        sqls.Add(sql);
                    }
                    #endregion
                }
                catch (Exception ex)
                {
                    FolderMap.WriteFile(ex.Message);
                }
            }
            SQLiteDriver2.DB_Name = "CobraDBv2.db3";
            SQLiteDriver2.ExecuteNonQueryTransaction(sqls);*/
            #endregion
            #region approach 2
            sql = "select table_type from Logs group by table_type";
            DataTable table_type_dt = new DataTable();
            SQLiteDriver2.DB_Name = OLD_DB_NAME;
            SQLiteDriver2.ExecuteSelect(sql, ref table_type_dt, ref row);
            int table_type_cnt = row;
            List<string> TableTypes = new List<string>();
            foreach (DataRow dr in table_type_dt.Rows)
            {
                TableTypes.Add(dr["table_type"].ToString());
            }
            int total_row = 0;
            foreach (string table_type in TableTypes)
            {
                try
                {
                    string TableName = "Table" + table_type.ToString();
                    /*
                    #region get column name
                    List<string> columns = new List<string>();

                    sql = "PRAGMA table_info(" + TableName + ");";
                    DataTable table_type_content_dt = new DataTable();
                    SQLiteDriver2.ExecuteSelect(sql, ref table_type_content_dt, ref row);
                    foreach (DataRow dr in table_type_content_dt.Rows)
                    {
                        if (dr["name"].ToString() != "log_id")
                            columns.Add(dr["name"].ToString());
                    }
                    #endregion
                    */
                    #region get data dictionary
                    DataTable temp_dt = new DataTable();
                    SQLiteDriver2.DB_Name = OLD_DB_NAME;
                    SQLiteDriver2.DBSelect(TableName, null, null, ref temp_dt, ref row);
                    total_row += row;
                    foreach (DataRow dr in temp_dt.Rows)
                    {
                        string data_set = "";
                        //foreach (string column in columns)
                        foreach (DataColumn dc in temp_dt.Columns)
                        {
                            if (dc.ColumnName != "log_id")
                                data_set += dc.ColumnName + "|" + dr[dc.ColumnName].ToString() + ",";
                        }
                        string log_id = dr["log_id"].ToString();
                        sql = "INSERT OR IGNORE INTO " + DataTableName + "(session_id, data_set) VALUES ('" + log_id + "', '" + data_set + "')";
                        sqls.Add(sql);
                        if (sqls.Count >= 50000)
                        {
                            SQLiteDriver2.DB_Name = NEW_DB_NAME;
                            SQLiteDriver2.ExecuteNonQueryTransaction(sqls);
                            sqls.Clear();
                            SQLiteDriver2.DB_Name = OLD_DB_NAME;
                        }
                    }
                    #endregion
                }
                catch (Exception ex)
                {
                    FolderMap.WriteFile(ex.Message);

                    FolderMap.WriteFile("sqls.Count:"+sqls.Count.ToString());
                }
            }
            if (sqls.Count != 0)
            {
                SQLiteDriver2.DB_Name = NEW_DB_NAME;
                SQLiteDriver2.ExecuteNonQueryTransaction(sqls);
                sqls.Clear();
            }
            FolderMap.WriteFile(total_row.ToString() + " rows in " + table_type_cnt.ToString() + " tables were added.");

            #endregion
            #endregion

            #region 随机验证
            string verification_sql = "";
            string old_log_id = "";
            string old_table_type = "";
            row = 0;
            string random_data_set = "";
            string row_id = "";
            DataTable random_dt = new DataTable();

            verification_sql = "select session_id from DATA_TABLE group by session_id order by random() asc limit 1";
            SQLiteDriver2.DB_Name = NEW_DB_NAME;
            SQLiteDriver2.ExecuteSelect(verification_sql, ref random_dt, ref row);
            old_log_id = random_dt.Rows[0]["session_id"].ToString();
            verification_sql = "select data_set from DATA_TABLE where session_id = " + old_log_id + " order by data_id desc limit 1";
            random_dt = new DataTable();
            SQLiteDriver2.ExecuteSelect(verification_sql, ref random_dt, ref row);
            random_data_set = random_dt.Rows[0]["data_set"].ToString();
            FolderMap.WriteFile("Data in new DB:\t\t" + random_data_set);


            random_dt = new DataTable();
            verification_sql = "select * from Logs where log_id = " + old_log_id;
            SQLiteDriver2.DB_Name = OLD_DB_NAME;
            SQLiteDriver2.ExecuteSelect(verification_sql, ref random_dt, ref row);
            old_table_type = random_dt.Rows[0]["table_type"].ToString();
            verification_sql = "select rowid, * from Table" + old_table_type + " where log_id = " + old_log_id + " order by rowid desc limit 1";
            random_dt = new DataTable();
            SQLiteDriver2.ExecuteSelect(verification_sql, ref random_dt, ref row);
            random_data_set = "";
            foreach (DataColumn dc in random_dt.Columns)
            {
                if (dc.ColumnName == "rowid")
                    row_id = random_dt.Rows[0][dc.ColumnName].ToString();
                else if (dc.ColumnName != "log_id")
                    random_data_set += dc.ColumnName + "|" + random_dt.Rows[0][dc.ColumnName].ToString() + ",";
            }
            FolderMap.WriteFile("Table Name: " + " Table" + old_table_type + " log_id: " + old_log_id);
            FolderMap.WriteFile("Data in old DB:\t\t" + random_data_set);
            SQLiteDriver2.DB_Name = NEW_DB_NAME;
            #endregion
        }
        public static void ExtensionRegister(string project_name)
        {
                Project_Name = project_name;
        }
        public static void NewSession(string module_name, ref int session_id, string device_index = "", string session_establish_time = "")
        {
            try
            {
                lock (DB_Lock)
                {
                    Dictionary<string, string> record = new Dictionary<string, string>();
                    record.Add("project_name", Project_Name);
                    record.Add("module_name", module_name);
                    record.Add("device_index", device_index);
                    record.Add("session_establish_time", session_establish_time);
                    int row = -1;
                    SQLiteDriver2.DBInsertInto(SessionTableName, record, ref row);
                    GetSessionIDFromSessionTable(module_name, ref session_id, device_index, session_establish_time);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("New session failed\n", ex);
                //MessageBox.Show("New session failed\n" + ex.Message);
            }
        }
        public static void BeginNewRow(int session_id, Dictionary<string, string> data_dictionary)
        {
            try
            {
                lock (DB_Lock)
                {
                    if (!flush_timer.Enabled)
                    {
                        flush_timer.Interval = FlushInterval;
                        flush_timer.Start();
                    }
                    Dictionary<string, string> record = new Dictionary<string, string>();
                    record.Add("session_id", session_id.ToString());

                    string data_dictionary_string = "";
                    foreach (string key in data_dictionary.Keys)
                    {
                        data_dictionary_string += (key + "|" + data_dictionary[key] + ",");
                    }
                    record.Add("data_set", data_dictionary_string);

                    string sql = SQLiteDriver2.SQLInsertInto(DataTableName, record);
                    sqls_buffer.Add(sql);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("New Row failed\n", ex);
            }
        }
        public static void BeginNewRow(int session_id, string data_normal)
        {
            try
            {
                lock (DB_Lock)
                {
                    if (!flush_timer.Enabled)
                    {
                        flush_timer.Interval = 1000;
                        flush_timer.Start();
                    }

                    Dictionary<string, string> record = new Dictionary<string, string>();
                    record.Add("session_id", session_id.ToString());
                    record.Add("data_set", data_normal);

                    string sql = SQLiteDriver2.SQLInsertInto(DataTableName, record);
                    sqls_buffer.Add(sql);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("New Row failed\n", ex);
            }
        }
        public static void ExecuteQuery(string sql, ref DataTable dt, ref int row)
        {
            try
            {
                lock (DB_Lock)
                {
                    SQLiteDriver2.ExecuteSelect(sql, ref dt, ref row);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Execute query failed\n", ex);
                //MessageBox.Show("Execute query failed\n" + ex.Message);
            }
        }
        
#region For Scan SFL
        public static void ScanSFLGetSessionsInfor(string module_name, ref List<List<string>> records)
        {
            try
            {
                lock (DB_Lock)
                {
                    if (sqls_buffer.Count != 0)
                    {
                        SQLiteDriver2.ExecuteNonQueryTransaction(sqls_buffer);
                        sqls_buffer.Clear();
                    }
                    Dictionary<string, string> conditions = new Dictionary<string, string>();
                    conditions.Add("project_name", Project_Name);
                    conditions.Add("module_name", module_name);
                    List<string> datacolumns = new List<string>();
                    datacolumns.Add("session_id");
                    datacolumns.Add("session_establish_time");
                    datacolumns.Add("device_index");

                    int row = -1;
                    List<List<string>> datavalues = new List<List<string>>();
                    SQLiteDriver2.DBSelect(SessionTableName, conditions, datacolumns, ref datavalues, ref row);
                    foreach (var datavalue in datavalues)
                    {
                        int session_id = Convert.ToInt32(datavalue[0]);
                        string timestamp = datavalue[1];
                        string device_num = datavalue[2];
                        int session_size = -1;
                        GetSessionSize(session_id, ref session_size);
                        List<string> item = new List<string>();
                        item.Add(timestamp);
                        item.Add(session_size.ToString());
                        item.Add(device_num);
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
        public static void ScanSFLDeleteOneSession(string module_name, string session_establish_time)
        {
            try
            {
                lock (DB_Lock)
                {
                    int session_id = -1;
                    GetSessionIDFromSessionTable(module_name, ref session_id, "", session_establish_time);

                    Dictionary<string, string> conditions = new Dictionary<string, string>();
                    conditions.Add("session_id", session_id.ToString());
                    int row = -1;
                    SQLiteDriver2.DBDelete(DataTableName, conditions, ref row);
                    SQLiteDriver2.DBDelete(SessionTableName, conditions, ref row);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Scan SFL Delete Session failed\n", ex);
                //MessageBox.Show("Scan SFL Delete Session failed\n" + ex.Message);
            }
        }
        public static void ScanSFLGetOneSession(string module_name, string session_establish_time, ref DataTable dt)
        {
            try
            {
                lock (DB_Lock)
                {
                    int session_id = -1;
                    GetSessionIDFromSessionTable(module_name, ref session_id, "", session_establish_time);

                    Dictionary<string, string> conditions = new Dictionary<string, string>();
                    conditions.Add("session_id", session_id.ToString());

                    List<string> datacolumns = new List<string>();
                    datacolumns.Add("data_set");
                    int row = -1;
                    DataTable dtTemp = new DataTable();
                    SQLiteDriver2.DBSelect(DataTableName, conditions, datacolumns, ref dtTemp, ref row);
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

        #endregion
    }
}
