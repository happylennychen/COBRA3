using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Collections.ObjectModel;
using O2Micro.Cobra.Common;

namespace O2Micro.Cobra.SBS2Panel
{
    public class SFLModel : INotifyPropertyChanged
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
                if (m_Data != value)
                {
                    m_Data = value;
                    OnPropertyChanged("data");
                }
            }
        }

        private string m_sData;
        public string sdata
        {
            get { return m_sData; }
            set
            {
                if (m_sData != value)
                {
                    m_sData = value;
                    OnPropertyChanged("sdata");
                }
            }
        }

        private UInt32 m_Guid;
        public UInt32 guid
        {
            get { return m_Guid; }
            set { m_Guid = value; }
        }

        private UInt16 m_Type;
        public UInt16 type
        {
            get { return m_Type; }
            set { m_Type = value; }
        }

        private UInt16 m_Order;
        public UInt16 order
        {
            get { return m_Order; }
            set { m_Order = value; }
        }

        private Boolean m_Clickable;
        public Boolean bClickable
        {
            get { return m_Clickable; }
            set { m_Clickable = value; }
        }

        private UInt16 m_Format;
        public UInt16 format
        {
            get { return m_Format; }
            set { m_Format = value; }
        }

        private UInt32 m_ErrorCode;
        public UInt32 errorcode
        {
            get { return m_ErrorCode; }
            set { m_ErrorCode = value; }
        }

        private UInt16 m_Mode;
        public UInt16 mode
        {
            get { return m_Mode; }
            set { m_Mode = value; }
        }

        private AsyncObservableCollection<string> m_ItemList = new AsyncObservableCollection<string>();
        public AsyncObservableCollection<string> itemlist
        {
            get { return m_ItemList; }
            set
            {
                m_ItemList = value;
                OnPropertyChanged("itemlist");
            }
        }
    }
}
