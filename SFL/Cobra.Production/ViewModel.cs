using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.ComponentModel;
using Cobra.Common;
using Cobra.EM;
using System.Windows.Media;

namespace Cobra.ProductionPanel
{
    public class ProcessItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }


        private ushort m_SubTaskID;
        public ushort SubTaskID
        {
            get { return m_SubTaskID; }
            set
            {
                    m_SubTaskID = value;
            }
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

        private string m_Time;
        public string Time
        {
            get { return m_Time; }
            set
            {
                m_Time = value;
                OnPropertyChanged("Time");
            }
        }

        private string m_FailedDetail;
        public string FailedDetail
        {
            get { return m_FailedDetail; }
            set
            {
                m_FailedDetail = value;
                OnPropertyChanged("FailedDetail");
            }
        }

        private bool? m_IsSuccessed;
        public bool? IsSuccessed
        {
            get { return m_IsSuccessed; }
            set
            {
                    m_IsSuccessed = value;
            }
        }

        private Brush m_color;
        public Brush Color
        {
            get
            {
                return m_color;
            }
            set
            {
                m_color = value;
                OnPropertyChanged("Color");
            }
        }

        public delegate UInt32 CallBack(ushort sub_task);
        public CallBack callback;

        public ProcessItem(byte id, string name)
        {
            SubTaskID = id;
            Name = name;
            this.Color = Brushes.Gray;
        }

        public ProcessItem()
        {
        }
    }

    public class TestItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private string m_GUID;
        public string GUID
        {
            get { return m_GUID; }
            set
            {
                m_GUID = value;
            }
        }

        private double m_StandardValue;
        public double StandardValue
        {
            get { return m_StandardValue; }
            set
            {
                m_StandardValue = value;
            }
        }

        private bool? m_isPassed;
        public bool? isPassed
        {
            get { return m_isPassed; }
            set
            {
                m_isPassed = value;
            }
        }

        private double m_Tolerance;
        public double Tolerance
        {
            get { return m_Tolerance; }
            set
            {
                m_Tolerance = value;
            }
        }

        private double m_ReadResult;
        public double ReadResult
        {
            get { 
                return Math.Round(m_ReadResult, 2); 
            }
            set
            {
                m_ReadResult = value;
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
                }
            }
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

        private string m_FailedDetail;
        public string FailedDetail
        {
            get { return m_FailedDetail; }
            set
            {
                m_FailedDetail = value;
                OnPropertyChanged("FailedDetail");
            }
        }

        private Brush m_color;
        public Brush Color
        {
            get
            {
                return m_color;
            }
            set
            {
                m_color = value;
                OnPropertyChanged("Color");
            }
        }
    }

    public class TestGroup
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private bool? m_isPassed;
        public bool? isPassed
        {
            get { return m_isPassed; }
            set
            {
                m_isPassed = value;
            }
        }

        private Brush m_color;
        public Brush Color
        {
            get
            {
                return m_color;
            }
            set
            {
                m_color = value;
                OnPropertyChanged("Color");
            }
        }

        private byte m_GroupID;
        public byte GroupID
        {
            get { return m_GroupID; }
            set
            {
                if (m_GroupID != value)
                {
                    m_GroupID = value;
                    OnPropertyChanged("GroupID");
                }
            }
        }

        private AsyncObservableCollection<TestItem> m_TestItems = new AsyncObservableCollection<TestItem>();
        public AsyncObservableCollection<TestItem> TestItems
        {
            get { return m_TestItems; }
            set
            {
                m_TestItems = value;
                OnPropertyChanged("TestItems");
            }
        }
    }
}
