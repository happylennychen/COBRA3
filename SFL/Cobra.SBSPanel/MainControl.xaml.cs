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
using Cobra.EM;
using Cobra.Common;

namespace Cobra.SBSPanel
{
    enum SBSType
    {
        Dynamic_SBSType = 0,
        Static_SBSType = 1,
        Wave_SBSType = 2,
        Charger_SBSType = 3
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

        private bool m_bSeaElf = true;
        public bool bSeaElf
        {
            get { return m_bSeaElf; }
            set { m_bSeaElf = value; }
        }

        private bool m_bJump = false;
        public bool bjump
        {
            get { return m_bJump; }
            set { m_bJump = value; }
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
            SBSProjectControl.pathdatagrid.ItemsSource = viewmode.path_parameterlist;
            SBSProjectControl.parent = this;

            SBSProjectControl.paramdatagrid.ItemsSource = viewmode.param_parameterlist;
            SBSProjectControl.parent = this;

            systeminforlb1.ItemsSource = viewmode.sfl_tempRightInfor_parameterlist;
            systeminfrolb2.ItemsSource = viewmode.sfl_tempLeftInfor_parameterlist;
            chargerctrl.SetDataSource(viewmode.sfl_charger_parameterlist);
            battery.SetDataSource(viewmode.sfl_batteryInfor_parameterlist);
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

        public void CallSBSProjectControl(bool bshow)
        {
            SBSProjectControl.Dispatcher.Invoke(new Action(() =>
            {
                SBSProjectControl.ShowDialog(bshow);
            }));
        }
        #endregion

        private void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            bjump = false;
            wavectrl.bftime = false;
            if ((bool)btnscan.IsChecked)    //点了Run
            {
                if (parent.bBusy)
                {
                    gm.controls = "Read From Device button";
                    gm.message = LibErrorCode.GetErrorDescription(LibErrorCode.IDS_ERR_EM_THREAD_BKWORKER_BUSY);
                    gm.bupdate = true;
                    CallWarningControl(gm);
                    return;
                }
                else
                {
                    path_list.Clear();
                    foreach (PathModel spath in viewmode.path_parameterlist)
                    {
                        if (spath.path != String.Empty)
                            path_list.Add(spath.path);
                    }
                    if (!m_GasGauge.InitializeGG(parent, m_Msg, viewmode.ggpccs_parameterlist, viewmode.ggpcsr_parameterlist, viewmode.ggpsbsr_parameterlist, path_list))
                    {
                        gm.controls = "Initial Gas Gauge";
                        gm.message = LibErrorCode.GetErrorDescription(LibErrorCode.IDS_ERR_SBSSFL_GG_ACCESS);
                        gm.level = 2;
                        gm.bupdate = true;
                        CallWarningControl(gm);
                        btnscan.IsChecked = false;
                        return;
                    }
                    wavectrl.curproject = m_GasGauge.GetProjectFile();
                    Cursor = System.Windows.Input.Cursors.Wait;
                }
                wavectrl.Inital(); //清除波形组上一次数据
                btnreset.IsEnabled = false;
                btnscan.Content = "Stop";
                gm.message = "Read Device";
                BatteryPoll.Interval = TimeSpan.FromMilliseconds(1000);
                BatteryPoll.Start();
            }
            else    //点了stop
            {
				m_GasGauge.UnloadGG();
                BatteryPoll.Stop();
                btnscan.Content = "Run";
                btnreset.IsEnabled = true;
            }
            Cursor = System.Windows.Input.Cursors.Arrow;
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            SBSProjectControl.Visibility = Visibility.Visible;
        }

        private void Init()
        {
            wavectrl.parent = this;

            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Cobra.GasGauge.dll"); 
            var varDllLocat = Assembly.LoadFile(path);
            foreach (Type t in varDllLocat.GetExportedTypes())
            {
                m_GasGauge = (GasGaugeInterface)Activator.CreateInstance(t);
                break;
            }

            #region 参数初始化
            ParamModel pmodel = new ParamModel();
            pmodel.name = "CutOff Voltage";
            pmodel.dval = 3400;
            pmodel.units = "mV";
            viewmode.param_parameterlist.Add(pmodel);

            pmodel = new ParamModel();
            pmodel.name = "Wire Impedance";
            pmodel.dval = 0;
            pmodel.units = "mohm";
            viewmode.param_parameterlist.Add(pmodel);
            #endregion

            #region Table文件初始化
            PathModel model = new PathModel();
            model.name = "SBS Project Package";
            model.path = FolderMap.m_root_folder + @"\tables\Project_Lenovo_BL216.xml";
            model.btncommand = "Project";
            viewmode.path_parameterlist.Add(model);

            model = new PathModel();
            model.name = "OCVbyTSOC Table";
            model.path = FolderMap.m_root_folder + @"\tables\OCVbySOC_BL216_Lenovo_3050mAh_V002_4350-3300mV_65p_04282014.txt";
            model.btncommand = "OCVbyTSOC";
            viewmode.path_parameterlist.Add(model);

            model = new PathModel();
            model.name = "TSOCbyOCV Table";
            model.path = FolderMap.m_root_folder + @"\tables\SOCbyOCV_BL216_Lenovo_3050mAh_V002_4350-3300mV_16mV_04282014.txt";
            model.btncommand = "TSOCbyOCV";
            viewmode.path_parameterlist.Add(model);

            model = new PathModel();
            model.name = "RC Table";
            model.path = FolderMap.m_root_folder + @"\tables\RC_Lenovo_BL238_2404mAhr_4350mV_3300mV_V002_20141127.txt";
            model.btncommand = "RC";
            viewmode.path_parameterlist.Add(model);

            model = new PathModel();
            model.name = "Thermal Table";
            model.path = FolderMap.m_root_folder + @"\tables\Thermal_Semitec_103JT025_V002_01142011.txt";
            model.btncommand = "Thermal";
            viewmode.path_parameterlist.Add(model);

            model = new PathModel();
            model.name = "Self Discharge Table";
            model.path = FolderMap.m_root_folder + @"\tables\Selfdis_O2_Generic_V002_09282005.txt";
            model.btncommand = "Self Discharge";
            viewmode.path_parameterlist.Add(model);

            model = new PathModel();
            model.name = "Reoc(T) Table";
            model.path = FolderMap.m_root_folder + @"\tables\Roec(T)_LENOVO_BL216_3050mAh_003_04222014.txt";
            model.btncommand = "Reoc(T)";
            viewmode.path_parameterlist.Add(model);
            #endregion
        }

        private void BatteryPoll_Elapsed(object sender, EventArgs e)
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            if (bjump)
            {
                if (!msg.bgworker.IsBusy)
                {
                    ret = Read();
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    {
                        m_GasGauge.UnloadGG();
                        BatteryPoll.Stop();
                        gm.message = LibErrorCode.GetErrorDescription(ret);
                        CallWarningControl(gm);
                        gm.bupdate = true;
                        btnscan.Content = "Run".ToString();
                        btnscan.IsChecked = false;
                        btnreset.IsEnabled = true;
                        return;
                    }
                    if (!bSeaElf)
                        ConvertHexToPhysical();
                }
                battery.update();
                wavectrl.update();
                chargerctrl.update(bSeaElf);
            }
            bjump = true;
        }

        private UInt32 Read()
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            #region 访问GG
			if (m_GasGauge.GetStatus() == LibErrorCode.IDS_ERR_SUCCESSFUL)
			{
				foreach (SFLModel model in viewmode.sfl_parameterlist)
					model.data = model.parent.phydata;
			}
			else
			{
				ret = m_GasGauge.GetStatus();
				return ret;
			}
            #endregion

            #region 访问SeaElf
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.gm.controls = "SBS Thread";
            msg.brw = true;
            parent.bBusy = true;
            msg.task = TM.TM_SPEICAL_GETREGISTEINFOR;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
           
            bSeaElf = m_Msg.sm.parts[0];
            parent.bBusy = false;

            if (!bSeaElf)
            {
                msg.owner = this;
                msg.gm.sflname = sflname;
                msg.gm.controls = "SBS Thread";
                msg.brw = true;
                parent.bBusy = true;
                msg.task = TM.TM_READ;
                msg.task_parameterlist = viewmode.charger_parameterlist;
                parent.AccessDevice(ref m_Msg);
                while (msg.bgworker.IsBusy)
                    System.Windows.Forms.Application.DoEvents();
                parent.bBusy = false;
            }
            #endregion
            return ret;
        }

        private UInt32 ConvertHexToPhysical()
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            msg.task = TM.TM_CONVERT_HEXTOPHYSICAL;

            foreach (Parameter param in viewmode.charger_parameterlist.parameterlist)
                param.PropertyChanged += viewmode.Parameter_PropertyChanged;

            msg.task_parameterlist = viewmode.charger_parameterlist;
            parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();

            foreach (Parameter param in viewmode.charger_parameterlist.parameterlist)
                param.PropertyChanged -= viewmode.Parameter_PropertyChanged;
            ret = msg.errorcode;
            return ret;
        }

        public void wavectrl_init()
        {
            foreach (ParamModel p in viewmode.param_parameterlist)
            {
                if (p == null) continue;
                switch(p.name)
                {
                    case "CutOff Voltage":
                        wavectrl.cutoff_voltage = p.dval;
                        break;
                    case "Wire Impedance":
                        wavectrl.wire_impedance = p.dval;
                        break;
                }
            }
        }
    }
}
