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
using System.Collections;
using System.ComponentModel;
using System.Windows.Controls.Primitives;
using System.Threading;
using System.Timers;
using System.IO;
using System.Xml;
using Cobra.EM;
using Cobra.Common;
using Cobra.ControlLibrary;

namespace Cobra.PreCHGDSGPanel
{

    public class PID
    {
        public double err = 0.0;
        public double last_err = 0.0;
        public double inte = 0.0;
        public double diff = 0.0;
        public double output = 0.0;

        public double kp = 0.2;
        public double ki = 0.015;
        public double kd = 0.2;

        public void init()
        {
            err = 0.0;
            last_err = 0.0;
            inte = 0.0;
            diff = 0.0;
            output = 0.0;

            kp = 0.2;
            ki = 0.015;
            kd = 0.2;
        }
    }

    public class AveragePI
    {
        public double[] pHistory = new double[4];
        public byte pcount = 0;
        public double[] iHistory = new double[4];
        public byte icount = 0;
        public void init()
        {
            for (int i = 0; i < 4; i++)
            {
                pHistory[i] = 0.0;
                iHistory[i] = 0.0;
            }
            pcount = 0;
            icount = 0;
        }
    }

    /// <summary>
    /// Interaction logic for MainControl.xaml
    /// </summary>
    public partial class MainControl
    {
        #region variable defination
        private PID pid = new PID();
        private AveragePI avrPI = new AveragePI();
        //父对象保存

        bool isCCReentrant_Run = false;   //控制Run button的重入

        bool isCPReentrant_Run = false;   //控制Run button的重入

        private Device m_parent;
        public Device parent
        {
            get { return m_parent; }
            set { m_parent = value; }
        }

        private string m_SFLname;
        public string sflname
        {
            get { return m_SFLname; }
            set { m_SFLname = value; }
        }

        private TASKMessage m_Msg = new TASKMessage();
        public TASKMessage msg
        {
            get { return m_Msg; }
            set { m_Msg = value; }
        }

        private GeneralMessage gm = new GeneralMessage("PreCHGDSG SFL", "", 0);


        private SFLViewModel mViewModel = new SFLViewModel();
        public SFLViewModel ViewModel
        {
            get { return mViewModel; }
            set { mViewModel = value; }
        }

        private System.Timers.Timer tCC = new System.Timers.Timer();
        private System.Timers.Timer tCP = new System.Timers.Timer();
        ParamContainer dynamicdatalist = new ParamContainer();
        Parameter SW_CHG_CTRL = new Parameter();
        Parameter SW_DSG_CTRL = new Parameter();
        Parameter PRE_SEL = new Parameter();
        Parameter PRE_SET = new Parameter();
        Parameter PRE_DSG = new Parameter();
        Parameter PRE_CHG = new Parameter();
        Parameter DSG_DRV = new Parameter();
        Parameter CHG_DRV = new Parameter();
        Parameter OV = new Parameter();
        Parameter OVH = new Parameter();
        AsyncObservableCollection<Parameter> paramlist = new AsyncObservableCollection<Parameter>();

        BindingExpression ptBE;
        BindingExpression itBE;
        private double lastI = 0;
        private bool isAdjusted = false;
        private byte Pcounter = 0;
        #endregion

        public MainControl(object pParent, string name)
        {
            InitializeComponent();

            #region 相关初始化
            parent = (Device)pParent;
            if (parent == null) return;

            sflname = name;
            if (String.IsNullOrEmpty(sflname)) return;

            WarningPopControl.SetParent(LayoutRoot);

            #endregion
            WorkPanel.DataContext = ViewModel;
            ptBE = PtargetInput.GetBindingExpression(TextBox.TextProperty);
            itBE = ItargetInput.GetBindingExpression(TextBox.TextProperty);

            paramlist = parent.GetParamLists(sflname).parameterlist;
            foreach (Parameter param in paramlist)
            {
                string pName = GetHashTableValueByKey("Name", param.sfllist[sflname].nodetable);
                if (pName == "Vpack" || pName == "Vbatt" || pName == "Current" || pName == "PRE_SET")
                    dynamicdatalist.parameterlist.Add(param);
                else if (pName == "SW_CHG_CTRL")
                    SW_CHG_CTRL = param;
                else if (pName == "SW_DSG_CTRL")
                    SW_DSG_CTRL = param;
                else if (pName == "PRE_SEL")
                    PRE_SEL = param;
                else if (pName == "Rsense")
                    ViewModel.Rsense = param;
                else if (pName == "PRE_DSG")
                    PRE_DSG = param;
                else if (pName == "PRE_CHG")
                    PRE_CHG = param;
                else if (pName == "DSG_DRV")
                    DSG_DRV = param;
                else if (pName == "CHG_DRV")
                    CHG_DRV = param;
                else if (pName == "OV")
                    OV = param;
                else if (pName == "OVH")
                    OVH = param;
                if (pName == "PRE_SET")
                    PRE_SET = param;
            }


            

            tCC.Elapsed += new ElapsedEventHandler(tCC_Elapsed);
            tCP.Elapsed += new ElapsedEventHandler(tCP_Elapsed);

            if (sflname == "PreDischarge")
            {
                OVTHInput.Visibility = Visibility.Collapsed;
                OVTHLabel.Visibility = Visibility.Collapsed;
            }

        }

        private string GetHashTableValueByKey(string str, Hashtable htable)
        {
            foreach (DictionaryEntry de in htable)
            {
                if (de.Key.ToString().Equals(str))
                    return de.Value.ToString();
            }
            return "NoSuchKey";
        }


        private delegate void UpdateChipDataDelegate();
        public void UpdateChipData()
        {
            foreach (Parameter param in dynamicdatalist.parameterlist)
            {
                //if (param.phydata != -999999)
                {
                    if (GetHashTableValueByKey("Name", param.sfllist[sflname].nodetable) == "Current")
                    {
                        ViewModel.I = param.phydata;
                    }
                    else if (GetHashTableValueByKey("Name", param.sfllist[sflname].nodetable) == "Vpack")
                    {
                        ViewModel.Vpack = param.phydata;
                    }
                    else if (GetHashTableValueByKey("Name", param.sfllist[sflname].nodetable) == "Vbatt")
                    {
                        ViewModel.Vbatt = param.phydata;
                    }
                    else if (GetHashTableValueByKey("Name", param.sfllist[sflname].nodetable) == "PRE_SET")
                    {
                        ViewModel.Ipre = param.phydata;
                    }
                }
            }
            if(sflname == "PreDischarge")
                ViewModel.Vfet = ViewModel.Vbatt - ViewModel.Vpack;
            else if (sflname == "PreCharge")
                ViewModel.Vfet = ViewModel.Vpack - ViewModel.Vbatt;

            ViewModel.P = Math.Abs(ViewModel.Vfet * ViewModel.I / 1000);
        }


        private delegate void UpdateCalDataIDelegate();
        public void UpdateCalData()
        {
            double Imax_chip = (12800 / ViewModel.Rsense.phydata);
            double Imin_chip = (100 / ViewModel.Rsense.phydata);
            //double Pmax_chip = Math.Abs((12800 / ViewModel.Rsense) * ViewModel.Vfet / 1000);
            //double Pmax_chip = 99999999;
            //double Pmin_chip = Math.Abs((100 / ViewModel.Rsense) * ViewModel.Vfet / 1000);

            ViewModel.Pmin = 0;
            ViewModel.Imin = Imin_chip;

            //ViewModel.Pmax_cal = Math.Abs(ViewModel.Imax_set * ViewModel.Vfet / 1000);
            //ViewModel.Pmax_cal = 99999999;
            //ViewModel.Imax_cal = ViewModel.Pmax_set * 1000 / ViewModel.Vfet;
            ViewModel.Imax_cal = 99999999;

            //ViewModel.Pmax = Minimum(ViewModel.Pmax_cal, ViewModel.Pmax_set, Pmax_chip);
            ViewModel.Pmax = ViewModel.Pmax_set;
            ViewModel.Imax = Minimum(ViewModel.Imax_cal, ViewModel.Imax_set, Imax_chip);
        }


        public bool OpenFet()
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            ParamContainer pc = new ParamContainer();

            for (int i = 0; ; i++)
            {
                pc.parameterlist.Clear();
                pc.parameterlist.Add(PRE_CHG);
                pc.parameterlist.Add(PRE_DSG);
                ret = Read(pc);
                if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    return false;
                ret = ConvertHexToPhysical(pc);
                if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    return false;

                if ((PRE_CHG.phydata != 1 && sflname == "PreCharge") || (PRE_DSG.phydata != 1 && sflname == "PreDischarge"))
                {
                    if (i < 3)
                    {
                        pc.parameterlist.Clear();
                        SW_CHG_CTRL.phydata = 1;
                        SW_DSG_CTRL.phydata = 1;
                        pc.parameterlist.Add(SW_CHG_CTRL);
                        pc.parameterlist.Add(SW_DSG_CTRL);
                        ret = ConvertPhysicalToHex(pc);
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                            return false;
                        ret = Write(pc);
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                            return false;
                    }
                    else
                        return false;
                }
                else
                    return true;
            }
        }

        public bool CloseFet()
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            ParamContainer pc = new ParamContainer();

            //if(sflname == "PreCharge")


            for (int i = 0; ; i++)
            {
                pc.parameterlist.Clear();
                pc.parameterlist.Add(CHG_DRV);
                pc.parameterlist.Add(DSG_DRV);
                pc.parameterlist.Add(PRE_CHG);
                pc.parameterlist.Add(PRE_DSG);
                ret = Read(pc);
                if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    return false;
                ret = ConvertHexToPhysical(pc);
                if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    return false;

                if (CHG_DRV.phydata != 0 || DSG_DRV.phydata != 0 || PRE_DSG.phydata != 0 || PRE_CHG.phydata != 0)
                {
                    if (i < 5)
                    {
                        pc.parameterlist.Clear();
                        SW_CHG_CTRL.phydata = 0;
                        SW_DSG_CTRL.phydata = 0;
                        pc.parameterlist.Add(SW_CHG_CTRL);
                        pc.parameterlist.Add(SW_DSG_CTRL);
                        ret = ConvertPhysicalToHex(pc);
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                            return false;
                        ret = Write(pc);
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                            return false;
                        ret = Command(dynamicdatalist, 1);                                  //读一次动态数据
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        {
                            return false;
                        }
                        Thread.Sleep(100);
                    }
                    else
                        return false;
                }
                else
                    return true;
            }
        }

        private delegate bool CurrentSafetyCheckDelegate();
        public bool CurrentSafetyCheck()
        {
            for (int i = 0; i < 3; i++)
            {
                avrPI.pHistory[i] = avrPI.pHistory[i + 1];
                avrPI.iHistory[i] = avrPI.iHistory[i + 1];
            }
            avrPI.iHistory[3] = ViewModel.I;
            if (avrPI.icount < 4)
                avrPI.icount++;

            avrPI.pHistory[3] = ViewModel.P;
            if (avrPI.pcount < 4)
                avrPI.pcount++;

            double avrP = 0.0;
            double avrI = 0.0;
            for (int i = 0; i < 4; i++)
            {
                avrP += avrPI.pHistory[i];
                avrI += avrPI.iHistory[i];
            }
            avrP /= avrPI.pcount;
            avrI /= avrPI.icount;

            if (avrI > ViewModel.Imax || (ViewModel.I-ViewModel.Imax) > (ViewModel.Imax * 1.3))
            {
                if(CCrunBtn.IsChecked == true)
                    CCEnterStopState(0);
                if(CPrunBtn.IsChecked == true)
                    CPEnterStopState(0);
                MessageBox.Show("Current exceeded! " + sflname + " stoped!");
                return false;
            }
            if (avrP > ViewModel.Pmax || (ViewModel.P - ViewModel.Pmax) > (ViewModel.Pmax * 1.3))
            {
                if (CCrunBtn.IsChecked == true)
                    CCEnterStopState(0);
                if (CPrunBtn.IsChecked == true)
                    CPEnterStopState(0);
                MessageBox.Show("Power exceeded! " + sflname + " stoped!");
                return false;
            }
            return true;
        }

        private delegate bool PowerSafetyCheckDelegate();
        public bool PowerSafetyCheck()
        {
            if (ViewModel.P <= ViewModel.Pmax)
            {
                Pcounter = 0;
                return true;
            }
            else if (Pcounter >= 3)
            {
                Pcounter = 0;
                return false;
            }
            else
            {
                Pcounter++;
                return true;
            }
        }

        void gm_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            parent.gm = (GeneralMessage)sender;
        }

        void msg_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            TASKMessage msg = sender as TASKMessage;
            switch (e.PropertyName)
            {
                case "controlreq":
                    switch (msg.controlreq)
                    {
                        case COMMON_CONTROL.COMMON_CONTROL_WARNING:
                            {
                                CallWarningControl(msg.gm);
                                /*t.Stop();
                                runBtn.IsChecked = false;
                                runBtn.Content = "Run";*/

                                break;
                            }
                    }
                    break;
            }
        }
        public void CallWarningControl(GeneralMessage message)
        {
            WarningPopControl.Dispatcher.Invoke(new Action(() =>
            {
                WarningPopControl.ShowDialog(message);
            }));
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            uint errorcode = 0;
            errorcode = GetSysInfo();                                           //读GPIO信息 
            if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                gm.message = LibErrorCode.GetErrorDescription(errorcode);
                gm.bupdate = true;
                //CCEnterStopState(errorcode);
                CallWarningControl(gm);
                return;
            }

            errorcode = GetDevInfo();                                           //读设备信息
            if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                gm.message = LibErrorCode.GetErrorDescription(errorcode);
                gm.bupdate = true;
                //CCEnterStopState(errorcode);
                CallWarningControl(gm);
                return;
            }
            errorcode = Command(dynamicdatalist, 7);                                  //Normal Mode
            if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                CCEnterStopState(errorcode);
                return;
            }
            errorcode = Command(dynamicdatalist, 1);                                  //读一次动态数据
            if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                return;
            }
            errorcode = ConvertHexToPhysical(dynamicdatalist);                   //转换一次静态数据
            if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                return;
            }
            OV.phydata = ViewModel.OVTH;
            if (OVH.phydata == 0)
                OVH.phydata = 1;
            ParamContainer pc = new ParamContainer();
            pc.parameterlist.Add(OV);
            pc.parameterlist.Add(OVH);
            errorcode = ConvertPhysicalToHex(pc);
            if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                return;
            }
            errorcode = Write(pc);
            if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                return;
            }
            errorcode = Read(pc);
            if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                return;
            }
            errorcode = ConvertHexToPhysical(pc);
            if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                return;
            }
            ViewModel.OVTH = OV.phydata;
            UpdateChipData();
            UpdateCalData();
            CPGroup.IsEnabled = true;
            CCGroup.IsEnabled = true;
        }

        private double Minimum(double a, double b, double c)
        {
            if (a <= b && a <= c)
                return a;
            else if (b <= c && b <= a)
                return b;
            else
                return c;
        }

        private void AdjustCurrent(double current)
        {
            isAdjusted = false;
            PRE_SET.phydata = current;
            if (PRE_SET.phydata != lastI)
            {
                lastI = PRE_SET.phydata;

                if (lastI < 100 / ViewModel.Rsense.phydata)
                {
                    lastI = 100 / ViewModel.Rsense.phydata;
                }

                if (lastI > 12800 / ViewModel.Rsense.phydata)
                {
                    lastI = 12800 / ViewModel.Rsense.phydata;
                }

                PRE_SET.phydata = lastI;

                Write(PRE_SET, PRE_SET.phydata);

                isAdjusted = true;
            }
        }

        #region CC Process
        private void CCEnterStopState(UInt32 errorcode)
        {
            tCC.Stop();

            if (!CloseFet())
                MessageBox.Show("Cannot close FET!");

            gm.message = LibErrorCode.GetErrorDescription(errorcode);
            gm.bupdate = true;

            CCrunBtn.Content = "Start";
            CCrunBtn.IsChecked = false;
            //ScanInterval.IsEnabled = true;
            //SubTask.IsEnabled = true;
            parent.bBusy = false;
            isCCReentrant_Run = false;
            Cursor = Cursors.Arrow;
            CPGroup.IsEnabled = true;
            CCUpdate.IsEnabled = false;

            avrPI.init();
        }

        private void CC_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton btn = sender as ToggleButton;
            uint errorcode = 0;
            if (isCCReentrant_Run == false)   //此次点击并没有重入
            {
                isCCReentrant_Run = true;
                Cursor = Cursors.Wait;
                if ((bool)btn.IsChecked)    //点了Run
                {
                    if (parent.bBusy)       //Scan功能是否被其他SFL占用
                    {
                        gm.controls = "Run button";
                        gm.message = LibErrorCode.GetErrorDescription(LibErrorCode.IDS_ERR_EM_THREAD_BKWORKER_BUSY);
                        gm.bupdate = true;
                        btn.Content = "Start";
                        btn.IsChecked = false;
                        isCCReentrant_Run = false;
                        Cursor = Cursors.Arrow;
                        return;
                    }
                    else
                        parent.bBusy = true;

                    if (msg.bgworker.IsBusy == true) //bus是否正忙
                    {
                        gm.controls = "Run button";
                        gm.message = LibErrorCode.GetErrorDescription(LibErrorCode.IDS_ERR_EM_THREAD_BKWORKER_BUSY);
                        gm.bupdate = true;
                        isCCReentrant_Run = false;
                        Cursor = Cursors.Arrow;
                        return;
                    }
                    itBE.UpdateSource();
                    if (ViewModel.Itarget < ViewModel.Imin || ViewModel.Itarget > ViewModel.Imax)
                    {
                        MessageBox.Show("Out of range!");
                        CCEnterStopState(errorcode);
                        return;
                    }
                    //一切正常，可以开始scan
                    btn.Content = "Stop";
                    gm.message = "CC Start";
                    CPGroup.IsEnabled = false;
                    CCUpdate.IsEnabled = true;
                    avrPI.init();
                    #region 预读数据
                    errorcode = GetSysInfo();                                           //读GPIO信息 
                    if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    {
                        CCEnterStopState(errorcode);
                        return;
                    }

                    errorcode = GetDevInfo();                                           //读设备信息
                    if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    {
                        CCEnterStopState(errorcode);
                        return;
                    }
                    errorcode = Command(dynamicdatalist, 1);                                  //读一次动态数据
                    if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    {
                        CCEnterStopState(errorcode);
                        return;
                    }
                    errorcode = ConvertHexToPhysical(dynamicdatalist);                  //转换一次动态数据
                    if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    {
                        CCEnterStopState(errorcode);
                        return;
                    }
                    #endregion
                    UpdateChipData();

                    #region 准备预充或预放
                    if (!CloseFet())
                    {
                        CCEnterStopState(errorcode);
                        MessageBox.Show("Cannot close FET!");
                        return;
                    }

                    if (sflname == "PreCharge")
                        Write(PRE_SEL, 1);
                    else if (sflname == "PreDischarge")
                        Write(PRE_SEL, 2);

                    Write(PRE_SET, ViewModel.Itarget);
                    ViewModel.Itarget = Read(PRE_SET);
                    itBE.UpdateTarget();
                    if (!OpenFet())
                    {
                        CCEnterStopState(errorcode);
                        MessageBox.Show("Cannot open FET!");
                        return;
                    }
                    #endregion
                    Thread.Sleep(10);
                    errorcode = Command(dynamicdatalist, 1);                                  //读一次动态数据
                    if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    {
                        CCEnterStopState(errorcode);
                        return;
                    }
                    errorcode = ConvertHexToPhysical(dynamicdatalist);                  //转换一次动态数据
                    if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    {
                        CCEnterStopState(errorcode);
                        return;
                    }
                    UpdateChipData();
                    if (CurrentSafetyCheck() == false)
                        return;
                    #region Scan rate
                    tCC.Interval = 500;
                    tCC.Start();
                    #endregion
                }
                else    //点了stop
                {
                    CCEnterStopState(errorcode);
                }

                isCCReentrant_Run = false;
                Cursor = Cursors.Arrow;
            }
            else
            {
                CCrunBtn.IsChecked = !CCrunBtn.IsChecked;
            }
        }

        private void CCUpdate_Click(object sender, RoutedEventArgs e)
        {
            /*tCC.Stop();
            Thread.Sleep(200);
            Write(PRE_SET, ViewModel.Itarget);
            ViewModel.Itarget = Read(PRE_SET);
            tCC.Start();*/
            //UInt32 errorcode = LibErrorCode.IDS_ERR_SUCCESSFUL;
            double oldItarget = ViewModel.Itarget;
            double newItarget = oldItarget;
            itBE.UpdateSource();
            if (ViewModel.Itarget < ViewModel.Imin || ViewModel.Itarget > ViewModel.Imax)
            {
                MessageBox.Show("Out of range!");
                //CCEnterStopState(errorcode);
                return;
            }
            byte i = 0;
            while (oldItarget == newItarget)
            {
                tCC.Stop();
                Thread.Sleep(50);
                Write(PRE_SET, ViewModel.Itarget);
                ViewModel.Itarget = Read(PRE_SET);
                tCC.Start();
                newItarget = ViewModel.Itarget;
                i++;
                if (i > 5)
                {
                    //MessageBox.Show("failed to update Itarget");
                    return;
                }
            }
            itBE.UpdateTarget();
        }
        private delegate void UpdateCCRunningUIDelegate();
        private void UpdateCCRunningUI()
        {
            CCrunBtn.Content = "Start";
            CCrunBtn.IsChecked = false;
        }
        void tCC_Elapsed(object sender, EventArgs e)
        {
            //定时到则读取并更新动态数据
            DateTime now = DateTime.Now;
            uint errorcode = 0;
            if (!msg.bgworker.IsBusy)
            {
                errorcode = Command(dynamicdatalist, 1);
                if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                {
                    tCC.Stop();
                    //this.Dispatcher.Invoke(new UpdateCCRunningUIDelegate(UpdateCCRunningUI));
                    gm.message = LibErrorCode.GetErrorDescription(errorcode);
                    gm.bupdate = true;
                    parent.bBusy = false;
                    CCEnterStopState(errorcode);
                    return;
                }
                errorcode = ConvertHexToPhysical(dynamicdatalist);
                if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                {
                    tCC.Stop();
                    //this.Dispatcher.Invoke(new UpdateCCRunningUIDelegate(UpdateCCRunningUI));
                    gm.message = LibErrorCode.GetErrorDescription(errorcode);
                    gm.bupdate = true;
                    parent.bBusy = false;
                    CCEnterStopState(errorcode);
                    return;
                }
                //*/

                this.Dispatcher.Invoke(new UpdateChipDataDelegate(UpdateChipData));
                this.Dispatcher.Invoke(new CurrentSafetyCheckDelegate(CurrentSafetyCheck));
            }
            else
            {
                int i = 0;
            }
        }
        #endregion

        #region CP Process
        private void CPEnterStopState(UInt32 errorcode)
        {
            tCP.Stop();

            if (!CloseFet())
                MessageBox.Show("Cannot close FET!");

            gm.message = LibErrorCode.GetErrorDescription(errorcode);
            gm.bupdate = true;

            CPrunBtn.Content = "Start";
            CPrunBtn.IsChecked = false;
            //ScanInterval.IsEnabled = true;
            //SubTask.IsEnabled = true;
            parent.bBusy = false;
            isCPReentrant_Run = false;
            Cursor = Cursors.Arrow;
            CCGroup.IsEnabled = true;
            CPUpdate.IsEnabled = false;

            pid.init();
            avrPI.init();
        }

        private void CP_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton btn = sender as ToggleButton;
            uint errorcode = 0;
            if (isCPReentrant_Run == false)   //此次点击并没有重入
            {
                isCPReentrant_Run = true;
                Cursor = Cursors.Wait;
                if ((bool)btn.IsChecked)    //点了Run
                {
                    if (parent.bBusy)       //Scan功能是否被其他SFL占用
                    {
                        gm.controls = "Run button";
                        gm.message = LibErrorCode.GetErrorDescription(LibErrorCode.IDS_ERR_EM_THREAD_BKWORKER_BUSY);
                        gm.bupdate = true;
                        btn.Content = "Start";
                        btn.IsChecked = false;
                        isCPReentrant_Run = false;
                        Cursor = Cursors.Arrow;
                        return;
                    }
                    else
                        parent.bBusy = true;

                    if (msg.bgworker.IsBusy == true) //bus是否正忙
                    {
                        gm.controls = "Run button";
                        gm.message = LibErrorCode.GetErrorDescription(LibErrorCode.IDS_ERR_EM_THREAD_BKWORKER_BUSY);
                        gm.bupdate = true;
                        isCPReentrant_Run = false;
                        Cursor = Cursors.Arrow;
                        return;
                    }
                    ptBE.UpdateSource();
                    if (ViewModel.Ptarget < ViewModel.Pmin || ViewModel.Ptarget > ViewModel.Pmax)
                    {
                        MessageBox.Show("Out of range!");
                        CPEnterStopState(errorcode);
                        return;
                    }
                    //一切正常，可以开始scan
                    btn.Content = "Stop";
                    gm.message = "CP Start";
                    CCGroup.IsEnabled = false;
                    CPUpdate.IsEnabled = true;

                    avrPI.init();
                    //pid.kp = Convert.ToDouble(kPInput.Text);
                    //pid.ki = Convert.ToDouble(kIInput.Text);
                    //pid.kd = Convert.ToDouble(kDInput.Text);

                    #region 预读数据
                    errorcode = GetSysInfo();                                           //读GPIO信息 
                    if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    {
                        CPEnterStopState(errorcode);
                        return;
                    }

                    errorcode = GetDevInfo();                                           //读设备信息
                    if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    {
                        CPEnterStopState(errorcode);
                        return;
                    }
                    errorcode = Command(dynamicdatalist, 1);                                  //读一次动态数据
                    if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    {
                        CPEnterStopState(errorcode);
                        return;
                    }
                    errorcode = ConvertHexToPhysical(dynamicdatalist);                  //转换一次动态数据
                    if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    {
                        CPEnterStopState(errorcode);
                        return;
                    }
                    #endregion
                    UpdateChipData();

                    #region 准备预充或预放
                    if (!CloseFet())
                    {
                        CPEnterStopState(errorcode);
                        MessageBox.Show("Cannot close FET!");
                        return;
                    }
                    if (sflname == "PreCharge")
                        Write(PRE_SEL, 1);
                    else if (sflname == "PreDischarge")
                        Write(PRE_SEL, 2);

                    AdjustCurrent(ViewModel.Ptarget / ViewModel.Vfet * 1000);
                    //Write(PRE_SET, ViewModel.Ptarget / ViewModel.Vfet * 1000);
                    //ViewModel.Ptarget = Read(PRE_SET) * ViewModel.Vfet / 1000;

                    if (!OpenFet())
                    {
                        CPEnterStopState(errorcode);
                        MessageBox.Show("Cannot open FET!");
                        return;
                    }
                    #endregion

                    Thread.Sleep(10);
                    errorcode = Command(dynamicdatalist, 1);                                  //读一次动态数据
                    if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    {
                        CPEnterStopState(errorcode);
                        return;
                    }
                    errorcode = ConvertHexToPhysical(dynamicdatalist);                  //转换一次动态数据
                    if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    {
                        CPEnterStopState(errorcode);
                        return;
                    }
                    UpdateChipData();
                    if (PowerSafetyCheck() == false)
                    {
                        CPEnterStopState(errorcode);
                        return;
                    }
                    #region Scan rate
                    tCP.Interval = 500;
                    tCP.Start();
                    #endregion
                }
                else    //点了stop
                {
                    CPEnterStopState(errorcode);
                }

                isCPReentrant_Run = false;
                Cursor = Cursors.Arrow;
            }
            else
            {
                CPrunBtn.IsChecked = !CPrunBtn.IsChecked;
            }
        }

        private void CPUpdate_Click(object sender, RoutedEventArgs e)
        {
            /*tCP.Stop();
            Thread.Sleep(200);
            Write(PRE_SET, ViewModel.Ptarget / ViewModel.Vfet * 1000);
            tCP.Start();*/
            //UInt32 errorcode = LibErrorCode.IDS_ERR_SUCCESSFUL;
            double Ptarget = Convert.ToDouble(PtargetInput.Text);
            if (Ptarget < ViewModel.Pmin || Ptarget > ViewModel.Pmax)
            {
                MessageBox.Show("Out of range!");
                //CPEnterStopState(errorcode);
                return;
            }
            ptBE.UpdateSource();
        }

        private delegate void UpdateCPRunningUIDelegate();
        private void UpdateCPRunningUI()
        {
            CPrunBtn.Content = "Start";
            CPrunBtn.IsChecked = false;
        }

        private delegate void AdjustPresetDelegate();
        public void AdjustCurrentByPower()
        {
            //UInt32 errorcode = LibErrorCode.IDS_ERR_SUCCESSFUL;
            //double cstep = 100 / ViewModel.Rsense.phydata;
            /*if (ViewModel.P < ViewModel.Ptarget)
            {
                double nextI = PRE_SET.phydata + cstep;
                if((nextI * ViewModel.Vfet / 1000.0) < ViewModel.Ptarget)
                    PRE_SET.phydata = nextI;
            }
            if (ViewModel.P > ViewModel.Ptarget)
                PRE_SET.phydata -= cstep;*/
            if (ViewModel.P <= ViewModel.Pmax)
            {
                AdjustCurrent(ViewModel.Ptarget / ViewModel.Vfet * 1000);
            }

        }

        void tCP_Elapsed(object sender, EventArgs e)
        {
            //定时到则读取并更新动态数据
            DateTime now = DateTime.Now;
            uint errorcode = 0;
            if (!msg.bgworker.IsBusy)
            {
                errorcode = Command(dynamicdatalist, 1);
                if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                {
                    tCP.Stop();
                    //this.Dispatcher.Invoke(new UpdateCCRunningUIDelegate(UpdateCPRunningUI));
                    gm.message = LibErrorCode.GetErrorDescription(errorcode);
                    gm.bupdate = true;
                    parent.bBusy = false;
                    CPEnterStopState(errorcode);
                    return;
                }
                errorcode = ConvertHexToPhysical(dynamicdatalist);
                if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                {
                    tCP.Stop();
                    gm.message = LibErrorCode.GetErrorDescription(errorcode);
                    gm.bupdate = true;
                    parent.bBusy = false;
                    CPEnterStopState(errorcode);
                    return;
                }
                //*/

                this.Dispatcher.Invoke(new UpdateChipDataDelegate(UpdateChipData));

                bool b = (bool)this.Dispatcher.Invoke(new CurrentSafetyCheckDelegate(PowerSafetyCheck));

                if (b == false)
                {
                    tCP.Stop();
                    gm.message = LibErrorCode.GetErrorDescription(errorcode);
                    gm.bupdate = true;
                    parent.bBusy = false;
                    CPEnterStopState(errorcode);
                    return;
                }

                this.Dispatcher.Invoke(new AdjustPresetDelegate(AdjustCurrentByPower));

                /*if (isAdjusted)
                {
                    errorcode = Command(dynamicdatalist, 1);
                    if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    {
                        tCP.Stop();
                        //this.Dispatcher.Invoke(new UpdateCCRunningUIDelegate(UpdateCPRunningUI));
                        gm.message = LibErrorCode.GetErrorDescription(errorcode);
                        gm.bupdate = true;
                        parent.bBusy = false;
                        CPEnterStopState(errorcode);
                        return;
                    }
                    errorcode = ConvertHexToPhysical(dynamicdatalist);
                    if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    {
                        tCP.Stop();
                        //this.Dispatcher.Invoke(new UpdateCCRunningUIDelegate(UpdateCPRunningUI));
                        gm.message = LibErrorCode.GetErrorDescription(errorcode);
                        gm.bupdate = true;
                        parent.bBusy = false;
                        CPEnterStopState(errorcode);
                        return;
                    }
                    //

                    this.Dispatcher.Invoke(new UpdateChipDataDelegate(UpdateChipData));
                }*/

                //this.Dispatcher.Invoke(new SafetyCheckDelegate(SafetyCheck));
            }
            else
            {
                int i = 0;
            }
        }
        #endregion

        #region DM提供的API
        public double Read(Parameter p)
        {
            ParamContainer pc = new ParamContainer();
            pc.parameterlist.Add(p);
            Read(pc);
            ConvertHexToPhysical(pc);
            return p.phydata;
        }
        public uint Read(ParamContainer pc)
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_READ;
            msg.task_parameterlist = pc;
            //msg.bupdate = false;            //需要从chip读数据
            //while (msg.bgworker.IsBusy) ;
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            return m_Msg.errorcode;
        }
        public uint Write(Parameter p, double value)
        {
            p.phydata = value;
            ParamContainer pc = new ParamContainer();
            pc.parameterlist.Add(p);
            ConvertPhysicalToHex(pc);
            return Write(pc);
        }
        public uint Write(ParamContainer pc)
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_WRITE;
            msg.task_parameterlist = pc;
            //while (msg.bgworker.IsBusy) ;
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            return m_Msg.errorcode;
        }
        public uint Command(ParamContainer pc, ushort sub_task)
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_COMMAND;
            msg.sub_task = sub_task;
            msg.task_parameterlist = pc;
            //msg.bupdate = false;            //需要从chip读数据
            //while (msg.bgworker.IsBusy) ;
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            return m_Msg.errorcode;
        }
        public uint ConvertHexToPhysical(ParamContainer pc)
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_CONVERT_HEXTOPHYSICAL;
            msg.task_parameterlist = pc;
            //msg.bupdate = true;         //不用从chip读，只从img读
            //while (msg.bgworker.IsBusy) ;
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            return m_Msg.errorcode;
        }

        public uint ConvertPhysicalToHex(ParamContainer pc)
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_CONVERT_PHYSICALTOHEX;
            msg.task_parameterlist = pc;
            //msg.bupdate = true;         //不用从chip读，只从img读
            //while (msg.bgworker.IsBusy) ;
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            return m_Msg.errorcode;
        }
        public uint GetSysInfo()
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_SPEICAL_GETSYSTEMINFOR;
            //msg.bupdate = false;            //需要从chip读数据
            //while (msg.bgworker.IsBusy) ;
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            return m_Msg.errorcode;
        }
        public uint GetDevInfo()
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_SPEICAL_GETDEVICEINFOR;
            //msg.bupdate = false;            //需要从chip读数据
            //while (msg.bgworker.IsBusy) ;
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            return m_Msg.errorcode;
        }
        public uint ClearBit(ParamContainer pc)
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_BITOPERATION;
            msg.task_parameterlist = pc;
            //msg.bupdate = false;            //需要从chip读数据
            //while (msg.bgworker.IsBusy) ;
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            return m_Msg.errorcode;
        }
        #endregion


    }


    public class SFLViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        //private Parameter paramR;

        private Parameter mRsense = new Parameter();
        public Parameter Rsense
        {
            get { return mRsense; }
            set {
                mRsense = value;
                OnPropertyChanged("Rsense");
            }
        }

        private double mPmax_set = 4000.0;
        public double Pmax_set
        {
            get
            {
                return Math.Round(mPmax_set, 2);
            }
            set
            {
                mPmax_set = value;
                OnPropertyChanged("Pmax_set");
            }
        }

        private double mImax_set = 6000.0;
        public double Imax_set
        {
            get
            {
                return Math.Round(mImax_set, 2);
            }
            set
            {
                mImax_set = value;
                OnPropertyChanged("Imax_set");
            }
        }


        private double mOVTH = 3300.0;
        public double OVTH
        {
            get
            {
                return Math.Round(mOVTH, 2);
            }
            set
            {
                mOVTH = value;
                OnPropertyChanged("OVTH");
            }
        }

        private double mPmax_cal = 4.0;
        public double Pmax_cal
        {
            get
            {
                return Math.Round(mPmax_cal, 2);
            }
            set
            {
                mPmax_cal = value;
                OnPropertyChanged("Pmax_cal");
            }
        }

        private double mImax_cal = 5.0;
        public double Imax_cal
        {
            get
            {
                return Math.Round(mImax_cal, 2);
            }
            set
            {
                mImax_cal = value;
                OnPropertyChanged("Imax_cal");
            }
        }

        private double mPmax = 0.0;
        public double Pmax
        {
            get
            {
                return Math.Round(mPmax, 2);
            }
            set
            {
                mPmax = value;
                OnPropertyChanged("Pmax");
            }
        }

        private double mPmin = 0.0;
        public double Pmin
        {
            get
            {
                return Math.Round(mPmin, 2);
            }
            set
            {
                mPmin = value;
                OnPropertyChanged("Pmin");
            }
        }

        private double mImax = 0.0;
        public double Imax
        {
            get
            {
                return Math.Round(mImax, 2);
            }
            set
            {
                mImax = value;
                OnPropertyChanged("Imax");
            }
        }

        private double mImin = 0.0;
        public double Imin
        {
            get
            {
                return Math.Round(mImin, 2);
            }
            set
            {
                mImin = value;
                OnPropertyChanged("Imin");
            }
        }

        private double mPtarget = 1000.0;
        public double Ptarget
        {
            get
            {
                return Math.Round(mPtarget, 2);
            }
            set
            {
                mPtarget = value;
                OnPropertyChanged("Ptarget");
            }
        }

        private double mItarget = 1000.0;
        public double Itarget
        {
            get
            {
                return Math.Round(mItarget, 2);
            }
            set
            {
                mItarget = value;
                OnPropertyChanged("Itarget");
            }
        }

        private double mIpre = 0.0;
        public double Ipre
        {
            get
            {
                return Math.Round(mIpre, 2);
            }
            set
            {
                mIpre = value;
                OnPropertyChanged("Ipre");
            }
        }

        private double mVpack = 0.0;
        public double Vpack
        {
            get
            {
                return Math.Round(mVpack, 2);
            }
            set
            {
                mVpack = value;
                OnPropertyChanged("Vpack");
            }
        }

        private double mVbatt = 0.0;
        public double Vbatt
        {
            get
            {
                return Math.Round(mVbatt, 2);
            }
            set
            {
                mVbatt = value;
                OnPropertyChanged("Vbatt");
            }
        }

        private double mVfet = 0.0;
        public double Vfet
        {
            get
            {
                return Math.Round(mVfet, 2);
            }
            set
            {
                mVfet = value;
                OnPropertyChanged("Vfet");
            }
        }

        private double mI = 0.0;
        public double I
        {
            get
            {
                return Math.Round(mI, 2);
            }
            set
            {
                mI = value;
                OnPropertyChanged("I");
            }
        }

        private double mP = 0.0;
        public double P
        {
            get
            {
                return Math.Round(mP, 2);
            }
            set
            {
                mP = value;
                OnPropertyChanged("P");
            }
        }
    }
}
