using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Cobra.Common;
using System.ComponentModel;
using System.Threading;

namespace Cobra.ScanPanel
{
    /// <summary>
    /// Interaction logic for VoltagePanel.xaml
    /// </summary>
    public partial class MiscPanel : GroupBox
    {

        private MainControl m_parent;
        public MainControl parent
        {
            get { return m_parent; }
            set { m_parent = value; }
        }

        private MiscPanelViewModel mmcViewModel = new MiscPanelViewModel();
        public MiscPanelViewModel pmcViewModel
        {
            get { return mmcViewModel; }
            set { mmcViewModel = value; }
        }

        public MiscPanel()
        {
            InitializeComponent();
            /*#region 假数据
            for (int i = 0; i < 20; i++)
            {
                SafetyEvent sevt = new SafetyEvent(pseViewModel);
                sevt.pValue = false;
                sevt.pLabel = i.ToString();
                sevt.pTip = "Cell" + (i + 1).ToString() + " Temperature";
                pseViewModel.SafetyEventList.Add(sevt);
            }
            #endregion*/
            mcGroup.DataContext = pmcViewModel.MiscList;
        }
    }

    public class Misc : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }
        private string mLabel;
        public string pLabel
        {
            get { return mLabel; }
            set
            {
                mLabel = value;
                OnPropertyChanged("pLabel");
            }
        }
        private string mTip;
        public string pTip
        {
            get { return mTip; }
            set
            {
                mTip = value;
                OnPropertyChanged("pTip");
            }
        }
        private int mIndex;
        public int pIndex
        {
            get { return mIndex; }
            set
            {
                mIndex = value;
                OnPropertyChanged("pIndex");
            }
        }
        private double mValue;
        public double pValue
        {
            get { return Math.Round(mValue, 2); }
            set
            {
                mValue = value;
                OnPropertyChanged("pValue");
            }
        }
        private object m_Parent;
        public object pParent
        {
            get { return m_Parent; }
            set { m_Parent = value; }
        }
        private bool _isRunning;
        public bool IsRunning
        {
            get { return _isRunning; }
            set
            {
                _isRunning = value;
                OnPropertyChanged("IsRunning");
            }
        }

        public Misc(object parent)
        {
            pParent = parent;
        }

        private Parameter mParam;
        public Parameter pParam
        {
            get { return mParam; }
            set { mParam = value; }
        }
    }
    public class MiscPanelViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private AsyncObservableCollection<Misc> m_Misc = new AsyncObservableCollection<Misc>();
        public AsyncObservableCollection<Misc> MiscList
        {
            get { return m_Misc; }
            set
            {
                m_Misc = value;
                OnPropertyChanged("MiscList");
            }
        }
    }
}
