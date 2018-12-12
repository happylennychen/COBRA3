using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Collections.ObjectModel;
using System.Data.SQLite;

namespace O2Micro.Cobra.Common
{
    /// <summary>
    /// MainControl.xaml 的交互逻辑
    /// </summary>
    public class SQLiteResult
    {
        public int I;   //出错时为-1，成功为0
        public string Str;  //出错信息
        public SQLiteResult(int i, string str)
        {
            I = i;
            Str = str;
        }
        public SQLiteResult(int i)
        {
            I = i;
            Str = "";
        }
    }
    public static class SQLiteDriver
    {
        public static string DB_Name = "";

        private static string path = "";
        public static string DB_Path
        {
            set
            {
                if (!value.EndsWith(@"\\"))
                    path = value + "\\";
                else
                    path = value;
            }
            get
            {
                return path;
            }
        }

        private static string connstr
        {
            get
            {
                return "Data Source=" + DB_Path + DB_Name;
            }
        }
        //private static SQLiteCommand cmd = null;
        #region 开关数据库并传递sql命令的基础操作函数
        public static SQLiteResult ExecuteNonQuery(string sql, ref int row)
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
            catch (Exception e)
            {
                //Do any logging operation here if necessary
                //MessageBox.Show(e.Message);
                return new SQLiteResult(-1, e.Message);
            }
            return new SQLiteResult(0);
        }
        public static SQLiteResult ExecuteNonQueryTransaction(List<string> sqls)
        {
            using (SQLiteConnection conn = new SQLiteConnection(connstr))
            {
                conn.Open();
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    /*cmd.CommandText = "begin transaction;";
                    cmd.ExecuteNonQuery();

                    foreach (var sql in sqls)
                    {
                        cmd.CommandText = sql;
                        cmd.ExecuteNonQuery();
                    }
                    try
                    {
                        cmd.CommandText = "commit;";
                        cmd.ExecuteNonQuery();
                    }
                    catch
                    {
                        cmd.CommandText = "rollback";
                        cmd.ExecuteNonQuery();
                        return new SQLiteResult(-1, e.Message);;
                    }*/
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
                        catch(Exception e)
                        {
                            trans.Rollback();
                            return new SQLiteResult(-1, e.Message);
                        }
                    }
                }
            }
            return new SQLiteResult(0);
        }
        public static SQLiteResult ExecuteSelect(string sql, ref DataTable dt, ref int row)
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
            catch (Exception e)
            {
                //Do any logging operation here if necessary
                //MessageBox.Show(e.Message);
                return new SQLiteResult(-1, e.Message);;
            }
            return new SQLiteResult(0);
        }
        public static SQLiteResult ExecuteReader(string sql, ref SQLiteDataReader dr)
        {
            try
            {
                SQLiteConnection conn = new SQLiteConnection(connstr);        //这里不能用using不然 caller后面的代码会执行不下去

                conn.Open();
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = sql;
                    dr = cmd.ExecuteReader(CommandBehavior.CloseConnection);
                    return new SQLiteResult(0);
                }
                //return LibErrorCode.IDS_ERR_SUCCESSFUL;
            }
            catch (Exception e)
            {
                //Do any logging operation here if necessary
                //return LibErrorCode.IDS_ERR_SECTION_SQLITE;
                //MessageBox.Show(e.Message);
                return new SQLiteResult(-1, e.Message);;
            }
        }
        #endregion

        #region 将任务翻译成sql，调用基础操作函数
        public static string PrepareString(string str)
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
        public static SQLiteResult DBCreateTable(string tablename, Dictionary<string, string> column)
        {
            string sql = "CREATE TABLE IF NOT EXISTS " + tablename + "(";
            foreach (string str in column.Keys)
            {
                sql += PrepareString(str) + " " + column[str] + ", ";
            }
            sql = sql.Remove(sql.Length - 2);
            sql += ");";
            int row = -1;
            return ExecuteNonQuery(sql, ref row);
        }
        public static SQLiteResult DBMultipleInsertInto(string tablename, List<Dictionary<string, string>> records)
        {
            List<string> sqls = new List<string>();
            foreach (var record in records)
            {
                List<string> tablecolumn = record.Keys.ToList<string>();
                List<string> values = record.Values.ToList<string>();
                string sql = "INSERT OR IGNORE INTO " + tablename + "(";
                foreach (string str in tablecolumn)
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
                //return SQLNonQuery(sql);
                sqls.Add(sql);
            }
            return ExecuteNonQueryTransaction(sqls);
        }
        public static string SQLInsertInto(string tablename, Dictionary<string, string> records)
        {
            List<string> columns = records.Keys.ToList<string>();
            List<string> values = records.Values.ToList<string>();
            return SQLInsertInto(tablename, columns, values);
        }
        public static SQLiteResult DBInsertInto(string tablename, Dictionary<string, string> records, ref int row)
        {
            List<string> columns = records.Keys.ToList<string>();
            List<string> values = records.Values.ToList<string>();
            string sql = SQLInsertInto(tablename, columns, values);
            return ExecuteNonQuery(sql, ref row);
        }
        public static string SQLInsertInto(string tablename, List<string> columns, List<string> values)
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
        public static SQLiteResult DBInsertInto(string tablename, List<string> columns, List<string> values, ref int row)
        {
            string sql = SQLInsertInto(tablename, columns, values);
            return ExecuteNonQuery(sql, ref row);
        }
        public static SQLiteResult DBUpdate(string tablename, Dictionary<string, string> condition, Dictionary<string, string> records, ref int row)
        {
            List<string> conditioncolumns = condition.Keys.ToList<string>();
            List<string> conditionvalues = condition.Values.ToList<string>();
            List<string> datacolumns = records.Keys.ToList<string>();
            List<string> datavalues = records.Values.ToList<string>();
            return DBUpdate(tablename, conditioncolumns, conditionvalues, datacolumns, datavalues, ref row);
        }
        public static SQLiteResult DBUpdate(string tablename, List<string> conditioncolumns, List<string> conditionvalues, List<string> datacolumns, List<string> datavalues, ref int row)
        {
            string sql = "UPDATE " + tablename + " SET ";
            for (int i = 0; i < datacolumns.Count; i++)
            {
                sql += PrepareString(datacolumns[i]) + "='" + datavalues[i] + "', ";
            }
            sql = sql.Remove(sql.Length - 2) + " WHERE ";
            for (int i = 0; i < conditioncolumns.Count; i++)
            {
                sql += PrepareString(conditioncolumns[i]) + "='" + conditionvalues[i] + "' AND ";
            }
            sql = sql.Remove(sql.Length - 5) + ";";
            return ExecuteNonQuery(sql, ref row);
        }
        public static SQLiteResult DBUpdateOrInsert(string tablename, Dictionary<string, string> condition, Dictionary<string, string> records, ref int row)
        {
            List<string> conditioncolumns = condition.Keys.ToList<string>();
            List<string> conditionvalues = condition.Values.ToList<string>();
            List<string> datacolumns = records.Keys.ToList<string>();
            List<string> values = records.Values.ToList<string>();
            return DBUpdateOrInsert(tablename, conditioncolumns, conditionvalues, datacolumns, values, ref row);
        }
        public static SQLiteResult DBUpdateOrInsert(string tablename, List<string> conditioncolumns, List<string> conditionvalues, List<string> datacolumns, List<string> datavalues, ref int row)
        {
            string sql = "UPDATE " + tablename + " SET ";
            for (int i = 0; i < datacolumns.Count; i++)
            {
                sql += PrepareString(datacolumns[i]) + "='" + datavalues[i] + "', ";
            }
            sql = sql.Remove(sql.Length - 2) + " WHERE ";
            for (int i = 0; i < conditioncolumns.Count; i++)
            {
                sql += PrepareString(conditioncolumns[i]) + "='" + conditionvalues[i] + "' AND ";
            }
            sql = sql.Remove(sql.Length - 5) + ";";
            SQLiteResult ret = ExecuteNonQuery(sql, ref row);
            if (row == 0 && ret.I == 0)    //row doesn't exist
            {
                Dictionary<string, string> records = new Dictionary<string, string>();
                for (int i = 0; i < conditioncolumns.Count; i++)
                {
                    records.Add(PrepareString(conditioncolumns[i]), conditionvalues[i]);
                }
                for (int i = 0; i < datacolumns.Count; i++)
                {
                    records.Add(PrepareString(datacolumns[i]), datavalues[i]);
                }
                return DBInsertInto(tablename, records, ref row);
            }
            return ret;
        }
        public static SQLiteResult DBSelect(string tablename, Dictionary<string, string> condition, List<string> datacolumns, ref List<List<string>> datavalues, ref int row)
        {
            SQLiteResult ret = new SQLiteResult(0);
            List<string> conditioncolumns = condition.Keys.ToList<string>();
            List<string> conditionvalues = condition.Values.ToList<string>();
            List<string> datavalue;
            string sql = "SELECT ";
            for (int i = 0; i < datacolumns.Count; i++)
            {
                sql += PrepareString(datacolumns[i]) + ", ";
            }
            sql = sql.Remove(sql.Length - 2) + " FROM " + tablename + " WHERE ";
            for (int i = 0; i < conditioncolumns.Count; i++)
            {
                sql += PrepareString(conditioncolumns[i]) + "='" + conditionvalues[i] + "' AND ";
            }
            sql = sql.Remove(sql.Length - 5) + ";";
            DataTable dt = new DataTable();
            ret = ExecuteSelect(sql, ref dt, ref row);
            if (ret.I == 0)
            {
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
            else
                return ret;
            return ret;
        }
        public static SQLiteResult DBSelect(string tablename, Dictionary<string, string> conditions, List<string> datacolumns, ref DataTable dt, ref int row)
        {
            SQLiteResult ret = new SQLiteResult(0);
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
            ret = ExecuteSelect(sql, ref dt, ref row);
            return ret;
        }

        public static SQLiteResult DBDelete(string tablename, Dictionary<string, string> conditions, ref int row)
        {
            SQLiteResult ret = new SQLiteResult(0);
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
            ret = ExecuteNonQuery(sql, ref row);
            return ret;
        }
        #endregion
    }
}
