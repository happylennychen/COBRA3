using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.IO;
using System.Threading;
using Cobra.Common;
using Cobra.EM;

namespace Cobra.GasGauge
{
	public class YL8316GasGauge : GasGaugeInterface
	{
		#region static variable

		//use in wait_ocv_flag_fun()
		private static byte waitocvflag_times = 0;
		//use in bmu_wait_ready()
		private static byte waitready_times = 0;
		private static byte waitready_retry_times = 0;
        private static byte power_on_retry_times = 1;
		private static byte waitready_calculate_times = 0;
		private static UInt32 charge_tick = 0;
		private static long previous_loop_timex = 0;
		private static int car_error = 0;
		private static byte check_board_offset_i = 0;
		private static byte polling_error_times = 0;
		private static UInt32 wakeup_charge_tick = 0;
        private static bool CHARGE_STRATEGY_CHGTABLE = false;
        private static bool CHARGE_STRATEGY_OCV = true;
        private static Int32 gettimeinterval_last_interval = 4;
        private static float chargeprocess_mCapacity = 0F;
        private static Int32 chargeprocess_ratio_timer = 0;
        private static DateTime chgendfun_time_start;
        private static byte chgendfun_start_record_flag = 0;
        private static Int32 idleprocess_idle_timer = 0;
        //private static timex 

		#endregion

		#region private enum

		private enum YL8316Register : byte
		{
			RegTrimRsv = 0x03,
			RegFreeze = 0x04,
			RegLDOTrim = 0x08,
			RegStatus = 0x09,
			RegBattID = 0x0b,
			RegCellTemp = 0x0c,
			RegCellVolt = 0x0e,
			RegCellOCV = 0x10,
			RegCellCurr = 0x12,
			RegCellCAR = 0x14,
			RegBoardOffset = 0x18,
		}

		private enum CompareStatus :byte
		{
			CP_EQUAL = 0, 
			CP_OVER = 1, 
			CP_LESS = 2,
		};

		#endregion

		#region porting struct declaration

		struct config_data_t
		{
			//Int32 fRsense;					//= 20;			//Rsense value of chip, in mini ohm
			//Int32 temp_pull_up;			//230000;
			//Int32 temp_ref_voltage;	//1800;1.8v
			//Int32 dbCARLSB;				//= 5.0;		//LSB of CAR, comes from spec
			//Int32 dbCurrLSB;				//781 (3.90625*100);	//LSB of Current, comes from spec
			//Int32 fVoltLSB;					//250 (2.5*100);	//LSB of Voltage, comes from spec

			//Int32 design_capacity;		//= 7000;		//design capacity of the battery
			//Int32 charge_cv_voltage;	//= 4200;		//CV Voltage at fully charged
			//Int32 charge_end_current;	//= 100;		//the current threshold of End of Charged
			//Int32 discharge_end_voltage;	//= 3550;		//mV
			//Int32 board_offset;					//0; 				//mA, not more than caculate data
			//byte debug;                                          // enable or disable COBRA debug information

			//francis, only debug used in code, move it to parameter_data_t
		}

		struct parameter_data_t
		{
			public Int32 ocv_data_num;
			public Int32 cell_temp_num;
			//one_latitude_data_t  	*ocv;
			//one_latitude_data_t	 	*temperature;
			//config_data_t 		 	*config;

			//struct i2c_client 	 	*client;

			public byte charge_pursue_step;
			public byte discharge_pursue_step;
			public byte discharge_pursue_th;
			public byte wait_method;
			public bool debug;              //move from config_data
			//public Int32 board_offset;       //move from config_data
			public float fconnect_resist;		//ri, record resistor value between battery to chip
			public float finternal_resist;		//bi, record resistor value in battery
			//public Int32 charge_soc_time_ratio;	//= 220;			//the current threshold of End of Charged
			public Int32 suspend_current;		//= 100;			//mA, suspend mode board consumption current
            public Int32 charge_soc_time_factor;    //= 220
		}

		struct bmu_data_t
		{
			public Int32 Battery_ok;
			public float fRC;			//= 0;		//Remaining Capacity, indicates how many mAhr in battery
			public float fRSOC;			//50 = 50%;	//Relative State Of Charged, present percentage of battery capacity
			public float fVolt;			//= 0;						//Voltage of battery, in mV
			public float fCurr;			//= 0;		//Current of battery, in mA; plus value means charging, minus value means discharging
			public float fPrevCurr;		//= 0;						//last one current reading
			public float fOCVVolt;		//= 0;						//Open Circuit Voltage
			public float fCellTemp;		//= 0;						//Temperature of battery
			public float fRCPrev;
			public float fVoltPrev;
			public Int32 sCaMAH;			//= 0;						//adjusted residual capacity				
			public Int32 i2c_error_times;
			public bool bPoOCV;	// = 0; save PoOCV bit value
			public bool bSleepOCV;	// = 0; save SleepOCV bit value
			public byte chg_dsg_flag;

			public byte chg_on;			//CHGON status, active if 1, check CHG_SEL before use
			public float m_volt_1st;
			public float m_volt_2nd;
			public float m_volt_pre;
			public float m_volt_avg_long;//avg voltage of very long average(0.98 and 0.02 weighted)
		}

		struct gas_gauge_t
		{
			public Int32 overflow_data;
			public byte discharge_end;
			public byte charge_end;
			public byte charge_fcc_update;
			public byte discharge_fcc_update;

			public Int32 sCtMAH;    //becarfull this must int32_t
			public Int32 fcc_data;
			public Int32 discharge_sCtMAH;//becarfull this must int32_t
			public byte charge_wait_times;
			public byte discharge_wait_times;
			public byte charge_count;
			public byte discharge_count;
			public UInt32 bmu_tick;
			public UInt32 charge_tick;

			public int charge_table_num;
			public int charge_voltage_table_num;
			public int rc_x_num;
			public int rc_y_num;
			public int rc_z_num;

			public byte charge_strategy;
			public Int32 charge_sCaUAH;
			public Int32 charge_usoc;
			public float charge_ratio;  //this must be static 
			public byte charge_table_flag;
			public Int32 charge_end_current_th2;
			public UInt32 charge_max_ratio;

			public byte discharge_strategy;
			public Int32 discharge_sCaUAH;
			public float discharge_ratio;  //this must be static 
			public byte discharge_table_flag;
			public Int32 discharge_current_th;
			public UInt32 discharge_max_ratio;

			public Int32 dsg_end_voltage_hi;
			public Int32 dsg_end_voltage_th1;
			public Int32 dsg_end_voltage_th2;
			public byte dsg_count_2;

			public byte ocv_flag;

			public byte vbus_ok;

			public byte charge_full;
			public Int32 ri;
			public Int32 batt_ri;
			public Int32 line_impedance;
			public Int32 max_chg_reserve_percentage;
			public Int32 fix_chg_reserve_percentage;
			public byte fast_charge_step;
			public Int32 start_fast_charge_ratio;
			public byte charge_method_select;
			public Int32 max_charge_current_fix;

			//public long time_pre;
			//public long time_now;
			public DateTime dt_time_pre;
			public DateTime dt_time_now;
			public long charge_time_increment;

			public float volt_average_pre;
			public float volt_average_now;
			public float volt_average_pre_temp;
			public float volt_average_now_temp;
            public byte volt_avg_index;
		}

		struct volt_gg_t
		{
			public float m_fCurrent_step;
			public float m_fCurrent;
			public float m_fCoulombCount;
			public Int32 m_fMaxErrorSoc;
			public float m_fStateOfCharge;
			public float m_fRsoc;
			public float m_fResCap;
			public float m_fFCC;
			public Int32 m_iSuspendTime;		//UTC time in second when suspend involked
			public DateTime m_dt_suspend;
			public byte		m_cPreState;		//pre-suspend state, 1:charge,0:idle,-1:discharge
            //for volt_gg_state
            public Int32 m_volt_drop_th;		//voltage drop for statet change
            public Int32 m_volt_cv_th;		//voltage delta from CV
        }

		struct one_latitude_data_t 
		{
			public int iVoltage;
			public int iRSOC;
		}

		#endregion

		#region private members

		private bool bFranTestMode = false;
		private UInt32 uGGErrorcode = LibErrorCode.IDS_ERR_SUCCESSFUL;
		private AsyncObservableCollection<Parameter> PListGGPolling = null;
		private AsyncObservableCollection<Parameter> PListGGSetting = null;
		private AsyncObservableCollection<Parameter> PListSBSReg = null;
		private ParamContainer pmcAll = new ParamContainer();
		private GasGaugeProject myProject = null;

		//Android Driver variable
        private string VERSION = "2015.10.19/5.00.01";
		private string strSystemLogFolder = Path.Combine(FolderMap.m_currentproj_folder, "GasGauge");
		private string strSystemPrintkFolder = Path.Combine(FolderMap.m_currentproj_folder, "Printk");
		private string strprintkfile;
		private string BATT_TEST = string.Format("init_ok.dat");
		private string BATT_CAPACITY = string.Format("sCaMAH.dat");
		private string BATT_FCC = string.Format("fcc.dat");
		private string OCV_FLAG = string.Format("ocv_flag.dat");
		private string BATT_OFFSET = string.Format("offset.dat");
		private string CM_PATH = string.Format("cobraapi");
		private string BATT_SOC = string.Format("soc.dat");
		private string BATT_RSOC = string.Format("rsoc.dat");

		#region Physical Parameter definition
		private Parameter ParamPhyVoltage = null;
		private Parameter ParamPhyCurrent = null;
		private Parameter ParamPhyTemperature = null;
		private Parameter ParamPhyCAR = null;
		#endregion

        #region Setting Parameter definition
        //how to read/write whole register, instead of bit regiter. Parameter defined by bit register in xml
        //private Parameter ParamYL8316Status = null;
		private Parameter ParamYL8316Regx03Bit7 = null;
		private Parameter ParamYL8316Regx04Bit0 = null;
		private Parameter ParamYL8316Regx08Bit0 = null;
		private Parameter ParamYL8316CtrlBI = null;
		private Parameter ParamYL8316Ctrlchgsel = null;
		private Parameter ParamYL8316Ctrlvme = null;
		private Parameter ParamYL8316CtrlChgActive = null;
		private Parameter ParamYL8316CtrlSlpOCVEn = null;
		private Parameter ParamYL8316CtrlSleepMode = null;
		private Parameter ParamYL8316CtrlSWReset = null;
		private Parameter ParamYL8316OCV = null;
		private Parameter ParamYL8316PoOCV = null;
		private Parameter ParamYL8316SleepOCV = null;
		private Parameter ParamYL8316BoardOffset = null;
		#endregion

		#region SBS Parameter definition
		private Parameter ParamSBS0x3C = null;
		private Parameter ParamSBS0x3D = null;
		private Parameter ParamSBS0x3E = null;
		private Parameter ParamSBS0x3F = null;
		private Parameter ParamSBS0x40 = null;
		private Parameter ParamSBS0x41 = null;
		private Parameter ParamSBS0x42 = null;
		private Parameter ParamSBS0x43 = null;
		private Parameter ParamSBS0x44 = null;
		private Parameter ParamSBS0x45 = null;
		private Parameter ParamSBS0x46 = null;
		private Parameter ParamSBS0x47 = null;
		private Parameter ParamSBS0x48 = null;
		private Parameter ParamSBS0x09 = null;
		private Parameter ParamSBS0x49 = null;
		private Parameter ParamSBS0x4A = null;
		private Parameter ParamSBS0x4B = null;
		private Parameter ParamSBS0x4C = null;
		private Parameter ParamSBS0x0A = null;
		private Parameter ParamSBS0x0B = null;
		private Parameter ParamSBS0x14 = null;
		private Parameter ParamSBS0x15 = null;
		private Parameter ParamSBS0x0D = null;
		private Parameter ParamSBS0x0E = null;
		private Parameter ParamSBS0x0F = null;
		private Parameter ParamSBS0x10 = null;
		private Parameter ParamSBS0x11 = null;
		private Parameter ParamSBS0x12 = null;
		private Parameter ParamSBS0x13 = null;
		private Parameter ParamSBS0x17 = null;
		private Parameter ParamSBS0x4D = null;	//not in Patrol, special for AgeFactor
		private Parameter ParamSBS0x16 = null;
		private Parameter ParamSBS0x1F = null;
		private Parameter ParamSBS0x18 = null;
		private Parameter ParamSBS0x19 = null;
		private Parameter ParamSBS0x03 = null;
		private Parameter ParamSBS0x1A = null;
		private Parameter ParamSBS0x1B = null;
		private Parameter ParamSBS0x1C = null;
		private Parameter ParamSBS0x20 = null;
		private Parameter ParamSBS0x21 = null;
		private Parameter ParamSBS0x22 = null;
		private Parameter ParamSBS0x23 = null;
		//for debug used only
		private Parameter ParamSBSxE0BgState = null;
		private Parameter ParamSBSxE1BGStatus = null;
		private Parameter ParamSBSxF0CarDiff = null;
		private Parameter ParamSBSxF1CTMah = null;
		private Parameter ParamSBSxF2CAMah = null;
		private Parameter ParamSBSxF3Prev = null;
		private Parameter ParamSBSxF4CRMah = null;
		private Parameter ParamSBSxF5SelfMah = null;
		private Parameter ParamSBSxF6FCC = null;
		#endregion

		#region oz_gg_agecomp.h porting

		private enum BgAgeState : int
		{
			BG_DISCHARGE = -1,
			BG_QUIET = 0,
			BG_CHARGE = 1,
		}

		private struct point_data
		{
			public bool pt_set;
			public int pt_volt;
			public UInt16 pt_time;
			public double pt_curr;
			public int pt_abscap;
			public int pt_soc;
		}

		private class age_compansate_t
		{
			/*****************************************************
			 *	Constant Data, from Lookup Tables
			******************************************************/
			public double res_line;		//Rsense + Rconnector + Rfet + Rpcb
			public double rch25_new;		//Rch for new cell
			public int delta_t;		//dT for EOD temperature calc
			public double r_thermal;		//thermal resistance factor
			public double k_factor;		//K factor for LPF (1/4 or 1/8)
			public int soc_ri_start; 	//soc range to calc Ri (low)
			public int soc_ri_end;		//soc range to calc Ri (high)
			public int temp_ri_high;	//temperature tange to calc Ri (high)
			public int temp_ri_low;	//temperature tange to calc Ri (low)
			public int charge_ri_timeout;	//min charge time to calc Ri 
			public double current_quiet_min;	//min current for FullAbsCap update
			public int quiet_timeout;		//min quiet time to calc FullAbsCap
			public int volt_fabs_pt4;		//voltage range to calc FullAbsCap (high)
			public int volt_fabs_pt3;		//voltage range to calc FullAbsCap (mid-high)
			public int volt_fabs_pt2;		//voltage range to calc FullAbsCap (mid-low)
			public int volt_fabs_pt1;		//voltage range to calc FullAbsCap (low)
			public int volt_rc_max;		//RC table max voltage (corrected)

			/*****************************************************
			 *	Dynamic Data, calculated during age compansate 
			******************************************************/
			public float abs_cap_now;		//estimated remaining capacity
			public float age_factor_last;
			public float age_factor_now;
			public int full_abs_cap_last;
			public int full_abs_cap_now;
			public UInt32 chg_ri_tick;
			public UInt32 ri_count;
			public float ri_now;
			public float ri_25;
			public float rch25_now;
			public bool quiet_state;
			public UInt32 quiet_tick;
			public BgAgeState bg_state_prev;
			public BgAgeState bg_state_now;
			public point_data fabs_high;
			public point_data fabs_low;

			public age_compansate_t()		//constructor
			{
			}
		}

		private age_compansate_t age_comp = new age_compansate_t();

		#endregion

		#region porting declaration from Android Driver

		private int DISCH_CURRENT_TH = -10;
		private int SHUTDOWN_HI = 50;
		private int SHUTDOWN_TH1 = 100;
		private int SHUTDOWN_TH2 = 300;
		private int FCC_UPPER_LIMIT = 100;
		private int FCC_LOWER_LIMIT = 70;   //for lianxiang
		private byte CHARGE_STATE = 1;
		private byte DISCHARGE_STATE = 0xFF;
		private byte IDLE_STATE = 0;
		private int MAX_EOD_TIME = (60 * 9);	//H7 add timer full
		private int MAX_SUSPEND_CURRENT = (-60);		//H7 add wakup CAR range check, max suspend current
		private int MAX_SUSPEND_CONSUME = (-45 * 10 / 36);	//H7 add wakup CAR range check, max suspend consumption(1000*mah)
		private int MAX_SUSPEND_CHARGE = (1800 * 10 / 36);//H7 add wakup CAR range check, max suspend charge(1000*mah)
		private int CHARGE_VOLT_NUM = 20;
		//private int VOLTGG_CMP_LOOP = 30;
		//private int VOLTGG_MAX_ERR = 300;
        private int	VOLT_DROP_TH1 =	50;	//voltage drop for state change
        private int	VOLT_D_AVG_CV1 = 50;	//voltage delta from CV
        private int VOLT_AVG_RNG_TH1 = 30;	//voltage average allowed delta

        private int	VOLTGG_CURR_STEP = 20;		//change current in 20mA step
        private int	VOLTGG_MAX_ERR = 300;		//compare SOC within 3% * 10000
        private int	VOLTGG_CMP_LOOP = 30;		//times before the increment of soc error
        private int	VOLTGG_ERR_STEP = 50;		//soc comparison error increment step 0.5% * 10000
        private int	VOLTGG_PWR_CURRENT = 700;		//norminal power on current
        private int	VOLTGG_CV_SOC_TIME = 90;		//cv stage soc update timer
        private int	VOLTGG_MAX_IDLE = 200;		//max idle current
		private int VOLTGG_CV_SOC = 90;//85;		//SOC near CV
        private int VOLTGG_IDLE_TIME = 7200;
        private int MAX_TIMETOFULL = 20;

        private bmu_data_t batt_info;
		private gas_gauge_t gas_gauge;
		private parameter_data_t parameter_customer;
		private volt_gg_t volt_gg;
		private List<one_latitude_data_t	> charge_volt_data = new List<one_latitude_data_t>();

		private byte bmu_init_ok = 0;
		private byte YL8316_pec_check = 0;
		private byte YL8316_cell_num = 1;
		private Int32 res_divider_ratio = 1000;
        private byte charger_finish = 0;
        private byte charge_end_flag = 0;
        private byte charge_times = 0;
        private byte chgon_use = 1;
		private Int32 pwron_current = 700;//VOLTGG_PWR_CURRENT;

		private byte wait_dc_charger = 0;
		private byte wait_voltage_end = 0;

		private byte wait_ocv_flag = 0;
		private byte wait_ocv_times = 0;//= 2; by francis, no need to wait ocv time
		//byte  adapter_in = 0;
		private byte adapter_in_pre = 2;

		private Int32 o2_temp_delta;

		private UInt32 o2_suspend_jiffies;
		private UInt32 o2_resume_jiffies;
		private byte YL8316_in_suspend = 0;

		private byte start_chg_count_flag = 0;
		private byte battery_ri = 40;
        private byte volt_avg_num = 4;
        private byte ext_thermal_read = 0;
        //private byte chgon_use = 0;

		private byte discharge_end = 0;
		private byte charge_end = 0;
		private byte charge_fcc_update = 0;
		private byte discharge_fcc_update = 0;

		//private parameter_data_t parameter;
		private byte power_on_flag = 0;
		private byte write_offset = 0;
		private byte check_offset_flag = 0;

		private byte bmu_sleep_flag = 0;
		//private float fRSOC_PRE;
		private float vRSOC_PRE;
		private float vSOC_PRE;

		private byte discharge_end_flag = 0;
		private byte sleep_ocv_flag = 0;
		private byte sleep_charge_flag = 0;
		//struct timex  time_x;
		//private struct rtc_time rtc_time;

		private float calculate_mah = 0F;
		private float calculate_soc = 0F;

		private byte adapter_status;

		private byte volt_num = 4;

		#endregion

		#endregion

		#region public methods

		public YL8316GasGauge()
		{
			myProject = new GasGaugeProject();
		}

		public bool InitializeGG(object deviceP, TASKMessage taskP,
												AsyncObservableCollection<Parameter> PPolling,
												AsyncObservableCollection<Parameter> PSetting,
												AsyncObservableCollection<Parameter> PSBSreg,
												List<string> projtable = null)
		{
			bool bInit = true;

			#region setup project file string
			try
			{
				PListGGPolling = PPolling;
				PListGGSetting = PSetting;
				PListSBSReg = PSBSreg;
				if (PListGGPolling == null)
				{
					uGGErrorcode = LibErrorCode.IDS_ERR_EGDLL_GGPOLLING_NULL;
					return false;
				}
				if (PListGGSetting == null)
				{
					uGGErrorcode = LibErrorCode.IDS_ERR_EGDLL_GGSETTING_NULL;
					return false;
				}
				if (PListSBSReg == null)
				{
					uGGErrorcode = LibErrorCode.IDS_ERR_EGDLL_SBSREG_NULL;
					return false;
				}

				if (projtable == null)
				{
					projtable = new List<string>();
					//string	tmpProjectFile = new string("C:\\Documents and Settings\\francis.hsieh\\My Documents\\COBRA Documents\\KillerWhaleFY\\OZBattery\\Project\\DefaultProject.xml".ToCharArray());
					string tmpProjectFile = new string("Project_Lenovo_BL216.xml.xml".ToCharArray());
					projtable.Add(tmpProjectFile);
					//string tmpDLLFile = new string("D:\\COBRA\\Project - Cobra\\SourceCode\\SW_Cobra\\output\\Cobra.GasGauge.dll".ToCharArray());
					//projtable.Add(tmpDLLFile);
					//string tmpOCVbyTSOC = new string("C:\\Documents and Settings\\francis.hsieh\\My Documents\\COBRA Documents\\KillerWhaleFY\\OZBattery\\Tables\\OCVbyTSOC_SANYO_UR18650F-SCUD_2200mAH_5_V001_03292006.txt".ToCharArray());
					string tmpOCVbyTSOC = new string("OCVbyTSOC_SANYO_UR18650F-SCUD_2200mAH_5_V001_03292006.txt".ToCharArray());
					projtable.Add(tmpOCVbyTSOC);
					//string tmpTSOCbyOCV = new string("C:\\Documents and Settings\\francis.hsieh\\My Documents\\COBRA Documents\\KillerWhaleFY\\OZBattery\\Tables\\TSOCbyOCV_SANYO_UR18650F-SCUD_2200mAH_5_V001_03292006.txt".ToCharArray());
					string tmpTSOCbyOCV = new string("TSOCbyOCV_SANYO_UR18650F-SCUD_2200mAH_5_V001_03292006.txt".ToCharArray());
					projtable.Add(tmpTSOCbyOCV);
					//string tmpRCTable = new string("C:\\Documents and Settings\\francis.hsieh\\My Documents\\COBRA Documents\\KillerWhaleFY\\OZBattery\\Tables\\RC_SANYO_UR18650F-SCUD_2200mAH_V003_03282006.txt".ToCharArray());
					string tmpRCTable = new string("RC_SANYO_UR18650F-SCUD_2200mAH_V003_03282006.txt".ToCharArray());
					projtable.Add(tmpRCTable);
					//string tmpThermalTable = new string("C:\\Documents and Settings\\francis.hsieh\\My Documents\\COBRA Documents\\KillerWhaleFY\\OZBattery\\Tables\\Thermal_Semitec_103JT025_V002_01142011.txt".ToCharArray());
					string tmpThermalTable = new string("Thermal_Semitec_103JT025_V002_01142011.txt".ToCharArray());
					projtable.Add(tmpThermalTable);
					//string tmpSelfDsgTable = new string("C:\\Documents and Settings\\francis.hsieh\\My Documents\\COBRA Documents\\KillerWhaleFY\\OZBattery\\Tables\\Selfdis_O2_Generic_V002_09282005.txt".ToCharArray());
					string tmpSelfDsgTable = new string("Selfdis_O2_Generic_V002_09282005.txt".ToCharArray());
					projtable.Add(tmpSelfDsgTable);
					//string tmpRITable = new string("C:\\Documents and Settings\\francis.hsieh\\My Documents\\COBRA Documents\\KillerWhaleFY\\OZBattery\\Tables\\Reoc(T)_BAK_18650_2200mAh_12182013 sqr B6.txt".ToCharArray());
					//string tmpRITable = new string("RI(T)_LENOVO_BL216_3050mAh_70-40%_003_04222014.txt".ToCharArray());
					string tmpRITable = new string("Reoc(T)_BAK_18650_2200mAh_12182013 sqr B6.txt".ToCharArray());
					projtable.Add(tmpRITable);
					//string tmpChgTable = new string("Charge_SANYO_UR18650F_2200mAhr_4350mV_3300mV_V006_20150202.txt".ToCharArray());
					//projtable.Add(tmpRITable);
				}
				//sRam[(int)prShortIndex.iDBGCode] = 0x71;
                //myProject = new GasGaugeProject(projtable);
				if (myProject == null)
				{
					//uGGErrorcode = LibErrorCode.IDS_ERR_EGDLL_GGPOLLING_NULL;
					//return false;
					myProject = new GasGaugeProject();	//shold not go here, just for case, but it will have importatn parameter error also
				}
				//else
				{
					if (myProject.dbChgEndCurr == 0)
					{
						uGGErrorcode = LibErrorCode.IDS_ERR_EGDLL_GGPARAMETER;
						return false;
					}
					if (myProject.dbDesignCp == 0)
					{
						uGGErrorcode = LibErrorCode.IDS_ERR_EGDLL_GGPARAMETER;
						return false;
					}
					if (myProject.dbDsgEndVolt == 0)
					{
						uGGErrorcode = LibErrorCode.IDS_ERR_EGDLL_GGPARAMETER;
						return false;
					}
				}
				myProject.SetTableList(projtable);
				//sRam[(int)prShortIndex.iDBGCode] = 0x72;
				bInit = myProject.InitializeProject(ref uGGErrorcode, false, true);	//false to don't check charge table, true to ignore project.xml
				if (!bInit)
				{
					MessageBox.Show(" Error!! YL8316 Gas Gauge needs charge table to calculate.");
					return bInit;
				}
				//check System log file path
				if (!Directory.Exists(strSystemLogFolder))
				{
					Directory.CreateDirectory(strSystemLogFolder);
				}
				if (!Directory.Exists(strSystemPrintkFolder))
				{
					Directory.CreateDirectory(strSystemPrintkFolder);
				}
				strprintkfile = "printk" + DateTime.Now.GetDateTimeFormats('s')[0].ToString().Replace(@":", @"-") + ".log";
				strprintkfile = Path.Combine(strSystemPrintkFolder, strprintkfile);
			}
			catch (Exception)
			{
				return bInit;
			}
			#endregion

			#region Android Driver initialization
			//sRam[(int)prShortIndex.iDBGCode] = 0x80;
			if ((PListGGPolling != null) && (PListGGSetting != null) && (PListSBSReg != null))
			{
				//sRam[(int)prShortIndex.iDBGCode] = 0x82;
				//uGGErrorcode = DBGCodeparamter.errorcode;
				//(A150723)Francis, initialize static variable when beginning
				waitocvflag_times = 0;
				waitready_times = 0;
				waitready_retry_times = 0;
                power_on_retry_times = 1;
				waitready_calculate_times = 0;
				charge_tick = 0;
				previous_loop_timex = 0;
				car_error = 0;
				check_board_offset_i = 0;
				polling_error_times = 0;
				//(E150723)
				bInit &= gdm_init();
				//using Android code to initialize
				SetupChargeVoltData();				//(A150903)Francis, set up charge_volt_data[20] content
				bmu_init_parameter();
				bmu_init_chip();
				//bmu_polling_loop();
				//sbd_udpate_sbs();		//copy result to SBS parameter
                CalculateGasGauge();
			}
			else
			{
				bInit = false;
			}
			#endregion

			//MessageBox.Show("GG Init");

			return bInit;
		}

		public bool UnloadGG()
		{
			/*
			//tmrFirmware.Stop();

			//MessageBox.Show("GG Unload");
			 * */
			return true;
		}

		public UInt32 GetStatus()
		{
			return uGGErrorcode;
		}

		public bool CalculateGasGauge()
		{
            system_charge_discharge_status();   //empty function
			bmu_polling_loop();

            if (adapter_status != 0) //YCHARGER_BATTERY
            {
                charge_end_fun(vRSOC_PRE); 
            }
            else
            {
                discharge_end_fun();
            }

            //(C151012)Francis, wake_lock is Kernel related, no need
            //if (adapter_status == YCHARGER_AC)
            //{
                //if (!wake_lock_active(&yl8316_wake_lock))
                //{
                    //wake_lock(&yl8316_wake_lock);
                //}
            //}
            //else
            //{
                //if (wake_lock_active(&yl8316_wake_lock))
                //{
                    //wake_unlock(&yl8316_wake_lock);
                //}
            //}

            //(C151012)Francis, power_supply_changed is Kernel related, no need
            //if (!data->is_suspended)
            //{
                //power_supply_changed(&data->bat);
            //}

            //(C151012)Francis, if bmu_init_ok no set, change polling time to 2 second
            //But, GG DLL cannot do it, ignore this
            //if (!bmu_init_ok)
            //{
                //schedule_delayed_work(&data->work, msecs_to_jiffies(2 * 1000));
            //}
            //else
            //{
                //schedule_delayed_work(&data->work, data->interval);
            //}
            
            if ((batt_info.fRSOC <= 0) && (batt_info.fCurr > 10))
				batt_info.fRSOC = 1;

			//update to SBS parameter, to report physical value on screen
			sbd_udpate_sbs();
			return true;
		}

		public GasGaugeProject GetProjectFile()
		{
			return myProject;
		}

		#endregion

		#region private methods, old EagleGasGauge, and porting

		private bool gdm_init()
		{
			bool bGDM = true;
			UInt16 u16Addr = 0x00;

			//TBD: how to use private section to get what i want in ParameterList, instead of using register address to find them out?
			#region assign Physical Parameter pointer
			foreach (Parameter pmGGP in PListGGPolling)
			{
				u16Addr = (UInt16)((pmGGP.guid & 0x0000FF00) >> 8);
				switch (u16Addr)
				{
					case (UInt16)YL8316Register.RegCellVolt:
						{
							ParamPhyVoltage = pmGGP;
							break;
						}
					case (UInt16)YL8316Register.RegCellCurr:
						{
							ParamPhyCurrent = pmGGP;
							break;
						}
					case (UInt16)YL8316Register.RegCellTemp:
						{
							ParamPhyTemperature = pmGGP;
							break;
						}
					case (UInt16)YL8316Register.RegCellCAR:
						{
							ParamPhyCAR = pmGGP;
							break;
						}
					default:
						{
							break;
						}
				}
			}
			if (ParamPhyVoltage == null)
			{
				uGGErrorcode = LibErrorCode.IDS_ERR_SBSSFL_GGDRV_NOVOLTAGE;
				return false;
			}
			//if (ParamPhyCurrent == null)
			//{
				//uGGErrorcode = LibErrorCode.IDS_ERR_SBSSFL_GGDRV_NOCURRENT;
				//return false;
			//}
			if (ParamPhyTemperature == null)
			{
				uGGErrorcode = LibErrorCode.IDS_ERR_SBSSFL_GGDRV_NOTEMPERATURE;
				return false;
			}
			//if (ParamPhyCAR == null)
			//{
				//uGGErrorcode = LibErrorCode.IDS_ERR_SBSSFL_GGDRV_NOCAR;
				//return false;
			//}
			#endregion

			#region assign Setting Parameter pointer
			foreach (Parameter pmGGS in PListGGSetting)
			{
				u16Addr = (UInt16)((pmGGS.guid & 0x0000FF00) >> 8);
				switch (u16Addr)
				{
					case (UInt16)YL8316Register.RegTrimRsv:
						{
							foreach (KeyValuePair<string, Reg> tmpreg in pmGGS.reglist)
							{
								if (tmpreg.Key.Equals("Low"))
								{
									if (tmpreg.Value.startbit == 7)
									{
										ParamYL8316Regx03Bit7 = pmGGS;
									}
								}
							}
							break;
						}
					case (UInt16)YL8316Register.RegFreeze:
						{
							foreach (KeyValuePair<string, Reg> tmpreg in pmGGS.reglist)
							{
								if (tmpreg.Key.Equals("Low"))
								{
									if (tmpreg.Value.startbit == 0)
									{
										ParamYL8316Regx04Bit0 = pmGGS;
									}
								}
							}
							break;
						}
					case (UInt16)YL8316Register.RegLDOTrim:
						{
							foreach (KeyValuePair<string, Reg> tmpreg in pmGGS.reglist)
							{
								if (tmpreg.Key.Equals("Low"))
								{
									if (tmpreg.Value.startbit == 0)
									{
										ParamYL8316Regx08Bit0 = pmGGS;
									}
								}
							}
							break;
						}
					case (UInt16)YL8316Register.RegStatus:
						{
							//ParamYL8316Status = pmGGS;
							foreach (KeyValuePair<string, Reg> tmpreg in pmGGS.reglist)
							{
								if (tmpreg.Key.Equals("Low"))
								{
									if (tmpreg.Value.startbit == 0)
									{
										ParamYL8316CtrlBI = pmGGS;
									}
									else if (tmpreg.Value.startbit == 1)
									{
										ParamYL8316Ctrlchgsel = pmGGS;
									}
									else if (tmpreg.Value.startbit == 2)
									{
										ParamYL8316Ctrlvme = pmGGS;
									}
									else if (tmpreg.Value.startbit == 4)
									{
										ParamYL8316CtrlChgActive = pmGGS;
									}
									else if (tmpreg.Value.startbit == 5)
									{
										ParamYL8316CtrlSlpOCVEn = pmGGS;
									}
									else if (tmpreg.Value.startbit == 6)
									{
										ParamYL8316CtrlSleepMode = pmGGS;
									}
									else if (tmpreg.Value.startbit == 7)
									{
										ParamYL8316CtrlSWReset = pmGGS;
									}
								}
							}
							break;
						}
					case (UInt16)YL8316Register.RegCellOCV:
						{
							foreach (KeyValuePair<string, Reg> tmpreg in pmGGS.reglist)
							{
								if (tmpreg.Key.Equals("Low"))
								{
									if (tmpreg.Value.startbit == 0)
									{
										ParamYL8316PoOCV = pmGGS;
									}
									else if (tmpreg.Value.startbit == 1)
									{
										ParamYL8316SleepOCV = pmGGS;
									}
									else if (tmpreg.Value.startbit == 4)
									{
										ParamYL8316OCV = pmGGS;
									}
								}
							}
							break;
						}
					case (UInt16)YL8316Register.RegBoardOffset:
						{
							ParamYL8316BoardOffset = pmGGS;
							break;
						}
					default:
						{
							break;
						}
				}
			}
			#endregion

			#region assign SBS parameter pointer
			//foreach (Parameter pmGDM in pmcSBSReg.parameterlist)
			foreach (Parameter pmGDM in PListSBSReg)
			{
				u16Addr = (UInt16)((pmGDM.guid & 0x0000FF00) >> 8);
				switch (u16Addr)
				{
					case (UInt16)EGSBS.SBSVoltCell01:
						{
							ParamSBS0x3C = pmGDM;
							break;
						}
					case (UInt16)EGSBS.SBSVoltCell02:
						{
							ParamSBS0x3D = pmGDM;
							break;
						}
					case (UInt16)EGSBS.SBSVoltCell03:
						{
							ParamSBS0x3E = pmGDM;
							break;
						}
					case (UInt16)EGSBS.SBSTotalVoltage:
						{
							ParamSBS0x09 = pmGDM;
							break;
						}
					case (UInt16)EGSBS.SBSExtTemp01:
						{
							ParamSBS0x4A = pmGDM;
							break;
						}
					case (UInt16)EGSBS.SBSCurrent:
						{
							ParamSBS0x0A = pmGDM;
							break;
						}
					case (UInt16)EGSBS.SBSChargingCurrent:
						{
							ParamSBS0x14 = pmGDM;
							break;
						}
					case (UInt16)EGSBS.SBSChargingVoltage:
						{
							ParamSBS0x15 = pmGDM;
							break;
						}
					case (UInt16)EGSBS.SBSRSOC:
						{
							ParamSBS0x0D = pmGDM;
							break;
						}
					case (UInt16)EGSBS.SBSRC:
						{
							ParamSBS0x0F = pmGDM;
							break;
						}
					case (UInt16)EGSBS.SBSFCC:
						{
							ParamSBS0x10 = pmGDM;
							break;
						}
					case (UInt16)EGSBS.SBSRunTimeToEmpty:
						{
							ParamSBS0x11 = pmGDM;
							break;
						}
					case (UInt16)EGSBS.SBSAvgTimeToEmpty:
						{
							ParamSBS0x12 = pmGDM;
							break;
						}
					case (UInt16)EGSBS.SBSAvgTimeToFull:
						{
							ParamSBS0x13 = pmGDM;
							break;
						}
					case (UInt16)EGSBS.SBSCycleCount:
						{
							ParamSBS0x17 = pmGDM;
							break;
						}
					case (UInt16)EGSBS.SBSAgeFactor:
						{
							ParamSBS0x4D = pmGDM;
							break;
						}
					case (UInt16)EGSBS.SBSBatteryStatus:
						{
							ParamSBS0x16 = pmGDM;
							break;
						}
					case (UInt16)EGSBS.SBSDesignCapacit:
						{
							ParamSBS0x18 = pmGDM;
							break;
						}
					case (UInt16)EGSBS.SBSDesignVoltage:
						{
							ParamSBS0x19 = pmGDM;
							break;
						}
					case 0xe0:
						{
							ParamSBSxE0BgState = pmGDM;
							break;
						}
					case 0xe1:
						{
							ParamSBSxE1BGStatus = pmGDM;
							break;
						}
					case 0xf0:
						{
							ParamSBSxF0CarDiff = pmGDM;
							break;
						}
					case 0xf1:
						{
							ParamSBSxF1CTMah = pmGDM;
							break;
						}
					case 0xf2:
						{
							ParamSBSxF2CAMah = pmGDM;
							break;
						}
					case 0xf3:
						{
							ParamSBSxF3Prev = pmGDM;
							break;
						}
					case 0xf4:
						{
							ParamSBSxF4CRMah = pmGDM;
							break;
						}
					case 0xf5:
						{
							ParamSBSxF5SelfMah = pmGDM;
							break;
						}
					case 0xf6:
						{
							ParamSBSxF6FCC = pmGDM;
							break;
						}
					default:
						break;
				}
			}
			#endregion

			//reset GDM
			//for (int fff = 0; fff < fRam.Length; fff++)
			//{
			//fRam[fff] = 0F;
			//}
			//for (int sss = 0; sss < sRam.Length; sss++)
			//{
			//sRam[sss] = 0x0000;
			//}
			//initialize GDM value for GG calculation, for test
			//fRam[(int)prFloatIndex.iDesignCapacity] = myProject.dbDesignCp;
			//fRam[(int)prFloatIndex.iFCC] = myProject.dbDesignCp;
			//fRam[(int)prFloatIndex.iRsense] = myProject.dbRsense;
			//fRam[(int)prFloatIndex.iExtTempPullR] = myProject.dbPullupR;
			//fRam[(int)prFloatIndex.iExtTempPullV] = myProject.dbPullupV;
			//fRam[(int)prFloatIndex.iAgeFactor] = 1.0F;
			//fRam[(int)prFloatIndex.iDsgRatio] = 1.0F;
			//fRam[(int)prFloatIndex.iChgCurrTh] = 10F;
			//fRam[(int)prFloatIndex.iDsgCurrTh] = -10F;
			//fRam[(int)prFloatIndex.iFChgEndCurr] = myProject.dbChgEndCurr;
			//fRam[(int)prFloatIndex.iFChgTimeout] = 60;
			//fRam[(int)prFloatIndex.iFChgCV] = myProject.dbChgCVVolt;
			//fRam[(int)prFloatIndex.iDsgZero] = myProject.dbDsgEndVolt;
			//fRam[(int)prFloatIndex.iRCTableHighV] = myProject.GetRCTableHighVolt();
			//fRam[(int)prFloatIndex.iRCTableLowV] = myProject.GetRCTableLowVolt();
			//
			//sRam[(int)prShortIndex.iOneMinTmr] = 60;
			//sRam[(int)prShortIndex.iOneHrTmr] = 60;
			//sRam[(int)prShortIndex.iThermStayCnt] = 0;
			//sRam[(int)prShortIndex.iBGState] = (short)BGStateenum.BGSTATE_IDLE;
			//sRam[(int)prShortIndex.iBGPMode] = (short)BGPwrenum.FW_PMODE_FULL;

			//copy DBGcode  to high byte of SBSx16
			ParamSBS0x16.phydata = 0;
			//DBGCodeparamter.errorcode = 0x00;
			//u16Addr = (UInt16.Parse(sRam[(int)prShortIndex.iDBGCode].ToString()));
			//u16Addr <<= 8;
			//ParamSBS0x16.phydata += u16Addr;

			//SBSx18, x19 is static data, updating once should be enough
			//SBSx18, DesignCapacity
			if (ParamSBS0x18 != null)
			{
				ParamSBS0x18.phydata = myProject.dbDesignCp;//fRam[(int)prFloatIndex.iDesignCapacity];
			}
			//SBSx19, DesignVoltage
			if (ParamSBS0x19 != null)
			{
				ParamSBS0x19.phydata = 4200F;
			}

			return bGDM;
		}

		private void sbd_udpate_sbs()
		{
			//float fTempsbd = 50F;

			//SBSx3c, CellVoltage 01~
			if (ParamSBS0x3C != null)
			{
				//ParamSBS0x3C.phydata = fRam[(int)prFloatIndex.iVoltage];
				ParamSBS0x3C.phydata = batt_info.fVolt; ;
			}
			//SBSx49~x4C, Int/Ext Temp 01~
			if (ParamSBS0x4A != null)
			{
				//ParamSBS0x4A.phydata = fRam[(int)prFloatIndex.iTemperature];
				ParamSBS0x4A.phydata = batt_info.fCellTemp;
			}
			//SBSx0a	//Current
			if (ParamSBS0x0A != null)
			{
				//ParamSBS0x0A.phydata = fRam[(int)prFloatIndex.iCurrent];
				ParamSBS0x0A.phydata = batt_info.fCurr;
			}
			//SBSx0d, SBSx10	//RelativeStateOfCharge, FullChargedCapacity
			if (ParamSBS0x0D != null)
				ParamSBS0x0D.phydata = batt_info.fRSOC;
			//SBSx0f, RemainingCapacity
			if (ParamSBS0x0F != null)
			{
				//ParamSBS0x0F.phydata = fRam[(byte)prFloatIndex.iCAR];
				//ParamSBS0x0F.phydata = fRam[(int)prFloatIndex.iCAMah];
				ParamSBS0x0F.phydata = batt_info.sCaMAH;
			}
			//SBSx12, AverageTimeToEmpty
			if (ParamSBS0x12 != null)
			{
				short ustate = 0;//(short)sRam[(int)prShortIndex.iBGState];
				float femp = 65535;

				//if ((ustate & (short)BGStateenum.BGSTATE_MASK) == (short)BGStateenum.BGSTATE_DISCHARGE)
				//{
				//if (((short)ParamSBS0x16.phydata & (short)SBS16enum.SBS_BSTAT_FULLY_DISCHARGED) != 0)
				//{
				//femp = 0;
				//}
				//else
				//{
				//femp = sbd_time_to_empty(fRam[(int)prFloatIndex.iCurrent]);
				//}
				//}
				//ParamSBS0x12.phydata = femp;
				ParamSBS0x12.phydata = Convert.ToDouble(String.Format("{0:F2}", femp));
			}
			//SBSx13, AverageTimeToFull
			if (ParamSBS0x13 != null)
			{
				short ustate = 0;// (short)sRam[(int)prShortIndex.iBGState];
				float femp = 65535;

				//if ((ustate & (short)BGStateenum.BGSTATE_MASK) == (short)BGStateenum.BGSTATE_CHARGE)
				//{
				//if (((short)ParamSBS0x16.phydata & (short)SBS16enum.SBS_BSTAT_FULLY_CHARGED) != 0)
				//{
				//femp = 0;
				//}
				//else
				//{
				//femp = sbd_time_to_full(fRam[(int)prFloatIndex.iCurrent]);
				//}
				//}
				//ParamSBS0x13.phydata = femp;
				ParamSBS0x13.phydata = Convert.ToDouble(String.Format("{0:F2}", femp));
			}
			//SBSx4D, AgeFactor
			if (ParamSBS0x4D != null)
			{
				//ParamSBS0x4D.phydata = Convert.ToDouble(String.Format("{0:F2}", "1.00"));
				ParamSBS0x4D.phydata = 1;//Convert.ToDouble(String.Format("{0:F2}", fRam[(int)prFloatIndex.iAgeFactor]));
			}
			//SBSxF1
			if (ParamSBSxF1CTMah != null)
			{
				//ParamSBSxF1CTMah.phydata = fRam[(int)prFloatIndex.iCTMah];
				ParamSBSxF1CTMah.phydata = batt_info.fRC;
			}
		}

		#endregion

		#region birdge code that Android connect to Eagle

		private bool CopyPhysicalToRam()
		{
			bool bReturn = true;

			if (ParamPhyVoltage != null)
			{
				batt_info.fVolt = (float)ParamPhyVoltage.phydata;
			}
			else
			{
				//fRam[(int)prFloatIndex.iVoltage] = 0;
				batt_info.fVolt = 3000;
				uGGErrorcode = LibErrorCode.IDS_ERR_SBSSFL_GGDRV_NOVOLTAGE;
				bReturn = false;
			}
			//if (ParamPhyCurrent != null)
			//{
				//batt_info.fCurr = (float)ParamPhyCurrent.phydata;
			//}
			//else
			//{
				//fRam[(int)prFloatIndex.iCurrent] = 0;
				//batt_info.fCurr = 0;
				//uGGErrorcode = LibErrorCode.IDS_ERR_SBSSFL_GGDRV_NOCURRENT;
				//bReturn = false;
			//}
			if (ParamPhyTemperature != null)
			{
				//sRam[(int)prShortIndex.iExtTempDK] = (short)(ParamPhyTemperature.phydata * 10F + 2730.5F);  //save in DK degree
				if (!bFranTestMode)
					batt_info.fCellTemp = (float)ParamPhyTemperature.phydata;
				else
					batt_info.fCellTemp = 25.0F;
			}
			else
			{
				//sRam[(int)prShortIndex.iExtTempDK] = 2730;
				batt_info.fCellTemp = 25.0F;
				uGGErrorcode = LibErrorCode.IDS_ERR_SBSSFL_GGDRV_NOTEMPERATURE;
				bReturn = false;
			}
			//if (ParamPhyCAR != null)
			//{
				//batt_info.fRC = (float)(ParamPhyCAR.phydata);
			//}
			//else
			//{
				////fRam[(byte)prFloatIndex.iCAR] = 0F;
				//batt_info.fRC = 0;
				//uGGErrorcode = LibErrorCode.IDS_ERR_SBSSFL_GGDRV_NOCAR;
				//bReturn = false;
			//}

			return bReturn;
		}

		private bool GetOCVValue()
		{
			bool bReturn = true;

			if (ParamYL8316OCV != null)
			{
				batt_info.fOCVVolt = (float)ParamYL8316OCV.phydata;
			}
			else
			{
				//fRam[(int)prFloatIndex.iOCVolt] = 0;
				batt_info.fOCVVolt = 3000;
				uGGErrorcode = LibErrorCode.IDS_ERR_SBSSFL_GGDRV_NOOCVOLTAGE;
				bReturn = false;
			}
			if ((ParamYL8316PoOCV != null) && (ParamYL8316SleepOCV != null))
			{
				if (ParamYL8316PoOCV.phydata != 0)
				{
					//sRam[(int)prShortIndex.iOCVbit] = 1;
					batt_info.bPoOCV = true;
					batt_info.bSleepOCV = false;
				}
				else if (ParamYL8316SleepOCV.phydata != 0)
				{
					//sRam[(int)prShortIndex.iOCVbit] = 2;
					batt_info.bPoOCV = false;
					batt_info.bSleepOCV = true;
				}
				else
				{
					//sRam[(int)prShortIndex.iOCVbit] = 0;
					batt_info.bPoOCV = false;
					batt_info.bSleepOCV = false;
				}
			}
			else
			{
				//sRam[(int)prShortIndex.iOCVbit] = 0;
				batt_info.bPoOCV = false;
				batt_info.bSleepOCV = false;
				if (ParamYL8316PoOCV == null)
				{
					uGGErrorcode = LibErrorCode.IDS_ERR_SBSSFL_GGDRV_NOPOOCV;
				}
				else
				{
					uGGErrorcode = LibErrorCode.IDS_ERR_SBSSFL_GGDRV_NOSLEEPOCV;
				}
				bReturn = false;
			}

			return bReturn;
		}

		private bool GetCtrlValue(ref byte yRet)
		{
			bool bReturn = true;
			byte yData = 0;

			//if (ParamYL8316Status != null)
			if ((ParamYL8316CtrlBI != null) && (ParamYL8316Ctrlchgsel !=null) && 
				(ParamYL8316Ctrlvme != null) && (ParamYL8316CtrlChgActive != null) &&
				(ParamYL8316CtrlSlpOCVEn != null) && (ParamYL8316CtrlSleepMode != null) && 
				(ParamYL8316CtrlSWReset != null))
			{
				//sRam[(int)prShortIndex.iStatus] = (short)ParamYL8316Status.phydata;
				yData = (byte)ParamYL8316CtrlBI.phydata;
				yData |= (byte)((int)ParamYL8316Ctrlchgsel.phydata << 1);
				yData |= (byte)((int)ParamYL8316Ctrlvme.phydata << 2);
				yData |= (byte)((int)ParamYL8316CtrlChgActive.phydata << 4);
				yData |= (byte)((int)ParamYL8316CtrlSlpOCVEn.phydata << 5);
				yData |= (byte)((int)ParamYL8316CtrlSleepMode.phydata << 6);
				yData |= (byte)((int)ParamYL8316CtrlSWReset.phydata << 7);
			}
			else
			{
				yData = 0;
				uGGErrorcode = LibErrorCode.IDS_ERR_SBSSFL_GGDRV_NOCTRLSTATUS;
				bReturn = false;
			}
			//sRam[(int)prShortIndex.iStatus] =  yData;
			yRet = yData;

			return bReturn;
		}

		private bool SetCtrlValue(byte yData)
		{
			//sRam[(int)prShortIndex.iStatus] = yData;
			bool bReturn = true;

			if ((ParamYL8316CtrlBI != null) && (ParamYL8316Ctrlchgsel != null) &&
				(ParamYL8316Ctrlvme != null) && (ParamYL8316CtrlChgActive != null) &&
				(ParamYL8316CtrlSlpOCVEn != null) && (ParamYL8316CtrlSleepMode != null) &&
				(ParamYL8316CtrlSWReset != null))
			{
				//sRam[(int)prShortIndex.iStatus] = (short)ParamYL8316Status.phydata;
				//ParamYL8316CtrlBI.phydata = (float)(yData & 0x01);		//it is readonly, should not try to modify it
				//ParamYL8316Ctrlchgsel.phydata = (float)((yData & 0x02) >> 1);	//it's readonly
				ParamYL8316Ctrlvme.phydata = (float)((yData & 0x0C) >> 2);
				//ParamYL8316CtrlChgActive.phydata = (float)((yData & 0x10) >> 4);	//it's readonly
				ParamYL8316CtrlSlpOCVEn.phydata = (float)((yData & 0x20) >> 5);
				ParamYL8316CtrlSleepMode.phydata = (float)((yData & 0x40) >> 6);
				ParamYL8316CtrlSWReset.phydata = (float)((yData & 0x80) >> 7);
				//ParamYL8316CtrlBI.errorcode = LibErrorCode.IDS_ERR_SBSSFL_REQUIREWRITE;		//readonly
				//ParamYL8316Ctrlchgsel.errorcode = LibErrorCode.IDS_ERR_SBSSFL_REQUIREWRITE;		//readonly
				ParamYL8316Ctrlvme.errorcode = LibErrorCode.IDS_ERR_SBSSFL_REQUIREWRITE;
				//ParamYL8316CtrlChgActive.errorcode = LibErrorCode.IDS_ERR_SBSSFL_REQUIREWRITE;	//readonly
				ParamYL8316CtrlSlpOCVEn.errorcode = LibErrorCode.IDS_ERR_SBSSFL_REQUIREWRITE;
				ParamYL8316CtrlSleepMode.errorcode = LibErrorCode.IDS_ERR_SBSSFL_REQUIREWRITE;
				ParamYL8316CtrlSWReset.errorcode = LibErrorCode.IDS_ERR_SBSSFL_REQUIREWRITE;
			}
			else
			{
				uGGErrorcode = LibErrorCode.IDS_ERR_SBSSFL_GGDRV_NOCTRLSTATUS;
				bReturn = false;
			}

			return bReturn;
		}

		private void WriteRegFunction(Parameter pmrTagWrite, float fValue)
		{
			if (pmrTagWrite != null)
			{
				pmrTagWrite.phydata = fValue;
				pmrTagWrite.errorcode = LibErrorCode.IDS_ERR_SBSSFL_REQUIREWRITE;
			}
		}

		private void SetupChargeVoltData()
		{
			one_latitude_data_t tda;
			charge_volt_data.Clear();
			tda.iVoltage = 3947;
			tda.iRSOC = 1;
			charge_volt_data.Add(tda);
			tda.iVoltage = 3977;
			tda.iRSOC = 5;
			charge_volt_data.Add(tda);
			tda.iVoltage = 4012;
			tda.iRSOC = 10;
			charge_volt_data.Add(tda);
			tda.iVoltage = 4037;
			tda.iRSOC = 15;
			charge_volt_data.Add(tda);
			tda.iVoltage = 4050;
			tda.iRSOC = 20;
			charge_volt_data.Add(tda);
			tda.iVoltage = 4062;
			tda.iRSOC = 25;
			charge_volt_data.Add(tda);
			tda.iVoltage = 4080;
			tda.iRSOC = 30;
			charge_volt_data.Add(tda);
			tda.iVoltage = 4097;
			tda.iRSOC = 35;
			charge_volt_data.Add(tda);
			tda.iVoltage = 4117;
			tda.iRSOC = 40;
			charge_volt_data.Add(tda);
			tda.iVoltage = 4140;
			tda.iRSOC = 45;
			charge_volt_data.Add(tda);
			tda.iVoltage = 4165;
			tda.iRSOC = 50;
			charge_volt_data.Add(tda);
			tda.iVoltage = 4192;
			tda.iRSOC = 55;
			charge_volt_data.Add(tda);
			tda.iVoltage = 4217;
			tda.iRSOC = 60;
			charge_volt_data.Add(tda);
			tda.iVoltage = 4250;
			tda.iRSOC = 65;
			charge_volt_data.Add(tda);
			tda.iVoltage = 4270;
			tda.iRSOC = 70;
			charge_volt_data.Add(tda);
			tda.iVoltage = 4300;
			tda.iRSOC = 80;
			charge_volt_data.Add(tda);
			tda.iVoltage = 4312;
			tda.iRSOC = 85;
			charge_volt_data.Add(tda);
			tda.iVoltage = 4325;
			tda.iRSOC = 90;
			charge_volt_data.Add(tda);
			tda.iVoltage = 4340;
			tda.iRSOC = 95;
			charge_volt_data.Add(tda);
			CHARGE_VOLT_NUM = charge_volt_data.Count;
		}

		private float LutChargeVoltDataKvalue(float fVolt)
		{
			float fRet = 0F;
			int itarg = 0;

			for (itarg = 0; itarg < charge_volt_data.Count; itarg++)
			{
				if (charge_volt_data[itarg].iVoltage >= fVolt)
					break;
			}

			if (itarg == 0)
			{
				if (charge_volt_data[itarg + 1].iVoltage == charge_volt_data[itarg].iVoltage)
				{
					fRet = -1F;
				}
				else
				{
					fRet = 1000 * (charge_volt_data[itarg + 1].iRSOC - charge_volt_data[itarg].iRSOC) /
											(charge_volt_data[itarg + 1].iVoltage - charge_volt_data[itarg].iVoltage);
				}
				bmu_printk(string.Format("11111*****{0:d}, {1:d}, {2:d}, {3:d}", charge_volt_data[itarg].iRSOC, charge_volt_data[itarg + 1].iRSOC, charge_volt_data[itarg].iVoltage, charge_volt_data[itarg + 1].iVoltage));
			}
			else if (itarg == charge_volt_data.Count)
			{
				if (charge_volt_data[itarg - 1].iVoltage == charge_volt_data[itarg - 2].iVoltage)
				{
					fRet = -1F;
				}
				else
				{
					fRet = 1000 * (charge_volt_data[itarg - 1].iRSOC - charge_volt_data[itarg - 2].iRSOC) / 
						(charge_volt_data[itarg - 1].iVoltage - charge_volt_data[itarg - 2].iVoltage);
				}
				bmu_printk(string.Format("2222*****{0:d}, {1:d}, {2:d}, {3:d}", charge_volt_data[itarg - 2].iRSOC, charge_volt_data[itarg - 1].iRSOC, charge_volt_data[itarg - 2].iVoltage, charge_volt_data[itarg - 1].iVoltage));
			}
			else
			{
				if (charge_volt_data[itarg].iVoltage == charge_volt_data[itarg - 1].iVoltage)
				{
					fRet = -1F;
				}
				else
				{
					fRet = 1000 * (charge_volt_data[itarg].iRSOC - charge_volt_data[itarg - 1].iRSOC) /
						(charge_volt_data[itarg].iVoltage - charge_volt_data[itarg - 1].iVoltage);
				}
				bmu_printk(string.Format("3333****{0:d},{1:d},{2:d},{3:d}", charge_volt_data[itarg].iRSOC, charge_volt_data[itarg - 1].iRSOC, charge_volt_data[itarg].iVoltage, charge_volt_data[itarg - 1].iVoltage));
			}

			return fRet;
		}

		#endregion

		#region porting bmulib.c

		private void bmu_init_chip()
		{
			byte i;
			int iData;
			float fdata;
			//memset((uint8_t*)kernel_memaddr, num_0, byte_num);    //assign memory, skip this...

			//--------------------------------------------------------------------------------------------------
			gas_gauge.charge_end = 0;
			gas_gauge.charge_max_ratio = 5000;
			gas_gauge.discharge_end = 0;
			volt_gg.m_fCurrent_step = VOLTGG_CURR_STEP;
			volt_gg.m_fCurrent = 0;
			volt_gg.m_fCoulombCount = 0;
			volt_gg.m_fStateOfCharge = 0;
			volt_gg.m_fResCap = 0;
			volt_gg.m_iSuspendTime = 0;
			volt_gg.m_fFCC = myProject.dbDesignCp;//(long)config_test.design_capacity;
			volt_gg.m_cPreState = 0;
			volt_gg.m_volt_drop_th = VOLT_DROP_TH1;
			volt_gg.m_volt_cv_th = VOLT_D_AVG_CV1;
			volt_gg.m_fMaxErrorSoc = VOLTGG_MAX_ERR;
			
			pwron_current = VOLTGG_PWR_CURRENT;		//(A151015)Francis, cannot set in initializer, so set it here.
			bmu_init_gg();

			//--------------------------------------------------------------------------------------------------
			//copy memory for table content, skip
			//memcpy(((uint8_t*)kernel_memaddr + byte_num), (uint8_t*)charge_data, 4 * gas_gauge.charge_table_num * 2);

			//bmu_printk(string.Format("byte_num is {0}", byte_num));

			power_on_flag = 0;
			bmu_sleep_flag = 0;
			batt_info.i2c_error_times = 0;

			bmu_printk(string.Format("AAAA COBRA YL8316 DRIVER VERSION is {0}", VERSION));

			//(D150828)Francis, delete it cause YL8316 has no current
			//note that dbRsense saves in Ohm format, not mOhm format
			//gas_gauge.overflow_data = (Int32)((32768 * 5) / (myProject.dbRsense * 1000));

			//if (parameter_customer.debug)
			//{
				//bmu_printk(string.Format("yyyy gas_gauge.overflow_data is {0}", gas_gauge.overflow_data));
			//}

			bmu_printk(string.Format("COBRA YL8316 test parameter  {0:F2},{1:F2},{2:F2},{3:F2},{4:F2},{5:F2},{6:F2},{7:d},{8:d},",
				myProject.dbPullupR,
				myProject.dbPullupV,
				2.5F,//myProject.fVoltLSB,
				myProject.dbDesignCp,
				myProject.dbChgCVVolt,
                parameter_customer.charge_soc_time_factor,
				myProject.dbDsgEndVolt,
                parameter_customer.suspend_current,//myProject.board_offset,
				parameter_customer.debug)
			);

			/* (D151015)Francis, move it up to before bmu_init_gg();
			volt_gg.m_fCurrent_step = 20;
			volt_gg.m_fCurrent = 0;
			volt_gg.m_fCoulombCount = 0;
			volt_gg.m_fStateOfCharge = 0;
			volt_gg.m_fResCap = 0;
			volt_gg.m_iSuspendTime = 0;
			volt_gg.m_fFCC = (int)myProject.dbDesignCp;
			volt_gg.m_cPreState = 0;
            volt_gg.m_volt_drop_th = VOLT_DROP_TH1;
            volt_gg.m_volt_cv_th = VOLT_D_AVG_CV1;
            volt_gg.m_fMaxErrorSoc = VOLTGG_MAX_ERR;
			*/

			//wake up YL8316 into FullPower mode
			//ret = afe_register_read_byte(YL8316_OP_CTRL, &i);
			//if (ParamYL8316Status != null)
			//i = (int)ParamYL8316Status.phydata;
			//else 
			//i = 0;
			i = 0;
			if (GetCtrlValue(ref i))
			{
				//i = sRam[(int)prShortIndex.iStatus];
			}
			else
			{
				i = 0;
			}
			bmu_printk(string.Format("first read 0x09, i is {0:d}", i));


			bmu_printk(string.Format("YL8316_cell_num  is  {0:d}", YL8316_cell_num));

			if (YL8316_cell_num > 1)
				//afe_register_write_byte(YL8316_OP_CTRL, num_0x2c);
				i = 0x2C;
			else
				//afe_register_write_byte(YL8316_OP_CTRL, num_0x20);
				i = 0x20;
			//WriteRegFunction(ParamYL8316Status, i);
			SetCtrlValue((byte)i);

			bmu_printk(string.Format("COBRA YL8316 parameter  {0:F2},{1:F2},{2:F2},{3:F2},{4:F2},{5:d},{6:F2},{7:d},{8:d}",
				myProject.dbPullupR,
				myProject.dbPullupV,
				2.5F,//myProject.fVoltLSB,
				myProject.dbDesignCp,
				myProject.dbChgCVVolt,
                parameter_customer.charge_soc_time_factor,
				myProject.dbDsgEndVolt,
                parameter_customer.suspend_current,//myProject.board_offset,
				parameter_customer.debug)
			);

            check_YL8316_staus();
			trim_bmu_VD23();

			/* (D151012)Francis, delete because of 2015.10.02 Version
			//read data
			//afe_read_cell_volt(&batt_info->fVolt);
			//afe_read_current(&batt_info->fCurr);
			CopyPhysicalToRam();
			GetOCVValue();
			batt_info.fCurr = -1 * volt_gg.m_fCurrent;
			//afe_read_cell_temp(&batt_info->fCellTemp);
			batt_info.chg_on = check_charger_status();

			volt_gg_state();

			//read OCV value
			//for(i = 0;i < 3;i++)
			//{
				//ret = afe_register_read_word(YL8316_OP_OCV_LOW,&value);
				//if(ret >= 0)break;
			//}

			bmu_printk(string.Format("read batt_info.fVolt is {0:F2}",(batt_info.fVolt * YL8316_cell_num)));
			bmu_printk(string.Format("read ocv flag ret is PoOCV = {0:d}, SleepOCV = {1:d}",(int)ParamYL8316OCV.phydata, (int)ParamYL8316SleepOCV.phydata));

			//if (ret >= num_0 )		//if communicate successfully
			//{
			// YL8316 First power on 
			//if (value & POWER_OCV_FLAG)
			if(ParamYL8316PoOCV.phydata != 0)	//PoOCV flag
			{
				power_on_flag = 1;
				//msleep(2000);
				Thread.Sleep(2000);
				//afe_read_ocv_volt(&batt_info->fOCVVolt);
				//afe_read_cell_volt(&batt_info->fVolt);

				volt_gg_state();		//check system state if possible
			
				if(YL8316_cell_num > 1)
					batt_info.fOCVVolt = batt_info.fVolt;
			
				bmu_printk(string.Format("AAAA ocv volt is {0:F2}",(batt_info.fOCVVolt * YL8316_cell_num)));
				bmu_printk(string.Format("AAAA volt is {0:F2}",(batt_info.fVolt * YL8316_cell_num)));
			
				if((batt_info.fOCVVolt  > myProject.dbChgCVVolt) ||//config_data.charge_cv_voltage) || 
					(batt_info.fOCVVolt  < myProject.GetTSOCbyOCVLowVolt()))//parameter.ocv[num_0].x)){
				{
					//msleep(num_1000);
					Thread.Sleep(1000);
					//afe_read_cell_volt(&batt_info.fVolt);
					batt_info.fOCVVolt = batt_info.fVolt;
					bmu_printk(string.Format("AAAAA ocv data errror ,so batt_info.fVolt is {0:F2}",batt_info.fVolt));
				}

				//JON ADD for OCV wrong value
				//if (ABS(batt_info.fOCVVolt, batt_info.fVolt) > 200) {
				if(Math.Abs(batt_info.fOCVVolt - batt_info.fVolt) > 200)
				{
					if (batt_info.chg_dsg_flag != CHARGE_STATE)
					{
						if (batt_info.fVolt > batt_info.fOCVVolt)
							batt_info.fOCVVolt = batt_info.fVolt;
					}
				}

				//afe_read_current(&batt_info.fCurr);
				batt_info.fCurr = volt_gg.m_fCurrent;
				bmu_printk(string.Format("AAAA batt_info.fCurr is {0:F2}",batt_info.fCurr));

				bmu_printk(string.Format("AAAA batt_info.chg_dsg_flag is {0:d}",batt_info.chg_dsg_flag));
				if (batt_info.chg_dsg_flag == CHARGE_STATE)
					fdata = batt_info.fOCVVolt - 100;
				else
					fdata = batt_info.fOCVVolt;
			
				//batt_info.fRSOC = one_latitude_table(parameter.ocv_data_num,parameter.ocv,data);
				batt_info.fRSOC = myProject.LutTSOCbyOCV(fdata);
				bmu_printk(string.Format("find table batt_info.fRSOC is {0:F2}",batt_info.fRSOC)); 
			
				//if((batt_info.fRSOC >100) || (batt_info.fRSOC < 0))
					//batt_info.fRSOC = 50;
				//(M150915)Francis,
				if (batt_info.fRSOC > 100)
					batt_info.fRSOC = 99;
				if (batt_info.fRSOC < 0)
					batt_info.fRSOC = 0;
				//(E150915)

				//batt_info.fRC = batt_info.fRSOC * config_data.design_capacity / num_100;
				//ADD_1% FIX
				batt_info.fRC = (batt_info.fRSOC + 1) * myProject.dbDesignCp / 100;//config_data.design_capacity / num_100;
			
				//afe_read_current(&batt_info.fCurr);
				batt_info.fCurr = (-1 * volt_gg.m_fCurrent);
			
				//start add for volt_gg
				//volt_gg.m_fStateOfCharge = (long)batt_info.fRSOC;
				//volt_gg.m_fRsoc = (long)batt_info.fRSOC;	
				//ADD_1% FIX
				volt_gg.m_fStateOfCharge = (int)(batt_info.fRSOC + 1);
				volt_gg.m_fRsoc = (int)(batt_info.fRSOC + 1);	
				volt_gg.m_fCoulombCount = (int)(volt_gg.m_fStateOfCharge * myProject.dbDesignCp + 0.5); //config_data.design_capacity;
				//end add for volt_gg			

				//be carefull lirui don't want this code
				if ((batt_info.fVolt < (myProject.dbDsgEndVolt + 350))//(config_data.discharge_end_voltage + 350))
					&& (batt_info.chg_dsg_flag == CHARGE_STATE))
				{
					bmu_printk("Power on mode vs charge on ");
					bmu_printk(string.Format("AAAA batt_info.fCurr {0:F2}",batt_info.fCurr));
					batt_info.fRSOC = 1;
					batt_info.fRC = myProject.dbDesignCp / 100 + 10;//config_data.design_capacity / num_100 + num_10;
				}
						

				batt_info.fRCPrev = batt_info.fRC;
				gas_gauge.fcc_data= (int)(myProject.dbDesignCp+0.5);//config_data.design_capacity;

				bmu_printk("Power on mode is activated \n");
				batt_info.sCaMAH = (int)(batt_info.fRSOC * myProject.dbDesignCp / 100 + 0.5);//config_data.design_capacity / num_100;
				gas_gauge.sCtMAH = batt_info.sCaMAH; 
				gas_gauge.discharge_sCtMAH = (int)(myProject.dbDesignCp - batt_info.sCaMAH +0.5);//config_data.design_capacity - batt_info.sCaMAH; 
			
				if(batt_info.fRSOC <= 1)
				{
					gas_gauge.charge_fcc_update = 1;
					gas_gauge.sCtMAH = 0;
				}
				if(batt_info.fRSOC <= 0)
				{
					batt_info.fRSOC = 0;
					gas_gauge.discharge_end = 1;
				}		
				if(batt_info.fRSOC >= 100)
				{
					gas_gauge.charge_end = 1;
					batt_info.fRSOC = 100;
					gas_gauge.discharge_sCtMAH = 0;
					gas_gauge.discharge_fcc_update = 1;
				}		

				//if(parameter.config.debug){
				if(parameter_customer.debug)
				{
					bmu_printk("----------------------------------------------------\n");
					bmu_printk(string.Format("AAAA batt_info.fVolt is {0:F2}",(batt_info.fVolt * YL8316_cell_num)));
					bmu_printk(string.Format("AAAA batt_info.fRSOC is {0:F2}",batt_info.fRSOC));
					bmu_printk(string.Format("AAAA batt_info.sCaMAH is {0:d}",batt_info.sCaMAH));
					bmu_printk(string.Format("AAAA batt_info.fRC is {0:F2}",batt_info.fRC));
					bmu_printk(string.Format("AAAA batt_info.fCurr is {0:F2}",batt_info.fCurr));
					bmu_printk("----------------------------------------------------\n");
				}
			}	//if(ParamYL8316PoOCV.phydata != 0)	//PoOCV flag
			//else if(value & SLEEP_OCV_FLAG)
			else if(ParamYL8316SleepOCV.phydata != 0)	//SleepOCV flag
			{
				//afe_read_ocv_volt(&batt_info->fOCVVolt);
				//msleep(2000);
				//afe_read_cell_volt(&batt_info->fVolt);
				Thread.Sleep(2000);
				sleep_ocv_flag = 1;
				volt_gg_state();
				bmu_printk("Sleep ocv mode is activated \n");
			}
			else
			{
				//(A150831)Francis, if CAR is close to LutTSOCbyOCV (Volt), use it as current RSOC
				if (YL8316_cell_num > 1)
				{
					batt_info.fOCVVolt = batt_info.fVolt;
				}
				if (batt_info.fCurr > 50F)
					fdata = batt_info.fVolt - 100;
				else
					fdata = batt_info.fVolt;
				batt_info.fRSOC = myProject.LutTSOCbyOCV(fdata);		//get RSOC from current voltage
				iData = 0;
				if(bmu_read_data(BATT_CAPACITY, ref iData))
					batt_info.fRC = iData;
				fdata = (batt_info.fRC * 100) / myProject.dbDesignCp;					//get SOC by CAR divided to DesignCapacity
				if ((Math.Abs(fdata - batt_info.fRSOC) < 10)	||						//if it's nearly close
					(batt_info.fRC == 0))																			//have no reference value
				{
					power_on_flag = 1;
					wait_ocv_flag = 1;
					if (batt_info.fRSOC > 100) batt_info.fRSOC = 100F;
					if (batt_info.fRSOC < 0) batt_info.fRSOC = 0F;
					if (batt_info.fRC == 0)
						batt_info.fRC = batt_info.fRSOC * myProject.dbDesignCp / 100;
					batt_info.sCaMAH = (int)(batt_info.fRC+0.5);
					batt_info.fRCPrev = batt_info.fRC;
					volt_gg.m_fStateOfCharge = (int)(batt_info.fRSOC + 1);
					volt_gg.m_fRsoc = (int)(batt_info.fRSOC + 1);
					volt_gg.m_fCoulombCount = (int)(volt_gg.m_fStateOfCharge * myProject.dbDesignCp + 0.5); //config_data.design_capacity;
					gas_gauge.fcc_data = (int)(myProject.dbDesignCp + 0.5);
					gas_gauge.sCtMAH = batt_info.sCaMAH;
					gas_gauge.discharge_sCtMAH = (int)(myProject.dbDesignCp - batt_info.sCaMAH + 0.5);//config_data.design_capacity - batt_info.sCaMAH; 
				}
				else
				//(E150831)
				bmu_printk("Normal mode is activated.");
			}
			//afe_register_read_byte(num_0,&i);
			//printk("COBRA regeidter 0x00 is %x\n",i);

			//afe_register_read_byte(num_9,&i);
			GetCtrlValue(ref i);
			bmu_printk(string.Format("COBRA register 0x09 is 0x{0:X2}", i));
            */
		}

		private void wait_ocv_flag_fun()
		{
			float fdata;
            int i = 0;

			if (wait_ocv_flag != 0)
				return;

			if (waitocvflag_times < wait_ocv_times)
			{
				waitocvflag_times++;
				bmu_printk(string.Format("wait_ocv_flag_fun times is {0:d}", waitocvflag_times));
				return;
			}

			wait_ocv_flag = 1;
			bmu_printk(string.Format("wait_ocv_flag_fun times is {0:d}", waitocvflag_times));
			//read data
			CopyPhysicalToRam();
            //check_charger_status();       //jon had this, but volt_gg_state() also has, so ignore it.
			GetOCVValue();
            volt_gg_state();
            //read 3 times of OCV value, no need
            //for (i = 0; i < 3; i++)
            //{
                //ret = afe_register_read_word(YL8316_OP_OCV_LOW, &value);
                //if (ret >= 0) break;
            //}

            bmu_printk(string.Format("read batt_info.fVolt is {0:F2}", (batt_info.fVolt * YL8316_cell_num)));

            if (batt_info.bPoOCV)
			{
				power_on_flag = 1;
				//here in Android is to request send another read command
				//afe_read_ocv_volt(&batt_info->fOCVVolt);
				//afe_read_cell_volt(&batt_info->fVolt);
				CopyPhysicalToRam();
                volt_gg_state();			//check system state if possible  

				if (YL8316_cell_num > 1)
				{
					batt_info.fOCVVolt = batt_info.fVolt;
				}

                bmu_printk(string.Format("AAAA ocv volt is {0:F2}", (batt_info.fOCVVolt * YL8316_cell_num)));
                bmu_printk(string.Format("AAAA volt is {0:F2}", (batt_info.fVolt * YL8316_cell_num)));
                
                //if (batt_info->fOCVVolt > (config_data->charge_cv_voltage + 70))
				if (batt_info.fOCVVolt > (myProject.dbChgCVVolt + 70))
				{
					//msleep(num_1000);
					//request voltage read again, to get cell voltage then assign to OCV voltage
					//afe_read_cell_volt(&batt_info->fVolt);
					batt_info.fOCVVolt = batt_info.fVolt;
					bmu_printk(string.Format("AAAAA ocv data errror ,so batt_info->fVolt is {0:F2}",batt_info.fVolt));
				}

			    //JON ADD for OCV wrong value
			    //if (ABS(batt_info->fOCVVolt, batt_info->fVolt) > 200) {
                if(Math.Abs(batt_info.fOCVVolt - batt_info.fVolt) > 200)
                {
				    if (batt_info.chg_dsg_flag != CHARGE_STATE) 
                    {
					    if (batt_info.fVolt > batt_info.fOCVVolt)
						    batt_info.fOCVVolt = batt_info.fVolt;
				    }
			    }

			    bmu_printk(string.Format("AAAA batt_info.chg_dsg_flag is {0:d}",batt_info.chg_dsg_flag));
			    if (batt_info.chg_dsg_flag == CHARGE_STATE)
				    fdata = batt_info.fOCVVolt - 50;
			    else
				    fdata = batt_info.fOCVVolt;

				//batt_info->fRSOC = one_latitude_table(parameter->ocv_data_num,parameter->ocv,data);
				batt_info.fRSOC = myProject.LutTSOCbyOCV(fdata);
				bmu_printk(string.Format("find table batt_info.fRSOC is {0:F2}", batt_info.fRSOC));


				//if((batt_info->fRSOC >num_100) || (batt_info->fRSOC < num_0))
				//batt_info->fRSOC = num_50;
				if ((batt_info.fRSOC > 100) || (batt_info.fRSOC < 0))
				{
					batt_info.fRSOC = 50;
				}

			    //start add for volt_gg
			    //ADD_1% FIX
			    volt_gg.m_fStateOfCharge = batt_info.fRSOC;
			    volt_gg.m_fCoulombCount = volt_gg.m_fStateOfCharge * gas_gauge.fcc_data;
			    //end add for volt_gg			
			    gas_gauge.fcc_data= (int)(myProject.dbDesignCp + 0.5);//config_data.design_capacity;
			    bmu_printk("Power on mode is activated \n");
			    batt_info.sCaMAH = (int)(batt_info.fRSOC * gas_gauge.fcc_data / 100 + 0.5);
			}
			//else if (value & SLEEP_OCV_FLAG)
			else if (batt_info.bSleepOCV)
			{
				//afe_read_ocv_volt(&batt_info->fOCVVolt);
				sleep_ocv_flag = 1;
                volt_gg_state();
				bmu_printk("Sleep ocv mode is activated.");
			}
			else
			{
				//(A150831)Francis, if CAR is close to LutTSOCbyOCV (Volt), use it as current RSOC
                if (YL8316_cell_num > 1)
				{
					batt_info.fOCVVolt = batt_info.fVolt;
				}
				//if (batt_info.fCurr > 50F)
					//fdata = batt_info.fVolt - 100;
				//else
					fdata = batt_info.fVolt;
				batt_info.fRSOC = myProject.LutTSOCbyOCV(fdata);		//get RSOC from current voltage
				//fdata = (batt_info.fRC * 100) / myProject.dbDesignCp;					//get SOC by CAR divided to DesignCapacity
				i = 0;
				bmu_read_data(BATT_RSOC, ref fdata);
				//fdata = i;
				power_on_flag = 1;
				wait_ocv_flag = 1;
				if (Math.Abs(fdata - batt_info.fRSOC) < 10)							//if it's nearly close
				{
					batt_info.fRSOC = fdata;		//use file's RSOC
				}
				else
				{
				}
				if (batt_info.fRSOC > 100) batt_info.fRSOC = 100F;
				if (batt_info.fRSOC < 0) batt_info.fRSOC = 0F;
				batt_info.sCaMAH = (int)(batt_info.fRSOC * myProject.dbDesignCp / 100 + 0.5);
				//(E150831)
				bmu_printk("Normal mode is activated.");
			}

			//afe_register_read_byte(num_0, &i);
			//bmu_printk(string.Format("COBRA regeidter 0x00 is %x", i));

			//afe_register_read_byte(num_9, &i);
			//bmu_printk(string.Format("COBRA regeidter 0x09 is %x", i));	
			bmu_printk("COBRA register 0x09 is ");

		}

		private void trim_bmu_VD23()
		{
			byte yVal = 0;

			if ((ParamYL8316Regx03Bit7 != null) && (ParamYL8316Regx04Bit0 != null))
			{
				yVal = (byte)ParamYL8316Regx03Bit7.phydata;
				yVal *= 2;
				yVal += (byte)ParamYL8316Regx04Bit0.phydata;
			}

			WriteRegFunction(ParamYL8316Regx08Bit0, (float)yVal);
		}

		private void check_shutdown_voltage()
		{
			float temp;
			//Int32 infVolt;

			//
			//afe_read_cell_volt(&batt_info->fVolt);
			CopyPhysicalToRam();
			GetOCVValue();

			if (sleep_ocv_flag == 0) return;

			//temp = one_latitude_table(parameter->ocv_data_num, parameter->ocv, batt_info->fOCVVolt);
			temp = myProject.LutTSOCbyOCV(batt_info.fOCVVolt);
			bmu_printk(string.Format("Sleep ocv soc is {0:F2}", temp));

			if(temp < batt_info.fRSOC)
			{
				//batt_info.fRSOC -= 5;
				batt_info.fRSOC = temp;
				if(batt_info.fRSOC < 0)	batt_info.fRSOC = 0;
		
				batt_info.sCaMAH = (int)(batt_info.fRSOC * gas_gauge.fcc_data/ 100 + 0.5);

		//#if 0	//volt_gg
				//if(batt_info.fRC >  (gas_gauge.overflow_data - 20))
					 //batt_info.fRC = gas_gauge.overflow_data - 20;
				////afe_write_car(batt_info.fRC);
		//#endif		
				batt_info.fRCPrev 	= batt_info.fRC;

				gas_gauge.sCtMAH = batt_info.sCaMAH;
				if(gas_gauge.fcc_data > batt_info.sCaMAH)
					gas_gauge.discharge_sCtMAH = gas_gauge.fcc_data- batt_info.sCaMAH;
				else
					gas_gauge.discharge_sCtMAH = 0;
			}
		}

		//no need
		private void check_pec_control()
		{
		}

		//no need
		private void check_YL8316_staus()
		{
		}

		//no used
		private float calculate_soc_result()
		{
			float infVolt;
			float current_temp;
			float infCal, infCal_end;
			byte rc_result = 0;
			Int32 voltage_end = (int)(myProject.dbDsgEndVolt + 0.5F);//config_data->discharge_end_voltage;
			float soc_temp = 0F;
			float mah_temp = 0F;

			if (batt_info.fCurr >= 0)
			{
				infVolt = batt_info.fVolt - (gas_gauge.ri + battery_ri) * batt_info.fCurr * 0.001F; // 1000;
				bmu_printk(string.Format("current > 0 infVolt: {0:F2}", infVolt));
				//soc_temp = one_latitude_table(parameter->ocv_data_num,parameter->ocv,infVolt);
				soc_temp = myProject.LutTSOCbyOCV(infVolt);
				mah_temp = soc_temp * myProject.dbDesignCp * 0.01F; // num_100;
			}
			else
			{
				infVolt = batt_info.fVolt - gas_gauge.ri * batt_info.fCurr * 0.001F; // 1000;
				current_temp = batt_info.fCurr;

				bmu_printk(string.Format("infVolt: {0:F2}", infVolt));
				bmu_printk(string.Format("current_temp: {0:F2}", current_temp));

				//rc_result = YL8316_LookUpRCTable(
				//infVolt,
				//-current_temp,
				//batt_info->fCellTemp * 10,
				//&infCal);
				//infCal = myProject.LutRCTable(infVolt, (current_temp * -1), (batt_info.fCellTemp * 10));
				infCal = (current_temp * -10000) / myProject.dbDesignCp;
				infCal = myProject.LutRCTable((infVolt - current_temp * parameter_customer.fconnect_resist),
																		infCal, (batt_info.fCellTemp * 10));
				infCal = infCal / myProject.dbDesignCp * 10000;

				bmu_printk("result: 0");
				bmu_printk(string.Format("infCal: {0:F2}", infCal));

				//if (!rc_result)
				{
					//rc_result = YL8316_LookUpRCTable(
					//voltage_end,
					//-current_temp,
					//batt_info->fCellTemp * 10,
					//&infCal_end);
					infCal_end = (current_temp * -10000) / myProject.dbDesignCp;
					//infCal_end = myProject.LutRCTable(voltage_end, (current_temp * -1), (batt_info.fCellTemp * 10));
					infCal_end = myProject.LutRCTable(voltage_end, infCal_end, (batt_info.fCellTemp * 10));
					infCal_end = infCal_end / myProject.dbDesignCp * 10000;

					bmu_printk("result: )");
					bmu_printk(string.Format("end: {0:F2}", infCal_end));

					infCal = infCal - infCal_end;
					infCal = myProject.dbDesignCp * infCal * 0.0001F;// 10000; //remain capacity
					infCal += myProject.dbDesignCp * 0.01F + 1;// / 100 + 1;    // 1% capacity can't use

					if (infCal <= 0)
						infCal = myProject.dbDesignCp * 0.01F - 1;//config_data->design_capacity / 100 - 1;

					mah_temp = infCal;
					soc_temp = infCal * 100 / myProject.dbDesignCp;
				}
			}
			bmu_printk(string.Format("vi_mah: {0:F2}", mah_temp));
			bmu_printk(string.Format("vi_soc: {0:F2}", soc_temp));

			return soc_temp;
		}

		/* OZ1C115 bmu_wait_ready
		private void bmu_wait_ready()
		{
			Int32 data;
			Int32 fcc;
			byte i;
			Int32 ret;
			UInt16 value;
			float infVolt;
			float infCal, infCal_end;
			byte rc_result = 0;
			float voltage_end = myProject.dbDsgEndVolt;
			float current_temp;

			Int32 soc_temp;
			Int32 soc_temp_cal;

			float calculate_mah_temp;
			float calculate_soc_temp;

			//int32_t sCaMAH_temp;
			//int32_t fcc_temp;

			if (wait_ocv_flag == 0)
				return;

			bmu_printk(string.Format("AAAA bmu wait times {0:d}", waitready_times));
			//wake up YL8316 into FullPower mode
			//ret = afe_register_read_byte(YL8316_OP_CTRL,&i);
			//batt_info->Battery_ok = i & BI_STATUS;
			i = 0;
			if (GetCtrlValue(ref i))
			{
				//i = (byte)sRam[(int)prShortIndex.iStatus];
			}
			else
			{
				i = 0;
			}
			batt_info.Battery_ok = i & 0x01;
			bmu_printk(string.Format("bmu_wait_ready read 0x09 ret is {0:d},i is {1:d}", 0, i));
			if (((i & 0x40) != 0) || (i == 0))
			{
				if (YL8316_cell_num > 1)
				{
					//afe_register_write_byte(YL8316_OP_CTRL,0x2c);
					//WriteRegFunction(ParamYL8316Status, 0x2c);
					SetCtrlValue(0x2c);
				}
				else
				{
					//afe_register_write_byte(YL8316_OP_CTRL,0x20);
					//WriteRegFunction(ParamYL8316Status, 0x20);
					SetCtrlValue(0x20);
				}
				bmu_printk("YL8316 wake up function");
			}
			//ret = afe_register_read_byte(YL8316_OP_CTRL,&i);
			//printk("bmu_wait_ready read 0x09 ret is {0:d},i is {1:d}",ret,i);

			//do we need add check ocv flag now?
			//ret = afe_register_read_word(YL8316_OP_OCV_LOW,&value);
			//if ((ret >= 0) && (value & POWER_OCV_FLAG))	
			//printk("read flag too late\n");	

			//afe_read_cell_temp(&batt_info->fCellTemp);
			//afe_read_cell_volt(&batt_info->fVolt);
			//afe_read_current(&batt_info->fCurr);
			//afe_read_car(&batt_info->fRC);
			////batt_info->fRSOC = num_1;
			CopyPhysicalToRam();

			bmu_printk(string.Format("fVolt is {0:F2}", (batt_info.fVolt * YL8316_cell_num)));
			bmu_printk(string.Format("fCurr is {0:F2}", batt_info.fCurr));
			bmu_printk(string.Format("ftemp is {0:F2}", batt_info.fCellTemp));

			// every time we check this and print to see if the voltage and temp and current is abnormal
			// how about if rc_result error

			calculate_soc_temp = calculate_soc_result();

			//
			//if(calculate_soc <= calculate_soc_temp)
			//{
				//calculate_soc = calculate_soc_temp;
				//calculate_mah = calculate_soc * config_data->design_capacity / num_100;

				//printk("use: vi_soc: {0:d}",calculate_soc );
				//printk("use: vi_mah: {0:d}",calculate_mah);
			//}

			calculate_soc = calculate_soc_temp;
			calculate_mah = calculate_soc * myProject.dbDesignCp * 0.01F;//config_data->design_capacity / num_100;

			waitready_calculate_times++;
			bmu_printk(string.Format("calculate_times: {0:d}", waitready_calculate_times));


			if (power_on_flag != 0)
			{
				//use to save previous data to Linux system
				data = 1;
				bmu_write_data(OCV_FLAG, data);
				bmu_read_data(OCV_FLAG, ref data);
				if (data != 1)
				{
					bmu_printk(string.Format("write OCV_FLAG fail:{0:d}", data));
					return;
				}
				else
				{
					bmu_printk(string.Format("write OCV_FLAG success:{0:d}", data));
				}

				// (M150728)Francis, here is using to make sure filesystem is working fine, could delete
				//bmu_read_data(BATT_CAPACITY, ref data);
	
				//if(data < 0)
				//{
					//bmu_printk(string.Format("open BATT_CAPACITY fail, retry_times:{0:d}", waitready_retry_times));
					//if(waitready_retry_times >= 1)
					//{
						//bmu_printk("BATT_CAPACITY file fail.");
						//batt_info.sCaMAH = (int)(calculate_mah+0.5F);
						////goto file_fail;
                        //wait_ready_file_fail();
                        //wait_ready_code_after_file_fail();
						//return;
					//}
					//else
					//{
						//waitready_retry_times++;
						//return;
					//}
				//}

				//bmu_printk(string.Format("open BATT_CAPACITY success, retry_times:{0:d}", waitready_retry_times));
				//bmu_printk(string.Format("{0:d}", data));

                //fcc = (int)(myProject.dbDesignCp+0.5); //Csharp force ref variable needs to be assigned value before using
				//bmu_read_data(BATT_FCC, ref fcc);
				//if(fcc < 0)
				//{
					//bmu_printk("BATT_FCC file fail\n");
					//batt_info.sCaMAH = (int)(calculate_mah+0.5F);
					////goto file_fail;
                    //wait_ready_file_fail();
					//wait_ready_code_after_file_fail();
                    //return;
				//}
				//bmu_printk(string.Format("{0:d}", fcc));

				//(M150728)Francis, here is using to make sure filesystem is working fine, could delete
				//if(waitready_calculate_times < 2)
				//{
				//bmu_printk(string.Format("calculate_times return: {0:d}", waitready_calculate_times));
				//return;
				//}

				fcc = (int)(myProject.dbDesignCp + 0.5);	//assign a psedo fcc value
				data = batt_info.sCaMAH;							//assign a psedo capacity value

				soc_temp = (int)(data * 100F / fcc + 0.5F);

				if ((batt_info.fRSOC > 90) && (soc_temp > 90))
				{
					batt_info.sCaMAH = data;
					gas_gauge.fcc_data = fcc;
					bmu_printk(string.Format("start soc high USE file: {0:d},{1:d},{2:d}", batt_info.sCaMAH, gas_gauge.fcc_data, soc_temp));
				}
				else if (batt_info.fCurr >= 0)
				{
					if (Math.Abs(calculate_soc - soc_temp) > 30)
					{
						batt_info.sCaMAH = (int)(calculate_soc * myProject.dbDesignCp * 0.01F + 0.5F);//config_data->design_capacity / num_100;
						gas_gauge.fcc_data = (int)(myProject.dbDesignCp + 0.5F);//config_data->design_capacity;
						bmu_printk(string.Format("start soc chg USE ocv: {0:F2} ,{1:d}", batt_info.fRSOC, soc_temp));
					}
					else // file
					{
						batt_info.sCaMAH = data;
						gas_gauge.fcc_data = fcc;
						bmu_printk(string.Format("start soc chg USE file: {0:d},{1:d},{2:d}", batt_info.sCaMAH, gas_gauge.fcc_data, soc_temp));
					}
				}
				else
				{
					if (Math.Abs(soc_temp - calculate_soc) > 20)
					{
						batt_info.sCaMAH = (int)(calculate_mah + 0.5F);
						gas_gauge.fcc_data = (int)(myProject.dbDesignCp + 0.5F);//config_data->design_capacity;
						bmu_printk(string.Format("start soc USE RC: {0:d} ,{1:d},{2:d}", batt_info.sCaMAH, gas_gauge.fcc_data, soc_temp));
					}
					else
					{
						batt_info.sCaMAH = data;
						gas_gauge.fcc_data = fcc;
						bmu_printk(string.Format("start soc RC USE file:: {0:d},{1:d},{2:d}", batt_info.sCaMAH, gas_gauge.fcc_data, soc_temp));
					}
				}

				batt_info.fRSOC = batt_info.sCaMAH * 100;
				batt_info.fRSOC = batt_info.fRSOC / gas_gauge.fcc_data;
				wait_ready_file_fail();
				//file_fail:
				//bmu_write_data(BATT_CAPACITY,batt_info->sCaMAH);
				//data =  bmu_read_data(BATT_CAPACITY);
				//if(data != batt_info->sCaMAH) return;
				//bmu_write_data(BATT_FCC,gas_gauge->fcc_data);
				//data = bmu_read_data(BATT_FCC);
				//if(data != gas_gauge->fcc_data) return;
				//bmu_write_data(OCV_FLAG,1);
				//data = bmu_read_data(OCV_FLAG);
				//if(data != 1) return;
				//bmu_init_ok = 1;
				//gas_gauge.ocv_flag = 1;
				//batt_info.fRCPrev = batt_info.fRC;
				//printk("bmu_wait_ready, power on ok \n");

				//#ifdef FCC_UPDATA_CHARGE
				//bmu_write_data(FCC_UPDATE_FLAG,num_0);
				//#endif
			}	//if(power_on_flag != 0)
			wait_ready_code_after_file_fail();
		}
		*/

		private void bmu_wait_ready()
		{
			float data, data_soc;
			//Int32 fcc, ocv_test;
			//static uint8_t times = num_0;
			byte i;
			//Int32 ret;
			//UInt16 value;
            float infVolt = 0;
            float ocv_temp = 3800;

			bmu_printk(string.Format("AAAA bmu wait times {0:d}", waitready_times));
			//wake up YL8316 into FullPower mode
			//ret = afe_register_read_byte(YL8316_OP_CTRL,&i);
			i = 0;
			if (GetCtrlValue(ref i))
			{
			}
			else
			{
				i = 0;
			}
			bmu_printk(string.Format("bmu_wait_ready read 0x09 ret is {0:d},i is 0x{1:X2}", 0, i));

			if (((i & 0x40) != 0) || (i == 0))
			{
				if (YL8316_cell_num > 1)
				{
					//afe_register_write_byte(YL8316_OP_CTRL,0x2c);
					SetCtrlValue(0x2c);
				}
				else
				{
					//afe_register_write_byte(YL8316_OP_CTRL, 0x20);
					SetCtrlValue(0x20);
				}
				bmu_printk("YL8316 wake up function");

			}
			//ret = afe_register_read_byte(YL8316_OP_CTRL,&i);
			//printk("bmu_wait_ready read 0x09 ret is %d,i is %d \n",ret,i);

			//do we need add check ocv flag now? TBD:
			//ret = afe_register_read_word(YL8316_OP_OCV_LOW,&value);
			//if ((ret >= 0) && (value & POWER_OCV_FLAG))	
				//printk("big error,read ocv flag too late\n");	
	
			//afe_read_cell_temp(&batt_info->fCellTemp);
			//afe_read_cell_volt(&batt_info->fVolt);
			CopyPhysicalToRam();
			check_charger_status();
			volt_gg_state();
            data = -1;
            data_soc = -1;
            bmu_read_data(BATT_RSOC, ref data);
            bmu_read_data(BATT_SOC, ref data_soc);

            //force get OCV data, for noraml and sleep power on
			if (power_on_flag == 0)
			{
				if (sleep_ocv_flag != 0)
				{
					infVolt = batt_info.fOCVVolt;
				}
				else if (batt_info.chg_dsg_flag == CHARGE_STATE)
				{
					infVolt = (batt_info.fVolt - 50);
				}
				else
				{
					infVolt = batt_info.fVolt + (battery_ri * pwron_current / 1000);
				}
				ocv_temp = myProject.LutTSOCbyOCV(infVolt);
				bmu_printk(string.Format("normal power on ocv, infVolt:{0:F2}, current: {1:d}", infVolt, pwron_current));
			}
			else
			{
				ocv_temp = batt_info.fRSOC;
			}
	
			if((data<0) || (data_soc<0))
			{	//should not come here, we should not have read failed happened!
                bmu_printk(string.Format("open BATT_RSOC fail, retry_times: {0:d}", waitready_retry_times));
                if (waitready_retry_times >= power_on_retry_times)
                {
                    bmu_printk("BATT_RSOC file fail");
                    //goto file_fail;
                    wait_ready_file_fail(data, ocv_temp);
                    return;
                }
                else
                {
                    waitready_retry_times++;
                    return;
                }
            }

            bmu_printk(string.Format("open BATT_RSOC success, retry_times: {0:d}", waitready_retry_times));
            bmu_printk(string.Format("AAAA read battery rsoc data is {0:F2}", data));

			//file ok, if ocv power on, ocv in batt_info->fRSOC
            if ((power_on_flag != 0 )|| (sleep_ocv_flag != 0))
            {
                //1.Both file and OCV more than 90
                if ((batt_info.fRSOC > 90) && (data_soc > 90))
                {
                    bmu_printk(string.Format("start soc high USE file: {0:F2}, {1:F2}", batt_info.fRSOC, data_soc));
                    batt_info.fRSOC = data_soc;
                }
                else if (batt_info.chg_dsg_flag == CHARGE_STATE)
                {
                    //if (ABS(ocv_temp, data_soc) > 30)
					//if (ABS(batt_info->fRSOC, data_soc) > 30)
                    if(Math.Abs(batt_info.fRSOC - data_soc) > 30)
                    {
						bmu_printk(string.Format("start soc chg USE ocv: {0:F2}, {1:F2}", batt_info.fRSOC, data_soc));
                        //batt_info.fRSOC = ocv_temp;
                    }
                    else // file
                    {
                        bmu_printk(string.Format("start soc chg USE file: {0:F2}, {1:F2}", batt_info.fRSOC, data_soc));
                        batt_info.fRSOC = data_soc;
                    }
                }
                else
                {
                    //if (ABS(data_soc, ocv_temp) > 20)
					//if (ABS(data_soc,batt_info->fRSOC) > 20)
                    if(Math.Abs(data_soc - batt_info.fRSOC) > 20)
                    {
                        bmu_printk(string.Format("start soc USE OCV: {0:F2}, {1:F2}", batt_info.fRSOC, data_soc));
                        //batt_info.fRSOC = ocv_temp;
                    }
                    else
                    {
                        bmu_printk(string.Format("start soc RC USE file::{0:F2}, {1:F2}", ocv_temp, data_soc));
                        batt_info.fRSOC = data_soc;
                    }
                }
                bmu_printk("bmu_wait_ready, power on ok");
            }
            //if not ocv power on
            else
            {
                bmu_printk("bmu_wait_ready, normal ok");
                //if (ABS(data_soc, data) > 30)
                if(Math.Abs(data_soc - data) > 30)
                {
                    bmu_printk(string.Format("File rsoc soc, use soc file: {0:F1}, {0:F2}", data, data_soc));
                    data = data_soc;
                }

                //if (ABS(data, ocv_temp) > 35)
                if(Math.Abs(data - ocv_temp) > 35)
                {
                    bmu_printk(string.Format("file ok USE ocv: {0:F2}, {1:F2}", ocv_temp, data));
                    batt_info.fRSOC = ocv_temp;
                }
                else
                {
                    bmu_printk(string.Format("file ok USE file: {0:F2}, {1:F2}", ocv_temp, data));
                    batt_info.fRSOC = data;
                }
            }
			wait_ready_file_fail(data, ocv_temp);
        }

		private void wait_ready_file_fail(float data, float ocv_temp)
		{
            float data_soc =  -1F;

            if(data < 0)
            {
                batt_info.fRSOC = ocv_temp;
                bmu_printk("bmu_wait_ready, file fail.");
            }
            gas_gauge.fcc_data = (int)(myProject.dbDesignCp+0.5);//config_data->design_capacity;
            //ADD_1% FIX
            batt_info.sCaMAH = (int)((batt_info.fRSOC + 1) * gas_gauge.fcc_data / 100 - 0.5);

            //ADD_1% FIX
            volt_gg.m_fStateOfCharge = batt_info.fRSOC;
            volt_gg.m_fCoulombCount = batt_info.sCaMAH * 100;
            volt_gg.m_fFCC = gas_gauge.fcc_data;

            bmu_printk(string.Format("AAAA batt_info.fVolt is {0:F2}", (batt_info.fVolt * YL8316_cell_num)));
            bmu_printk(string.Format("AAAA batt_info.fRSOC is {0:F2}", batt_info.fRSOC));
            bmu_printk(string.Format("AAAA batt_info.sCaMAH is {0:d}", batt_info.sCaMAH));
            bmu_printk(string.Format("AAAA fcc is {0:d}", gas_gauge.fcc_data));

            bmu_init_ok = 1;

            if (batt_info.fRSOC <= 0)
            {
                gas_gauge.discharge_end = 1;
                batt_info.fRSOC = 0;
            }
            if (batt_info.fRSOC >= 100)
            {
                gas_gauge.charge_end = 1;
                batt_info.fRSOC = 100;
            }
            vRSOC_PRE = batt_info.fRSOC;
            vSOC_PRE = volt_gg.m_fStateOfCharge;

            // double check
            data = -1;
            bmu_read_data(BATT_RSOC, ref data);
            if (data != batt_info.fRSOC)
            {
                bmu_write_data(BATT_RSOC, batt_info.fRSOC);
                bmu_printk(string.Format("init write RSOC {0:F2}, {1:F2}", batt_info.fRSOC, data));
            }
            bmu_read_data(BATT_SOC, ref data_soc);
            if (data_soc != batt_info.fRSOC)
            {
                bmu_write_data(BATT_SOC, volt_gg.m_fStateOfCharge);
                bmu_printk(string.Format("init write SOC {0:F2}, {1:F2}", volt_gg.m_fStateOfCharge, data_soc));
            }
        }

		private void bmu_power_down_chip()
		{
			if (YL8316_cell_num > 1)
			{
				//afe_register_write_byte(YL8316_OP_CTRL,num_0x4c |SLEEP_OCV_EN);
				//WriteRegFunction(ParamYL8316Status, 0x6C);
				SetCtrlValue(0x6c);
			}
			else
			{
				//afe_register_write_byte(YL8316_OP_CTRL,SLEEP_MODE | SLEEP_OCV_EN);
				//WriteRegFunction(ParamYL8316Status, 0x60);
				SetCtrlValue(0x60);
			}

			//afe_read_cell_temp(&batt_info->fCellTemp);
			//afe_read_cell_volt(&batt_info->fVolt);
			//afe_read_current(&batt_info->fCurr);
			//afe_read_car(&batt_info->fRC);
			CopyPhysicalToRam();

			if (parameter_customer.debug)
			{
				bmu_printk("eeee power down  YL8316.");
				bmu_printk(string.Format("eeee batt_info->fVolt is {0:F2}", (batt_info.fVolt * YL8316_cell_num)));
				bmu_printk(string.Format("eeee batt_info->fRSOC is {0:F2}", batt_info.fRSOC));
				bmu_printk(string.Format("eeee batt_info->sCaMAH is {0:d}", batt_info.sCaMAH));
				bmu_printk(string.Format("eeee batt_info->fRC is {0:F2}", batt_info.fRC));
				bmu_printk(string.Format("eeee batt_info->fCurr is {0:F2}", batt_info.fCurr));
			}
		}

		private void bmu_wake_up_chip()
		{
			//byte ydata;
			//int32_t ret;
			//int32_t value;
			//byte discharge_flag = 0;
			//byte i= 0;
			//static uint32_t charge_tick = 0;
			//static struct timex  time_x;
			//static struct rtc_time rtc_time;

			bmu_printk("AAAA driver is __FUNCTION__");

			gas_gauge.dt_time_pre = gas_gauge.dt_time_now;
			//do_gettimeofday(&(time_x.time));
			//rtc_time_to_tm(time_x.time.tv_sec,&rtc_time);
			gas_gauge.dt_time_now = DateTime.Now;;

			//afe_read_cell_volt(&batt_info->fVolt);
			CopyPhysicalToRam();
			volt_gg_state();

			//Modify average_voltage to use charge_process() in api
			batt_info.fVoltPrev = batt_info.fVolt;
			gas_gauge.volt_average_pre_temp = batt_info.fVolt;
			gas_gauge.volt_average_pre = gas_gauge.volt_average_pre_temp;
			//gas_gauge.volt_num_i = volt_num;
			gas_gauge.volt_average_now = batt_info.fVolt;

			//Call api
			//bmu_call();		//call system, no need
			if(parameter_customer.debug)
			{
				bmu_printk("----------------------------------------------------\n");
				bmu_printk(string.Format("AAAA batt_info.fVolt is {0:F2}  {0:d}",(batt_info.fVolt * YL8316_cell_num),gas_gauge.bmu_tick));
				bmu_printk(string.Format("AAAA batt_info.fRSOC is {0:F2}  {0:d}",batt_info.fRSOC,gas_gauge.bmu_tick));
				bmu_printk(string.Format("AAAA batt_info.chg_on is {0:d}",batt_info.chg_on));
				bmu_printk(string.Format("AAAA volt_gg RSOC is {0:F2} ", volt_gg.m_fRsoc));
				bmu_printk(string.Format("AAAA volt_gg m_fStateOfCharge is {0:F2}", volt_gg.m_fStateOfCharge));
				bmu_printk("----------------------------------------------------");
			}

			//skip lots
			//#if 0
			//#endif
		}

		private void bmu_polling_loop()
		{
			float fdata;
			Int32 ret;
			byte i;

			if (bmu_init_ok == 0)
			{
				wait_ocv_flag_fun();
				bmu_wait_ready();
				return;
			}

			//afe_read_cell_temp(&batt_info->fCellTemp);
			//afe_read_cell_volt(&batt_info->fVolt);
            batt_info.fVoltPrev = batt_info.fVolt;
            CopyPhysicalToRam();
            //check_charger_status();		//it's called in volt_gg_state()
			volt_gg_state();

			//do_gettimeofday(&(time_x.time));
			//rtc_time_to_tm(time_x.time.tv_sec,&rtc_time);
            gas_gauge.dt_time_pre = gas_gauge.dt_time_now;
			gas_gauge.dt_time_now = DateTime.Now;
            volt_gg.m_dt_suspend = DateTime.Now;

			average_voltage();
			//bmu_call();
            if (batt_info.chg_dsg_flag == CHARGE_STATE)
            {
                charge_process();
            }
            else if (batt_info.chg_dsg_flag == DISCHARGE_STATE)
            {
                discharge_process();
            }
            else
            {
                idle_process();
            }

            if (gas_gauge.charge_end != 0)
            {
                if (charge_end_flag == 0)
                {
                    bmu_printk("enter yl8316 charge end");
                    charge_end_flag = 1;
                    charge_end_process();
                }
            }
            else
            {
                charge_end_flag = 0;
            }


            if (gas_gauge.discharge_end != 0)
            {
                if (discharge_end_flag == 0)
                {
                    bmu_printk("enter yl8316 discharge end");
                    discharge_end_flag = 1;
                    discharge_end_process();
                }
            }
            else
            {
                discharge_end_flag = 0;
            }

			if (parameter_customer.debug)
			{
				bmu_printk("----------------------------------------------------\n");
				bmu_printk(string.Format("VERSION is {0}", VERSION));
                bmu_printk(string.Format("battery_ok: {0:d}", batt_info.Battery_ok));
                bmu_printk(string.Format("tim_x: {0}", gas_gauge.dt_time_now.ToString()));
				bmu_printk(string.Format("fVolt: {0:F2}, sCaMAH: {1}, fCellTemp: {2:F2}, fRSOC: {3:F2}", (batt_info.fVolt * YL8316_cell_num), batt_info.sCaMAH, batt_info.fCellTemp,batt_info.fRSOC));
				bmu_printk(string.Format("m_fFCC: {0:F2}, m_fCoulombCount: {1:F2}, m_fStateOfCharge: {2:F2}", volt_gg.m_fFCC,volt_gg.m_fCoulombCount,volt_gg.m_fStateOfCharge));
				bmu_printk(string.Format("charge_time_increment: {0:d}, i2c_error_times: {1:d}", gas_gauge.charge_time_increment,batt_info.i2c_error_times));
				bmu_printk(string.Format("charger_finish: {0:d}, charge_end: {1:d}, discharge_end: {2:d}", charger_finish,charge_end_flag,gas_gauge.discharge_end));
				bmu_printk(string.Format("adapter_status: {0:d}, chg_dsg_flag: {1:d}", adapter_status,batt_info.chg_dsg_flag));
				bmu_printk("----------------------------------------------------");
			}

            fdata = -1;
	        bmu_read_data(BATT_RSOC, ref fdata);
	        if(parameter_customer.debug)
                bmu_printk(string.Format("read from FILE RSOC is {0:F2}",fdata));
	        //if(data >= 0)
			if (vRSOC_PRE != (int)(batt_info.fRSOC+0.5))
	        {
		        //if(vRSOC_PRE != batt_info.fRSOC)
				vRSOC_PRE = batt_info.fRSOC;
				if (fdata >= 0)
		        {
			        vRSOC_PRE = batt_info.fRSOC;
			        bmu_write_data(BATT_RSOC,batt_info.fRSOC);
			        if(parameter_customer.debug)
                        bmu_printk(string.Format("o2 back fRSOC 1 is {0:F2}",batt_info.fRSOC));
		        }
	        }
            fdata = -1;
	        bmu_read_data(BATT_SOC, ref fdata);
	        if(parameter_customer.debug)
                bmu_printk(string.Format("read from FILE SOC is {0:F2}",fdata));
	        //if(data >= 0)
			if (vSOC_PRE != volt_gg.m_fStateOfCharge)
			{
				if(fdata >= 0)
			    {
				    vSOC_PRE = volt_gg.m_fStateOfCharge;
				    bmu_write_data(BATT_SOC, volt_gg.m_fStateOfCharge);
				    if(parameter_customer.debug)
                        bmu_printk(string.Format("o2 back fStateOfCharge 1 is {0:F2}",volt_gg.m_fStateOfCharge));
			    }
	        }

			//wake up YL8316 into FullPower mode
			//ret = afe_register_read_byte(YL8316_OP_CTRL,&i);
			ret = 0;
			i = 0;
			//if(ParamYL8316Status != null)
			if (GetCtrlValue(ref i))
			{
				//i = (byte)(int)ParamYL8316Status.phydata;
				//i = (byte)sRam[(int)prShortIndex.iStatus];
			}
			else
			{
				i = 0;
			}
			batt_info.Battery_ok = i & 0x01;
			if (ret < 0)
				batt_info.Battery_ok = 0;

			if (((i & 0x40) != 0) || (ret < 0))
			{
				//if (parameter_customer.debug)
				//{
					//bmu_printk(string.Format("bmu_polling_loop read 0x09 ret is {0:d},i is {1:d}", ret, i));
				//}
				if (YL8316_cell_num > 1)
				{
					//afe_register_write_byte(YL8316_OP_CTRL,num_0x2c);
					//WriteRegFunction(ParamYL8316Status, 0x2C);
					SetCtrlValue(0x2c);
				}
				else
				{
					//afe_register_write_byte(YL8316_OP_CTRL,num_0x20);
					//WriteRegFunction(ParamYL8316Status, 0x20);
					SetCtrlValue(0x20);
				}
				bmu_printk("YL8316 wake up function\n");
			}
		}

		//(A150827)Francis,
		private void volt_gg_state()
		{
			//byte change_flg = 0;	//state about to change flag
			float volt_old = 0;
			float volt_avg = 0;	//four reading avg volt
            float volt_th = 0;

			check_charger_status();

			//find voltage now
			if (batt_info.fVolt == 0)
				//afe_read_cell_volt(&batt_info.fVolt);
				batt_info.fVolt = 3800F;		//pseudo value
			if (batt_info.m_volt_2nd == 0)
				batt_info.m_volt_2nd = batt_info.fVolt;
			if (batt_info.m_volt_1st == 0)
				batt_info.m_volt_1st = batt_info.fVolt;

			volt_old = batt_info.m_volt_2nd;	//copy 2nd to temp value
			batt_info.m_volt_2nd = batt_info.m_volt_1st;	//copy 1st to 2nd
			batt_info.m_volt_1st = batt_info.m_volt_pre;	//copy pre to 1st
			batt_info.m_volt_pre = batt_info.fVolt;		//copy now to pre
			volt_avg = (volt_old + batt_info.m_volt_pre +
				batt_info.m_volt_1st + batt_info.m_volt_2nd) * 100 / 4;
			//Update long average
			if (batt_info.m_volt_avg_long == 0)
				batt_info.m_volt_avg_long = batt_info.fVolt * 100;
			else
			{
				batt_info.m_volt_avg_long = (batt_info.m_volt_avg_long * 98 / 100) +
					(batt_info.fVolt * 2);
			}

			/**************************************************************
			*	IF CHARGER STATUS IS ACTIVE
			***************************************************************/
			if (adapter_status != 0)
			{
				/*
				 * BELOW ARE CHARGER PRESENT PROCESSES
				 * DEFAULT WE ASSUME IN CHARGE_STATE, ONLY WHEN 
				 * 1. VOLTAGE DROP A LITTLE BIT, MEANS CHARGER REMOVED DURING TEST
				 * 2. VOLTAGE LONG AVG EQUALS TO CV VOLTAGE, MEANS FULLY CHARGED
				 */
                volt_gg.m_cPreState = batt_info.chg_dsg_flag;
				//By default, volt_avg will raise a little bit when charger attached
                volt_th = VOLT_AVG_RNG_TH1 * 100;   //30mV
				if ((volt_avg > batt_info.m_volt_avg_long)
					//&& (ABS(volt_avg, batt_info.m_volt_avg_long) > 300))
					&& (Math.Abs(volt_avg - batt_info.m_volt_avg_long) > volt_th))
				{
					if (batt_info.chg_dsg_flag != CHARGE_STATE)
						batt_info.chg_dsg_flag = CHARGE_STATE;
				}

				//1. volt_avg drop more than 150, means charging stopped, but charger not removed
                volt_th = volt_gg.m_volt_drop_th * 100; //50mV
				if ((volt_avg < batt_info.m_volt_avg_long)
					//&& (ABS(volt_avg, batt_info.m_volt_avg_long) > 200))
					&& (Math.Abs(volt_avg - batt_info.m_volt_avg_long) > volt_th))
				{
					if (batt_info.chg_dsg_flag == CHARGE_STATE)
						batt_info.chg_dsg_flag = DISCHARGE_STATE;

				}
				//2. volt_avg and volt_avg_long almost equal to CV voltage, means charger attached
				//if ((ABS(volt_avg, batt_info.m_volt_avg_long) < 100)
				//&& (ABS(volt_avg, config_data.charge_cv_voltage) < 100))
                volt_th = volt_gg.m_volt_cv_th * 100;   //50mV
                volt_old = myProject.dbChgCVVolt * 100;//config_data->charge_cv_voltage * 100;
				if ((Math.Abs(volt_avg - batt_info.m_volt_avg_long) < volt_th)
                    && (Math.Abs(volt_avg - volt_old) < volt_th))
				{
					if (batt_info.chg_dsg_flag != CHARGE_STATE)
						batt_info.chg_dsg_flag = CHARGE_STATE;
				}
				else
				{
					if (parameter_customer.debug)
					{
						bmu_printk(string.Format("FFF volt_avg is {0:F2}", volt_avg));
						bmu_printk(string.Format("FFF batt_info.m_volt_avg_long is {0:F2}", batt_info.m_volt_avg_long));
						bmu_printk(string.Format("FFF volt_old is{0:F2}", volt_old));
					}
					volt_th = 500;  //5mV
                    volt_old = batt_info.fVolt * 100;
                    //if ((ABS(batt_info->m_volt_avg_long, volt_old) < volt_th)
                    if((Math.Abs(batt_info.m_volt_avg_long - volt_old) < volt_th)
                        && ((volt_old+volt_th) <= batt_info.m_volt_avg_long))
                    {
						if (parameter_customer.debug)
						{
							bmu_printk(string.Format("FFF volt_th is {0:F2}", volt_th));
							bmu_printk(string.Format("FFF batt_info.m_volt_avg_long is {0:F2}", batt_info.m_volt_avg_long));
							bmu_printk(string.Format("FFF volt_old is{0:F2}", volt_old));
						}
						batt_info.chg_dsg_flag = IDLE_STATE;
                    }
                    else
                    {
                        if (volt_old > volt_avg)
                            batt_info.chg_dsg_flag = CHARGE_STATE;
                    }
                }
			}
			/**************************************************************
			*	IF CHARGER STATUS IS INACTIVE
			***************************************************************/
			else
			{
                volt_gg.m_cPreState = batt_info.chg_dsg_flag;
                if (batt_info.chg_dsg_flag != DISCHARGE_STATE)
                {
                    batt_info.chg_dsg_flag = DISCHARGE_STATE;
                }
                if ((volt_gg.m_iSuspendTime != 0) && (volt_gg.m_cPreState == DISCHARGE_STATE))
                {
                    batt_info.chg_dsg_flag = IDLE_STATE;
                }
                /*volt_th = 500;		//5mV
                volt_old = batt_info->fVolt * 100;
                if (ABS(batt_info->m_volt_avg_long, volt_old) < volt_th)
                {
                    batt_info->chg_dsg_flag = IDLE_STATE;
                }*/
            }
			if (parameter_customer.debug)
			{
				bmu_printk(string.Format("BBBB batt_info.chg_dsg_flag is {0:d}", batt_info.chg_dsg_flag));
				bmu_printk(string.Format("BBBB batt_info.m_volt_avg_long is {0:F2}", batt_info.m_volt_avg_long));
				bmu_printk(string.Format("BBBB volt_avg is{0:F2}", volt_avg));
			}
		}

		private void average_voltage()
		{
			//find average voltage pre and now
			gas_gauge.volt_average_pre = gas_gauge.volt_average_now;

            if ((gas_gauge.volt_avg_index < volt_avg_num) && (gas_gauge.volt_average_pre <= 3000))
            {
                if (gas_gauge.volt_avg_index == 0)
                {
                    gas_gauge.volt_average_pre_temp = 0;
                }

                //if (ABS(batt_info.fVoltPrev, batt_info.fVolt) < VOLT_AVG_RNG_TH1)
                if(Math.Abs(batt_info.fVoltPrev - batt_info.fVolt) < VOLT_AVG_RNG_TH1)
                {
                    gas_gauge.volt_average_pre_temp += batt_info.fVolt;
                    gas_gauge.volt_avg_index++;
                    //printk("111gas_gauge.volt_average_pre_temp is %d\n",gas_gauge.volt_average_pre_temp);
                    //printk("111gas_gauge.volt_avg_index is %d\n",gas_gauge.volt_avg_index);
                }
                if (gas_gauge.volt_avg_index == volt_avg_num)
                {
                    gas_gauge.volt_average_pre = gas_gauge.volt_average_pre_temp / volt_avg_num;
                    gas_gauge.volt_average_now = gas_gauge.volt_average_pre;
                    gas_gauge.volt_avg_index = 0;
                    gas_gauge.volt_average_pre_temp = 0;
                    //printk("222gas_gauge.volt_average_pre_temp is %d\n",gas_gauge.volt_average_pre_temp);
                    //printk("222gas_gauge.volt_avg_index is %d\n",gas_gauge.volt_avg_index);
                }
            }
            else
            {
                //if (ABS(batt_info.fVoltPrev, batt_info.fVolt) < VOLT_AVG_RNG_TH1)
                if(Math.Abs(batt_info.fVoltPrev - batt_info.fVolt) < VOLT_AVG_RNG_TH1)
                {
                    gas_gauge.volt_average_now_temp += batt_info.fVolt;
                    gas_gauge.volt_avg_index++;
                    //printk("333gas_gauge.volt_average_now_temp is %d\n",gas_gauge.volt_average_now_temp);
                    //printk("333gas_gauge.volt_avg_index is %d\n",gas_gauge.volt_avg_index);
                }
                if (gas_gauge.volt_avg_index == volt_avg_num)
                {
                    gas_gauge.volt_average_now = gas_gauge.volt_average_now_temp / volt_avg_num;
                    if ((batt_info.chg_dsg_flag == CHARGE_STATE)
                        && (gas_gauge.volt_average_pre > gas_gauge.volt_average_now))
                    {
                        gas_gauge.volt_average_now = gas_gauge.volt_average_pre;
                    }
                    if ((batt_info.chg_dsg_flag == DISCHARGE_STATE)
                        && (gas_gauge.volt_average_pre < gas_gauge.volt_average_now))
                    {
                        gas_gauge.volt_average_now = gas_gauge.volt_average_pre;
                    }
                    gas_gauge.volt_avg_index = 0;
                    gas_gauge.volt_average_now_temp = 0;
                    //printk("444gas_gauge.volt_average_now_temp is %d\n",gas_gauge.volt_average_now_temp);
                    //printk("444gas_gauge.volt_avg_index is %d\n",gas_gauge.volt_avg_index);
                }
            }
            if (parameter_customer.debug)
            {
                bmu_printk(string.Format("gas_gauge.volt_average_now is {0:F2}", gas_gauge.volt_average_now));
                bmu_printk(string.Format("gas_gauge.volt_average_pre is {0:F2}", gas_gauge.volt_average_pre));
            }            
		}

		private void charge_end_process()
		{
            batt_info.fRSOC = 100;
			batt_info.sCaMAH = (int)(batt_info.fRSOC * volt_gg.m_fFCC / 100 + 0.5);
            gas_gauge.fcc_data = (int)(volt_gg.m_fStateOfCharge * myProject.dbDesignCp / 100 + 0.5);//config_data.design_capacity / 100;
            volt_gg.m_fStateOfCharge = 100;
            gas_gauge.charge_end = 1;
            charge_end_flag = 1;
            charger_finish = 0;
            power_on_flag = 0;
            bmu_write_data(BATT_RSOC, batt_info.fRSOC);
            bmu_write_data(BATT_SOC, volt_gg.m_fStateOfCharge);
            bmu_printk("yyyy end charge \n");
        }

		private void discharge_end_process()
		{
            batt_info.fRSOC = 0;
			batt_info.sCaMAH = (int)(volt_gg.m_fFCC / 100 - 0.5);
            gas_gauge.discharge_end = 1;
            bmu_write_data(BATT_RSOC, batt_info.fRSOC);
            bmu_write_data(BATT_SOC, volt_gg.m_fStateOfCharge);
            bmu_printk("yyyy end discharge \n");
        }

		private void charge_process()
		{
            Int32 target_soc, delta_volt, delta_cap;
			Int32 charge_interval = 0;
            Int32 charge_soc_timer = parameter_customer.charge_soc_time_factor;

			gas_gauge.discharge_end = 0;

            if (gas_gauge.charge_end != 0) return;//this must be here

            /*if (volt_gg.m_iSuspendTime != 0) {
                if (config_data.debug) {
                    printk("nnnn wakeup time is %d\n", volt_gg.m_iSuspendTime);
                }
                charge_interval = gas_gauge.time_now - volt_gg.m_iSuspendTime;
                if (charge_interval <= 0)	charge_interval = 4;
                volt_gg.m_iSuspendTime = 0;
            }
            else {
                charge_interval = gas_gauge.time_now - gas_gauge.time_pre;
                if (charge_interval <= 0)	charge_interval = 4;
            }
            if (config_data.debug) {
                printk("charge time interval is %d\n", charge_interval);
            }*/
            charge_interval = get_time_interval();

	        //**********************************************************************
	        //	Check state change, and find initial current
	        //**********************************************************************
	        if (volt_gg.m_cPreState != CHARGE_STATE)
	        {
		        volt_gg.m_fCurrent = volt_gg_chg_current();
		        volt_gg.m_fFCC = batt_info.fRSOC - volt_gg.m_fStateOfCharge;
		        if (volt_gg.m_fFCC > 0)	
			        volt_gg.m_fFCC = gas_gauge.fcc_data;
		        else
		        {
			        volt_gg.m_fFCC = gas_gauge.fcc_data * 
						        (100 + (batt_info.fRSOC - volt_gg.m_fStateOfCharge)) / 100F;
		        }
		        //#if (1)
		        volt_gg.m_cPreState = CHARGE_STATE;		//make sure state changed
		        //#endif
		        if (parameter_customer.debug) {
			        bmu_printk(string.Format("_FUNCTION_ init state fcc: {0:F2}, fCurr: {1:F2}",
								        volt_gg.m_fFCC, volt_gg.m_fCurrent));
		        }
		        return;
	        }

	        //**********************************************************************
	        //	Do coulomb count and update SOC
	        //**********************************************************************	
	        //1. Do coulomb count w/ small value (1000*mAh)
            if (gas_gauge.charge_ratio <= 0)
            {
                gas_gauge.charge_ratio = 1200;
            }
            chargeprocess_mCapacity += volt_gg.m_fCurrent * charge_interval * gas_gauge.charge_ratio / 36000;//100*mAh
	        if (parameter_customer.debug) {
		        bmu_printk(string.Format("_FUNCTION_ charge mCapacity: {0:F2}",chargeprocess_mCapacity));
	        }
	        //2. Update SOC, RSOC w/ timer update
	        if (chargeprocess_mCapacity > (100))
	        {
		        do {
			        chargeprocess_mCapacity -= 100;
			        batt_info.sCaMAH += 1;
			        volt_gg.m_fCoulombCount += 100;
		        } while (chargeprocess_mCapacity > 100);
		        if (gas_gauge.volt_average_now < myProject.dbChgCVVolt)//config_data.charge_cv_voltage)
		        {
			        gas_gauge.charge_time_increment = 0;	//clear timer update here
		        }
		        else
		        {
			        if (vRSOC_PRE != batt_info.fRSOC)
				        gas_gauge.charge_time_increment = 0;	//clear timer update here
		        }
	        }
	
	        batt_info.fRSOC = batt_info.sCaMAH * 100 / volt_gg.m_fFCC;
	        volt_gg.m_fStateOfCharge = volt_gg.m_fCoulombCount / gas_gauge.fcc_data;
		    gas_gauge.charge_time_increment += charge_interval;
		    if (gas_gauge.volt_average_now >= myProject.dbChgCVVolt)//config_data.charge_cv_voltage)
		    {
			    charge_soc_timer = VOLTGG_CV_SOC_TIME;
		    }
		    if (gas_gauge.charge_time_increment >=charge_soc_timer)
		    {
			    while (gas_gauge.charge_time_increment >= charge_soc_timer) {
				    gas_gauge.charge_time_increment -= charge_soc_timer;
				    batt_info.sCaMAH += (int)(volt_gg.m_fFCC / 100 + 0.5);
			    }
			    bmu_printk("4444444 charge time udpate \n");
		    }

	        //**********************************************************************
	        //	Find next current, w/ charging ratio
	        //**********************************************************************
	        //1. Get current from SOC
	        volt_gg.m_fCurrent = volt_gg_chg_current();
	
	        //2. Target SOC and ratio update
			//(M151015)Francis, according to 10.13 version
	        //if ((gas_gauge.volt_average_now < (myProject.dbChgCVVolt - 20))//(config_data.charge_cv_voltage - 20))
		        //|| (batt_info.m_volt_avg_long < ((myProject.dbChgCVVolt - 20) * 100)))//((config_data.charge_cv_voltage - 20) * 100)))
			if ((gas_gauge.volt_average_now < (myProject.dbChgCVVolt - 10))//(config_data.charge_cv_voltage - 10))
				|| (batt_info.m_volt_avg_long < ((myProject.dbChgCVVolt - 10) * 100)))//((config_data.charge_cv_voltage - 10) * 100)))
			//(E151015)
			{
		        //find voltage climb ratio below cv
		        if (gas_gauge.volt_average_now == gas_gauge.volt_average_pre)
		        {
			        chargeprocess_ratio_timer += charge_interval;
		        }
		        else if (volt_gg.m_fStateOfCharge < VOLTGG_CV_SOC)
		        {
			        delta_volt = (int)(gas_gauge.volt_average_now - gas_gauge.volt_average_pre + 0.5);
			        if (delta_volt <= 0)	delta_volt = 1;
					//(M151015)Francis, according to 10.13 version
			        //chargeprocess_ratio_timer *= (int)(((myProject.dbChgCVVolt - 20) - gas_gauge.volt_average_now) / (delta_volt) + 0.5);
                        //((config_data.charge_cv_voltage - 20) - gas_gauge.volt_average_now) / (delta_volt);
					chargeprocess_ratio_timer *= (int)(((myProject.dbChgCVVolt - 10) - gas_gauge.volt_average_now) / (delta_volt) + 0.5);
					//(E151015)
			        target_soc = (int)(VOLTGG_CV_SOC - volt_gg.m_fStateOfCharge + 0.5);
					if (target_soc <= 0)	target_soc = 1;
			        delta_cap = target_soc * gas_gauge.fcc_data;	//100*mAh
			        if (parameter_customer.debug) {
				        bmu_printk(string.Format("target_soc: {0:d}, ratio_timer: {1:d}", target_soc, chargeprocess_ratio_timer));
			        }
			        if (volt_gg.m_fCurrent <= 0)	volt_gg.m_fCurrent = 1;
			        target_soc = (int)(delta_cap * 36 / volt_gg.m_fCurrent + 0.5);
			        if (chargeprocess_ratio_timer <= 0)	
                    {
                        chargeprocess_ratio_timer = 1;
                    }
			        gas_gauge.charge_ratio = target_soc * 1000 / chargeprocess_ratio_timer;
			        if (parameter_customer.debug) {
				        bmu_printk(string.Format("speed: {0:d}, ratio: {1:F2}", target_soc, gas_gauge.charge_ratio));
			        }
			        chargeprocess_ratio_timer = 0;	//clear timer here
		        }
		        else
			        chargeprocess_ratio_timer = 0;	//clear timer here
	        }
	        else
	        {
		        gas_gauge.charge_ratio = 1200;
		        chargeprocess_ratio_timer = 0;
	        }
	        //target_soc = 100 - batt_info.fRSOC;
	        //if (target_soc <= 0)	target_soc = 1;
	        //gas_gauge.charge_ratio = 1200 * (100 - volt_gg.m_fStateOfCharge) / target_soc;
	        if (gas_gauge.charge_ratio > gas_gauge.charge_max_ratio)
		        gas_gauge.charge_ratio = gas_gauge.charge_max_ratio;
	        if (parameter_customer.debug) {
		        bmu_printk(string.Format("charge fCurr: {0:F2}, ratio: {1:F2}", volt_gg.m_fCurrent, gas_gauge.charge_ratio));
	        }	
	        if (volt_gg.m_fStateOfCharge > 100)
	        {
		        volt_gg.m_fStateOfCharge = 100;
	        }
	        //EOC condition, this may be different during each project
	        if (batt_info.fRSOC >= 100)
	        {
		        if(parameter_customer.debug)
		        {
			        bmu_printk("charge count end");
		        }
		        gas_gauge.charge_end = 1;
		        //batt_info.fRSOC = 100;
		        //batt_info.sCaMAH = (int)(batt_info.fRSOC * volt_gg.m_fFCC / 100 + 0.5);
	        }
        }

		private void discharge_process()
		{
            Int16 rc_result = 0;

            if (batt_info.fRSOC < 100)
                gas_gauge.charge_end = 0;


            if (gas_gauge.discharge_end != 0) return;

            if (volt_gg.m_cPreState != DISCHARGE_STATE)
            {
                volt_gg.m_fCurrent = 0;
                volt_gg.m_fResCap = 0;
                volt_gg.m_fFCC = gas_gauge.fcc_data;
                volt_gg.m_cPreState = DISCHARGE_STATE;
            }

            rc_result = voltage_gasgauge_lookup();
            if (rc_result != 0)
            {
                bmu_printk(string.Format("volt_gg_lookup result is {0:d}", rc_result));
            }
			//EOD condition, this may be different during each project
			if (batt_info.fRSOC <= 0)
			{
				gas_gauge.discharge_end = 1;
			}
		}

        private float volt_gg_chg_current()
        {
	        Int32	inst_ri = 0;
	        float	rev_volt = 0;
	        float	rev_current = 0;
	
	        //**********************************************************************
	        //	Check state change, and find initial current
	        //**********************************************************************
        //#ifdef RI_TABLE_SUPPORT
	        //inst_ri = one_latitude_table(RI_TBL_NUM,ri_table,volt_gg.m_fStateOfCharge);
        //#else
	        inst_ri = battery_ri;
        //#endif
	        if (parameter_customer.debug)
	        {
		        bmu_printk(string.Format("_FUNCTION_ ri: {0:d}", inst_ri));
	        }
	        //rev_volt = rev_one_latitude_table(parameter.ocv_data_num,parameter.ocv,
					        //volt_gg.m_fStateOfCharge);
            rev_volt = myProject.LutOCVbyTSOC(volt_gg.m_fStateOfCharge * 32767 / 100);
            if (parameter_customer.debug)
	        {
		         bmu_printk(string.Format("_FUNCTION_  rev_volt: {0:F2}", rev_volt));
	        }
	        rev_current = (batt_info.fVolt - rev_volt) * 1000 / inst_ri;
	        if (rev_current < 0)	rev_current = 0;
	
	        return rev_current;
        }

        private Int16 voltage_gasgauge_lookup()
        {
	        int loop = VOLTGG_CMP_LOOP;
	        int ret = 0;
	        CompareStatus cpResult, cpResultPrev = CompareStatus.CP_EQUAL;
	        float dRSOCPrev = batt_info.fRSOC;
	        float fResult = 0;
	        float fSocTbl = 0, fSocEnd = 0, fSocCal = 0;
	        Int32 time_increment = 0;
	        float dFCCPrev = volt_gg.m_fFCC;
	        float fSocEndLast = 0;
	        float maxcurr = myProject.GetRCTableHighCurr();//(int)yaxis_table[Y_AXIS - 1];

	        /*if (volt_gg.m_iSuspendTime == 0) {
		        time_increment = gas_gauge.time_now - gas_gauge.time_pre;
		        if (time_increment <= 0)	time_increment = 4;
	        }
	        else {
		        time_increment = gas_gauge.time_now - volt_gg.m_iSuspendTime;
		        if (time_increment <= 0)	time_increment = 4;
		        if (volt_gg.m_fCurrent != 0) {	//clear suspendtime only after current predicted.
			        volt_gg.m_iSuspendTime = 0;
			        //volt_gg.m_cPreState = 0;
		        }
	        }*/
	        time_increment = get_time_interval();
	
	        //check 1st time here
	        if (volt_gg.m_fCurrent == 0)
	        {
		        //ret = i_lookup(batt_info->fVolt,
				        //volt_gg->m_fStateOfCharge,
				        //(batt_info->fCellTemp * 10),
				        //&fResult);
		        //if (ret)	return 1;
                fResult = myProject.LutRCTableCurrent(batt_info.fVolt, volt_gg.m_fStateOfCharge, (batt_info.fCellTemp * 10));
		        volt_gg.m_fCurrent = fResult / 10000 * myProject.dbDesignCp;
				//volt_gg.m_fCurrent = fResult;
				bmu_printk(string.Format("FFF check RC table current, volt_gg.m_fCurrent = {0:F2}", volt_gg.m_fCurrent));
		        return 0;
	        }

	        //***********************************************************
	        //not 1st here, update everything
	        //***********************************************************

	        //0. find EOD residual capacity according to latest predict current
	        //ret = rc_lookup((config_data->discharge_end_voltage),
			        //volt_gg->m_fCurrent,
			        //(batt_info->fCellTemp * 10),
			        //&fSocEnd);
	        //if (ret)	return 1;
			fSocEnd = (volt_gg.m_fCurrent * 10000) / myProject.dbDesignCp;
			fSocEnd = myProject.LutRCTable(myProject.dbDsgEndVolt, fSocEnd, (batt_info.fCellTemp * 10), false);

	        //1. Use latest predict current to coulomb count and calculate SOC
	        fSocCal = volt_gg.m_fCurrent * time_increment / 36;
	        fSocCal = volt_gg.m_fCoulombCount * 100 - (fSocCal * 100);
			if (fSocCal < 0) fSocCal = 0;
	        fSocCal /= gas_gauge.fcc_data;

	        //2. Use predict current to find RC table result
	        //ret = rc_lookup(batt_info->fVolt,
			        //volt_gg->m_fCurrent,
			        //(batt_info->fCellTemp * 10),
			        //&fSocTbl);
	        //if (ret)	return 1;
			fSocTbl = (volt_gg.m_fCurrent * 10000) / myProject.dbDesignCp;
            fSocTbl = myProject.LutRCTable(batt_info.fVolt, fSocTbl, (batt_info.fCellTemp * 10), false);

	        if (parameter_customer.debug)
            {
		        bmu_printk("----------------------------------------------------");
    	        bmu_printk(string.Format("TIME_INTERVAL: {0:d}", time_increment));
    	        bmu_printk(string.Format("fSocTbl: {0:F2}, fSocCal: {1:F2}, fSocEnd: {2:F2}", fSocTbl, fSocCal, fSocEnd));
    	        bmu_printk(string.Format("dRSOCPrev: {0:F2}, dFCCPrev: {1:F2}, m_fCurrent: {2:F2}", dRSOCPrev, dFCCPrev, volt_gg.m_fCurrent));
	        }
	        //3. Compare RC lookup result and calculated SOC
	        do {
		        //compare long type table SOC and predict SOC
		        cpResult = fCompare(ref fSocTbl, ref fSocCal, ref volt_gg.m_fMaxErrorSoc);

		        if (cpResult == CompareStatus.CP_OVER)
                {
			        //if table result over predicted, assume current drop
			        volt_gg.m_fCurrent -= volt_gg.m_fCurrent_step;
					if (volt_gg.m_fCurrent < volt_gg.m_fCurrent_step)
					{
						volt_gg.m_fCurrent = 0;
						//volt_gg.m_fCurrent = volt_gg.m_fCurrent_step;
					}
			        //ret = rc_lookup(batt_info.fVolt,
					        //volt_gg.m_fCurrent,
					        //(batt_info.fCellTemp * 10),
					        //&fSocTbl);
			        //if (ret)	return 1;
					fSocTbl = (volt_gg.m_fCurrent * 10000) / myProject.dbDesignCp;
                    fSocTbl = myProject.LutRCTable(batt_info.fVolt, fSocTbl, (batt_info.fCellTemp * 10), false);
		        }
		        else if (cpResult == CompareStatus.CP_LESS) {
			        //if table result under predicted, assume current raise
			        volt_gg.m_fCurrent += volt_gg.m_fCurrent_step;
			        /*don't limit max current here, to decrease SOC by bigger coulobcount
			        if (volt_gg.m_fCurrent > yaxis_table[Y_AXIS-1])
				        volt_gg.m_fCurrent = yaxis_table[Y_AXIS-1];*/
			        //if (volt_gg.m_fCurrent > 2000000000)
			        //	volt_gg.m_fCurrent = 2000000000;
			        //ret = rc_lookup(batt_info.fVolt,
					        //volt_gg.m_fCurrent,
					        //(batt_info.fCellTemp * 10),
					        //&fSocTbl);
			        //if (ret)	return 1;
					fSocTbl = (volt_gg.m_fCurrent * 10000) / myProject.dbDesignCp;
                    fSocTbl = myProject.LutRCTable(batt_info.fVolt, fSocTbl, (batt_info.fCellTemp * 10), false);
		        }
		        else {
			        //if equal in two serious comparison
			        /*if ((cpResult == CP_EQUAL) && (cpResultPrev == CP_EQUAL)) {
				        ret = rc_lookup_i(batt_info.fVolt,
						        volt_gg->m_fStateOfCharge,
						        (batt_info->fCellTemp * 10),
						        &fResult);
				        if (ret)	return 1;
				        volt_gg->m_fCurrent = (long)fResult;
			        }*/
		        }
		        cpResultPrev = cpResult;

		        if (cpResult != CompareStatus.CP_EQUAL)
                {
			        if (--loop <= 0)
                    {
				        //loop = VOLTGG_CMP_LOOP;
				        if (parameter_customer.debug)
                        {
					        bmu_printk(string.Format("volt_gg_SOC_compare_result: {0:d}", (byte)cpResult));
					        bmu_printk(string.Format("Adj_fSocTbl: {0:F2}, Adj_fSocCal: {1:F2}, Adj_fSocEnd: {2:F2}", fSocTbl, fSocCal, fSocEnd)); 
					        bmu_printk(string.Format("Adj_m_fCurrent: {0:F2}", volt_gg.m_fCurrent));
					        bmu_printk(string.Format("MaxErr: {0:d}", volt_gg.m_fMaxErrorSoc));
					        bmu_printk("----------------------------------------------------");
				        }
				        //leave while loop, instead of increase m_fMaxErrorSoc,
				        //this ensure kernel won't stuck in this while-loop
				        //volt_gg->m_fMaxErrorSoc += VOLTGG_ERR_STEP;				
				        //force equal to proceed the calculation
				        cpResult = CompareStatus.CP_EQUAL;
				        //break;
			        }
		        }
		        if (cpResult == CompareStatus.CP_EQUAL)
		        {
			        //if equal update absolute SOC and absoulte RC
			        volt_gg.m_fCoulombCount -= volt_gg.m_fCurrent *
												         time_increment / 36;
					if (volt_gg.m_fCoulombCount < 0) volt_gg.m_fCoulombCount = 0;
			        volt_gg.m_fStateOfCharge = volt_gg.m_fCoulombCount /
												         gas_gauge.fcc_data;

			        //if equal find new FCC, RSOC and RC
			        //1. Weight the fSocEnd to smooth new FCC and RSOC
			        fSocEndLast = 10000 - (10000 * dFCCPrev / gas_gauge.fcc_data);
			        if ((fSocEndLast == 0) || (volt_gg.m_fResCap < 0))
				        volt_gg.m_fResCap = 0;

			        //2. Calculate small increment according to the current
			        if (volt_gg.m_fCurrent > maxcurr)
				        fSocEndLast = maxcurr;
			        else
				        fSocEndLast = volt_gg.m_fCurrent;
			        fSocEndLast = fSocEndLast * time_increment * 10000 / 36 / dFCCPrev;

			        //3. Calculate new RSOC by small increment
			        fResult = (dRSOCPrev * 10000 - fSocEndLast) / 100;
			        if (fResult < 0)	fResult = 0;

			        //4. Apply chase, and wait factor to small increment
			        if (fResult > ((fSocTbl - fSocEnd) * 10000 / (10000 - fSocEnd)))
                    {
				        if ((fSocTbl > fSocEnd) && (fSocCal > fSocEnd))
                        {
					        bmu_printk(string.Format("volt_gg is chasing {0:F2}, {1:d}", fSocEndLast, time_increment));
					        fSocEndLast *= ((fSocCal - fSocEnd) * 1500 / (fSocTbl - fSocEnd));
				        }
				        volt_gg.m_fResCap += (fSocEndLast / 1000);
			        }
			        else {
				        bmu_printk(string.Format("volt_gg is waiting {0:F2}, {1:d}\n", fSocEndLast, time_increment));
						if ((fSocTbl > fSocEnd) && (fResult > 0))
						{
							//fSocEndLast *= (fResult / ((fSocTbl - fSocEnd) * 10000 / (10000 - fSocEnd))) * 1000;
							fSocEndLast *= (fResult * 1000 / ((fSocTbl - fSocEnd) * 10000 / (10000 - fSocEnd)));
						}
						else
						{
							fSocEndLast *= 250;
						}
				        volt_gg.m_fResCap += (fSocEndLast / 1000);
			        }
			        bmu_printk(string.Format("volt_gg delta is {0:F2}", fSocEndLast));

			        volt_gg.m_fFCC = gas_gauge.fcc_data -
					        (gas_gauge.fcc_data * fSocEnd) / 10000;
			
			        //EOD ADJUST
			        if ((batt_info.fVolt <= (myProject.dbDsgEndVolt - 10))//(config_data.discharge_end_voltage - 10))
				        && (batt_info.fRSOC >= 1))
			        {
				        volt_gg.m_fResCap += (1000 * time_increment);
			        }
			        if ((batt_info.fVolt > (myProject.dbDsgEndVolt + 5))//(config_data.discharge_end_voltage + 5))
				        && (dRSOCPrev < 2)) 
                    {
				        //volt_gg.m_fResCap = 0;
						if (volt_gg.m_fResCap >= 10000) volt_gg.m_fResCap = 9999;
			        }
			        /*if ((batt_info.fVolt <= config_data.discharge_end_voltage)
				        && (batt_info.fRSOC <= 1)) {
				        volt_gg.m_fResCap += 5000;
			        }*/
			        if (volt_gg.m_fResCap >= 10000)
			        {
				        //if ((batt_info.fVolt > config_data.discharge_end_voltage)
				        //	&& (dRSOCPrev <= 1))
				        //	dRSOCPrev = 2;
				        batt_info.fRSOC = dRSOCPrev - 1;
				        volt_gg.m_fResCap -= 10000;
			        }
			        else
				        batt_info.fRSOC = dRSOCPrev;

			        if (batt_info.fRSOC < 0)
				        batt_info.fRSOC = 0;

					ret = (int)(batt_info.fRSOC * volt_gg.m_fFCC / 100 + 0.5);
					if (batt_info.chg_dsg_flag == DISCHARGE_STATE)
					{
						if (ret < batt_info.sCaMAH)
							batt_info.sCaMAH = ret;
					}
			        bmu_printk(string.Format("volt_gg sCAUMAH is {0:F2}", volt_gg.m_fResCap));
			        bmu_printk(string.Format("volt_gg LOOP_COUNT is {0:d}", (VOLTGG_CMP_LOOP - loop)));
		        }
	        } while (cpResult != CompareStatus.CP_EQUAL);

	        return 0;
        }

		private float gauge_adjust_blend(byte speed)
		{
			float data;
			float factor;

			factor = gas_gauge.fcc_data / 100;
			data = factor * speed * 10 / 100;

			if (data > (factor / 2))
				data = factor / 2;

			if (data < 1) data = 2;

			return data;
		}

		private static CompareStatus fCompare(ref float fAvalue, ref float fBvalue, ref Int32 maxerr)
		{
			float relativeError = 0;
			
			if(fAvalue > fBvalue)
			{
				relativeError = ((fAvalue - fBvalue) * 10000) / fAvalue;
			}
			else
			{
				relativeError = ((fAvalue - fBvalue) * 10000) / fBvalue;
			}

			if (fAvalue == fBvalue)
				return CompareStatus.CP_EQUAL;

			if ((relativeError >= 0) && (relativeError <= maxerr))
				return CompareStatus.CP_EQUAL;
			else if ((relativeError <= 0) && (relativeError >=  -maxerr))
				return CompareStatus.CP_EQUAL;
			else if (relativeError > 0)
				return CompareStatus.CP_OVER;
			else
				return CompareStatus.CP_LESS;
		}

        private void idle_process()
        {
	        float ocv_target = 0;
	        float infVolt = 0;
	        Int32 inst_ri = 0;
	        float delta_soc = 0;
	        Int32 idle_interval = 0;
	
	        //**********************************************************************
	        //	Check state initial, setup current
	        //**********************************************************************
	        if (volt_gg.m_cPreState != IDLE_STATE)
	        {
		        volt_gg.m_fCurrent = parameter_customer.suspend_current;
		        volt_gg.m_cPreState = IDLE_STATE;
	        }

	        /*if (volt_gg.m_iSuspendTime != 0) {
		        if (config_data.debug) {
			        printk("nnnn wakeup time is %d\n", volt_gg.m_iSuspendTime);
		        }
		        idle_interval = gas_gauge.time_now - volt_gg.m_iSuspendTime;
		        if (idle_interval <= 0)	idle_interval = 4;
		        volt_gg.m_iSuspendTime = 0;
	        }
	        else {
		        idle_interval = gas_gauge.time_now - gas_gauge.time_pre;
		        if (idle_interval <= 0)	idle_interval = 4;
	        }*/
	        idle_interval = get_time_interval();
	        idleprocess_idle_timer += idle_interval;

	        //**********************************************************************
	        //	Check state timer, if reached 2hr, do OCV and current update
	        //**********************************************************************
	        if (idleprocess_idle_timer > VOLTGG_IDLE_TIME)
	        {
		        idleprocess_idle_timer -= VOLTGG_IDLE_TIME;
	        //#ifdef	RI_TABLE_SUPPORT
		        //inst_ri = one_latitude_table(RI_TBL_NUM,ri_table,volt_gg.m_fStateOfCharge);
	        //#else
		        inst_ri = battery_ri;
	        //#endif
		        infVolt = batt_info.fVolt + (volt_gg.m_fCurrent * battery_ri) / 1000;
                ocv_target = myProject.LutTSOCbyOCV(infVolt);//one_latitude_table(parameter.ocv_data_num,parameter.ocv,infVolt);
                delta_soc = Math.Abs(ocv_target - volt_gg.m_fStateOfCharge);//ABS(ocv_target, volt_gg.m_fStateOfCharge);
		        delta_soc *= volt_gg.m_fFCC / 200;
		
		        if (ocv_target < volt_gg.m_fStateOfCharge)
			        volt_gg.m_fCurrent += delta_soc;
		        else
			        volt_gg.m_fCurrent = parameter_customer.suspend_current;
		        if (volt_gg.m_fCurrent > VOLTGG_MAX_IDLE)
			        volt_gg.m_fCurrent = VOLTGG_MAX_IDLE;
		        if (parameter_customer.debug) {
			        bmu_printk(string.Format("idle timer triggered, ocv target is {0:F2}",ocv_target));
			        bmu_printk(string.Format("idle timer triggered, new current is {0:F2}",volt_gg.m_fCurrent));
		        }
		        idleprocess_idle_timer = 0;
	        }
	
	        //**********************************************************************
	        //	Coulomb count and update SOC/RSOC
	        //**********************************************************************
	        volt_gg.m_fCoulombCount -= volt_gg.m_fCurrent * idle_interval / 36;	//100*mAh
	        if (volt_gg.m_fCoulombCount <= 0)
		        volt_gg.m_fCoulombCount = 0;
	        volt_gg.m_fStateOfCharge = volt_gg.m_fCoulombCount / gas_gauge.fcc_data;
	        if ((int)vSOC_PRE != (int)volt_gg.m_fStateOfCharge)	//soc changed, update RSOC
	        {
		        batt_info.fRSOC -= 1;
		        if (batt_info.fRSOC <= 0)
			        batt_info.fRSOC = 0;
		        batt_info.sCaMAH = (int)(batt_info.fRSOC * volt_gg.m_fFCC / 100 + 0.5);
		        if (parameter_customer.debug) 
				{
					bmu_printk(string.Format("idle RSOC update, new volt_gg.m_fCoulombCount is {0:F2}", volt_gg.m_fCoulombCount));
					bmu_printk(string.Format("idle RSOC update, new volt_gg.m_fStateOfCharge is {0:F2}", volt_gg.m_fStateOfCharge));
					bmu_printk(string.Format("idle RSOC update, new gas_gauge.fcc_data is {0:d}", gas_gauge.fcc_data));
			        bmu_printk(string.Format("idle RSOC update, new current is {0:F2}",volt_gg.m_fCurrent));
		        }
	        }
        }
		/*
		private bool bmu_write_data(string strAddr, int iData)
		{
			bool bReturn = true;
			string strTargetPath = Path.Combine(strSystemLogFolder, strAddr);
			FileStream fswrite;
			StreamWriter stmwrite;

			try
			{
				fswrite = new FileStream(strTargetPath, FileMode.Create);
				stmwrite = new StreamWriter(fswrite);
				stmwrite.WriteLine(iData);
				stmwrite.Flush();
				stmwrite.Close();
				fswrite.Close();
			}
			catch (Exception e)
			{
				bReturn = false;
			}

			return bReturn;
		}

		private bool bmu_read_data(string strAddr, ref int iData)
		{
			bool bReturn = true;
			string strTargetPath = Path.Combine(strSystemLogFolder, strAddr);
			FileStream fsread;
			StreamReader stmread;
			string strtmp;

			try
			{
				fsread = new FileStream(strTargetPath, FileMode.Open);
				stmread = new StreamReader(fsread);
				strtmp = stmread.ReadLine();
				if (!int.TryParse(strtmp, out iData))
					iData = 0;
				stmread.Close();
				fsread.Close();
			}
			catch (Exception e)
			{
				iData = -1;
				bReturn = false;
			}

			return bReturn;
		}
		*/
		private bool bmu_write_data(string strAddr, float fData)
		{
			bool bReturn = true;
			string strTargetPath = Path.Combine(strSystemLogFolder, strAddr);
			FileStream fswrite;
			StreamWriter stmwrite;

			try
			{
				fswrite = new FileStream(strTargetPath, FileMode.Create);
				stmwrite = new StreamWriter(fswrite);
				stmwrite.WriteLine(fData);
				stmwrite.Flush();
				stmwrite.Close();
				fswrite.Close();
			}
			catch (Exception e)
			{
				bReturn = false;
			}

			return bReturn;
		}

		private bool bmu_read_data(string strAddr, ref float fData)
		{
			bool bReturn = true;
			string strTargetPath = Path.Combine(strSystemLogFolder, strAddr);
			FileStream fsread;
			StreamReader stmread;
			string strtmp;

			try
			{
				fsread = new FileStream(strTargetPath, FileMode.Open);
				stmread = new StreamReader(fsread);
				strtmp = stmread.ReadLine();
				//if (!int.TryParse(strtmp, out iData))
					//iData = 0;
				if (!float.TryParse(strtmp, out fData))
					fData = 0F;
				stmread.Close();
				fsread.Close();
			}
			catch (Exception e)
			{
				fData = -1;
				bReturn = false;
			}

			return bReturn;
		}

		//(A150723)Francis, add specially, to record Linux printk debug information
		private bool bmu_printk(string strLog)
		{
			bool bReturn = true;
			FileStream fsprintk;
			StreamWriter stmprintk;

			try
			{
				fsprintk = new FileStream(strprintkfile, FileMode.OpenOrCreate | FileMode.Append);
				stmprintk = new StreamWriter(fsprintk);
				stmprintk.WriteLine(strLog);
				stmprintk.Flush();
				stmprintk.Close();
				fsprintk.Close();
			}
			catch (Exception e)
			{
				bReturn = false;
			}

			return bReturn;
		}

		//(A150825)Francis,
		private void check_charger_status()
		{
			byte data = (byte)ParamYL8316CtrlChgActive.phydata;

			if (data != 0)	//PRG pull to high, CHGON is low active
			{
                adapter_status = 2; //YCHARGER_AC
			}
			else
			{
                adapter_status = 0; //YCHARGER_BATTERY
			}
		}

        //(A151014)Francis,
        private Int32 get_time_interval()
        {
	        //static int32_t last_interval = 4;	//record of last time result, default 4s
	        //struct rtc_time	rtc_now, rtc_pre;
            TimeSpan ts_now = new TimeSpan(gas_gauge.dt_time_now.Ticks);
            TimeSpan ts_pre = new TimeSpan(gas_gauge.dt_time_pre.Ticks);
            TimeSpan ts_sus = new TimeSpan(volt_gg.m_dt_suspend.Ticks);
            TimeSpan ts_diff;
	        double	interval_now = 0;
	
	        //rtc_time_to_tm(gas_gauge.time_now,&rtc_now);
	        //rtc_time_to_tm(gas_gauge.time_pre,&rtc_pre);

	        //check if RTC pre and now not in same month or same year
	        //if ((rtc_now.tm_mon != rtc_pre.tm_mon) || (rtc_now.tm_year != rtc_pre.tm_year))
            if((gas_gauge.dt_time_now.Month != gas_gauge.dt_time_pre.Month) || (gas_gauge.dt_time_now.Year != gas_gauge.dt_time_pre.Year))
	        {
		        bmu_printk(string.Format("rtc time is changed from {0}-{1} to {2}-{3}", 
			        gas_gauge.dt_time_pre.Year.ToString("D4"),
                    gas_gauge.dt_time_pre.Month.ToString("D2"),
			        gas_gauge.dt_time_now.Year.ToString("D4"),
                    gas_gauge.dt_time_now.Month.ToString("D2")));
		        if (volt_gg.m_iSuspendTime == 0) //and not wakeup from suspend
			        return gettimeinterval_last_interval;	//use last interval
	        }
	
	        if (volt_gg.m_iSuspendTime != 0) {
		        if (parameter_customer.debug) {
			        bmu_printk(string.Format("nnnn wakeup time is {0:d}", volt_gg.m_iSuspendTime));
		        }
		        //interval_now = gas_gauge.time_now - volt_gg.m_iSuspendTime;
                ts_diff = ts_now.Subtract(ts_sus).Duration();
                interval_now = ts_diff.TotalSeconds;
                if (interval_now <= 0) return gettimeinterval_last_interval;
		        if (volt_gg.m_fCurrent != 0) {	//clear suspendtime only after current predicted.
			        volt_gg.m_iSuspendTime = 0;
		        }
	        }
	        else {
		        //interval_now = gas_gauge->time_now - gas_gauge->time_pre;
                ts_diff = ts_now.Subtract(ts_pre).Duration();
                interval_now = ts_diff.TotalSeconds;
                if (interval_now <= 0) return gettimeinterval_last_interval;
	        }
	        return (Int32)interval_now;
        }

		#endregion

		#region porting parameter.c

		public void bmu_init_parameter()
		{
			//parameter_customer.config = &config_data;
			//parameter_customer.ocv = ocv_data;
			//parameter_customer.temperature = cell_temp_data;
			//parameter_customer.client = client;
			parameter_customer.ocv_data_num = myProject.GetOCVPointsNo();
			parameter_customer.cell_temp_num = myProject.GetThermalPointsNo();
			parameter_customer.charge_pursue_step = 10;
			parameter_customer.discharge_pursue_step = 6;
			parameter_customer.discharge_pursue_th = 10;
			parameter_customer.wait_method = 2;
			parameter_customer.debug = true;
            parameter_customer.charge_soc_time_factor = 320;
			parameter_customer.suspend_current = 30;

			//(A150729)Francis, copy ri,bi from project file
			parameter_customer.fconnect_resist = myProject.dbRcon;
			parameter_customer.finternal_resist = myProject.dbRbat;
			//(E150729)

			//BATT_CAPACITY = "/data/sCaMAH.dat";
			//BATT_FCC 		= "/data/fcc.dat";
			//OCV_FLAG 		= "/data/ocv_flag.dat";
			//BATT_OFFSET 	= "/data/offset.dat";
			//CM_PATH 	   	= "/system/xbin/YL8316api";

			//res_divider_ratio = 353 ;  // note: multiplied by 1000 

			//r1 = 220k,r2 = 120k,so 120 * 1000 / 120 + 220 = 353
			//r2's voltage is the voltage which YL8316 sample.

			//For example :
			//Read YL8316 voltage is vin
			//then the whole voltage is  vin * 1000 / res_divider_ratio;
		}

		public void bmu_init_gg()
		{
			gas_gauge.charge_table_num = myProject.GetChargePointsNo();
			gas_gauge.charge_voltage_table_num = CHARGE_VOLT_NUM;
			gas_gauge.rc_x_num = myProject.GetXAxisLengthofRCTable();
			gas_gauge.rc_y_num = myProject.GetWAxisLengthofRCTable();
			gas_gauge.rc_z_num = myProject.GetVAxisLengthofRCTable();
			//gas_gauge.dt_time_now = DateTime.Now;
            //gas_gauge.dt_time_pre = DateTime.Now;
            chgon_use = 1;
            battery_ri = 160;
		}


		#endregion

		#region porting oz8806_battery.c

		private void system_charge_discharge_status()
		{
			//adapter_status = 0; //CHARGER_BATTERY
			//if (ParamChargerVBusOK != null)
			//{
				//if (ParamChargerVBusOK.phydata != 0)
				//{
					//adapter_status = 1;//CHARGER_USB;
					//bmu_printk(string.Format("adapter_status: {0:d}", adapter_status));
				//}
			//}
		}

		private void discharge_end_fun()
		{
			//End discharge, this may jump 2%
			if (batt_info.fVolt < (myProject.dbDsgEndVolt -50))//(config_data.discharge_end_voltage - 50))
			{
				if (batt_info.fRSOC > 0)
				{
					batt_info.fRSOC--;
					if (batt_info.fRSOC < 0)
					{
						batt_info.fRSOC = 0;
						batt_info.sCaMAH = gas_gauge.fcc_data / 100 - 1;
						discharge_end_process();
					}
					else
						batt_info.sCaMAH = (int)(batt_info.fRSOC * gas_gauge.fcc_data / 100 + 0.5);
				}
			}
		}

		private void charge_end_fun(float rsoc_pre_in)
		{
            TimeSpan ts_diff;

			bmu_printk(string.Format("FFF enter charge_end_fun adapter_status= {0:d}", adapter_status));
			bmu_printk(string.Format("FFF rsoc_pre_in = {0:F2}, charger_time = {1:d}, charge_finish = {2:d}", rsoc_pre_in, charge_times, charger_finish));
            if (adapter_status == 0)//YCHARGER_BATTERY)
            {
                charge_times = 0;
                charger_finish = 0;
            }

            /*
            if(adapter_status == YCHARGER_USB)
            {
                gas_gauge.fast_charge_step = 1;
            }
            else
            {
                gas_gauge.fast_charge_step = 2;
            }
            */

            /*if((batt_info.fVolt >= (config_data.charge_cv_voltage -50))&&(batt_info.fCurr >= DISCH_CURRENT_TH) &&
                (batt_info.fCurr <  config_data.charge_end_current)&& (!charge_end_flag))
            {
                charge_times++;
                //you must read 2times
                if(charge_times > 3)
                {
                    charger_finish	 = 1;
                    charge_times = 0;
                    printk("enter exteral charger finish \n");
                }
            }*/

            /*if ((batt_info.fCurr < DISCH_CURRENT_TH) || (batt_info.fCurr > config_data.charge_end_current))
            {
                charge_times = 0;
                charger_finish	 = 0;
            }*/

            if (((batt_info.fRSOC >= 99) && (batt_info.fRSOC < 100)) && (chgendfun_start_record_flag == 0))
            {
                chgendfun_time_start = gas_gauge.dt_time_now;
                chgendfun_start_record_flag = 1;
                //printk("start_record: %d,%d\n",start_record_flag,time_start);
            }
            if ((batt_info.fRSOC < 99))
            {
                chgendfun_start_record_flag = 0;
            }

            if (chgendfun_start_record_flag != 0)
            {
                //if ((gas_gauge.time_now - chgendfun_time_start) > (60 * MAX_TIMETOFULL))
                ts_diff = chgendfun_time_start - gas_gauge.dt_time_now;
                if(ts_diff.TotalSeconds > (60 * MAX_TIMETOFULL))
                {
                    charger_finish = 1;
                    charge_times = 0;
                    chgendfun_start_record_flag = 0;
                    bmu_printk("enter charge timer finish\n");
                }
            }

            if (charger_finish != 0)
            {
                if (charge_end_flag == 0)
                {
                    if (batt_info.fRSOC < 99)
                    {
                        if (batt_info.fRSOC <= rsoc_pre_in)
                            batt_info.fRSOC++;

                        if (batt_info.fRSOC > 100)
                            batt_info.fRSOC = 100;

                        batt_info.sCaMAH = (int)(batt_info.fRSOC * gas_gauge.fcc_data / 100 + 0.5);
                        batt_info.sCaMAH += gas_gauge.fcc_data / 100 - 1;
                    }
                    else
                    {
                        bmu_printk("enter charger charge end\n");
                        charge_end_flag = 1;
                        gas_gauge.charge_end = 1;
                        charge_end_process();
                        charger_finish = 0;
                    }
                }
                else
                    charger_finish = 0;
            }
        }

		#endregion


	}
}
