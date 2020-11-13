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
using Cobra.EM;

namespace Cobra.ScanPanel
{
    /// <summary>
    /// Interaction logic for VoltagePanel.xaml
    /// </summary>
    public partial class VoltagePanel : GroupBox
    {

        private MainControl m_parent;
        public MainControl parent
        {
            get { return m_parent; }
            set { m_parent = value; }
        }

        private VoltagePanelViewModel mvViewModel=new VoltagePanelViewModel();
        public VoltagePanelViewModel vViewModel
        {
            get { return mvViewModel; }
            set { mvViewModel = value; }
        }
        public VoltagePanel()
        {
            InitializeComponent();
            /*#region 假数据
            vViewModel.pOVTH = 3500;
            vViewModel.pUVTH = 3000;
            for (int i = 0; i < 20; i++)
            {
                CellVoltage cellVoltage = new CellVoltage(vViewModel);
                cellVoltage.pIndex = i;
                cellVoltage.pValue = i + 3200;
                cellVoltage.pTip = "Cell" + (i + 1).ToString() + " Voltage";
                if (i > 17)
                    cellVoltage.pUsability = true;
                else
                    cellVoltage.pUsability = false;
                vViewModel.voltageList.Add(cellVoltage);
            }
            vViewModel.voltageList[0].pValue = 2000;
            vViewModel.voltageList[1].pValue = 3000;
            vViewModel.voltageList[2].pValue = 3500;
            vViewModel.voltageList[3].pValue = 5000;


            #endregion*/
            vViewModel.pOVTH = null;
            vViewModel.pUVTH = null;
            vGroup.DataContext = vViewModel.voltageList;
            //vListBox1.ItemsSource = vViewModel.voltageList;
            //vListBox2.ItemsSource = vViewModel.voltageList;
            //totalTxtBx.DataContext = vViewModel;
            //vListBox1.DataContext = vViewModel.voltageList;
        }
    }

    public class CellVoltage : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private bool mUsability;
        public bool pUsability
        {
            get { return mUsability; }
            set
            {
                mUsability = value;
                OnPropertyChanged("pUsability");
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
            get {
                if (pUsability == true)
                    return double.NaN;
                return Math.Round(mValue, 2); 
            }
            set
            {
                mValue = value;
                OnPropertyChanged("pValue");
            }
        }
        private double? mMinValue;
        public double? pMinValue
        {
            get { return mMinValue; }
            set
            {
                mMinValue = value;
                OnPropertyChanged("pMinValue");
            }
        }
        private double? mMaxValue;
        public double? pMaxValue
        {
            get { return mMaxValue; }
            set
            {
                mMaxValue = value;
                OnPropertyChanged("pMaxValue");
            }
        }
        private bool? mBleeding;
        public bool? pBleeding  //初始化时是null
        {
            get { return mBleeding; }
            set
            {
                mBleeding = value;
                OnPropertyChanged("pBleeding");
            }
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


        private object m_Parent;
        public object pParent
        {
            get { return m_Parent; }
            set { m_Parent = value; }
        }

        public CellVoltage(object parent)
        {
            pParent = parent; 
        }
    }
    public class VoltagePanelViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private double? mOVTH;
        public double? pOVTH
        {
            get { return mOVTH; }
            set
            {
                if (mOVTH != value)
                {
                    mOVTH = value;
                    OnPropertyChanged("pOVTH");
                }
            }
        }
        private double? mUVTH;
        public double? pUVTH
        {
            get { return mUVTH; }
            set
            {
                if (mUVTH != value)
                {
                    mUVTH = value;
                    OnPropertyChanged("pUVTH");
                }
            }
        }
        /*
        private double mCellNum;
        public double pCellNum
        {
            get { return mCellNum; }
            set
            {
                if (mCellNum != value)
                {
                    mCellNum = value;
                    OnPropertyChanged("pCellNum");
                }
            }
        }*/

        private AsyncObservableCollection<CellVoltage> m_voltageList = new AsyncObservableCollection<CellVoltage>();
        public AsyncObservableCollection<CellVoltage> voltageList
        {
            get { return m_voltageList; }
            set
            {
                m_voltageList = value;
                OnPropertyChanged("voltageList");
            }
        }
        public VoltagePanelViewModel(object parent)
        {
        }
        public VoltagePanelViewModel()
        {
        }
    }
}
