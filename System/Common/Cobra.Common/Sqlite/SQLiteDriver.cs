using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Data.SQLite;
using System.IO;
using Cobra.Common;

namespace Cobra.Common
{
    /// <summary>
    /// MainControl.xaml 的交互逻辑
    /// </summary>
    public static class SQLiteDriver
    {
        private static object SQL_Lock = new object();
        public static string DB_Name = "Cobra.db3";
        public static string DB_Path = Path.Combine(FolderMap.m_projects_folder, @"Database\");
        private static string connstr
        {
            get
            {
                return string.Format("{0}{1}{2}", "Data Source=", DB_Path, DB_Name);
            }
        }

        #region 开关数据库并传递sql命令的基础操作函数
        public static void ExecuteNonQuery(string sql, ref int row)
        {
            lock (SQL_Lock)
            {
                try
                {
                    using (SQLiteConnection conn = new SQLiteConnection(connstr))
                    {

                        conn.Open();
                        using (SQLiteCommand cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = sql;
                            row = cmd.ExecuteNonQuery();
                        }
                    }
                }
                catch(Exception e)
                {
                    throw;
                }
            }
        }
        public static void ExecuteNonQueryTransaction(List<string> sqls)
        {
            lock (SQL_Lock)
            {
                using (SQLiteConnection conn = new SQLiteConnection(connstr))
                {
                    conn.Open();
                    using (SQLiteCommand cmd = conn.CreateCommand())
                    {
                        using (SQLiteTransaction trans = conn.BeginTransaction())
                        {
                            foreach (var sql in sqls)
                            {
                                cmd.CommandText = sql;
                                cmd.ExecuteNonQuery();
                            }
                            try
                            {
                                trans.Commit();
                            }
                            catch
                            {
                                trans.Rollback();
                                throw;
                            }
                        }
                    }
                }
            }
        }
        private static void ExecuteSelect(string sql, ref DataTable dt, ref int row)
        {
            lock (SQL_Lock)
            {
                try
                {
                    using (SQLiteConnection conn = new SQLiteConnection(connstr))
                    {

                        conn.Open();
                        using (SQLiteCommand cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = sql;
                            SQLiteDataAdapter da = new SQLiteDataAdapter(cmd);
                            row = da.Fill(dt);
                        }
                    }
                }
                catch
                {
                    throw;
                }
            }
        }
        #endregion

        private static string PrepareString(string str)
        {
            str = str.Replace('!', '_');
            str = str.Replace('@', '_');
            str = str.Replace('#', '_');
            str = str.Replace('$', '_');
            str = str.Replace('%', '_');
            str = str.Replace('^', '_');
            str = str.Replace('&', '_');
            str = str.Replace('*', '_');
            str = str.Replace('\\', '_');
            str = str.Replace('/', '_');
            str = str.Replace('(', '_');
            str = str.Replace(')', '_');
            return str.Replace(' ', '_');
        }

        #region SQL insert
        public static string SQLInsertInto(string tablename, Dictionary<string, string> records)
        {
            lock (SQL_Lock)
            {
                List<string> columns = records.Keys.ToList<string>();
                List<string> values = records.Values.ToList<string>();
                return SQLInsertInto(tablename, columns, values);
            }
        }
        public static void DBInsertInto(string tablename, Dictionary<string, string> records, ref int row)
        {
            lock (SQL_Lock)
            {
                List<string> columns = records.Keys.ToList<string>();
                List<string> values = records.Values.ToList<string>();
                string sql = SQLInsertInto(tablename, columns, values);
                ExecuteNonQuery(sql, ref row);
            }
        }
        private static string SQLInsertInto(string tablename, List<string> columns, List<string> values)
        {
            string sql = "INSERT OR IGNORE INTO " + tablename + "(";
            foreach (string str in columns)
            {
                sql += PrepareString(str) + ", ";
            }
            sql = sql.Remove(sql.Length - 2);
            sql += ") VALUES ('";
            foreach (string str in values)
            {
                sql += str + "', '";
            }
            sql = sql.Remove(sql.Length - 3);
            sql += ");";
            return sql;
        }
        #endregion

        #region DB Operation
        public static void DBSelect(string tablename, Dictionary<string, string> conditions, List<string> datacolumns, ref List<List<string>> datavalues, ref int row)
        {
            lock (SQL_Lock)
            {
                DataTable dt = new DataTable();
                DBSelect(tablename, conditions, datacolumns, ref dt, ref row);
                List<string> datavalue;
                foreach (DataRow dr in dt.Rows)
                {
                    datavalue = new List<string>();
                    foreach (DataColumn column in dt.Columns)
                    {
                        datavalue.Add(dr[column].ToString());
                    }
                    datavalues.Add(datavalue);
                }
            }
        }
        public static void DBSelect(string tablename, Dictionary<string, string> conditions, List<string> datacolumns, ref DataTable dt, ref int row)
        {
            lock (SQL_Lock)
            {
                string sql = "SELECT ";
                if (datacolumns == null)
                {
                    sql += "*";
                }
                else
                {
                    for (int i = 0; i < datacolumns.Count; i++)
                    {
                        sql += PrepareString(datacolumns[i]) + ", ";
                    }
                    sql = sql.Remove(sql.Length - 2);
                }
                sql = sql + " FROM " + tablename;
                if (conditions != null)
                {
                    sql += " WHERE ";
                    List<string> conditioncolumns = conditions.Keys.ToList<string>();
                    List<string> conditionvalues = conditions.Values.ToList<string>();
                    for (int i = 0; i < conditioncolumns.Count; i++)
                    {
                        sql += PrepareString(conditioncolumns[i]) + "='" + conditionvalues[i] + "' AND ";
                    }
                    sql = sql.Remove(sql.Length - 5) + ";";
                }
                else
                {
                    sql += ";";
                }
                //DataTable dt = new DataTable();
                ExecuteSelect(sql, ref dt, ref row);
            }
        }
        public static void DBDelete(string tablename, Dictionary<string, string> conditions, ref int row)
        {
            lock (SQL_Lock)
            {
                string sql = "DELETE FROM " + tablename;
                if (conditions != null)
                {
                    sql += " WHERE ";
                    List<string> conditioncolumns = conditions.Keys.ToList<string>();
                    List<string> conditionvalues = conditions.Values.ToList<string>();
                    for (int i = 0; i < conditioncolumns.Count; i++)
                    {
                        sql += PrepareString(conditioncolumns[i]) + "='" + conditionvalues[i] + "' AND ";
                    }
                    sql = sql.Remove(sql.Length - 5) + ";";
                }
                else
                {
                    sql += ";";
                }
                //DataTable dt = new DataTable();
                ExecuteNonQuery(sql, ref row);
            }
        }
        #endregion
    }
}
