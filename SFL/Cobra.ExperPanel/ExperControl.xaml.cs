using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Linq;
using System.Xml.Linq;
using System.IO;
using System.Reflection;
using System.ComponentModel;
using System.Globalization;
using System.Xml;
using System.Threading;
using Cobra.EM;
using Cobra.Common;

namespace Cobra.ExperPanel
{
	/// <summary>
	/// Interaction logic for ExperControl.xaml
	/// </summary>
    public partial class ExperControl
    {
        #region private members

        private TASKMessage m_tskmsgExper = new TASKMessage();	//Device working thread
        private GeneralMessage m_gnlmsgExper = new GeneralMessage("Exper SFL", "", 2);	//General Message object for Warning Control 
        private ControlMessage m_ctlmsgExper = new ControlMessage();	//Control Message object for Waiting Control
        private Device devParent { get; set; }	//save Device handler reference
        private ExperViewMode myViewMode { get; set; }	//data and action model
        private Int16 uTglNumber = 0;
        bool bRepeatBtn = false;

        #endregion

        #region public members

        public string sflname { get; set; }	//save sfl name string
        public bool bEngModeRunning { get; set; }	//ExperViewMode will refer this value to show/hide controls about Engineer/Customer Mode
        public bool bFranTestMode { get; set; }	//Evaluation controlled by francis

        //use to bind with UI button, like WriteAll, ReadOne, WriteOne ...etc
        public static readonly DependencyProperty ButtonExperProperty = DependencyProperty.Register(
            "bBtnExper", typeof(bool), typeof(ExperControl));
        public bool bBtnExper
        {
            get { return (bool)GetValue(ButtonExperProperty); }
            set { SetValue(ButtonExperProperty, value); }
        }

        public bool bSharedPublic { get; set; }     //true, parse public property to get Addrss, BitStart, BitlLength, id=547
        public bool bForceHidePro { get; set; }     //true, force to hide "Pro" button, otherwise, show it
        public byte yBitTotal { get; set; }         //0x08 or 0x10, it indicates bit length of chip register

        #endregion

        #region Constructor/Destructor

        // <summary>
        // Constructor, initialize components, set up SFL name, Property, and controls' binding source
        // </summary>
        // <param name="pParent"></param>
        // <param name="name"></param>
        public ExperControl(object pParent, string name)
        {
            InitializeComponent();

            #region Initialization of private/public members

            devParent = (Device)pParent;
            if (devParent == null) return;

            sflname = name;
            if (String.IsNullOrEmpty(sflname)) return;
            sflname = name;
            if (string.IsNullOrEmpty(sflname)) return;

            //gnlmsgProduct.PropertyChanged += new PropertyChangedEventHandler(m_gm_PropertyChanged);
            m_tskmsgExper.PropertyChanged += new PropertyChangedEventHandler(tskmsg_PropertyChanged);
            m_tskmsgExper.gm.sflname = name;
            m_tskmsgExper.gm.level = 2;
            m_tskmsgExper.owner = this;

            ExperWaitControl.SetParent(grdExperParent);
            ExperWarnMsg.SetParent(grdExperParent);

            #endregion

            bEngModeRunning = false;	//running on Customer mode
            bFranTestMode = false;
            ReadSettingFromExtDescrip();
            myViewMode = new ExperViewMode(pParent, this);	//creat data
            //dtgRegistersPresent.ItemsSource = myViewMode.ExpRegisterList;
            dtgRegistersPresent.ItemsSource = myViewMode.lstclRegisterList;

            dtgTestMode.ItemsSource = myViewMode.ExpTestBtnList;
            bBtnExper = true;
            grdExperParent.IsVisibleChanged += (o, e) => Dispatcher.BeginInvoke(new DependencyPropertyChangedEventHandler(grdExperParent_IsVisibleChanged), o, e);

            //(A140310)Francis, there will be X/Y version of XML, so use one variable to control
            if ((!myViewMode.bEngModeFromXML) || (bForceHidePro))   //or if XML configure force to hide pro
            {
                //grdRWButtonPanel.Height = 100;
                grdRWButtonPanel.Height = 120;
                btnExperPro.Visibility = Visibility.Collapsed;
            }
            LibInfor.AssemblyRegister(Assembly.GetExecutingAssembly(), ASSEMBLY_TYPE.SFL);
        }

        // <summary>
        // Destructor, 
        // </summary>
        ~ExperControl()
        {
            //MessageBox.Show("SFL expired");
        }

        #endregion

        #region Property Changed, Event Handler

        // <summary>
        // Common Controls, WarningControl and WaitingControl message handler
        // </summary>
        // <param name="sender"></param>
        // <param name="e"></param>
        void tskmsg_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            TASKMessage tskmsgSend = sender as TASKMessage;
            switch (e.PropertyName)
            {
                case "controlreq":
                    switch (tskmsgSend.controlreq)
                    {
                        case COMMON_CONTROL.COMMON_CONTROL_WARNING:
                            {
                                //WarningControlPInvoke(gnlmsgProduct);
                                ExperWarnMsgInvoke(tskmsgSend.gm.message);
                                break;
                            }

                        case COMMON_CONTROL.COMMON_CONTROL_WAITTING:
                            {
                                ExperWaitControlInvoke(m_ctlmsgExper);
                                break;
                            }
                    }
                    break;
            }
        }
        #endregion

        #region Event Handler for all UI controls

        // <summary>
        // Write button click event handler, get which index button, by Button.Tag, is pressed 
        // and do TM.TM_WRITE DEM service
        // </summary>
        // <param name="sender"></param>
        // <param name="e"></param>
        private void btnWrite_Click(object sender, RoutedEventArgs e)
        {
            Button btntmp = sender as Button;
            UInt32 u32tmp = (UInt32)btntmp.Tag;
            ExperModel expmtmp = btntmp.DataContext as ExperModel;

            if (!bRepeatBtn)
            {
                bRepeatBtn = true;
                bBtnExper = false;
                btnReadAll.IsEnabled = false;
                //expmtmp = myViewMode.SearchExpModelByIndex(u16tmp, myViewMode.ExpRegisterList);
                //expmtmp = myViewMode.SearchExpModelByIndex(u16tmp);
                if (expmtmp == null)
                {
                    ExperWarnMsgInvoke(LibErrorCode.GetErrorDescription(LibErrorCode.IDS_ERR_EXPSFL_DATABINDING));
                }
                else
                {
                    //have Binding Data, try to Write value to Device
                    m_tskmsgExper.gm.controls = btntmp.Content.ToString();
                    if (!myViewMode.WriteRegToDevice(ref m_tskmsgExper, expmtmp))
                    {
                        ExperWarnMsgInvoke(LibErrorCode.GetErrorDescription(m_tskmsgExper.errorcode));
                    }
                    else
                    {
                        bBtnExper = true;
                    }
                }
                btnReadAll.IsEnabled = true;
                Thread.Sleep(1);
                bRepeatBtn = false;
            }
        }

        // <summary>
        // Read button click event handler, get which index button, by Button.Tage, is pressed
        // and do TM.TM_READ DEM service
        // </summary>
        // <param name="sender"></param>
        // <param name="e"></param>
        private void btnRead_Click(object sender, RoutedEventArgs e)
        {
            Button btntmp = sender as Button;
            UInt32 u32tmp = (UInt32)btntmp.Tag;
            ExperModel expmtmp = btntmp.DataContext as ExperModel;

            if (!bRepeatBtn)
            {
                bRepeatBtn = true;
                bBtnExper = false;
                btnReadAll.IsEnabled = false;
                //expmtmp = myViewMode.SearchExpModelByIndex(u16tmp, myViewMode.ExpRegisterList);
                //expmtmp = myViewMode.SearchExpModelByIndex(u16tmp);
                if (expmtmp == null)
                {
                    ExperWarnMsgInvoke(LibErrorCode.GetErrorDescription(LibErrorCode.IDS_ERR_EXPSFL_DATABINDING));
                }
                else
                {
                    //have Binding Data, try to read from Device
                    m_tskmsgExper.gm.controls = btntmp.Content.ToString();
                    if (!myViewMode.ReadRegFromDevice(ref m_tskmsgExper, expmtmp))
                    {
                        ExperWarnMsgInvoke(LibErrorCode.GetErrorDescription(m_tskmsgExper.errorcode));
                    }
                    else
                    {
                        bBtnExper = true;
                    }
                }
                btnReadAll.IsEnabled = true;
                Thread.Sleep(1);
                bRepeatBtn = false;
            }
        }

        // <summary>
        // Bit 0/1 toggle button click event handler, set up display value and value stored in ExperModel
        // </summary>
        // <param name="sender"></param>
        // <param name="e"></param>
        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton tglbtntmp = sender as ToggleButton;
            ExperModel expmtmp = tglbtntmp.DataContext as ExperModel;

            expmtmp.SumRegisterValue(false, false);
            //(A141222)Francis, SumRegisterValue() will summerize all bit's value into u16RegVal
            m_tskmsgExper.gm.controls = tglbtntmp.Content.ToString();
            if (!myViewMode.AdjustPhyValueByUser(ref m_tskmsgExper, expmtmp))
            {
            }
            //(E141222)
        }

        // <summary>
        // ReadAll button click event handler, parse all parameter in ExpRegisterList and do TM.TM_READ DEM service
        // </summary>
        // <param name="sender"></param>
        // <param name="e"></param>
        private void btnReadAll_Click(object sender, RoutedEventArgs e)
        {
            Button btntmp = sender as Button;

            if (!bRepeatBtn)
            {
                bRepeatBtn = true;
                bBtnExper = false;
                btnReadAll.IsEnabled = false;
                m_tskmsgExper.gm.controls = btntmp.Content.ToString();
                if (!myViewMode.ReadRegFromDevice(ref m_tskmsgExper))
                {
                    ExperWarnMsgInvoke(LibErrorCode.GetErrorDescription(m_tskmsgExper.errorcode));
                }
                else
                {
                    bBtnExper = true;
                }
                btnReadAll.IsEnabled = true;
                bRepeatBtn = false;
            }
        }

        // <summary>
        // WriteAll button click event handler, parse all parameter in ExpRegisterList and do TM.TM_WRITE DEM service
        // </summary>
        // <param name="sender"></param>
        // <param name="e"></param>
        private void btnWriteAll_Click(object sender, RoutedEventArgs e)
        {
            Button btntmp = sender as Button;

            if (!bRepeatBtn)
            {
                bRepeatBtn = true;
                bBtnExper = false;
                btnReadAll.IsEnabled = false;
                m_tskmsgExper.gm.controls = btntmp.Content.ToString();
                if (!myViewMode.WriteRegToDevice(ref m_tskmsgExper))
                {
                    ExperWarnMsgInvoke(LibErrorCode.GetErrorDescription(m_tskmsgExper.errorcode));
                }
                else
                {
                    bBtnExper = true;
                }
                btnReadAll.IsEnabled = true;
                bRepeatBtn = false;
            }
        }

        // <summary>
        // Label MouseMove event handler, when mouse is moving on description label, check length of description
        // of register/threshold, and show Tip on label if description is too long that cannot display completely in label
        // </summary>
        // <param name="sender"></param>
        // <param name="e"></param>
        private void Label_MouseMove(object sender, MouseEventArgs e)
        {
            string tips = string.Empty;
            Label lbltmp = sender as Label;
            ExperModel model = lbltmp.DataContext as ExperModel;
            bool bShow = true;

            /*			foreach (ExperBitComponent exbitcmp in expmtmp.ArrRegComponet)
                        {
                            if (exbitcmp.strBitDescrip.Equals(lbltmp.Content.ToString()))
                            {
                                if (exbitcmp.yDescripVisiLgth > 1)
                                {	//more than 1 bit, don't show
                                    return;
                                }
                                break;
                            }
                        }*/
            //MessageBox.Show(lbltmp.Content.ToString());
            bShow = FindParameterTips(model, lbltmp.Content.ToString(), ref tips);
            if (bShow)
            {
                popText.Text = " " + tips + " ";// +lbltmp.Content.ToString().Length.ToString() + ", " + lbltmp.Width.ToString();
                popTip.PlacementTarget = lbltmp;
                popTip.HorizontalOffset = lbltmp.ActualWidth * 3 / 4;
                popTip.IsOpen = bShow;
            }
            else
            {
                popText.Text = " " + lbltmp.Content.ToString() + " ";// +lbltmp.Content.ToString().Length.ToString() + ", " + lbltmp.Width.ToString();
                popTip.PlacementTarget = lbltmp;
                popTip.HorizontalOffset = lbltmp.ActualWidth * 3 / 4;
                //if ((popText.ActualWidth - 4) <= lbltmp.Width) popTip.IsOpen = false;
                //MessageBox.Show(popTip.ActualWidth.ToString() + " " + popText.ActualWidth.ToString());
                popTip.IsOpen = true;
                if ((popText.ActualWidth - 4) <= lbltmp.ActualWidth) popTip.IsOpen = false;
            }
        }
        // <summary>
        // Label MovueLeave event handler, when mouse is moving out of label, hide Tip
        // </summary>
        // <param name="sender"></param>
        // <param name="e"></param>
        private void Label_MouseLeave(object sender, MouseEventArgs e)
        {
            popTip.IsOpen = false;
        }

        // <summary>
        // Test buttons click event handler, including Normal Mode button. setup dtgRegistersPresent.ItemsSource
        // and show/hide items in ExpTestRegList according to which button is pressed.
        // </summary>
        // <param name="sender"></param>
        // <param name="e"></param>
        private void btnTest_Click(object sender, RoutedEventArgs e)
        {
            Button btntmp = sender as Button;
            byte ytmp = Convert.ToByte(btntmp.Tag.ToString());
            ExperTestButtonModel extest = btntmp.DataContext as ExperTestButtonModel;
            bool bReturn = true;

            if (!bRepeatBtn)
            {
                bRepeatBtn = true;
                bBtnExper = false;
                if (extest == null)
                {
                    ExperWarnMsgInvoke(LibErrorCode.GetErrorDescription(LibErrorCode.IDS_ERR_EXPSFL_DATABINDING));
                    return;
                }

                m_tskmsgExper.errorcode = LibErrorCode.IDS_ERR_SECTION_EXPERSFL;	//TBD
                //			WaitControlExperSetup(true, 10, "Check " + extest.strTestTrim, 100);
                WaitControlExperSetup(true, 20, "Command to device to change mode", 100);
                m_tskmsgExper.gm.controls = btntmp.Content.ToString();
                if (!bFranTestMode)
                {
                    bReturn = myViewMode.CommandTestToDevice(ref m_tskmsgExper, extest);
                }
                else
                {
                    bReturn = true;
                }
                if (bReturn)
                {
                    WaitControlExperSetup(true, 70, "Turn register on/off ", 100);
                    //if (ytmp == 0x00)
                    if (extest.bHost)	//if it's normal mode button, switch to normal mode register UI
                    {
                        //					if (extest.bReadBack)
                        //(A150105)Francis
                        if ((extest.bRegReadFrom == false) && (extest.bRegWriteTo == false))
                        {
                            dtgRegistersPresent.ItemsSource = myViewMode.ExpRegisterList;
                            //dtgRegistersPresent.ItemsSource = myViewMode.lstclRegisterList;
                            myViewMode.EnableTestButton(true);		// setup item show/hide in ExpTestBtnList
                        }
                    }
                    else
                    {
                        if (extest.bReadBack)
                        {	//if readback, UI should be changed to TestMode register
                            myViewMode.DisplayTestRegister(extest);	//Arrange show/hide
                            //dtgRegistersPresent.ItemsSource = myViewMode.ExpTestRegList;
                            dtgRegistersPresent.ItemsSource = myViewMode.lstclTestRegList;
                            myViewMode.EnableTestButton(false, extest);		// setup item show/hide in ExpTestBtnList
                        }
                    }
                    if (extest.bReadBack)
                        WaitControlExperSetup(true, 100, "Swtich to " + extest.strTestTrim + " successfully.", 100);
                    else
                        WaitControlExperSetup(true, 100, "Command  " + extest.strTestTrim + " successfully.", 100);
                    bBtnExper = true;
                }
                else
                {
                    WaitControlExperSetup(true, -1, "Error on switch to " + extest.strTestTrim);
                }
                bRepeatBtn = false;
            }
        }

        private void grdRegHigh_Click(object sender, RoutedEventArgs e)
        {
            //Button btntmp = sender as Button;
            ToggleButton btntmp = sender as ToggleButton;
            ExperModel extest = btntmp.DataContext as ExperModel;

            //extest.bMarkReg = !extest.bMarkReg;
            if (extest.bMarkReg)
                uTglNumber += 1;
            else
                uTglNumber -= 1;
            if (uTglNumber < 0) uTglNumber = 00;
        }


        /* Label_MouseLeftButtonUp, no used
                private void Label_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
                {
                    Label lbltmp = (Label)sender;
                    byte ytmp = Convert.ToByte(lbltmp.Content);
                    if (ytmp == 0)
                    {
                        lbltmp.Content = "1".ToString();
                    }
                    else if (ytmp == 1)
                    {
                        lbltmp.Content = "0".ToString();
                    }
                }
        */

        // <summary>
        // Grid of ExperControl Show/Hide event handler; if Grid is changed to show, do sync with chip and change
        // corresponding UI to show; if Grid is changed to hidded, do nothing
        // </summary>
        // <param name="sender"></param>
        // <param name="e"></param>
        private void grdExperParent_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            AsyncObservableCollection<ExperModel> tmpCollect = null;
            //if SFL is hidding, no need to do sync
            if (e.NewValue.ToString().ToLower().Equals("false"))
                return;
            WaitControlExperSetup(true, 50, "Synchronizing with chip...");
            //dtgRegistersPresent.ItemsSource = myViewMode.ExpRegisterList;
            dtgRegistersPresent.ItemsSource = myViewMode.lstclRegisterList;
            if (!bFranTestMode)
            {
                if (myViewMode.bTrimModeFromXML)
                {
                    bBtnExper = myViewMode.SyncModeWithDev(ref m_tskmsgExper, ref tmpCollect);
                    if (tmpCollect != null)
                    {
                        //dtgRegistersPresent.ItemsSource = tmpCollect;
                        dtgRegistersPresent.ItemsSource = myViewMode.ExpRegisterList;
                        //dtgRegistersPresent.ItemsSource = myViewMode.lstclTestRegList;
                    }
                }
            }
            ExperWaitControlClear();
        }

        /// <summary>
        /// Pro button click event, to switch Pro interface or close
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnExperPro_Click(object sender, RoutedEventArgs e)
        {
            Button btntmp = sender as Button;
            AsyncObservableCollection<ExperModel> tmpCollect = null;

            WaitControlExperSetup(true, 50, "Waiting for switching UI...", 100);
            if (btntmp.Content.Equals("Pro"))
            {
                btntmp.Content = "Exit";
                //				btnReadAll.IsEnabled = false;
                btnWriteAll.IsEnabled = false;
                dtgTestMode.IsEnabled = false;
                myViewMode.OpenExpertUI();
                dtgRegistersPresent.ItemsSource = myViewMode.lstclRegisterList;
            }
            else
            {
                btntmp.Content = "Pro";
                //				btnReadAll.IsEnabled = true;
                btnWriteAll.IsEnabled = true;
                dtgTestMode.IsEnabled = true;
                myViewMode.RestoreExpertUI();
                if (!bFranTestMode)
                {
                    if (myViewMode.bTrimModeFromXML)
                    {
                        bBtnExper = myViewMode.SyncModeWithDev(ref m_tskmsgExper, ref tmpCollect);
                        if (tmpCollect != null)
                        {
                            //dtgRegistersPresent.ItemsSource = tmpCollect;
                            dtgRegistersPresent.ItemsSource = myViewMode.lstclTestRegList;
                        }
                    }
                }
            }
            ExperWaitControlClear();
        }

        private void btnGoto_Click(object sender, RoutedEventArgs e)
        {
            int iIndex = 0;
            //ExperModel expTmp = null;
            UInt32 yTmp;

            if (!UInt32.TryParse(txtGoto.Text, NumberStyles.HexNumber, null, out yTmp))
                return;

            ICollectionView lstt = dtgRegistersPresent.ItemsSource as ICollectionView;
            IList<ExperModel> list = lstt.SourceCollection as IList<ExperModel>;

            dtgRegistersPresent.ScrollIntoView(list[list.Count - 1]);
            //foreach (ExperModel expd in myViewMode.ExpRegisterList)
            foreach (ExperModel expd in list)
            {
                if (expd.u32RegNum == yTmp)
                {
                    //expd.bMarkReg = true;
                    if ((expd.strGroupReg.Equals(expd.strTestXpr)) || (expd.strTestXpr.IndexOf("Normal") != -1))
                    {
                        dtgRegistersPresent.ScrollIntoView(expd);
                        iIndex = 1;
                        break;
                    }
                }
                else
                {
                    //iIndex += 1;
                }
            }

            if (iIndex == 0)
            {
                dtgRegistersPresent.ScrollIntoView(list[0]);
            }

            //			if (iIndex != 0)
            //			{
            //				ListViewItem lstItem = dtgRegistersPresent.ItemContainerGenerator.ContainerFromIndex(iIndex) as ListViewItem;
            //				dtgRegistersPresent.ScrollIntoView(lstItem);
            //			}
        }

        private void txtGoto_KeyDown(object sender, KeyEventArgs e)
        {
            TextBox txtbx = sender as TextBox;

            if ((e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9) ||
                (e.Key == Key.Decimal) || (e.Key == Key.Subtract) || (e.Key == Key.Delete) ||
                (e.Key == Key.A) || (e.Key == Key.B) || (e.Key == Key.C) ||
                (e.Key == Key.D) || (e.Key == Key.E) || (e.Key == Key.F) ||
                ((e.Key >= Key.D0) && (e.Key <= Key.D9)))
            {
                //if (txtbx.Text.Length < 2)
                {
                    e.Handled = false;
                    return;
                }
                //e.Handled = false;
            }
            else if (e.Key == Key.Enter)
            {
                btnGoto.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                e.Handled = false;
                return;
            }
            e.Handled = true;
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            TextBox txtbx = sender as TextBox;

            if ((e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9) ||
                (e.Key == Key.Subtract) || (e.Key == Key.Delete) ||
                (e.Key == Key.A) || (e.Key == Key.B) || (e.Key == Key.C) ||
                (e.Key == Key.D) || (e.Key == Key.E) || (e.Key == Key.F) ||
                ((e.Key >= Key.D0) && (e.Key <= Key.D9)))
            {
                //if ((txtbx.Text.Length <2) || (txtbx.Text.Length == 0))
                //{
                //e.Handled = true;
                //return;
                //}
                //else
                {
                    e.Handled = false;
                    return;
                }
                //e.Handled = false;
            }
            else if (e.Key == Key.Enter)
            {
                txtbx.RaiseEvent(new RoutedEventArgs(TextBox.LostFocusEvent));
                e.Handled = false;
                return;
            }
            else
            {
                e.Handled = true;
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox txtbx = sender as TextBox;
            string strtmp = txtbx.Text.ToLower();
            ExperModel expmtmp = txtbx.DataContext as ExperModel;

            if (strtmp.IndexOf("0x") == -1)
            {
                txtbx.Text = "0x" + strtmp;
            }
            txtbx.CaretIndex = txtbx.Text.Length;
            //expmtmp.bValueChange = true;
            if (!myViewMode.AdjustPhyValueByUser(ref m_tskmsgExper, expmtmp))
            {
            }
        }

        #endregion

        #region Message/Waiting Control

        // <summary>
        // Warning Control invoke function, display parsing in message in Warning Control, and setup its level
        // </summary>
        // <param name="strGnlMsgIn"></param>
        // <param name="iLevelIn"></param>
        public void ExperWarnMsgInvoke(string strGnlMsgIn, int iLevelIn = 2)
        {
            if ((m_tskmsgExper.errorcode & LibErrorCode.IDS_ERR_SECTION_EXPERSFL) != 0)
            {
                bBtnExper = false;
            }

            //m_gnlmsgExper.level = 2;
            m_gnlmsgExper.level = iLevelIn;
            m_gnlmsgExper.message = strGnlMsgIn;
            ExperWarnMsg.Dispatcher.Invoke(new Action(() =>
            {
                ExperWarnMsg.ShowDialog(m_gnlmsgExper);
            }));
        }

        // <summary>
        // Wait Control clear function, clear value in Wait Control and hide it
        // </summary>
        public void ExperWaitControlClear()
        {
            m_ctlmsgExper.bshow = false;
            m_ctlmsgExper.percent = 0;
            m_ctlmsgExper.message = String.Empty;
            ExperWaitControlInvoke(m_ctlmsgExper);
        }

        // <summary>
        // Wait Control Invoke function, display Wait Control according to parsed-in ControlMessage
        // </summary>
        // <param name="ctlmsgInput"></param>
        public void ExperWaitControlInvoke(ControlMessage ctlmsgInput)
        {
            ExperWaitControl.Dispatcher.Invoke(new Action(() =>
            {
                ExperWaitControl.IsBusy = ctlmsgInput.bshow;
                ExperWaitControl.Text = ctlmsgInput.message;
                ExperWaitControl.Percent = String.Format("{0}%", ctlmsgInput.percent);
            }));
        }

        // <summary>
        // Set up ControlMessage, then call ExperWaitControlInvoke() to show Wait Control
        // iDelay will makes Control display longer, and defult is 0
        // </summary>
        // <param name="bShowsIn"></param>
        // <param name="iPercentIn"></param>
        // <param name="strMessageIn"></param>
        // <param name="iDelayIn"></param>
        public void WaitControlExperSetup(bool bShowsIn, int iPercentIn, string strMessageIn, int iDelayIn = 0)
        {
            m_ctlmsgExper.bshow = bShowsIn;
            m_ctlmsgExper.percent = iPercentIn;
            m_ctlmsgExper.message = strMessageIn;

            if (iPercentIn == 100)
            {
                ExperWaitControlClear();
                ExperWarnMsgInvoke(strMessageIn, 0);
            }
            else if (iPercentIn == -1)
            {
                ExperWaitControlClear();
                ExperWarnMsgInvoke(strMessageIn);
            }
            else
            {
                if (iDelayIn > 0)
                {
                    Action EmptyDelegate = delegate() { };
                    ExperWaitControlInvoke(m_ctlmsgExper);
                    ExperWaitControl.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Render, EmptyDelegate);
                    m_tskmsgExper.controlreq = COMMON_CONTROL.COMMON_CONTROL_WAITTING;
                    for (int i = 0; i < iDelayIn; i++)
                    {
                        System.Windows.Forms.Application.DoEvents();
                        Thread.Sleep(1);
                    }
                }
            }
        }

        #endregion

        #region other pirvate methods

        //private void EnDisableButtons(bool bActEnIn)
        //{
        //}

        /// <summary>
        /// Read ExtensionDescriptor to decide using public property to get Addrss, BitStart, BitlLength, id=547
        /// </summary>
        private void ReadSettingFromExtDescrip()
        {
            XmlNodeList nodelist = devParent.GetUINodeList(sflname);
            bool bdata = false;
            byte ydata = 0x00;

            //assign default
            bSharedPublic = false;
            bForceHidePro = false;
            yBitTotal = 0x00;
            //parse from XML
            foreach (XmlNode cnode in nodelist)
            {
                switch (cnode.Name)
                {
                    case "Configure":
                        {
                            foreach (XmlNode csub in cnode)
                            {
                                switch (csub.Name)
                                {
                                    case "SharedPublic":
                                        {
                                            if (!Boolean.TryParse(csub.InnerText.ToString(), out bdata))
                                            {
                                                bSharedPublic = false;
                                            }
                                            else
                                            {
                                                bSharedPublic = bdata;
                                            }
                                            break;
                                        }
                                    case "HidePro":
                                        {
                                            if (!Boolean.TryParse(csub.InnerText.ToString(), out bdata))
                                            {
                                                bForceHidePro = false;
                                            }
                                            else
                                            {
                                                bForceHidePro = bdata;
                                            }
                                            break;
                                        }
                                    case "BitTotal":
                                        {
                                            if (!Byte.TryParse(csub.InnerText.ToString(), NumberStyles.HexNumber, null as IFormatProvider, out ydata))
                                            {
                                                yBitTotal = 0x08;
                                            }
                                            else
                                            {
                                                yBitTotal = Convert.ToByte(csub.InnerText.ToString(), 16);
                                            }
                                            break;
                                        }
                                }
                            }
                            break;
                        }
                }
            }
        }

        #endregion
        #region Add by guo 20190514 to support OZ26786
        private bool FindParameterTips(ExperModel model, string des, ref string tips)
        {
            bool bval = false;
            foreach (ExperBitComponent ebctmp in model.ArrRegComponet)
            {
                if (ebctmp.strBitDescrip.Equals(des))
                {
                    tips = ebctmp.strBitTips;
                    if (string.IsNullOrEmpty(tips))
                        bval = false;
                    else
                        bval = true;
                }
            }
            return bval;
        }
        #endregion
    }
}
