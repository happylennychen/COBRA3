using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Collections.ObjectModel;
using Cobra.Common;

namespace Cobra.TrimPanel
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

        private UInt32 m_Guid;
        public UInt32 guid
        {
            get { return m_Guid; }
            set { m_Guid = value; }
        }

        //参数在SFL参数列表中位置
        private Int32 m_Order;
        public Int32 order
        {
            get { return m_Order; }
            set { m_Order = value; }
        }

        private UInt16 m_Format;
        public UInt16 format
        {
            get { return m_Format; }
            set { m_Format = value; }
        }

        private string m_Description;
        public string description
        {
            get { return m_Description; }
            set { m_Description = value; }
        }

        private UInt32 m_ErrorCode;
        public UInt32 errorcode
        {
            get { return m_ErrorCode; }
            set { m_ErrorCode = value; }
        }

        private UInt16 m_RetryTime;
        public UInt16 retry_time 
        {
            get { return m_RetryTime; }
            set { m_RetryTime = value; }
        }

        private UInt16 m_SubType;
        public UInt16 subType
        {
            get { return m_SubType; }
            set { m_SubType = value; }
        }

        private Parameter m_Offset_Relation = new Parameter();
        public Parameter offset_relation
        {
            get { return m_Offset_Relation; }
            set
            {
                m_Offset_Relation = value;
                OnPropertyChanged("offset_relation");
            }
        }

        private Parameter m_Slope_Relation = new Parameter();
        public Parameter slope_relation
        {
            get { return m_Slope_Relation; }
            set
            {
                m_Slope_Relation = value;
                OnPropertyChanged("slope_relation");
            }
        }
    }
}
