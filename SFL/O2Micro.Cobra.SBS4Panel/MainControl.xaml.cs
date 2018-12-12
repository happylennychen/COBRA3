using System;
using System.Collections.Generic;
using System.Collections;
using System.Data;
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
using System.Threading;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Forms;
using System.Xml;
using O2Micro.Cobra.EM;
using O2Micro.Cobra.Common;

namespace O2Micro.Cobra.SBS4Panel
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

        private CobraLog m_sbslog;
        public CobraLog sbslog
        {
            get { return m_sbslog; }
            set { m_sbslog = value; }
        }

		public bool bFranTestMode = false;

        private byte m_count;
        public byte count
        {
            get { return m_count; }
            set { m_count = value; }
        }

        #region 数据队列
        private List<string> m_Path_List = new List<string>();
        public List<string> path_list
        {
            get { return m_Path_List; }
            set { m_Path_List = value; }
        }
        #endregion
        #endregion

        private string GetHashTableValueByKey(string str, Hashtable htable)
        {
            foreach (DictionaryEntry de in htable)
            {
                if (de.Key.ToString().Equals(str))
                    return de.Value.ToString();
            }
            return "NoSuchKey";
        }

        public MainControl(object pParent, string name)
        {
            InitializeComponent();
            count = 0;

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

            #region log初始化
            //Get folder name
            string logfolder = System.IO.Path.Combine(FolderMap.m_currentproj_folder, "SBSLog\\");
            if (!Directory.Exists(logfolder))
                Directory.CreateDirectory(logfolder);
            sbslog = new CobraLog(logfolder, 10);
            //将目录中已有的可识别的logdata加入scanlog.logdatalist中
            sbslog.SyncLogData();
            #endregion

			if(bFranTestMode)
				viewmode.folder_path = "C:\\Documents and Settings\\francis.hsieh\\My Documents\\COBRA Documents\\bwF\\Samsung 18650";

            #region UC数据分发
            SBSProjectControl.pathdatagrid.ItemsSource = viewmode.path_parameterlist;
            SBSProjectControl.parent = this;

            //SBSProjectControl.paramdatagrid.ItemsSource = viewmode.param_parameterlist;
            SBSProjectControl.parent = this;

            systeminforlb1.ItemsSource = viewmode.sfl_tempLeftInfor_parameterlist;
            systeminfrolb2.ItemsSource = viewmode.sfl_tempRightInfor_parameterlist;
            statusctrl.SetDataSource(this,viewmode.sfl_status_parameterlist);
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
					if (!GetDeviceAllInformation())     //(M151002)Francis, call GetRegisterInfo to read ChgEndCurr from chip (if there is)
					{
						gm.controls = "Communication failed";
						gm.message = LibErrorCode.GetErrorDescription(LibErrorCode.IDS_ERR_SBSSFL_WRITE_REGISTER);
						gm.level = 2;
						gm.bupdate = true;
						CallWarningControl(gm);
						btnscan.IsChecked = false;
						parent.bBusy = false;
						return;
					}
					parent.bBusy = true;
					if (!LoadGasGauge())
					{
						System.Windows.MessageBox.Show("Error on loading GasGauge Dll");
						parent.bBusy = false;
						return;
					}
					if (m_GasGauge == null)
					{
						System.Windows.MessageBox.Show("Error on loading GasGauge Dll");
						parent.bBusy = false;
						return;
					}
					//(D150902)Francis, move to SBSProjectContro.xaml.cs
                    //path_list.Clear();
                    //foreach (PathModel spath in viewmode.path_parameterlist)
                    //{
                        //if (spath.path != String.Empty)
                            //path_list.Add(spath.path);
                    //}
					//(E150902)
                    //(A150717)Francis
					if (!ReadOpReg(true))
                    {
                        gm.controls = "Communication failed";
						gm.message = LibErrorCode.GetErrorDescription(LibErrorCode.IDS_ERR_SBSSFL_WRITE_REGISTER);
                        gm.level = 2;
                        gm.bupdate = true;
                        CallWarningControl(gm);
                        btnscan.IsChecked = false;
						parent.bBusy = false;
                        return;
                    }
					SetChipWakeup();
					PollingOpReg();
                    //(E150717)
                    if (!m_GasGauge.InitializeGG(null, null, 
                                                    viewmode.ggpccs_parameterlist, 
                                                    viewmode.ggpcsr_parameterlist, 
                                                    viewmode.ggpsbsr_parameterlist, 
                                                    path_list))
                    {
                        gm.controls = "Initial Gas Gauge";
                        gm.message = LibErrorCode.GetErrorDescription(m_GasGauge.GetStatus());
                        gm.level = 2;
                        gm.bupdate = true;
                        CallWarningControl(gm);
                        btnscan.IsChecked = false;
						parent.bBusy = false;
                        return;
                    }
					WriteOpReg(); //(A150728)Francis
                    wavectrl.curproject = m_GasGauge.GetProjectFile();
                    Cursor = System.Windows.Input.Cursors.Wait;
                }
                wavectrl.Inital(); //清除波形组上一次数据
                btnreset.IsEnabled = false;
                btnscan.Content = "Stop";
                gm.message = "Read Device";

                #region new logdata
                string str = DateTime.Now.Year.ToString("D4");
                str += DateTime.Now.Month.ToString("D2");
                str += DateTime.Now.Day.ToString("D2");
                str += DateTime.Now.Hour.ToString("D2");
                str += DateTime.Now.Minute.ToString("D2");
                str += DateTime.Now.Second.ToString("D2");
                LogData ld = new LogData(str + "Log" + ".csv.tmp", sbslog);
                //根据dynamicdatalist中的数据来初始化DataTable的column
                List<string> strlist = new List<string>();
                foreach (Parameter param in viewmode.ggpsbsr_parameterlist)
                {
                    str = GetHashTableValueByKey("Name", param.sfllist[sflname].nodetable);
                    if(str != "NoSuchKey")
                        strlist.Add(str);
                }
                ld.BuildColumn(strlist, true);
                sbslog.logdatalist.Add(ld);
                //logfilelist.ScrollIntoView(logfilelist.Items[logfilelist.Items.Count - 1]); //scroll to the last item
                #endregion

				//(A150722)Francis
				Read();
				SaveLogAfterSuccedRead();
				battery.update();
				wavectrl.update();
				statusctrl.update();
				//(E150722)
                BatteryPoll.Interval = TimeSpan.FromMilliseconds(4000);
                BatteryPoll.Start();
            }
            else    //点了stop
            {
                count = 0;
				if(m_GasGauge != null)
					m_GasGauge.UnloadGG();
                BatteryPoll.Stop();
                btnscan.Content = "Run";
                btnreset.IsEnabled = true;
                parent.bBusy = false;
                #region 储存剩余数据到logdata, logdatalist中的最后一个，即为当前的logdata
                LogData lg = sbslog.logdatalist[sbslog.logdatalist.Count - 1];
                lg.Save2Temp();
                lg.Complete();
                #endregion
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
            model.path = FolderMap.m_root_folder + @"\tables\RC_LENOVO_BL216_3050mAh_4300-3300mV_V004_05092014.txt";
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

			model = new PathModel();
			model.name = "Charge Table";
			model.path = FolderMap.m_root_folder + @"\tables\Charge_LENOVO_BL216_3050mAh_003_04222014.txt";
			model.btncommand = "Charge";
			viewmode.path_parameterlist.Add(model);
			#endregion
        }

        private void BatteryPoll_Elapsed(object sender, EventArgs e)
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;

            if (count < 1)
            {
                count++;
                return;
            }

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
					parent.bBusy = false;

                    #region 储存剩余数据到logdata, logdatalist中的最后一个，即为当前的logdata
                    LogData lg = sbslog.logdatalist[sbslog.logdatalist.Count - 1];
                    lg.Save2Temp();
                    //lg.Complete();
                    #endregion

                    return;
                }
				SaveLogAfterSuccedRead();
            }
            battery.update();
            wavectrl.update();
            statusctrl.update();
        }
		
		private UInt32 Read()
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            #region 访问GG
            //(A150717)Francis, call gasgauge to calculate RSOC
            if (PollingOpReg())
            {
                m_GasGauge.CalculateGasGauge();
            }
            else
            {
				return LibErrorCode.IDS_ERR_SBSSFL_POLLING_REGISTER;
            }
            if (m_GasGauge.GetStatus() == LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                foreach (SFLModel model in viewmode.sfl_parameterlist)
                    model.data = model.parent.phydata;
				//(A150722)Francis,  check is there register writing request
				WriteOpReg();
            }
            else
            {
                ret = m_GasGauge.GetStatus();
            }
            #endregion
            return ret;
        }

        public void wavectrl_init()
        {
            foreach (ParamModel p in viewmode.param_parameterlist)
            {
                if (p == null) continue;
                switch (p.name)
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

		private void SaveLogAfterSuccedRead()
		{
			#region log
			#region save data to buffer
			LogData ld = sbslog.logdatalist[sbslog.logdatalist.Count - 1];
			DataTable table = ld.logbuf;
			DataRow row = table.NewRow();

			foreach (Parameter param in viewmode.ggpsbsr_parameterlist)
			{
				string str = GetHashTableValueByKey("Name", param.sfllist[sflname].nodetable);
				if (str != "NoSuchKey")
					row[str] = param.phydata.ToString();
			}
			row["Time"] = DateTime.Now;
			table.Rows.Add(row);
			#endregion
			#region transfer buffer to harddisk
			if (table.Rows.Count >= sbslog.logbuflen)
				ld.Save2Temp();
			#endregion
			#endregion
		}

        //(A150717)Francis
        public bool ReadOpReg(bool bReadAll = false)
        {
            bool bReturn = true;
            if(bReadAll)
            {
                bReturn &= viewmode.ReadOpRegFromDevice(ref m_Msg, viewmode.ggpcsr_parameterlist);
            }
            else
            {
                AsyncObservableCollection<Parameter>    pmlisttmp = new AsyncObservableCollection<Parameter>();

                foreach(Parameter pmr in viewmode.ggpcsr_parameterlist)
                {
                    if(pmr.errorcode == LibErrorCode.IDS_ERR_SBSSFL_REQUIREREAD)
                    {
                        pmlisttmp.Add(pmr);
                        pmr.errorcode = LibErrorCode.IDS_ERR_SUCCESSFUL;
                    }
                }
                bReturn &= viewmode.ReadOpRegFromDevice(ref m_Msg, pmlisttmp);
            }

            return bReturn;
        }

        public bool WriteOpReg(bool bWriteAll = false)
        {
            bool bReturn = true;

            if (bWriteAll)
            {
                bReturn &= viewmode.WriteOpRegtoDevice(ref m_Msg, viewmode.ggpcsr_parameterlist);
            }
            else
            {
                AsyncObservableCollection<Parameter>    pmlisttmp = new AsyncObservableCollection<Parameter>();

                foreach(Parameter pmr in viewmode.ggpcsr_parameterlist)
                {
                    if(pmr.errorcode == LibErrorCode.IDS_ERR_SBSSFL_REQUIREWRITE)
                    {
                        pmlisttmp.Add(pmr);
                        pmr.errorcode = LibErrorCode.IDS_ERR_SUCCESSFUL;
                    }
                }
                bReturn &= viewmode.WriteOpRegtoDevice(ref m_Msg, pmlisttmp);
            }

            return bReturn;
        }

        public bool PollingOpReg()
        {
            bool bReturn = true;

            bReturn &= viewmode.ReadOpRegFromDevice(ref m_Msg, viewmode.ggpccs_parameterlist);  //it should be only volt/curr/temp/CAR in here list

            return bReturn;
        }

		public bool SetChipWakeup()
		{
			bool bReturn = true;
			AsyncObservableCollection<Parameter> pmlisttmp = new AsyncObservableCollection<Parameter>();

			foreach (Parameter pmr in viewmode.ggpcsr_parameterlist)
			{
				if (((pmr.guid & 0x0000FF00) >> 8) == 0x09)		//battery CtrlStatus register
				{
					foreach (KeyValuePair<string, Reg> tmpreg in pmr.reglist)
					{
						if (tmpreg.Key.Equals("Low"))
						{
							if (tmpreg.Value.startbit == 6)
							{
								pmlisttmp.Add(pmr);
								pmr.phydata = 0;
								break;
							}
							else if (tmpreg.Value.startbit == 7)
							{
								pmlisttmp.Add(pmr);
								pmr.phydata = 0;
								break;
							}
						}
					}
				}
				/*
				else if (((pmr.guid & 0x0000FF00) >> 8) == 0x80)		//charger charging voltage
				{
					foreach (KeyValuePair<string, Reg> tmpreg in pmr.reglist)
					{
						if (tmpreg.Key.Equals("Low"))
						{
							if (tmpreg.Value.startbit == 0)
							{
								pmlisttmp.Add(pmr);
								pmr.phydata = 10;		//0x0a hex => 4250mV
								break;
							}
						}
					}
				}
				else if (((pmr.guid & 0x0000FF00) >> 8) == 0x85)		//Vsys minimum voltage
				{
					foreach (KeyValuePair<string, Reg> tmpreg in pmr.reglist)
					{
						if (tmpreg.Key.Equals("Low"))
						{
							if (tmpreg.Value.startbit == 0)
							{
								pmlisttmp.Add(pmr);
								pmr.phydata = 4;		//4 index of dropbox, 0x0D hex => 2600mV
								break;
							}
						}
					}
				}
				else if (((pmr.guid & 0x0000FF00) >> 8) == 0x90)		//charger charging current
				{
					foreach (KeyValuePair<string, Reg> tmpreg in pmr.reglist)
					{
						if (tmpreg.Key.Equals("Low"))
						{
							if (tmpreg.Value.startbit == 0)
							{
								pmlisttmp.Add(pmr);
								pmr.phydata = 4;		//4th index of dropbox, 0x0c hex => 1200mA
								break;
							}
						}
					}
				}
				else if (((pmr.guid & 0x0000FF00) >> 8) == 0x92)		//EoC
				{
					foreach (KeyValuePair<string, Reg> tmpreg in pmr.reglist)
					{
						if (tmpreg.Key.Equals("Low"))
						{
							if (tmpreg.Value.startbit == 0)
							{
								pmlisttmp.Add(pmr);
								pmr.phydata = 5;		//5th index of dropbox, 0x0a hex => 100mA
								break;
							}
						}
					}
				}
				else if (((pmr.guid & 0x0000FF00) >> 8) == 0x93)		//VBus input current limit
				{
					foreach (KeyValuePair<string, Reg> tmpreg in pmr.reglist)
					{
						if (tmpreg.Key.Equals("Low"))
						{
							if (tmpreg.Value.startbit == 0)
							{
								pmlisttmp.Add(pmr);
								pmr.phydata = 4;		//4th index of dropbox, 0x0e hex => 1400mA
								break;
							}
						}
					}
				}
				else if (((pmr.guid & 0x0000FF00) >> 8) == 0xB0)		//Rthm and KI2C
				{
					foreach (KeyValuePair<string, Reg> tmpreg in pmr.reglist)
					{
						if (tmpreg.Key.Equals("Low"))
						{
							if (tmpreg.Value.startbit == 0)
							{
								pmlisttmp.Add(pmr);
								pmr.phydata = 1;		//0x01 hex => 10kOhm
								break;
							}
							else if (tmpreg.Value.startbit == 3)
							{
								pmlisttmp.Add(pmr);
								pmr.phydata = 1;		//0x01 hex => 0.9
								break;
							}
						}
					}
				}
				 * */
				//if(pmlisttmp.Count > 1)	//after found, break for loop
				//{
					//break;
				//}
			}

			bReturn &= viewmode.WriteOpRegtoDevice(ref m_Msg, pmlisttmp);
			Thread.Sleep(2500);

			return bReturn;
		}
        //(E150717)

        public void WriteOneBtn_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.Button wbtn = (System.Windows.Controls.Button)sender;

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

		public bool LoadGasGauge()
		{
			string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "O2Micro.Cobra.GasGauge.dll");
			var varDllLocat = Assembly.LoadFile(path);
			string strGasg = String.Empty;

            XmlNodeList xmlNlst = parent.GetUINodeList(sflname);
			foreach (XmlNode xnode in xmlNlst)
			{
				if (xnode.Name.ToLower().IndexOf("gasgauge") != -1)
				{
					strGasg = xnode.Attributes["Name"].Value.ToString();
					break;
				}
			}

			if (String.IsNullOrEmpty(strGasg))
				return false;
			m_GasGauge = null;
			foreach (Type t in varDllLocat.GetExportedTypes())
			{
				if (t.Name.IndexOf(strGasg) != -1)
				{
					m_GasGauge = (GasGaugeInterface)Activator.CreateInstance(t);
					break;
				}
			}

			if (m_GasGauge == null)
				return false;
			else
			{
				SetGGSettingFromXML();      //(A151002)Francis, to set up ProjectSetting
				return true;
			}
		}

        private bool GetDeviceAllInformation()
        {
            bool bReturn = false;

            m_Msg.task = TM.TM_SPEICAL_GETDEVICEINFOR;
            parent.AccessDevice(ref m_Msg);
            while(m_Msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            System.Windows.Forms.Application.DoEvents();
            if (m_Msg.errorcode == LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                m_Msg.task = TM.TM_SPEICAL_GETREGISTEINFOR;
                parent.AccessDevice(ref m_Msg);
                while (m_Msg.bgworker.IsBusy)
                    System.Windows.Forms.Application.DoEvents();
                System.Windows.Forms.Application.DoEvents();
                if (m_Msg.errorcode == LibErrorCode.IDS_ERR_SUCCESSFUL)
                {
                    bReturn = true;
                }
            }
            return bReturn;
        }

        private void SetGGSettingFromXML()
        {
            bool bChgEndCurrInXML = false;

            //just for case
            if (m_GasGauge == null)
                return;
            if (m_GasGauge.GetProjectFile() == null)
                return;

            foreach (SFLModel mdltmp in viewmode.sfl_parameterlist)
            {
                switch(mdltmp.format)
                {
                    case (UInt16)SettingFlag.PDC:       //0x0201: Design Capacity
                        {
                            m_GasGauge.GetProjectFile().dbDesignCp = (float)mdltmp.parent.phydata;
                            break;
                        }
                    case (UInt16)SettingFlag.PCHGCV:    //0x0205: Charge Constant Voltage
                        {
                            m_GasGauge.GetProjectFile().dbChgCVVolt = (float)mdltmp.parent.phydata;
                            break;
                        }
                    case (UInt16)SettingFlag.PCHGCUR:   //0x0206: Charge End Current
                        {
                            m_GasGauge.GetProjectFile().dbChgEndCurr = (float)mdltmp.parent.phydata;
                            bChgEndCurrInXML = true;
                            break;
                        }
                    case (UInt16)SettingFlag.PDSGV:     //0x0207: Discharge End Voltage
                        {
                            m_GasGauge.GetProjectFile().dbDsgEndVolt = (float)mdltmp.parent.phydata;
                            break;
                        }
                    case (UInt16)SettingFlag.PRBAT:     //0x0209: RBat
                        {
                            m_GasGauge.GetProjectFile().dbRbat = (float)mdltmp.parent.phydata;
                            break;
                        }
                    case (UInt16)SettingFlag.PRCON:     //0x020a: RCon
                        {
                            m_GasGauge.GetProjectFile().dbRcon = (float)mdltmp.parent.phydata;
                            break;
                        }
                    default:
                        {
                            break;
                        }
                }   //switch-case
            }   //foreach

            //ChargeEndCurrent could come from DeviceDescriptor.xml or come from charger chip's setting
            if(!bChgEndCurrInXML)   //if no found in xml
            {
                m_GasGauge.GetProjectFile().dbChgEndCurr = m_Msg.sm.misc[0];    //get it from misc[0], this must be set by DEM=>GetRegisterInfo() sub function
            }
        }
    }
}
