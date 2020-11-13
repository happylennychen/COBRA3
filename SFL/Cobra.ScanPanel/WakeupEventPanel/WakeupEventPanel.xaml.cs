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
    public partial class WakeupEventPanel : GroupBox
    {
        private MainControl m_parent;
        public MainControl parent
        {
            get { return m_parent; }
            set { m_parent = value; }
        }

        public WakeupEventPanelViewModel mweViewModel = new WakeupEventPanelViewModel();
        public WakeupEventPanelViewModel pweViewModel
        {
            get { return mweViewModel; }
            set { mweViewModel = value; }
        }
        public WakeupEventPanel()
        {
            InitializeComponent();
            /*#region 假数据
            for (int i = 0; i < 4; i++)
            {
                WakeupEvent wkevt = new WakeupEvent(pweViewModel);
                wkevt.pValue = false;
                wkevt.pLabel = i.ToString();
                wkevt.pTip = "Cell" + (i + 1).ToString() + " Temperature";
                pweViewModel.WakeupEventList.Add(wkevt);
            }
            #endregion*/
            weGroup.DataContext = pweViewModel.WakeupEventList;
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Button btn = (Button)sender;
            ParamContainer tempContainer = new ParamContainer();
            //SafetyEvent setemp = new SafetyEvent();
            foreach (WakeupEvent we in pweViewModel.WakeupEventList)
            {
                if (we.pLabel.ToString() == btn.Content.ToString()) //首先找到对应的se
                {
                    if (we.pClearable == false)   //再看看可不可以清除
                        return;
                    if (we.pParam.phydata == 0)    //本身就为0，也不用清除
                        return;
                    //we.pParam.phydata = 0;
                    tempContainer.parameterlist.Add(we.pParam);
                    break;
                }
            }

            if (parent.msg.bgworker.IsBusy)
            {
                MessageBox.Show("Bus busy");
                return;
            }
            else
            {
                MessageBoxResult ret = MessageBox.Show("Do you want to clear this bit?", "", MessageBoxButton.YesNo);
                if (ret == MessageBoxResult.Yes)
                {
                    parent.ClearBit(tempContainer);
                    //Thread.Sleep(100);
                    parent.Read(tempContainer);
                    parent.ConvertHexToPhysical(tempContainer);
                    //parent.RefreshUI();
                    //if (setemp.pValue == false)
                    /*if(btn.Background == SystemColors.ControlBrush)
                        MessageBox.Show("Clear successed.");
                    else
                        MessageBox.Show("Clear failed.");*/
                    if (tempContainer.parameterlist[0].phydata == 0)
                        MessageBox.Show("Clear successed.");
                    else
                        MessageBox.Show("Clear failed.");
                }
                else if (ret == MessageBoxResult.No)
                {
                    return;
                }
            }
        }
    }
    public class WakeupEvent : INotifyPropertyChanged
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
        private bool mClearable;
        public bool pClearable
        {
            get { return mClearable; }
            set
            {
                mClearable = value;
                OnPropertyChanged("pClearable");
            }
        }
        private object m_Parent;
        public object pParent
        {
            get { return m_Parent; }
            set { m_Parent = value; }
        }

        public WakeupEvent(object parent)
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
    public class WakeupEventPanelViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private AsyncObservableCollection<WakeupEvent> m_WakeupEvent = new AsyncObservableCollection<WakeupEvent>();
        public AsyncObservableCollection<WakeupEvent> WakeupEventList
        {
            get { return m_WakeupEvent; }
            set
            {
                m_WakeupEvent = value;
                OnPropertyChanged("WakeupEventList");
            }
        }
    }
}
