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
    public static class DBManager
    {
        public enum DataType
        {
            INTERGER,
            FLOAT,
            TEXT,
        }

        public static bool supportdb = false;       //目前只有部分oce支持db，通过此变量来兼容不支持的oce。当所有oce都支持db后，可以去掉
        public static bool DebugMode = false;       //为true时，每次启动都会在CobraDocument/Database/DebugDB创建新的临时DB
        private static string DBName = "CobraDBv1.1.db3";
        private static string DBpath = "";
        private static object DB_Lock = new object();
        //From Guo.zhu
        private static Dictionary<string, string> m_record = new Dictionary<string, string>();
        private static DateTime curDt = DateTime.Now;
        private static DateTime startDt = DateTime.Now;
        private static TimeSpan ts = curDt - startDt;

        #region 数据库中与当前工程或进程关联的某系数据
        private static Dictionary<string, int> currentLogID = new Dictionary<string, int>();
        private static int Device_id = 0;
        private static int Project_id = 0;
        #endregion

        #region 内存缓冲区
        private static List<string> sqls = new List<string>();
        private static DataTable Modules = new DataTable();
        private static DataTable TableTypes = new DataTable();
        private static System.Timers.Timer t = new System.Timers.Timer();
        private static bool isTimerBooked = false;
        private static byte idle_cnt = 0;
        #endregion

        private static void t_Elapsed(object sender, EventArgs e)
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

        #region 这部分以后可以考虑重构
        private static int GetProjectIDFromProject(int product_id, string user_type, string date)
        {
            /*string sql = "SELECT project_id FROM Projects WHERE product_id = '" + product_id.ToString()
                + "' AND user_type = '" + user_type + "' AND date = '" + date + "';";
            using (SQLiteDataReader reader = SQLReader(sql))
            {
                reader.Read();
                return reader.GetInt32(0);
            }*/
            List<string> datacolumns = new List<string>();
            datacolumns.Add("project_id");

            string tablename = "Projects";
            Dictionary<string,string> conditions = new Dictionary<string,string>();
            conditions.Add("product_id", product_id.ToString());
            conditions.Add("user_type", user_type);
            conditions.Add("date", date);
            DataTable dt = new DataTable();
            int row = -1;
            SQLiteResult sret = SQLiteDriver.DBSelect(tablename, conditions, datacolumns, ref dt, ref row);
            if (sret.I != 0)
            {
                //todo: add warning here
                //MessageBox.Show(sret.Str);
                return -1;
            }
            else if (dt.Rows.Count == 0)
            {
                //todo: add warning here
                //MessageBox.Show("No such Project ID");
                return -1;
            }
            else
                return Convert.ToInt32(dt.Rows[0]["project_id"]);
        }
        private static int GetDeviceIDFromProduct(string name, string version)
        {
            /*string sql = "SELECT product_id FROM Products WHERE name = '" + name + "' AND version = '" + version + "';";
            using (SQLiteDataReader reader = SQLReader(sql))
            {
                reader.Read();
                return reader.GetInt32(0);
            }*/
            List<string> datacolumns = new List<string>();
            datacolumns.Add("product_id");

            string tablename = "Products";
            Dictionary<string, string> conditions = new Dictionary<string, string>();
            conditions.Add("name", name);
            conditions.Add("version", version);
            DataTable dt = new DataTable();
            int row = -1;
            SQLiteResult sret = SQLiteDriver.DBSelect(tablename, conditions, datacolumns, ref dt, ref row);
            if (sret.I != 0)
            {
                //todo: add warning here
                //MessageBox.Show(sret.Str);
                return -1;
            }
            else if (dt.Rows.Count == 0)
            {
                //todo: add warning here
                //MessageBox.Show("No such Device ID");
                return -1;
            }
            else
                return Convert.ToInt32(dt.Rows[0]["product_id"]);
        }
        private static int GetModuleIDFromModules(string module_name)
        {
            /*string sql = "SELECT module_id FROM Modules WHERE module_name = '" + module_name + "';";
            using (SQLiteDataReader reader = SQLReader(sql))
            {
                reader.Read();
                return reader.GetInt32(0);
            }*/

            List<string> datacolumns = new List<string>();
            datacolumns.Add("module_id");

            string tablename = "Modules";
            Dictionary<string, string> conditions = new Dictionary<string, string>();
            conditions.Add("module_name", module_name);
            DataTable dt = new DataTable();
            int row = -1;
            SQLiteResult sret = SQLiteDriver.DBSelect(tablename, conditions, datacolumns, ref dt, ref row);
            if (sret.I != 0)
            {
                //todo: add warning here
                //MessageBox.Show(sret.Str);
                return -1;
            }
            else if (dt.Rows.Count == 0)
            {
                //todo: add warning here
                //MessageBox.Show("No such Module ID");
                return -1;
            }
            else
                return Convert.ToInt32(dt.Rows[0]["module_id"]);
        }
        private static int GetTableTypeFromTableTypes(int project_id, int module_id)
        {
            /*string sql = "SELECT table_type FROM TableTypes WHERE project_id = '" + project_id.ToString() + "' AND module_id = '" + module_id.ToString() + "';";
            using (SQLiteDataReader reader = SQLReader(sql))
            {
                reader.Read();
                return reader.GetInt32(0);
            }*/

            List<string> datacolumns = new List<string>();
            datacolumns.Add("table_type");

            string tablename = "TableTypes";
            Dictionary<string, string> conditions = new Dictionary<string, string>();
            conditions.Add("project_id", project_id.ToString());
            conditions.Add("module_id", module_id.ToString());
            DataTable dt = new DataTable();
            int row = -1;
            SQLiteResult sret = SQLiteDriver.DBSelect(tablename, conditions, datacolumns, ref dt, ref row);
            if (sret.I != 0)
            {
                //todo: add warning here
                //MessageBox.Show(sret.Str);
                return -1;
            }
            else if (dt.Rows.Count == 0)
            {
                //todo: add warning here
                //MessageBox.Show("No such table type");
                return -1;
            }
            else
                return Convert.ToInt32(dt.Rows[0]["table_type"]);
        }
        private static int GetLogIDFromLogs(int table_type, string timestamp)
        {
            /*string sql = "SELECT log_id FROM Logs WHERE table_type = '" + table_type.ToString() + "' AND timestamp = '" + timestamp + "';";
            using (SQLiteDataReader reader = SQLReader(sql))
            {
                reader.Read();
                return reader.GetInt32(0);
            }*/

            List<string> datacolumns = new List<string>();
            datacolumns.Add("log_id");

            string tablename = "Logs";
            Dictionary<string, string> conditions = new Dictionary<string, string>();
            conditions.Add("table_type", table_type.ToString());
            conditions.Add("timestamp", timestamp);
            DataTable dt = new DataTable();
            int row = -1;
            SQLiteResult sret = SQLiteDriver.DBSelect(tablename, conditions, datacolumns, ref dt, ref row);
            if (sret.I != 0)
            {
                //todo: add warning here
                //MessageBox.Show(sret.Str);
                return -1;
            }
            else if (dt.Rows.Count == 0)
            {
                //todo: add warning here
                //MessageBox.Show("No such Log ID");
                return -1;
            }
            else
                return Convert.ToInt32(dt.Rows[0]["log_id"]);
        }
        private static int GetLogSize(int log_id)
        {
            string tablename = "Logs";
            Dictionary<string, string> conditions = new Dictionary<string, string>();
            conditions.Add("log_id", log_id.ToString());
            List<string> datacolumns = new List<string>();
            datacolumns.Add("table_type");

            DataTable dt = new DataTable();
            int row = -1;
            List<List<string>> datavalues = new List<List<string>>();
            SQLiteResult sret = SQLiteDriver.DBSelect(tablename, conditions, datacolumns, ref datavalues, ref row);
            if (sret.I == 0 && datavalues.Count == 1 && datavalues[0].Count != 0)
            {
                string table_type = datavalues[0][0];
                sret = SQLiteDriver.DBSelect("Table" + table_type.ToString(), conditions, null, ref dt, ref row);
                if (sret.I == 0)
                    return row;
                else
                    return sret.I;
            }
            else
                return sret.I;
        }
        #endregion

        #region API
        //return: succuss 0, failed -1
        public static Int32 CobraDBInit(string folder)
        {
            lock (DB_Lock)
            {
                //if (DBManager.supportdb == true)
                //{
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
                    List<string> sqls = new List<string>();
                    sqls.Add("CREATE TABLE IF NOT EXISTS Products(product_id INTEGER PRIMARY KEY, name VARCHAR(30) NOT NULL, version VARCHAR(30) NOT NULL, UNIQUE(name, version));");
                    sqls.Add("CREATE TABLE IF NOT EXISTS NameAlias(orig_name VARCHAR(30) NOT NULL, name_alias VARCHAR(30) NOT NULL, UNIQUE(orig_name, name_alias));");
                    sqls.Add("CREATE TABLE IF NOT EXISTS VersionAlias(product_id INTEGER NOT NULL, version_alias VARCHAR(30) NOT NULL, UNIQUE(product_id, version_alias));");
                    sqls.Add("CREATE TABLE IF NOT EXISTS Projects(project_id INTEGER PRIMARY KEY, product_id INTEGER NOT NULL, user_type TEXT NOT NULL, date TEXT NOT NULL, bus_type TEXT NOT NULL, UNIQUE(product_id, user_type, date));");
                    sqls.Add("CREATE TABLE IF NOT EXISTS Modules(module_id INTEGER PRIMARY KEY, module_name VARCHAR(30) NOT NULL, UNIQUE(module_name));");
                    sqls.Add("CREATE TABLE IF NOT EXISTS TableTypes(table_type INTEGER PRIMARY KEY, project_id INTEGER NOT NULL, module_id INTEGER NOT NULL, UNIQUE(project_id, module_id));");
                    sqls.Add("CREATE TABLE IF NOT EXISTS Logs(log_id INTEGER PRIMARY KEY, table_type INTEGER NOT NULL, log_info VARCHAR(30), timestamp VARCHAR(17) NOT NULL, device_num VARCHAR(10));");
                    sqls.Add("CREATE TABLE IF NOT EXISTS Bus_I2C(project_id INTEGER, device_id INTEGER, frequency INTEGER NOT NULL, address INTEGER NOT NULL, pec_enable BOOLEAN NOT NULL, UNIQUE(project_id, device_id));");
                    //todo: Bus_SPI Bus_I2C2 Bus_???
                    SQLiteResult sret = SQLiteDriver.ExecuteNonQueryTransaction(sqls);
                    if (sret.I != 0)
                    {
                        //todo: add warning here
                        //MessageBox.Show(sret.Str);
                    }
                    return sret.I;
                //}
                //else
                    //return 0;
            }
        }
        public static Int32 ExtensionRegister(string orig_name, string chip_name, string orig_version, string chip_version, string user_type, string date, string bus_type, List<string> modulelist)
        {
            lock (DB_Lock)
            {
                //if (DBManager.supportdb == true)
                //{
                    Dictionary<string, string> record = new Dictionary<string, string>();
                    record.Add("name", orig_name);
                    record.Add("version", orig_version);
                    int row = -1;
                    SQLiteResult sret = SQLiteDriver.DBInsertInto("Products", record, ref row);
                    if (sret.I != 0)
                        return sret.I;
                    int product_id = GetDeviceIDFromProduct(orig_name, orig_version);
                    Device_id = product_id;
                    record.Clear();

                    if (chip_version != "")
                    {
                        record.Add("product_id", product_id.ToString());
                        record.Add("version_alias", chip_version);
                        sret = SQLiteDriver.DBInsertInto("VersionAlias", record, ref row);
                        if (sret.I != 0)
                            return sret.I;
                        record.Clear();
                    }

                    if (chip_name != "")
                    {
                        record.Add("orig_name", orig_name);
                        record.Add("name_alias", chip_name);
                        sret = SQLiteDriver.DBInsertInto("NameAlias", record, ref row);
                        if (sret.I != 0)
                            return sret.I;
                        record.Clear();
                    }

                    record.Add("product_id", product_id.ToString());
                    record.Add("user_type", user_type);
                    record.Add("date", date);
                    record.Add("bus_type", bus_type);
                    sret = SQLiteDriver.DBInsertInto("Projects", record, ref row);
                    if (sret.I != 0)
                        return sret.I;
                    int project_id = GetProjectIDFromProject(product_id, user_type, date);
                    Project_id = project_id;
                    record.Clear();

                    foreach (string module_name in modulelist)
                    {
                        record.Add("module_name", module_name);
                        sret = SQLiteDriver.DBInsertInto("Modules", record, ref row);
                        if (sret.I != 0)
                            return sret.I;
                        record.Clear();

                        int module_id = GetModuleIDFromModules(module_name);
                        record.Add("project_id", project_id.ToString());
                        record.Add("module_id", module_id.ToString());
                        sret = SQLiteDriver.DBInsertInto("TableTypes", record, ref row);
                        if (sret.I != 0)
                            return sret.I;
                        record.Clear();
                    }
                    sret = SQLiteDriver.DBSelect("Modules", null, null, ref Modules, ref row);
                    if (sret.I != 0)
                        return sret.I;
                    sret = SQLiteDriver.DBSelect("TableTypes", null, null, ref TableTypes, ref row);
                    if (sret.I != 0)
                        return sret.I;

                    return sret.I;
                //}
                //else
                    //return 0;
            }
        }
        public static Int32 CreateTableN(string module_name, Dictionary<string, DBManager.DataType> columns)
        {
            lock (DB_Lock)
            {
                if (DBManager.supportdb == true)
                {
                    int module_id = GetModuleIDFromModules(module_name);
                    int table_type = GetTableTypeFromTableTypes(Project_id, module_id);
                    Dictionary<string, DBManager.DataType> m_columns = new Dictionary<string, DBManager.DataType>();
                    m_columns.Add("log_id", DBManager.DataType.INTERGER);
                    foreach (string key in columns.Keys)
                    {
                        m_columns.Add(key, columns[key]);
                    }
                    Dictionary<string, string> strcol = new Dictionary<string, string>();
                    foreach (string key in m_columns.Keys)
                    {
                        string datatype = "";
                        switch (m_columns[key])
                        {
                            case DataType.FLOAT:
                                datatype = "FLOAT";
                                break;
                            case DataType.INTERGER:
                                datatype = "INTERGER";
                                break;
                            case DataType.TEXT:
                                datatype = "TEXT";
                                break;
                        }
                        strcol.Add(key, datatype);
                    }
                    SQLiteResult sret = SQLiteDriver.DBCreateTable("Table" + table_type.ToString(), strcol);
                    if (sret.I != 0)
                    {
                        //todo: add warning here
                        //MessageBox.Show(sret.Str);
                    }
                    return sret.I;
                }
                else return 0;
            }
        }
        public static int NewLog(string module_name, string log_info, string timestamp, ref int log_id)
        {
            lock (DB_Lock)
            {
                if (DBManager.supportdb == true)
                {
                    int module_id = DBManager.GetModuleIDFromModules(module_name);
                    if (module_id == -1)
                        return -1;
                    int table_type = DBManager.GetTableTypeFromTableTypes(Project_id, module_id);
                    if (table_type == -1)
                        return -1;

                    Dictionary<string, string> record = new Dictionary<string, string>();
                    record.Add("table_type", table_type.ToString());
                    record.Add("log_info", log_info);
                    record.Add("timestamp", timestamp);
                    int row = -1;
                    SQLiteResult sret = SQLiteDriver.DBInsertInto("Logs", record, ref row);
                    if (sret.I != 0)
                    {
                        //todo: add warning here
                        //MessageBox.Show(sret.Str);
                        return sret.I;
                    }
                    log_id = DBManager.GetLogIDFromLogs(table_type, timestamp);
                    if (log_id == -1)
                        return -1;
                    if (currentLogID.ContainsKey(module_name))
                        currentLogID[module_name] = log_id;
                    else
                        currentLogID.Add(module_name, log_id);
                    return sret.I;
                }
                else
                    return 0;
            }
        }
        public static Int32 NewRow(string module_name, Dictionary<string, string> record)
        {
            lock (DB_Lock)
            {
                if (DBManager.supportdb == true)
                {
                    int module_id = DBManager.GetModuleIDFromModules(module_name);
                    if (module_id == -1)
                        return -1;
                    int table_type = DBManager.GetTableTypeFromTableTypes(Project_id, module_id);
                    if (table_type == -1)
                        return -1;
                    int log_id = currentLogID[module_name];

                    Dictionary<string, string> m_record = new Dictionary<string, string>();
                    m_record.Add("log_id", log_id.ToString());
                    foreach (string key in record.Keys)
                    {
                        m_record.Add(key, record[key]);
                    }
                    int row = -1;
                    SQLiteResult sret = SQLiteDriver.DBInsertInto("Table" + table_type.ToString(), m_record, ref row);
                    if (sret.I != 0)
                    {
                        //todo: add warning here
                        //MessageBox.Show(sret.Str);
                    }
                    return sret.I;
                }
                else
                    return 0;
            }
        }

        public static Int32 BeginNewRow(string module_name, Dictionary<string, string> record)
        {
            lock (DB_Lock)
            {
                if (DBManager.supportdb == true)
                {
                    if (!t.Enabled)
                    {
                        t.Interval = 15000;
                        t.Start();
                    }

                    int module_id;
                    //module_id = DBManager.GetModuleID(module_name);
                    var queryResults =
                        from module in Modules.AsEnumerable()
                        select module;
                    var rows =
                        queryResults.Where(p => p.Field<string>("module_name") == module_name);
                    module_id = (int)rows.ToArray()[0].Field<long>("module_id");

                    int table_type;
                    //table_type = DBManager.GetTableType(Project_id, module_id);
                    queryResults =
                        from tabletype in TableTypes.AsEnumerable()
                        select tabletype;
                    rows =
                        queryResults.Where(p => p.Field<long>("project_id") == Project_id);
                    rows =
                        rows.Where(p => p.Field<long>("module_id") == module_id);
                    table_type = (int)rows.ToArray()[0].Field<long>("table_type");

                    int log_id = currentLogID[module_name];

                    Dictionary<string, string> m_record = new Dictionary<string, string>();
                    m_record.Add("log_id", log_id.ToString());
                    foreach (string key in record.Keys)
                    {
                        m_record.Add(key, record[key]);
                    }
                    /*int row = -1;
                    SQLiteResult sret = SQLiteDriver.DBInsertInto("Table" + table_type.ToString(), m_record, ref row);
                    if (sret.I != 0)
                    {
                        //todo: add warning here
                        //MessageBox.Show(sret.Str);
                    }
                    return sret.I;*/
                    string sql = SQLiteDriver.SQLInsertInto("Table" + table_type.ToString(), m_record);
                    sqls.Add(sql);
                    return 0;
                }
                else
                    return 0;
            }
        }

        public static void BeginNewRow(string module_name, DataRow record)  //Added by Guo, used by FSBS2 and UFPSBS
        {
            lock (DB_Lock)
            {
                int module_id;
                int table_type;

                if (DBManager.supportdb == true)
                {
                    /*if (!t.Enabled)
                    {
                        t.Interval = 15000;
                        t.Start();
                    }*/

                    var queryResults = from module in Modules.AsEnumerable() select module;
                    var rows = queryResults.Where(p => p.Field<string>("module_name") == module_name);
                    module_id = (int)rows.ToArray()[0].Field<long>("module_id");

                    queryResults = from tabletype in TableTypes.AsEnumerable() select tabletype;
                    rows = queryResults.Where(p => p.Field<long>("project_id") == Project_id);
                    rows = rows.Where(p => p.Field<long>("module_id") == module_id);
                    table_type = (int)rows.ToArray()[0].Field<long>("table_type");
                    int log_id = currentLogID[module_name];
                    m_record.Add("log_id", log_id.ToString());
                    foreach (DataColumn item in record.Table.Columns)
                        m_record.Add(item.ColumnName, record[item.ColumnName].ToString());

                    string sql = SQLiteDriver.SQLInsertInto("Table" + table_type.ToString(), m_record);
                    sqls.Add(sql);
                    m_record.Clear();
                    curDt = DateTime.Now;
                    ts = curDt - startDt;
                    if (ts.TotalSeconds > 15)
                    {
                        SQLiteDriver.ExecuteNonQueryTransaction(sqls);
                        sqls.Clear();
                        startDt = DateTime.Now;
                    }
                }
                else
                    return;
            }
        }

        public static Int32 NewRows(string module_name, List<Dictionary<string, string>> records)   //Used by COMM and AMT
        {
            lock (DB_Lock)
            {
                if (DBManager.supportdb == true)
                {
                    int module_id = DBManager.GetModuleIDFromModules(module_name);
                    if (module_id == -1)
                        return -1;
                    int table_type = DBManager.GetTableTypeFromTableTypes(Project_id, module_id);
                    if (table_type == -1)
                        return -1;
                    int log_id = currentLogID[module_name];

                    List<Dictionary<string, string>> m_records = new List<Dictionary<string, string>>();
                    foreach (var record in records)
                    {
                        Dictionary<string, string> m_record = new Dictionary<string, string>();
                        m_record.Add("log_id", log_id.ToString());
                        foreach (string key in record.Keys)
                        {
                            m_record.Add(key, record[key]);
                        }
                        m_records.Add(m_record);
                    }
                    SQLiteResult sret = SQLiteDriver.DBMultipleInsertInto("Table" + table_type.ToString(), m_records);

                    if (sret.I != 0)
                    {
                        //todo: add warning here
                        //MessageBox.Show(sret.Str);
                    }
                    return sret.I;
                }
                else
                    return 0;
            }
        }

        public static Int32 SaveBusOptions(BUS_TYPE bustype, int device_id, Dictionary<string, string> options)
        {
            lock (DB_Lock)
            {
                if (DBManager.supportdb == true)
                {
                    if (bustype != BUS_TYPE.BUS_TYPE_I2C)	//暂时不支持其他类型，后续需要支持
                        return 0;
                    string tablename = "";
                    switch (bustype)
                    {
                        case BUS_TYPE.BUS_TYPE_I2C:
                            tablename = "Bus_I2C";
                            break;
                    }
                    Dictionary<string, string> conditions = new Dictionary<string, string>();
                    conditions.Add("project_id", Project_id.ToString());
                    conditions.Add("device_id", device_id.ToString());
                    int row = -1;
                    SQLiteResult sret = SQLiteDriver.DBUpdateOrInsert(tablename, conditions, options, ref row);
                    if (sret.I != 0)
                    {
                        //todo: add warning here
                        //MessageBox.Show(sret.Str);
                    }
                    return sret.I;
                }
                else
                    return 0;
            }
        }
        public static Int32 LoadBusOptions(BUS_TYPE bustype, int device_id, ref Dictionary<string, string> options)
        {
            lock (DB_Lock)
            {
                if (DBManager.supportdb == true)
                {
                    string tablename = "";
                    if (bustype == BUS_TYPE.BUS_TYPE_SPI)
                        return 0;
                    switch (bustype)
                    {
                        case BUS_TYPE.BUS_TYPE_I2C:
                            tablename = "Bus_I2C";
                            break;
                    }
                    Dictionary<string, string> conditions = new Dictionary<string, string>();
                    conditions.Add("project_id", Project_id.ToString());
                    conditions.Add("device_id", device_id.ToString());
                    List<string> datacolumns = new List<string>();
                    datacolumns.Add("frequency");
                    datacolumns.Add("address");
                    datacolumns.Add("pec_enable");
                    int row = -1;
                    List<List<string>> datavalues = new List<List<string>>();
                    SQLiteResult sret = SQLiteDriver.DBSelect(tablename, conditions, datacolumns, ref datavalues, ref row);
                    if (sret.I == 0 && datavalues.Count == 1 && datavalues[0].Count != 0)
                    {
                        for (int i = 0; i < datacolumns.Count; i++)
                        {
                            options.Add(datacolumns[i], datavalues[0][i]);
                        }
                        return sret.I;
                    }
                    else
                        return sret.I;
                }
                else
                    return 0;
            }
        }//*/

        public static Int32 GetLogsInfor(string module_name, ref List<List<string>> records)
        {
            lock (DB_Lock)
            {
                if (DBManager.supportdb == true)
                {
                    SQLiteResult sret;
                    if (sqls.Count != 0)
                    {
                        sret = SQLiteDriver.ExecuteNonQueryTransaction(sqls);
                        if (sret.I != 0)
                            return sret.I;
                        sqls.Clear();
                    }
                    int module_id = GetModuleIDFromModules(module_name);
                    if (module_id == -1)
                        return -1;
                    int table_type = GetTableTypeFromTableTypes(Project_id, module_id);
                    if (table_type == -1)
                        return -1;

                    Dictionary<string, string> conditions = new Dictionary<string, string>();
                    conditions.Add("table_type", table_type.ToString());
                    List<string> datacolumns = new List<string>();
                    datacolumns.Add("log_id");
                    datacolumns.Add("timestamp");

                    int row = -1;
                    List<List<string>> datavalues = new List<List<string>>();
                    sret = SQLiteDriver.DBSelect("Logs", conditions, datacolumns, ref datavalues, ref row);
                    if (sret.I == 0 && datavalues.Count != 0)
                    {
                        foreach (var datavalue in datavalues)
                        {
                            int log_id = Convert.ToInt32(datavalue[0]);
                            string timestamp = datavalue[1];
                            int log_size = GetLogSize(log_id);
                            List<string> item = new List<string>();
                            item.Add(timestamp);
                            item.Add(log_size.ToString());
                            records.Add(item);
                        }
                        return sret.I;
                    }
                    else
                        return sret.I;
                }
                else
                    return 0;
            }
        }
        public static Int32 GetLog(string module_name, string timestamp, ref DataTable dt)
        {
            lock (DB_Lock)
            {
                if (DBManager.supportdb == true)
                {
                    SQLiteResult sret;
                    int module_id = GetModuleIDFromModules(module_name);
                    if (module_id == -1)
                        return -1;
                    int table_type = GetTableTypeFromTableTypes(Project_id, module_id);
                    if (table_type == -1)
                        return -1;

                    Dictionary<string, string> conditions = new Dictionary<string, string>();
                    conditions.Add("table_type", table_type.ToString());
                    conditions.Add("timestamp", timestamp.ToString());
                    List<string> datacolumns = new List<string>();
                    datacolumns.Add("log_id");

                    int row = -1;
                    List<List<string>> datavalues = new List<List<string>>();
                    sret = SQLiteDriver.DBSelect("Logs", conditions, datacolumns, ref datavalues, ref row);
                    int log_id=0;
                    if (sret.I == 0 && datavalues.Count == 1)
                    {
                        log_id = Convert.ToInt32(datavalues[0][0]);
                    }
                    conditions.Clear();
                    conditions.Add("log_id", log_id.ToString());
                    sret = SQLiteDriver.DBSelect("Table" + table_type.ToString(), conditions, null, ref dt, ref row);
                    return sret.I;
                }
                else
                    return 0;
            }
        }

        public static Int32 ClearLog(string module_name, string timestamp)
        {
            lock (DB_Lock)
            {
                if (DBManager.supportdb == true)
                {
                    SQLiteResult sret;
                    int module_id = GetModuleIDFromModules(module_name);
                    if (module_id == -1)
                        return -1;
                    int table_type = GetTableTypeFromTableTypes(Project_id, module_id);
                    if (table_type == -1)
                        return -1;

                    Dictionary<string, string> conditions = new Dictionary<string, string>();
                    conditions.Add("table_type", table_type.ToString());
                    conditions.Add("timestamp", timestamp.ToString());
                    List<string> datacolumns = new List<string>();
                    datacolumns.Add("log_id");

                    int row = -1;
                    List<List<string>> datavalues = new List<List<string>>();
                    sret = SQLiteDriver.DBSelect("Logs", conditions, datacolumns, ref datavalues, ref row);
                    int log_id = 0;
                    if (sret.I == 0 && datavalues.Count == 1)
                    {
                        log_id = Convert.ToInt32(datavalues[0][0]);
                    }
                    conditions.Clear();
                    conditions.Add("log_id", log_id.ToString());
                    sret = SQLiteDriver.DBDelete("Table" + table_type.ToString(), conditions, ref row);
                    if (sret.I != 0) return sret.I;

                    sret = SQLiteDriver.DBDelete("Logs", conditions, ref row);
                    return sret.I;
                }
                else
                    return 0;
            }
        }

        #region support multiple device
        public static int NewLog(string module_name, string log_info, string timestamp, string device_num, ref int log_id)
        {
            lock (DB_Lock)
            {
                if (DBManager.supportdb == true)
                {
                    int module_id = DBManager.GetModuleIDFromModules(module_name);
                    if (module_id == -1)
                        return -1;
                    int table_type = DBManager.GetTableTypeFromTableTypes(Project_id, module_id);
                    if (table_type == -1)
                        return -1;

                    Dictionary<string, string> record = new Dictionary<string, string>();
                    record.Add("device_num", device_num);
                    record.Add("table_type", table_type.ToString());
                    record.Add("log_info", log_info);
                    record.Add("timestamp", timestamp);
                    int row = -1;
                    SQLiteResult sret = SQLiteDriver.DBInsertInto("Logs", record, ref row);
                    if (sret.I != 0)
                    {
                        //todo: add warning here
                        //MessageBox.Show(sret.Str);
                        return sret.I;
                    }
                    log_id = DBManager.GetLogIDFromLogs(table_type, timestamp);
                    if (log_id == -1)
                        return -1;
                    if (currentLogID.ContainsKey(device_num + module_name))
                        currentLogID[device_num + module_name] = log_id;
                    else
                        currentLogID.Add(device_num + module_name, log_id);
                    return sret.I;
                }
                else
                    return 0;
            }
        }
        public static Int32 BeginNewRow(string module_name, string device_num, Dictionary<string, string> record)
        {
            lock (DB_Lock)
            {
                if (DBManager.supportdb == true)
                {
                    if (!t.Enabled)
                    {
                        t.Interval = 15000;
                        t.Start();
                    }

                    int module_id;
                    //module_id = DBManager.GetModuleID(module_name);
                    var queryResults =
                        from module in Modules.AsEnumerable()
                        select module;
                    var rows =
                        queryResults.Where(p => p.Field<string>("module_name") == module_name);
                    module_id = (int)rows.ToArray()[0].Field<long>("module_id");

                    int table_type;
                    //table_type = DBManager.GetTableType(Project_id, module_id);
                    queryResults =
                        from tabletype in TableTypes.AsEnumerable()
                        select tabletype;
                    rows =
                        queryResults.Where(p => p.Field<long>("project_id") == Project_id);
                    rows =
                        rows.Where(p => p.Field<long>("module_id") == module_id);
                    table_type = (int)rows.ToArray()[0].Field<long>("table_type");

                    int log_id = currentLogID[device_num + module_name];

                    Dictionary<string, string> m_record = new Dictionary<string, string>();
                    m_record.Add("log_id", log_id.ToString());
                    foreach (string key in record.Keys)
                    {
                        m_record.Add(key, record[key]);
                    }
                    /*int row = -1;
                    SQLiteResult sret = SQLiteDriver.DBInsertInto("Table" + table_type.ToString(), m_record, ref row);
                    if (sret.I != 0)
                    {
                        //todo: add warning here
                        //MessageBox.Show(sret.Str);
                    }
                    return sret.I;*/
                    string sql = SQLiteDriver.SQLInsertInto("Table" + table_type.ToString(), m_record);
                    sqls.Add(sql);
                    return 0;
                }
                else
                    return 0;
            }
        }
        public static Int32 GetLogsInforV2(string module_name, ref List<List<string>> records)
        {
            lock (DB_Lock)
            {
                if (DBManager.supportdb == true)
                {
                    SQLiteResult sret;
                    if (sqls.Count != 0)
                    {
                        sret = SQLiteDriver.ExecuteNonQueryTransaction(sqls);
                        if (sret.I != 0)
                            return sret.I;
                        sqls.Clear();
                    }
                    int module_id = GetModuleIDFromModules(module_name);
                    if (module_id == -1)
                        return -1;
                    int table_type = GetTableTypeFromTableTypes(Project_id, module_id);
                    if (table_type == -1)
                        return -1;

                    Dictionary<string, string> conditions = new Dictionary<string, string>();
                    conditions.Add("table_type", table_type.ToString());
                    List<string> datacolumns = new List<string>();
                    datacolumns.Add("log_id");
                    datacolumns.Add("timestamp");
                    datacolumns.Add("device_num");

                    int row = -1;
                    List<List<string>> datavalues = new List<List<string>>();
                    sret = SQLiteDriver.DBSelect("Logs", conditions, datacolumns, ref datavalues, ref row);
                    if (sret.I == 0 && datavalues.Count != 0)
                    {
                        foreach (var datavalue in datavalues)
                        {
                            int log_id = Convert.ToInt32(datavalue[0]);
                            string timestamp = datavalue[1];
                            string device_num = datavalue[2];
                            int log_size = GetLogSize(log_id);
                            List<string> item = new List<string>();
                            item.Add(timestamp);
                            item.Add(log_size.ToString());
                            item.Add(device_num);
                            records.Add(item);
                        }
                        return sret.I;
                    }
                    else
                        return sret.I;
                }
                else
                    return 0;
            }
        }
        #endregion
        #endregion
    }
    /// <summary>
    /// Only used by Communication module
    /// </summary>
    public class CommunicationDBLog
    {
        private static Dictionary<string, DBManager.DataType> lststrComDBLogCol = new Dictionary<string, DBManager.DataType>();
        private static List<Dictionary<string, string>> buffer = new List<Dictionary<string, string>>();

        public static string[] strColHeader = { "DayTime", "CID", "ErrCode", "R/W", "I2CAddr", "RegIndex", "Data1", "Data2", "Data3", "ErrComments" };
        public static UInt32 uCounting;
        public static UInt16 uAmount = 200;

        public static void ComDBInit()
        {
            lststrComDBLogCol.Clear();
            foreach (string strHead in strColHeader)
            {
                lststrComDBLogCol.Add(strHead, DBManager.DataType.TEXT);
            }
            int ret = DBManager.CreateTableN("Com", lststrComDBLogCol);
            if (ret != 0)
                MessageBox.Show("Create Com Table Failed!");
            uCounting = 0;
        }

        //will be used in Device.cs
        //public static bool CreateComLogFile(string strIn = null)
        //{
            //bool bReturn = true;

            //return bReturn;
        //}

        public static void FlushBuffer2DB()
        {
            if (buffer.Count > 0)
            {
                int ret = DBManager.NewRows("Com", buffer);
                if (ret != 0)
                    MessageBox.Show("Com New Rows Failed!");
                buffer.Clear();
            }
        }

        public static void WriteDatatoDBLog(Dictionary<string, string> inRecord)
        {
            buffer.Add(inRecord);
            if (buffer.Count >= uAmount)
            {
                FlushBuffer2DB();
            }
        }

        public static void CompleteComDBLogFile()
        {
            FlushBuffer2DB();
        }

        public static void NewLog(string timestamp)
        {
            int log_id = -1;
            int ret = DBManager.NewLog("Com", "Com Log", timestamp, ref log_id);
            if (ret != 0)
                MessageBox.Show("New Com Log Failed!");
        }

    }

}
