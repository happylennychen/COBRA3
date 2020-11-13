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
    public partial class TemperaturePanel : GroupBox
    {

        private MainControl m_parent;
        public MainControl parent
        {
            get { return m_parent; }
            set { m_parent = value; }
        }
        private TemperaturePanelViewModel mtViewModel = new TemperaturePanelViewModel();
        public TemperaturePanelViewModel tViewModel
        {
            get { return mtViewModel; }
            set { mtViewModel = value; }
        }
        public TemperaturePanel()
        {
            InitializeComponent();
            /*
            #region 假数据
            tViewModel.pIOTTH = 80;
            tViewModel.pIUTTH = -30;
            for (int i = 0; i < 4; i++)
            {
                CellTemperature cellTemperature = new CellTemperature(tViewModel);
                cellTemperature.pIndex = i;
                cellTemperature.pValue = i + 32;
                cellTemperature.pTip = "Cell" + (i + 1).ToString() + " Temperature";
                cellTemperature.pUsability = false;
                cellTemperature.pLabel = "I" + i.ToString();
                tViewModel.itemperatureList.Add(cellTemperature);
            }
            tViewModel.itemperatureList[0].pValue = -60;
            tViewModel.itemperatureList[1].pValue = -30;
            tViewModel.itemperatureList[2].pValue = 80;
            tViewModel.itemperatureList[3].pValue = 150;

            tViewModel.pEOTTH = 70;
            tViewModel.pEUTTH = -20;
            for (int i = 0; i < 4; i++)
            {
                CellTemperature cellTemperature = new CellTemperature(tViewModel);
                cellTemperature.pIndex = i;
                cellTemperature.pValue = i + 32;
                cellTemperature.pTip = "Cell" + (i + 1).ToString() + " Temperature";
                cellTemperature.pUsability = false;
                cellTemperature.pLabel = "E" + i.ToString();
                tViewModel.etemperatureList.Add(cellTemperature);
            }
            tViewModel.etemperatureList[0].pValue = -20;
            tViewModel.etemperatureList[1].pValue = -60;
            tViewModel.etemperatureList[2].pValue = 150;
            tViewModel.etemperatureList[3].pValue = 70;


            #endregion
            //*/
            tViewModel.pIOTTH = null;
            tViewModel.pIUTTH = null;
            tViewModel.pEOTTH = null;
            tViewModel.pEUTTH = null;
            mainborder.DataContext = tViewModel;
            itcvs.DataContext = tViewModel.itemperatureList;
            etcvs.DataContext = tViewModel.etemperatureList;
            //vListBox1.DataContext = vViewModel.voltageList;
        }
    }

    public class CellTemperature : INotifyPropertyChanged
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
        private int mIndexGPIO;
        public int pIndexGPIO
        {
            get { return mIndexGPIO; }
            set
            {
                mIndexGPIO = value;
                OnPropertyChanged("pIndexGPIO");
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

        public CellTemperature(object parent)
        {
            pParent = parent; 
        }
    }
    public class TemperaturePanelViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }
        private double? mIOTTH;
        public double? pIOTTH
        {
            get { return mIOTTH; }
            set
            {
                if (mIOTTH != value)
                {
                    mIOTTH = value;
                    OnPropertyChanged("pIOTTH");
                }
            }
        }
        private double? mIUTTH;
        public double? pIUTTH
        {
            get { return mIUTTH; }
            set
            {
                if (mIUTTH != value)
                {
                    mIUTTH = value;
                    OnPropertyChanged("pIUTTH");
                }
            }
        }
        private double? mEOTTH;
        public double? pEOTTH
        {
            get { return mEOTTH; }
            set
            {
                if (mEOTTH != value)
                {
                    mEOTTH = value;
                    OnPropertyChanged("pEOTTH");
                }
            }
        }
        private double? mEUTTH;
        public double? pEUTTH
        {
            get { return mEUTTH; }
            set
            {
                if (mEUTTH != value)
                {
                    mEUTTH = value;
                    OnPropertyChanged("pEUTTH");
                }
            }
        }

        private AsyncObservableCollection<CellTemperature> m_itemperatureList = new AsyncObservableCollection<CellTemperature>();
        public AsyncObservableCollection<CellTemperature> itemperatureList
        {
            get { return m_itemperatureList; }
            set
            {
                m_itemperatureList = value;
                OnPropertyChanged("itemperatureList");
            }
        }
        private AsyncObservableCollection<CellTemperature> m_etemperatureList = new AsyncObservableCollection<CellTemperature>();
        public AsyncObservableCollection<CellTemperature> etemperatureList
        {
            get { return m_etemperatureList; }
            set
            {
                m_etemperatureList = value;
                OnPropertyChanged("etemperatureList");
            }
        }
    }
}
