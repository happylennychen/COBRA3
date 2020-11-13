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
using System.ComponentModel;

namespace Cobra.SBSPanel
{
    /// <summary>
    /// Interaction logic for ThermalCurControl.xaml
    /// </summary>
    public class ConfigData : INotifyPropertyChanged
    {
        #region INotifyPropertyChanged 成员
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        void NotifyPropertyChange(string proper)
        {
            if (PropertyChanged == null)
                return;
            PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(proper));
        }
        #endregion

        public static ConfigData Instance = new ConfigData();
        public static ConfigData CCInstance = new ConfigData();

        private ConfigData() { }

        private bool m_Bit0 = false;
        public bool bit0
        {
            get { return m_Bit0; }
            set { m_Bit0 = value; NotifyPropertyChange("bit0"); }
        }

        private bool m_Bit1 = false;
        public bool bit1
        {
            get { return m_Bit1; }
            set { m_Bit1 = value; NotifyPropertyChange("bit1"); }
        }

        private bool m_Bit2 = false;
        public bool bit2
        {
            get { return m_Bit2; }
            set { m_Bit2 = value; NotifyPropertyChange("bit2"); }
        }

        private bool m_Bit3 = false;
        public bool bit3
        {
            get { return m_Bit3; }
            set { m_Bit3 = value; NotifyPropertyChange("bit3"); }
        }

        private bool m_Bit4 = false;
        public bool bit4
        {
            get { return m_Bit4; }
            set { m_Bit4 = value; NotifyPropertyChange("bit4"); }
        }

        private bool m_Bit5 = false;
        public bool bit5
        {
            get { return m_Bit5; }
            set { m_Bit5 = value; NotifyPropertyChange("bit5"); }
        }

        private string m_HConstant_Cur = "CC/2";
        public string hconstant_cur
        {
            get { return m_HConstant_Cur; }
            set { m_HConstant_Cur = value; NotifyPropertyChange("hconstant_cur"); }
        }

        private string m_Constant_Cur = "CC";
        public string constant_cur
        {
            get { return m_Constant_Cur; }
            set { m_Constant_Cur = value; NotifyPropertyChange("constant_cur"); }
        }

        private string m_Constant_Vol ="CV";
        public string constant_vol
        {
            get { return m_Constant_Vol; }
            set { m_Constant_Vol = value; NotifyPropertyChange("constant_vol"); }
        }

        private string m_CV_T34 = "CV_T34";
        public string cv_t34
        {
            get { return m_CV_T34; }
            set { m_CV_T34 = value; NotifyPropertyChange("cv_t34"); }
        }

        private string m_CV_T45 = "CV_T45";
        public string cv_t45
        {
            get { return m_CV_T45; }
            set { m_CV_T45 = value; NotifyPropertyChange("cv_t45"); }
        }        
    }

    public partial class ThermalControl : UserControl
    {
        public ThermalControl()
        {
            InitializeComponent();
        }

        public void Update(bool bcc,byte bdata,string[] dlist)
        {
            if (bcc)
            {
                for (int i = 0; i < 7; i++)
                {
                    switch (i)
                    {
                        case 0:
                            ConfigData.CCInstance.bit0 = ((bdata & (1 << i)) != 0) ? true : false;
                            break;
                        case 1:
                            ConfigData.CCInstance.bit1 = ((bdata & (1 << i)) != 0) ? true : false;
                            break;
                        case 2:
                            ConfigData.CCInstance.bit2 = ((bdata & (1 << i)) != 0) ? true : false;
                            break;
                        case 3:
                            ConfigData.CCInstance.bit3 = ((bdata & (1 << i)) != 0) ? true : false;
                            break;
                        case 4:
                            ConfigData.CCInstance.bit4 = ((bdata & (1 << i)) != 0) ? true : false;
                            break;
                        case 5:
                            ConfigData.CCInstance.bit5 = ((bdata & (1 << i)) != 0) ? true : false;
                            break;
                    }
                }
            }
            else
            {
                for (int i = 0; i < 7; i++)
                {
                    switch (i)
                    {
                        case 0:
                            ConfigData.CCInstance.bit0 = false;
                            break;
                        case 1:
                            ConfigData.CCInstance.bit1 = false;
                            break;
                        case 2:
                            ConfigData.CCInstance.bit2 = false;
                            break;
                        case 3:
                            ConfigData.CCInstance.bit3 = false;
                            break;
                        case 4:
                            ConfigData.CCInstance.bit4 = false;
                            break;
                        case 5:
                            ConfigData.CCInstance.bit5 = false;
                            break;
                    }
                }
            }

            for (int i = 0; i < 7; i++)
            {
                switch (i)
                {
                    case 0:
                        ConfigData.Instance.bit0 = ((bdata&(1 << i)) != 0) ? true : false; 
                        break;
                    case 1:
                        ConfigData.Instance.bit1 = ((bdata & (1 << i)) != 0) ? true : false; 
                        break;
                    case 2:
                        ConfigData.Instance.bit2 = ((bdata & (1 << i)) != 0) ? true : false; 
                        break;
                    case 3:
                        ConfigData.Instance.bit3 = ((bdata & (1 << i)) != 0) ? true : false; 
                        break;
                    case 4:
                        ConfigData.Instance.bit4 = ((bdata & (1 << i)) != 0) ? true : false; 
                        break;
                    case 5:
                        ConfigData.Instance.bit5 = ((bdata & (1 << i)) != 0) ? true : false; 
                        break;
                }
            }

            ConfigData.CCInstance.hconstant_cur = dlist[0];
            ConfigData.CCInstance.constant_cur = dlist[1];
            ConfigData.Instance.constant_vol = dlist[2];
            ConfigData.Instance.cv_t34 = dlist[3];
            ConfigData.Instance.cv_t45 = dlist[4];
        }
    }
}
