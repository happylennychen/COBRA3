﻿using System;
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

namespace Cobra.MonitorPanel
{
    /// <summary>
    /// Interaction logic for CurrentPanel.xaml
    /// </summary>
    public partial class CurrentPanel : GroupBox
    {

        private MainControl m_parent;
        public MainControl parent
        {
            get { return m_parent; }
            set { m_parent = value; }
        }
        private CurrentPanelViewModel mcViewModel = new CurrentPanelViewModel();
        public CurrentPanelViewModel cViewModel
        {
            get { return mcViewModel; }
            set { mcViewModel = value; }
        }
        public CurrentPanel()
        {
            InitializeComponent();
            /*#region 假数据
            cViewModel.pCOCTH = 6000;
            cViewModel.pDOCTH = -8000;
            cViewModel.pValue = 3000;
            cViewModel.pCharge = true;
            cViewModel.pDischarge = false;
            cViewModel.pUsability = false;

            #endregion//*/
            cViewModel.pCOCTH = null;
            cViewModel.pDOCTH = null;
            cViewModel.pCharge = null;
            cViewModel.pDischarge = null;
            cViewModel.pUsability = true;   //初始隐藏
            cGroup.DataContext = cViewModel;
        }
    }

    public class CurrentPanelViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private double? mCOCTH;
        public double? pCOCTH
        {
            get { return mCOCTH; }
            set
            {
                if (mCOCTH != value)
                {
                    mCOCTH = value;
                    OnPropertyChanged("pCOCTH");
                }
            }
        }
        private double? mDOCTH;
        public double? pDOCTH
        {
            get { return mDOCTH; }
            set
            {
                if (mDOCTH != value)
                {
                    mDOCTH = value;
                    OnPropertyChanged("pDOCTH");
                }
            }
        }
        private double mValue;
        public double pValue
        {
            get { return Math.Round(mValue, 2); }
            set
            {
                if (mValue != value)
                {
                    mValue = value;
                    OnPropertyChanged("pValue");
                }
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
        private bool? mCharge;
        public bool? pCharge
        {
            get { return mCharge; }
            set
            {
                if (mCharge != value)
                {
                    mCharge = value;
                    OnPropertyChanged("pCharge");
                }
            }
        }
        private bool? mDischarge;
        public bool? pDischarge
        {
            get { return mDischarge; }
            set
            {
                if (mDischarge != value)
                {
                    mDischarge = value;
                    OnPropertyChanged("pDischarge");
                }
            }
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
    }
}
