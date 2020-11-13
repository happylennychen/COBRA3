using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using Cobra.Common;
using Cobra.EM;

namespace Cobra.GasGauge
{
	//index value of SBS dynamic/static data, this numberred is following SBS spec, and what Eagle/Patrol used now

	public class EagleGasGauge : GasGaugeInterface
	{
		#region private enum

		private enum GGFCCCase : byte
		{
			FCC_NORM		= 0x0A,
			FCC_CND			= 0x0B,
		}

		private enum BGStateenum : byte
		{
			//BG States: high 4 bits= main, low 4 bits = detail
			BGSTATE_MASK									=	0xF0,		// mask for major state
			BGSTATE_INIT											=	0x00,		// initializing - waiting for self calibration to complete
			BGSTATE_CHARGE								=	0x20,		// normal charge cycle - charge & discharge OK 
			BGSTATE_DISCHARGE						=	0x30,		// normal discharge cycle
			BGSTATE_IDLE										=	0x40,		// idle state,
			BGSTATE_SUSPEND							=	0x50,		// suspend state

			BGSTATE_SUBMASK							=	0x0F,		// mask for bgstate sub
			BGSTATE_SUBSTART							=	0x00,		//bgstate start
			BGSTATE_SUBRUN								=	0x01,		//bgstate runnning
			BGSTATE_SUBSTATE1						=	0x02,		//bgstate substate 1
			BGSTATE_SUBSTATE2						=	0x03,		//bgstate substate 2
			BGSTATE_SUBSTATE3						=	0x04,		//bgstate substate 3
			BGSTATE_SUBEND								=	0x0E,		//bgstate end

			BGSTATE_IDLE_START						=	0x40,		//idle state start
			BGSTATE_IDLE_RUN							=	0x41,		//idle stare running
			BGSTATE_IDLE_ENDED						=	0x4E,		//processing end of idle state

			BGSTATE_CHARGE_START				=	0x20,		//starting main charge		
			BGSTATE_CHARGE_RUN					=	0x21,		//running CC charge monitoring	
			BGSTATE_CHARGE_PRECHG			=	0x22,		//precharge state 	
			BGSTATE_CHARGE_PULSE				=	0x23,		//running pulsing charge monitoring
			BGSTATE_CHARGE_BALACE			=	0x24,		//running charge balancing
			BGSTATE_CHARGE_FULL					=	0x2A,		//processing full charge	
			BGSTATE_CHARGE_ENDED				=	0x2E,	//charge ended	

			BGSTATE_DISCHARGE_START			=	0x30,		//initialize discharge monitoring	
			BGSTATE_DISCHARGE_RUN				=	0x31,		//running discharge monitoring	
			BGSTATE_DISCHARGE_LOWVOLT	=	0x32,		//discharge lowvolt condition detected	
			BGSTATE_DISCHARGE_FULL			=	0x33,		//discharge empty - terminate discharge	
			BGSTATE_DISCHARGE_ENDED		=	0x3E,		//discharge ended	

			BGSTATE_SUSPEND_START				=	0x50,
			BGSTATE_SUSPEND_RUN					=	0x51,
			BGSTATE_SUSPEND_END					=	0x5E
		}

		private enum BGStatusenum : ushort
		{
			BG_STATUS_AFE_BUSY					=	0x0001,	//afe communication busy
			BG_STATUS_CHECKSUM				=	0x0002,	//checksum error
			BG_STATUS_DFCC_START			=	0x0004,
			BG_STATUS_THERM_STABLE		=	0x0008,	//bg DFCC condition, temperature stable

			BG_STATUS_SLEEPREADY			=	0x0010,	//bg sleep mode transition ready
			BG_STATUS_IDLE_2HOUR				=	0x0020,	//bg found idle more than 2 hour for GG
			BG_STATUS_SLEEP_2HOUR			=	0x0040,	//bg found sleep time more than 2 hour for GG

			BG_STATUS_BLEEDING					=	0x0100,	//bg charge cell balancing condition met flag
			BG_STATUS_PULSECHG					=	0x0200,	//bg pulse charge off FET flag
			BG_STATUS_CONDITION_DSG		=	0x0400,	//bg conditional discharge cycle flag
			BG_STATUS_CONDITION_CHG		=	0x0800,	//bg conditional charge cycle flag
			BG_STATUS_LOWVOLT					=	0x1000,	//bg discharge to low voltage state, rsoc = 0
			BG_STATUS_CVSTATE					=	0x2000,	//bg charge entered cv state
			BG_STATUS_ENDMA						=	0x4000,	//bg charge tape current checked
			BG_STATUS_PRECHG						=	0x8000,	//bg precharge condition met flag
		}

		private enum BGPwrenum : byte
		{
			AFE_PMODE_FULL					=	0x00,
			AFE_PMODE_SLEEP				=	0x01,
			AFE_PMODE_SHUTDOWN		=	0x02,

			FW_PMODE_FULL						=	0x00,
			FW_PMODE_SLEEP					=	0x01
		}

		private enum SBS16enum : ushort
		{
			SBS_BALARM_CFET_STATUS				=	0x8000,
			SBS_BALARM_DFET_STATUS				=	0x4000,
			SBS_BALARM_SOH				 					=	0x0400,
			SBS_BALARM_MASK				 				=	0xFF00,
			SBS_BSTAT_INITIALIZED							=	0x0080,
			SBS_BSTAT_DISCHARGING					=	0x0040,
			SBS_BSTAT_FULLY_CHARGED				=	0x0020,
			SBS_BSTAT_FULLY_DISCHARGED		=	0x0010,
			SBS_ERROR_NOAFE								=	0x0008,
			SBS_ERROR_AFECOMMUNICATION	=	0x0004,
			SBS_ERROR_EEPROMMAPPING			=	0x0002,
			SBS_ERROR_EEPROMCHECKSUM		=	0x0001,
			SBS_ERROR_MASK									=	0x000F,
		}

		private enum prShortIndex : byte
		{
			iBGState				= 0x00,			//BGState
			iBGPMode				= 0x01,			//Power Mode
			iBGStatus				= 0x02,			//BGStatus
			iMode						= 0x03,			//-1, 0, or 1, means discharge, idle, or charge
			iModeP					= 0x04,			//-1, 0, or 1, means discharge, idle, or charge
			iOneMinTmr			= 0x05,			//0~59, one minute timer
			iOneHrTmr				= 0x06,			//0~58, one hour time
			iThermStayCnt		= 0x07,			//0~9, thermal stable counter
			iGGMinTimer			= 0x08,			//GasGauge minutes timer
			iSeaElfRegx40		= 0x09,			//SeaElf Regx40 value
			iSeaElfRegx41		= 0x0a,			//SeaElf Regx41 value
			iSeaElfRegx42		= 0x0b,			//SeaElf Regx42 value
			iExtTempDK			= 0x10,			//in 0.1'K, save External Temperature
			//iIntTempDK			= 0x11,
			iTempStableDK	= 0x11,			//in 0.1'K, save Temprature Stable
			iStateTmrSec		= 0x12,			//in sec, state timer in second
			iStateTmrMin			= 0x13,			//in min, state timer in minute
			ifcc_therm				= 0x14,			//save temperature for DFCC condition
			iDBGCode				= 0x1F,			//debug code value
		}

		private enum prFloatIndex : byte
		{
			iVoltage					= 0x00,			//in mV, save voltage value from chip
			iVoltDiff					= 0x01,			//in mV, save the difference of voltage comparing to last voltage
			iCurrent					= 0x02,			//in mA, save current value from chip
			iCurrDiff					= 0x03,			//in mA, save the difference of current comparing to last current
			iTempVolt				= 0x04,			//in mV, save temperature value from chip
			//iTempDiff				= 0x05,			//in mV, save the difference of temperature comparing to last temperature
			iCAR						= 0x06,			//in mAhr, save CAR value from chip
			iCarDiff					= 0x07,			//in mAhr, save the difference of CAR comparing to last CAR
			iOCVolt					= 0x08,			//in mV, read OCV value from chip
			iOCVbit					= 0x09,			//in bool, 1 : PoOCV, 0 is SleepOCV
			//iPercent					= 0x0a,			//in % format, if = 23 means 0.23, percentage is calculated in sbd_update_sbs()
			//iMode						= 0x0b,			//-1, 0, or 1, means discharge, idle, or charge
			iAgeMah					= 0x0a,			//in mAhr, Age accumulated
			iOCVCompsate	= 0x0b,			//in mAhr, OCV compensation bass
			iCTMah					= 0x10,			//in mAhr, capacity value of coulomb counting, it's same sa iCAR
			iCAMah					= 0x0c,			//in mAhr, capacity value after adjustment
			iPrevCAMah			= 0x0d,			//in mAhr, previous capacity value before calculation
			iCRMah					= 0x0e,			//in mAhr, capacity residual keeper after full dsg
			iSelfMah					= 0x0f,			//in mAhr, Self discharge capacity
			//iDesignCapacity	= 0x10,			//in mAhr, Design Capacity
			iFCC						= 0x11,			//in mAhr, FullChargeCapacity
			//iRsense					= 0x12,			//in mOhm, Current Sense Resistor
			//iExtTempPullR		= 0x13,			//in Ohm, ExtTemp Pullup Resistor
			//iExtTempPullV		= 0x14,			//in mV, ExtTemp Pullup Voltage
			iAgeFactor				= 0x12,			//in float number, Age Factor
			iChgCurrTh			= 0x13,			//in mA, charge current threshold
			iDsgCurrTh			= 0x14,			//in mA discharge current threshold
			iRCVoltMax			= 0x15,			//in mV, Max Voltage value in RC table
			iRCVoltMin				= 0x16,			//in mV, Min Voltage value in RC table
			iDsgRatio				= 0x17,			//in float, ratio value to chase RC table value
			iFChgEndCurr		= 0x18,			//in mA, EndOfCharge current value
			iFChgTimeout		= 0x19,			//in Sec, Fully Charge End Time
			iFChgCV				= 0x1a,			//in mV, CV mode threshold
			//iFChgReset			= 0x1b,			//in %, reset fully charged bit
			iDsgZero				= 0x1b,			//in mV, EndOfDischarge voltage
			iRCTableHighV		= 0x1c	,			//in mV, the highest voltage of RC table
			iRCTableLowV		= 0x1d,			//in mV, the lowest voltage of RC table
			iChgStartRC			= 0x1e,			//in mAhr, RC value when charge start
			iCAUMah				= 0x1f,				//in 1000 times of delta RC value
		}

		private enum SupportID : byte
		{
			IDOZ8805 = 0x31,
			IDOZ8806	= 0x38
		}

		private enum VBusSysReg : byte
		{
			VBUSOVP		= 0x40,
			VBUSOK		= 0x20,
			VBUSUVP		= 0x10,
			VSYSOVP		= 0x01,
		}

		private enum ChargerReg : byte
		{
			CHARGERCCTIMEREXPIRED		= 0x40,
			CHARGERWKTIMEREXPIRED		= 0x20,
			CHARGERFULLYCHARGED			= 0x10,
			CHARGERCVSTATE						= 0x08,
			CHARGERCCSTATE						= 0x04,
			CHARGERWKSTATE						= 0x02,
			CHARGERINITIAL							= 0x01,
		}

		private enum THMReg : byte
		{
			THMICINTERNALOT		= 0x40,
			THMBIGGERT5					= 0x20,
			THMBETWT5T4				= 0x10,
			THMBETWT4T3				= 0x08,
			THMBETWT3T2				= 0x04,
			THMBETWT2T1				= 0x02,
			THMSMLERT1					= 0x01,
		}

		#endregion

		#region private members

		//private byte[] yRam = new byte[32];
		//private UInt16[] wRam = new UInt16[32];
		//private bool bFranTestMode = false;
		private bool bDEMAccess = true;		//test flag, if true use DEM to get ADC data from chip; if false, assign a fake value
		private bool bSeaElfSupport = true;	//test flag, true: has SeaElf chip to communicate
		private bool bDFCCSupport = true;	//as a flag that much easier to add/remove DFCC algorithm
		private byte yOZ8806SubSec = 0;		//subsection definition, same as XML, used in DEM
		private byte yOZSeaElfSubSec = 1;	//sunsection definition, same as XML, used in DEM
		private byte ySeaElfSubSec = 1;
		private UInt32 uGGErrorcode = LibErrorCode.IDS_ERR_SUCCESSFUL;
		private float[] fRam = new float[0x20];
		private short[] sRam = new short[0x20];
		//private float[] fSBS = new float[0x80];
		private UInt16 uFlag;
		private System.Windows.Threading.DispatcherTimer tmrFirmware = new System.Windows.Threading.DispatcherTimer();
		private Device devParent { get; set; }
		private TASKMessage tskMsgParent = null;
		//private ParamContainer pmcGGPolling = null;
		//private ParamContainer pmcGGSetting = null;
		//private ParamContainer pmcSBSReg = null;
		private AsyncObservableCollection<Parameter> PListGGPolling = null;
		private AsyncObservableCollection<Parameter> PListGGSetting = null;
		private AsyncObservableCollection<Parameter> PListSBSReg = null;
		private ParamContainer pmcAll = new ParamContainer();
		private GasGaugeProject myProject = null;
		private Parameter DBGCodeparamter = null;
		private Parameter SeaElfStatusRegx40 = null;
		private Parameter SeaElfStatusRegx41 = null;
		private Parameter SeaElfStatusRegx42 = null;

		//Eagle Gas Gague variable
		private Int16 rc_crate_factor = -10000;
		private float[] fEcMAH = new float[4];
		private bool GLOBAL_STATUS_CONDITIONAL = false;		//not all GLOBAL_STATUS are used, so declare one by one if we need it
		private int IDX_GPM_W_DFCCTEMP = 200;
		private long bmutick = 0;

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
			BG_DISCHARGE		= -1,
			BG_QUIET					= 0,
			BG_CHARGE			= 1,
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

		#endregion

		#region public members

		public enum Definition
		{
			MaxNumber = 20
		}

		//public byte yDBGCode {get; set;}

		#endregion

		#region public methods

		public EagleGasGauge()
		{
			devParent = null;
			tskMsgParent = null;
			sRam[(int)prShortIndex.iDBGCode] = 0x70;
			//for (int i = 0; i < 20; i++)
			//{
				//yRam[i] = 0;
				//wRam[i] = 0;
				//fRam[i] = 0F;
			//}
			tmrFirmware.Tick += new EventHandler(tmrFirmware_Elapsed);

			//MessageBox.Show("GG Construct");
		}

		//public bool InitializeGG(object deviceP, TASKMessage taskP, ParamContainer polling, ParamContainer setting, ParamContainer sbsreg, List<string> projtable = null)
		public bool InitializeGG(object deviceP, TASKMessage taskP, 
												AsyncObservableCollection<Parameter> PPolling, 
												AsyncObservableCollection<Parameter> PSetting, 
												AsyncObservableCollection<Parameter> PSBSreg, 
												List<string> projtable = null)
		
		{
			bool bInit = true;
			UInt32 uErrorcode = LibErrorCode.IDS_ERR_SUCCESSFUL;

			#region setup project file string
			try
			{
				devParent = (Device)deviceP;
				tskMsgParent = taskP;
				//pmcGGPolling = polling;
				//pmcGGSetting = setting;
				//pmcSBSReg = sbsreg;
				PListGGPolling = PPolling;
				PListGGSetting = PSetting;
				PListSBSReg = PSBSreg;
				//strPrjFile = FolderMap.m_currentproj_folder + filename;
				//for debug convinient, create  string list
				if (taskP == null)
				{
					taskP.errorcode = LibErrorCode.IDS_ERR_EGDLL_TASKMSG_NULL;
					return false;
				}
				if (deviceP == null)
				{
					taskP.errorcode = LibErrorCode.IDS_ERR_EGDLL_DEVICE_NULL;
					return false;
				}
				if (PListGGPolling == null)
				{
					taskP.errorcode = LibErrorCode.IDS_ERR_EGDLL_GGPOLLING_NULL; 
					return false;
				}
				if (PListGGSetting == null)
				{
					taskP.errorcode = LibErrorCode.IDS_ERR_EGDLL_GGSETTING_NULL;
					return false;
				}
				if (PListSBSReg == null)
				{
					taskP.errorcode = LibErrorCode.IDS_ERR_EGDLL_SBSREG_NULL;
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
					string tmpOCVbyTSOC = new string("OCVbySOC_BL216_Lenovo_3050mAh_V002_4350-3300mV_65p_04282014.txt".ToCharArray());
					projtable.Add(tmpOCVbyTSOC);
					//string tmpTSOCbyOCV = new string("C:\\Documents and Settings\\francis.hsieh\\My Documents\\COBRA Documents\\KillerWhaleFY\\OZBattery\\Tables\\TSOCbyOCV_SANYO_UR18650F-SCUD_2200mAH_5_V001_03292006.txt".ToCharArray());
					string tmpTSOCbyOCV = new string("SOCbyOCV_BL216_Lenovo_3050mAh_V002_4350-3300mV_16mV_04282014.txt".ToCharArray());
					projtable.Add(tmpTSOCbyOCV);
					//string tmpRCTable = new string("C:\\Documents and Settings\\francis.hsieh\\My Documents\\COBRA Documents\\KillerWhaleFY\\OZBattery\\Tables\\RC_SANYO_UR18650F-SCUD_2200mAH_V003_03282006.txt".ToCharArray());
					string tmpRCTable = new string("RC_LENOVO_BL216_3050mAh_4300-3300mV_V004_05092014.txt".ToCharArray());
					projtable.Add(tmpRCTable);
					//string tmpThermalTable = new string("C:\\Documents and Settings\\francis.hsieh\\My Documents\\COBRA Documents\\KillerWhaleFY\\OZBattery\\Tables\\Thermal_Semitec_103JT025_V002_01142011.txt".ToCharArray());
					string tmpThermalTable = new string("Thermal_Semitec_103JT025_V002_01142011.txt".ToCharArray());
					projtable.Add(tmpThermalTable);
					//string tmpSelfDsgTable = new string("C:\\Documents and Settings\\francis.hsieh\\My Documents\\COBRA Documents\\KillerWhaleFY\\OZBattery\\Tables\\Selfdis_O2_Generic_V002_09282005.txt".ToCharArray());
					string tmpSelfDsgTable = new string("Selfdis_O2_Generic_V002_09282005.txt".ToCharArray());
					projtable.Add(tmpSelfDsgTable);
					//string tmpRITable = new string("C:\\Documents and Settings\\francis.hsieh\\My Documents\\COBRA Documents\\KillerWhaleFY\\OZBattery\\Tables\\Reoc(T)_BAK_18650_2200mAh_12182013 sqr B6.txt".ToCharArray());
					//string tmpRITable = new string("RI(T)_LENOVO_BL216_3050mAh_70-40%_003_04222014.txt".ToCharArray());
					string tmpRITable = new string("Roec(T)_LENOVO_BL216_3050mAh_003_04222014.txt".ToCharArray());
					projtable.Add(tmpRITable);
				}
				sRam[(int)prShortIndex.iDBGCode] = 0x71;
				myProject = new GasGaugeProject(projtable);
				sRam[(int)prShortIndex.iDBGCode] = 0x72;
				bInit = myProject.InitializeProject(ref uErrorcode);
				if (!bInit)
				{
					taskP.errorcode = uErrorcode;
					return bInit;
				}
			}
			catch (Exception)
			{
				return bInit;
			}
			#endregion

			#region Eagle Gas Gauge initialization
			sRam[(int)prShortIndex.iDBGCode] = 0x80;
			if ((devParent != null) && (tskMsgParent != null) && 
				//(pmcGGPolling != null) && (pmcGGSetting != null) && (pmcSBSReg != null))
				(PListGGPolling != null) && (PListGGSetting != null) && (PListSBSReg != null))
			{
				bInit &= bg_init();
				if (!bInit) return bInit;

				sRam[(int)prShortIndex.iDBGCode] = 0x81;
				DBGCodeparamter.errorcode = (UInt16)sRam[(int)prShortIndex.iDBGCode];
				uGGErrorcode = DBGCodeparamter.errorcode;
				//eflash_init	();			//no need to do

				sRam[(int)prShortIndex.iDBGCode] = 0x82;
				DBGCodeparamter.errorcode = (UInt16)sRam[(int)prShortIndex.iDBGCode];
				uGGErrorcode = DBGCodeparamter.errorcode;
				//wRam[0] = 0;
				bInit &= gdm_init();
				if (!bInit) return bInit;

				sRam[(int)prShortIndex.iDBGCode] = 0x83;
				DBGCodeparamter.errorcode = (UInt16)sRam[(int)prShortIndex.iDBGCode];
				uGGErrorcode = DBGCodeparamter.errorcode;
				//bInit &=gpio_init();		//no need to do
				//bInit &= myProject.ReadTwoAxisTableContent();		//tables save in myProject
				//if (!bInit) return bInit;

				sRam[(int)prShortIndex.iDBGCode] = 0x84;
				DBGCodeparamter.errorcode = (UInt16)sRam[(int)prShortIndex.iDBGCode];
				uGGErrorcode = DBGCodeparamter.errorcode;
				//led_force_ignite(LED_ALL_OFF);	//no need to do

				sRam[(int)prShortIndex.iDBGCode] = 0x85;
				DBGCodeparamter.errorcode = (UInt16)sRam[(int)prShortIndex.iDBGCode];
				uGGErrorcode = DBGCodeparamter.errorcode;
				//smb_init();				//no need to do

				sRam[(int)prShortIndex.iDBGCode] = 0x86;
				DBGCodeparamter.errorcode = (UInt16)sRam[(int)prShortIndex.iDBGCode];
				uGGErrorcode = DBGCodeparamter.errorcode;
				//smb_pec_enable(ENABLE);		//no need to do
				//WriteChipsReg(yOZ8806SubSec, 0x08, 0x02, 0x01);	//force PEC enable

				sRam[(int)prShortIndex.iDBGCode] = 0x87;
				DBGCodeparamter.errorcode = (UInt16)sRam[(int)prShortIndex.iDBGCode];
				uGGErrorcode = DBGCodeparamter.errorcode;
				//err_init(EEPROM_EN);			//no need to do

				sRam[(int)prShortIndex.iDBGCode] = 0x88;
				DBGCodeparamter.errorcode = (UInt16)sRam[(int)prShortIndex.iDBGCode];
				uGGErrorcode = DBGCodeparamter.errorcode;
				//i2c_init(SCL_PIN, SDA_PIN);		//no need to do

				sRam[(int)prShortIndex.iDBGCode] = 0x89;
				DBGCodeparamter.errorcode = (UInt16)sRam[(int)prShortIndex.iDBGCode];
				uGGErrorcode = DBGCodeparamter.errorcode;
				//log_sys_evt();			//no need to do

				sRam[(int)prShortIndex.iDBGCode] = 0x8a;
				DBGCodeparamter.errorcode = (UInt16)sRam[(int)prShortIndex.iDBGCode];
				uGGErrorcode = DBGCodeparamter.errorcode;
				//bg_clr_reset();		//no need to do

				sRam[(int)prShortIndex.iDBGCode] = 0x8b;
				DBGCodeparamter.errorcode = (UInt16)sRam[(int)prShortIndex.iDBGCode];
				uGGErrorcode = DBGCodeparamter.errorcode;
				//led_force_ignite();		//no need to do

				sRam[(int)prShortIndex.iDBGCode] = 0x90;
				DBGCodeparamter.errorcode = (UInt16)sRam[(int)prShortIndex.iDBGCode];
				uGGErrorcode = DBGCodeparamter.errorcode;
				bInit &= afe_detect_device();
				if (!bInit) return bInit;

				sRam[(int)prShortIndex.iDBGCode] = 0x91;
				DBGCodeparamter.errorcode = (UInt16)sRam[(int)prShortIndex.iDBGCode];
				uGGErrorcode = DBGCodeparamter.errorcode;
				bInit &= afe_open_device();
				if (!bInit) return bInit;

				sRam[(int)prShortIndex.iDBGCode] = 0x92;
				DBGCodeparamter.errorcode = (UInt16)sRam[(int)prShortIndex.iDBGCode];
				uGGErrorcode = DBGCodeparamter.errorcode;
				//led_force_ignite();		//no need to do

				sRam[(int)prShortIndex.iDBGCode] = 0xA0;
				DBGCodeparamter.errorcode = (UInt16)sRam[(int)prShortIndex.iDBGCode];
				uGGErrorcode = DBGCodeparamter.errorcode;
				//afe_eeprom_init();		//no need to do

				sRam[(int)prShortIndex.iDBGCode] = 0xA1;
				DBGCodeparamter.errorcode = (UInt16)sRam[(int)prShortIndex.iDBGCode];
				uGGErrorcode = DBGCodeparamter.errorcode;
				//led_force_ignite();		//no need to do

				sRam[(int)prShortIndex.iDBGCode] = 0xA2;
				DBGCodeparamter.errorcode = (UInt16)sRam[(int)prShortIndex.iDBGCode];
				uGGErrorcode = DBGCodeparamter.errorcode;
				//tmm_init();
				//(M140917)Francis, adjust polling loop to 4 seconds as OZ8806 did
				tmrFirmware.Interval = TimeSpan.FromMilliseconds(4000);

				sRam[(int)prShortIndex.iDBGCode] = 0xA3;
				DBGCodeparamter.errorcode = (UInt16)sRam[(int)prShortIndex.iDBGCode];
				uGGErrorcode = DBGCodeparamter.errorcode;
				bInit &= daq_init();		//read once ADC data
				if (!bInit) return bInit;

				sRam[(int)prShortIndex.iDBGCode] = 0xA4;
				DBGCodeparamter.errorcode = (UInt16)sRam[(int)prShortIndex.iDBGCode];
				uGGErrorcode = DBGCodeparamter.errorcode;
				gg_init();		//read OCV value to initialize Gas Gauge

				sRam[(int)prShortIndex.iDBGCode] = 0xA5;
				DBGCodeparamter.errorcode = (UInt16)sRam[(int)prShortIndex.iDBGCode];
				uGGErrorcode = DBGCodeparamter.errorcode;
				sbd_init();

				sRam[(int)prShortIndex.iDBGCode] = 0xA6;
				DBGCodeparamter.errorcode = (UInt16)sRam[(int)prShortIndex.iDBGCode];
				uGGErrorcode = DBGCodeparamter.errorcode;
				//smuser_init();		//no need to do

				sRam[(int)prShortIndex.iDBGCode] = 0xA7;
				DBGCodeparamter.errorcode = (UInt16)sRam[(int)prShortIndex.iDBGCode];
				uGGErrorcode = DBGCodeparamter.errorcode;
				//led_force_ignite(LED_4);		//no need to do

				sRam[(int)prShortIndex.iDBGCode] = 0xA8;
				DBGCodeparamter.errorcode = (UInt16)sRam[(int)prShortIndex.iDBGCode];
				uGGErrorcode = DBGCodeparamter.errorcode;
				sbd_udpate_sbs();

				sRam[(int)prShortIndex.iDBGCode] = 0xA9;
				DBGCodeparamter.errorcode = (UInt16)sRam[(int)prShortIndex.iDBGCode];
				uGGErrorcode = DBGCodeparamter.errorcode;
				sm_init();

				sRam[(int)prShortIndex.iDBGCode] = 0xAA;
				DBGCodeparamter.errorcode = (UInt16)sRam[(int)prShortIndex.iDBGCode];
				uGGErrorcode = DBGCodeparamter.errorcode;
				//ext_init();		//no need to do

				sRam[(int)prShortIndex.iDBGCode] = 0xAB;
				DBGCodeparamter.errorcode = (UInt16)sRam[(int)prShortIndex.iDBGCode];
				uGGErrorcode = DBGCodeparamter.errorcode;
				//led_force_ignite(LED_ALL_OFF);		no need to do

				sRam[(int)prShortIndex.iDBGCode] = 0xF0;
				age_compensate_init();

				sRam[(int)prShortIndex.iDBGCode] = 0x00;
				DBGCodeparamter.errorcode = (UInt16)sRam[(int)prShortIndex.iDBGCode];
				uGGErrorcode = DBGCodeparamter.errorcode;
				ParamSBS0x16.phydata = (UInt16)SBS16enum.SBS_BSTAT_INITIALIZED;

				//if (bInit)
				{
					
					tmrFirmware.Start();
					//bInit = true;
				}
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
			tmrFirmware.Stop();

			//MessageBox.Show("GG Unload");
			return true;
		}

		public UInt32 GetStatus()
		{
			return uGGErrorcode;
		}

		public bool CalculateGasGauge()
		{
			return false;
		}

		/*
		public bool AccessSBSParam(ref UInt16 uOut, int iIndex)
		{
			bool bAccess = true;

			if (uFlag != (0x0100 + iIndex))
			{
				uOut = wRam[iIndex];
			}
			else
			{
				Thread.Sleep(10);
			}


			return bAccess;
		}

		public bool AccessSBSParam(ref byte yOut, int iIndex)
		{
			return true;
		}
		*/

		public bool AccessSBSParam(ref float fOut, byte yIndex)
		{
			bool bAccess = true;
			if (yIndex < fRam.Length)
			{
				fOut = fRam[yIndex];
			}
			else
			{
				Thread.Sleep(10);
				bAccess = false;
			}

			return bAccess;
		}

		public bool AccessSBSParam(ref short sOut, byte yIndex)
		{
			bool bAccess = true;

			if(yIndex < sRam.Length)
			{
				sOut = sRam[yIndex];
			}
			else
			{
				Thread.Sleep(10);
				bAccess = false;
			}

			return bAccess;
		}

		public GasGaugeProject GetProjectFile()
		{
			return myProject;
		}

		#endregion 

//		#region private methods

		//		#region system_initialize() simulation

		#region hwinterface.c porting

		private bool bg_init()
		{
			bool bBG = true;	//suppose everything is OK, due to xml is defined by ourselves.
			UInt16 tmpType, ySum = 0;

			#region check polling data in paramterlist is complete or not

			//summarize typevalue that all polling definition that defined in GGType enum
			foreach(GGType gt in Enum.GetValues(typeof(GGType)))
			{
				//tmpType = (UInt16)((UInt16)Enum.Parse(typeof(GGType), Enum.GetName(typeof(GGType), i)) & (UInt16)GGType.OZGGPollMask);
				tmpType = (UInt16)Enum.Parse(typeof(GGType), gt.ToString());
				if (((tmpType & (UInt16)GGType.OZGGPollMask) != 0)
					&& (tmpType != (UInt16)GGType.OZGGPollMask))
				{
					ySum += tmpType;
				}
			}
			//summarize typevalue of polling data in parameterlist
			tmpType = 0;
			//foreach (Parameter pmScan in pmcGGPolling.parameterlist)
			foreach (Parameter pmScan in PListGGPolling)
			{
				tmpType += (UInt16)(Convert.ToUInt16(pmScan.key) & (UInt16)GGType.OZGGPollMask);
			}

			//if not equal, polling parameterlist has something error.
			if (tmpType != ySum)
				bBG = false;

			//(A140414)Francis, add SeaElf charger status register maunally, cause xml define bit by bit,
			//it's too many to add in each node
			//foreach (Parameter pmSet in pmcGGSetting.parameterlist)
			//{
				//if (((pmSet.guid & 0x0000FFFF) >> 16) == (byte)EGSBS.SBSCHGSet40)
				//{
					//if (SeaElfStatusRegx40 == null)
					//{
						//SeaElfStatusRegx40 = new Parameter();
						//SeaElfStatusRegx40.guid = pmSet.guid;
						//SeaElfStatusRegx40.r
					//}
				//}
			//}
			//(E140414)

			//(A140414)Francis, add pointer of ErrorCode paramter
			//foreach (Parameter pmSBStmp in pmcSBSReg.parameterlist)
			foreach (Parameter pmSBStmp in PListSBSReg)
			{
				if (((pmSBStmp.guid & 0x0000FFFF) >> 8) == (byte)(EGSBS.SBSBatteryStatus))
				{	//found 0x16 parameter
					DBGCodeparamter = pmSBStmp;
					DBGCodeparamter.errorcode = 0x00;
					break;
				}
			}
			if (DBGCodeparamter == null)
				bBG = false;
			//(E140414)

			#endregion

			return bBG;
		}

		#endregion

		#region gdm.c porting

		private bool gdm_init()
		{
			bool bGDM = true;
			UInt16 u16Addr = 0x00;

			//fSBS[(byte)EGSBS.SBSVoltCell01] = 3000;
			//fSBS[(byte)EGSBS.SBSCurrent] = 1000;
			//fSBS[(byte)EGSBS.SBSExtTemp01] = 25;
			//fSBS[(byte)EGSBS.SBSRSOC] = 10;
			//fSBS[(byte)EGSBS.SBSRC] = 300;

			//
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
			for (int fff = 0; fff < fRam.Length; fff++ )
			{
				fRam[fff] = 0F;
			}
			for (int sss = 0; sss < sRam.Length; sss++)
			{
				sRam[sss] = 0x0000;
			}
			//initialize GDM value for GG calculation, for test
			//fRam[(int)prFloatIndex.iDesignCapacity] = myProject.dbDesignCp;
			fRam[(int)prFloatIndex.iFCC] = myProject.dbDesignCp;
			//fRam[(int)prFloatIndex.iRsense] = myProject.dbRsense;
			//fRam[(int)prFloatIndex.iExtTempPullR] = myProject.dbPullupR;
			//fRam[(int)prFloatIndex.iExtTempPullV] = myProject.dbPullupV;
			fRam[(int)prFloatIndex.iAgeFactor] = 1.0F;
			fRam[(int)prFloatIndex.iDsgRatio] = 1.0F;
			fRam[(int)prFloatIndex.iChgCurrTh] = 10F;
			fRam[(int)prFloatIndex.iDsgCurrTh] = -10F;
			fRam[(int)prFloatIndex.iFChgEndCurr] = myProject.dbChgEndCurr;
			fRam[(int)prFloatIndex.iFChgTimeout] = 60;
			fRam[(int)prFloatIndex.iFChgCV] = myProject.dbChgCVVolt;
			fRam[(int)prFloatIndex.iDsgZero] = myProject.dbDsgEndVolt;
			fRam[(int)prFloatIndex.iRCTableHighV] = myProject.GetRCTableHighVolt();
			fRam[(int)prFloatIndex.iRCTableLowV] = myProject.GetRCTableLowVolt() ;
			//
			sRam[(int)prShortIndex.iOneMinTmr] = 60;
			sRam[(int)prShortIndex.iOneHrTmr] = 60;
			sRam[(int)prShortIndex.iThermStayCnt] = 0;
			sRam[(int)prShortIndex.iBGState] = (short)BGStateenum.BGSTATE_IDLE;
			sRam[(int)prShortIndex.iBGPMode] = (short)BGPwrenum.FW_PMODE_FULL;

			//copy DBGcode  to high byte of SBSx16
			ParamSBS0x16.phydata = 0;
			DBGCodeparamter.errorcode = 0x00;
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

		//private void gdm_reload_calib(); skip

		#endregion

		#region afe OZ89xx.c porting

		private bool afe_detect_device()
		{
			bool bAFE = true;
			byte yResult = 0x00;
			Reg regID = new Reg();

			//read register 0x00, to make sure chip is existed.
			regID.address = 0x00;
			regID.startbit = 0;
			regID.bitsnumber = 0x08;
			bAFE = ReadChipsByte(yOZ8806SubSec, ref regID, ref yResult, true);
			if (bAFE)
			{
				if (yResult != (byte)SupportID.IDOZ8806)		//support OZ8806 only
				{
					bAFE = false;
				}
			}
			if (!bDEMAccess)
				bAFE = true;

			return bAFE;
		}

		//no used code inside
		private bool afe_open_device()
		{
			bool bAFE = true;

			return bAFE;
		}

		#endregion

		#region dataacquire.c porting

		private bool daq_init()
		{
			//bg_adc_set_cell_num(bget(IDX_GDV_B_CELLNUM));	//cell number initialization; skip
			//bg_set_cadc_scan(CADC_SCAN_ENABLE_SCAN);	//only enable scan, no coulomb counting; skip
			//bg_set_scan_rate(ADC_SCANRATE_CURRENT_ONLY);	//set current adc sacn only; skip
			//bg_adc_do_offset();			//do offset;	skip
			//bg_set_cadc_scan(CADC_SCAN_ENABLE_ALL);			//enable both scan and coulomb counting; skip
			//bg_set_scan_rate(ADC_SCANRATE_4HZ);				//enable both cadc and multi-channel adc scan; skip
			//bg_event_clr_all();	skip
			//bg_event_enable(EVT_SCANADC);
			//wput(IDX_GDV_W_HITEMPDK, 0);	
			//wput(IDX_GDV_W_LOTEMPDK, 4000);	skip, we only have 1 Temp channel

			//Wait for 1st scan result
			//daq_scan_data_filt();	 skip
			//OV Threshold	//COC Threshold	//DOC Threshold; skip
			bool breturn = daq_adc_data_read();

			return breturn;
		}

		private bool daq_adc_data_read()
		{
			bool bPoll = false;
			GGType tmpTyp;
			float ftmp;
			Reg SeaElfSatusReg = new Reg();
			byte yValue = 0;

			if (devParent.bBusy)
			{
				devParent.msg.errorcode = LibErrorCode.IDS_ERR_EGDLL_BUS_BUSY;
				return bPoll;
			}

			if(!bDEMAccess)
				return true;

			pmcAll.parameterlist = PListGGPolling;
			devParent.bBusy = true;
			tskMsgParent.task_parameterlist = pmcAll;
			tskMsgParent.task = TM.TM_READ;
			devParent.AccessDevice(ref tskMsgParent);
			while (tskMsgParent.bgworker.IsBusy)
				System.Windows.Forms.Application.DoEvents();
			System.Windows.Forms.Application.DoEvents();
			if (bDEMAccess)
			{
				DBGCodeparamter.errorcode = tskMsgParent.errorcode;
				uGGErrorcode = DBGCodeparamter.errorcode;
				if (tskMsgParent.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
				{
					devParent.bBusy = false;
					tskMsgParent.errorcode = LibErrorCode.IDS_ERR_EGDLL_GGREGISTERRW;
					DBGCodeparamter.errorcode = tskMsgParent.errorcode;
					uGGErrorcode = DBGCodeparamter.errorcode;
					UnloadGG();
					return bPoll;
				}
			}
			//if (bDEMAccess == true) and if(bDEMAccess == false) + (errorcode == SUCCESSFUL)
			//go belows
			{
				uGGErrorcode = LibErrorCode.IDS_ERR_SUCCESSFUL;
				tskMsgParent.task = TM.TM_CONVERT_HEXTOPHYSICAL;
				devParent.AccessDevice(ref tskMsgParent);
				while (tskMsgParent.bgworker.IsBusy)
					System.Windows.Forms.Application.DoEvents();
				//foreach (Parameter pmScan in pmcGGPolling.parameterlist)
				foreach (Parameter pmScan in PListGGPolling)
				{
					tmpTyp = (GGType)Convert.ToUInt16(pmScan.key) & GGType.OZGGPollMask;
					ftmp = Convert.ToSingle(pmScan.phydata);
					switch (tmpTyp)
					{
						case GGType.OZVoltage:
							{
								fRam[(byte)prFloatIndex.iVoltDiff] = ftmp - fRam[(byte)prFloatIndex.iVoltage];
								fRam[(byte)prFloatIndex.iVoltage] = ftmp;
								if (ftmp >= myProject.dbChgCVVolt)
								{
									sRam[(int)prShortIndex.iBGStatus] |= (short)BGStatusenum.BG_STATUS_CVSTATE;
								}
								else if (ftmp <= fRam[(int)prFloatIndex.iDsgZero])
								{
									sRam[(int)prShortIndex.iBGStatus] |= (short)BGStatusenum.BG_STATUS_LOWVOLT;
								}
								else
								{	//clear CV and LowVolt bit
									sRam[(int)prShortIndex.iBGStatus] &=
										~((short)BGStatusenum.BG_STATUS_LOWVOLT + (short)BGStatusenum.BG_STATUS_CVSTATE);
								}
								break;
							}
						case GGType.OZCurrent:
							{
								Int16 ihex = (Int16)(UInt16)(pmScan.hexdata << 2);
								ihex /= 4;
								ftmp = (float)(ihex * pmScan.phyref / pmScan.regref);
								ftmp /= myProject.dbRsense;	//convert to mA
								fRam[(int)prFloatIndex.iCurrDiff] = ftmp - fRam[(byte)prFloatIndex.iCurrent];
								if(fRam[(int)prFloatIndex.iCurrDiff] < 0) fRam[(int)prFloatIndex.iCurrDiff] *= -1;	//make it as posi
								fRam[(int)prFloatIndex.iCurrent] = ftmp;
								sRam[(int)prShortIndex.iModeP] = sRam[(int)prShortIndex.iMode];	//back up iMode
								if (ftmp <= fRam[(int)prFloatIndex.iDsgCurrTh])
								{
									sRam[(int)prShortIndex.iMode] = -1;
								}
								else if (ftmp >= fRam[(int)prFloatIndex.iChgCurrTh])
								{
									sRam[(int)prShortIndex.iMode] = 1;
								}
								else
								{
									sRam[(int)prShortIndex.iMode] = 0;
									//if((sRam[(int)prShortIndex.iModeP] == 1) && (fRam[(int)prFloatIndex.iCurrDiff] < 0)
								}
								break;
							}
						case GGType.OZExtTemp:
							{
								//fRam[(byte)prFloatIndex.iTempDiff] = ftmp - fRam[(byte)prFloatIndex.iTemperature];
								fRam[(int)prFloatIndex.iTempVolt] = ftmp;		//save ADC voltage value
								sRam[(int)prShortIndex.iExtTempDK] = (short)myProject.LutThermalDK(ftmp);	//save DegreeK
								break;
							}
						case GGType.OZCAR:
							{
								Int16 ihex = (Int16)(UInt16)(pmScan.hexdata);
								ftmp = (float)(ihex * pmScan.phyref / pmScan.regref);
								ftmp /= myProject.dbRsense;		//convert to mAhr
								//(M140407)Francis, use CAR as Colomb Counting, read it as different and clear it every time
								//fRam[(byte)prFloatIndex.iCarDiff] = ftmp;// -fRam[(byte)prFloatIndex.iCAR];
								//reset CAR as 0
								//if ((ftmp > 0.25F) || (ftmp < -0.25F))
								//{
									//for (int i = 0; i < 10; i++)
									//{
										//ftmp = 0;
										//bPoll = AccessCARReg(true, false, ref ftmp);
										//bPoll = AccessCARReg(false, true, ref ftmp);
										//if (ftmp == 0)
										//{
											//break;
										//}
										//else
										//{
											//Thread.Sleep(10);
										//}
									//}
								//}
								//(M140502)Francis,
								if (ftmp != fRam[(byte)prFloatIndex.iCAR])
								{
									fRam[(byte)prFloatIndex.iCarDiff] = ftmp - fRam[(byte)prFloatIndex.iCAR];
									fRam[(byte)prFloatIndex.iCAR] = ftmp;
									//fRam[(byte)prFloatIndex.iCTMah] += fRam[(byte)prFloatIndex.iCarDiff];
								}
								else
								{
									fRam[(byte)prFloatIndex.iCarDiff] = 0;
								}
								//(E140502)
								if (!bDEMAccess)
									bPoll = true;
								//fRam[(byte)prFloatIndex.iCAR] = ftmp;
								//(E140407)
								break;
							}
						default:
							break;

					}
				}

				devParent.bBusy = false;	//clear bus busy

				//read SeaElf status registerx40
				if (bSeaElfSupport)
				{
					SeaElfSatusReg.address = 0x40;
					SeaElfSatusReg.startbit = 0;
					SeaElfSatusReg.bitsnumber = 8;
					if (bSeaElfSupport)
					{
						bPoll = ReadChipsByte(yOZSeaElfSubSec, ref SeaElfSatusReg, ref yValue, true);
					}
					else
					{
						bPoll = true;
						yValue = 00;
					}
				}
				else
				{
					bPoll =true;
					//if (fRam[(byte)prFloatIndex.iVoltage] > myProject.dbChgCVVolt)
					//{
						//yValue |= (byte)VBusSysReg.VBUSOVP;
					//}
				}
				if(bPoll)		sRam[(int)prShortIndex.iSeaElfRegx40] = yValue;
				//OVP,UVP bit set
				if ((yValue & (byte)VBusSysReg.VBUSOVP) != 0)
				{
					//OVP, no OVP flag?? TBD
					//sRam[(int)prShortIndex.iBGStatus] |= (short)BGStatusenum.ovp
				}
				if ((yValue & (byte)VBusSysReg.VBUSUVP) != 0)
				{
					//UVP, no UVP flag?? TBD
					//sRam[(int)prShortIndex.iBGStatus] |= (short)BGStatusenum.uvp
				}

				//read SeaElf status registerx41
				if (bSeaElfSupport)
				{
					SeaElfSatusReg.address = 0x41;
					SeaElfSatusReg.startbit = 0;
					SeaElfSatusReg.bitsnumber = 8;
					if (bSeaElfSupport)
					{
						bPoll = ReadChipsByte(yOZSeaElfSubSec, ref SeaElfSatusReg, ref yValue, true);
					}
					else
					{
						bPoll = true;
						yValue = 0;
					}
				}
				else
				{
					bPoll = true;
					if (fRam[(byte)prFloatIndex.iVoltage] > myProject.dbChgCVVolt)
					{
						yValue |= (byte)ChargerReg.CHARGERCVSTATE;
					}
					//in charge mode, charge current < tapcurrent, and currdiff < 20
					if ((fRam[(byte)prFloatIndex.iCurrent] < myProject.dbChgEndCurr) 
						&& (fRam[(byte)prFloatIndex.iCurrDiff] < 20)
						&& (sRam[(int)prShortIndex.iMode] == 1))
					{
						yValue |= (byte)ChargerReg.CHARGERFULLYCHARGED;
					}
				}

				if(bPoll)		sRam[(int)prShortIndex.iSeaElfRegx41] = yValue;
				//CC,CV mode state set
				if ((yValue & (byte)ChargerReg.CHARGERCVSTATE) != 0)
				{
					//CV state
					sRam[(int)prShortIndex.iBGStatus] |= (short)BGStatusenum.BG_STATUS_CVSTATE;
				}
				if ((yValue & (byte)ChargerReg.CHARGERFULLYCHARGED) != 0)
				{
					// last time is charged and now is idle and currdiff is small, we can think it's Fully charged
					//if ((sRam[(int)prShortIndex.iModeP] > 0) && (sRam[(int)prShortIndex.iMode] == 0) && (fRam[(int)prFloatIndex.iCurrDiff] < 20))
					//if((sRam[(int)prShortIndex.iMode] == 0) && (sRam[(int)prShortIndex.iModeP] == 1))
					{
						sRam[(int)prShortIndex.iBGStatus] |= (short)BGStatusenum.BG_STATUS_ENDMA;
					}
//					else
//					{
//						if(
//					}
				}
				
				//read SeaElf status registerx42
				SeaElfSatusReg.address = 0x42;
				SeaElfSatusReg.startbit = 0;
				SeaElfSatusReg.bitsnumber = 8;
				if (bSeaElfSupport)
				{
					bPoll = ReadChipsByte(yOZSeaElfSubSec, ref SeaElfSatusReg, ref yValue, true);
				}
				else
				{
					bPoll = true;
					yValue = 0;
				}
				if(bPoll)		sRam[(int)prShortIndex.iSeaElfRegx42] = yValue;
				//Thermal state set
				//if(
			}

			return bPoll;
		}

		#endregion

		#region gasgauge.c porting

		private void gg_init()
		{
			bool bGInit = true;
			byte yResult = 00;
			float OCVData = 3800F;
			float dbPercent;

			fRam[(int)prFloatIndex.iRCTableHighV] = float.Parse(myProject.GetRCTableHighVolt().ToString());
			fRam[(int)prFloatIndex.iRCTableLowV] = float.Parse(myProject.GetRCTableLowVolt().ToString());

			//if UV touched first; skip
			//Due to UV cannot be controlled by OZ8806 and SeaElf either

			//if (lut_rc_boundary(AXIS_CRATE, MAX_SIDE) >= 10000); skip set -100 directly
			//rc_crate_factor = -100;
			rc_crate_factor = myProject.GetRCCrateFactor();

			//initialize EOC array
			gg_eoc_init();

			//Set PEC enable
			yResult = 0x04;
			bGInit = AccessPECControl(true, false, ref yResult);
			if (!bDEMAccess) bGInit = true;

			//get capacity from ocv table
			bGInit = GetOCVdata(ref OCVData);
			if (!bDEMAccess)
			{
				bGInit = true;
				OCVData = 3800;
			}
			if (bGInit)
			{
				fRam[(int)prFloatIndex.iOCVolt] = OCVData;
				//if there is big difference between OCV and voltage us current voltage to initialize CAR, instead of OCV
				if (Math.Abs(fRam[(int)prFloatIndex.iOCVolt] - fRam[(int)prFloatIndex.iVoltage]) > 20)
				{
					OCVData = fRam[(int)prFloatIndex.iVoltage];	//use current voltage to LutOCVtable
					//float dbPercent = myProject.LutTSOCbyOCV(fRam[(int)prFloatIndex.iVoltage]);
					//fRam[(int)prFloatIndex.iCAR] = (float)(dbPercent * 0.01 * myProject.dbDesignCp);
					////fRam[(int)prFloatIndex.iPercent] = dbPercent;		//percentage is calculated in sbd_update_sbs()
					////fRam[(int)prFloatIndex.iCAR] = (float)((dbPercent /32768) * myProject.dbDesignCp);
					//if (fRam[(int)prFloatIndex.iCAR] > myProject.dbDesignCp)
					//{
						//fRam[(int)prFloatIndex.iCAR] = myProject.dbDesignCp;
					//}
					//fRam[(int)prFloatIndex.iCAMah] = fRam[(int)prFloatIndex.iCAR];
					//fRam[(int)prFloatIndex.iPrevCAMah] = fRam[(int)prFloatIndex.iCAR];
				}
				dbPercent = myProject.LutTSOCbyOCV(OCVData);
				fRam[(int)prFloatIndex.iCAR] = (float)(dbPercent * 0.01 * myProject.dbDesignCp);
				//fRam[(int)prFloatIndex.iPercent] = dbPercent;		//percentage is calculated in sbd_update_sbs()
				//fRam[(int)prFloatIndex.iCAR] = (float)((dbPercent /32768) * myProject.dbDesignCp);
				if (fRam[(int)prFloatIndex.iCAR] > myProject.dbDesignCp)
				{
					fRam[(int)prFloatIndex.iCAR] = myProject.dbDesignCp;
				}
				fRam[(int)prFloatIndex.iCTMah] = fRam[(int)prFloatIndex.iCAR];
				fRam[(int)prFloatIndex.iCAMah] = fRam[(int)prFloatIndex.iCAR];
				fRam[(int)prFloatIndex.iPrevCAMah] = fRam[(int)prFloatIndex.iCAR];
				fRam[(int)prFloatIndex.iCAR] = 0;
				dbPercent = 7F;		//according to OZ8806 V4.0
				bGInit = AccessBoardCurrent(true, false, ref dbPercent);
				dbPercent = 0F;	//reset CAR as 0
				bGInit = AccessCARReg(true, false, ref dbPercent);
				//bGInit = AccessCARReg(false, true, ref dbPercent);

				if (myProject.iCellNum > 1)
				{
					yResult = 0x3D;
				}
				else
				{
					yResult = 0x31;
				}
				bGInit = AccessCtrlReg(true, false, ref yResult);

				gg_eoc_init();
			}
		}

		private void gg_avg_rc(int iblend)
		{
			float fbld = fRam[(int)prFloatIndex.iCarDiff];
			if (iblend != 0)		//if blend required
			{
				if (sRam[(int)prShortIndex.iMode] > 0)	//charging
				{
					if (iblend > 0)		//charging and chase blending
					{
						fbld = 0.0025F * (float)myProject.dbChgEndCurr;
						fbld += fRam[(int)prFloatIndex.iCarDiff];
					}
					else			//charging and waiting
					{
						fbld = fRam[(int)prFloatIndex.iCarDiff] / 8.0F;
					}
				}
				else			//discharging
				{
					/*	(M140527)Francis, adjust with Jon, use ratio to chase RC table value
					if (iblend > 0)		//discharging and chase blending
					{
						fbld = 5.0F * fRam[(int)prFloatIndex.iCarDiff];
					}
					else			//discharging and waiting
					{
						fbld = fRam[(int)prFloatIndex.iCarDiff] / 16;
					}
					*/
					if (iblend > 0)
					{
						fRam[(int)prFloatIndex.iCAUMah] += fRam[(int)prFloatIndex.iDsgRatio] * fRam[(int)prFloatIndex.iCarDiff];
						if (Math.Abs(fRam[(int)prFloatIndex.iCAUMah] / 1000F) < (fRam[(int)prFloatIndex.iFCC] / 100F))	//lower than 1% of FCC
						{
							fbld = ((int)fRam[(int)prFloatIndex.iCAUMah]) / 1000;
						}
						fRam[(int)prFloatIndex.iCAUMah] = (float)((int)fRam[(int)prFloatIndex.iCAUMah]) % 1000;
					}
					else
					{
						fbld = fRam[(int)prFloatIndex.iCarDiff];
					}
				}
			}	//if(iblend !=0 )

			//fRam[(int)prFloatIndex.iCarDiff] = fbld;		//no need to set back to CTDELTA
			fRam[(int)prFloatIndex.iCAMah] = fRam[(int)prFloatIndex.iPrevCAMah] + fbld;
			//SBSxF0, log it before reset
			if (ParamSBSxF0CarDiff != null)
			{
				ParamSBSxF0CarDiff.phydata = fRam[(int)prFloatIndex.iCarDiff];
			}
			fRam[(int)prFloatIndex.iCarDiff] = 0F;		//reset
			if (fRam[(int)prFloatIndex.iCAMah] > fRam[(int)prFloatIndex.iFCC])
				fRam[(int)prFloatIndex.iCAMah] = fRam[(int)prFloatIndex.iFCC];
			if (fRam[(int)prFloatIndex.iCAMah] < 0)
				fRam[(int)prFloatIndex.iCAMah] = 0F;
		}

		private void gg_fcc_update(GGFCCCase fcccase)
		{
			float fFCC09per = fRam[(int)prFloatIndex.iFCC] * 0.9F;
			float fFCC11per = fRam[(int)prFloatIndex.iFCC] * 1.1F;

			//	if (((wget(IDX_GDV_W_FUNCSUPPORT) & BCFG_FCCDISPLAY) == 0)	//FCC can limited by design
			//&& (fcomp(IDX_GDV_F_CTMAH, IDX_GPM_F_DESIGNCAP) > 0))	//and CTMAH > DESIGN CAP
			//fcopy(IDX_GDV_F_CTMAH, IDX_GPM_F_DESIGNCAP); skip
			//default is CTMAH is able to be bigger then DESIGN CAPACITY

			if (fcccase == GGFCCCase.FCC_NORM)
			{
				//newFCC = oldFCC * 0.9 + CTMah * 0.1
				fFCC09per += fRam[(int)prFloatIndex.iCTMah] * 0.1F;
				fRam[(int)prFloatIndex.iFCC] = fFCC09per;
				fRam[(int)prFloatIndex.iCTMah] = fFCC09per;
			}
			else if (fcccase == GGFCCCase.FCC_CND)
			{
				if (!GLOBAL_STATUS_CONDITIONAL)
				{
					//set update in 1.1~09* range
					if (fRam[(int)prFloatIndex.iCTMah] < fFCC09per)
					{
						fRam[(int)prFloatIndex.iCTMah] = fFCC09per;
					}
					else if (fRam[(int)prFloatIndex.iCTMah] > fFCC11per)
					{
						fRam[(int)prFloatIndex.iCTMah] = fFCC11per;
					}
				}
				fRam[(int)prFloatIndex.iFCC] = fRam[(int)prFloatIndex.iCTMah];
				GLOBAL_STATUS_CONDITIONAL = false;
			}
			else
			{
				return;
			}
			fRam[(int)prFloatIndex.iCAMah] = fRam[(int)prFloatIndex.iFCC];
			fRam[(int)prFloatIndex.iPrevCAMah] = fRam[(int)prFloatIndex.iFCC];
			//default is (DFCC_SUPPORT)
			sRam[(int)prShortIndex.ifcc_therm] = sRam[(int)prShortIndex.iExtTempDK];
		}

		private void gg_chg_blend()
		{
			int chgblend = 0;
			int h = 1;

			while (++h <= 5)
			{
				if (fEcMAH[h - 2] != 0)
					break;
			}

			if (h > 5)
			{
				chgblend = 0;
			}
			else
			{
				//IDX_GDV_F_TEMP1 = (IDX_GDV_F_AVGIMMA - IDX_GDV_F_CHGENDMA) / (IDX_GDV_F_CHGENDMA * h);
				float fTemp1 = fRam[(int)prFloatIndex.iCurrent] - myProject.dbChgEndCurr;
				fTemp1 /= (myProject.dbChgEndCurr * h);

				//IDX_GDV_F_CAMAH = (FLOAT32)fEcMAH[h-2] * IDX_GDV_F_TEMP1 + IDX_GDV_F_CFMAH * (1-IDX_GDV_F_TEMP1);
				float fTemp2 = fEcMAH[h - 2] * fTemp1;
				fTemp2 += (fRam[(int)prFloatIndex.iFCC] * (1.0F - fTemp1));
				fTemp2 -= fRam[(int)prFloatIndex.iPrevCAMah];
				if (fTemp2 > 0.0001F)
				{
					chgblend = 1;
				}
				else if (fTemp2 < -0.0001F)
				{
					chgblend = -1;
				}
			}

			gg_avg_rc(chgblend);
			if (fRam[(int)prFloatIndex.iPrevCAMah] < fRam[(int)prFloatIndex.iCAMah])	//prevent CaMAH going down during charging
			{	//if CAMah > previous, set up previous
				fRam[(int)prFloatIndex.iPrevCAMah] = fRam[(int)prFloatIndex.iCAMah];
			}
		}

		private void gg_dsg_blend()
		{
			//float fret = 0;
			int dsgblend = 0;
			float fvolt, ftherm, fcurr;
			float frc1, frc2;
			//float teod;
			float frcDiff;

			//fcurr = fRam[(int)prFloatIndex.iAgeFactor];		//adjust current by AgeFactor
			fcurr = 1.0F;
			fvolt = fRam[(int)prFloatIndex.iVoltage];
			//(A140918)Francis, add Eason's algorithm if voltage is below than ZeroVoltage but RSOC is not 0%
			if (fvolt < fRam[(int)prFloatIndex.iDsgZero])
			{
					fRam[(int)prFloatIndex.iCarDiff] = fRam[(int)prFloatIndex.iFCC] * 0.005F * -1.0F;
					dsgblend = -1;		//no chasing
			}
			//(E140918)
			else if (fvolt <= fRam[(int)prFloatIndex.iRCTableHighV])
			{
				fcurr *= (rc_crate_factor * fRam[(int)prFloatIndex.iCurrent]) / myProject.dbDesignCp; //transfer to 10000C
				ftherm = sRam[(int)prShortIndex.iExtTempDK] - 2730;
				//teod = calc_teod_by_ieod((int)fcurr, age_comp.rch25_now, (int)ftherm, age_comp.r_thermal, age_comp.delta_t);
				frc1 = myProject.LutRCTable(fvolt, fcurr, ftherm);
				//fvolt = fRam[(int)prFloatIndex.iRCTableLowV];			//use volt min value
				fvolt = myProject.dbDsgEndVolt;		//Francis, use DischargeEndVoltage instead
				frc2 = myProject.LutRCTable(fvolt, fcurr, ftherm);
				//myProject.LutRCTable is returning mAhr value
				/* (M140528)
				frc1 -= frc2;
				frc1 -= fRam[(int)prFloatIndex.iPrevCAMah];
				if (frc1 > 0.0001F)
				{
					dsgblend = -1;
				}
				else if (frc1 < 0.0001F)
				{
					dsgblend = 1;
				}
				 */
				frcDiff = frc1 - frc2;
				frcDiff += myProject.dbDesignCp / 100 + 1;
				if (frcDiff <= 0)
				{
					fRam[(int)prFloatIndex.iDsgRatio] = 0F;
					dsgblend = -1;		//no chasing
				}
				else
				{
					fRam[(int)prFloatIndex.iDsgRatio] = 1000 * fRam[(int)prFloatIndex.iCAMah] / frcDiff;
					dsgblend = 1;
				}
			}
			else
			{	//not in table, use coulomb counting
				fRam[(int)prFloatIndex.iCAMah] = fRam[(int)prFloatIndex.iCTMah];
			}

			//default is (DFCC_SUPPORT)
			if( ((sRam[(int)prShortIndex.iBGStatus] & (short)BGStatusenum.BG_STATUS_THERM_STABLE) !=0)
				&& ((sRam[(int)prShortIndex.iBGStatus] & (short)BGStatusenum.BG_STATUS_DFCC_START) !=0)
				&& (IDX_GPM_W_DFCCTEMP > 0)	//(wget(IDX_GPM_W_DFCCTEMP) > DK_BASE)
				&& (sRam[(int)prShortIndex.ifcc_therm] !=0 ))
			{
				fvolt = (float)(IDX_GPM_W_DFCCTEMP);
				ftherm = (float)sRam[(int)prShortIndex.iExtTempDK];
				if (((ftherm - sRam[(int)prShortIndex.ifcc_therm]) > fvolt) 
					|| ((sRam[(int)prShortIndex.ifcc_therm] - ftherm) > fvolt))	//big enough to do DFCC compensation
				{
					fcurr *= rc_crate_factor * fRam[(int)prFloatIndex.iCurrent] / myProject.dbDesignCp;
					frc1 = myProject.LutRCTable(fRam[(int)prFloatIndex.iRCTableHighV], fcurr, (ftherm - 2730F));
					frc2 = myProject.LutRCTable(fRam[(int)prFloatIndex.iRCTableHighV], fcurr, (sRam[(int)prShortIndex.ifcc_therm] - 2730F));
					frc2 = (frc1 - frc2);	// *0.0001F * myProject.dbDesignCp; don't need to convert again
					frc1 = fRam[(int)prFloatIndex.iFCC] - frc2;
					fRam[(int)prFloatIndex.iFCC] = frc1;
					fRam[(int)prFloatIndex.iCAMah] = frc1;
					fRam[(int)prFloatIndex.iPrevCAMah] = frc1;
					fRam[(int)prFloatIndex.iCTMah] = frc1;
					sRam[(int)prShortIndex.ifcc_therm] = (short)ftherm;;
				}
			}

			gg_avg_rc(dsgblend);
			if (fRam[(int)prFloatIndex.iPrevCAMah] > fRam[(int)prFloatIndex.iCAMah])	//prevent CaMAH going up during discharging
			{
				fRam[(int)prFloatIndex.iPrevCAMah] = fRam[(int)prFloatIndex.iCAMah];
			}
		}

		private void gg_update_fctmah()
		{
			//FULL AND SLEEP BOARD LEVEL POWER CONSUMPTION; skip
			//directly add in CTMah with CTDelta
			fRam[(int)prFloatIndex.iCTMah] += fRam[(int)prFloatIndex.iCarDiff];

			gg_eoc_set();
			if (sRam[(int)prShortIndex.iMode] > 0)
			{
				gg_chg_blend();
			}
			else if (sRam[(int)prShortIndex.iMode] < 0)
			{
				gg_dsg_blend();
			}
			else
			{
				gg_avg_rc(0);
				fRam[(int)prFloatIndex.iPrevCAMah] = fRam[(int)prFloatIndex.iCAMah];
			}
		}

		private void gg_eoc_init()
		{
			for (int i = 0; i < fEcMAH.Length; i++)
			{
				fEcMAH[i] = 0F;
			}
		}

		private void gg_eoc_set()
		{
			float fmulti = 0;

			//not in CV charge, do init
			if ((sRam[(int)prShortIndex.iBGStatus] & (short)BGStatusenum.BG_STATUS_CVSTATE) == 0)
			//|| ((fcomp(IDX_GDV_F_AVGIMMA, IDX_GDV_F_AVGMA) < 0) && (fcomp(IDX_GDV_F_AVGMA, IDX_GDV_F_SMPMA) < 0))) {
			{
				gg_eoc_init();
				return;
			}

			for (int i = 5; i >= 2; i--)
			{
				fmulti = i * myProject.dbChgEndCurr;
				fmulti -= fRam[(int)prFloatIndex.iCurrent];
				if (fmulti >= 0)
				{
					if (fEcMAH[i - 2] == 0)
					{
						fEcMAH[i - 2] = fRam[(int)prFloatIndex.iCAMah];
					}
				}
				else
				{
					fEcMAH[i - 2] = 0;
				}
			}
		}

		private void gg_self_discharge(int iHour)
		{
			//float fself = myProject.LutSelfDsg(fRam[(int)prFloatIndex.iTemperature]);		//TBD: DK or DC??
			float fself = myProject.LutSelfDsg(sRam[(int)prShortIndex.iExtTempDK]);		//TBD: DK or DC??

			fself = fself * 2 * fRam[(int)prFloatIndex.iFCC] / (-24000000.0F);	//add negative here, 10000 * % per day
			fRam[(int)prFloatIndex.iSelfMah] = fself;
			fRam[(int)prFloatIndex.iCTMah] += fself;
			if (fRam[(int)prFloatIndex.iCTMah] < 0)
			{
				fRam[(int)prFloatIndex.iCTMah] = 0;
			}
			fRam[(int)prFloatIndex.iCAMah] += fself;
			if (fRam[(int)prFloatIndex.iCAMah] < 0)
			{
				fRam[(int)prFloatIndex.iCAMah] = 0; 
			}
			fRam[(int)prFloatIndex.iPrevCAMah] = fRam[(int)prFloatIndex.iCAMah];
		}

		#endregion

		#region sbsdata.c porting

		private void sbd_init()
		{
			UInt16 wtmp = 0;
			//ParamContainer pmcTemp = new ParamContainer();

			wtmp = (UInt16)SBS16enum.SBS_BSTAT_INITIALIZED;		//initialized
			devParent.bSBSReady = false;
			pmcAll.parameterlist.Add(ParamSBS0x16);
			tskMsgParent.task_parameterlist = pmcAll;
			ParamSBS0x16.phydata = wtmp;
			tskMsgParent.task = TM.TM_CONVERT_PHYSICALTOHEX;
			devParent.AccessDevice(ref tskMsgParent);
			while (tskMsgParent.bgworker.IsBusy)
				System.Windows.Forms.Application.DoEvents();
			System.Windows.Forms.Application.DoEvents();
			devParent.bSBSReady = true;
			//TBD: to improve, use pointer to point to SBS Parameter, then will be much easier to set
			//its value
		}

		//private int16_t sbd_mah_to_mw_cnvt(int16_t sval); skip

		//private int16_t sbd_mw_to_mah_cnvt(int16_t sval);skip

		//private uint16_t sbd_at_rate_calc(int16_t capacity, int16_t rate); skip

		private float sbd_time_to_empty(float fetmp)
		{
			float fcal = fRam[(int)prFloatIndex.iCAMah];

			if (fetmp > 0) return 65535F;		//input is positive current return 65535
			fcal /= (fetmp * -1);		//transfer minus current to positive
			fcal *= 60;		//convert to minute unit
			if (fcal <= 0)
			{
				return 0F;
			}
			else
			{
				return fcal;
			}
		}

		private float sbd_time_to_full(float fftmp)
		{
			float fcalf = fRam[(int)prFloatIndex.iFCC] - fRam[(int)prFloatIndex.iCAMah];

			if (fftmp < 0) return 65535F;	//minus current return 65535;
			fcalf /= fftmp;
			fcalf *= 60;		//convert to minute unit
			//let's set default IDX_GPM_B_ATTFADJ = 40
			if (ParamSBS0x0D.phydata > 75F)
			{
				//let's set default 100-IDX_GPM_B_ATTFADJ = 60
				fcalf += (float)((100F-ParamSBS0x0D.phydata) / 25F * 40F);
			}
			else
			{
				fcalf += 40;		
			}

			return (fcalf-1);
		}

		private double ConvertionFloat2P(String instr)
		{
			return Convert.ToDouble(String.Format("{0:F2}", instr));
		}

		private void sbd_udpate_sbs()
		{
			float fTempsbd;

			devParent.bSBSReady = false;
			//SBSx03
			//SBSx01
			//SBSx04~06; those are related OZ9310, typical SBS data format, ignore in KillerWhale project
			//SBSx3c~x3e, CellVoltage 01~
			if (ParamSBS0x3C != null)
				//ParamSBS0x3C.phydata = fRam[(int)prFloatIndex.iVoltage];
				ParamSBS0x3C.phydata = Convert.ToDouble(String.Format("{0:F2}", fRam[(int)prFloatIndex.iVoltage]));
			//SBSx49~x4C, Int/Ext Temp 01~
			if (ParamSBS0x4A != null)
				//ParamSBS0x4A.phydata = fRam[(int)prFloatIndex.iTemperature];
				ParamSBS0x4A.phydata = Convert.ToDouble(String.Format("{0:F2}", ((float)(sRam[(int)prShortIndex.iExtTempDK] - 2730) / 10F)));		//to DegreeC
			//SBSx08
			//SBSx09	//TotalVoltage
			if (ParamSBS0x09 != null)
				//ParamSBS0x09.phydata = fRam[(int)prFloatIndex.iVoltage];
				ParamSBS0x09.phydata = Convert.ToDouble(String.Format("{0:F2}", fRam[(int)prFloatIndex.iVoltage]));
			//SBSx0a	//Current
			if (ParamSBS0x0A != null)
				//ParamSBS0x0A.phydata = fRam[(int)prFloatIndex.iCurrent];
				ParamSBS0x0A.phydata = Convert.ToDouble(String.Format("{0:F2}", fRam[(int)prFloatIndex.iCurrent]));
			//SBSx0b
			//SBSx0c
			//SBSx0d, SBSx10	//RelativeStateOfCharge, FullChargedCapacity
			if (ParamSBS0x0D != null)
			{
				short dbgphy = (short)ParamSBS0x16.phydata;

				if (ParamSBS0x10 != null)
				{
					fTempsbd = fRam[(int)prFloatIndex.iFCC];
					//ParamSBS0x10.phydata = fTempsbd;
					ParamSBS0x10.phydata = Convert.ToDouble(String.Format("{0:F2}", fTempsbd));
				}
				else
				{
					fTempsbd = myProject.dbDesignCp;//fRam[(int)prFloatIndex.iDesignCapacity];
				}
				//fTempsbd = (100 * fRam[(int)prFloatIndex.iCAR]) / fTempsbd;
				fTempsbd = (100 * fRam[(int)prFloatIndex.iCAMah]) / fTempsbd;
				//clear fully discharged bit if not discharging and RSOC > 20%
				if (((sRam[(int)prShortIndex.iBGState] & (short)BGStateenum.BGSTATE_MASK) != (short)BGStateenum.BGSTATE_DISCHARGE)
					&& (fTempsbd > 20F))
				{
					if((dbgphy & (short)SBS16enum.SBS_BSTAT_FULLY_DISCHARGED) != 0)
					{
						dbgphy &= ~((short)SBS16enum.SBS_BSTAT_FULLY_DISCHARGED);
					}
					//ParamSBS0x16.phydata = (float)dbgphy;
					ParamSBS0x16.phydata = Convert.ToDouble(String.Format("{0:F2}", dbgphy));
				}
				//clear fully charged bit if not charging and RSOC < 97%
				if (((sRam[(int)prShortIndex.iBGState] & (short)BGStateenum.BGSTATE_MASK) != (short)BGStateenum.BGSTATE_CHARGE)
					&& (fTempsbd < 95F))
				{
					if ((dbgphy & (short)SBS16enum.SBS_BSTAT_FULLY_CHARGED) != 0)
					{
						dbgphy &= ~((short)SBS16enum.SBS_BSTAT_FULLY_CHARGED);
					}
					//ParamSBS0x16.phydata = (float)dbgphy;
					ParamSBS0x16.phydata = Convert.ToDouble(String.Format("{0:F2}", dbgphy));
				}
				//FullyCharged bit to set 100%, FullDischarged bit to set 1%
				if (((dbgphy & (short)SBS16enum.SBS_BSTAT_FULLY_CHARGED) != 0) && (fTempsbd >= 100))
				{
					fTempsbd = 100F;
				}
				if (((dbgphy & (short)SBS16enum.SBS_BSTAT_FULLY_DISCHARGED) != 0) && (fTempsbd <= 1))
				{
					fTempsbd = 1F;
				}
				//ParamSBS0x0D.phydata = fTempsbd;
				ParamSBS0x0D.phydata = Convert.ToDouble(String.Format("{0:F2}", fTempsbd));
			}

			//SBSx0e,	//AbsoluteStateOfCharge
			if (ParamSBS0x0E != null)
			{
				fTempsbd = myProject.dbDesignCp;//fRam[(int)prFloatIndex.iDesignCapacity];
				//fTempsbd = (100 * fRam[(int)prFloatIndex.iCAR]) / fTempsbd;
				fTempsbd = (100 * fRam[(int)prFloatIndex.iCAMah]) / fTempsbd;
				//ParamSBS0x0E.phydata = fTempsbd;
				ParamSBS0x0E.phydata = Convert.ToDouble(String.Format("{0:F2}", fTempsbd));
			}

			//SBSx0f, RemainingCapacity
			if(ParamSBS0x0F != null)
			{
				//ParamSBS0x0F.phydata = fRam[(int)prFloatIndex.iCAR];
				//ParamSBS0x0F.phydata = fRam[(int)prFloatIndex.iCAMah];
				ParamSBS0x0F.phydata = Convert.ToDouble(String.Format("{0:F2}", fRam[(int)prFloatIndex.iCAMah]));
			}
			//SBSx10 is done with 0x0d
			if (ParamSBS0x10 != null)
			{
				fTempsbd = fRam[(int)prFloatIndex.iFCC];
				//ParamSBS0x10.phydata = fTempsbd;
				ParamSBS0x10.phydata = Convert.ToDouble(String.Format("{0:F2}",  fTempsbd));
			}
			//SBSx11, RunTimeToEmpty
			if(ParamSBS0x11 != null)
			{
				short ustate = (short)sRam[(int)prShortIndex.iBGState];
				float femp = 65535;

				if((ustate & (short)BGStateenum.BGSTATE_MASK) == (short)BGStateenum.BGSTATE_DISCHARGE)
				{
					if (((short)ParamSBS0x16.phydata & (short)SBS16enum.SBS_BSTAT_FULLY_DISCHARGED) != 0)
					{
						femp = 0;
					}
					else
					{
						femp = sbd_time_to_empty(fRam[(int)prFloatIndex.iCurrent]);
					}
				}
				//ParamSBS0x11.phydata = femp;
				ParamSBS0x11.phydata = Convert.ToDouble(String.Format("{0:F2}", femp));
			}
			//SBSx12, AverageTimeToEmpty
			if(ParamSBS0x12 != null)
			{
				short ustate = (short)sRam[(int)prShortIndex.iBGState];
				float femp = 65535;

				if ((ustate & (short)BGStateenum.BGSTATE_MASK) == (short)BGStateenum.BGSTATE_DISCHARGE)
				{
					if (((short)ParamSBS0x16.phydata & (short)SBS16enum.SBS_BSTAT_FULLY_DISCHARGED) != 0)
					{
						femp = 0;
					}
					else
					{
						femp = sbd_time_to_empty(fRam[(int)prFloatIndex.iCurrent]);
					}
				}
				//ParamSBS0x12.phydata = femp;
				ParamSBS0x12.phydata = Convert.ToDouble(String.Format("{0:F2}", femp));
			}
			//SBSx13, AverageTimeToFull
			if(ParamSBS0x13 != null)
			{
				short ustate = (short)sRam[(int)prShortIndex.iBGState];
				float femp = 65535;

				if ((ustate & (short)BGStateenum.BGSTATE_MASK) == (short)BGStateenum.BGSTATE_CHARGE)
				{
					if (((short)ParamSBS0x16.phydata & (short)SBS16enum.SBS_BSTAT_FULLY_CHARGED) != 0)
					{
						femp = 0;
					}
					else
					{
						femp = sbd_time_to_full(fRam[(int)prFloatIndex.iCurrent]);
					}
				}
				//ParamSBS0x13.phydata = femp;
				ParamSBS0x13.phydata =Convert.ToDouble(String.Format("{0:F2}",  femp));
			}
			//SBSx14, ChargingCurrent
			if(ParamSBS0x14 != null)
			{
				ParamSBS0x14.phydata = Convert.ToDouble(String.Format("{0:F2}","1000"));// TBD; should come from SeaElf
			}
			//SBSx15, ChargingVoltage
			if(ParamSBS0x15 != null)
			{
				ParamSBS0x15.phydata = Convert.ToDouble(String.Format("{0:F2}", "4200")) ;// TBD; shoud come from SeaElf
			}
			//SBSx16, BatteryStatus
			if(ParamSBS0x16 != null)
			{
				ushort s16phy = (ushort)ParamSBS0x16.phydata;
				s16phy |= (ushort)SBS16enum.SBS_BSTAT_INITIALIZED;
				s16phy &= 0xFFF0;
				//low 3 bit to present iMode value
				if(sRam[(int)prShortIndex.iMode] > 0)
				{
					s16phy += 4;
				}
				else if(sRam[(int)prShortIndex.iMode] < 0)
				{
					s16phy += 2;
				}
				else
				{
					s16phy += 1;
				}
				//ParamSBS0x16.phydata = (float)s16phy;
				ParamSBS0x16.phydata = Convert.ToDouble(String.Format("{0:F2}", s16phy));
			}
			//SBSx17, CycleCount
			if(ParamSBS0x17 != null)
			{
				fTempsbd = fRam[(int)prFloatIndex.iAgeMah];
				fTempsbd /= myProject.dbDesignCp;//fRam[(int)prFloatIndex.iDesignCapacity];
				int iCyc = (int)fTempsbd ;// int.Parse(fTempsbd.ToString());
				//ParamSBS0x17.phydata = iCyc;
				ParamSBS0x17.phydata = Convert.ToDouble(String.Format("{0:F2}", iCyc));
			}
			//SBSx4D, AgeFactor
			if (ParamSBS0x4D != null)
			{
				//ParamSBS0x4D.phydata = Convert.ToDouble(String.Format("{0:F2}", "1.00"));		//TBD: wait implement of Age Factor calculation
				ParamSBS0x4D.phydata = Convert.ToDouble(String.Format("{0:F2}", fRam[(int)prFloatIndex.iAgeFactor]));	
			}

			pmcAll.parameterlist = PListSBSReg;
			tskMsgParent.task_parameterlist = pmcAll;
			tskMsgParent.task = TM.TM_CONVERT_PHYSICALTOHEX;
			devParent.AccessDevice(ref tskMsgParent);
			while (tskMsgParent.bgworker.IsBusy)
				System.Windows.Forms.Application.DoEvents();
			System.Windows.Forms.Application.DoEvents();

			//SBSxE0 
			if (ParamSBSxE0BgState != null)
			{
				//ParamSBSxE0BgState.phydata = (float)sRam[(int)prShortIndex.iBGState];
				ParamSBSxE0BgState.phydata = Convert.ToDouble(String.Format("{0:F2}", sRam[(int)prShortIndex.iBGState]));
			}
			//SBSxE1 
			if (ParamSBSxE1BGStatus != null)
			{
				//ParamSBSxE1BGStatus.phydata = (float)sRam[(int)prShortIndex.iBGStatus];
				ParamSBSxE1BGStatus.phydata = Convert.ToDouble(String.Format("{0:F2}", sRam[(int)prShortIndex.iBGStatus]));
			}
			//SBSxF0 
			//if (ParamSBSxF0CarDiff != null)
			//{
				//ParamSBSxF0CarDiff.phydata = fRam[(int)prFloatIndex.iCarDiff];
			//}
			//SBSxF1
			if (ParamSBSxF1CTMah != null)
			{
				//ParamSBSxF1CTMah.phydata = fRam[(int)prFloatIndex.iCTMah];
				ParamSBSxF1CTMah.phydata = Convert.ToDouble(String.Format("{0:F2}", fRam[(int)prFloatIndex.iCTMah]));
			}
			//SBSxF2
			if (ParamSBSxF2CAMah != null)
			{
				//ParamSBSxF2CAMah.phydata = fRam[(int)prFloatIndex.iCAMah];
				ParamSBSxF2CAMah.phydata = Convert.ToDouble(String.Format("{0:F2}", fRam[(int)prFloatIndex.iCAMah]));
			}
			//SBSxF3
			if (ParamSBSxF3Prev != null)
			{
				//ParamSBSxF3Prev.phydata = fRam[(int)prFloatIndex.iPrevCAMah];
				ParamSBSxF3Prev.phydata = Convert.ToDouble(String.Format("{0:F2}", fRam[(int)prFloatIndex.iPrevCAMah]));
			}
			//SBSxF4
			if (ParamSBSxF4CRMah != null)
			{
				//ParamSBSxF4CRMah.phydata = fRam[(int)prFloatIndex.iCRMah];
				ParamSBSxF4CRMah.phydata = Convert.ToDouble(String.Format("{0:F2}", fRam[(int)prFloatIndex.iCRMah]));
			}
			//SBSxF5
			if (ParamSBSxF5SelfMah != null)
			{
				//ParamSBSxF5SelfMah.phydata = fRam[(int)prFloatIndex.iSelfMah];
				//ParamSBSxF5SelfMah.phydata = fRam[(int)prFloatIndex.iCAR];
				ParamSBSxF5SelfMah.phydata = Convert.ToDouble(String.Format("{0:F2}", fRam[(int)prFloatIndex.iCAR]));
			}
			//SBSxF6
			if (ParamSBSxF6FCC != null)
			{
				//ParamSBSxF6FCC.phydata = fRam[(int)prFloatIndex.iFCC];
				ParamSBSxF6FCC.phydata = Convert.ToDouble(String.Format("{0:F2}", fRam[(int)prFloatIndex.iFCC]));
			}

			devParent.bSBSReady = true;
		}

		#endregion

		#region statemachine.c porting

		private void sm_init()
		{
			//it's done in gdm_init(); actually, Eagle did this setting twice
			//sRam[(int)prShortIndex.iBGState] = (short)BGStateenum.BGSTATE_IDLE;
			//sRam[(int)prShortIndex.iBGStatus] = 0x0000;
			//sRam[(int)prShortIndex.iBGPMode] = (short)BGPwrenum.FW_PMODE_FULL;
		}

		//private int sm_bgstate_check()	; skip, directly check iMode value

		//private void sm_pdm_control(PdmCtrl ctl); skip 

		private void sm_update_timers()
		{
			bool bRet = true;
			//UInt16 u16Addr = 0xFF;

			//tmm_timer_counter();timer count down; skip
			//check alert/event; skip
			//check push button; skip
			//500mS passed stuff...... no need to do

			//collect ADC per 1 second
			#region read ADC data, and gg_update_fctmah()

			if (bDEMAccess)
			{
				//daq_adc_data_read() combine daq_gen_evt_collect()
				bRet = daq_adc_data_read();
			}
			else
			{
				GGType noDem;
				float ftmp;
				//foreach (Parameter pmScan in pmcGGPolling.parameterlist)
				foreach (Parameter pmScan in PListGGPolling)
				{
					noDem = (GGType)Convert.ToUInt16(pmScan.key) & GGType.OZGGPollMask;
					//ftmp = Convert.ToSingle(pmScan.phydata);
					switch (noDem)
					{
						case GGType.OZVoltage:
							{
								ftmp = 3500f;
								fRam[(int)prFloatIndex.iVoltDiff] = ftmp - fRam[(byte)prFloatIndex.iVoltage];
								fRam[(int)prFloatIndex.iVoltage] = ftmp;
								break;
							}
						case GGType.OZCurrent:
							{
								ftmp = 1.29F;
								fRam[(int)prFloatIndex.iCurrDiff] = ftmp - fRam[(byte)prFloatIndex.iCurrent];
								fRam[(int)prFloatIndex.iCurrent] = ftmp;
								break;
							}
						case GGType.OZExtTemp:
							{
								ftmp = 324F;
								//fRam[(byte)prFloatIndex.iTempDiff] = ftmp - fRam[(byte)prFloatIndex.iTemperature];
								fRam[(int)prFloatIndex.iVoltage] = ftmp;
								sRam[(int)prShortIndex.iExtTempDK] = (short)myProject.LutThermalDK(ftmp);
								break;
							}
						case GGType.OZCAR:
							{
								ftmp = 2.5F;
								//(M140407)Francis, use CAR as Colomb Counting, read it as different and clear it every time
								fRam[(byte)prFloatIndex.iCarDiff] = ftmp;// -fRam[(byte)prFloatIndex.iCAR];
								//fRam[(byte)prFloatIndex.iCAR] = ftmp;
								//(E140407)
								break;
							}
						default:
							break;

					}
				}
			}	//if (bDEMAccess)

			gg_update_fctmah();

			//led_display();	//display led control;	skip

			#endregion

			//one second stuff
			//calculate average current, no need to do
			//broadcase support, no need to do
			//low cell voltage collection, no need to do
			//one minute stuff
			sRam[(int)prShortIndex.iOneMinTmr] -= 1;
			if (sRam[(int)prShortIndex.iOneMinTmr] <= 0)
			{
				#region 2 hours stuff
				if (((ushort)sRam[(int)prShortIndex.iBGStatus] & (ushort)BGStatusenum.BG_STATUS_IDLE_2HOUR) != 0)
				{
					float fcomp = myProject.LutCapFromOCVTable(fRam[(int)prFloatIndex.iVoltage]);
					if (fRam[(int)prFloatIndex.iOCVCompsate] == 0)		//1st time here, no compensate
					{
						fRam[(int)prFloatIndex.iOCVCompsate] = fcomp;
					}
					else
					{
						float fdelta = fcomp - fRam[(int)prFloatIndex.iOCVCompsate];
						if (fdelta < 0)
						{
							fRam[(int)prFloatIndex.iOCVCompsate] = fcomp;
							fRam[(int)prFloatIndex.iCAR] += fdelta;			//add into CC
							fRam[(int)prFloatIndex.iCAMah] += fdelta;		//add into adjusted 
							fRam[(int)prFloatIndex.iPrevCAMah] = fRam[(int)prFloatIndex.iCAMah];
						}
					}
					//gg_self_discharge(2);	//as Jon comment, we can ignore it
				}	// 2 hour stuff, if (((ushort)sRam[(int)prShortIndex.iBGStatus] & (ushort)BGStatusenum.BG_STATUS_IDLE_2HOUR) != 0)	
				#endregion

				#region DFCC stuff
				//Check thermal stable --- once per minute
				if (bDFCCSupport)
				{
					sRam[(int)prShortIndex.iThermStayCnt] += 1;
					if (sRam[(int)prShortIndex.iThermStayCnt] <= 10)
					{
						float fDiff = sRam[(int)prShortIndex.iExtTempDK] - sRam[(int)prShortIndex.iTempStableDK];
						if (fDiff < 0) fDiff *= -1F;
						if (fDiff > 30)		//DFCC_THERM_STABLE_DELTA
						{
							sRam[(int)prShortIndex.iThermStayCnt] = 0;
							sRam[(int)prShortIndex.iTempStableDK] = sRam[(int)prShortIndex.iExtTempDK];
							sRam[(int)prShortIndex.iBGStatus] &= ~((short)BGStatusenum.BG_STATUS_THERM_STABLE);
						}
					}
					else
					{
						sRam[(int)prShortIndex.iBGStatus] |= ((short)BGStatusenum.BG_STATUS_THERM_STABLE);
					}
				}
				#endregion

				sRam[(int)prShortIndex.iOneMinTmr] = 60;
				sRam[(int)prShortIndex.iGGMinTimer] += 1;
				sRam[(int)prShortIndex.iOneHrTmr] -= 1;
			}	// one minute stuff, if (sRam[(int)prShortIndex.iOneMinTmr] <= 0)

			if (sRam[(int)prShortIndex.iOneHrTmr] <= 0)
			{
				sRam[(int)prShortIndex.iOneHrTmr] = 60;
				//gdm_gtype_update();				//update g-type parameters
			}

			/* it was done sbd_update_sbs()
			if (bRet)
			{
				foreach (Parameter pmsbs in pmcSBSReg.parameterlist)
				{
					u16Addr = (UInt16)((pmsbs.guid & 0x0000FF00) >> 8);
					switch (u16Addr)
					{
						case (UInt16)EGSBS.SBSVoltCell01:
							{
								//pmsbs.phydata += 2.5F;
								pmsbs.phydata = fRam[(byte)prFloatIndex.iVoltage];
								break;
							}
						case (UInt16)EGSBS.SBSCurrent:
							{
								//pmsbs.phydata += 0.7F;
								pmsbs.phydata = fRam[(byte)prFloatIndex.iCurrent];
								break;
							}
						case (UInt16)EGSBS.SBSExtTemp01:
							{
								//pmsbs.phydata += 0.1F;
								pmsbs.phydata = fRam[(byte)prFloatIndex.iTemperature];
								break;
							}
						case (UInt16)EGSBS.SBSRSOC:
							{
								pmsbs.phydata += 1.0F;
								break;
							}
						case (UInt16)EGSBS.SBSRC:
							{
								//pmsbs.phydata += 123.0F;
								pmsbs.phydata = fRam[(byte)prFloatIndex.iCAR];
								break;
							}
					}
				}
				devParent.bBusy = false;
			}	//if (bRet)
			 * */

		}

		//private void sm_pmode_timer_process(); skip

		private void sm_idle_process()
		{
			UInt16 state_timer = 0;	//state timer in minute

			//check state time
			if (sRam[(int)prShortIndex.iBGState] != (short)BGStateenum.BGSTATE_IDLE_START)
			{
				tmm_state_timer_chk(ref state_timer);
			}
			if (state_timer > 120)	//idle 2 hours process
			{
				sRam[(int)prShortIndex.iBGStatus] |= (short)BGStatusenum.BG_STATUS_IDLE_2HOUR;
				tmm_state_timer_start();
			}

			//check idle stop
			if (sRam[(int)prShortIndex.iMode] != 0)
			{
				sRam[(int)prShortIndex.iBGState] = (short)BGStateenum.BGSTATE_IDLE_ENDED;
			}

			if (sRam[(int)prShortIndex.iBGState] == (short)BGStateenum.BGSTATE_IDLE_START)
			{
				sRam[(int)prShortIndex.iBGState] = (short)BGStateenum.BGSTATE_IDLE_RUN;
				tmm_state_timer_start();
			}
			else if (sRam[(int)prShortIndex.iBGState] == (short)BGStateenum.BGSTATE_IDLE_RUN)
			{
				// Idle state precharge process; skip
				// Sleep Power mode transition; skip
			}
			else if (sRam[(int)prShortIndex.iBGState] == (short)BGStateenum.BGSTATE_IDLE_ENDED)
			{
				if (sRam[(int)prShortIndex.iMode] > 0)
				{
					sRam[(int)prShortIndex.iBGState] = (short)BGStateenum.BGSTATE_CHARGE_START;
				}
				else if (sRam[(int)prShortIndex.iMode] < 0)
				{
					sRam[(int)prShortIndex.iBGState] = (short)BGStateenum.BGSTATE_DISCHARGE_START;
				}
				else
				{
					sRam[(int)prShortIndex.iBGState] = (short)BGStateenum.BGSTATE_IDLE_START;
				}

				//tmm_clr_timer();

				fRam[(int)prFloatIndex.iOCVCompsate] = 0F;
			}
		}

		private void sm_charge_process()
		{
			UInt16 state_timer = 0;	//state timer in minute

			//check state timer
			if (sRam[(int)prShortIndex.iBGState] != (short)BGStateenum.BGSTATE_CHARGE_START)
			{
				tmm_state_timer_chk(ref state_timer);
			}
			//Maximum charge time; skip
			//if (state_timer > 480)
			//{
			//}

			//check charge stop
			if (sRam[(int)prShortIndex.iMode] < 0)
			{
				sRam[(int)prShortIndex.iBGState] = (short)BGStateenum.BGSTATE_CHARGE_ENDED;
			}

			//charge 
			if (sRam[(int)prShortIndex.iBGState] == (short)BGStateenum.BGSTATE_CHARGE_START)
			{
				sRam[(int)prShortIndex.iBGState] = (short)BGStateenum.BGSTATE_CHARGE_RUN;
				//precharge state check; skip

				tmm_state_timer_start();
				fRam[(int)prFloatIndex.iChgStartRC] = fRam[(int)prFloatIndex.iCTMah];		//save start charge capacity
				sRam[(int)prShortIndex.iBGStatus] &= ~((short)BGStatusenum.BG_STATUS_CONDITION_CHG);	//clear CND_CHG
				//check charge interval timer; skip
				//init AVGMA and AVGIMMA with SMPMA; skip
			}
			//else if(sRam[(int)prShortIndex.iBGState] == (short)BGStateenum.BGSTATE_CHARGE_PRECHG)	//precharge; skip
			//{
			//}
			else if (sRam[(int)prShortIndex.iBGState] == (short)BGStateenum.BGSTATE_CHARGE_RUN)
			{
				//OZ9310 & OZ9320 pulse charge; skip
				//fully charge, determined by SeaElf charger status regx41
				if ((sRam[(int)prShortIndex.iBGStatus] & (short)BGStatusenum.BG_STATUS_ENDMA) != 0)
				{
					sRam[(int)prShortIndex.iBGState] = (short)BGStateenum.BGSTATE_CHARGE_FULL;
				}
				else if (sRam[(int)prShortIndex.iMode] == 0)
				{
					sRam[(int)prShortIndex.iBGState] = (short)BGStateenum.BGSTATE_CHARGE_ENDED;
				}
			}
			//else if (bgstate == BGSTATE_CHARGE_PULSE) {; skip
			else if (sRam[(int)prShortIndex.iBGState] == (short)BGStateenum.BGSTATE_CHARGE_FULL)
			{
				//gdm_set_sbs_status(SBS_BSTAT_FULLY_CHARGED);
				//DBGCodeparamter.phydata += (float)SBS16enum.SBS_BSTAT_FULLY_CHARGED;
				ushort u16sbs = (ushort)ParamSBS0x16.phydata;
				u16sbs |= (ushort)SBS16enum.SBS_BSTAT_FULLY_CHARGED;
				ParamSBS0x16.phydata = (float)u16sbs;
				if (state_timer > 15)	//check timer, mark as conditional charged
				{
					sRam[(int)prShortIndex.iBGStatus] |= (short)BGStatusenum.BG_STATUS_CONDITION_CHG;
				}
				sRam[(int)prShortIndex.iBGState] = (short)BGStateenum.BGSTATE_CHARGE_ENDED;
			}
			else if (sRam[(int)prShortIndex.iBGState] == (short)BGStateenum.BGSTATE_CHARGE_ENDED)
			{
				if (sRam[(int)prShortIndex.iMode] < 0)
				{
					sRam[(int)prShortIndex.iBGState] = (short)BGStateenum.BGSTATE_DISCHARGE_START;
				}
				else if (sRam[(int)prShortIndex.iMode] == 0)
				{
					sRam[(int)prShortIndex.iBGState] = (short)BGStateenum.BGSTATE_IDLE_START;
				}
				else
				{
					return;
				}

				//Age update
				if (fRam[(int)prFloatIndex.iCTMah] > fRam[(int)prFloatIndex.iChgStartRC])
				{
					float fChgdelta = fRam[(int)prFloatIndex.iCTMah] - fRam[(int)prFloatIndex.iChgStartRC];
					fRam[(int)prFloatIndex.iAgeMah] += fChgdelta;
				}

				//FCC update		//if fully charged and condition charged found
				if (((sRam[(int)prShortIndex.iBGStatus] & (short)BGStatusenum.BG_STATUS_ENDMA) != 0)
					&& ((sRam[(int)prShortIndex.iBGStatus] & (short)BGStatusenum.BG_STATUS_CONDITION_CHG) != 0))
				{
					//if condition discharged also found
					if (((sRam[(int)prShortIndex.iBGStatus] & (short)BGStatusenum.BG_STATUS_CONDITION_DSG) != 0))
					{
						gg_fcc_update(GGFCCCase.FCC_CND);
					}
					else
					{
						gg_fcc_update(GGFCCCase.FCC_NORM);
					}
					//if BCFG_SOHENABLE SOH function enable; skip
					//set SOH function disable as default
				}
				//deal with fully charge, clear it
				sRam[(int)prShortIndex.iBGStatus] &= ~((short)BGStatusenum.BG_STATUS_ENDMA);
			}
		}

		private void sm_discharge_process()
		{
			UInt16 state_timer = 0;	//state timer in minute

			//check state timer
			if (sRam[(int)prShortIndex.iBGState] != (short)BGStateenum.BGSTATE_DISCHARGE_START)
			{
				tmm_state_timer_chk(ref state_timer);
			}

			//check discharge stop
			if (sRam[(int)prShortIndex.iMode] > 0)
			{
				sRam[(int)prShortIndex.iBGState] = (short)BGStateenum.BGSTATE_DISCHARGE_ENDED;
			}

			//discharge start
			if (sRam[(int)prShortIndex.iBGState] == (short)BGStateenum.BGSTATE_DISCHARGE_START)
			{
				sRam[(int)prShortIndex.iBGState] = (short)BGStateenum.BGSTATE_DISCHARGE_RUN;
				tmm_state_timer_start();
				sRam[(int)prShortIndex.iBGStatus] &= ~((short)BGStatusenum.BG_STATUS_CONDITION_DSG);	//clear CND_DSG
				//clear charge timeout; skip
				//init AVGMA and AVGIMMA with SMPMA; skip
			}
			else if(sRam[(int)prShortIndex.iBGState] == (short)BGStateenum.BGSTATE_DISCHARGE_RUN)
			{
				if (state_timer > 5)	//DFCC start time
				{
					sRam[(int)prShortIndex.iBGStatus] |= (short)BGStatusenum.BG_STATUS_DFCC_START;
				}
				//fully discharge check, just check if LowVolt event happened
				if ((sRam[(int)prShortIndex.iBGStatus] & (short)BGStatusenum.BG_STATUS_LOWVOLT) != 0)
				{
					//(A140918)Francis, add Eason's algorithm if voltage is below than ZeroVoltage but RSOC is not 0%
					if (ParamSBS0x0D.phydata > 0F)
					{
					}
					else
					{
						sRam[(int)prShortIndex.iBGState] = (short)BGStateenum.BGSTATE_DISCHARGE_FULL;
					}
					//(E140918)
				}
				else
				{
					if (sRam[(int)prShortIndex.iMode] == 0)
					{
						sRam[(int)prShortIndex.iBGState] = (short)BGStateenum.BGSTATE_DISCHARGE_ENDED;
					}
				}
			}
			else if(sRam[(int)prShortIndex.iBGState] == (short)BGStateenum.BGSTATE_DISCHARGE_FULL)
			{
				sRam[(int)prShortIndex.iBGStatus] |= (short)BGStatusenum.BG_STATUS_CONDITION_DSG;
				//DBGCodeparamter.phydata += (float)SBS16enum.SBS_BSTAT_FULLY_DISCHARGED;
				ushort u16sbs = (ushort)ParamSBS0x16.phydata;
				u16sbs |= (ushort)SBS16enum.SBS_BSTAT_FULLY_DISCHARGED;
				ParamSBS0x16.phydata = (float)u16sbs;
				sRam[(int)prShortIndex.iBGState] = (short)BGStateenum.BGSTATE_DISCHARGE_ENDED;
			}
			else if (sRam[(int)prShortIndex.iBGState] == (short)BGStateenum.BGSTATE_DISCHARGE_ENDED)
			{
				if (sRam[(int)prShortIndex.iMode] > 0)
				{
					sRam[(int)prShortIndex.iBGState] = (short)BGStateenum.BGSTATE_CHARGE_START;
				}
				else if (sRam[(int)prShortIndex.iMode] == 0)
				{
					sRam[(int)prShortIndex.iBGState] = (short)BGStateenum.BGSTATE_IDLE_START;
				}
				else
				{
					return;
				}
				//SOH CALCULATION
				if (((sRam[(int)prShortIndex.iBGStatus] & (short)BGStatusenum.BG_STATUS_CONDITION_DSG) != 0)
					|| (fRam[(int)prFloatIndex.iCTMah] <= 0F))
				{
					fRam[(int)prFloatIndex.iCTMah] = 0F;
				}
				fRam[(int)prFloatIndex.iCRMah] = fRam[(int)prFloatIndex.iCTMah];
			}
		}

		//empty, no simulate suspend function
		private void sm_suspend_process()
		{
		}

		//private void sm_callback_pmode(); skip

		//private void sm_callback_pulse(); skip

		//private void sm_callback_pdm(); skip

		//private void sm_sbs_broadcast; skip

		#endregion

		#region timmer.c porting

		private void tmm_state_timer_start()
		{
			sRam[(int)prShortIndex.iStateTmrSec] = 60;
			sRam[(int)prShortIndex.iStateTmrMin] = 0;
		}
		
		private void tmm_state_timer_chk(ref UInt16 pstatetimer)
		{
			if (sRam[(int)prShortIndex.iStateTmrSec] <= 0)
			{
				sRam[(int)prShortIndex.iStateTmrMin] += 1;
				sRam[(int)prShortIndex.iStateTmrSec] += 60;
			}
			pstatetimer = (ushort)sRam[(int)prShortIndex.iStateTmrMin];
		}

		/*private void tmm_clr_timer(ref byte ytimer)
		{
		}
		 * */

		#endregion

		#region oz_gg_agecomp.c porting

		private void age_compensate_init()
		{
			age_comp.abs_cap_now = fRam[(int)prFloatIndex.iCTMah];
			age_comp.age_factor_last = 1;
			age_comp.age_factor_now = 1;
			age_comp.full_abs_cap_last = (int)myProject.dbDesignCp;
			age_comp.full_abs_cap_now = (int)myProject.dbDesignCp;
			age_comp.chg_ri_tick = 0;
			age_comp.ri_count = 0;
			age_comp.ri_now = 1;
			age_comp.ri_25 = 1;
			age_comp.rch25_now = 100;
			age_comp.quiet_state = false;
			age_comp.quiet_tick = 0;
			age_comp.bg_state_prev = BgAgeState. BG_DISCHARGE;
			age_comp.bg_state_now = BgAgeState.BG_DISCHARGE;
			age_comp.temp_ri_low = 10;
			age_comp.temp_ri_high = 40;

			age_comp.fabs_high.pt_set = false;
			age_comp.fabs_high.pt_volt = 0;
			age_comp.fabs_high.pt_time = 0;
			age_comp.fabs_high.pt_curr = 0;
			age_comp.fabs_high.pt_abscap = 0;
			age_comp.fabs_high.pt_soc = 0;

			age_comp.fabs_low.pt_set = false;
			age_comp.fabs_low.pt_volt = 0;
			age_comp.fabs_low.pt_time = 0;
			age_comp.fabs_low.pt_curr = 0;
			age_comp.fabs_low.pt_abscap = 0;
			age_comp.fabs_low.pt_soc = 0;
		}

		private void age_compensate_process(BgAgeState sys_state)
		{
			BgAgeState age_state;
			//Update state status in age_comp
			if (((sys_state == BgAgeState.BG_CHARGE) && (fRam[(int)prFloatIndex.iCurrent] < age_comp.current_quiet_min))
				|| ((sys_state == BgAgeState.BG_DISCHARGE) && (fRam[(int)prFloatIndex.iCurrent] > (-1*age_comp.current_quiet_min))))
				age_state = BgAgeState.BG_QUIET;				//QUIET STATE
			else
				age_state = sys_state;

			if (age_comp.bg_state_now != age_comp.bg_state_prev)
			{
				age_comp.bg_state_prev = age_comp.bg_state_now;
				age_comp.bg_state_now = age_state;
			}

			///***********************************************
			//Check and process quiet state high/low point collection
			///***********************************************
			age_compensate_quiet_state(sys_state);

			///***********************************************
			//Calculate Charge Ri
			///***********************************************
			age_compensate_ri_update(sys_state);

		}

		private void age_compensate_quiet_state(BgAgeState sys_state)
		{
			long data;
			int delta_cap;
			int delta_soc;
			int fabs_new;
			///***********************************************
			//check if charge-discharge condition available
			///***********************************************
			//1. high point not set --- must only allow charge
			if ((!age_comp.fabs_high.pt_set)	//high point check
				&& (age_comp.bg_state_now == BgAgeState.BG_DISCHARGE))
			{
				//clear low point data
				age_comp.fabs_low.pt_set = false;
			}
			//2. low point not set --- must only allow discharge
			if ((!age_comp.fabs_low.pt_set)	//low point check
				&& (age_comp.bg_state_now == BgAgeState.BG_CHARGE))
			{
				//clear high point data
				age_comp.fabs_high.pt_set = false;
			}

			///***********************************************
			//quiet state check
			///***********************************************
			if (age_comp.bg_state_now != BgAgeState.BG_QUIET)
			{
				age_comp.quiet_state = false;	//clear quiet state flag
				age_comp.quiet_tick = 0;	//clear tick
				return;
			}
			else
			{
				age_comp.quiet_state = true;	//set quiet state
				age_comp.quiet_tick = Convert.ToUInt16(bmutick);	//save tick
			}

			///***********************************************
			//quiet state point data collection
			///***********************************************
			if ((bmutick - age_comp.quiet_tick) >= age_comp.quiet_timeout)
			{
				//check range 1 (low to mid-low)
				//if ((batt_info.fVolt >= age_comp.volt_fabs_pt1)
					//&& (batt_info.fVolt <= age_comp.volt_fabs_pt2))
				if ((fRam[(int)prFloatIndex.iVoltage] >= age_comp.volt_fabs_pt1)
					&& (fRam[(int)prFloatIndex.iVoltage] <= age_comp.volt_fabs_pt2))
				{
					age_comp.fabs_low.pt_set = true;
					//age_comp.fabs_low.pt_volt = batt_info.fVolt;
					age_comp.fabs_low.pt_volt = (int)fRam[(int)prFloatIndex.iVoltage];
					age_comp.fabs_low.pt_time = Convert.ToUInt16(bmutick);
					//age_comp.fabs_low.pt_curr = batt_info.fCurr;
					age_comp.fabs_low.pt_curr = (int)fRam[(int)prFloatIndex.iCurrent];
					//age_comp.fabs_low.pt_abscap = gas_gauge.sCtMAH;
					age_comp.fabs_low.pt_abscap = (int)fRam[(int)prFloatIndex.iCTMah];
					//data = one_latitude_table(parameter.ocv_data_num, parameter.ocv, age_comp.fabs_low.pt_volt);
					data = (long)myProject.LutCapFromOCVTable(age_comp.fabs_low.pt_volt);
					age_comp.fabs_low.pt_soc = (int)(((data * age_comp.full_abs_cap_now) / 100));
				}

				//check range 2 (mid-high to high)
				//if ((batt_info.fVolt >= age_comp.volt_fabs_pt3)
					//&& (batt_info.fVolt <= age_comp.volt_fabs_pt4))
				if ((fRam[(int)prFloatIndex.iVoltage] >= age_comp.volt_fabs_pt3)
					&& (fRam[(int)prFloatIndex.iVoltage] <= age_comp.volt_fabs_pt4))
				{
					age_comp.fabs_high.pt_set = true;
					//age_comp.fabs_high.pt_volt = batt_info.fVolt;
					age_comp.fabs_high.pt_time = Convert.ToUInt16(bmutick);
					//age_comp.fabs_high.pt_curr = batt_info.fCurr;
					age_comp.fabs_high.pt_curr = (int)fRam[(int)prFloatIndex.iCurrent];
					//age_comp.fabs_high.pt_abscap = gas_gauge.sCtMAH;
					age_comp.fabs_high.pt_abscap = (int)fRam[(int)prFloatIndex.iCTMah];
					//data = one_latitude_table(parameter.ocv_data_num, parameter.ocv, age_comp.fabs_high.pt_volt);
					data = (long)myProject.LutCapFromOCVTable(age_comp.fabs_high.pt_volt);
					age_comp.fabs_low.pt_soc = (int)((data * age_comp.full_abs_cap_now) / 100);
				}
			}

			///***********************************************
			//Calculate FullAbsCap
			///***********************************************
			if ((age_comp.fabs_high.pt_set) && (age_comp.fabs_low.pt_set))
			{
				delta_cap = (age_comp.fabs_high.pt_abscap - age_comp.fabs_low.pt_abscap);
				delta_soc = (age_comp.fabs_high.pt_soc - age_comp.fabs_low.pt_soc);
				fabs_new = delta_cap / delta_soc;
				age_comp.full_abs_cap_now = age_comp.full_abs_cap_last
						+ (int)((fabs_new - age_comp.full_abs_cap_last) * age_comp.k_factor);
			}
		}

		private void age_compensate_ri_update(BgAgeState sys_state)
		{
			int ocv;
			double r25, rnow;
			//double soc_now = batt_info.sCtMAH / age_comp.full_abs_cap_now;
			double soc_now = fRam[(int)prFloatIndex.iCTMah] / age_comp.full_abs_cap_now;

			///***********************************************
			//Only do Ri update in charge state
			///***********************************************
			if (sys_state == BgAgeState.BG_DISCHARGE)
			{
				age_comp.chg_ri_tick = 0;
				age_comp.ri_count = 0;
				return;
			}

			///***********************************************
			//First time here, save system time only
			///***********************************************
			if (age_comp.chg_ri_tick == 0)
			{
				age_comp.chg_ri_tick = Convert.ToUInt16(bmutick);	//save tick
				age_comp.ri_25 = 0;
				age_comp.ri_count = 0;
				return;
			}

			///***********************************************
			//Ri update condition check
			///***********************************************
			if (((bmutick - age_comp.chg_ri_tick) > age_comp.charge_ri_timeout)
				//&& (batt_info.fCellTemp >= age_comp.temp_ri_low)
				//&& (batt_info.fCellTemp <= age_comp.temp_ri_high))
				&& (sRam[(int)prShortIndex.iExtTempDK] >= age_comp.temp_ri_low)
				&& (sRam[(int)prShortIndex.iExtTempDK] <= age_comp.temp_ri_high))
			{
				if ((soc_now >= age_comp.soc_ri_start)
					&& (soc_now <= age_comp.soc_ri_end))
				{
					float fsoc_bit = (float)soc_now;
					//ocv = one_latitude_rev_table(parameter.ocv_data_num, parameter.ocv, soc_now);
					fsoc_bit /= 100;
					fsoc_bit *= 32768;		//convert to 32767 present
					ocv = (int)myProject.LutOCVbyTSOC(fsoc_bit);
					//age_comp.ri_now = (ocv - batt_info.fVolt) / (batt_info.fCurr);
					age_comp.ri_now = (ocv - fRam[(int)prFloatIndex.iVoltage]) / (fRam[(int)prFloatIndex.iCurrent]);
					r25 = get_rch_by_t(25);
					rnow = get_rch_by_t(sRam[(int)prShortIndex.iExtTempDK]);
					age_comp.ri_25 += (int)(age_comp.ri_now * (r25 / rnow));
					age_comp.ri_count++;
				}
				else if (soc_now > age_comp.soc_ri_end)
				{
					age_comp.rch25_now = (age_comp.ri_25 / age_comp.ri_count);
					age_comp.age_factor_now = (float)(age_comp.rch25_now / age_comp.rch25_new);
					age_comp.age_factor_now = age_comp.age_factor_last
						+ (float)((age_comp.age_factor_now - age_comp.age_factor_last) * age_comp.k_factor);

					age_comp.age_factor_last = age_comp.age_factor_now;	//update new
					age_comp.chg_ri_tick = 0;
				}
			}
			else
			{
				age_comp.chg_ri_tick = 0;
				age_comp.ri_count = 0;
			}
		}

		private void age_compensate_rc_lookup()
		{
			double curr_adj, volt_adj, volt_rc_max;
			double soc_eod, soc_now;
			int	teod;
			byte rc_result = 0;
			int voltage_end = (int)myProject.dbDsgEndVolt;// config_data->discharge_end_voltage;


			//curr_adj = batt_info.fCurr * age_comp.age_factor_now;
			curr_adj = fRam[(int)prFloatIndex.iCurrent] * age_comp.age_factor_now;
			//volt_adj = voltage_end - (batt_info.fCurr * age_comp.res_line);
			volt_adj = voltage_end - (fRam[(int)prFloatIndex.iCurrent] * age_comp.res_line);
			volt_rc_max = age_comp.volt_rc_max;	//parameter

			//teod = calc_teod_by_ieod(
							//curr_adj, age_comp.rch25_now, 
							//batt_info.fCellTemp, 
							//age_comp.r_thermal, 
							//age_comp.delta_t);
			teod = calc_teod_by_ieod(
							Convert.ToInt16(curr_adj), age_comp.rch25_now,
							sRam[(int)prShortIndex.iExtTempDK],
							age_comp.r_thermal,
							age_comp.delta_t);

			//rc_result = OZ8806_LookUpRCTable(
							//volt_adj,
							//-curr_adj * 10000 / age_comp.full_abs_cap_now,
							//teod * 10,
							//ref soc_eod);
			soc_eod = myProject.LutRCTable(
								(float)volt_adj,
								(float)(-10000 * curr_adj / age_comp.full_abs_cap_now),
								teod * 10);

			if (rc_result != 0) {
				//volt_adj = batt_info.fVolt - (batt_info.fCurr * age_comp.res_line);
				volt_adj = fRam[(int)prFloatIndex.iVoltage] - (fRam[(int)prFloatIndex.iCurrent] * age_comp.res_line);
				//rc_result = OZ8806_LookUpRCTable(
							//volt_adj,
							//-curr_adj * 10000 / age_comp.full_abs_cap_now,
							//batt_info.fCellTemp * 10,
							//&soc_now);
				soc_now = myProject.LutRCTable(
									(float)volt_adj,
									(float)(-10000 * curr_adj / age_comp.full_abs_cap_now),
									sRam[(int)prShortIndex.iExtTempDK]);

				if (rc_result != 0)
				{
					age_comp.abs_cap_now = (float)(soc_now - soc_eod) * age_comp.full_abs_cap_now / 10000;
					return;
				}
			}
			//age_comp.abs_cap_now = batt_info->fRC;	//set CAR value if table lookup failed  
			age_comp.abs_cap_now = fRam[(int)prFloatIndex.iCAR];// batt_info->fRC;	//set CAR value if table lookup failed  
		}

		private int calc_teod_by_ieod(int ieod, double rch25, int tamb, double rth, double dt)
		{
			float Rch, Pd;
			UInt32 SqIeod;
			int Teod1, Teod2;

			SqIeod = (UInt32)(ieod * ieod);
			Rch = (float)(rch25 * get_rch_by_t(tamb) / 100);
			Pd = SqIeod * Rch;
			Teod2 = (int)(tamb + (Pd * rth));
			Teod1 = tamb;
			while (((Teod2 - Teod1) > dt) || ((Teod1 - Teod2) > dt)) {
				Rch = (float)(rch25 * get_rch_by_t(Teod2) / 100);
				Pd = SqIeod * Rch;
				Teod2 = (int)(tamb + (Pd * rth));
				Teod1 = Teod2;
			}
			return Teod2;
		}

		/*
		private float one_latitude_rev_table(int number, one_latitude_data_t* data, double value)
		{
			int j;
			int res;

			for (j = 0; j < number; j++)
			{
				if (data[j].y == value)
				{
					res = data[j].x;
					return res;
				}
				if (data[j].y > value)
					break;
			}

			if (j == 0)
				res = data[j].x;
			else if (j == number)
				res = data[j - 1].x;
			else
			{
				res = ((value - data[j - 1].y) * (data[j].x - data[j - 1].x));
				if ((data[j].y - data[j - 1].y) != 0)
					res = res / (data[j].y - data[j - 1].y);
				res += data[j - 1].x;
			}
			return res;
		}
		*/

		//
		private float get_rch_by_t(int intamb)
		{
			return myProject.LutRiFromTemp((float)(sRam[(int)prShortIndex.iExtTempDK] - 2730));
			//return 100F;
		}

		//private 

		#endregion

		/*
		private string SearchChipPollXML(Parameter pamIn, string strNode = "SBSLoc")
		{
			foreach (DictionaryEntry de in pamIn.sfllist["OZBattery"].nodetable)
			{
				switch (de.Key.ToString())
				{
					case "NickName":
						{
							chipPhs.strNickName = de.Value.ToString();
							break;
						}
					case "DefValue":
						{
							chipPhs.shtValue = Convert.ToInt16(de.Value.ToString(), 16);
							break;
						}
					case "Type":
						{
							chipPhs.tType = (GGType)Convert.ToUInt16(de.Value.ToString(), 16);
							paramIn.key = (Double)Convert.ToUInt16(de.Value.ToString(), 16);
							break;
						}
					case "SBSLoc":
						{
							chipPhs.ySBSLoc = Convert.ToByte(de.Value.ToString(), 16);
							break;
						}
					case "Chip":
						{
							chipPhs.yChip = Convert.ToByte(de.Value.ToString(), 16);
							break;
						}
				}
			}
		}
		 * */

		#region Read/Write/Polling function through DEM

		private bool GetOCVdata(ref float fOCVmV)
		{
			bool bOCV = false;
			Reg addrLo = new Reg();
			Reg addrHi = new Reg();

			addrLo.address = 0x10;
			addrLo.startbit = 0x04;
			addrLo.bitsnumber = 0x04;
			addrHi.address = 0x11;
			addrHi.startbit = 0;
			addrHi.bitsnumber = 0x08;

			bOCV = ReadChipsShort(yOZ8806SubSec, ref addrLo, ref addrHi, ref fOCVmV);

			return bOCV;
		}

		private bool GetPoOCVFlag(ref byte yFlag)
		{
			bool bFlag = false;
			Reg regOCV = new Reg();

			regOCV.address = 0x10;
			regOCV.startbit = 0;
			regOCV.bitsnumber = 0x08;

			bFlag = ReadChipsByte(yOZ8806SubSec, ref regOCV, ref yFlag, true);

			return bFlag;
		}

		private bool AccessCtrlReg(bool bWriteR, bool bReadR, ref byte yValData)
		{
			bool bCtrl = false;
			Reg regCtrl = new Reg();

			regCtrl.address = 0x09;
			regCtrl.startbit = 0;
			regCtrl.bitsnumber = 0x08;

			if (bWriteR)
			{
				bCtrl = WriteChipsReg(yOZ8806SubSec, ref regCtrl, yValData, true);
			}
			else if (bReadR)
			{
				bCtrl = ReadChipsByte(yOZ8806SubSec, ref regCtrl, ref yValData, true);
			}

			return bCtrl;
		}

		private bool AccessPECControl(bool bWriteP, bool bReadP, ref byte yValPEC)
		{
			bool bPEC = false;
			Reg regPEC = new Reg();
			byte yInput = yValPEC;

			regPEC.address = 0x08;
			regPEC.startbit = 0;
			regPEC.bitsnumber = 0x08;

			if (bWriteP)
			{
				bPEC = WriteChipsReg(yOZ8806SubSec, ref regPEC, yInput, true);
			}
			else if (bReadP)
			{
				bPEC = ReadChipsByte(yOZ8806SubSec, ref regPEC, ref yInput, true);
			}

			return bPEC;
		}

		private bool AccessBoardCurrent(bool bWriteA, bool bReadA, ref float fValData)
		{
			bool bBdoff = false;
			Reg regBoardoffLo = new Reg();
			Reg regBoardoffHi = new Reg();

			regBoardoffLo.address = 0x18;
			regBoardoffLo.startbit = 0;
			regBoardoffLo.bitsnumber = 0x08;
			regBoardoffHi.address = 0x19;
			regBoardoffHi.startbit = 0;
			regBoardoffHi.bitsnumber = 0x03;

			if (bWriteA)
			{
				fValData *= myProject.dbRsense;	//convert to Voltage Unit
				bBdoff = WriteChipsFloat(yOZ8806SubSec, ref regBoardoffLo, ref regBoardoffHi, fValData);
				//bBdoff = WriteChipsReg(yOZ8806SubSec, ref regBoardoffLo, (byte)((byte)fValData & 0xFF), true);
			}
			else if (bReadA)
			{
				bBdoff = ReadChipsShort(yOZ8806SubSec, ref regBoardoffLo, ref regBoardoffHi, ref fValData);
			}

			return bBdoff;
		}

		private bool AccessCARReg(bool bWriteA, bool bReadA, ref float fValData)
		{
			bool bCAR0 = false;
			Reg regCARLo = new Reg();
			Reg regCARHi = new Reg();

			regCARLo.address = 0x14;
			regCARLo.startbit = 0;
			regCARLo.bitsnumber = 0x08;
			regCARHi.address = 0x15;
			regCARHi.startbit = 0;
			regCARHi.bitsnumber = 0x08;

			if (bWriteA)
			{
				bCAR0 = WriteChipsFloat(yOZ8806SubSec, ref regCARLo, ref regCARHi, fValData);
			}
			else if (bReadA)
			{
				bCAR0 = ReadChipsShort(yOZ8806SubSec, ref regCARLo, ref regCARHi, ref fValData);
			}

			return bCAR0;
		}

		private bool ReadChipsShort(byte yChip, ref Reg regLo, ref Reg regHi, ref float fData)//, bool bReadAll = false)
		{
			bool bRead = false;
			bool bFoundLo = false, bFoundHi = false;
			ParamContainer pmcTemp = new ParamContainer();
			Parameter pamTemp = null;
			//Reg regBackup = null;

			if ((devParent.bBusy))
			{
				devParent.msg.errorcode = LibErrorCode.IDS_ERR_EGDLL_BUS_BUSY;
				return bRead;
			}

			if ((regLo == null) || (regHi == null))
			{
				devParent.msg.errorcode = LibErrorCode.IDS_ERR_EGDLL_REGISTER;
				return bRead;
			}

			//foreach (Parameter pmRead in pmcGGSetting.parameterlist)
			foreach (Parameter pmRead in PListGGSetting)
			{
				if (pmRead.subsection == (UInt16)yChip)		//match chip's subsection type
				{
					foreach (KeyValuePair<string, Reg> tmpreg in pmRead.reglist)
					{
						if (tmpreg.Key.Equals("Low"))
						{
							if (tmpreg.Value.address == regLo.address)		//address match
							{
								//if (bReadAll)
								//{
								//regBackup = new Reg();
								//regBackup.address = tmpreg.Value.address;
								if ((regLo.bitsnumber == tmpreg.Value.bitsnumber) &&
									(regLo.startbit == tmpreg.Value.startbit))
								{
									bFoundLo = true;
									//break;
								}
							}
						}
						else if (tmpreg.Key.Equals("High"))
						{
							if (tmpreg.Value.address == regHi.address)		//address match
							{
								//if (bReadAll)
								//{
								//regBackup = new Reg();
								//regBackup.address = tmpreg.Value.address;
								if ((regHi.bitsnumber == tmpreg.Value.bitsnumber) &&
									(regHi.startbit == tmpreg.Value.startbit))
								{
									bFoundHi = true;
									break;
								}
							}
						}
					}
				}
				if ((bFoundLo) && (bFoundHi))
				{
					pmcTemp.parameterlist.Add(pmRead);
					pamTemp = pmRead;
					break;
				}
			}

			if ((!bFoundLo) || (!bFoundHi))
			{
				devParent.msg.errorcode = LibErrorCode.IDS_ERR_EGDLL_REGISTER;
				return bRead;
			}
			else
			{
				devParent.bBusy = true;
				tskMsgParent.task_parameterlist = pmcTemp;
				tskMsgParent.task = TM.TM_READ;
				devParent.AccessDevice(ref tskMsgParent);
				while (tskMsgParent.bgworker.IsBusy)
					System.Windows.Forms.Application.DoEvents();
				System.Windows.Forms.Application.DoEvents();
				if (bDEMAccess)
				{
					if (tskMsgParent.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
					{
						devParent.bBusy = false;
						return bRead;
					}
				}
				else
				{
					pamTemp.hexdata = 3900;
				}
				//if (bDEMAccess == true) and if(bDEMAccess == false) + (errorcode == SUCCESSFUL)
				//go belows
				{
					tskMsgParent.task = TM.TM_CONVERT_HEXTOPHYSICAL;
					devParent.AccessDevice(ref tskMsgParent);
					while (tskMsgParent.bgworker.IsBusy)
						System.Windows.Forms.Application.DoEvents();
					if (!float.TryParse(pamTemp.phydata.ToString(), out fData))
					{
						fData = 3900;
					}
					bRead = true;
				}
				//if (bReadAll)
				//{
					//foreach (KeyValuePair<string, Reg> pareg in pamTemp.reglist)
					//{
						//pareg.Value.startbit = regTart.startbit;
						//pareg.Value.bitsnumber = regTart.bitsnumber;
					//}
				//}
				devParent.bBusy = false;
			}

			return bRead;
		}

		private bool ReadChipsByte(byte yChip, ref Reg regTart, ref byte yData, bool bReadAll = false)
		{
			bool bRead = false;
			bool bFound = false;
			ParamContainer pmcTemp = new ParamContainer();
			Parameter	pamTemp = null;
			//Reg regBackup = null;

			if ((devParent.bBusy))
			{
				devParent.msg.errorcode = LibErrorCode.IDS_ERR_EGDLL_BUS_BUSY;
				return bRead;
			}
			if ((regTart == null))
			{
				devParent.msg.errorcode = LibErrorCode.IDS_ERR_EGDLL_REGISTER;
				return bRead;
			}

			//foreach (Parameter pmRead in pmcGGSetting.parameterlist)
			foreach (Parameter pmRead in PListGGSetting)
			{
				if (pmRead.subsection == (UInt16)yChip)		//match chip's subsection type
				{
					foreach (KeyValuePair<string, Reg> tmpreg in pmRead.reglist)
					{
						if (tmpreg.Value.address == regTart.address)		//address match
						{
							if(bReadAll)
							{
								//regBackup = new Reg();
								//regBackup.address = tmpreg.Value.address;
								regTart.bitsnumber = tmpreg.Value.bitsnumber;
								regTart.startbit = tmpreg.Value.startbit;
								tmpreg.Value.startbit = 0;
								tmpreg.Value.bitsnumber = 8;
								bFound = true;
								break;
							}
							else
							{
								if((regTart.bitsnumber == tmpreg.Value.bitsnumber) && 
									(regTart.startbit == tmpreg.Value.startbit))
								{
									bFound = true;
									break;
								}
							}
						}
					}
				}
				if (bFound)
				{
					pmcTemp.parameterlist.Add(pmRead);
					pamTemp = pmRead;
					break;
				}
			}

			if (!bFound)
			{
				devParent.msg.errorcode = LibErrorCode.IDS_ERR_EGDLL_PARAMETERRW;
				return bRead;
			}
			else
			{
				devParent.bBusy = true;
				tskMsgParent.task_parameterlist = pmcTemp;
				tskMsgParent.task = TM.TM_READ;
				devParent.AccessDevice(ref tskMsgParent);
				while (tskMsgParent.bgworker.IsBusy)
					System.Windows.Forms.Application.DoEvents();
				System.Windows.Forms.Application.DoEvents();
				if (bDEMAccess)
				{
					if (tskMsgParent.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
					{
						devParent.bBusy = false;
						return bRead;
					}
				}
				else
				{
					pamTemp.hexdata = 0x38;
				}
				//if (bDEMAccess == true) and if(bDEMAccess == false) + (errorcode == SUCCESSFUL)
				//go belows
				{
					tskMsgParent.task = TM.TM_CONVERT_HEXTOPHYSICAL;
					devParent.AccessDevice(ref tskMsgParent);
					while (tskMsgParent.bgworker.IsBusy)
						System.Windows.Forms.Application.DoEvents();
					if ((UInt16)pamTemp.phydata < 0xFF)
					{
						yData = Convert.ToByte(pamTemp.phydata);
						bRead = true;
					}
					else
					{
						bRead = false;
					}
				}
				if(bReadAll)
				{
					foreach (KeyValuePair<string, Reg> pareg in pamTemp.reglist)
					{
						pareg.Value.startbit = regTart.startbit;
						pareg.Value.bitsnumber = regTart.bitsnumber;
					}
				}
				devParent.bBusy = false;
			}

			return bRead;
		}

		private bool WriteChipsReg(byte yChip, ref Reg regTart, byte yData, bool bWriteAll = false)
		{
			bool bWrite = false;
			bool bFound = false;
			ParamContainer pmcTemp = new ParamContainer();
			Parameter pamTemp = null;
			//Reg regBackup = null;

			if ((devParent.bBusy))
			{
				devParent.msg.errorcode = LibErrorCode.IDS_ERR_EGDLL_BUS_BUSY;
				return bWrite;
			}
			if ((regTart == null))
			{
				devParent.msg.errorcode = LibErrorCode.IDS_ERR_EGDLL_REGISTER;
				return bWrite;
			}

			//foreach (Parameter pmWrite in pmcGGSetting.parameterlist)
			foreach (Parameter pmWrite in PListGGSetting)
			{
				if(pmWrite.subsection == (UInt16)yChip)
				{
					foreach (KeyValuePair<string, Reg> tmpreg in pmWrite.reglist)
					{
						if (tmpreg.Value.address == regTart.address)
						{
							if (bWriteAll )
							{
								regTart.bitsnumber = tmpreg.Value.bitsnumber;
								regTart.startbit = tmpreg.Value.startbit;
								tmpreg.Value.startbit = 0;
								tmpreg.Value.bitsnumber = 8;
								bFound = true;
								break;
							}
							else
							{
								if ((regTart.bitsnumber == tmpreg.Value.bitsnumber) &&
									(regTart.startbit == tmpreg.Value.startbit))
								{
									bFound = true;
									break;
								}
							}
						}
					}
				}
				if (bFound)
				{
					pmcTemp.parameterlist.Add(pmWrite);
					pamTemp = pmWrite;
					break;
				}
			}

			if (!bFound)
			{
				//TBD: error code, cannot find register in ParamContainer
				return bWrite;
			}
			else
			{
				devParent.bBusy = true;
				tskMsgParent.task_parameterlist = pmcTemp;
				pamTemp.phydata = yData;
				tskMsgParent.task = TM.TM_CONVERT_PHYSICALTOHEX;
				devParent.AccessDevice(ref tskMsgParent);
				while (tskMsgParent.bgworker.IsBusy)
					System.Windows.Forms.Application.DoEvents();
				System.Windows.Forms.Application.DoEvents();
				tskMsgParent.task = TM.TM_WRITE;
				devParent.AccessDevice(ref tskMsgParent);
				while (tskMsgParent.bgworker.IsBusy)
					System.Windows.Forms.Application.DoEvents();
				System.Windows.Forms.Application.DoEvents();
				if (bDEMAccess)
				{
					if (tskMsgParent.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
					{
						devParent.bBusy = false;
						return bWrite;
					}
					else
					{
						bWrite = true;
					}
				}
				else
				{
					//pamTemp.hexdata = 0x38;
				}
				if (bWriteAll)
				{
					foreach (KeyValuePair<string, Reg> pareg in pamTemp.reglist)
					{
						pareg.Value.startbit = regTart.startbit;
						pareg.Value.bitsnumber = regTart.bitsnumber;
					}
				}

				devParent.bBusy = false;
			}

			return bWrite;
		}

		private bool WriteChipsFloat(byte yChip, ref Reg regLo, ref Reg regHi, float fData)
		{
			bool bWrite = false;
			bool bFoundLo = false, bFoundHi = false;;
			ParamContainer pmcTemp = new ParamContainer();
			Parameter pamTemp = null;
			//Reg regBackup = null;
			float fBack = 1;

			//if ((devParent.bBusy) || (regLo == null) || (regHi == null))
			if ((regLo == null) || (regHi == null))
			{
				//TBD: Error Message for Device is busy
				return bWrite;
			}

			//foreach (Parameter pmWrite in pmcGGSetting.parameterlist)
			foreach (Parameter pmWrite in PListGGSetting)
			{
				if (pmWrite.subsection == (UInt16)yChip)
				{
					foreach (KeyValuePair<string, Reg> tmpreg in pmWrite.reglist)
					{
						if (tmpreg.Key.Equals("Low"))
						{
							if (tmpreg.Value.address == regLo.address)
							{
								if ((tmpreg.Value.startbit == regLo.startbit) &&
								(tmpreg.Value.bitsnumber == regLo.bitsnumber))	//address match
								{
									bFoundLo = true;
								}
							}
						}
						else if (tmpreg.Key.Equals("High"))
						{
							if (tmpreg.Value.address == regHi.address)		//address match
							{
								//if (bReadAll)
								//{
								//regBackup = new Reg();
								//regBackup.address = tmpreg.Value.address;
								if ((regHi.bitsnumber == tmpreg.Value.bitsnumber) &&
									(regHi.startbit == tmpreg.Value.startbit))
								{
									bFoundHi = true;
									break;
								}
							}
						}
					}
				}
				if ((bFoundLo) && (bFoundHi))
				{
					pmcTemp.parameterlist.Add(pmWrite);
					pamTemp = pmWrite;
					break;
				}
			}

			if ((!bFoundLo) || (!bFoundHi))
			{
				devParent.msg.errorcode = LibErrorCode.IDS_ERR_EGDLL_PARAMETERRW;
				return bWrite;
			}
			else
			{
				devParent.bBusy = true;
				tskMsgParent.task_parameterlist = pmcTemp;
				pamTemp.phydata = fData;
				tskMsgParent.task = TM.TM_CONVERT_PHYSICALTOHEX;
				devParent.AccessDevice(ref tskMsgParent);
				
				while (tskMsgParent.bgworker.IsBusy)
					System.Windows.Forms.Application.DoEvents();
				System.Windows.Forms.Application.DoEvents();
				tskMsgParent.task = TM.TM_WRITE;
				devParent.AccessDevice(ref tskMsgParent);
				while (tskMsgParent.bgworker.IsBusy)
					System.Windows.Forms.Application.DoEvents();
				System.Windows.Forms.Application.DoEvents();
				if (bDEMAccess)
				{
					if (tskMsgParent.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
					{
						devParent.bBusy = false;
						return bWrite;
					}
				}
				else
				{
					//pamTemp.hexdata = 0x38;
				}

				devParent.bBusy = false;
				//bWrite = true;
				ReadChipsShort(yOZ8806SubSec, ref regLo, ref regHi, ref fBack);
				fBack *= myProject.dbRsense;
				if (fData != 0)
				{
					fBack -= fData;
					if (fBack < 0) fBack *= -1;
					if (((fBack / fData) * 100) < 1)
					{
						bWrite = true;
					}
					else
					{
						//error code, cannot read back value is not same as write-to-value
					}
				}
				else
				{
					if (fBack == 0)
					{
						bWrite = true;
					}
					else
					{
						bWrite = false;
					}
				}
			}

			return bWrite;
		}

		#endregion

		#region main() simulation, actually this is timer function, run every 1 second

		private void tmrFirmware_Elapsed(object sender, EventArgs e)
		{
			bmutick++;

			sRam[(int)prShortIndex.iStateTmrSec] -= 1;

			sm_update_timers();

			sbd_udpate_sbs();

			short tmpState = (short)(sRam[(int)prShortIndex.iBGState] & (short)BGStateenum.BGSTATE_MASK);

			switch (tmpState)
			{
				case (short)BGStateenum.BGSTATE_IDLE:
					{
						sm_idle_process();
						break;
					}
				case (short)BGStateenum.BGSTATE_CHARGE:
					{
						sm_charge_process();
						break;
					}
				case (short)BGStateenum.BGSTATE_DISCHARGE:
					{
						sm_discharge_process();
						break;
					}
				case (short)BGStateenum.BGSTATE_SUSPEND:
					{
						break;
					}
				default:
					sRam[(int)prShortIndex.iBGState] = (short)BGStateenum.BGSTATE_IDLE;
					break;
			}

		}

		#endregion

//		#endregion
	}
}
