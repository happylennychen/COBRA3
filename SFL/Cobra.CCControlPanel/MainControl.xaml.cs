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

namespace Cobra.CCControlPanel
{

    /// <summary>
    /// Interaction logic for MainControl.xaml
    /// </summary>
    public partial class MainControl
    {
        #region variable defination
        //父对象保存

        bool isReentrant_Run = false;   //控制Run button的重入
        private byte count = 0;
 
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

        private GeneralMessage gm = new GeneralMessage("CCControl SFL", "", 0);

        private AsyncObservableCollection<CCPoint> mCCPointList = new AsyncObservableCollection<CCPoint>();
        public AsyncObservableCollection<CCPoint> CCPointList
        {
            get { return mCCPointList; }
            set
            {
                mCCPointList = value;
                //OnPropertyChanged("CCPointList");
            }
        }

        private System.Timers.Timer t = new System.Timers.Timer();
        Parameter SW_CHG_CTRL = new Parameter();
        Parameter SW_DSG_CTRL = new Parameter();
        Parameter PRE_SEL = new Parameter();
        Parameter PRE_SET = new Parameter();
        Parameter PRE_DSG = new Parameter();
        Parameter PRE_CHG = new Parameter();
        Parameter DSG_DRV = new Parameter();
        Parameter CHG_DRV = new Parameter();
        AsyncObservableCollection<Parameter> paramlist = new AsyncObservableCollection<Parameter>();
        private bool ischarge = true;
        #endregion

        public MainControl(object pParent, string name)
        {
            InitializeComponent();

            #region 相关初始化
            parent = (Device)pParent;
            if (parent == null) return;

            sflname = name;
            if (String.IsNullOrEmpty(sflname)) return;

            #endregion

            CCPoints.DataContext = CCPointList;

            paramlist = parent.GetParamLists("PreDischarge").parameterlist;
            foreach (Parameter param in paramlist)
            {
                string pName = GetHashTableValueByKey("Name", param.sfllist["PreDischarge"].nodetable);
                if (pName == "PRE_SET")
                    PRE_SET = param;
                else if (pName == "SW_CHG_CTRL")
                    SW_CHG_CTRL = param;
                else if (pName == "SW_DSG_CTRL")
                    SW_DSG_CTRL = param;
                else if (pName == "PRE_SEL")
                    PRE_SEL = param;
                else if (pName == "PRE_DSG")
                    PRE_DSG = param;
                else if (pName == "PRE_CHG")
                    PRE_CHG = param;
                else if (pName == "DSG_DRV")
                    DSG_DRV = param;
                else if (pName == "CHG_DRV")
                    CHG_DRV = param;
            }


            

            t.Elapsed += new ElapsedEventHandler(t_Elapsed);

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

                if ((PRE_CHG.phydata != 1 && ischarge) || (PRE_DSG.phydata != 1 && !ischarge))
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
                    if (i < 3)
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
                    }
                    else
                        return false;
                }
                else
                    return true;
            }
        }

        #region CC Process
        private void EnterStopState(UInt32 errorcode)
        {
            t.Stop();

            if (!CloseFet())
                MessageBox.Show("Cannot close FET!");

            gm.message = LibErrorCode.GetErrorDescription(errorcode);
            gm.bupdate = true;

            runBtn.Content = "Run";
            runBtn.IsChecked = false;
            //ScanInterval.IsEnabled = true;
            //SubTask.IsEnabled = true;
            parent.bBusy = false;
            isReentrant_Run = false;
            Cursor = Cursors.Arrow;
        }


        private void Add_Click(object sender, RoutedEventArgs e)
        {
            CCPoint ccp = new CCPoint();
            ccp.Ipre = Convert.ToDouble(CPInput.Text);
            Write(PRE_SET, ccp.Ipre);
            ccp.Ipre = Read(PRE_SET);
            CCPointList.Add(ccp);
        }
        private void RunBtn_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton btn = sender as ToggleButton;
            uint errorcode = 0;
            if (isReentrant_Run == false)   //此次点击并没有重入
            {
                isReentrant_Run = true;
                Cursor = Cursors.Wait;
                if ((bool)btn.IsChecked)    //点了Run
                {
                    if (parent.bBusy)       //Scan功能是否被其他SFL占用
                    {
                        gm.controls = "Run button";
                        gm.message = LibErrorCode.GetErrorDescription(LibErrorCode.IDS_ERR_EM_THREAD_BKWORKER_BUSY);
                        gm.bupdate = true;
                        btn.Content = "Run";
                        btn.IsChecked = false;
                        isReentrant_Run = false;
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
                        btn.Content = "Run";
                        btn.IsChecked = false;
                        isReentrant_Run = false;
                        Cursor = Cursors.Arrow;
                        return;
                    }
                    //一切正常，可以开始scan
                    btn.Content = "Stop";
                    gm.message = "CC Start";

                    #region 准备预充或预放
                    if (!CloseFet())
                    {
                        EnterStopState(errorcode);
                        MessageBox.Show("Cannot close FET!");
                        return;
                    }

                    if (ModeSelect.SelectedIndex == 0)
                    {
                        Write(PRE_SEL, 1);
                        ischarge = true;
                    }
                    else if (ModeSelect.SelectedIndex == 1)
                    {
                        Write(PRE_SEL, 2);
                        ischarge = false;
                    }

                    //Write(PRE_SET, CCPointList[0].Ipre);
                    //ViewModel.Itarget = Read(PRE_SET);
                    #endregion
                    #region Scan rate
                    t.Interval = Convert.ToDouble(Period.Text);
                    t.Start();
                    #endregion
                }
                else    //点了stop
                {
                    EnterStopState(errorcode);
                }

                isReentrant_Run = false;
                Cursor = Cursors.Arrow;
            }
            else
            {
                runBtn.IsChecked = !runBtn.IsChecked;
            }
        }

        void t_Elapsed(object sender, EventArgs e)
        {

            UInt32 errorcode = LibErrorCode.IDS_ERR_SUCCESSFUL;
            errorcode = Write(PRE_SET, CCPointList[count].Ipre);
            if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                EnterStopState(errorcode);
                return;
            }

            count++;
            if (count == CCPointList.Count)
                count = 0;

            if (!OpenFet())
            {
                MessageBox.Show("Cannot open FET!");
                EnterStopState(errorcode);
                return;
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
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            return m_Msg.errorcode;
        }
        #endregion


    }


    public class CCPoint : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
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
    }
}
