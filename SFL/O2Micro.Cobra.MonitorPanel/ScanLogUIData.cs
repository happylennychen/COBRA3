using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using O2Micro.Cobra.Common;
using System.Data;
using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace O2Micro.Cobra.MonitorPanel
{
    public struct LogParam
    {
        public string name;
        public string group;
    }

    public class KV : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }
        private string mKey;
        public string pKey
        {
            get { return mKey; }
            set
            {
                mKey = value;
                OnPropertyChanged("pKey");
            }
        }
        private bool mValue;
        public bool pValue
        {
            get { return mValue; }
            set
            {
                mValue = value;
                OnPropertyChanged("pValue");
            }
        }
    }

    public class ScanLogUIData : LogUIData
    {
        public void BuildColumn(List<LogParam> paramlist, bool isWithTime)  //从param list创建Column,Caption中包含Group信息
        {
            DataColumn col;
            foreach (LogParam param in paramlist)
            {
                col = new DataColumn();
                col.DataType = System.Type.GetType("System.String");
                col.ColumnName = param.name;
                col.AutoIncrement = false;
                col.Caption = param.group;
                col.ReadOnly = false;
                col.Unique = false;
                //col.Expression = param.group;
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
                //col.Expression = "";
                logbuf.Columns.Add(col);
            }
        }

        /*public void BuildColumn(List<string> strlist, bool isWithTime)
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
                if (str.Contains("mV"))
                    col.Caption = "1";
                else if (str.Contains("Current"))
                    col.Caption = "2";
                else if (str.Contains("Temperature"))
                    col.Caption = "3";
                else
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
        }*/
        public void LoadFromFile(string filename, List<LogParam> paramlist)   //Load cvs file to data table
        {
            logbuf.Clear();
            logbuf.Columns.Clear();

            FileStream file = new FileStream(filename, FileMode.Open);
            if (file.Length < 1000)
            {
                StreamReader sr = new StreamReader(file);
                List<string> strlist = new List<string>();
                strlist = GetCSVStrList(sr.ReadLine());
                //BuildColumn(strlist, false);
                BuildColumn(paramlist, true);
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
                //BuildColumn(strlist, false);
                BuildColumn(paramlist, true);
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
                        //BuildColumn(strlist, false);
                        BuildColumn(paramlist, true);
                        isFirst = false;
                        continue;
                    }
                    AddRow(strlist);
                }
            }
        }
        ObservableCollection<KV> m_isDisplay = new ObservableCollection<KV>();
        public ObservableCollection<KV> isDisplay = new ObservableCollection<KV>();

        public void BuildIsDisplay(List<LogParam> paramlist)
        {
            foreach (LogParam param in paramlist)
            {
                int flag = 1;
                foreach (KV k in isDisplay)
                {
                    if (k.pKey == param.name)
                        flag = 0;
                }
                if (flag == 1)
                {
                    KV kv = new KV();
                    kv.pKey = param.name;
                    kv.pValue = true;
                    isDisplay.Add(kv);
                }
            }

            int f = 1;
            foreach (KV v in isDisplay)
            {
                if (v.pKey == "Time")
                    f = 0;
            }
            if (f == 1)
            {
                KV kv1 = new KV();
                kv1.pKey = "Time";
                kv1.pValue = true;
                isDisplay.Add(kv1);
            }
        }
    }
}
