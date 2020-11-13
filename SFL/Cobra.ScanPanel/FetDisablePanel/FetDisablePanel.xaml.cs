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

namespace Cobra.ScanPanel
{
    /// <summary>
    /// Interaction logic for VoltagePanel.xaml
    /// </summary>
    public partial class FetDisablePanel : GroupBox
    {
        public FetDisablePanelViewModel mfdViewModel = new FetDisablePanelViewModel();
        public FetDisablePanelViewModel pfdViewModel
        {
            get { return mfdViewModel; }
            set { mfdViewModel = value; }
        }
        public FetDisablePanel()
        {
            InitializeComponent();
            /*#region 假数据
            for (int i = 0; i < 20; i++)
            {
                FetDisable fd = new FetDisable(pfdViewModel);
                fd.pValue = false;
                fd.pLabel = i.ToString();
                fd.pTip = "Cell" + (i + 1).ToString() + " Temperature";
                fd.pTimer = null;
                pfdViewModel.FetDisableList.Add(fd);
            }
            for (int i = 0; i < 10; i++)
            {
                pfdViewModel.FetDisableList[i].pTimer = i;
            }
            #endregion*/
                fdGroup.DataContext = pfdViewModel.FetDisableList;
        }
    }
    public class FetDisable : INotifyPropertyChanged
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
        private Nullable<int> mTimer = null;
        public Nullable<int> pTimer
        {
            get { return mTimer; }
            set 
            {
                mTimer = value;
                OnPropertyChanged("pTimer");
            }
        }
        private string mTimerTip;
        public string pTimerTip
        {
            get { return mTimerTip; }
            set
            {
                mTimerTip = value;
                OnPropertyChanged("pTimerTip");
            }
        }
        private object m_Parent;
        public object pParent
        {
            get { return m_Parent; }
            set { m_Parent = value; }
        }

        public FetDisable(object parent)
        {
            pParent = parent;
        }
    }
    public class FetDisablePanelViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private AsyncObservableCollection<FetDisable> m_FetDisable = new AsyncObservableCollection<FetDisable>();
        public AsyncObservableCollection<FetDisable> FetDisableList
        {
            get { return m_FetDisable; }
            set
            {
                m_FetDisable = value;
                OnPropertyChanged("FetDisableList");
            }
        }
    }
}
