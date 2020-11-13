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
	public class OZ88105GasGauge : GasGaugeInterface
	{
		#region static variable

		//use in wait_ocv_flag_fun()
		private static byte waitocvflag_times = 0;
		//use in bmu_wait_ready()
		private static byte waitready_times = 0;
		private static byte waitready_retry_times = 0;
		private static byte waitready_calculate_times = 0;
		private static UInt32 charge_tick = 0;
		private static long previous_loop_timex = 0;
		private static int car_error = 0;
		private static byte check_board_offset_i = 0;
		private static byte polling_error_times = 0;
		private static UInt32 wakeup_charge_tick = 0;
		//private static timex 

		#endregion

		#region private enum

		private enum OZ88105Register : byte
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
			public Int32 board_offset;       //move from config_data
			public float fconnect_resist;		//ri, record resistor value between battery to chip
			public float finternal_resist;		//bi, record resistor value in battery
			public Int32 charge_soc_time_ratio;	//= 220;			//the current threshold of End of Charged
			public Int32 suspend_current;		//= 100;			//mA, suspend mode board consumption current
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
			public Int32 m_volt_1st;
			public Int32 m_volt_2nd;
			public Int32 m_volt_pre;
			public Int32 m_volt_avg_long;//avg voltage of very long average(0.98 and 0.02 weighted)
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
			public byte volt_num_i;
		}

		struct voltage_gasgauge_t
		{
			public Int32 m_fCurrent_step;
			public Int32 m_fCurrent;
			public Int32 m_fCoulombCount;
			public Int32 m_fMaxErrorSoc;
			public Int32 m_fStateOfCharge;
			public Int32 m_fRsoc;
			public Int32 m_fResCap;
			public Int32 m_fFCC;
			public Int32		m_iSuspendTime;		//UTC time in second when suspend involked
			public DateTime m_dt_suspend;
			public byte		m_cPreState;		//pre-suspend state, 1:charge,0:idle,-1:discharge
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
		private string VERSION = "2015.04.28/4.02.09";
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
        //private Parameter ParamOZ88105Status = null;
		private Parameter ParamOZ88105Regx03Bit7 = null;
		private Parameter ParamOZ88105Regx04Bit0 = null;
		private Parameter ParamOZ88105Regx08Bit0 = null;
		private Parameter ParamOZ88105CtrlBI = null;
		private Parameter ParamOZ88105Ctrlchgsel = null;
		private Parameter ParamOZ88105Ctrlvme = null;
		private Parameter ParamOZ88105CtrlChgActive = null;
		private Parameter ParamOZ88105CtrlSlpOCVEn = null;
		private Parameter ParamOZ88105CtrlSleepMode = null;
		private Parameter ParamOZ88105CtrlSWReset = null;
		private Parameter ParamOZ88105OCV = null;
		private Parameter ParamOZ88105PoOCV = null;
		private Parameter ParamOZ88105SleepOCV = null;
		private Parameter ParamOZ88105BoardOffset = null;
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
		private int VOLTGG_CMP_LOOP = 30;
		private int VOLTGG_MAX_ERR = 300;

		private bmu_data_t batt_info;
		private gas_gauge_t gas_gauge;
		private parameter_data_t parameter_customer;
		private voltage_gasgauge_t voltage_gasgauge;
		private List<one_latitude_data_t	> charge_volt_data = new List<one_latitude_data_t>();

		private byte bmu_init_ok = 0;
		private byte OZ88105_pec_check = 0;
		private byte OZ88105_cell_num = 1;
		private Int32 res_divider_ratio = 1000;
        private byte charger_finish = 0;
        private byte charge_end_flag = 0;
        private byte charge_times = 0;

		private byte wait_dc_charger = 0;
		private byte wait_voltage_end = 0;

		private byte wait_ocv_flag = 0;
		private byte wait_ocv_times = 0;//= 2; by francis, no need to wait ocv time
		//byte  adapter_in = 0;
		private byte adapter_in_pre = 2;

		private Int32 o2_temp_delta;

		private UInt32 o2_suspend_jiffies;
		private UInt32 o2_resume_jiffies;
		private byte OZ88105_in_suspend = 0;

		private byte start_chg_count_flag = 0;
		private byte battery_ri = 40;

		private byte discharge_end = 0;
		private byte charge_end = 0;
		private byte charge_fcc_update = 0;
		private byte discharge_fcc_update = 0;

		//private parameter_data_t parameter;
		private byte power_on_flag = 0;
		private byte write_offset = 0;
		private byte check_offset_flag = 0;

		private byte bmu_sleep_flag = 0;
		private float fRSOC_PRE;
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

		public OZ88105GasGauge()
		{
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
				myProject = new GasGaugeProject(projtable);
				//sRam[(int)prShortIndex.iDBGCode] = 0x72;
				bInit = myProject.InitializeProject(ref uGGErrorcode, true);
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
				bmu_polling_loop();
				sbd_udpate_sbs();		//copy result to SBS parameter
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
			//system_charge_discharge_status();
			//fRSOC_PRE = batt_info.fRSOC;
			gas_gauge.charge_max_ratio = 1200;		//default 
			//CopyPhysicalToRam();
			bmu_polling_loop();
			//set up in system_charge_discharge_status(), get from VBusOK bit
			//if (adapter_status != 0)//CHARGER_BATTERY)
			//{
				//charge_end_fun();
			//}
			//else
			//{
				//discharge_end_fun();
			//}
			//if ((adapter_status == 0) ||
				//(batt_info.fCurr < DISCH_CURRENT_TH) ||
				//(batt_info.fCurr > myProject.dbChgEndCurr)) //config_data.charge_end_current))
			//{
				//charge_times = 0;
				//charger_finish = 0;
			//}
			//if (adapter_status != 0)
			//{
				//if ((batt_info.fRSOC == 99) && ((batt_info.fCurr > myProject.dbChgEndCurr) || //config_data.charge_end_current) ||
						//(batt_info.fVolt < (myProject.dbChgCVVolt - 50)))) //(config_data.charge_cv_voltage -50))))
				//{
					//if (batt_info.sCaMAH > (992 * gas_gauge.fcc_data / 1000 ))
						//batt_info.sCaMAH = 992 * gas_gauge.fcc_data / 1000;
				//}
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
					case (UInt16)OZ88105Register.RegCellVolt:
						{
							ParamPhyVoltage = pmGGP;
							break;
						}
					case (UInt16)OZ88105Register.RegCellCurr:
						{
							ParamPhyCurrent = pmGGP;
							break;
						}
					case (UInt16)OZ88105Register.RegCellTemp:
						{
							ParamPhyTemperature = pmGGP;
							break;
						}
					case (UInt16)OZ88105Register.RegCellCAR:
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
					case (UInt16)OZ88105Register.RegTrimRsv:
						{
							foreach (KeyValuePair<string, Reg> tmpreg in pmGGS.reglist)
							{
								if (tmpreg.Key.Equals("Low"))
								{
									if (tmpreg.Value.startbit == 7)
									{
										ParamOZ88105Regx03Bit7 = pmGGS;
									}
								}
							}
							break;
						}
					case (UInt16)OZ88105Register.RegFreeze:
						{
							foreach (KeyValuePair<string, Reg> tmpreg in pmGGS.reglist)
							{
								if (tmpreg.Key.Equals("Low"))
								{
									if (tmpreg.Value.startbit == 0)
									{
										ParamOZ88105Regx04Bit0 = pmGGS;
									}
								}
							}
							break;
						}
					case (UInt16)OZ88105Register.RegLDOTrim:
						{
							foreach (KeyValuePair<string, Reg> tmpreg in pmGGS.reglist)
							{
								if (tmpreg.Key.Equals("Low"))
								{
									if (tmpreg.Value.startbit == 0)
									{
										ParamOZ88105Regx08Bit0 = pmGGS;
									}
								}
							}
							break;
						}
					case (UInt16)OZ88105Register.RegStatus:
						{
							//ParamOZ88105Status = pmGGS;
							foreach (KeyValuePair<string, Reg> tmpreg in pmGGS.reglist)
							{
								if (tmpreg.Key.Equals("Low"))
								{
									if (tmpreg.Value.startbit == 0)
									{
										ParamOZ88105CtrlBI = pmGGS;
									}
									else if (tmpreg.Value.startbit == 1)
									{
										ParamOZ88105Ctrlchgsel = pmGGS;
									}
									else if (tmpreg.Value.startbit == 2)
									{
										ParamOZ88105Ctrlvme = pmGGS;
									}
									else if (tmpreg.Value.startbit == 4)
									{
										ParamOZ88105CtrlChgActive = pmGGS;
									}
									else if (tmpreg.Value.startbit == 5)
									{
										ParamOZ88105CtrlSlpOCVEn = pmGGS;
									}
									else if (tmpreg.Value.startbit == 6)
									{
										ParamOZ88105CtrlSleepMode = pmGGS;
									}
									else if (tmpreg.Value.startbit == 7)
									{
										ParamOZ88105CtrlSWReset = pmGGS;
									}
								}
							}
							break;
						}
					case (UInt16)OZ88105Register.RegCellOCV:
						{
							foreach (KeyValuePair<string, Reg> tmpreg in pmGGS.reglist)
							{
								if (tmpreg.Key.Equals("Low"))
								{
									if (tmpreg.Value.startbit == 0)
									{
										ParamOZ88105PoOCV = pmGGS;
									}
									else if (tmpreg.Value.startbit == 1)
									{
										ParamOZ88105SleepOCV = pmGGS;
									}
									else if (tmpreg.Value.startbit == 4)
									{
										ParamOZ88105OCV = pmGGS;
									}
								}
							}
							break;
						}
					case (UInt16)OZ88105Register.RegBoardOffset:
						{
							ParamOZ88105BoardOffset = pmGGS;
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

			if (ParamOZ88105OCV != null)
			{
				batt_info.fOCVVolt = (float)ParamOZ88105OCV.phydata;
			}
			else
			{
				//fRam[(int)prFloatIndex.iOCVolt] = 0;
				batt_info.fOCVVolt = 3000;
				uGGErrorcode = LibErrorCode.IDS_ERR_SBSSFL_GGDRV_NOOCVOLTAGE;
				bReturn = false;
			}
			if ((ParamOZ88105PoOCV != null) && (ParamOZ88105SleepOCV != null))
			{
				if (ParamOZ88105PoOCV.phydata != 0)
				{
					//sRam[(int)prShortIndex.iOCVbit] = 1;
					batt_info.bPoOCV = true;
					batt_info.bSleepOCV = false;
				}
				else if (ParamOZ88105SleepOCV.phydata != 0)
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
				if (ParamOZ88105PoOCV == null)
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

			//if (ParamOZ88105Status != null)
			if ((ParamOZ88105CtrlBI != null) && (ParamOZ88105Ctrlchgsel !=null) && 
				(ParamOZ88105Ctrlvme != null) && (ParamOZ88105CtrlChgActive != null) &&
				(ParamOZ88105CtrlSlpOCVEn != null) && (ParamOZ88105CtrlSleepMode != null) && 
				(ParamOZ88105CtrlSWReset != null))
			{
				//sRam[(int)prShortIndex.iStatus] = (short)ParamOZ88105Status.phydata;
				yData = (byte)ParamOZ88105CtrlBI.phydata;
				yData |= (byte)((int)ParamOZ88105Ctrlchgsel.phydata << 1);
				yData |= (byte)((int)ParamOZ88105Ctrlvme.phydata << 2);
				yData |= (byte)((int)ParamOZ88105CtrlChgActive.phydata << 4);
				yData |= (byte)((int)ParamOZ88105CtrlSlpOCVEn.phydata << 5);
				yData |= (byte)((int)ParamOZ88105CtrlSleepMode.phydata << 6);
				yData |= (byte)((int)ParamOZ88105CtrlSWReset.phydata << 7);
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

			if ((ParamOZ88105CtrlBI != null) && (ParamOZ88105Ctrlchgsel != null) &&
				(ParamOZ88105Ctrlvme != null) && (ParamOZ88105CtrlChgActive != null) &&
				(ParamOZ88105CtrlSlpOCVEn != null) && (ParamOZ88105CtrlSleepMode != null) &&
				(ParamOZ88105CtrlSWReset != null))
			{
				//sRam[(int)prShortIndex.iStatus] = (short)ParamOZ88105Status.phydata;
				//ParamOZ88105CtrlBI.phydata = (float)(yData & 0x01);		//it is readonly, should not try to modify it
				//ParamOZ88105Ctrlchgsel.phydata = (float)((yData & 0x02) >> 1);	//it's readonly
				ParamOZ88105Ctrlvme.phydata = (float)((yData & 0x0C) >> 2);
				//ParamOZ88105CtrlChgActive.phydata = (float)((yData & 0x10) >> 4);	//it's readonly
				ParamOZ88105CtrlSlpOCVEn.phydata = (float)((yData & 0x20) >> 5);
				ParamOZ88105CtrlSleepMode.phydata = (float)((yData & 0x40) >> 6);
				ParamOZ88105CtrlSWReset.phydata = (float)((yData & 0x80) >> 7);
				//ParamOZ88105CtrlBI.errorcode = LibErrorCode.IDS_ERR_SBSSFL_REQUIREWRITE;		//readonly
				//ParamOZ88105Ctrlchgsel.errorcode = LibErrorCode.IDS_ERR_SBSSFL_REQUIREWRITE;		//readonly
				ParamOZ88105Ctrlvme.errorcode = LibErrorCode.IDS_ERR_SBSSFL_REQUIREWRITE;
				//ParamOZ88105CtrlChgActive.errorcode = LibErrorCode.IDS_ERR_SBSSFL_REQUIREWRITE;	//readonly
				ParamOZ88105CtrlSlpOCVEn.errorcode = LibErrorCode.IDS_ERR_SBSSFL_REQUIREWRITE;
				ParamOZ88105CtrlSleepMode.errorcode = LibErrorCode.IDS_ERR_SBSSFL_REQUIREWRITE;
				ParamOZ88105CtrlSWReset.errorcode = LibErrorCode.IDS_ERR_SBSSFL_REQUIREWRITE;
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
			gas_gauge.charge_end_current_th2 = 350;// (int)(myProject.dbChgEndCurr + 2);
			gas_gauge.charge_strategy = 1;
			gas_gauge.charge_max_ratio = 1200;
			//gas_gauge.charge_max_ratio = 3000;  // for lianxiang
			gas_gauge.discharge_end = 0;
			//gas_gauge.discharge_current_th = DISCH_CURRENT_TH;
			//gas_gauge.discharge_strategy = 1;
			//gas_gauge.discharge_max_ratio = 150000;  // for lianxiang
			//gas_gauge.discharge_max_ratio = config_data.design_capacity * 1000 / (100 * 2);
			//gas_gauge.discharge_max_ratio = 8000;  // for lianxiang
			//gas_gauge.dsg_end_voltage_hi = 50;

			//gas_gauge.batt_ri = 120;
			//gas_gauge.line_impedance = 0;
			//gas_gauge.max_chg_reserve_percentage = 1000;
			//gas_gauge.fix_chg_reserve_percentage = 0;
			//gas_gauge.fast_charge_step = 2;
			gas_gauge.start_fast_charge_ratio = 1500;
			//gas_gauge.charge_method_select = 0;
			//gas_gauge.max_charge_current_fix = 2000;
			gas_gauge.dsg_end_voltage_hi = SHUTDOWN_HI;
			if (parameter_customer.debug)
				gas_gauge.dsg_end_voltage_hi = 0;
			gas_gauge.dsg_end_voltage_th1 = SHUTDOWN_TH1;
			gas_gauge.dsg_end_voltage_th2 = SHUTDOWN_TH2;
			gas_gauge.dsg_count_2 = 0;
			bmu_init_gg();

			//--------------------------------------------------------------------------------------------------
			//copy memory for table content, skip
			//memcpy(((uint8_t*)kernel_memaddr + byte_num), (uint8_t*)charge_data, 4 * gas_gauge.charge_table_num * 2);

			//bmu_printk(string.Format("byte_num is {0}", byte_num));

			power_on_flag = 0;
			bmu_sleep_flag = 0;
			batt_info.i2c_error_times = 0;

			bmu_printk(string.Format("AAAA OZ88105 DRIVER VERSION is {0}", VERSION));

			//(D150828)Francis, delete it cause OZ88105 has no current
			//note that dbRsense saves in Ohm format, not mOhm format
			//gas_gauge.overflow_data = (Int32)((32768 * 5) / (myProject.dbRsense * 1000));

			//if (parameter_customer.debug)
			//{
				//bmu_printk(string.Format("yyyy gas_gauge.overflow_data is {0}", gas_gauge.overflow_data));
			//}

			bmu_printk(string.Format("OZ88105 test parameter  {0:F2},{1:F2},{2:F2},{3:F2},{4:F2},{5:F2},{6:F2},{7:F2},{8:F2},{9:F2},{10:d},{11:d},",
				(myProject.dbRsense * 1000),
				myProject.dbPullupR,
				myProject.dbPullupV,
				2.5F,//myProject.dbCARLSB,
				3.9F,//myProject.dbCurrLSB,
				2.5F,//myProject.fVoltLSB,
				myProject.dbDesignCp,
				myProject.dbChgCVVolt,
				myProject.dbChgEndCurr,
				myProject.dbDsgEndVolt,
				parameter_customer.board_offset,//myProject.board_offset,
				parameter_customer.debug)
			);

			voltage_gasgauge.m_fCurrent_step = 0;
			voltage_gasgauge.m_fCurrent = 0;
			voltage_gasgauge.m_fCoulombCount = 0;
			voltage_gasgauge.m_fMaxErrorSoc = 0;
			voltage_gasgauge.m_fStateOfCharge = 0;
			voltage_gasgauge.m_fRsoc = 0;
			voltage_gasgauge.m_fResCap = 0;
			voltage_gasgauge.m_fCurrent = 0;
			voltage_gasgauge.m_iSuspendTime = 0;
			voltage_gasgauge.m_dt_suspend = DateTime.Now;
			voltage_gasgauge.m_fFCC = (int)myProject.dbDesignCp;
			voltage_gasgauge.m_cPreState = 0;

			//check_OZ88105_staus();     //check OZ88105 i2c address, we don't need it, done by DEM+BusOption
			//check_pec_control();

			//OZ88105_create_sys();

			//wake up OZ88105 into FullPower mode
			//ret = afe_register_read_byte(OZ88105_OP_CTRL, &i);
			//if (ParamOZ88105Status != null)
			//i = (int)ParamOZ88105Status.phydata;
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


			bmu_printk(string.Format("OZ88105_cell_num  is  {0:d}", OZ88105_cell_num));

			if (OZ88105_cell_num > 1)
				//afe_register_write_byte(OZ88105_OP_CTRL, num_0x2c);
				i = 0x2C;
			else
				//afe_register_write_byte(OZ88105_OP_CTRL, num_0x20);
				i = 0x20;
			//WriteRegFunction(ParamOZ88105Status, i);
			SetCtrlValue((byte)i);

			bmu_printk(string.Format("OZ88105 test parameter  {0:F2},{1:F2},{2:F2},{3:F2},{4:F2},{5:F2},{6:F2},{7:F2},{8:F2},{9:F2},{10:F2},{11:F2},",
				(myProject.dbRsense * 1000),
				myProject.dbPullupR,
				myProject.dbPullupV,
				2.5F,//myProject.dbCARLSB,
				3.9F,//myProject.dbCurrLSB,
				2.5F,//myProject.fVoltLSB,
				myProject.dbDesignCp,
				myProject.dbChgCVVolt,
				myProject.dbChgEndCurr,
				myProject.dbDsgEndVolt,
				0F,//myProject.board_offset,
				parameter_customer.debug)
			);

			trim_bmu_VD23();

			//read data
			//afe_read_cell_volt(&batt_info->fVolt);
			//afe_read_current(&batt_info->fCurr);
			CopyPhysicalToRam();
			GetOCVValue();
			batt_info.fCurr = -1 * voltage_gasgauge.m_fCurrent;
			//afe_read_cell_temp(&batt_info->fCellTemp);
			batt_info.chg_on = check_charger_status();

			volt_gg_state();

			//read OCV value
			//for(i = 0;i < 3;i++)
			//{
				//ret = afe_register_read_word(OZ88105_OP_OCV_LOW,&value);
				//if(ret >= 0)break;
			//}

			bmu_printk(string.Format("read batt_info.fVolt is {0:F2}",(batt_info.fVolt * OZ88105_cell_num)));
			bmu_printk(string.Format("read ocv flag ret is PoOCV = {0:d}, SleepOCV = {1:d}",(int)ParamOZ88105OCV.phydata, (int)ParamOZ88105SleepOCV.phydata));

			//if (ret >= num_0 )		//if communicate successfully
			//{
			// oz88105 First power on 
			//if (value & POWER_OCV_FLAG)
			if(ParamOZ88105PoOCV.phydata != 0)	//PoOCV flag
			{
				power_on_flag = 1;
				//msleep(2000);
				Thread.Sleep(2000);
				//afe_read_ocv_volt(&batt_info->fOCVVolt);
				//afe_read_cell_volt(&batt_info->fVolt);

				volt_gg_state();		//check system state if possible
			
				if(OZ88105_cell_num > 1)
					batt_info.fOCVVolt = batt_info.fVolt;
			
				bmu_printk(string.Format("AAAA ocv volt is {0:F2}",(batt_info.fOCVVolt * OZ88105_cell_num)));
				bmu_printk(string.Format("AAAA volt is {0:F2}",(batt_info.fVolt * OZ88105_cell_num)));
			
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
				batt_info.fCurr = voltage_gasgauge.m_fCurrent;
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
				batt_info.fCurr = (-1 * voltage_gasgauge.m_fCurrent);
			
				//start add for voltage_gasgauge
				//voltage_gasgauge.m_fStateOfCharge = (long)batt_info.fRSOC;
				//voltage_gasgauge.m_fRsoc = (long)batt_info.fRSOC;	
				//ADD_1% FIX
				voltage_gasgauge.m_fStateOfCharge = (int)(batt_info.fRSOC + 1);
				voltage_gasgauge.m_fRsoc = (int)(batt_info.fRSOC + 1);	
				voltage_gasgauge.m_fCoulombCount = (int)(voltage_gasgauge.m_fStateOfCharge * myProject.dbDesignCp + 0.5); //config_data.design_capacity;
				//end add for voltage_gasgauge			

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
					bmu_printk(string.Format("AAAA batt_info.fVolt is {0:F2}",(batt_info.fVolt * OZ88105_cell_num)));
					bmu_printk(string.Format("AAAA batt_info.fRSOC is {0:F2}",batt_info.fRSOC));
					bmu_printk(string.Format("AAAA batt_info.sCaMAH is {0:d}",batt_info.sCaMAH));
					bmu_printk(string.Format("AAAA batt_info.fRC is {0:F2}",batt_info.fRC));
					bmu_printk(string.Format("AAAA batt_info.fCurr is {0:F2}",batt_info.fCurr));
					bmu_printk("----------------------------------------------------\n");
				}
			}	//if(ParamOZ88105PoOCV.phydata != 0)	//PoOCV flag
			//else if(value & SLEEP_OCV_FLAG)
			else if(ParamOZ88105SleepOCV.phydata != 0)	//SleepOCV flag
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
				if (OZ88105_cell_num > 1)
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
					voltage_gasgauge.m_fStateOfCharge = (int)(batt_info.fRSOC + 1);
					voltage_gasgauge.m_fRsoc = (int)(batt_info.fRSOC + 1);
					voltage_gasgauge.m_fCoulombCount = (int)(voltage_gasgauge.m_fStateOfCharge * myProject.dbDesignCp + 0.5); //config_data.design_capacity;
					gas_gauge.fcc_data = (int)(myProject.dbDesignCp + 0.5);
					gas_gauge.sCtMAH = batt_info.sCaMAH;
					gas_gauge.discharge_sCtMAH = (int)(myProject.dbDesignCp - batt_info.sCaMAH + 0.5);//config_data.design_capacity - batt_info.sCaMAH; 
				}
				else
				//(E150831)
				bmu_printk("Normal mode is activated.");
			}
			/*	communicate failed situation, won't be happened
			else
			{
				bmu_printk("AAAA COBRA oz88105 DRIVER Big Error\n");
				printk("AAAA COBRA oz88105 can't read OZ88105_OP_OCV_LOW\n");


				batt_info->fOCVVolt = batt_info->fVolt;
				afe_read_cell_volt(&batt_info->fVolt);
				volt_gg_state();	//check system state if possible
				printk("AAAA batt_info.chg_dsg_flag is %d\n",batt_info->chg_dsg_flag);
				if (batt_info->chg_dsg_flag == CHARGE_STATE)
					data = batt_info->fOCVVolt - num_50;
				else
					data = batt_info->fOCVVolt + num_50;
			
				batt_info->fRSOC = one_latitude_table(parameter->ocv_data_num,parameter->ocv,data);
				printk("find table batt_info.fRSOC is %d\n",batt_info->fRSOC); 
		
				if((batt_info->fRSOC >num_100) || (batt_info->fRSOC < num_0))
					batt_info->fRSOC = num_50;
				//batt_info->fRC = batt_info->fRSOC * config_data->design_capacity / num_100;
				//ADD_1% FIX
				batt_info->fRC = (batt_info->fRSOC + 1) * config_data->design_capacity / num_100;
				//start add for voltage_gasgauge
				//voltage_gasgauge->m_fStateOfCharge = (long)batt_info->fRSOC;
				//voltage_gasgauge->m_fRsoc = (long)batt_info->fRSOC;	
				//ADD_1% FIX
				voltage_gasgauge->m_fStateOfCharge = (long)(batt_info->fRSOC + 1);
				voltage_gasgauge->m_fRsoc = (long)(batt_info->fRSOC + 1);
				voltage_gasgauge->m_fCoulombCount = voltage_gasgauge->m_fStateOfCharge * config_data->design_capacity;
				//end add for voltage_gasgauge			
			}
			*/
			//afe_register_read_byte(num_0,&i);
			//printk("regeidter 0x00 is %x\n",i);

			//afe_register_read_byte(num_9,&i);
			GetCtrlValue(ref i);
			bmu_printk(string.Format("register 0x09 is 0x{0:X2}", i));
		}

		private void wait_ocv_flag_fun()
		{
			//Int32 ret;
			//UInt16 value;	
			//byte i;
			//Int32 data;
			float fdata;

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
			GetOCVValue();
			bmu_printk(string.Format("read batt_info.fVolt is {0:F2}", (batt_info.fVolt * OZ88105_cell_num)));
			bmu_printk(string.Format("read batt_info.fRC is {0:F2}", batt_info.fRC));
			bmu_printk(string.Format("read batt_info.fCurr is {0:F2}", batt_info.fCurr));
			bmu_printk(string.Format("read Poocv flag, value is {0:d}", batt_info.bPoOCV));//sRam[(int)prShortIndex.iOCVbit]));
			bmu_printk(string.Format("read sleep ocv flag, value is {0:d}", batt_info.bSleepOCV));
			bmu_printk(string.Format("read fCellTemp is {0:F2}", batt_info.fCellTemp));
			//if (sRam[(int)prShortIndex.iOCVbit] == 1)
			if (batt_info.bPoOCV)
			{
				power_on_flag = 1;
				//TBD: here in Android is to request send another read command
				//afe_read_ocv_volt(&batt_info->fOCVVolt);
				//afe_read_cell_volt(&batt_info->fVolt);
				CopyPhysicalToRam();

				if (OZ88105_cell_num > 1)
				{
					batt_info.fOCVVolt = batt_info.fVolt;
				}

				//if (batt_info->fOCVVolt > (config_data->charge_cv_voltage + 70))
				if (batt_info.fOCVVolt > (myProject.dbChgCVVolt + 70))
				{
					//msleep(num_1000);
					//TBD: request voltage read again, to get cell voltage then assign to OCV voltage
					//afe_read_cell_volt(&batt_info->fVolt);
					batt_info.fOCVVolt = batt_info.fVolt;
					//printk("AAAAA ocv data errror ,so batt_info->fVolt is {0:d}",batt_info->fVolt);
				}

				//read again
				//afe_read_current(&batt_info->fCurr);
				bmu_printk(string.Format("AAAA batt_info.fCurr is {0:F2}", batt_info.fCurr));



				if (batt_info.fCurr > 50F)
					fdata = batt_info.fOCVVolt - 100;
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

				//batt_info->fRC = batt_info->fRSOC * config_data->design_capacity / num_100;
				batt_info.fRC = batt_info.fRSOC * 0.01F * myProject.dbDesignCp;

				//read current again
				//afe_read_current(&batt_info->fCurr);

				if (batt_info.fRC >= (gas_gauge.overflow_data - 10))
				{
					batt_info.fRC = gas_gauge.overflow_data - gas_gauge.overflow_data * 0.01F;
					batt_info.fRCPrev = batt_info.fRC;
				}

				//afe_write_car(batt_info->fRC)
				WriteRegFunction(ParamPhyCAR, batt_info.fRC);
				batt_info.fRCPrev = batt_info.fRC;
				gas_gauge.fcc_data = (int)(myProject.dbDesignCp + 0.5F);


				bmu_printk("Power on mode is activated.");


				//batt_info->sCaMAH = batt_info->fRSOC * config_data->design_capacity / num_100;
				//gas_gauge->sCtMAH = batt_info->sCaMAH; 
				//gas_gauge->discharge_sCtMAH = config_data->design_capacity - batt_info->sCaMAH;
				batt_info.sCaMAH = (int)(batt_info.fRSOC * 0.01 * myProject.dbDesignCp + 0.5F);
				gas_gauge.sCtMAH = batt_info.sCaMAH;
				gas_gauge.discharge_sCtMAH = (int)(myProject.dbDesignCp + 0.5F) - batt_info.sCaMAH;
			}
			//else if (value & SLEEP_OCV_FLAG)
			//else if (sRam[(int)prShortIndex.iOCVbit] == 2)
			else if (batt_info.bSleepOCV)
			{
				//afe_read_ocv_volt(&batt_info->fOCVVolt);
				sleep_ocv_flag = 1;
				bmu_printk("Sleep ocv mode is activated.");
			}
			else
			{
				//(A150831)Francis, if CAR is close to LutTSOCbyOCV (Volt), use it as current RSOC
                if (OZ88105_cell_num > 1)
				{
					batt_info.fOCVVolt = batt_info.fVolt;
				}
				if (batt_info.fCurr > 50F)
					fdata = batt_info.fVolt - 100;
				else
					fdata = batt_info.fVolt;
				batt_info.fRSOC = myProject.LutTSOCbyOCV(fdata);		//get RSOC from current voltage
				fdata = (batt_info.fRC * 100) / myProject.dbDesignCp;					//get SOC by CAR divided to DesignCapacity
				if (Math.Abs(fdata - batt_info.fRSOC) < 10)							//if it's nearly close
				{
					power_on_flag = 1;
					wait_ocv_flag = 1;
					if (batt_info.fRSOC > 100) batt_info.fRSOC = 100F;
					if (batt_info.fRSOC < 0) batt_info.fRSOC = 0F;
					batt_info.sCaMAH = (int)(batt_info.fRC+0.5);
				}
				//(E150831)
				bmu_printk("Normal mode is activated.");
			}

			//for ifive
			//afe_read_car(&batt_info->fRC);
			//if ((batt_info->fRC < (config_data->design_capacity / num_100)) && (batt_info->fRC > -3000))
			if ((batt_info.fRC < (myProject.dbDesignCp * 0.01)) && (batt_info.fRC > -3000F))
			{
				if (parameter_customer.debug)
				{
					bmu_printk(string.Format("yyyy fRC will over fRC  is {0:F2}", batt_info.fRC));
				}

				//batt_info->fRC = config_data->design_capacity / num_100 - num_1;
				//afe_write_car(batt_info->fRC);
				//batt_info->fRCPrev = batt_info->fRC;
				batt_info.fRC = myProject.dbDesignCp * 0.01F - 1;
				WriteRegFunction(ParamPhyCAR, batt_info.fRC);
				batt_info.fRCPrev = batt_info.fRC;
			}
			//afe_read_car(&batt_info->fRC);
			bmu_printk(string.Format("fRC  is {0:f}", batt_info.fRC));

			//afe_register_read_byte(num_0, &i);
			//bmu_printk(string.Format("COBRA regeidter 0x00 is %x", i));

			//afe_register_read_byte(num_9, &i);
			//bmu_printk(string.Format("COBRA regeidter 0x09 is %x", i));	

		}

		private void trim_bmu_VD23()
		{
			byte yVal = 0;

			if ((ParamOZ88105Regx03Bit7 != null) && (ParamOZ88105Regx04Bit0 != null))
			{
				yVal = (byte)ParamOZ88105Regx03Bit7.phydata;
				yVal *= 2;
				yVal += (byte)ParamOZ88105Regx04Bit0.phydata;
			}

			WriteRegFunction(ParamOZ88105Regx08Bit0, (float)yVal);
		}

		//it's used in 8806, not in OZ88105
		private void check_shutdown_voltage_8806()
		{
			//Int32 ret;
			//uint8_t i;
			float temp;
			//Int32 infVolt;

			//
			//afe_read_cell_volt(&batt_info->fVolt);
			CopyPhysicalToRam();
			GetOCVValue();

			if (sleep_ocv_flag == 0) return;

			//temp = one_latitude_table(parameter->ocv_data_num, parameter->ocv, batt_info->fOCVVolt);
			temp = myProject.LutTSOCbyOCV(batt_info.fOCVVolt);
			bmu_printk(string.Format("Sleep data is {0:d}", temp));

			// select hilg data 
			if (calculate_soc > temp)
				temp = calculate_soc;

			//Very dangerous
			if ((batt_info.fRSOC - temp) > 20)
			//if(temp < batt_info->fRSOC)
			{
				//batt_info->fRSOC -= 5;
				batt_info.fRSOC = temp;
				if (batt_info.fRSOC < 0) batt_info.fRSOC = 0;

				//batt_info->sCaMAH = batt_info->fRSOC * gas_gauge->fcc_data / 100;
				//batt_info->fRC = batt_info->sCaMAH;
				//Consider float value conversion, swap assign
				batt_info.fRC = batt_info.fRSOC * gas_gauge.fcc_data * 0.01F;
				batt_info.sCaMAH = (int)(batt_info.fRC + 0.5F);

				if (batt_info.fRC > (gas_gauge.overflow_data - 20))
					batt_info.fRC = gas_gauge.overflow_data - 20;

				//write to CAR
				//afe_write_car(batt_info->fRC);
				WriteRegFunction(ParamPhyCAR, batt_info.fRC);

				batt_info.fRCPrev = batt_info.fRC;

				gas_gauge.sCtMAH = batt_info.sCaMAH;
				if (gas_gauge.fcc_data > batt_info.sCaMAH)
					gas_gauge.discharge_sCtMAH = gas_gauge.fcc_data - batt_info.sCaMAH;
				else
					gas_gauge.discharge_sCtMAH = 0;
			}

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
		private void check_OZ88105_staus()
		{
		}

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

				//rc_result = OZ88105_LookUpRCTable(
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
					//rc_result = OZ88105_LookUpRCTable(
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
			//wake up OZ88105 into FullPower mode
			//ret = afe_register_read_byte(OZ88105_OP_CTRL,&i);
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
				if (OZ88105_cell_num > 1)
				{
					//afe_register_write_byte(OZ88105_OP_CTRL,0x2c);
					//WriteRegFunction(ParamOZ88105Status, 0x2c);
					SetCtrlValue(0x2c);
				}
				else
				{
					//afe_register_write_byte(OZ88105_OP_CTRL,0x20);
					//WriteRegFunction(ParamOZ88105Status, 0x20);
					SetCtrlValue(0x20);
				}
				bmu_printk("OZ88105 wake up function");
			}
			//ret = afe_register_read_byte(OZ88105_OP_CTRL,&i);
			//printk("bmu_wait_ready read 0x09 ret is {0:d},i is {1:d}",ret,i);

			//do we need add check ocv flag now?
			//ret = afe_register_read_word(OZ88105_OP_OCV_LOW,&value);
			//if ((ret >= 0) && (value & POWER_OCV_FLAG))	
			//printk("read flag too late\n");	

			//afe_read_cell_temp(&batt_info->fCellTemp);
			//afe_read_cell_volt(&batt_info->fVolt);
			//afe_read_current(&batt_info->fCurr);
			//afe_read_car(&batt_info->fRC);
			////batt_info->fRSOC = num_1;
			CopyPhysicalToRam();

			bmu_printk(string.Format("fVolt is {0:F2}", (batt_info.fVolt * OZ88105_cell_num)));
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
			Int32 data, data_soc;
			Int32 fcc, ocv_test;
			//static uint8_t times = num_0;
			byte i;
			Int32 ret;
			UInt16 value;

			bmu_printk(string.Format("AAAA bmu wait times {0:d}", waitready_times));
			//wake up oz88105 into FullPower mode
			//ret = afe_register_read_byte(OZ88105_OP_CTRL,&i);
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
				if (OZ88105_cell_num > 1)
				{
					//afe_register_write_byte(OZ88105_OP_CTRL,0x2c);
					SetCtrlValue(0x2c);
				}
				else
				{
					//afe_register_write_byte(OZ88105_OP_CTRL, 0x20);
					SetCtrlValue(0x20);
				}
				bmu_printk("OZ88105 wake up function");

			}
			//ret = afe_register_read_byte(OZ88105_OP_CTRL,&i);
			//printk("bmu_wait_ready read 0x09 ret is %d,i is %d \n",ret,i);

			//do we need add check ocv flag now?
			//ret = afe_register_read_word(OZ88105_OP_OCV_LOW,&value);
			//if ((ret >= 0) && (value & POWER_OCV_FLAG))	
				//printk("big error,read ocv flag too late\n");	
	
			//afe_read_cell_temp(&batt_info->fCellTemp);
			//afe_read_cell_volt(&batt_info->fVolt);
			CopyPhysicalToRam();
			//batt_info.chg_on = check_charger_status();
			volt_gg_state();
			batt_info.fCurr = -1 * voltage_gasgauge.m_fCurrent;
			//afe_read_current(&batt_info->fCurr);
			//afe_read_car(&batt_info->fRC);
			//batt_info->fRSOC = num_1;
	
			if(power_on_flag != 0)
			{
				bmu_write_data(BATT_CAPACITY, batt_info.sCaMAH);
				//check filesys working ok, we can skip
				//bmu_read_data(BATT_CAPACITY, ref data);
				//if(data != batt_info.sCaMAH) return;
				bmu_write_data(BATT_FCC,gas_gauge.fcc_data);
				//bmu_read_data(BATT_FCC, ref data);
				//if(data != gas_gauge.fcc_data) return;
				data = 1;
				bmu_write_data(OCV_FLAG,1);
				bmu_read_data(OCV_FLAG, ref data);
				//if(data != 1) return;
				bmu_init_ok = 1;
				gas_gauge.ocv_flag = 1;
				batt_info.fRCPrev = batt_info.fRC;

				bmu_write_data(BATT_SOC, voltage_gasgauge.m_fStateOfCharge);
				//bmu_read_data(BATT_SOC, ref data);
				//if(data != voltage_gasgauge.m_fStateOfCharge) return;

				bmu_write_data(BATT_RSOC,voltage_gasgauge.m_fRsoc);
				//bmu_read_data(BATT_RSOC, ref data);
				//if(data != voltage_gasgauge.m_fRsoc) return;

				bmu_printk("bmu_wait_ready, power on ok.");
			}


			data = 0;
			data_soc = 0;
			fcc = 0;
			bmu_read_data(OCV_FLAG, ref data);
			gas_gauge.ocv_flag = (byte)data;
			bmu_read_data(BATT_RSOC, ref data);
			bmu_read_data(BATT_SOC, ref data_soc);
			bmu_read_data(BATT_FCC, ref fcc);
	
			//JON ADD for RSOC wrong value
			//ocv_test = one_latitude_table(parameter->ocv_data_num,parameter->ocv,batt_info->fVolt);
			ocv_test = (int)myProject.LutTSOCbyOCV((float)batt_info.fVolt);
			//if (ABS(ocv_test, data) > 20)
			if(Math.Abs(ocv_test - data) > 20)
			{
				if (batt_info.chg_dsg_flag != CHARGE_STATE) {
					if (ocv_test > data)
					{
						//force re-do OCV
						data = -1;
					}
				}
			}
	
			if((data<0) || (fcc<=0) || (data_soc<0) || (gas_gauge.ocv_flag<0))
			{	//should not come here, we should not have read failed happened!
				if (waitready_times < 6) waitready_times++;

				if (waitready_times > 0)		// > 5) wait 6 times? modify to 0
				{
					waitready_times = 0;
					bmu_printk("============================================");
					bmu_printk("Can't read battery data from file,use default.");
					bmu_printk(string.Format("EEEE BATT_FCC is {0}", fcc));
					bmu_printk(string.Format("EEEE BATT_RSOC is {0}", data));
					bmu_printk(string.Format("EEEE BATT_SOC is {0}", data_soc));
					bmu_printk(string.Format("EEEE BATT_OCV_FLAG is {0}", gas_gauge.ocv_flag));
					bmu_printk("============================================\n");

					gas_gauge.fcc_data = (int)(myProject.dbDesignCp+ 0.5);//config_data->design_capacity;

					//afe_read_cell_volt(&batt_info->fVolt);
					//batt_info->fRSOC = one_latitude_table(parameter->ocv_data_num,parameter->ocv,batt_info->fVolt);
					batt_info.fRSOC = (int)myProject.LutTSOCbyOCV((float)batt_info.fVolt);
					if((batt_info.fRSOC >100) || (batt_info.fRSOC < 0))
						batt_info.fRSOC = 50;

					//batt_info->fRC = batt_info->fRSOC * gas_gauge->fcc_data / num_100;
					//ADD_1% FIX
					batt_info.fRC = (batt_info.fRSOC + 1) * myProject.dbDesignCp / 100; //gas_gauge->fcc_data / num_100;
					batt_info.fRCPrev = batt_info.fRC;
					batt_info.sCaMAH = (int)(batt_info.fRC+0.5);
					gas_gauge.sCtMAH = batt_info.sCaMAH;
					gas_gauge.discharge_sCtMAH = (int)(myProject.dbDesignCp - batt_info.sCaMAH+0.5);//config_data.design_capacity - batt_info.sCaMAH;
			
					//voltage_gasgauge->m_fStateOfCharge = (long)batt_info->fRSOC;
					//voltage_gasgauge->m_fRsoc = (long)batt_info->fRSOC;	
					//ADD_1% FIX
					voltage_gasgauge.m_fStateOfCharge = (int)(batt_info.fRSOC + 0.5);
					voltage_gasgauge.m_fRsoc = (int)(batt_info.fRSOC + 0.5);
					voltage_gasgauge.m_fCoulombCount = (int)(batt_info.fRC * 100 +0.5);
					voltage_gasgauge.m_fFCC = gas_gauge.fcc_data;
			
					bmu_write_data(BATT_RSOC,voltage_gasgauge.m_fRsoc);
					//bmu_read_data(BATT_RSOC, ref data);
					//if(data != voltage_gasgauge.m_fRsoc) return;

					bmu_write_data(BATT_SOC,voltage_gasgauge.m_fStateOfCharge);
					//bmu_read_data(BATT_SOC, ref data);
					//if(data != voltage_gasgauge.m_fStateOfCharge) return;
			
					bmu_write_data(BATT_CAPACITY,batt_info.sCaMAH);
					//bmu_read_data(BATT_CAPACITY, ref data);
					//if(data != batt_info.sCaMAH) return;
			
					bmu_write_data(BATT_FCC,(int)(myProject.dbDesignCp+0.5));
					//bmu_read_data(BATT_FCC, ref data);
					//if(data != config_data->design_capacity) return;

					bmu_write_data(OCV_FLAG,1);
					//bmu_read_data(OCV_FLAG, ref data);
					//if(data != 1) return;
			
					batt_info.fRSOC = batt_info.sCaMAH   * 100;
					batt_info.fRSOC = batt_info.fRSOC / gas_gauge.fcc_data ;
					bmu_init_ok = 1;
			
					bmu_printk(string.Format("AAAA batt_info.fVolt is {0:F2}",(batt_info.fVolt * OZ88105_cell_num)));
					bmu_printk(string.Format("AAAA batt_info.fRSOC is {0:F2}",batt_info.fRSOC));
					bmu_printk(string.Format("AAAA batt_info.sCaMAH is {0:d}",batt_info.sCaMAH));
					bmu_printk(string.Format("AAAA sCtMAH is {0:d}",gas_gauge.sCtMAH));
					bmu_printk(string.Format("AAAA fcc is {0:d}",gas_gauge.fcc_data));
					bmu_printk(string.Format("AAAA batt_info.fRC is {0:F2}",batt_info.fRC));
					if(batt_info.fRSOC <= 1)
					{
						gas_gauge.sCtMAH = 0;
					}
					if(batt_info.fRSOC <= 0)
					{
						gas_gauge.discharge_end = 1;
						batt_info.fRSOC = 0;
					}	
					if(batt_info.fRSOC >= 100)
					{
						gas_gauge.charge_end = 1;
						batt_info.fRSOC = 100;
						gas_gauge.discharge_sCtMAH = 0;
					}
					fRSOC_PRE = batt_info.fRSOC;
					return;
				}
				else	
					return;
			}

			data = 0;
			bmu_read_data(BATT_SOC, ref data);
			voltage_gasgauge.m_fStateOfCharge = data;
			bmu_read_data(BATT_RSOC, ref data);
			voltage_gasgauge.m_fRsoc = data;
			voltage_gasgauge.m_fCoulombCount = (int)(voltage_gasgauge.m_fStateOfCharge * myProject.dbDesignCp + 0.5);//config_data.design_capacity;

			bmu_read_data(BATT_FCC, ref fcc);
			//if(fcc >= (config_data.design_capacity * FCC_UPPER_LIMIT / 100))
			if (fcc >= (myProject.dbDesignCp * FCC_UPPER_LIMIT / 100))
			{
				//fcc=  config_data->design_capacity  * FCC_UPPER_LIMIT / 100 ;
				bmu_printk(string.Format("EEEE before update is {0:d}, {1:F2}, {2:F2}", fcc, myProject.dbDesignCp, (myProject.dbDesignCp * FCC_UPPER_LIMIT / 100)));
				fcc = (int)(myProject.dbDesignCp+0.5);// config_data->design_capacity;
				bmu_write_data(BATT_FCC,fcc);
				bmu_printk(string.Format("EEEE fcc1 update is {0:d}",fcc));

			}
			if (fcc <= 0)
			{
				bmu_printk(string.Format("EEEE fcc2 update is {0:d}",fcc));
				fcc = (int)(myProject.dbDesignCp * FCC_LOWER_LIMIT / 100 + 0.5);//config_data->design_capacity * FCC_LOWER_LIMIT/ 100;
				bmu_write_data(BATT_FCC,fcc);	
			}
			gas_gauge.fcc_data = fcc;

			bmu_read_data(BATT_CAPACITY, ref data);
			bmu_printk(string.Format("AAAA read battery capacity data is {0:d}",data));
			batt_info.sCaMAH = data;
			data = fcc * voltage_gasgauge.m_fRsoc / 100;
			if((batt_info.sCaMAH <= 0) || (batt_info.sCaMAH >(myProject.dbDesignCp * 3 / 2)) //(config_data->design_capacity *3/2))
				//|| (ABS(data, batt_info->sCaMAH) > 50))
				|| (Math.Abs(data - batt_info.sCaMAH) > 50))
			{
				batt_info.sCaMAH = data;

				batt_info.fRC = data;
				batt_info.fRCPrev = data;
			}
	
			if(batt_info.sCaMAH > (gas_gauge.fcc_data + myProject.dbDesignCp / 100))//config_data->design_capacity / 100))
			{
				bmu_printk(string.Format("EEEE big error sCaMAH is {0:d}",batt_info.sCaMAH));
				batt_info.sCaMAH = (int)(gas_gauge.fcc_data + myProject.dbDesignCp / 100 + 0.5);//config_data->design_capacity / 100;
				bmu_write_data(BATT_CAPACITY,batt_info.sCaMAH);
			}
	
			batt_info.fRSOC = batt_info.sCaMAH   * 100;
			batt_info.fRSOC = batt_info.fRSOC / gas_gauge.fcc_data ;

			batt_info.fRC = batt_info.fRSOC * gas_gauge.fcc_data / 100;
			bmu_printk(string.Format("AAAA batt_info.fVolt is {0:F2}",(batt_info.fVolt * OZ88105_cell_num)));
			bmu_printk(string.Format("AAAA batt_info.fRSOC is {0:F2}",batt_info.fRSOC));
			bmu_printk(string.Format("AAAA batt_info.sCaMAH is {0:d}",batt_info.sCaMAH));
			bmu_printk(string.Format("AAAA gas_gauge.sCtMAH is {0:d}",gas_gauge.sCtMAH));
			bmu_printk(string.Format("AAAA fcc is {0:F2}",gas_gauge.fcc_data));
			bmu_printk(string.Format("AAAA batt_info.fRC is {0:F2}",batt_info.fRC));
		
			batt_info.fRCPrev = batt_info.fRC;	
			bmu_init_ok = 1;

			gas_gauge.sCtMAH = batt_info.sCaMAH;
	
			if(fcc > batt_info.sCaMAH)
				gas_gauge.discharge_sCtMAH = fcc - batt_info.sCaMAH;
			else
				gas_gauge.discharge_sCtMAH = 0;
	
			if(batt_info.fRSOC <= 2)
			{
				gas_gauge.charge_fcc_update = 1;
	
			}
			if(batt_info.fRSOC <= 0)
			{
				gas_gauge.discharge_end = 1;
				batt_info.fRSOC = 0;
				gas_gauge.sCtMAH = 0;
				gas_gauge.ocv_flag = 0;
				bmu_write_data(OCV_FLAG,0);
			}
			if(batt_info.fRSOC >= 100)
			{
				gas_gauge.charge_end = 1;
				batt_info.fRSOC = 100;
				gas_gauge.discharge_sCtMAH = 0;
				gas_gauge.discharge_fcc_update = 1;
				gas_gauge.ocv_flag = 0;
				bmu_write_data(OCV_FLAG,0);
			}

			if(batt_info.fRSOC >= 95)
			{
				gas_gauge.discharge_fcc_update = 1;
			}

			check_shutdown_voltage();
			fRSOC_PRE = batt_info.fRSOC;

			vRSOC_PRE = voltage_gasgauge.m_fRsoc;
			vSOC_PRE  = 	voltage_gasgauge.m_fStateOfCharge;

			if(gas_gauge.ocv_flag == 1)
			{
				charge_fcc_update = 0;
				discharge_fcc_update = 0;
			}
			else
			{
				charge_fcc_update = 1;
				discharge_fcc_update = 1;

			}	
		}

		//no used in OZ88105
		private void wait_ready_file_fail()
		{
			Int32 data = 0;
			//Int32 fcc = 0;
			//Int32 tmp = 0;

			bmu_write_data(BATT_CAPACITY, batt_info.sCaMAH);
			bmu_read_data(BATT_CAPACITY, ref data);
			if (data != batt_info.sCaMAH) return;
			bmu_write_data(BATT_FCC, gas_gauge.fcc_data);
			bmu_read_data(BATT_FCC, ref data);
			if (data != gas_gauge.fcc_data) return;
			bmu_write_data(OCV_FLAG, 1);
			bmu_read_data(OCV_FLAG, ref data);
			if (data != 1) return;
			bmu_init_ok = 1;
			gas_gauge.ocv_flag = 1;
			batt_info.fRCPrev = batt_info.fRC;
			bmu_printk("bmu_wait_ready, power on ok \n");

			//#ifdef FCC_UPDATA_CHARGE
			//bmu_write_data(FCC_UPDATE_FLAG,num_0);
			//#endif
			//wait_ready_code_after_file_fail();
		}

		//no used in OZ88105
		private void wait_ready_code_after_file_fail()
		{
			Int32 data = 0;
			Int32 fcc = 0;
			Int32 tmp = 0;

			//code after file_fail
			bmu_read_data(BATT_CAPACITY, ref data);
			bmu_read_data(BATT_FCC, ref fcc);
			bmu_read_data(OCV_FLAG, ref tmp);
			gas_gauge.ocv_flag = (byte)tmp;   //afraid that without initializaiton of ocv_flag, cannot pass complier

			if ((data < 0) || (fcc < 0) || (gas_gauge.ocv_flag < 0))
			{
				if (waitready_times < 6) waitready_times++;

				if (waitready_times > 5)
				{
					waitready_times = 0;
					bmu_printk("Can't read battery capacity,use default\n");
					bmu_printk("open BATT_CAPACITY fail\n");

					gas_gauge.fcc_data = (int)(myProject.dbDesignCp + 0.5F);//config_data->design_capacity;
					batt_info.fRSOC = calculate_soc;
					if ((batt_info.fRSOC > 100) || (batt_info.fRSOC < 0))
						batt_info.fRSOC = 50;

					batt_info.fRC = batt_info.fRSOC * gas_gauge.fcc_data * 0.01F; // num_100;
					if (batt_info.fRC >= (gas_gauge.overflow_data - 10))
					{
						batt_info.fRC = gas_gauge.overflow_data - gas_gauge.overflow_data * 0.01F;//num_100;
						batt_info.fRCPrev = batt_info.fRC;
					}

					//afe_write_car(batt_info->fRC);
					WriteRegFunction(ParamPhyCAR, batt_info.fRC);
					batt_info.fRCPrev = batt_info.fRC;

					batt_info.sCaMAH = (int)(batt_info.fRC + 0.5F);

					gas_gauge.sCtMAH = batt_info.sCaMAH;
					gas_gauge.discharge_sCtMAH = (int)(myProject.dbDesignCp - batt_info.sCaMAH + 0.5F);// config_data->design_capacity - batt_info->sCaMAH;

					bmu_write_data(BATT_CAPACITY, batt_info.sCaMAH);
					bmu_read_data(BATT_CAPACITY, ref data);
					if (data != batt_info.sCaMAH) return;

					bmu_write_data(BATT_FCC, (int)(myProject.dbDesignCp));//config_data->design_capacity);
					bmu_read_data(BATT_FCC, ref data);
					if (data != (int)myProject.dbDesignCp) return; //config_data->design_capacity) return;

					bmu_write_data(OCV_FLAG, 1);
					bmu_read_data(OCV_FLAG, ref data);
					if (data != 1) return;

					batt_info.fRSOC = batt_info.sCaMAH * 100;
					batt_info.fRSOC = batt_info.fRSOC / gas_gauge.fcc_data;
					bmu_init_ok = 1;


					bmu_printk(string.Format("AAAA batt_info->fVolt is {0:F2}", (batt_info.fVolt * OZ88105_cell_num)));
					bmu_printk(string.Format("AAAA batt_info->fRSOC is {0:F2}", batt_info.fRSOC));
					bmu_printk(string.Format("AAAA batt_info->sCaMAH is {0:d}", batt_info.sCaMAH));
					bmu_printk(string.Format("AAAA sCtMAH is {0:d}", gas_gauge.sCtMAH));
					bmu_printk(string.Format("AAAA fcc is {0:d}", gas_gauge.fcc_data));
					bmu_printk(string.Format("AAAA batt_info->fRC is {0:F2}", batt_info.fRC));
					bmu_printk(string.Format("AAAA batt_info->fCurr is {0:F2}", batt_info.fCurr));
					if (batt_info.fRSOC <= 1)
					{
						//gas_gauge->charge_fcc_update = num_1;
						gas_gauge.sCtMAH = 0;
					}
					if (batt_info.fRSOC <= 0)
					{
						gas_gauge.discharge_end = 1;
						batt_info.fRSOC = 0;
					}
					if (batt_info.fRSOC >= 100)
					{
						gas_gauge.charge_end = 1;
						batt_info.fRSOC = 100;
						gas_gauge.discharge_sCtMAH = 0;
					}
					fRSOC_PRE = batt_info.fRSOC;
					return;
				}
				else
					return;
			}

			bmu_read_data(BATT_CAPACITY, ref data);

			bmu_printk("open BATT_CAPACITY success");
			bmu_printk(string.Format("AAAA read battery capacity data is {0:d}", data));
			batt_info.sCaMAH = data;

			if ((batt_info.sCaMAH <= 0) || (batt_info.sCaMAH > (myProject.dbDesignCp * 3 / 2)))//(config_data->design_capacity * 3 / 2)))
			{
				bmu_printk(string.Format("calculate_soc is {0:F2}", calculate_soc));
				CopyPhysicalToRam();
				//afe_read_cell_volt(&batt_info->fVolt);
				//batt_info->fRSOC = one_latitude_table(parameter->ocv_data_num,parameter->ocv,batt_info->fVolt);
				batt_info.fRSOC = calculate_soc;
				if ((batt_info.fRSOC > 100) || (batt_info.fRSOC < 0))
					batt_info.fRSOC = 50;


				bmu_printk(string.Format("batt_info->fRSOC is {0:F2}", batt_info.fRSOC));
				batt_info.fRC = batt_info.fRSOC * myProject.dbDesignCp * 0.01F; //config_data->design_capacity / num_100;
				bmu_printk(string.Format("batt_info->fRC is {0:F2}", batt_info.fRC));
				if (batt_info.fRC >= (gas_gauge.overflow_data - 10))
				{
					batt_info.fRC = gas_gauge.overflow_data - gas_gauge.overflow_data * 0.01F; // num_100;
					batt_info.fRCPrev = batt_info.fRC;
				}

				bmu_printk(string.Format("batt_info->fRC is {0:F2}", batt_info.fRC));

				//write to 
				//afe_write_car(batt_info->fRC);
				WriteRegFunction(ParamPhyCAR, batt_info.fRC);
				batt_info.fRCPrev = batt_info.fRC;

				batt_info.sCaMAH = (int)(batt_info.fRC + 0.5F);
			}
			bmu_printk(string.Format("batt_info->sCaMAH is {0:d}", batt_info.sCaMAH));
			bmu_read_data(BATT_FCC, ref fcc);

			if (fcc > (myProject.dbDesignCp * FCC_UPPER_LIMIT * 0.01F))//(config_data->design_capacity * FCC_UPPER_LIMIT / 100))
			{
				fcc = (int)(myProject.dbDesignCp * FCC_UPPER_LIMIT * 0.01F + 0.5F);//config_data->design_capacity * FCC_UPPER_LIMIT / 100;
				bmu_write_data(BATT_FCC, fcc);

				bmu_printk(string.Format("fcc1 update is {0:d}", fcc));

			}
			if ((fcc <= 0) || (fcc < (myProject.dbDesignCp * FCC_LOWER_LIMIT * 0.01F)))//(fcc < (config_data->design_capacity * FCC_LOWER_LIMIT / 100)))
			{
				fcc = (int)(myProject.dbDesignCp * FCC_LOWER_LIMIT * 0.01F);//config_data->design_capacity * FCC_LOWER_LIMIT / 100;
				bmu_write_data(BATT_FCC, fcc);

				bmu_printk(string.Format("fcc2 update is {0:d}", fcc));

			}
			gas_gauge.fcc_data = fcc;

			if (batt_info.sCaMAH > (gas_gauge.fcc_data + gas_gauge.fcc_data * 0.01F - 1))
			{
				bmu_printk(string.Format("big error sCaMAH is {0:d}", batt_info.sCaMAH));
				batt_info.sCaMAH = (int)(gas_gauge.fcc_data + gas_gauge.fcc_data * 0.01F - 1);
				bmu_write_data(BATT_CAPACITY, batt_info.sCaMAH);
			}

			batt_info.fRSOC = batt_info.sCaMAH * 100F;
			batt_info.fRSOC = batt_info.fRSOC / gas_gauge.fcc_data;

			batt_info.fRC = batt_info.fRSOC * gas_gauge.fcc_data * 0.01F; // num_100;
			if (batt_info.fRC >= (gas_gauge.overflow_data - 10))
			{
				batt_info.fRC = gas_gauge.overflow_data - gas_gauge.overflow_data * 0.01F;// num_100;
				batt_info.fRCPrev = batt_info.fRC;
			}

			bmu_printk(string.Format("AAAA batt_info->fVolt is {0:F2}", (batt_info.fVolt * OZ88105_cell_num)));
			bmu_printk(string.Format("AAAA batt_info->fRSOC is {0:F2}", batt_info.fRSOC));
			bmu_printk(string.Format("AAAA batt_info->sCaMAH is {0:d}", batt_info.sCaMAH));
			bmu_printk(string.Format("AAAA gas_gauge->sCtMAH is {0:d}", gas_gauge.sCtMAH));
			bmu_printk(string.Format("AAAA fcc is {0:d}", gas_gauge.fcc_data));
			bmu_printk(string.Format("AAAA batt_info->fRC is {0:F2}", batt_info.fRC));
			bmu_printk(string.Format("AAAA batt_info->fCurr is {0:F2}", batt_info.fCurr));

			//write to
			//afe_write_car(batt_info->fRC);
			WriteRegFunction(ParamPhyCAR, batt_info.fRC);
			batt_info.fRCPrev = batt_info.fRC;
			bmu_init_ok = 1;

			gas_gauge.sCtMAH = batt_info.sCaMAH;

			if (fcc > batt_info.sCaMAH)
				gas_gauge.discharge_sCtMAH = fcc - batt_info.sCaMAH;
			else
				gas_gauge.discharge_sCtMAH = 0;

			if (batt_info.fRSOC <= 2)
			{
				gas_gauge.charge_fcc_update = 1;
			}
			if (batt_info.fRSOC <= 0)
			{
				gas_gauge.discharge_end = 1;
				batt_info.fRSOC = 0;
				gas_gauge.sCtMAH = 0;
				gas_gauge.ocv_flag = 0;
				bmu_write_data(OCV_FLAG, 0);
			}
			if (batt_info.fRSOC >= 100)
			{
				gas_gauge.charge_end = 1;
				batt_info.fRSOC = 100F;
				gas_gauge.discharge_sCtMAH = 0;
				gas_gauge.discharge_fcc_update = 1;
				gas_gauge.ocv_flag = 0;
				bmu_write_data(OCV_FLAG, 0);
			}

			if (batt_info.fRSOC >= 95)
			{
				gas_gauge.discharge_fcc_update = 1;
			}

			check_shutdown_voltage();
			fRSOC_PRE = batt_info.fRSOC;

			if (gas_gauge.ocv_flag != 0)
			{
				charge_fcc_update = 0;
				discharge_fcc_update = 0;
			}
			else
			{
				charge_fcc_update = 1;
				discharge_fcc_update = 1;
			}

			// double check
			bmu_read_data(BATT_CAPACITY, ref data);
			if (data != batt_info.sCaMAH)
			{
				bmu_write_data(BATT_CAPACITY, batt_info.sCaMAH);
				bmu_printk(string.Format("init write sCaMAH  {0:d},{1:d}\n", batt_info.sCaMAH, data));
			}

			bmu_read_data(BATT_FCC, ref data);
			if (data != gas_gauge.fcc_data)
			{
				bmu_write_data(BATT_FCC, gas_gauge.fcc_data);
				bmu_printk(string.Format("init write fcc  {0:d},{1:d}\n", gas_gauge.fcc_data, data));
			}
		}

		private void bmu_power_down_chip()
		{
			if (OZ88105_cell_num > 1)
			{
				//afe_register_write_byte(OZ88105_OP_CTRL,num_0x4c |SLEEP_OCV_EN);
				//WriteRegFunction(ParamOZ88105Status, 0x6C);
				SetCtrlValue(0x6c);
			}
			else
			{
				//afe_register_write_byte(OZ88105_OP_CTRL,SLEEP_MODE | SLEEP_OCV_EN);
				//WriteRegFunction(ParamOZ88105Status, 0x60);
				SetCtrlValue(0x60);
			}

			//afe_read_cell_temp(&batt_info->fCellTemp);
			//afe_read_cell_volt(&batt_info->fVolt);
			//afe_read_current(&batt_info->fCurr);
			//afe_read_car(&batt_info->fRC);
			CopyPhysicalToRam();

			if (parameter_customer.debug)
			{
				bmu_printk("eeee power down  OZ88105.");
				bmu_printk(string.Format("eeee batt_info->fVolt is {0:F2}", (batt_info.fVolt * OZ88105_cell_num)));
				bmu_printk(string.Format("eeee batt_info->fRSOC is {0:F2}", batt_info.fRSOC));
				bmu_printk(string.Format("eeee batt_info->sCaMAH is {0:d}", batt_info.sCaMAH));
				bmu_printk(string.Format("eeee batt_info->fRC is {0:F2}", batt_info.fRC));
				bmu_printk(string.Format("eeee batt_info->fCurr is {0:F2}", batt_info.fCurr));
			}
		}

		//no used in OZ88105
		private void bmu_wake_up_chip_8806()
		{
			byte data;
			Int32 ret;
			float fvalue;
			Int32 car = 1;
			byte discharge_flag = 0;
			byte i;
			long sleep_time = 0;

			bmu_printk(string.Format("fCurr is {0:F2}", batt_info.fCurr));

			if ((batt_info.fCurr < gas_gauge.discharge_current_th) ||
				(adapter_status == 0))
			{
				discharge_flag = 1;
			}

			bmu_printk(string.Format("adapter_status: {0:d}", adapter_status));
			bmu_sleep_flag = 1;

			//do_gettimeofday(&(time_x.time));
			//sleep_time = time_x.time.tv_sec - previous_loop_timex;
			sleep_time = DateTime.Now.ToBinary();
			sleep_time = sleep_time - previous_loop_timex;
			bmu_printk(string.Format("time: {0:d}", sleep_time));

			//ret = afe_register_read_byte(OZ88105_OP_CTRL, &data);//wake up OZ88105 into FullPower mode
			//if(ParamOZ88105Status != null)
			data = 0;
			if (GetCtrlValue(ref data))
			{
				ret = 0;
				//data = (byte)ParamOZ88105Status.phydata;
				//data = (byte)sRam[(int)prShortIndex.iStatus];
			}
			else
			{
				ret = -1;
				data = 0;
			}
			if (((data & 0x40) != 0) || (ret < 0))
			{
				bmu_printk(string.Format("bmu_wake_up_chip read 0x09 ret is {0:d},i is {1:d}", ret, data));
				if (OZ88105_cell_num > 1)
				{
					//afe_register_write_byte(OZ88105_OP_CTRL, 0x2c);
					//WriteRegFunction(ParamOZ88105Status, 0x2C);
					SetCtrlValue(0x2c);
				}
				else
				{
					//afe_register_write_byte(OZ88105_OP_CTRL, 0x20);
					//WriteRegFunction(ParamOZ88105Status, 0x20);
					SetCtrlValue(0x20);
				}
				bmu_printk("OZ88105 wake up function\n");

			}

			//
			//afe_read_current(&batt_info->fCurr);
			//afe_read_cell_volt(&batt_info->fVolt);
			CopyPhysicalToRam();

			if (batt_info.fRSOC >= 100)
			{
				if (batt_info.fRSOC >= 100) batt_info.fRSOC = 100;
				if (batt_info.sCaMAH > (gas_gauge.fcc_data + gas_gauge.fcc_data * 0.01F - 1))
				{
					bmu_printk(string.Format("OZ88105 wake up batt_info.sCaMAH big error is {0:d}", batt_info.sCaMAH));
					batt_info.sCaMAH = (int)(gas_gauge.fcc_data + gas_gauge.fcc_data * 0.01F - 1);// full_charge_data;

				}
				if ((discharge_flag != 0) && (batt_info.fVolt >= (myProject.dbChgCVVolt - 150)))//(config_data->charge_cv_voltage - 150)))
				{
					//afe_read_car(&batt_info->fRC);
					bmu_printk(string.Format("batt_info->fRC is {0:F2}", batt_info.fRC));
					if (batt_info.fRC > 0)
						batt_info.fRCPrev = batt_info.fRC;
					else
						OZ88105_over_flow_prevent();
					return;
				}

			}

			//afe_read_current(&batt_info->fCurr);
			//afe_read_current(&batt_info->fCurr);
			bmu_printk(string.Format("sCaMAH:{0:d},fRC:{1:F2},fcurr:{2:F2},volt:{3:F2},sCtMAH:{4:d}\n",
				batt_info.sCaMAH, batt_info.fRC, batt_info.fCurr,
				(batt_info.fVolt * OZ88105_cell_num),
				batt_info.fRC));
			for (i = 0; i < 3; i++)
			{
				//ret = afe_read_car(&car);
				ret = 1;
				car = 1;
				if (ret >= 0)
				{
					if (car == 0)
					{
						car_error += 1;
						bmu_printk(string.Format("test fRC read: {0:d}, {1:d}\n", car_error, car));	//H7 Test Version
					}
					if (car > 0)
						break;
				}
			}

			fvalue = car - batt_info.fRC;

			//*******CAR zero reading Workaround******************************
			//#if 1
			if (sleep_time < 0) sleep_time = 60;			//force 60s
			if (discharge_flag == 1)
				sleep_time *= MAX_SUSPEND_CONSUME;			//time * current / 3600 * 1000
			else
				sleep_time *= MAX_SUSPEND_CHARGE;			//time * current / 3600 * 1000

			if (Math.Abs(car - batt_info.fRC) > (myProject.dbDesignCp * 0.1F))//(config_data->design_capacity / 10))		//delta over 10%
			{
				if (((fvalue * 1000) - sleep_time) < 0)				//over max car range
				{
					fvalue = sleep_time * 0.001F;// (sleep_time / 1000);
					bmu_printk(string.Format("Ab CAR:{0:d},mod:{1:d}", car, fvalue));
					car = (int)(fvalue + batt_info.fRC + 0.5F);
				}
			}
			//#endif
			batt_info.fRCPrev = batt_info.fRC;
			batt_info.fRC = car;
			//*******CAR zero reading Workaround******************************	
			if (batt_info.fRC < 0)
			{
				bmu_printk(string.Format("fRC is {0:F2}", batt_info.fRC));
				OZ88105_over_flow_prevent();
				gas_gauge.charge_fcc_update = 0;
				gas_gauge.discharge_fcc_update = 0;

				if (discharge_flag != 0)
				{
					//value = batt_info->sCaMAH;
					check_shutdown_voltage();
					fvalue = calculate_soc_result();
					if (fvalue < batt_info.fRSOC)
					{
						batt_info.fRSOC = fvalue;
						batt_info.sCaMAH = (int)(batt_info.fRSOC * gas_gauge.fcc_data * 0.01F + 0.5F);/// 100;
						batt_info.fRC = batt_info.sCaMAH;
						batt_info.fRCPrev = batt_info.fRC;
						//afe_write_car(batt_info.fRC);
						WriteRegFunction(ParamPhyCAR, batt_info.fRC);
					}

					if (batt_info.fRSOC <= 0)
					{
						if ((batt_info.fVolt <= myProject.dbDsgEndVolt))//config_data->discharge_end_voltage))
						{
							discharge_end_process();
						}
						//wait voltage
						else
						{
							batt_info.fRSOC = 1F;
							batt_info.sCaMAH = (int)(gas_gauge.fcc_data * 0.01F); // num_100;
							gas_gauge.discharge_end = 0;
						}
					}
					return;
				}
				else  //charge
				{
					//
					//msleep(600);
					//ret = afe_read_current(&batt_info->fCurr);

					if ((batt_info.fCurr <= myProject.dbChgEndCurr) &&//config_data.charge_end_current) &&
						(batt_info.fCurr >= gas_gauge.discharge_current_th)
						&& (batt_info.fVolt >= (myProject.dbChgCVVolt - 26)))//charge_full_voltage))
					{
						charge_end_process();
						return;
					}
					else
					{
						//value = batt_info->sCaMAH;
						//check_shutdwon_voltage();
						fvalue = calculate_soc_result();
						if (fvalue > batt_info.fRSOC)
						{
							batt_info.fRSOC = fvalue;
							batt_info.sCaMAH = (int)(batt_info.fRSOC * gas_gauge.fcc_data * 0.01);// 100;
							batt_info.fRC = batt_info.sCaMAH;
							batt_info.fRCPrev = batt_info.fRC;
							//afe_write_car(batt_info.fRC);
							WriteRegFunction(ParamPhyCAR, batt_info.fRC);
						}

						if (batt_info.fRSOC >= 100)
						{
							batt_info.fRSOC = 99;
							batt_info.sCaMAH = (int)(gas_gauge.fcc_data - 1);
							gas_gauge.discharge_end = 0;
						}
						return;
					}
				}
			}
			bmu_printk(string.Format("fRCPrev:{0:F2},fRC:{1:F2}", batt_info.fRCPrev, batt_info.fRC));

			if ((discharge_flag != 0) && (batt_info.fRC > batt_info.fRCPrev))
			{
				//batt_info.sCaMAH = batt_info.sCaMAH;
				bmu_printk("it seems error 1");
			}
			else if ((discharge_flag == 0) && (batt_info.fRC < batt_info.fRCPrev))
			{
				//batt_info.sCaMAH = batt_info.sCaMAH;
				bmu_printk("it seems error 2");
			}
			else
			{
				if ((discharge_flag == 0) && (fvalue > 0))
					charge_tick += (uint)(fvalue);

				bmu_printk(string.Format("charge_tick is {0:d}", charge_tick));

				if (discharge_flag == 0)
				{
					if (charge_tick < 25)
						batt_info.sCaMAH += (int)(fvalue);
					else
					{
						batt_info.sCaMAH += (int)(fvalue - charge_tick / 25);
						charge_tick %= 25;
					}
				}
				else
				{
					batt_info.sCaMAH += (int)(fvalue);
				}

				bmu_printk(string.Format("wakup sCaMAH update is {0:d}, {1:F2}", batt_info.sCaMAH, fvalue));

				if (batt_info.sCaMAH > (gas_gauge.fcc_data + gas_gauge.fcc_data * 0.01F - 1))//(full_charge_data))
				{
					bmu_printk(string.Format("sleep big error sCaMAH is {0:d}", batt_info.sCaMAH));
					batt_info.sCaMAH = (int)(gas_gauge.fcc_data + gas_gauge.fcc_data * 0.01F - 1);//full_charge_data;
				}

				gas_gauge.sCtMAH += (int)(fvalue);
				gas_gauge.discharge_sCtMAH -= (int)(fvalue);
				bmu_printk(string.Format("wakup sCtMAH1 update is {0:d}, {1:F2}", gas_gauge.sCtMAH, fvalue));
				bmu_printk(string.Format("wakup sCtMAH2 update is {0:d}, {1:F2}", gas_gauge.discharge_sCtMAH, fvalue));
			}

			if (gas_gauge.sCtMAH < 0) gas_gauge.sCtMAH = 0;
			if (gas_gauge.discharge_sCtMAH < 0) gas_gauge.discharge_sCtMAH = 0;

			if (parameter_customer.debug) bmu_printk(string.Format("tttt batt_info->fRC is {0:F2}", batt_info.fRC));

			/*
			//if(discharge_flag)
			//{
				//value = batt_info->sCaMAH;
				//check_shutdwon_voltage();
				//if(value < batt_info->sCaMAH)
				//{
					//batt_info->sCaMAH = value;
				//}
			//}
			*/

			//	batt_info->fRCPrev = batt_info->fRC;	updated above
			batt_info.fRSOC = batt_info.sCaMAH * 100;
			batt_info.fRSOC = batt_info.fRSOC / gas_gauge.fcc_data;

			if ((batt_info.fRSOC >= 100) && (discharge_flag == 0))
			{
				//wait charger
				batt_info.fRSOC = 99;
				batt_info.sCaMAH = gas_gauge.fcc_data - 1;
				charge_end = 0;
				bmu_printk(string.Format("sleep wake up waiter charger. is {0:d}", batt_info.sCaMAH));
				return;

				/*
				//ret = afe_read_current(&batt_info->fCurr);
				//printk("11batt_info->fCurr is {0:d}",batt_info->fCurr);
				//msleep(600);
				//ret = afe_read_current(&batt_info->fCurr);
		
				//if(wait_dc_charger || gas_gauge->ocv_flag || (batt_info->fCurr >= CHARGE_END_CURRENT2))
				//{
					//if((batt_info->fCurr <= config_data->charge_end_current)&&(batt_info->fCurr >= gas_gauge->discharge_current_th) 
						//&& (batt_info->fVolt >= charge_full_voltage))
					//{
						//printk("sleep CAR ok and end charge1\n");
						//charge_end_process();
						//return;
					//}
					//wait charger
					//else
					//{
						//batt_info->fRSOC  = 99;
						//batt_info->sCaMAH = gas_gauge->fcc_data - 1;
						//charge_end = 0;
						//printk("sleep wake up waiter charger. is {0:d}",batt_info->sCaMAH);
						//return;
					//}
				//}
				//else
				//{
					//if((batt_info->fCurr >= gas_gauge->discharge_current_th)&&(batt_info->fCurr <= CHARGE_END_CURRENT2) 
						//&& (batt_info->fVolt >= charge_full_voltage))
					//{
						//printk("sleep CAR ok,end charge2\n");
						//charge_end_process();
						//return;
					//}
					//printk("nothing to do\n");
				//}
				*/
			}
			else if (batt_info.fRSOC <= 0)
			{
				//
				//afe_read_cell_volt(&batt_info->fVolt);
				bmu_printk(string.Format("batt_info->fVolt is {0:F2}", batt_info.fVolt));
				if ((batt_info.fVolt <= myProject.dbDsgEndVolt))//config_data->discharge_end_voltage))
				{
					bmu_printk("sleep CAR ok and end discharge\n");
					discharge_end_process();
					return;
				}

				//wait voltage
				else
				{
					batt_info.fRSOC = 1;
					batt_info.sCaMAH = (int)(gas_gauge.fcc_data * 0.01F + 0.5F);
					gas_gauge.discharge_end = 0;
					if (parameter_customer.debug) bmu_printk(string.Format("fffff wake up waiter voltage. is {0:d}", batt_info.sCaMAH));
					return;
				}
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
			gas_gauge.volt_num_i = volt_num;
			gas_gauge.volt_average_now = batt_info.fVolt;

			//Call api
			//bmu_call();		//call system, no need
			if(parameter_customer.debug)
			{
				bmu_printk("----------------------------------------------------\n");
				bmu_printk(string.Format("AAAA batt_info.fVolt is {0:F2}  {0:d}",(batt_info.fVolt * OZ88105_cell_num),gas_gauge.bmu_tick));
				bmu_printk(string.Format("AAAA batt_info.fRSOC is {0:F2}  {0:d}",batt_info.fRSOC,gas_gauge.bmu_tick));
				bmu_printk(string.Format("AAAA batt_info.chg_on is {0:d}",batt_info.chg_on));
				bmu_printk(string.Format("AAAA volt_gg RSOC is {0:F2} ", voltage_gasgauge.m_fRsoc));
				bmu_printk(string.Format("AAAA volt_gg m_fStateOfCharge is {0:d}", voltage_gasgauge.m_fStateOfCharge));
				bmu_printk("----------------------------------------------------");
			}

			//skip lots
			//#if 0
			//#endif
		}

		//no used in OZ88105
		private void check_board_offset()
		{
			Int32 data;
			Int32 ret;
			Int32 offset;
			Int32 ret2;

			if (parameter_customer.board_offset != 0)
			{
				//afe_write_board_offset(config_data->board_offset);
				WriteRegFunction(ParamOZ88105BoardOffset, parameter_customer.board_offset);
				check_offset_flag = 0;
				return;
			}

			//ret = bmu_check_file(BATT_OFFSET);
			ret = 0;
			if ((ret < 0) && (check_board_offset_i < 3))
			{
				check_board_offset_i++;
				return;
			}

			//ret2 = afe_read_board_offset(&data);
			//printk("AAAA board_offset is  {0:d}\n",data);
			ret = -1;
			data = 0;
			ret2 = 0;
			if (ret < 0)
			{

				if ((data > 10) || (data <= 0))
				{
					//afe_write_board_offset(7);
					data = 7;
					WriteRegFunction(ParamOZ88105BoardOffset, data);
				}

				if (ret2 >= 0)
				{
					if ((data < 10) && (data > 0) && (data != 0))
					{
						//ret = bmu_write_data(BATT_OFFSET,data);
						bmu_write_data(BATT_OFFSET, data);
						//if(ret <num_0)
						//printk("first write board_offset error\n");

						//data = bmu_read_data(BATT_OFFSET);
						bmu_read_data(BATT_OFFSET, ref data);

						bmu_printk(string.Format("first write board_offset is {0:d}", data));
						write_offset = 1;
					}
				}
			}
			else
			{

				offset = 0;
				//offset = bmu_read_data(BATT_OFFSET);
				bmu_read_data(BATT_OFFSET, ref offset);
				if (((offset - data) > 2) || ((offset - data) < -2))
				{
					//afe_write_board_offset(offset);
					WriteRegFunction(ParamOZ88105BoardOffset, offset);
				}
			}

			//afe_read_board_offset(&data);
			if (ParamOZ88105BoardOffset != null)
			{
				data = (int)ParamOZ88105BoardOffset.phydata;
			}
			if ((data > 10) || (data <= 0))
			{
				data = 7;
				//afe_write_board_offset(num_7);
				WriteRegFunction(ParamOZ88105BoardOffset, data);
			}

			//afe_read_board_offset(&data);
			bmu_printk(string.Format("AAAA board_offset is  {0:d}", data));
			check_offset_flag = 0;
		}

		//no used in OZ88105
		private void bmu_polling_loop_8806()
		{
			Int32 data;
			Int32 ret;
			byte i;
			////uint32_t charge_end_time = 0;
			////static uint8_t charge_full_times = 0;


			if (bmu_init_ok == 0)
			{
				//wait_ocv_flag_fun();
				bmu_wait_ready();
				//if (bmu_init_ok != 0)
				//{
					//check_offset_flag = 1;
				//}
				return;
			}
			//no current in OZ88105
			//if (check_offset_flag != 0)
			//{
				//check_board_offset();
			//}

			//batt_info.fRCPrev = batt_info.fRC;
			//batt_info.fVoltPrev = batt_info.fVolt;
			gas_gauge.dt_time_pre = gas_gauge.dt_time_now;
			batt_info.fVoltPrev = batt_info.fVolt;

			//afe_read_cell_temp(&batt_info->fCellTemp);
			//afe_read_cell_volt(&batt_info->fVolt);
			//afe_read_current(&batt_info->fCurr);
			CopyPhysicalToRam();
			volt_gg_state();

			//do_gettimeofday(&(time_x.time));
			//rtc_time_to_tm(time_x.time.tv_sec,&rtc_time);
			gas_gauge.dt_time_now = DateTime.Now;

			//be careful for large current
			//and wake up condition
			//ret = afe_read_car(&data);
			if (ParamPhyCAR != null)
			{
				data = (int)ParamPhyCAR.phydata;
			}
			else
			{
				data = 0;
			}
			if (parameter_customer.debug)
				bmu_printk(string.Format("CAR is {0:d}", data));

			ret = 0;
			if ((ret >= 0) && (data > 0))
			{
				// for big current charge
				if (((batt_info.fRCPrev - data) < (10 * myProject.dbDesignCp * 0.01F)) &&
					((data - batt_info.fRCPrev) < (10 * myProject.dbDesignCp * 0.01F)))
				{
					batt_info.fRC = data;
					polling_error_times = 0;
				}
				else
				{
					polling_error_times++;
					if (polling_error_times > 3)
					{
						bmu_printk(string.Format("CAR error_times is {0:d}", polling_error_times));
						polling_error_times = 0;
						//afe_write_car(batt_info->sCaMAH);
						WriteRegFunction(ParamPhyCAR, batt_info.sCaMAH);
						batt_info.fRCPrev = batt_info.sCaMAH;
						batt_info.fRC = batt_info.sCaMAH;
					}
				}
			}
			if (parameter_customer.debug)
			{
				bmu_printk(string.Format("first sCaMAH:{0:F2},fRC:{1:F2},fRCPrev:{2:F2},fcurr:{3:F2},volt:{4:F2}\n",
					batt_info.sCaMAH, batt_info.fRC, batt_info.fRCPrev,
					batt_info.fCurr, (batt_info.fVolt * OZ88105_cell_num)));
			}
			//back sCaMAH
			//H7====LOW VOLTAGE CHARGE PREVENT======
			if ((batt_info.fVolt < myProject.dbDsgEndVolt)//config_data->discharge_end_voltage)
				&& (batt_info.fCurr > 0)) //charge and voltage < 3300
			{
				bmu_printk(string.Format("Low voltage {0:F2} charge current {1:F2} detected", batt_info.fVolt, batt_info.fCurr));
				if (batt_info.fRC > batt_info.fRCPrev)	//CAR increased
				{
					bmu_printk(string.Format("Low voltage fRC limit triggered {0:F2}", batt_info.fRCPrev));
					batt_info.fRC = batt_info.fRCPrev;	//Limit CAR as previous	
					//afe_write_car(batt_info->fRC);			//write back CAR
					WriteRegFunction(ParamPhyCAR, batt_info.fRC);
				}
			}
			//H7====LOW VOLTAGE CHARGE PREVENT======

			gas_gauge.sCtMAH += (int)(batt_info.fRC - batt_info.fRCPrev);
			gas_gauge.discharge_sCtMAH += (int)(batt_info.fRCPrev - batt_info.fRC);

			if (gas_gauge.sCtMAH < 0) gas_gauge.sCtMAH = 0;
			if (gas_gauge.discharge_sCtMAH < 0) gas_gauge.discharge_sCtMAH = 0;

			//bmu_call();

			if (batt_info.fCurr > gas_gauge.discharge_current_th)
				charge_process();
			else
				discharge_process();


			if (parameter_customer.debug)
				bmu_printk(string.Format("second sCaMAH:{0:d},fRC:{1:F2},fRCPrev:{2:F2},fcurr:{3:F2},volt:{4:F2}\n",
					batt_info.sCaMAH, batt_info.fRC, batt_info.fRCPrev,
					batt_info.fCurr, (batt_info.fVolt * OZ88105_cell_num)));

			if (gas_gauge.charge_end != 0)
			{
				if (charge_end_flag == 0)
				{
					bmu_printk("enter 8806 charge end\n");
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
					discharge_end_flag = 1;
					discharge_end_process();
				}

			}
			else
			{
				discharge_end_flag = 0;
			}

			OZ88105_over_flow_prevent();

			/*
			//very dangerous
			if(batt_info->fVolt <= (config_data->discharge_end_voltage - num_100))
			{
				ret = afe_read_cell_volt(&batt_info->fVolt);
				if((ret >=0) && (batt_info->fVolt > 2500))
					discharge_end_process();
			}
			*/


			gas_gauge.bmu_tick++;
			//do_gettimeofday(&(time_x.time));
			previous_loop_timex = DateTime.Now.ToBinary();//time_x.time.tv_sec;


			if (parameter_customer.debug)
			{
				bmu_printk("----------------------------------------------------");
				bmu_printk(string.Format("VERSION: {0}, battery_ok: {1:d}, chg_fcc_update: {3:d}, disg_fcc_update: {4:d}", VERSION, batt_info.Battery_ok, previous_loop_timex, gas_gauge.charge_fcc_update, gas_gauge.discharge_fcc_update));
				bmu_printk(string.Format("fVolt: {0:F2}   fCurr: {1:F2}   fCellTemp: {2:F2}   fRSOC: {3:F2}\n", (batt_info.fVolt * OZ88105_cell_num), batt_info.fCurr, batt_info.fCellTemp, batt_info.fRSOC));
				bmu_printk(string.Format("sCaMAH: {0:d}, sCtMAH1: {1:d}, sCtMAH2: {2:d}", batt_info.sCaMAH, gas_gauge.sCtMAH, gas_gauge.discharge_sCtMAH));
				bmu_printk(string.Format("fcc: {0:d}, fRC: {1:F2}, i2c_error_times: {2:d}", gas_gauge.fcc_data, batt_info.fRC, batt_info.i2c_error_times));
				bmu_printk(string.Format("charger_finish: {0:d}, charge_end: {1:d}, discharge_end: {2:d}, adapter_status: {3:d}", charger_finish, charge_end_flag, gas_gauge.discharge_end, adapter_status));
				bmu_printk("----------------------------------------------------");
			}

			//data = bmu_read_data(BATT_CAPACITY);
			data = 0;
			bmu_read_data(BATT_CAPACITY, ref data);
			if (batt_info.sCaMAH > (gas_gauge.fcc_data + gas_gauge.fcc_data * 0.01F - 1))//(full_charge_data))
			{
				bmu_printk(string.Format("big error sCaMAH is {0:d}", batt_info.sCaMAH));
				batt_info.sCaMAH = (int)(gas_gauge.fcc_data + gas_gauge.fcc_data * 0.01F - 1);//full_charge_data;
			}

			if (batt_info.sCaMAH < (gas_gauge.fcc_data * 0.01F - 1)) // 100 - 1))
			{
				//printk("big error sCaMAH is {0:d}",batt_info->sCaMAH);
				batt_info.sCaMAH = (int)(gas_gauge.fcc_data * 0.01F - 1); // 100 - 1;
			}

			if (gas_gauge.fcc_data > (myProject.dbDesignCp * FCC_UPPER_LIMIT * 0.01F))/// 100))
			{
				gas_gauge.fcc_data = (int)(myProject.dbDesignCp * FCC_UPPER_LIMIT * 0.01F + 0.5F);// 100;
				bmu_write_data(BATT_FCC, gas_gauge.fcc_data);

				bmu_printk(string.Format("fcc error is {0:d}", gas_gauge.fcc_data));

			}
			if ((gas_gauge.fcc_data <= 0) || (gas_gauge.fcc_data < (myProject.dbDesignCp * FCC_LOWER_LIMIT * 0.01F)))// 100)))
			{
				gas_gauge.fcc_data = (int)(myProject.dbDesignCp * FCC_LOWER_LIMIT * 0.01F + 0.5F);// 100;
				bmu_write_data(BATT_FCC, gas_gauge.fcc_data);

				bmu_printk(string.Format("fcc error is {0:d}", gas_gauge.fcc_data));
			}

			if (parameter_customer.debug)
			{
				bmu_printk(string.Format("read from RAM batt_info->sCaMAH is {0:d}", data));
			}
			if (data >= 0)
			{
				if (fRSOC_PRE != batt_info.fRSOC)
				{
					fRSOC_PRE = batt_info.fRSOC;
					bmu_write_data(BATT_CAPACITY, batt_info.sCaMAH);
					if (parameter_customer.debug)
						bmu_printk(string.Format("o2 back batt_info->sCaMAH num_1 is {0:d}", batt_info.sCaMAH));
					return;

				}

				if (((batt_info.sCaMAH - data) > (gas_gauge.fcc_data / 200)) || ((data - batt_info.sCaMAH) > (gas_gauge.fcc_data / 200)))
				{
					bmu_write_data(BATT_CAPACITY, batt_info.sCaMAH);
					if (parameter_customer.debug)
					{
						bmu_printk(string.Format("o2 back batt_info->sCaMAH 2 is {0:d}", batt_info.sCaMAH));
					}
				}
			}

			//wake up OZ88105 into FullPower mode
			//ret = afe_register_read_byte(OZ88105_OP_CTRL,&i);
			ret = 0;
			i = 0;
			//if(ParamOZ88105Status != null)
			if (GetCtrlValue(ref i))
			{
				//i = (byte)(int)ParamOZ88105Status.phydata;
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
				if (parameter_customer.debug)
				{
					bmu_printk(string.Format("bmu_polling_loop read 0x09 ret is {0:d},i is {1:d}", ret, i));
				}
				if (OZ88105_cell_num > 1)
				{
					//afe_register_write_byte(OZ88105_OP_CTRL,num_0x2c);
					//WriteRegFunction(ParamOZ88105Status, 0x2C);
					SetCtrlValue(0x2c);
				}
				else
				{
					//afe_register_write_byte(OZ88105_OP_CTRL,num_0x20);
					//WriteRegFunction(ParamOZ88105Status, 0x20);
					SetCtrlValue(0x20);
				}
				//printk("OZ88105 wake up function\n");
			}

			//ret  = afe_read_board_offset(&data);
			ret = 0;
			if (ParamOZ88105BoardOffset != null)
			{
				data = (int)ParamOZ88105BoardOffset.phydata;
			}
			else
			{
				data = 0;
			}
			if (ret >= 0)
			{
				if ((data > 10) || (data <= 0))
				{
					bmu_printk(string.Format("OZ88105 board offset error is {0:d}", data));
					//afe_write_board_offset(7);
					data = 7;
					WriteRegFunction(ParamOZ88105BoardOffset, data);
				}
			}

			if (parameter_customer.debug)
			{
				bmu_printk(string.Format("test data is {0:d}", batt_info.sCaMAH));
			}

			//data = bmu_read_data(BATT_FCC);
			data = 0;
			bmu_read_data(BATT_FCC, ref data);

			if (data != gas_gauge.fcc_data)
			{
				bmu_write_data(BATT_FCC, gas_gauge.fcc_data);
				bmu_printk(string.Format("test {0:d}", gas_gauge.fcc_data));
			}
		}

		private void bmu_polling_loop()
		{
			Int32 data;
			Int32 ret;
			byte i;
			////uint32_t charge_end_time = 0;
			////static uint8_t charge_full_times = 0;

			if (bmu_init_ok == 0)
			{
				//wait_ocv_flag_fun();
				bmu_wait_ready();
				//if (bmu_init_ok != 0)
				//{
				//check_offset_flag = 1;
				//}
				return;
			}
			//no current in OZ88105
			//if (check_offset_flag != 0)
			//{
			//check_board_offset();
			//}

			//batt_info.fRCPrev = batt_info.fRC;
			//batt_info.fVoltPrev = batt_info.fVolt;
			gas_gauge.dt_time_pre = gas_gauge.dt_time_now;
			batt_info.fVoltPrev = batt_info.fVolt;

			//afe_read_cell_temp(&batt_info->fCellTemp);
			//afe_read_cell_volt(&batt_info->fVolt);
			//afe_read_current(&batt_info->fCurr);
			CopyPhysicalToRam();
			volt_gg_state();

			//do_gettimeofday(&(time_x.time));
			//rtc_time_to_tm(time_x.time.tv_sec,&rtc_time);
			gas_gauge.dt_time_now = DateTime.Now;

			#region //no CAR
			////ret = afe_read_car(&data);
			//if (ParamPhyCAR != null)
			//{
				//data = (int)ParamPhyCAR.phydata;
			//}
			//else
			//{
				//data = 0;
			//}
			//if (parameter_customer.debug)
				//bmu_printk(string.Format("CAR is {0:d}", data));

			//ret = 0;
			//if ((ret >= 0) && (data > 0))
			//{
				// for big current charge
				//if (((batt_info.fRCPrev - data) < (10 * myProject.dbDesignCp * 0.01F)) &&
					//((data - batt_info.fRCPrev) < (10 * myProject.dbDesignCp * 0.01F)))
				//{
					//batt_info.fRC = data;
					//polling_error_times = 0;
				//}
				//else
				//{
					//polling_error_times++;
					//if (polling_error_times > 3)
					//{
						//bmu_printk(string.Format("CAR error_times is {0:d}", polling_error_times));
						//polling_error_times = 0;
						////afe_write_car(batt_info->sCaMAH);
						//WriteRegFunction(ParamPhyCAR, batt_info.sCaMAH);
						//batt_info.fRCPrev = batt_info.sCaMAH;
						//batt_info.fRC = batt_info.sCaMAH;
					//}
				//}
			//}
			//if (parameter_customer.debug)
			//{
				//bmu_printk(string.Format("first sCaMAH:{0:F2},fRC:{1:F2},fRCPrev:{2:F2},fcurr:{3:F2},volt:{4:F2}\n",
					//batt_info.sCaMAH, batt_info.fRC, batt_info.fRCPrev,
					//batt_info.fCurr, (batt_info.fVolt * OZ88105_cell_num)));
			//}
			//back sCaMAH
			//H7====LOW VOLTAGE CHARGE PREVENT======
			//if ((batt_info.fVolt < myProject.dbDsgEndVolt)//config_data->discharge_end_voltage)
				//&& (batt_info.fCurr > 0)) //charge and voltage < 3300
			//{
				//bmu_printk(string.Format("Low voltage {0:F2} charge current {1:F2} detected", batt_info.fVolt, batt_info.fCurr));
				//if (batt_info.fRC > batt_info.fRCPrev)	//CAR increased
				//{
					//bmu_printk(string.Format("Low voltage fRC limit triggered {0:F2}", batt_info.fRCPrev));
					//batt_info.fRC = batt_info.fRCPrev;	//Limit CAR as previous	
					////afe_write_car(batt_info->fRC);			//write back CAR
					//WriteRegFunction(ParamPhyCAR, batt_info.fRC);
				//}
			//}
			//H7====LOW VOLTAGE CHARGE PREVENT======
			#endregion

			average_voltage();
			//bmu_call();
			//(A150909)Francis
			if (batt_info.chg_dsg_flag == CHARGE_STATE) 
				charge_process();
			if (batt_info.chg_dsg_flag == DISCHARGE_STATE) 
				discharge_process();
			if (batt_info.chg_dsg_flag != DISCHARGE_STATE)
			{
				voltage_gasgauge.m_fCurrent = 0;
				batt_info.fCurr = 0;
			}

			if (batt_info.fRSOC >= 100) batt_info.fRSOC = 100;
			if (batt_info.fRSOC <= 0) batt_info.fRSOC = 0;
			//(E150909)
			gas_gauge.fcc_data = voltage_gasgauge.m_fFCC;
			gas_gauge.sCtMAH += (int)(batt_info.fRC - batt_info.fRCPrev + 0.5);
			gas_gauge.discharge_sCtMAH += (int)(batt_info.fRCPrev - batt_info.fRC+0.5);
			if (gas_gauge.sCtMAH < 0) gas_gauge.sCtMAH = 0;
			if (gas_gauge.discharge_sCtMAH < 0) gas_gauge.discharge_sCtMAH = 0;

			//gas_gauge.sCtMAH += (int)(batt_info.fRC - batt_info.fRCPrev);
			//gas_gauge.discharge_sCtMAH += (int)(batt_info.fRCPrev - batt_info.fRC);

			//if (gas_gauge.sCtMAH < 0) gas_gauge.sCtMAH = 0;
			//if (gas_gauge.discharge_sCtMAH < 0) gas_gauge.discharge_sCtMAH = 0;

			//if (batt_info.fCurr > gas_gauge.discharge_current_th)
				//charge_process();
			//else
				//discharge_process();


			//if (parameter_customer.debug)
				//bmu_printk(string.Format("second sCaMAH:{0:d},fRC:{1:F2},fRCPrev:{2:F2},fcurr:{3:F2},volt:{4:F2}\n",
					//batt_info.sCaMAH, batt_info.fRC, batt_info.fRCPrev,
					//batt_info.fCurr, (batt_info.fVolt * OZ88105_cell_num)));

			if (gas_gauge.charge_end != 0)
			{
				if (charge_end_flag == 0)
				{
					bmu_printk("enter 88105 charge end\n");
					charge_end_flag = 1;
					charge_end_process();
				}
			}
			else
			{
				charge_end_flag = 0;
			}

			if (charger_finish != 0)
			{
				if (charge_end_flag == 0)
				{
					if (voltage_gasgauge.m_fRsoc < 99)
					{
						voltage_gasgauge.m_fRsoc++;
						voltage_gasgauge.m_fStateOfCharge++;
					}
					else
					{
						bmu_printk("enter charger charge end\n");
						charge_end_flag = 1;
						gas_gauge.charge_end = 1;
						charge_end_process();
						charger_finish = 0;
					}
				}	//if (charge_end_flag == 0)
				else
				{
					charger_finish = 0;
				}
			}

			if (gas_gauge.discharge_end != 0)
			{
				if (discharge_end_flag == 0)
				{
					discharge_end_flag = 1;
					discharge_end_process();
				}
			}
			else
			{
				discharge_end_flag = 0;
			}

			//no CAR, no overflow
			//OZ88105_over_flow_prevent();

			/*
			//very dangerous
			if(batt_info.fVolt <= (config_data->discharge_end_voltage - num_100))
			{
				ret = afe_read_cell_volt(&batt_info->fVolt);
				if((ret >=0) && (batt_info->fVolt > 2500))
					discharge_end_process();
			}
			*/


			gas_gauge.bmu_tick++;
			//do_gettimeofday(&(time_x.time));
			//previous_loop_timex = DateTime.Now.ToBinary();//time_x.time.tv_sec;


			if (parameter_customer.debug)
			{
				bmu_printk("----------------------------------------------------\n");
				bmu_printk(string.Format("AAAA VERSION is {0}", VERSION));
				//bmu_printk(string.Format("UTC time :%d-%d-%d %d:%d:%d \n", rtc_time.tm_year + 1900, rtc_time.tm_mon, rtc_time.tm_mday, rtc_time.tm_hour, rtc_time.tm_min, rtc_time.tm_sec));
				//bmu_printk(string.Format("AAAA time_x is %d\n", time_x.time.tv_sec));
				bmu_printk(string.Format("AAAA gas_gauge.charge_time_increment is {0:d}", gas_gauge.charge_time_increment));

				//printk("AAAA charge_fcc_update is %d\n",gas_gauge.charge_fcc_update);
				//printk("AAAA discharge_fcc_update is %d\n",gas_gauge.discharge_fcc_update);
				bmu_printk(string.Format("AAAA batt_info.fVolt is {0:F2}  {1:d}", (batt_info.fVolt * OZ88105_cell_num), gas_gauge.bmu_tick));
				bmu_printk(string.Format("AAAA gas_gauge.volt_average_pre is {0:F2} ", gas_gauge.volt_average_pre));
				bmu_printk(string.Format("AAAA gas_gauge.volt_average_now is {0:F2}", gas_gauge.volt_average_now));
				bmu_printk(string.Format("AAAA batt_info.fRSOC is {0:F2}  {1:d}", batt_info.fRSOC, gas_gauge.bmu_tick));
				bmu_printk(string.Format("AAAA batt_info.chg_on is {0:d}", batt_info.chg_on));
				//printk("AAAA batt_info.sCaMAH is %d\n",batt_info.sCaMAH);
				//printk("AAAA sCtMAH1 is %d\n",gas_gauge.sCtMAH);
				//printk("AAAA sCtMAH2 is %d\n",gas_gauge.discharge_sCtMAH);
				//printk("AAAA fcc is %d\n",gas_gauge.fcc_data);
				//printk("AAAA batt_info.fRC is %d\n",batt_info.fRC);
				bmu_printk(string.Format("AAAA batt_info.fCurr is {0:F2}  {1:d}", batt_info.fCurr, gas_gauge.bmu_tick));
				bmu_printk(string.Format("AAAA batt_info.fCellTemp is {0:F2}  {1:d}", batt_info.fCellTemp, gas_gauge.bmu_tick));
				bmu_printk(string.Format("AAAA batt_info.i2c_error_times++ is {0:d}", batt_info.i2c_error_times));
				//printk("charger_finish is %d\n",charger_finish);
				//printk("gas_gauge.charge_end is %d\n",gas_gauge.charge_end);
				//printk("charge_end_flag is %d\n",charge_end_flag);
				//printk("gas_gauge.discharge_end is %d\n",gas_gauge.discharge_end);
				bmu_printk(string.Format("AAAA volt_gg fCurrent is {0:d}", voltage_gasgauge.m_fCurrent));
				bmu_printk(string.Format("AAAA volt_gg RSOC is {0:d}", voltage_gasgauge.m_fRsoc));
				bmu_printk(string.Format("AAAA volt_gg m_fStateOfCharge is {0:d}", voltage_gasgauge.m_fStateOfCharge));
				bmu_printk(string.Format("AAAA volt_gg m_fCoulombCount is {0:d}", voltage_gasgauge.m_fCoulombCount));
				//printk("AAAA volt_gg ResidualCapacity is %d\n", voltage_gasgauge.m_fResCap);
				bmu_printk(string.Format("AAAA volt_gg fFCC is {0:d}", voltage_gasgauge.m_fFCC));
				bmu_printk("----------------------------------------------------");
			}

			if (bmu_sleep_flag != 0)
			{
				bmu_sleep_flag = 0;
				if (charger_finish != 0)
				{
					bmu_printk("enter dc charge end\n");
					charge_end = 1;
					charge_end_process();
					charger_finish = 0;
				}
			}

			//data = bmu_read_data(BATT_CAPACITY);
			data = 0;
			bmu_read_data(BATT_CAPACITY, ref data);
			//if (batt_info.sCaMAH > (gas_gauge.fcc_data + gas_gauge.fcc_data * 0.01F - 1))//(full_charge_data))
			if (batt_info.sCaMAH > (myProject.dbDesignCp + (myProject.dbDesignCp * 0.01F)))//(full_charge_data))
			{
				bmu_printk(string.Format("big error sCaMAH is {0:d}, {1:F2}", batt_info.sCaMAH, (myProject.dbDesignCp + (myProject.dbDesignCp * 0.01F))));
				//batt_info.sCaMAH = (int)(gas_gauge.fcc_data + gas_gauge.fcc_data * 0.01F - 1);//full_charge_data;
				batt_info.sCaMAH = (int)(myProject.dbDesignCp + 0.5);
			}

			if (batt_info.sCaMAH < (gas_gauge.fcc_data * 0.01F - 1)) // 100 - 1))
			{
				//printk("big error sCaMAH is {0:d}",batt_info->sCaMAH);
				batt_info.sCaMAH = (int)(gas_gauge.fcc_data * 0.01F - 1); // 100 - 1;
			}

			/*
			//if (gas_gauge.fcc_data > (myProject.dbDesignCp * FCC_UPPER_LIMIT * 0.01F))/// 100))
			//{
				//gas_gauge.fcc_data = (int)(myProject.dbDesignCp * FCC_UPPER_LIMIT * 0.01F + 0.5F);// 100;
				//bmu_write_data(BATT_FCC, gas_gauge.fcc_data);

				//bmu_printk(string.Format("fcc error is {0:d}", gas_gauge.fcc_data));

			//}
			//if ((gas_gauge.fcc_data <= 0) || (gas_gauge.fcc_data < (myProject.dbDesignCp * FCC_LOWER_LIMIT * 0.01F)))// 100)))
			//{
				//gas_gauge.fcc_data = (int)(myProject.dbDesignCp * FCC_LOWER_LIMIT * 0.01F + 0.5F);// 100;
				//bmu_write_data(BATT_FCC, gas_gauge.fcc_data);

				//bmu_printk(string.Format("fcc error is {0:d}", gas_gauge.fcc_data));
			//}

			//if (parameter_customer.debug)
			//{
				//bmu_printk(string.Format("read from RAM batt_info->sCaMAH is {0:d}", data));
			//}
			*/
			if (data >= 0)
			{
				if (fRSOC_PRE != batt_info.fRSOC)
				{
					fRSOC_PRE = batt_info.fRSOC;
					bmu_write_data(BATT_CAPACITY, batt_info.sCaMAH);
					if (parameter_customer.debug)
						bmu_printk(string.Format("o2 back batt_info->sCaMAH num_1 is {0:d}", batt_info.sCaMAH));
					return;

				}

				if (vRSOC_PRE != voltage_gasgauge.m_fRsoc)
				{
					vRSOC_PRE = voltage_gasgauge.m_fRsoc;
					bmu_write_data(BATT_RSOC, voltage_gasgauge.m_fRsoc);
					if (parameter_customer.debug) 
						bmu_printk(string.Format("o2 back voltage_gasgauge.m_fRsoc num_1 is {0:d}", voltage_gasgauge.m_fRsoc));
					return;
				}

				if (vSOC_PRE != voltage_gasgauge.m_fStateOfCharge)
				{
					vSOC_PRE = voltage_gasgauge.m_fStateOfCharge;
					bmu_write_data(BATT_SOC, voltage_gasgauge.m_fStateOfCharge);
					if (parameter_customer.debug) 
						bmu_printk(string.Format("o2 back voltage_gasgauge.m_fStateOfCharge num_1 is {0:d}", voltage_gasgauge.m_fStateOfCharge));
					return;
				}

				if (((batt_info.sCaMAH - data) > (gas_gauge.fcc_data / 200)) || ((data - batt_info.sCaMAH) > (gas_gauge.fcc_data / 200)))
				{
					bmu_write_data(BATT_CAPACITY, batt_info.sCaMAH);
					if (parameter_customer.debug)
					{
						bmu_printk(string.Format("o2 back batt_info->sCaMAH 2 is {0:d}", batt_info.sCaMAH));
					}
				}
			}

			//wake up OZ88105 into FullPower mode
			//ret = afe_register_read_byte(OZ88105_OP_CTRL,&i);
			ret = 0;
			i = 0;
			//if(ParamOZ88105Status != null)
			if (GetCtrlValue(ref i))
			{
				//i = (byte)(int)ParamOZ88105Status.phydata;
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
				if (OZ88105_cell_num > 1)
				{
					//afe_register_write_byte(OZ88105_OP_CTRL,num_0x2c);
					//WriteRegFunction(ParamOZ88105Status, 0x2C);
					SetCtrlValue(0x2c);
				}
				else
				{
					//afe_register_write_byte(OZ88105_OP_CTRL,num_0x20);
					//WriteRegFunction(ParamOZ88105Status, 0x20);
					SetCtrlValue(0x20);
				}
				bmu_printk("OZ88105 wake up function\n");
			}

			/* OZ88105 has no current register
			//ret  = afe_read_board_offset(&data);
			ret = 0;
			if (ParamOZ88105BoardOffset != null)
			{
				data = (int)ParamOZ88105BoardOffset.phydata;
			}
			else
			{
				data = 0;
			}
			if (ret >= 0)
			{
				if ((data > 10) || (data <= 0))
				{
					bmu_printk(string.Format("OZ88105 board offset error is {0:d}", data));
					//afe_write_board_offset(7);
					data = 7;
					WriteRegFunction(ParamOZ88105BoardOffset, data);
				}
			}
			* */

			if (parameter_customer.debug)
			{
				bmu_printk(string.Format("test data is {0:d}", batt_info.sCaMAH));
			}

			//data = bmu_read_data(BATT_FCC);
			data = 0;
			bmu_read_data(BATT_FCC, ref data);

			if (data != gas_gauge.fcc_data)
			{
				bmu_write_data(BATT_FCC, gas_gauge.fcc_data);
				bmu_printk(string.Format("test {0:d}", gas_gauge.fcc_data));
			}
		}

		//(A150827)Francis,
		private void volt_gg_state()
		{
			//byte change_flg = 0;	//state about to change flag
			Int32 volt_old = 0;
			Int32 volt_avg = 0;	//four reading avg volt

			batt_info.chg_on = check_charger_status();

			//find voltage now
			if (batt_info.fVolt == 0)
				//afe_read_cell_volt(&batt_info.fVolt);
				batt_info.fVolt = 3800F;		//pseudo value
			if (batt_info.m_volt_2nd == 0)
				batt_info.m_volt_2nd = (int)(batt_info.fVolt + 0.5);
			if (batt_info.m_volt_1st == 0)
				batt_info.m_volt_1st = (int)(batt_info.fVolt + 0.5);

			volt_old = batt_info.m_volt_2nd;	//copy 2nd to temp value
			batt_info.m_volt_2nd = batt_info.m_volt_1st;	//copy 1st to 2nd
			batt_info.m_volt_1st = batt_info.m_volt_pre;	//copy pre to 1st
			batt_info.m_volt_pre = (int)(batt_info.fVolt + 0.5);		//copy now to pre
			volt_avg = (volt_old + batt_info.m_volt_pre +
				batt_info.m_volt_1st + batt_info.m_volt_2nd) * 100 / 4;
			//Update long average
			if (batt_info.m_volt_avg_long == 0)
				batt_info.m_volt_avg_long = (int)(batt_info.fVolt * 100 + 0.5);
			else
			{
				batt_info.m_volt_avg_long = (int)(((batt_info.m_volt_avg_long * 98 / 100) +
					(batt_info.fVolt * 2)) + 0.5);
			}

			/**************************************************************
			*	IF CHARGER STATUS IS ACTIVE
			***************************************************************/
			if (batt_info.chg_on != 0)
			{
				/*
				 * BELOW ARE CHARGER PRESENT PROCESSES
				 * DEFAULT WE ASSUME IN CHARGE_STATE, ONLY WHEN 
				 * 1. VOLTAGE DROP A LITTLE BIT, MEANS CHARGER REMOVED DURING TEST
				 * 2. VOLTAGE LONG AVG EQUALS TO CV VOLTAGE, MEANS FULLY CHARGED
				 */
				//By default, volt_avg will raise a little bit when charger attached
				if ((volt_avg > batt_info.m_volt_avg_long)
					//&& (ABS(volt_avg, batt_info.m_volt_avg_long) > 300))
					&& (Math.Abs(volt_avg - batt_info.m_volt_avg_long) > 300))
				{
					if (batt_info.chg_dsg_flag != CHARGE_STATE)
						batt_info.chg_dsg_flag = (byte)CHARGE_STATE;
				}

				//1. volt_avg drop more than 150, means charging stopped, but charger not removed
				if ((volt_avg < batt_info.m_volt_avg_long)
					//&& (ABS(volt_avg, batt_info.m_volt_avg_long) > 200))
					&& (Math.Abs(volt_avg - batt_info.m_volt_avg_long) > 200))
				{
					if (batt_info.chg_dsg_flag == CHARGE_STATE)
						batt_info.chg_dsg_flag = (byte)IDLE_STATE;

				}
				//2. volt_avg and volt_avg_long almost equal to CV voltage, means charger attached
				//if ((ABS(volt_avg, batt_info.m_volt_avg_long) < 100)
				//&& (ABS(volt_avg, config_data.charge_cv_voltage) < 100))
				if ((Math.Abs(volt_avg - batt_info.m_volt_avg_long) < 100)
					&& (Math.Abs(volt_avg - myProject.dbChgCVVolt) < 100))
				{
					if (batt_info.chg_dsg_flag != CHARGE_STATE)
						batt_info.chg_dsg_flag = (byte)CHARGE_STATE;
				}
				else
				{
					if (batt_info.chg_dsg_flag != CHARGE_STATE)
						batt_info.chg_dsg_flag = (byte)IDLE_STATE;
				}
			}
			/**************************************************************
			*	IF CHARGER STATUS IS INACTIVE
			***************************************************************/
			else
			{
				if (batt_info.chg_dsg_flag != DISCHARGE_STATE)
					batt_info.chg_dsg_flag = (byte)DISCHARGE_STATE;
			}
			if (parameter_customer.debug)
			{
				bmu_printk(string.Format("BBBB batt_info.chg_dsg_flag is {0:d}", batt_info.chg_dsg_flag));
				bmu_printk(string.Format("BBBB batt_info.m_volt_avg_long is {0:d}", batt_info.m_volt_avg_long));
				bmu_printk(string.Format("BBBB batt_info.volt_avg is{0:d}", volt_avg));
			}
		}

		private void average_voltage()
		{
			//find average voltage pre and now
			gas_gauge.volt_average_pre = gas_gauge.volt_average_now;

			if ((gas_gauge.volt_num_i < volt_num) && (gas_gauge.volt_average_pre <= 3000))
			{
				if (gas_gauge.volt_num_i == 0)
				{
					gas_gauge.volt_average_pre_temp = 0;
				}

				//if(ABS(batt_info.fVoltPrev, batt_info.fVolt) < 60 )
				if (Math.Abs(batt_info.fVoltPrev - batt_info.fVolt) < 60)
				{
					gas_gauge.volt_average_pre_temp += batt_info.fVolt;
					gas_gauge.volt_num_i++;
					//printk("111gas_gauge.volt_average_pre_temp is %d\n",gas_gauge.volt_average_pre_temp);
					//printk("111gas_gauge.volt_num_i is %d\n",gas_gauge.volt_num_i);
				}
				if (gas_gauge.volt_num_i == volt_num)
				{
					gas_gauge.volt_average_pre = gas_gauge.volt_average_pre_temp / volt_num;
					gas_gauge.volt_average_now = gas_gauge.volt_average_pre;
					gas_gauge.volt_num_i = 0;
					gas_gauge.volt_average_pre_temp = 0;
					//printk("222gas_gauge.volt_average_pre_temp is %d\n",gas_gauge.volt_average_pre_temp);
					//printk("222gas_gauge.volt_num_i is %d\n",gas_gauge.volt_num_i);
				}

			}
			else
			{
				//if(ABS(batt_info.fVoltPrev, batt_info.fVolt) < 60 )
				if (Math.Abs(batt_info.fVoltPrev - batt_info.fVolt) < 60)
				{
					gas_gauge.volt_average_now_temp += batt_info.fVolt;
					gas_gauge.volt_num_i++;
					//printk("333gas_gauge.volt_average_now_temp is %d\n",gas_gauge.volt_average_now_temp);
					//printk("333gas_gauge.volt_num_i is %d\n",gas_gauge.volt_num_i);
				}
				if (gas_gauge.volt_num_i == volt_num)
				{
					gas_gauge.volt_average_now = gas_gauge.volt_average_now_temp / volt_num;
					if ((batt_info.chg_dsg_flag == CHARGE_STATE) && (gas_gauge.volt_average_pre > gas_gauge.volt_average_now))
						gas_gauge.volt_average_now = gas_gauge.volt_average_pre;
					if ((batt_info.chg_dsg_flag == DISCHARGE_STATE) && (gas_gauge.volt_average_pre < gas_gauge.volt_average_now))
						gas_gauge.volt_average_now = gas_gauge.volt_average_pre;

					gas_gauge.volt_num_i = 0;
					gas_gauge.volt_average_now_temp = 0;
					/*
					capacity = gas_gauge.volt_average_pre - gas_gauge.volt_average_now;
					if(capacity > 0)
						gas_gauge.volt_average_pre = gas_gauge.volt_average_now;


					*/
					//printk("444gas_gauge.volt_average_now_temp is %d\n",gas_gauge.volt_average_now_temp);
					//printk("444gas_gauge.volt_num_i is %d\n",gas_gauge.volt_num_i);
				}

			}

		}

		//no CAR, no overflow prevent
		private void OZ88105_over_flow_prevent()
		{
			Int32 ret;
			Int32 data;

			if ((batt_info.fRSOC > 0) && (gas_gauge.discharge_end != 0))
				gas_gauge.discharge_end = 0;

			if ((batt_info.fRSOC < 100) && (gas_gauge.charge_end != 0))
				gas_gauge.charge_end = 0;

			if (batt_info.fRC < 0)
			{
				if (batt_info.fVolt >= (myProject.dbChgCVVolt - 200))//(config_data.charge_cv_voltage - 200))
				{
					batt_info.fRC = gas_gauge.fcc_data - 1;
				}
				else
				{
					if (batt_info.fRSOC > 0)
						batt_info.fRC = batt_info.fRSOC * gas_gauge.fcc_data * 0.01F - 1;
					else
						batt_info.fRC = batt_info.sCaMAH;
				}

				//write to
				//afe_write_car(batt_info.fRC);
				WriteRegFunction(ParamPhyCAR, batt_info.fRC);
				batt_info.fRCPrev = batt_info.fRC;
			}

			/*
			//ret = OZ88105_read_word(OZ88105_OP_CAR);

			//if(ret >= num_0)
			//{
				//ret = (int16_t)ret;
				//if(ret >= (num_32768 - num_10 * config_data.fRsense))
				//{
					//if(parameter.config.debug)printk("yyyy  CAR WILL UP OVER {0:d}",ret);
					//ret = 32768 - 15 * config_data.fRsense;
					//OZ88105_write_word(OZ88105_OP_CAR,(int16_t)ret);
					//afe_read_car(&batt_info.fRC);
					//batt_info.fRCPrev = batt_info.fRC;
				
				//}
				//else if(ret <= (num_10 * config_data.fRsense))
				//{
					//if(parameter.config.debug)printk("yyyy  CAR WILL DOWN OVER {0:d}",ret);
					//ret =  num_15 * config_data.fRsense;
					//OZ88105_write_word(OZ88105_OP_CAR,(int16_t)ret);
					//afe_read_car(&batt_info.fRC);
					//batt_info.fRCPrev = batt_info.fRC;	
				//}	
			//}	
			*/

			//
			//ret = afe_read_car(&data);
			ret = 0;
			if (ParamPhyCAR != null)
			{
				data = (int)ParamPhyCAR.phydata;
			}
			else
			{
				data = 0;
			}
			if (ret < 0)
				return;

			if (data < 5)
			{
				//wrtie to
				//afe_write_car(batt_info.sCaMAH);
				WriteRegFunction(ParamPhyCAR, batt_info.sCaMAH);
				batt_info.fRCPrev = batt_info.sCaMAH;
				batt_info.fRC = batt_info.sCaMAH;
				if (parameter_customer.debug)
				{
					bmu_printk(string.Format("dddd car down is {0:d}", data));
				}
			}
			else if (data > (32768 * 2.5F / (myProject.dbRsense * 1000) - 5)) /*parameter_customer.dbCARLSB*/ //config_data.fRsense - 5))
			{
				//write to
				//afe_write_car(batt_info.sCaMAH);
				WriteRegFunction(ParamPhyCAR, batt_info.sCaMAH);
				batt_info.fRCPrev = batt_info.sCaMAH;
				batt_info.fRC = batt_info.sCaMAH;
				if (parameter_customer.debug)
				{
					bmu_printk(string.Format("dddd car up is {0:d}", data));
				}

			}

			if (((batt_info.sCaMAH - data) > (myProject.dbDesignCp * 0.01)) || ((data - batt_info.sCaMAH) > (myProject.dbDesignCp * 0.01)))
			{
				if ((batt_info.sCaMAH < gas_gauge.overflow_data) && (batt_info.sCaMAH > 0))
				{
					//write to
					//afe_write_car(batt_info.sCaMAH);
					WriteRegFunction(ParamPhyCAR, batt_info.sCaMAH);
					batt_info.fRCPrev = batt_info.sCaMAH;
					batt_info.fRC = batt_info.sCaMAH;
					if (parameter_customer.debug)
					{
						bmu_printk(string.Format("dddd write car batt_info.fRCPrev is {0:F2}", batt_info.fRCPrev));
						bmu_printk(string.Format("dddd write car batt_info.fRC is {0:F2}", batt_info.fRC));
						bmu_printk(string.Format("dddd write car batt_info.sCaMAH is {0:d}", batt_info.sCaMAH));
					}
				}
			}
		}

		private void charge_end_process()
		{
			//FCC UPdate
			/*
			//if (gas_gauge.charge_fcc_update != 0)
			//{
				//if (batt_info.fCurr < myProject.dbChgEndCurr)//config_data.charge_end_current)
					//gas_gauge.fcc_data = gas_gauge.sCtMAH;
				//bmu_write_data(BATT_FCC, gas_gauge.fcc_data);

				//bmu_printk(string.Format("charge1 fcc update is {0:d}", gas_gauge.fcc_data));
			//}

			//if (gas_gauge.fcc_data > (myProject.dbDesignCp * FCC_UPPER_LIMIT * 0.01))//(config_data.design_capacity * FCC_UPPER_LIMIT / 100))
			//{
				//gas_gauge.fcc_data = (int)(myProject.dbDesignCp * FCC_UPPER_LIMIT * 0.01F + 1);//config_data.design_capacity * FCC_UPPER_LIMIT / 100;
				//bmu_write_data(BATT_FCC, gas_gauge.fcc_data);

				//bmu_printk(string.Format("charge2 fcc update is {0:d}", gas_gauge.fcc_data));

			//}
			//if ((gas_gauge.fcc_data <= 0) || (gas_gauge.fcc_data < (myProject.dbDesignCp * FCC_LOWER_LIMIT * 0.01F)))//(config_data.design_capacity * FCC_LOWER_LIMIT / 100)))
			//{
				//gas_gauge.fcc_data = (int)(myProject.dbDesignCp * FCC_LOWER_LIMIT * 0.01F + 1);//config_data.design_capacity * FCC_LOWER_LIMIT / 100;
				//bmu_write_data(BATT_FCC, gas_gauge.fcc_data);

				//bmu_printk(string.Format("charge3 fcc update is {0:d}", gas_gauge.fcc_data));
			//}
			* */

			voltage_gasgauge.m_fStateOfCharge = 100;
			voltage_gasgauge.m_fRsoc = 100;

			batt_info.sCaMAH = (int)(gas_gauge.fcc_data + gas_gauge.fcc_data * 0.01 + 0.5);//full_charge_data; ;
			bmu_write_data(BATT_CAPACITY, batt_info.sCaMAH);

			if (gas_gauge.ocv_flag != 0)
				bmu_write_data(OCV_FLAG, 0);

			bmu_printk("yyyy  end charge");
			batt_info.fRSOC = 100;
			gas_gauge.charge_end = 1;
			charge_end_flag = 1;
			charger_finish = 0;
			gas_gauge.charge_fcc_update = 0;// 1;
			gas_gauge.discharge_fcc_update = 1;
			gas_gauge.discharge_sCtMAH = 0;
			power_on_flag = 0;
		}

		private void discharge_end_process()
		{
			Int32 voltage_end = (int)(myProject.dbDsgEndVolt + 0.5);//config_data->discharge_end_voltage;

			//FCC UPdate
			if (gas_gauge.discharge_fcc_update != 0)
			{
				gas_gauge.fcc_data = gas_gauge.discharge_sCtMAH;
				bmu_write_data(BATT_FCC, gas_gauge.fcc_data);
				bmu_printk(string.Format("discharge1 fcc update is {0:d}", gas_gauge.fcc_data));

			}

			/* 
			//if (gas_gauge.fcc_data > (myProject.dbDesignCp * FCC_LOWER_LIMIT * 0.01F))//(config_data.design_capacity * FCC_UPPER_LIMIT / 100))
			//{
				//gas_gauge.fcc_data = (int)(myProject.dbDesignCp * FCC_UPPER_LIMIT * 0.01F + 0.5F);//config_data.design_capacity * FCC_UPPER_LIMIT / 100;
				//bmu_write_data(BATT_FCC, gas_gauge.fcc_data);
				//bmu_printk(string.Format("discharge2 fcc update is {0:d}", gas_gauge.fcc_data));
			//}
			//if ((gas_gauge.fcc_data <= 0) || (gas_gauge.fcc_data < (myProject.dbDesignCp * FCC_LOWER_LIMIT * 0.01F)))//(config_data.design_capacity * FCC_LOWER_LIMIT / 100)))
			//{
				//gas_gauge.fcc_data = (int)(myProject.dbDesignCp * FCC_LOWER_LIMIT * 0.01F + 0.5F);//config_data.design_capacity * FCC_LOWER_LIMIT / 100;
				//bmu_write_data(BATT_FCC, gas_gauge.fcc_data);
				//bmu_printk(string.Format("discharge3 fcc update is {0:d}", gas_gauge.fcc_data));
			//}
			*/

			if (gas_gauge.ocv_flag != 0)
				bmu_write_data(OCV_FLAG, 0);

			batt_info.sCaMAH = gas_gauge.fcc_data / 100 - 1;
			bmu_write_data(BATT_CAPACITY, batt_info.sCaMAH);

			//no CAR, no write to
			//afe_write_car(batt_info.sCaMAH);
			//WriteRegFunction(ParamPhyCAR, batt_info.sCaMAH);
			batt_info.fRCPrev = batt_info.sCaMAH;

			bmu_printk("yyyy  end discharge \n");
			batt_info.fRSOC = 0;
			gas_gauge.discharge_end = 1;
			gas_gauge.discharge_fcc_update = 0;
			gas_gauge.charge_fcc_update = 1;
			gas_gauge.sCtMAH = 0;
		}

		//no used
		private void charge_process_8806()
		{
			//uint8_t i;
			float capacity;
			float estimate_capacity;
			float data;
			//uint8_t catch_ok = 0;
			Int32 charge_voltage_end = (int)(myProject.dbChgCVVolt - 30);// = config_data->charge_cv_voltage - 30;
			float infVolt;
			float infCurr;
			Int32 voltage_end = (int)(myProject.dbDsgEndVolt); //config_data->discharge_end_voltage;
			float infCal, infCal_end, inf_reserve;
			byte rc_result = 0;
			Int32 system_ri;

			gas_gauge.discharge_end = 0;

			if (gas_gauge.charge_end != 0) return;//this must be here
			if (batt_info.fRSOC >= 100) return;

			system_ri = gas_gauge.batt_ri + gas_gauge.line_impedance;


			capacity = batt_info.fRC - batt_info.fRCPrev;
			if (parameter_customer.debug)
			{
				bmu_printk(string.Format("chg capacity: {0:F2}", capacity));

			}


			if (gas_gauge.charge_table_flag == 0)
			{

				if (parameter_customer.debug)
				{
					bmu_printk(string.Format("data: {0:F2},{1:F2},{2:F2}", batt_info.fVolt, batt_info.fCurr, batt_info.fCellTemp * 10));

				}

				if (parameter_customer.debug)
				{
					bmu_printk(string.Format("batt_ri {0:F2},{1:F2}", gas_gauge.batt_ri, gas_gauge.line_impedance));

				}

				if (parameter_customer.debug)
				{
					bmu_printk("-------------------------------------------------\n");

				}
				// calculate  voltage when current is 0
				infVolt = batt_info.fVolt - system_ri * batt_info.fCurr / 1000;

				if (parameter_customer.debug)
				{
					bmu_printk(string.Format("infVolt: {0:F2}", infVolt));

				}


				//if (batt_info.fCurr > yaxis_table[Y_AXIS - 1])
				if (batt_info.fCurr > myProject.GetRCTableHighCurr())
				{
					infCurr = myProject.GetRCTableHighCurr();
				}
				else
				{
					infCurr = batt_info.fCurr;
				}

				if (infCurr < myProject.GetRCTableLowCurr())
					infCurr = myProject.GetRCTableLowCurr();

				if (infCurr > gas_gauge.max_charge_current_fix)
					infCurr = gas_gauge.max_charge_current_fix;

				if (parameter_customer.debug)
				{
					bmu_printk(string.Format("infCurr: {0:F2}", infCurr));
				}

				// calculate  voltage when current is -curernt

				data = infVolt - system_ri * infCurr / 1000;

				if (data < myProject.GetRCTableLowVolt()) //xaxis_table[0])
				{
					infCurr = infCurr / 2;
					bmu_printk(string.Format("lower current infVolt: {0:F2},{1:F2}", infVolt, infCurr));
				}

				infVolt -= system_ri * infCurr / 1000;

				if (parameter_customer.debug)
				{
					bmu_printk(string.Format("infVolt: {0:F2}", infVolt));
				}

				// calculate  soc now
				//rc_result = OZ88105_LookUpRCTable(
				//infVolt,
				//infCurr,
				//batt_info.fCellTemp * 10,
				//&infCal);
				//infCal =  myProject.LutRCTable(infVolt, infCurr, batt_info.fCellTemp * 10);
				infCal = (infCurr * 10000) / myProject.dbDesignCp;
				infCal = myProject.LutRCTable((infVolt - infCurr * parameter_customer.fconnect_resist),
																	infCal, batt_info.fCellTemp * 10);
				infCal = infCal / myProject.dbDesignCp * 10000;

				if (parameter_customer.debug)
				{
					bmu_printk(string.Format("infCal data: {0:F2},{1:F2},{2:F2}", infVolt, infCurr, batt_info.fCellTemp * 10));
					bmu_printk(string.Format("infCal: {0:F2}", infCal));
				}
				if (parameter_customer.debug)
				{
					bmu_printk("-------------------------------------------------\n");
				}

				// calculate  end soc
				rc_result = 0;
				if (rc_result == 0)
				{
					//rc_result = OZ88105_LookUpRCTable(
					//voltage_end,
					//infCurr,
					//batt_info.fCellTemp * 10,
					//&infCal_end);
					//infCal_end = myProject.LutRCTable(voltage_end, infCurr, batt_info.fCellTemp * 10);
					infCal_end = (infCurr * 10000) / myProject.dbDesignCp;
					infCal_end = myProject.LutRCTable(voltage_end, infCal_end, batt_info.fCellTemp * 10);
					infCal_end = infCal_end / myProject.dbDesignCp * 10000;

					if (parameter_customer.debug)
					{
						bmu_printk(string.Format("end data: {0:F2},{1:F2},{2:F2}", voltage_end, infCurr, batt_info.fCellTemp * 10));
						bmu_printk(string.Format("end: {0:F2}", infCal_end));
					}

					if (parameter_customer.debug)
					{
						bmu_printk("-------------------------------------------------\n");
					}

					//-----------------------------------------------------------------------------------------------
					// calculate  reserve soc
					//infVolt = config_data.charge_cv_voltage -
					//system_ri * config_data.charge_end_current / 1000;
					infVolt = myProject.dbChgCVVolt - system_ri * myProject.dbChgEndCurr * 0.001F;

					infVolt -= system_ri * myProject.GetRCTableLowCurr() * 0.001F; //yaxis_table[0] / 1000;

					if (parameter_customer.debug)
					{
						bmu_printk(string.Format("reserve volt curr: {0:F2},{1:F2}", infVolt, myProject.GetRCTableLowVolt())); //yaxis_table[0]);
					}

					//rc_result = OZ88105_LookUpRCTable(
					//infVolt,
					//yaxis_table[0],
					//batt_info.fCellTemp * 10,
					//&inf_reserve);
					//inf_reserve = myProject.LutRCTable(infVolt, myProject.GetRCTableLowCurr(), batt_info.fCellTemp * 10);
					inf_reserve = myProject.LutRCTable((infVolt - (myProject.GetRCTableLowCurr() * parameter_customer.fconnect_resist / 1000)),
																			myProject.GetRCTableLowCurrCRate(), batt_info.fCellTemp * 10);	//current in RC table is positive value

					if (parameter_customer.debug)
					{
						bmu_printk(string.Format("data: {0:F2},{1:F2},{2:F2}", infVolt, myProject.GetRCTableLowCurr(), batt_info.fCellTemp * 10F));
						bmu_printk(string.Format("inf_reserve: {0:F2}", inf_reserve));
					}

					//inf_reserve = 10000 - inf_reserve;
					inf_reserve = (myProject.dbDesignCp - inf_reserve) / myProject.dbDesignCp * 10000;

					if (inf_reserve < 0)
						inf_reserve = 0;
					if (inf_reserve > gas_gauge.max_chg_reserve_percentage)
						inf_reserve = gas_gauge.max_chg_reserve_percentage;

					if (parameter_customer.debug)
					{
						bmu_printk(string.Format("inf_reserve: {0:F2},{1:d},{2:d}\n", inf_reserve, gas_gauge.max_chg_reserve_percentage, gas_gauge.fix_chg_reserve_percentage));
					}
					// calculate  reserve soc
					//-----------------------------------------------------------------------------------------------

					if (parameter_customer.debug)
					{
						bmu_printk("-------------------------------------------------\n");
					}

					if (infCal_end > infCal)
						infCal_end = infCal;

					if (parameter_customer.debug)
					{
						bmu_printk(string.Format("all: {0:F2},{1:F2},{2:F2}\n", infCal, infCal_end, inf_reserve));
					}

					estimate_capacity = infCal - infCal_end + inf_reserve + gas_gauge.fix_chg_reserve_percentage;
					if (estimate_capacity < 0)
						estimate_capacity = 0;

					if (parameter_customer.debug)
					{
						bmu_printk(string.Format("estimate: {0:F2}", estimate_capacity));
					}

					estimate_capacity = gas_gauge.fcc_data * estimate_capacity / 10000;
					if (parameter_customer.debug)
					{
						bmu_printk(string.Format("estimate r: {0:F2}", estimate_capacity));
					}

					estimate_capacity = gas_gauge.fcc_data - estimate_capacity;//remain capacity will reach full
					if (parameter_customer.debug)
					{
						bmu_printk(string.Format("estimate to full: {0:F2}", estimate_capacity));
					}

					if ((estimate_capacity <= 0) || (batt_info.sCaMAH >= gas_gauge.fcc_data))
						gas_gauge.charge_ratio = 0F;
					else
						gas_gauge.charge_ratio = 1000F * (gas_gauge.fcc_data - batt_info.sCaMAH) / estimate_capacity;

					//very dangerous  fast catch
					if ((batt_info.fCurr < (myProject.dbChgEndCurr * gas_gauge.fast_charge_step)) &&//(config_data.charge_end_current * gas_gauge.fast_charge_step)) &&
						(batt_info.fVolt >= charge_voltage_end) &&
						(batt_info.fCurr > gas_gauge.discharge_current_th))
					{
						if ((capacity <= 0) && (gas_gauge.charge_ratio > gas_gauge.start_fast_charge_ratio))
						{
							capacity = 1;
							if (parameter_customer.debug)
							{
								bmu_printk(string.Format("fast charge\n"));
							}
						}
					}
				}

				if (parameter_customer.debug)
				{
					bmu_printk(string.Format("chg_ratio: {0:F2}", gas_gauge.charge_ratio));
					//printk("-------------------------------------------\n");
				}

				if (gas_gauge.charge_ratio != 0)
				{
					gas_gauge.charge_table_flag = 1;
				}
				if (gas_gauge.charge_ratio > gas_gauge.charge_max_ratio)
				{
					gas_gauge.charge_ratio = gas_gauge.charge_max_ratio;
				}
				if (gas_gauge.charge_strategy != 1)
				{
					gas_gauge.charge_table_flag = 0;
				}
			}

			// normal counting	
			//if((capacity > 0) && (capacity < (10 *config_data.design_capacity / 100))&&(!catch_ok))
			if (capacity > 0)
			{
				if (gas_gauge.charge_table_flag != 0)
				{
					if (parameter_customer.debug)
					{
						bmu_printk(string.Format("chg sCaUAH bf: {0:d}", gas_gauge.charge_sCaUAH));
					}

					gas_gauge.charge_sCaUAH += (int)(capacity * gas_gauge.charge_ratio + 0.5F);

					/*
					if((gas_gauge.charge_sCaUAH /1000) < (gas_gauge.fcc_data / 100))
						batt_info.sCaMAH += gas_gauge.charge_sCaUAH /1000;
					*/
					batt_info.sCaMAH += gas_gauge.charge_sCaUAH / 1000;

					if (parameter_customer.debug)
					{
						bmu_printk(string.Format("add: {0:F1}", gas_gauge.charge_sCaUAH * 0.001F));

					}

					gas_gauge.charge_sCaUAH -= (gas_gauge.charge_sCaUAH / 1000) * 1000;
					gas_gauge.charge_table_flag = 0;

					if (parameter_customer.debug)
					{
						bmu_printk(string.Format("chg sCaUAH af: {0:d}", gas_gauge.charge_sCaUAH));
					}
				}
				else
				{
					batt_info.sCaMAH += (int)(capacity + 0.5F);
				}

				batt_info.fRSOC = batt_info.sCaMAH * 100;
				batt_info.fRSOC = batt_info.fRSOC / gas_gauge.fcc_data;

				if (parameter_customer.debug)
				{
					bmu_printk(string.Format("chg sCaMAH: {0:d}", batt_info.sCaMAH));
				}

				if ((gas_gauge.ocv_flag != 0) && (batt_info.fCurr >= gas_gauge.charge_end_current_th2))
				{
					if (batt_info.fRSOC >= 100)
					{
						batt_info.fRSOC = 99;
						batt_info.sCaMAH = gas_gauge.fcc_data - 1;
					}
				}
				else if (batt_info.fRSOC >= 100)
				{
					bmu_printk("new method end charge\n");
					gas_gauge.charge_end = 1;
				}
			}
		}

		private void charge_process()
		{
			byte i;
			float capacity;
			long charge_interval = 0;
			UInt32 data;
			byte catch_ok = 0;
			Int32 charge_voltage_end = (int)(myProject.dbChgCVVolt - 50 + 0.5);//config_data->charge_cv_voltage - 50;
			float taget_soc;
			float k, temp;
			TimeSpan Tsdifference;

			gas_gauge.discharge_end = 0;

			if (voltage_gasgauge.m_iSuspendTime != 0)
			{
				if (parameter_customer.debug)
				{
					bmu_printk(string.Format("nnnn wakeup time is {0:d}", voltage_gasgauge.m_iSuspendTime));
					//ioctl(devfd, OZ88105_IOCTL_SETLANG, yytest);
					/*sprintf(yytest,"nnnn wakeup voltage is %d\n", batt_info->fVolt);
					ioctl(devfd,OZ88105_IOCTL_SETLANG,yytest);
					sprintf(yytest,"nnnn charge_table MaxVoltage is %d\n", 
						charge_volt_table[gas_gauge->charge_voltage_table_num - 1].x);
					ioctl(devfd,OZ88105_IOCTL_SETLANG,yytest);*/
				}
				//Currently, Cobra doesn't support suspend mode;
				//charge_interval = gas_gauge.dt_time_now - voltage_gasgauge.m_iSuspendTime;
				charge_interval = 0;
				if (charge_interval <= 0) charge_interval = 0;
				voltage_gasgauge.m_iSuspendTime = 0;
				voltage_gasgauge.m_cPreState = 0;

				/*if((batt_info->fVolt < charge_volt_table[gas_gauge->charge_voltage_table_num - 1].x) 
					&& (batt_info->fVolt > 3000)) {
					taget_soc = one_latitude_table(gas_gauge->charge_voltage_table_num,charge_volt_table,
									batt_info->fVolt);
					if (config_data->debug) {
						sprintf(yytest,"nnnn wakeup SOC is %d\n", taget_soc);
						ioctl(devfd,OZ88105_IOCTL_SETLANG,yytest);
					}
					if (taget_soc > voltage_gasgauge->m_fRsoc) {
						voltage_gasgauge->m_fRsoc = taget_soc;
						voltage_gasgauge->m_fStateOfCharge = taget_soc;
						batt_info->fRSOC = voltage_gasgauge->m_fRsoc;
						batt_info->sCaMAH = batt_info->fRSOC * voltage_gasgauge->m_fFCC / num_100;
						voltage_gasgauge->m_fCoulombCount = voltage_gasgauge->m_fStateOfCharge * config_data->design_capacity;
						gas_gauge->sCtMAH = voltage_gasgauge->m_fCoulombCount * config_data->design_capacity;
						gas_gauge->discharge_sCtMAH = config_data->design_capacity - gas_gauge->sCtMAH; 
						batt_info->fRCPrev = batt_info->fRC;
						batt_info->fRC = (voltage_gasgauge->m_fRsoc) * voltage_gasgauge->m_fFCC / 100;
						gas_gauge->fcc_data = voltage_gasgauge->m_fFCC;
						return;
					}
					else {
						return;
					}
				}*/
			}

			if (gas_gauge.charge_end != 0) return;//this must be here


			/*capacity = batt_info->fRC -batt_info->fRCPrev;
			if(config_data->debug)
			{
				sprintf(yytest,"charge capacity is %d\n",capacity);
				ioctl(devfd,OZ88105_IOCTL_SETLANG,yytest);
			}

			batt_info->sCaMAH = batt_info->fRSOC * config_data->design_capacity / num_100;
			gas_gauge->sCtMAH = batt_info->sCaMAH; 
			gas_gauge->discharge_sCtMAH = config_data->design_capacity - batt_info->sCaMAH; 
			*/
			//**********************************************************************
			//	Update voltage_gasgauge members here
			//**********************************************************************

			if ((voltage_gasgauge.m_fCurrent != 0) || (voltage_gasgauge.m_fResCap != 0))
			{
				voltage_gasgauge.m_fCurrent = 0;
				voltage_gasgauge.m_fResCap = 0;
				voltage_gasgauge.m_fFCC = gas_gauge.fcc_data;
			}

			//batt_info->fRSOC = voltage_gasgauge->m_fRsoc;

			//find charge ratio
			if ((gas_gauge.volt_average_now < (charge_volt_data[charge_volt_data.Count -1].iVoltage))//charge_volt_table[gas_gauge.charge_voltage_table_num - 1].x)
				&& (gas_gauge.volt_average_now > 3000))
			{
				//printk("enter find gas_gauge.volt_average_now is %d\n",gas_gauge.volt_average_now);
				//taget_soc = one_latitude_table(gas_gauge.charge_voltage_table_num, charge_volt_table,
								//gas_gauge.volt_average_now);
				taget_soc = myProject.LutChargeTable(gas_gauge.volt_average_now) / 100;
				//k = find_one_latitude_table_k(gas_gauge.charge_voltage_table_num, charge_volt_table,
							//gas_gauge->volt_average_now);
				k = LutChargeVoltDataKvalue(gas_gauge.volt_average_now);

				gas_gauge.charge_ratio = k * taget_soc / voltage_gasgauge.m_fRsoc;
				if (parameter_customer.debug)
				{
					bmu_printk(string.Format("taget_soc is {0:F2},k is {1:F2},charge_ratio is {2:F2}", taget_soc, k, gas_gauge.charge_ratio));
					//ioctl(devfd, OZ88105_IOCTL_SETLANG, yytest);
				}
				if (gas_gauge.charge_ratio < 0)
					gas_gauge.charge_ratio = 0;
			}
			else if (gas_gauge.volt_average_now < 3000)
			{
				taget_soc = 0;
			}
			else
			{
				taget_soc = 100;
			}

			if (charge_interval == 0)
			{
				//gas_gauge.charge_time_increment += gas_gauge.dt_time_now - gas_gauge.dt_time_pre;
				Tsdifference = gas_gauge.dt_time_now.Subtract(gas_gauge.dt_time_pre);
				gas_gauge.charge_time_increment = Convert.ToInt64(Tsdifference.TotalSeconds);
			}
			else
				gas_gauge.charge_time_increment += charge_interval;

			if (gas_gauge.charge_time_increment >= parameter_customer.charge_soc_time_ratio)
			{
				while (gas_gauge.charge_time_increment >= parameter_customer.charge_soc_time_ratio)
				{
					gas_gauge.charge_time_increment -= parameter_customer.charge_soc_time_ratio;
					voltage_gasgauge.m_fRsoc++;
					voltage_gasgauge.m_fStateOfCharge++;
				}
				gas_gauge.charge_time_increment = 0;
				//voltage_gasgauge.m_fRsoc++;
				//voltage_gasgauge.m_fStateOfCharge++;
				bmu_printk("4444444 charge time udpate ");
				//ioctl(devfd, OZ88105_IOCTL_SETLANG, yytest);
			}

			//EOC condition, this may be different during each project
			if (voltage_gasgauge.m_fRsoc >= 100)
			{
				bmu_printk("4444444 charge time udpate");
				//ioctl(devfd, OZ88105_IOCTL_SETLANG, yytest);
				gas_gauge.charge_end = 1;
				voltage_gasgauge.m_fStateOfCharge = 100;

				voltage_gasgauge.m_fCoulombCount = (int)(voltage_gasgauge.m_fStateOfCharge * myProject.dbDesignCp + 0.5);
						//* config_data.design_capacity;
				batt_info.fRSOC = voltage_gasgauge.m_fRsoc;
				batt_info.sCaMAH = (int)(batt_info.fRSOC * voltage_gasgauge.m_fFCC / 100 + 0.5);
				gas_gauge.sCtMAH = voltage_gasgauge.m_fCoulombCount;
				gas_gauge.discharge_sCtMAH = (int)(myProject.dbDesignCp - gas_gauge.sCtMAH + 0.5);//config_data.design_capacity - gas_gauge.sCtMAH;
				return;
			}

			// normal counting
			// be careful power on but close screen.
			capacity = gas_gauge.volt_average_now - gas_gauge.volt_average_pre;
			bmu_printk(string.Format("444capacity is {0:F2}", capacity));
			//ioctl(devfd, OZ88105_IOCTL_SETLANG, yytest);
			if (capacity > 0)
			{
				if (parameter_customer.debug)
				{
					bmu_printk(string.Format("usoc before is {0:d}", gas_gauge.charge_usoc));
					//ioctl(devfd, OZ88105_IOCTL_SETLANG, yytest);
				}

				//fast
				if (voltage_gasgauge.m_fRsoc < taget_soc)
				{
					temp = taget_soc - voltage_gasgauge.m_fRsoc;
					if (temp > 2) temp = 2;
					if (gas_gauge.charge_ratio <= 100)
					{
						gas_gauge.charge_usoc += (int)(temp * capacity * 100 * taget_soc / voltage_gasgauge.m_fRsoc + 0.5);
						if (parameter_customer.debug)
						{
							bmu_printk(string.Format("1111discharge_usoc add {0:F2}", temp * capacity * 100 * taget_soc / voltage_gasgauge.m_fRsoc));
							//ioctl(devfd, OZ88105_IOCTL_SETLANG, yytest);
						}

					}
					else
					{
						gas_gauge.charge_usoc += (int)(temp * capacity * gas_gauge.charge_ratio + 0.5);
						if (parameter_customer.debug)
						{
							bmu_printk(string.Format("2222discharge_usoc add {0:d}", (int)(temp * capacity * gas_gauge.charge_ratio + 0.5)));
							//ioctl(devfd, OZ88105_IOCTL_SETLANG, yytest);
						}
					}
				}
				//slow
				else if (voltage_gasgauge.m_fRsoc > taget_soc)
				{
					temp = voltage_gasgauge.m_fRsoc - taget_soc;
					if (temp > 2) temp = 2;
					if (gas_gauge.charge_ratio >= 100)
					{
						gas_gauge.charge_usoc += (int)(100 * taget_soc / (voltage_gasgauge.m_fRsoc * temp) + 0.5);
						if (parameter_customer.debug)
						{
							bmu_printk(string.Format("3333charge_usoc add {0:d}", (int)(100 * taget_soc / (voltage_gasgauge.m_fRsoc * temp) + 0.5)));
							//ioctl(devfd, OZ88105_IOCTL_SETLANG, yytest);
						}

					}
					else
					{
						if ((gas_gauge.charge_ratio / temp) < 50)
							gas_gauge.charge_usoc += 50;
						else
							gas_gauge.charge_usoc += (int)(gas_gauge.charge_ratio / temp + 0.5);
						if (parameter_customer.debug)
						{

							bmu_printk(string.Format("4444charge_usoc add {0:d}", (int)(gas_gauge.charge_ratio / temp + 0.5)));
							//ioctl(devfd, OZ88105_IOCTL_SETLANG, yytest);
						}
					}
				}
				else
				{
					if (gas_gauge.charge_ratio > 150)
					{
						gas_gauge.charge_usoc += 80;
						if (parameter_customer.debug)
						{

							bmu_printk("5555charge_usoc add 80");
							//ioctl(devfd, OZ88105_IOCTL_SETLANG, yytest);
						}
					}
					else
					{
						gas_gauge.charge_usoc += (int)(gas_gauge.charge_ratio / 2 + 0.5);
						if (parameter_customer.debug)
						{

							bmu_printk(string.Format("6666charge_usoc add {0:d}", (gas_gauge.charge_ratio / 2 + 0.5)));
							//ioctl(devfd, OZ88105_IOCTL_SETLANG, yytest);
						}
					}
				}


				if (parameter_customer.debug)
				{

					bmu_printk(string.Format("usoc now is {0:d}", gas_gauge.charge_usoc));
					//ioctl(devfd, OZ88105_IOCTL_SETLANG, yytest);
				}

				if (gas_gauge.charge_usoc >= 1000)
				{
					gas_gauge.charge_time_increment = 0;
					voltage_gasgauge.m_fRsoc++;
					voltage_gasgauge.m_fStateOfCharge++;
				}


				gas_gauge.charge_usoc -= (gas_gauge.charge_usoc / 1000) * 1000;


				if (parameter_customer.debug)
				{

					bmu_printk(string.Format("usoc after is {0:d}", gas_gauge.charge_usoc));
					//ioctl(devfd, OZ88105_IOCTL_SETLANG, yytest);
				}
			}
			if (voltage_gasgauge.m_fRsoc >= 100)
			{
				voltage_gasgauge.m_fRsoc = 100;
			}
			if (voltage_gasgauge.m_fStateOfCharge >= 100)
			{
				voltage_gasgauge.m_fStateOfCharge = 100;
			}


			voltage_gasgauge.m_fCoulombCount = (int)(voltage_gasgauge.m_fStateOfCharge * myProject.dbDesignCp + 0.5);
					//* config_data.design_capacity;
			batt_info.fRSOC = voltage_gasgauge.m_fRsoc;
			batt_info.sCaMAH = (int)(batt_info.fRSOC * voltage_gasgauge.m_fFCC / 100 + 0.5);
			gas_gauge.sCtMAH = voltage_gasgauge.m_fCoulombCount;
			gas_gauge.discharge_sCtMAH = (int)(myProject.dbDesignCp - gas_gauge.sCtMAH+0.5);//config_data.design_capacity - gas_gauge.sCtMAH;

			bmu_printk(string.Format("usoc is {0:d}", gas_gauge.charge_usoc));
			//ioctl(devfd, OZ88105_IOCTL_SETLANG, yytest);
		}

		//no used
		private void discharge_process_8806()
		{
			float capacity;
			Int32 voltage_end = (int)myProject.dbDsgEndVolt;//config_data.discharge_end_voltage;
			byte rc_result = 0;
			float infCal, infCal_end;
			float infVolt;

			if (batt_info.fRSOC < 100)
				gas_gauge.charge_end = 0;

			/*
			//very dangerous
			//if(batt_info.fVolt <= (voltage_end - gas_gauge.dsg_end_voltage_th2))
			//{
				//gas_gauge.discharge_end = 1;
				//return;
			//}
			*/
			if (parameter_customer.debug)
			{
				bmu_printk(string.Format("discharge_end: {0:d},{1:d}", gas_gauge.discharge_end, gas_gauge.discharge_table_flag));

			}

			if (gas_gauge.discharge_end == 1) return;

			if ((gas_gauge.discharge_table_flag == 0) && (batt_info.fVolt < (myProject.dbChgCVVolt - 100)))//(config_data.charge_cv_voltage -100) ))
			{
				/*
				//rc_result = OZ88105_LookUpRCTable(
							//batt_info.fVolt,
							//-batt_info.fCurr * 10000 / config_data.design_capacity , 
							//batt_info.fCellTemp * 10, 
							//&infCal);
				 */
				if (parameter_customer.debug)
				{
					bmu_printk(string.Format("data: {0:F2},{1:F2},{2:F2}\n", batt_info.fVolt, -batt_info.fCurr, batt_info.fCellTemp * 10));
				}

				infVolt = batt_info.fVolt - gas_gauge.ri * batt_info.fCurr * 0.001F;// 1000;		
				//rc_result = OZ88105_LookUpRCTable(
				//infVolt,
				//-batt_info.fCurr,
				//batt_info.fCellTemp * 10,
				//&infCal);
				//infCal = myProject.LutRCTable(infVolt, -batt_info.fCurr, batt_info.fCellTemp * 10);
				infCal = (batt_info.fCurr * -10000) / myProject.dbDesignCp;
				infCal = myProject.LutRCTable((infVolt - batt_info.fCurr * parameter_customer.fconnect_resist),
																infCal, batt_info.fCellTemp * 10);
				infCal = infCal / myProject.dbDesignCp * 10000;
				rc_result = 0;

				if (parameter_customer.debug)
				{
					bmu_printk(string.Format("result: {0:d}", rc_result));
					bmu_printk(string.Format("infCal: {0:F2}", infCal));
					//printk("--------------------------\n");
					//
				}

				if (rc_result == 0)
				{
					//voltage_end maybe lower than rc table,this will be changed
					/*
					//rc_result = OZ88105_LookUpRCTable(
							//voltage_end,
							//-batt_info.fCurr * 10000 / config_data.design_capacity , 
							//batt_info.fCellTemp * 10, 
							//&infCal_end);
					*/
					//rc_result = OZ88105_LookUpRCTable(
					//voltage_end,
					//-batt_info.fCurr ,
					//batt_info.fCellTemp * 10,
					//&infCal_end);
					//infCal_end = myProject.LutRCTable(voltage_end, -batt_info.fCurr, batt_info.fCellTemp * 10);
					infCal_end = (batt_info.fCurr * -10000) / myProject.dbDesignCp;
					infCal_end = myProject.LutRCTable(voltage_end, infCal_end, batt_info.fCellTemp * 10);
					infCal_end = infCal_end / myProject.dbDesignCp * 10000;
					rc_result = 0;
					if (parameter_customer.debug)
					{
						bmu_printk(string.Format("result: {0:d}", rc_result));
						bmu_printk(string.Format("end: {0:F2}", infCal_end));
						//printk("----------------------\n");
						//
					}

					infCal = infCal - infCal_end;
					infCal = infCal * myProject.dbDesignCp * 0.0001F;//config_data.design_capacity * infCal / 10000; //remain capacity
					infCal += myProject.dbDesignCp * 0.01F;//config_data.design_capacity / 100 + 1;    // 1% capacity can't use

					if (infCal <= 0)
						gas_gauge.discharge_ratio = 0;
					else
						gas_gauge.discharge_ratio = 1000 * batt_info.sCaMAH / infCal;
					if (parameter_customer.debug)
					{
						bmu_printk(string.Format("target capacit: {0:F2}", infCal));
						bmu_printk(string.Format("rc_ratio: {0:d}", gas_gauge.discharge_ratio));
						//printk("----------------------------------------------------\n");
						//
					}

					gas_gauge.discharge_table_flag = 1;
					/*
					//if(gas_gauge.discharge_ratio != 0)
					//{
						//gas_gauge.discharge_table_flag = 1;
					//}	
					*/
					if (gas_gauge.discharge_strategy != 1)
					{
						gas_gauge.discharge_table_flag = 0;
					}

					/*
					//if((batt_info.fCellTemp < 0) && (batt_info.fCurr < -500))
					//{
						//infCal = 10000 - infCal_end;
						//infCal = config_data.design_capacity * infCal / 10000; //remain capacity
						//gas_gauge.discharge_ratio = 1000 * gas_gauge.fcc_data / infCal;
						//if(config_data.debug)
						//{
							//printk("change discharge_ratio is {0:d}",gas_gauge.discharge_ratio);
						//}
					//}
					*/

					if (gas_gauge.discharge_ratio > gas_gauge.discharge_max_ratio)
					{
						gas_gauge.discharge_ratio = gas_gauge.discharge_max_ratio;
					}
				}
			}


			/*
			//wait hardware
			//if(gas_gauge.ocv_flag || (batt_info.fVolt > (voltage_end + gas_gauge.dsg_end_voltage_hi)))
			//{
				//if((batt_info.fRSOC <= 0)&&(batt_info.fVolt > voltage_end )){
					//batt_info.fRSOC  = 1;
					//batt_info.sCaMAH = gas_gauge.fcc_data / 100;
					//gas_gauge.discharge_count = 0;
					////if(parameter.config.debug)printk("3333wait discharge voltage. is {0:d}",batt_info.sCaMAH);
					//return;
				//}
			//}
			*/

			//#if 0
			//wait hardware
			//if((batt_info.fRSOC <= 0)&&(batt_info.fVolt > voltage_end )){
			//batt_info.fRSOC  = 1;
			//batt_info.sCaMAH = gas_gauge.fcc_data / 100;
			//gas_gauge.discharge_count = 0;
			////if(parameter.config.debug)printk("3333wait discharge voltage. is {0:d}",batt_info.sCaMAH);
			//return;
			//}
			//End discharge
			//if(batt_info.fVolt <= voltage_end)
			//{
			//if((batt_info.fRSOC >= 2) && (!catch_ok)){
			//batt_info.sCaMAH -= gauge_adjust_blend(5);
			//if(batt_info.sCaMAH <= (gas_gauge.fcc_data / 100))
			//batt_info.sCaMAH = gas_gauge.fcc_data / 100;
			//batt_info.fRSOC = batt_info.sCaMAH  * 100;
			//batt_info.fRSOC = batt_info.fRSOC / gas_gauge.fcc_data;
			//if(batt_info.fRSOC <= 0){
			//batt_info.fRSOC = 1;
			//}
			//gas_gauge.discharge_count = 0;
			//catch_ok = 1;
			//return;
			//}
			//if ((gas_gauge.discharge_count >= 2)&& (!catch_ok))
			//{
			//gas_gauge.discharge_count = 0;
			//gas_gauge.discharge_end = 1;
			//return;
			//}
			//else
			//gas_gauge.discharge_count++;
			//}
			//else
			//gas_gauge.discharge_count = 0;
			//End discharge 2
			//if(batt_info.fVolt <= (voltage_end - gas_gauge.dsg_end_voltage_th1))
			//{
			//if (gas_gauge.dsg_count_2 >= 2)
			//{
			//gas_gauge.discharge_end = 1;
			//gas_gauge.dsg_count_2 = 0;
			//return;
			//}
			//else
			//gas_gauge.dsg_count_2++;
			//}
			//else
			//gas_gauge.dsg_count_2 = 0;

			//#endif

			// normal counting
			// be careful power on but close screen.
			capacity = batt_info.fRCPrev - batt_info.fRC;
			if (parameter_customer.debug)
			{
				bmu_printk(string.Format("dsg capacity: {0:F2}", capacity));

			}
			//if((capacity > 0) && (capacity < (2* config_data.design_capacity /100) ))
			if (capacity > 0)
			{
				if (gas_gauge.discharge_table_flag != 0)
				{
					if (parameter_customer.debug)
					{
						bmu_printk(string.Format("sCaUAH bf: {0:d}", gas_gauge.discharge_sCaUAH));
					}
					gas_gauge.discharge_sCaUAH += (int)(capacity * gas_gauge.discharge_ratio + 0.5F);
					////if((gas_gauge.discharge_sCaUAH /1000) < (gas_gauge.fcc_data / 100))

					if (gas_gauge.discharge_ratio <= 0)
						batt_info.sCaMAH -= gas_gauge.fcc_data / 100;
					else
						batt_info.sCaMAH -= gas_gauge.discharge_sCaUAH / 1000;

					if (parameter_customer.debug)
					{
						bmu_printk(string.Format("reduce: {0:F2}", gas_gauge.discharge_sCaUAH / 1000));
					}
					gas_gauge.discharge_sCaUAH -= (gas_gauge.discharge_sCaUAH / 1000) * 1000;
					gas_gauge.discharge_table_flag = 0;
					if (parameter_customer.debug)
					{
						bmu_printk(string.Format("sCaUAH af: {0:d}", gas_gauge.discharge_sCaUAH));
					}
				}
				else
				{
					batt_info.sCaMAH -= (int)(capacity + 0.5F);
				}

				batt_info.fRSOC = batt_info.sCaMAH * 100;
				batt_info.fRSOC = batt_info.fRSOC / gas_gauge.fcc_data;
				//(A150831)Francis, check is more than 100%, limit it
				if (batt_info.fRSOC > 100) batt_info.fRSOC = 100F;
				//(E150831)

				/*
				//if(batt_info.fRSOC <= 0){
					//batt_info.fRSOC = 1;
					//batt_info.sCaMAH = gas_gauge.fcc_data /100;
				//}
				*/

				if (parameter_customer.debug)
				{
					bmu_printk(string.Format("dsg sCaMAH: {0:d}", batt_info.sCaMAH));
				}

				/*
				//if(gas_gauge.ocv_flag || (batt_info.fVolt > (voltage_end + gas_gauge.dsg_end_voltage_hi)))
				//{
					//if((batt_info.fRSOC <= 0)&&(batt_info.fVolt > voltage_end ))
					//{
						//batt_info.fRSOC  = 1;
						//batt_info.sCaMAH = gas_gauge.fcc_data / 100;
						//gas_gauge.discharge_count = 0;
					//}
				//}
				//else if(batt_info.fRSOC <= 0)
				//{
					//gas_gauge.discharge_end = 1;
					//printk("7777 new method end discharge\n" );
				//}
				*/

				if (batt_info.fRSOC <= 0)
				{
					gas_gauge.discharge_end = 1;
					bmu_printk("7777 new method end discharge\n");
				}
			}
		}

		private void discharge_process()
		{
			float fcapacity;
			byte i = 0;
			byte catch_ok = 0;
			Int32 voltage_end = (int)(myProject.dbDsgEndVolt + 0.5);//config_data->discharge_end_voltage;
			float rc_result = 0;
			//float infCal, infCal_end;

			if (batt_info.fRSOC < 100)
				gas_gauge.charge_end = 0;

			/*
			//very dangerous
			if(batt_info.fVolt <= (voltage_end - gas_gauge.dsg_end_voltage_th2))
			{
				gas_gauge.discharge_end = 1;
				return;
			}
			*/

			if (gas_gauge.discharge_end != 0) return;

			rc_result = voltage_gasgauge_lookup();
			if (rc_result != 0)	//have result if nonzero returned
			{
				bmu_printk(string.Format("volt_gg_lookup result is {0:F2}", rc_result));
				//ioctl(devfd, OZ88105_IOCTL_SETLANG, yytest);
			}

			batt_info.fCurr = -voltage_gasgauge.m_fCurrent;
			batt_info.fRSOC = voltage_gasgauge.m_fRsoc;
			batt_info.fRCPrev = batt_info.fRC;
			batt_info.fRC = (voltage_gasgauge.m_fRsoc * 100 + voltage_gasgauge.m_fResCap) *
				voltage_gasgauge.m_fFCC / 10000;
			gas_gauge.fcc_data = voltage_gasgauge.m_fFCC;

			//wait hardware
			//	if((batt_info.fRSOC <= 0)&&(batt_info.fVolt > voltage_end )){
			//		batt_info.fRSOC  = 1;
			//		batt_info.sCaMAH = gas_gauge.fcc_data / 100;
			//		gas_gauge.discharge_count = 0;
			//		return;
			//	}

			//End discharge
			if (batt_info.fVolt <= voltage_end)
			{
				if ((batt_info.fRSOC >= 2) && (catch_ok == 0))
				{
					batt_info.sCaMAH -= (int)(gauge_adjust_blend(8) + 0.5);
					if (batt_info.sCaMAH <= (gas_gauge.fcc_data / 100))
						batt_info.sCaMAH = gas_gauge.fcc_data / 100;
					batt_info.fRSOC = batt_info.sCaMAH * 100;
					batt_info.fRSOC = batt_info.fRSOC / gas_gauge.fcc_data;
					if (batt_info.fRSOC <= 0)
					{
						batt_info.fRSOC = 1;
					}
					if (batt_info.fRSOC > voltage_gasgauge.m_fRsoc)
					{
						batt_info.fRSOC = voltage_gasgauge.m_fRsoc;
					}
					gas_gauge.discharge_count = 0;
					catch_ok = 1;
					return;

				}
				if ((gas_gauge.discharge_count >= 2) && (catch_ok == 0))
				{
					gas_gauge.discharge_count = 0;
					gas_gauge.discharge_end = 1;
					return;
				}
				else
					gas_gauge.discharge_count++;


			}
			else
				gas_gauge.discharge_count = 0;

			//End discharge 2
			if (batt_info.fVolt <= (voltage_end - gas_gauge.dsg_end_voltage_th1))
			{
				if (gas_gauge.dsg_count_2 >= 2)
				{
					gas_gauge.discharge_end = 1;
					gas_gauge.dsg_count_2 = 0;
					return;
				}
				else
					gas_gauge.dsg_count_2++;
			}
			else
				gas_gauge.dsg_count_2 = 0;

			// normal counting
			// be careful power on but close screen.
			//(M150911)Francis, in Jon's design, this will not be run, put it back
			fcapacity = batt_info.fRCPrev - batt_info.fRC;
			if((fcapacity > 0) && (fcapacity < (gas_gauge.fcc_data /100) ))
			{
				if(gas_gauge.discharge_table_flag != 0)  
				{
					gas_gauge.discharge_sCaUAH += (int)(fcapacity * gas_gauge.discharge_ratio+0.5);
					if((gas_gauge.discharge_sCaUAH /1000) < (gas_gauge.fcc_data / 100))
						batt_info.sCaMAH -= gas_gauge.discharge_sCaUAH /1000;
					gas_gauge.discharge_sCaUAH -= (gas_gauge.discharge_sCaUAH /1000) * 1000;			
					gas_gauge.discharge_table_flag = 0;
				}
				else             
				{
					batt_info.sCaMAH -=  (int)(fcapacity+0.5);
				}

				batt_info.fRSOC = batt_info.sCaMAH  * 100;
				batt_info.fRSOC = batt_info.fRSOC / gas_gauge.fcc_data ;
				if(batt_info.fRSOC <= 0){
					batt_info.fRSOC = 1;
					batt_info.sCaMAH = gas_gauge.fcc_data /100;
				}
				if(batt_info.fRSOC > voltage_gasgauge.m_fRsoc) {
					batt_info.fRSOC = voltage_gasgauge.m_fRsoc;
				}
			}
		}

		//new in OZ88105
		int voltage_gasgauge_lookup()
		{
			int loop = VOLTGG_CMP_LOOP;
			int ret = 0;
			CompareStatus cpResult, cpResultPrev = CompareStatus.CP_EQUAL;
			long	dRSOCPrev = voltage_gasgauge.m_fRsoc;
			float	fResult = 0;
			float	fSocTbl = 0, fSocEnd = 0, fSocCal = 0;
			long time_increment = 0;
			long	dFCCPrev = voltage_gasgauge.m_fFCC;
			float	fSocEndLast = 0;
			int maxcurr = (int)(myProject.GetRCTableHighCurr()+0.5);//yaxis_table[Y_AXIS - 1];
			TimeSpan Tsdifference;

			if (voltage_gasgauge.m_iSuspendTime == 0)
			{
				//time_increment = gas_gauge.dt_time_now - gas_gauge.dt_time_pre;
				Tsdifference = gas_gauge.dt_time_now.Subtract(gas_gauge.dt_time_pre);
				time_increment = Convert.ToInt64(Tsdifference.TotalSeconds);
				if (time_increment <= 0)	time_increment = 4;
			}
			else
			{
				//Currently, Cobra doesn't support suspend mode;
				//time_increment = gas_gauge.dt_time_now - voltage_gasgauge.m_iSuspendTime;
				time_increment = 0;
				if (time_increment <= 0)	time_increment = 4;
				if (voltage_gasgauge.m_fCurrent != 0) {	//clear suspendtime only after current predicted.
					voltage_gasgauge.m_iSuspendTime = 0;
					voltage_gasgauge.m_cPreState = 0;
				}
			}
			voltage_gasgauge.m_fMaxErrorSoc = (VOLTGG_MAX_ERR);
			//check 1st time here
			if (voltage_gasgauge.m_fCurrent == 0)
			{
				//ret = rc_lookup_i(batt_info.fVolt,
						//voltage_gasgauge.m_fStateOfCharge,
						//(batt_info.fCellTemp * 10),
						//&fResult);
				//if (ret)	return 1;
				fResult = myProject.LutRCTableCurrent(batt_info.fVolt, (voltage_gasgauge.m_fStateOfCharge*100), (batt_info.fCellTemp * 10));
				if(fResult == -1) return 1;
				voltage_gasgauge.m_fCurrent = (int)(fResult + 0.5);
				return 0;
			}

			//***********************************************************
			//not 1st here, update everything
			//***********************************************************

			//0. find EOD residual capacity according to latest predict current
			//ret = rc_lookup((config_data->discharge_end_voltage),
					//voltage_gasgauge->m_fCurrent,
					//(batt_info->fCellTemp * 10),
					//&fSocEnd);
			//if (ret)	return 1;
			fSocEnd = myProject.LutRCTable(myProject.dbDsgEndVolt, voltage_gasgauge.m_fCurrent, (batt_info.fCellTemp * 10));

			//1. Use latest predict current to coulomb count and calculate SOC
			fSocCal = voltage_gasgauge.m_fCurrent * time_increment / 3600;
			fSocCal = voltage_gasgauge.m_fCoulombCount * 100 - (fSocCal * 10000);
			fSocCal /= myProject.dbDesignCp;//config_data.design_capacity;

			//2. Use predict current to find RC table result
			//ret = rc_lookup(batt_info->fVolt,
					//voltage_gasgauge->m_fCurrent,
					//(batt_info->fCellTemp * 10),
					//&fSocTbl);
			//if (ret)	return 1;
			fSocTbl = myProject.LutRCTable(batt_info.fVolt, voltage_gasgauge.m_fCurrent, (batt_info.fCellTemp * 10));

			if (parameter_customer.debug)
			{
				bmu_printk("--------------------------\n");
				//ioctl(devfd,OZ88105_IOCTL_SETLANG,yytest);
				bmu_printk(string.Format("volt_gg TIME_INTERVAL is {0:d}", time_increment));
				//ioctl(devfd,OZ88105_IOCTL_SETLANG,yytest);
				bmu_printk(string.Format("volt_gg SOC_Result is {0:F2}, {1:F2}, {2:F2}", fSocTbl, fSocCal, fSocEnd));
				//ioctl(devfd,OZ88105_IOCTL_SETLANG,yytest);
				bmu_printk(string.Format("volt_gg dRSOCPrev is {0:d}", dRSOCPrev));
				//ioctl(devfd,OZ88105_IOCTL_SETLANG,yytest);
				bmu_printk(string.Format("volt_gg dFCCPrev is {0:d}", dFCCPrev));
				//ioctl(devfd,OZ88105_IOCTL_SETLANG,yytest);
				bmu_printk(string.Format("volt_gg fCurrent is {0:d}", voltage_gasgauge.m_fCurrent));
				//ioctl(devfd,OZ88105_IOCTL_SETLANG,yytest);
				bmu_printk("--------------------------\n");
				//ioctl(devfd,OZ88105_IOCTL_SETLANG,yytest);
			}
			//3. Compare RC lookup result and calculated SOC
			do {
				//compare float type table SOC and predict SOC
				cpResult = fCompare(ref fSocTbl, ref fSocCal,
						ref voltage_gasgauge.m_fMaxErrorSoc);

				if (cpResult == CompareStatus.CP_OVER) {
					//if table result over predicted, assume current drop
					voltage_gasgauge.m_fCurrent -= voltage_gasgauge.m_fCurrent_step;
					if (voltage_gasgauge.m_fCurrent < voltage_gasgauge.m_fCurrent_step)
						voltage_gasgauge.m_fCurrent = voltage_gasgauge.m_fCurrent_step;
					//ret = rc_lookup(batt_info.fVolt,
							//voltage_gasgauge.m_fCurrent,
							//(batt_info.fCellTemp * 10),
							//&fSocTbl);
					//if (ret)	return 1;
					fSocTbl = myProject.LutRCTable(batt_info.fVolt, voltage_gasgauge.m_fCurrent, (batt_info.fCellTemp * 10));
				}
				else if (cpResult == CompareStatus.CP_LESS) {
					//if table result under predicted, assume current raise
					voltage_gasgauge.m_fCurrent += voltage_gasgauge.m_fCurrent_step;
					/*don't limit max current here, to decrease SOC by bigger coulobcount
					if (voltage_gasgauge.m_fCurrent > yaxis_table[Y_AXIS-1])
						voltage_gasgauge.m_fCurrent = yaxis_table[Y_AXIS-1];*/
					if (voltage_gasgauge.m_fCurrent > 2000000000)
						voltage_gasgauge.m_fCurrent = 2000000000;
					//ret = rc_lookup(batt_info.fVolt,
							//voltage_gasgauge.m_fCurrent,
							//(batt_info.fCellTemp * 10),
							//&fSocTbl);
					//if (ret)	return 1;
					fSocTbl = myProject.LutRCTable(batt_info.fVolt, voltage_gasgauge.m_fCurrent, (batt_info.fCellTemp * 10));
				}
				else {
					//if equal in two serious comparison
				/*	if ((cpResult == CP_EQUAL) && (cpResultPrev == CP_EQUAL)) {
						ret = rc_lookup_i(batt_info->fVolt,
								voltage_gasgauge->m_fStateOfCharge,
								(batt_info->fCellTemp * 10),
								&fResult);
						if (ret)	return 1;
						voltage_gasgauge->m_fCurrent = (long)fResult;
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
							bmu_printk("Not equal CompareStatus.CP_EQUAL --------------------------");
							//ioctl(devfd,OZ88105_IOCTL_SETLANG,yytest);
							bmu_printk(string.Format("volt_gg SOC compare result is {0:d}", (byte)cpResult));
							//ioctl(devfd,OZ88105_IOCTL_SETLANG,yytest);
							bmu_printk(string.Format(string.Format("volt_gg SOC Result is {0:F2}, {1:F2}, {2:F2}",fSocTbl,fSocCal, fSocEnd)));
							//ioctl(devfd,OZ88105_IOCTL_SETLANG,yytest);
							bmu_printk(string.Format("volt_gg fCurrent is {0:d}",voltage_gasgauge.m_fCurrent));
							//ioctl(devfd,OZ88105_IOCTL_SETLANG,yytest);
							bmu_printk(string.Format("volt_gg MaxErr is {0:d}",voltage_gasgauge.m_fMaxErrorSoc));
							//ioctl(devfd,OZ88105_IOCTL_SETLANG,yytest);
							bmu_printk("--------------------------\n");
							//ioctl(devfd,OZ88105_IOCTL_SETLANG,yytest);
						}				
						//leave while loop, instead of increase m_fMaxErrorSoc,
						//this ensure kernel won't stuck in this while-loop
						//voltage_gasgauge.m_fMaxErrorSoc += VOLTGG_ERR_STEP;
				
						//force equal to proceed the calculation
						cpResult = CompareStatus.CP_EQUAL;
						//break;
					}
				}
				if (cpResult == CompareStatus.CP_EQUAL)
				{
					//if equal update absolute SOC and absoulte RC
					voltage_gasgauge.m_fCoulombCount -=  (int)(voltage_gasgauge.m_fCurrent * time_increment / 36 + 0.5);
					voltage_gasgauge.m_fStateOfCharge = (int)(voltage_gasgauge.m_fCoulombCount / myProject.dbDesignCp + 0.5);//config_data.design_capacity;

					//if equal find new FCC, RSOC and RC
					//1. Weight the fSocEnd to smooth new FCC and RSOC
					fSocEndLast = 10000 - (10000 * dFCCPrev / myProject.dbDesignCp);//config_data.design_capacity);
					if ((fSocEndLast == 0) || (voltage_gasgauge.m_fResCap < 0))
						voltage_gasgauge.m_fResCap = 0;

					//2. Calculate small increment according to the current
					if (voltage_gasgauge.m_fCurrent > maxcurr)
						fSocEndLast = maxcurr;
					else
						fSocEndLast = voltage_gasgauge.m_fCurrent;
					fSocEndLast = fSocEndLast * time_increment * 1000000 / 3600 / dFCCPrev;

					//3. Calculate new RSOC by small increment
					fResult = (dRSOCPrev * 10000 - fSocEndLast) / 100;
					if (fResult < 0)	fResult = 0;

					//4. Apply chase, and wait factor to small increment
					if (fResult > ((fSocTbl - fSocEnd) / (10000 - fSocEnd) * 10000)) {
						if ((fSocTbl > fSocEnd) && (fSocCal > fSocEnd))
						{
							bmu_printk(string.Format("volt_gg is chasing {0:F2}, {1:d}", fSocEndLast, time_increment));
							//ioctl(devfd,OZ88105_IOCTL_SETLANG,yytest);
							fSocEndLast *= (int)(((fSocCal - fSocEnd) / (fSocTbl - fSocEnd)) * 1.5 + 0.5);
						}
						voltage_gasgauge.m_fResCap += (int)(fSocEndLast+0.5);
					}
					else {
						bmu_printk(string.Format("volt_gg is waiting {0:F2}, {1:d}", fSocEndLast, time_increment));
						//ioctl(devfd,OZ88105_IOCTL_SETLANG,yytest);
						if (fSocTbl > fSocEnd)
							fSocEndLast *= ((fSocTbl - fSocEnd) / fResult);
						else
							fSocEndLast *= 0.25F;
						voltage_gasgauge.m_fResCap += (int)(fSocEndLast+0.5);
					}
					bmu_printk(string.Format("volt_gg delta is {0:F2}", fSocEndLast));
					//ioctl(devfd,OZ88105_IOCTL_SETLANG,yytest);

					voltage_gasgauge.m_fFCC = (int)(myProject.dbDesignCp - myProject.dbDesignCp * fSocEnd / 10000 + 0.5);
					// config_data->design_capacity -
							//config_data->design_capacity * fSocEnd / 10000;
			
					//EOD ADJUST
					if ((batt_info.fVolt <= (myProject.dbDsgEndVolt + 5))//(config_data.discharge_end_voltage + 5))
						&& (voltage_gasgauge.m_fRsoc >= 2))
					{
						voltage_gasgauge.m_fResCap += (int)(2000 * time_increment + 0.5);
					}
					if ((batt_info.fVolt > (myProject.dbDsgEndVolt + 5))//(config_data.discharge_end_voltage + 5))
						&& (dRSOCPrev < 2)) {
						voltage_gasgauge.m_fResCap = 0;
					}
					if ((batt_info.fVolt <= (myProject.dbDsgEndVolt))//config_data->discharge_end_voltage)
						&& (voltage_gasgauge.m_fRsoc <= 1)) {
						voltage_gasgauge.m_fResCap += 5000;
					}
					if (voltage_gasgauge.m_fResCap >= 10000)
					{
						if ((batt_info.fVolt > (myProject.dbDsgEndVolt))//config_data->discharge_end_voltage)
							&& (dRSOCPrev <= 1))
							dRSOCPrev = 2;
						voltage_gasgauge.m_fRsoc = (int)(dRSOCPrev - 1);
						voltage_gasgauge.m_fResCap -= 10000;
					}
					else
						voltage_gasgauge.m_fRsoc = (int)dRSOCPrev;

					if (voltage_gasgauge.m_fRsoc < 0)
						voltage_gasgauge.m_fRsoc = 0;

					bmu_printk(string.Format("volt_gg sCAUMAH is {0:d}", voltage_gasgauge.m_fResCap));
					//ioctl(devfd,OZ88105_IOCTL_SETLANG,yytest);
					bmu_printk(string.Format("volt_gg LOOP_COUNT is {0:d}", (VOLTGG_CMP_LOOP - loop)));
					//ioctl(devfd,OZ88105_IOCTL_SETLANG,yytest);
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
			float relativeError = ((fAvalue - fBvalue) / fBvalue) * 10000;

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
		private byte check_charger_status()
		{
			byte data = (byte)ParamOZ88105CtrlChgActive.phydata;

			if (ParamOZ88105Ctrlchgsel.phydata == 1)	//PRG pull to high, CHGON is low active
			{
				return (byte)((data == 1) ? 0 : 1);
			}
			else
			{
				return (byte)((data == 1) ? 1 : 0);
			}
			//if (data & CHG_SEL)	//PRG pull to high, CHGON is low active
				//return ((data & CHARGE_ON) ? 0 : 1);
			//else
				//return ((data & CHARGE_ON) ? 1 : 0);
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
			parameter_customer.charge_soc_time_ratio = 220;
			parameter_customer.suspend_current = 10;

			//(A150729)Francis, copy ri,bi from project file
			parameter_customer.fconnect_resist = myProject.dbRcon;
			parameter_customer.finternal_resist = myProject.dbRbat;
			//(E150729)

			//BATT_CAPACITY = "/data/sCaMAH.dat";
			//BATT_FCC 		= "/data/fcc.dat";
			//OCV_FLAG 		= "/data/ocv_flag.dat";
			//BATT_OFFSET 	= "/data/offset.dat";
			//CM_PATH 	   	= "/system/xbin/OZ88105api";

			//res_divider_ratio = 353 ;  // note: multiplied by 1000 

			//r1 = 220k,r2 = 120k,so 120 * 1000 / 120 + 220 = 353
			//r2's voltage is the voltage which OZ88105 sample.

			//For example :
			//Read OZ88105 voltage is vin
			//then the whole voltage is  vin * 1000 / res_divider_ratio;
		}

		public void bmu_init_gg()
		{
			gas_gauge.charge_table_num = myProject.GetChargePointsNo();
			gas_gauge.charge_voltage_table_num = CHARGE_VOLT_NUM;
			gas_gauge.rc_x_num = myProject.GetXAxisLengthofRCTable();
			gas_gauge.rc_y_num = myProject.GetWAxisLengthofRCTable();
			gas_gauge.rc_z_num = myProject.GetVAxisLengthofRCTable();
			gas_gauge.dt_time_now = DateTime.Now;
			//gas_gauge.discharge_max_ratio = config_data.design_capacity * 1000 / (100 * 2);
			//gas_gauge.discharge_current_th = DISCH_CURRENT_TH;
			//gas_gauge.ri = 18;
			//gas_gauge.ri = (int)myProject.dbRcon;
			//gas_gauge.batt_ri = (int)myProject.dbRbat;
		}


		#endregion

		#region porting oz8806_battery.c

		private void system_charge_discharge_status()
		{
			adapter_status = 0; //CHARGER_BATTERY
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

		private void charge_end_fun()
		{
			//if ((batt_info.fVolt >= (config_data.charge_cv_voltage - 50)) && (batt_info.fCurr >= DISCH_CURRENT_TH) &&
				//(batt_info.fCurr < config_data.charge_end_current) && (!charge_end_flag))
			//current value might get idle value, cause charger turn MOSFET off if reaching EndOfCharge
			if((batt_info.fVolt >=(myProject.dbChgCVVolt - 50)) && (batt_info.fCurr >= DISCH_CURRENT_TH) &&
				(get_charger_full_status()) && (charge_end_flag == 0 ))
			{
				charge_times++;
				//you must read 2times
				if (charge_times > 4)
				{
					charger_finish = 1;
					charge_times = 0;
					bmu_printk("enter exteral charger finish");
				}
			}
			if (charger_finish != 0 )
			{
				if (charge_end_flag == 0)
				{
					if (batt_info.fRSOC < 99)
					{
						if (batt_info.fRSOC <= fRSOC_PRE)
							batt_info.fRSOC++;

						if (batt_info.fRSOC > 100)
							batt_info.fRSOC = 100;

						batt_info.sCaMAH = (int)(batt_info.fRSOC * gas_gauge.fcc_data / 100 + 0.5);
						batt_info.sCaMAH += gas_gauge.fcc_data / 100 - 1;
						bmu_printk(string.Format("enter charger finsh update soc:{0:F2}", batt_info.fRSOC));
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

		//To get ChargerFull bit 
		private bool get_charger_full_status()
		{
			bool bRet = false;

			//if (ParamChargerChgFull != null)
			//{
				//if (ParamChargerChgFull.phydata != 0)
				//{
					//bRet = true;
					//bmu_printk("Charger Full");
				//}
			//}
			return bRet;
		}


		#endregion


	}
}
