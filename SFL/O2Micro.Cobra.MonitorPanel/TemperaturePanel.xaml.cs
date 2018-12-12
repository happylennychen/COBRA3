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
using O2Micro.Cobra.Common;
using System.ComponentModel;

namespace O2Micro.Cobra.MonitorPanel
{
    /// <summary>
    /// Interaction logic for VoltagePanel.xaml
    /// </summary>
    public partial class VoltagePanel : UserControl
    {
        private VoltagePanelViewModel mvViewModel = new VoltagePanelViewModel();
        public VoltagePanelViewModel vViewModel
        {
            get { return mvViewModel; }
            set { mvViewModel = value; }
        }
        public VoltagePanel()
        {
            InitializeComponent();
            #region 假数据
            vViewModel.pOVTH = 3500;
            vViewModel.pUVTH = 3000;
            for (int i = 0; i < 20; i++)
            {
                CellVoltage cellVoltage = new CellVoltage();
                cellVoltage.pIndex = i;
                cellVoltage.pValue = i + 3200;
                cellVoltage.pTip = "Cell" + (i + 1).ToString() + " Voltage";
                cellVoltage.pUsability = ((i % 2) == 1);
                #region 逼不得已
                cellVoltage.pOVTH = vViewModel.pOVTH;
                cellVoltage.pUVTH = vViewModel.pUVTH;
                #endregion
                vViewModel.voltageList.Add(cellVoltage);
            }
            vViewModel.voltageList[0].pValue = 2000;
            vViewModel.voltageList[1].pValue = 3000;
            vViewModel.voltageList[2].pValue = 3500;
            vViewModel.voltageList[3].pValue = 5000;


            #endregion
            vGroup.DataContext = vViewModel.voltageList;
            totalTxtBx.DataContext = vViewModel;
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
            get { return mValue; }
            set
            {
                mValue = value;
                OnPropertyChanged("pValue");
            }
        }

        #region 逼不得已
        private double mOVTH;
        public double pOVTH
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
        private double mUVTH;
        public double pUVTH
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
        #endregion
    }
    public class VoltagePanelViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }
        private double mOVTH;
        public double pOVTH
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
        private double mUVTH;
        public double pUVTH
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
    }
}
