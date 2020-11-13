using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Collections.ObjectModel;
using Cobra.Common;

namespace Cobra.SBSPanel
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

    public class PathModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private string m_Name;
        public string name
        {
            get { return m_Name; }
            set { m_Name = value; }
        }

        private string m_Path;
        public string path
        {
            get { return m_Path; }
            set
            {
                if (m_Path != value)
                {
                    m_Path = value;
                    OnPropertyChanged("path");
                }
            }
        }

        private string m_BtnCommand;
        public string btncommand
        {
            get { return m_BtnCommand; }
            set
            {
                if (m_BtnCommand != value)
                {
                    m_BtnCommand = value;
                    OnPropertyChanged("btncommand");
                }
            }
        }
    }

    public class ParamModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private string m_Name;
        public string name
        {
            get { return m_Name; }
            set { m_Name = value; }
        }

        private float m_Dval;
        public float dval
        {
            get { return m_Dval; }
            set
            {
                m_Dval = value;
                OnPropertyChanged("dval");
            }
        }

        private string m_Units;
        public string units
        {
            get { return m_Units; }
            set
            {
                m_Units = value;
                OnPropertyChanged("units");
            }
        }
    }

    public class RCObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        //参数名称
        //GUID
        private UInt16 m_ID;
        public UInt16 id
        {
            get { return m_ID; }
            set { m_ID = value; }
        }

        //电压
        private double m_DVol;
        public double dvol
        {
            get { return m_DVol; }
            set { m_DVol = value; }
        }

        //电流
        private double m_DCur;
        public double dcur
        {
            get { return m_DCur; }
            set { m_DCur = value; }
        }

        //温度
        private double m_DTemp;
        public double dtemp
        {
            get { return m_DTemp; }
            set { m_DTemp = value; }
        }

        //RSOC
        private double m_DRsoc;
        public double drsoc
        {
            get { return m_DRsoc; }
            set { m_DRsoc = value; }
        }

        private double m_DRsocFromCVT;
        public double drsocfromcvt
        {
            get { return m_DRsocFromCVT; }
            set { m_DRsocFromCVT = value; }
        }

        private double m_DRsocDiff;
        public double drsocdiff
        {
            get { return m_DRsocDiff; }
            set { m_DRsocDiff = value; }
        }

        //MAH2
        private double m_DMAH2;
        public double dmah2
        {
            get { return m_DMAH2; }
            set { m_DMAH2 = value; }
        }

        //RC
        private double m_RC;
        public double drc
        {
            get { return m_RC; }
            set { m_RC = value; }
        }
    }
}
