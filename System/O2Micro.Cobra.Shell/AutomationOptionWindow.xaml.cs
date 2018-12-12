using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using O2Micro.Cobra.Common;
using O2Micro.Cobra.EM;

namespace O2Micro.Cobra.Shell
{
    /// <summary>
    /// Interaction logic for AutomationOptionWindow.xaml
    /// </summary>
    public partial class AutomationOptionWindow : Window
    {
        private AsyncObservableCollection<AutomationElement> ATMSettingDisplay = null;

        private MainWindow m_wndParent;
        public MainWindow wndParent
        {
            get { return m_wndParent; }
            set { m_wndParent = value; }
        }

        public AutomationOptionWindow(object objParent)
        {
            InitializeComponent();
            m_wndParent = (MainWindow)objParent;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (Registry.busoptionslist.Count < 1)
            {
                MessageBox.Show("Please select at least one device.", "Error");
            }
            else
            {
                ATMSettingDisplay = Registry.busoptionslist[0].AtMationSettingList; //TBD, get 1st one BusOption, currently support 1 
            }
            dtagrdATMOption.ItemsSource = ATMSettingDisplay;
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            double dbTemp = 0;

            foreach(AutomationElement atmElem in ATMSettingDisplay)
            {
                if (atmElem.u16EditType == (int)UI_TYPE.TextBox_Type)
                {
                    if (!double.TryParse(atmElem.strDisplayValue, System.Globalization.NumberStyles.Integer, null, out dbTemp))
                    {
                        dbTemp = 1;
                    }
                }
                else if(atmElem.u16EditType == (int)UI_TYPE.ComboBox_Type)
                {
                    dbTemp = atmElem.iCbxIndex;
                }
                else if(atmElem.u16EditType == (int)UI_TYPE.CheckBox_Type)
                {
                    dbTemp = atmElem.iCbxIndex;
                }
                else
                {   //for case
                    dbTemp = 0;
                }

                atmElem.dbValue = dbTemp;
            }
            m_wndParent.m_EM_Lib.CreateInterface(); //check simulation setting in Communication Layer
            Hide();
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (ATMSettingDisplay != null)
            {
                foreach (AutomationElement atmElem in ATMSettingDisplay)
                {
                    atmElem.SynchdbValueToDisplay();
                }
            }
            Hide();
            Close();
        }

        private void txtboxIn_KeyDown(object sender, KeyEventArgs e)
        {
            TextBox txtbx = sender as TextBox;

            if ((e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9) ||
                (e.Key == Key.Decimal) || (e.Key == Key.Subtract) || (e.Key == Key.Delete) ||
                ((e.Key >= Key.D0) && (e.Key <= Key.D9)))
            {
                {
                    e.Handled = false;
                    return;
                }
                //e.Handled = false;
            }
            else if (e.Key == Key.Enter)
            {
                txtbx.RaiseEvent(new RoutedEventArgs(ButtonBase.LostFocusEvent));
                e.Handled = false;
                return;
            }
            e.Handled = true;
        }

        private void txtboxIn_LostFocus(object sender, RoutedEventArgs e)
        {
            double dbtmp = 0;
            TextBox txtbx = sender as TextBox;
            ContentPresenter cntpretTmp = (ContentPresenter)txtbx.TemplatedParent;
            AutomationElement atmTemp = (AutomationElement)cntpretTmp.Content;

            if (!double.TryParse(txtbx.Text, System.Globalization.NumberStyles.Integer, null, out dbtmp))
            {
                MessageBox.Show("Input value error, please check it.", "Error");
                return;
            }

            if ((dbtmp > atmTemp.dbMaxvalue) || (dbtmp < atmTemp.dbMinvalue))
            {
                MessageBox.Show(string.Format("Input value should be between {0:f} and {1:f}, please check it.", atmTemp.dbMinvalue, atmTemp.dbMaxvalue),
                    "Error");
            }
            else
            {
            }
            return;
        }
    }
}
