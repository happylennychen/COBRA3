using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using System.ComponentModel;
using System.Collections.ObjectModel;
using Cobra.Common;

namespace Cobra.SCSPanel
{
    public class Model : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private Parameter m_Parent;
        public Parameter parent
        {
            get { return m_Parent; }
            set { m_Parent = value; }
        }

        private UInt32 m_Guid;
        public UInt32 guid
        {
            get { return m_Guid; }
            set { m_Guid = value; }
        }

        private string m_NickName;
        public string nickname
        {
            get { return m_NickName; }
            set { m_NickName = value; }
        }

        private double m_Data;
        public double data
        {
            get { return m_Data; }
            set
            {
                m_Data = value;
                OnPropertyChanged("data");
            }
        }

        //参数CMD标签
        private UInt16 m_Index;
        public UInt16 index
        {
            get { return m_Index; }
            set { m_Index = value; }
        }

        private string m_sPhydata;
        public string sphydata
        {
            get { return m_sPhydata; }
            set
            {
                m_sPhydata = value;
                OnPropertyChanged("sphydata");

            }
        }

        private UInt32 m_ErrorCode;
        public UInt32 errorcode
        {
            get { return m_ErrorCode; }
            set { m_ErrorCode = value; }
        }
    }

    public class DataBaseRecord : INotifyPropertyChanged
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
