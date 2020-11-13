using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.ComponentModel;
using Cobra.Common;
using Cobra.EM;

namespace Cobra.ProductionPanel
{
    public class CFGProcessItem
    {
        public string SubTaskID;

        public string Name;
    }

    public class CFGTestItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private string m_Name;
        public string Name
        {
            get { return m_Name; }
            set
            {
                m_Name = value;
                OnPropertyChanged("Name");
            }
        }

        private string m_Unit;
        public string Unit
        {
            get { return m_Unit; }
            set
            {
                m_Unit = value;
                OnPropertyChanged("Unit");
            }
        }

        private double m_StandardValue;
        public double StandardValue
        {
            get { return m_StandardValue; }
            set
            {
                if (m_StandardValue != value)
                {
                    m_StandardValue = value;
                    OnPropertyChanged("StandardValue");
                }
            }
        }

        private double m_MinValue;
        public double MinValue
        {
            get { return m_MinValue; }
            set
            {
                if (m_MinValue != value)
                {
                    m_MinValue = value;
                    OnPropertyChanged("MinValue");
                }
            }
        }

        private double m_MaxValue;
        public double MaxValue
        {
            get { return m_MaxValue; }
            set
            {
                if (m_MaxValue != value)
                {
                    m_MaxValue = value;
                    OnPropertyChanged("MaxValue");
                }
            }
        }

        private bool m_EnableTolerance;
        public bool EnableTolerance
        {
            get { return m_EnableTolerance; }
            set
            {
                if (m_EnableTolerance != value)
                {
                    m_EnableTolerance = value;
                    OnPropertyChanged("EnableTolerance");
                }
            }
        }

        private double m_Tolerance;
        public double Tolerance
        {
            get { return m_Tolerance; }
            set
            {
                if (m_Tolerance != value)
                {
                    m_Tolerance = value;
                    OnPropertyChanged("Tolerance");
                }
            }
        }

        private byte m_Group;
        public byte Group
        {
            get { return m_Group; }
            set
            {
                if (m_Group != value)
                {
                    m_Group = value;
                    OnPropertyChanged("Group");
                }
            }
        }

        private bool m_IsEnable;
        public bool IsEnable
        {
            get { return m_IsEnable; }
            set
            {
                if (m_IsEnable != value)
                {
                    m_IsEnable = value;
                    OnPropertyChanged("IsEnable");
                }
            }
        }

        private string m_GUID;
        public string GUID
        {
            get { return m_GUID; }
            set
            {
                if (m_GUID != value)
                {
                    m_GUID = value;
                    OnPropertyChanged("GUID");
                }
            }
        }

        public CFGTestItem()
        {
            Name = "";
            Unit = "";
            StandardValue = 0;
            MinValue = 0;
            MaxValue = 0;
            EnableTolerance = false;
            Tolerance = 0;
            GUID = "";
            Group = 0;
            IsEnable = true;
        }
    }

    public class CFGViewModel
    {
        private AsyncObservableCollection<CFGTestItem> m_TestItems = new AsyncObservableCollection<CFGTestItem>();
        public AsyncObservableCollection<CFGTestItem> TestItems
        {
            get { return m_TestItems; }
            set { m_TestItems = value; }
        }
    }
}
