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

namespace Cobra.Common
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
                        sqls.Add("CREATE TABLE IF NOT EXISTS " + SessionTableName + " (session_id INTEGER PRIMARY KEY, project_name VARCHAR(30) NOT NULL, module_name VARCHAR(30) NOT NULL, session_establish_time VARCHAR(17) NOT NULL, device_index VARCHAR(10), row_number VARCHAR(10), UNIQUE(project_name, module_name, session_establish_time));");//Issue1406 Leon
                        sqls.Add("CREATE TABLE IF NOT EXISTS " + DataTableName + " (data_id INTEGER PRIMARY KEY, session_id INTEGER NOT NULL, data_set VARCHAR(500) NOT NULL);");
                        //todo: Bus_SPI Bus_I2C2 Bus_???
                        //int row = -1;
                        SQLiteDriver2.ExecuteNonQueryTransaction(sqls);
                        sqls.Clear();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Create CobraDB failed\n", ex);
            }
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
        public static void ScanSFLUpdateSessionSize(int session_id, ulong session_row_number)
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
                    string sql = "UPDATE " + SessionTableName + " SET row_number = " + session_row_number.ToString() + " WHERE session_id = " + session_id.ToString() + ";";
                    int row = -1;
                    SQLiteDriver2.ExecuteNonQuery(sql, ref row);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Scan SFL Get Sessions Infor failed\n", ex);
                //MessageBox.Show("Scan SFL Get Sessions Infor failed\n" + ex.Message);
            }
        }
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
                    datacolumns.Add("row_number");

                    int row = -1;
                    List<List<string>> datavalues = new List<List<string>>();
                    SQLiteDriver2.DBSelect(SessionTableName, conditions, datacolumns, ref datavalues, ref row);
                    foreach (var datavalue in datavalues)
                    {
                        int session_id = Convert.ToInt32(datavalue[0]);
                        string timestamp = datavalue[1];
                        string device_num = datavalue[2];
                        string session_size = datavalue[3];
                        //int session_size = -1;
                        //GetSessionSize(session_id, ref session_size);
                        List<string> item = new List<string>();
                        item.Add(timestamp);
                        item.Add(session_size);
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
