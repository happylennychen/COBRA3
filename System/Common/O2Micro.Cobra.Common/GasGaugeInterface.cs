using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace O2Micro.Cobra.Common
{
	#region physical value ADC data typ enum number

	public enum SBSFormat : ushort
	{
		TYPEINTEGER = 0x0001,
		TYPEHEX			= 0x0002,
		TYPEFLOAT		= 0x0004,
	}

	// Gas Gauge parameter type definition for XML <Element>. 
	// Due to parameter may be Gas Gauge algorithm polling parameter, and also be Readable/Writable 
	// normal parameter, so we use bit mask here and use WORD type just for case
	public enum GGType : ushort
	{
		OZVoltage = 0x0001,
		OZCurrent = 0x0002,
		OZExtTemp = 0x0004,
		OZCAR = 0x0008,
		//OZStatus = 0x0010,
		//OZCHGSet40 = 0x0010,		//charger polling register, Regx40
		//OZCHGSet41 = 0x0020,		//charger polling register, Regx41
		//OZCHGSet42 = 0x0040,		//charger polling register, Regx42
		OZRead = 0x0100,
		OZWrite = 0x0200,
        OZSetting = 0x1000,         //setting parameter, this is read by GetRegisteInfor()
		OZGGRegMask = 0xFF00,
		OZGGPollMask = 0x00FF
	}

	// SBS index value definition, according to SBS data spec and Eagle Frimware definition
	// But voltage channel may be up to 20 in OZ8966, we still have no plain to implement
	// This need to be modified when supporting OZ8966
	public enum EGSBS : byte
	{
		//static data
		SBSBatteryMode = 0x03,
		SBSDesignCapacit = 0x18,
		SBSDesignVoltage = 0x19,
		SBSSpecificaitonInfo = 0x1A,
		SBSManufactureDate = 0x1B,
		SBSSerailNumber = 0x1C,
		SBSManufactureName = 0x20,
		SBSDeviceName = 0x21,
		SBSDeviceChemistry = 0x22,
		SBSManufactureData = 0x23,
		//dynamic data
		SBSTotalVoltage = 0x09,
		SBSCurrent = 0x0a,
		SBSAvgCurrent = 0x0b,
		SBSRSOC = 0x0d,
		SBSASOC = 0x0e,
		SBSRC = 0x0f,
		SBSFCC = 0x10,
		SBSRunTimeToEmpty = 0x11,
		SBSAvgTimeToEmpty = 0x12,
		SBSAvgTimeToFull = 0x13,
		SBSChargingCurrent = 0x14,
		SBSChargingVoltage = 0x15,
		SBSBatteryStatus = 0x16,
		SBSCycleCount = 0x17,
		SBSSafetyStatus = 0x1f,
		//O2 defined data
		SBSVoltCell01 = 0x3c,
		SBSVoltCell02 = 0x3d,
		SBSVoltCell03 = 0x3e,
		SBSVoltCell04 = 0x3f,
		SBSVoltCell05 = 0x40,
		SBSVoltCell06 = 0x41,
		SBSVoltCell07 = 0x42,
		SBSVoltCell08 = 0x43,
		SBSVoltCell09 = 0x44,
		SBSVoltCell10 = 0x45,
		SBSVoltCell11 = 0x46,
		SBSVoltCell12 = 0x47,
		SBSVoltCell13 = 0x48,
		SBSIntTemp = 0x49,
		SBSExtTemp01 = 0x4a,
		SBSExtTemp02 = 0x4b,
		SBSExtTemp03 = 0x4c,
		SBSAgeFactor = 0x4d,
		//SBSCHGSet40 = 0x0010,		//charger polling register, Regx40
		//SBSCHGSet41 = 0x0020,		//charger polling register, Regx41
		//SBSCHGSet42 = 0x0040,		//charger polling register, Regx42
		SBSMCUChargerControl = 0x60,
		SBSMCUChagerBoardRunTime = 0x63,
		SBSMCUStatus = 0x65,
		SBSMCUChargerStatus = 0x67,
		SBSMCUVBusVoltage = 0x69,
		SBSMCUBatteryVoltage = 0x6B,
		SBSMCUBatteryCurrent = 0x6D,
		SBSMCUBatteryTemp = 0x6F,
		SBSMCUBatteryCapacity = 0x71,
		SBSMCUBatterySoC = 0x72,
	}

	#endregion

	#region GasGauge interface/class definition

	public interface GasGaugeInterface
	{
		//string strSFLName;
		//byte	yDBGCode {get; set;}
		//bool InitializeGG(object deviceP, TASKMessage taskP, ParamContainer polling, ParamContainer setting, ParamContainer sbsreg, List<string> projtable = null);
		bool InitializeGG(object deviceP, TASKMessage taskP,
										AsyncObservableCollection<Parameter> PPolling,
										AsyncObservableCollection<Parameter> PSetting,
										AsyncObservableCollection<Parameter> PSBSreg,
										List<string> projtable = null);
		bool UnloadGG();
		//bool AccessSBSParam(ref UInt16 uOut, int iIndex);
		//bool AccessSBSParam(ref byte yOut, int iIndex);
		//bool AccessSBSParam(ref float fOut, int iIndex);
		//bool AccessSBSParam(ref float fOut, byte yIndex);
		UInt32 GetStatus();
        GasGaugeProject GetProjectFile();

		bool CalculateGasGauge();
	}

	//public struct GasGaugeData
	//{
		//Parameter pmDEMTarget;
		//GGType wType;
		//EGSBS ySbs;
	//}

	#endregion

}
