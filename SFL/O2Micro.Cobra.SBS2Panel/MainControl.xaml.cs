using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
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
using System.Windows.Threading;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Forms;
using System.Xml;
using O2Micro.Cobra.EM;
using O2Micro.Cobra.Common;

namespace O2Micro.Cobra.SBS2Panel
{
    enum SBSType
    {
        Dynamic_SBSType = 0,
        Static_SBSType = 1,
        Wave_SBSType = 2,
        Status_SBSType = 3
    }

    /// <summary>
    /// Interaction logic for MainControl.xaml
    /// </summary>
    public partial class MainControl : System.Windows.Controls.UserControl
    {
        #region
        //父对象保存
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

        private SFLViewMode m_viewmode;
        public SFLViewMode viewmode
        {
            get { return m_viewmode; }
            set { m_viewmode = value; }
        }

        public GeneralMessage gm = new GeneralMessage("SBS SFL", "", 0);
        private GasGaugeInterface m_GasGauge = null;
        private DispatcherTimer BatteryPoll = new DispatcherTimer();

        #region 数据队列
        private List<string> m_Path_List = new List<string>();
        public List<string> path_list
        {
            get { return m_Path_List; }
            set { m_Path_List = value; }
        }
        #endregion
        #endregion

        public MainControl(object pParent, string name)
        {
            InitializeComponent();

            #region 相关初始化
            parent = (Device)pParent;
            if (parent == null) return;

            sflname = name;
            if (String.IsNullOrEmpty(sflname)) return;

            m_Msg.gm.sflname = sflname;
            m_Msg.gm.level = 2;
            m_Msg.gm.controls = sflname;

            gm.PropertyChanged += new PropertyChangedEventHandler(gm_PropertyChanged);
            msg.PropertyChanged += new PropertyChangedEventHandler(msg_PropertyChanged);
            msg.gm.PropertyChanged += new PropertyChangedEventHandler(gm_PropertyChanged);

            viewmode = new SFLViewMode(pParent, this);
            Init();

            BatteryPoll.Tick += new EventHandler(BatteryPoll_Elapsed);
            #endregion

            #region UC数据分发
            systeminforlb1.ItemsSource = viewmode.sfl_tempLeftInfor_parameterlist;
            systeminfrolb2.ItemsSource = viewmode.sfl_tempMidInfor_parameterlist;
            systeminfrolb3.ItemsSource = viewmode.sfl_tempRightInfor_parameterlist;
            statusctrl.SetDataSource(this,viewmode.sfl_status_parameterlist);
            //battery.SetDataSource(viewmode.sfl_batteryInfor_parameterlist);
            wavectrl.SetDataSource(viewmode.sfl_wave_parameterlist);
            #endregion
            LibInfor.AssemblyRegister(Assembly.GetExecutingAssembly(), ASSEMBLY_TYPE.SFL);
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
                                break;
                            }
                    }
                    break;
            }
        }

        #region 通用控件消息响应
        public void CallWarningControl(GeneralMessage message)
        {
            WarningPopControl.Dispatcher.Invoke(new Action(() =>
            {
                WarningPopControl.ShowDialog(message);
            }));
        }
        #endregion

        private void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            wavectrl.bftime = false;
            if ((bool)btnscan.IsChecked)    //点了Run
            {
                wavectrl.Inital(); //清除波形组上一次数据
                btnscan.Content = "Stop";
                gm.message = "Read Device";
                BatteryPoll.Interval = TimeSpan.FromMilliseconds(1000);
                BatteryPoll.Start();
            }
            else    //点了stop
            {
                BatteryPoll.Stop();
                btnscan.Content = "Run";
            }
            Cursor = System.Windows.Input.Cursors.Arrow;
        }

        private void Init()
        {
            wavectrl.parent = this;
        }

        private void BatteryPoll_Elapsed(object sender, EventArgs e)
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;

            if (!msg.bgworker.IsBusy)
            {
                ret = Read();
                if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                {
                    //BatteryPoll.Stop();
                    gm.level = 2;
                    gm.controls = "SBS Monitor!";
                    gm.message = LibErrorCode.GetErrorDescription(ret);
                    
                    CallWarningControl(gm);
                    gm.bupdate = true;
                    //btnscan.Content = "Run".ToString();
                    //btnscan.IsChecked = false;
                    //return;
                }
            }
            //battery.update();
            wavectrl.update();
            statusctrl.update();
        }
		
		private UInt32 Read()
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task_parameterlist = viewmode.dm_parameterlist;
            /*
            msg.brw = false;
            msg.task = TM.TM_SPEICAL_GETDEVICEINFOR;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                return msg.errorcode;

            msg.task = TM.TM_SPEICAL_GETREGISTEINFOR;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                return msg.errorcode;*/

            msg.task = TM.TM_READ;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            ret |= msg.errorcode;
            //if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                //return msg.errorcode;

            msg.task = TM.TM_CONVERT_HEXTOPHYSICAL;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            ret |= msg.errorcode;
            return ret;
        }

        public void WriteOneBtn_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.ContentControl wbtn = (System.Windows.Controls.ContentControl)sender;

            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            if (parent.bBusy)
            {
                gm.level = 1;
                gm.controls = "Write To Device button!";
                gm.message = LibErrorCode.GetErrorDescription(LibErrorCode.IDS_ERR_EM_THREAD_BKWORKER_BUSY);
                gm.bupdate = true;
                CallWarningControl(gm);
                return;
            }
            else
                parent.bBusy = true;

            ret = viewmode.WriteDevice(wbtn.Uid);
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                gm.level = 2;
                gm.message = LibErrorCode.GetErrorDescription(ret);
                CallWarningControl(gm);
                parent.bBusy = false;
                return;
            }

            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.gm.controls = ((System.Windows.Controls.ContentControl)sender).Content.ToString();
            msg.task_parameterlist = viewmode.dm_wo_parameterlist;

            msg.brw = false;
            msg.task = TM.TM_SPEICAL_GETDEVICEINFOR;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                gm.level = 2;
                gm.message = LibErrorCode.GetErrorDescription(msg.errorcode);
                CallWarningControl(gm);
                parent.bBusy = false;
                return;
            }

            msg.task = TM.TM_SPEICAL_GETREGISTEINFOR;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                gm.level = 2;
                gm.message = LibErrorCode.GetErrorDescription(msg.errorcode);
                CallWarningControl(gm);
                parent.bBusy = false;
                return;
            }

            msg.task = TM.TM_READ;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                gm.level = 2;
                gm.message = LibErrorCode.GetErrorDescription(msg.errorcode);
                CallWarningControl(gm);
                parent.bBusy = false;
                return;
            }

            msg.task = TM.TM_CONVERT_PHYSICALTOHEX;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();

            msg.task = TM.TM_WRITE;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                gm.level = 2;
                gm.message = LibErrorCode.GetErrorDescription(msg.errorcode);
                CallWarningControl(gm);
                parent.bBusy = false;
                return;
            }

            msg.task = TM.TM_READ;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                gm.level = 2;
                gm.message = LibErrorCode.GetErrorDescription(msg.errorcode);
                CallWarningControl(gm);
                parent.bBusy = false;
                return;
            }

            msg.task = TM.TM_CONVERT_HEXTOPHYSICAL;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();

            parent.bBusy = false;
        }
    }
}
