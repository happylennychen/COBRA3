using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Data;
using System.Xml.Linq;

namespace Cobra.Common
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

	public enum SettingFlag : ushort
	{
		FileNum = 6,        //(M151002)Francis, modify to 6, casue we can ignore ProjectSetting.xml
		FileSetting = 0x0100,
		FDLL = 0x0101,
		FOCVTSOC = 0x0102,
		FTSOCOCV = 0x0103,
		FRC = 0x0104,
		FTHERM = 0x0105,
		FSELFDSG = 0x0106,
		FRITable = 0x0107,
		FCHGTable = 0x108,
		ParamNum = 9,
		ParamSetting = 0x0200,
		PDC = 0x0201,
		PRS = 0x0202,
		PXTPR = 0x0203,
		PXTPV = 0x0204,
		PCHGCV = 0x0205,
		PCHGCUR = 0x0206,
		PDSGV = 0x0207,
		PCellNum = 0x0208,
		PRBAT = 0x209,
		PRCON = 0x20a
	}

	public class GasGaugeProject
	{
		private struct GG2WayTable
		{
			public float xAxis;
			public float yAxis;
			public GG2WayTable(float i1, float i2)
			{
				xAxis = i1;
				yAxis = i2;
			}
		}

		private List<string> ProjectTableList = null;

		private List<GG2WayTable> OCVbyTSOCTable = null;
		private List<GG2WayTable> TSOCbyOCVTable = null;
		private List<GG2WayTable> ThermalTable = null;
		private List<GG2WayTable> SelfDsgTable = null;
		private List<GG2WayTable> RITable = null;
		private List<GG2WayTable> ChgTable = null;
		private List<float> RCXaxis = null;
		private List<float> RCWaxis = null;
		private List<float> RCVaxis = null;
		private List<float> RCYaxis = null;

		public string strFileProject { get; set; }
		public string strFileGGDll { get; set; }
		public string strOCVbyTSOCTable { get; set; }
		public string strTSOCbyOCVTable { get; set; }
		public string strThermTable { get; set; }
		public string strRCTable { get; set; }
		public string strSelfDsgTable { get; set; }
		public string strRITable { get; set; }
		public string strChgTable { get; set; }
		public float dbDesignCp { get; set; }
		public float dbRsense { get; set; }
		public float dbPullupR { get; set; }
		public float dbPullupV { get; set; }
		public float dbChgCVVolt { get; set; }
		public float dbChgEndCurr { get; set; }
		public float dbDsgEndVolt { get; set; }
		public float dbRbat { get; set; }
		public float dbRcon { get; set; }
		public int iCellNum { get; set; }

		public GasGaugeProject(List<string> projtable)
		{
			ProjectTableList = projtable;
			strFileProject = null;
			strFileGGDll = null;
			strOCVbyTSOCTable = null;
			strTSOCbyOCVTable = null;
			strThermTable = null;
			strRCTable = null;
			strSelfDsgTable = null;
			dbDesignCp = 2200;
			dbRsense = 0.002F;
			dbPullupR = 230000;
			dbPullupV = 1800;
			dbChgCVVolt = 4100;
			dbChgEndCurr = 100;
			dbDsgEndVolt = 3500;
			iCellNum = 1;
		}

		public GasGaugeProject()
		{
			strFileProject = null;
			strFileGGDll = null;
			strOCVbyTSOCTable = null;
			strTSOCbyOCVTable = null;
			strThermTable = null;
			strRCTable = null;
			strSelfDsgTable = null;
			dbDesignCp = 0;
			dbRsense = 0.002F;
			dbPullupR = 230000;
			dbPullupV = 1800;
			dbChgCVVolt = 4100;
			dbChgEndCurr = 0;
			dbDsgEndVolt = 0;
			iCellNum = 1;
		}

		public void SetTableList(List<string> projtable)
		{
			ProjectTableList = projtable;
		}

		public bool InitializeProject(ref UInt32 refError, bool bForceChgTable = false, bool bNoCheckProject = false)
		{
			bool bInit = false;
			ushort uflag = 0x0000;

			//string strfilechk = null;
			refError = LibErrorCode.IDS_ERR_SUCCESSFUL;

			//(M140404)Francis, according to 0403 meeting, file path string will be passed from SFL,
			//instead of saving project file
			//if ((ProjectTableList == null) || (ProjectTableList.Count-1 != (ushort)SettingFlag.FileNum))
			//(M140514)Fracnis, according to Guo 0514 code, file path string excludes DLL file path
			//if ((ProjectTableList == null) || (ProjectTableList.Count != (ushort)SettingFlag.FileNum))
			//(M150721)Francis, adding a ChargeTable for Android Driver, for compatibility,  use "< FileNum (7)" to check file number
			if ((ProjectTableList == null) || (ProjectTableList.Count < (ushort)SettingFlag.FileNum))
			{
				refError = LibErrorCode.IDS_ERR_EGDLL_TABLE_NUMBER;
				return bInit;
			}
			else
			{
				for (int i = 0; i < ProjectTableList.Count; i++)
				{
					//if (i != 1)	//dll file path
					{
						//if bForceChg = false, no charge table need and i=7 is going to check charge table, break for loop
						if ((i == 7) && (!bForceChgTable))
						{
							break;
						}
						//(M151002)Francis, it's OK if projectsetting.xml not existed
						if ((i == 0) && (bNoCheckProject))
						{
							continue;
						}

						if (!File.Exists(ProjectTableList[i]))
						{
							if (i == 0)
							{
								refError = LibErrorCode.IDS_ERR_EGDLL_PROJECT_FILE_NOEXIST;
							}
							else if (i == 1)
							{
								refError = LibErrorCode.IDS_ERR_EGDLL_OCVBYTSOC_NOEXIST;
							}
							else if (i == 2)
							{
								refError = LibErrorCode.IDS_ERR_EGDLL_TSOCBYOCV_NOEXIST;
							}
							else if (i == 3)
							{
								refError = LibErrorCode.IDS_ERR_EGDLL_RC_NOEXIST;
							}
							else if (i == 4)
							{
								refError = LibErrorCode.IDS_ERR_EGDLL_THERMAL_NOEXIST;
							}
							else if (i == 5)
							{
								refError = LibErrorCode.IDS_ERR_EGDLL_SELFDSG_NOEXIST;
							}
							else if (i == 6)
							{
								refError = LibErrorCode.IDS_ERR_EGDLL_RI_NOEXIST;
							}
							else if (i == 7)
							{
								refError = LibErrorCode.IDS_ERR_EGDLL_CHGTABLE_NOEXIST;
							}
							return bInit;
						}
					}
				}
				if (bNoCheckProject)
				{
					strFileProject = String.Empty;
				}
				else
				{
					strFileProject = ProjectTableList[0];
				}
				//(M140514)Fracnis, according to Guo 0514 code, file path string excludes DLL file path
				//strOCVbyTSOCTable = ProjectTableList[2];
				//strTSOCbyOCVTable = ProjectTableList[3];
				//strRCTable = ProjectTableList[4];
				//strThermTable = ProjectTableList[5];
				//strSelfDsgTable = ProjectTableList[6];
				//strRITable = ProjectTableList[7];
				strOCVbyTSOCTable = ProjectTableList[1];
				strTSOCbyOCVTable = ProjectTableList[2];
				strRCTable = ProjectTableList[3];
				strThermTable = ProjectTableList[4];
				strSelfDsgTable = ProjectTableList[5];
				strRITable = ProjectTableList[6];
				if (ProjectTableList.Count > 7)
				{
					if (File.Exists(ProjectTableList[7]))
					{
						strChgTable = ProjectTableList[7];          //(A150714)Francis, charge table file path
					}
				}
				//(E140514)
			}

			//(E140404)

			if ((strFileProject != null) && (strFileProject != String.Empty))
			{
				XElement rootNode = XElement.Load(strFileProject);
				IEnumerable<XElement> xel = from target in rootNode.Elements("Section") select target;
				foreach (XElement node in xel)
				{
					if (node.Attribute("Flag") != null)
						uflag = Convert.ToUInt16(node.Attribute("Flag").Value, 16);
					if (uflag == (ushort)SettingFlag.FileSetting)
					{
						bInit = ParseFileStringFromProjectXML(node, ref refError);
						if (!bInit)
						{
							//error code will be defined in ParseFileStringFromProjectXML();
							return bInit;
						}
						//float dbResult;
						//dbResult = LutOCVbyTSOC(10000);
						//dbResult = LutTSOCbyOCV(2900);
						//dbResult = LutThermalTemp(12053);
						//dbResult = LutThermalResistor(430);
						//dbResult = LutSelfDsg(250);
						//dbResult = LutRCTable(3000, 4000, 230);
						//dbResult = LutRiFromTemp(250);
						//bInit = true;
					}
					else if (uflag == (ushort)SettingFlag.ParamSetting)
					{
						bInit = ParseParamValueFromProjectXML(node, ref refError);
						if (!bInit)
						{
							//error code should be set in ParseParamValueFromProjectXML();
							return bInit;
						}
						//bInit = true;
						bInit = OpenTableFiles(ref refError);       //(M140407)Francis, read table accoriding 04/04 meeting conclusion
																	//float dbResult;
																	//dbResult = LutOCVbyTSOC(10000);
																	//dbResult = LutTSOCbyOCV(2900);
																	//dbResult = LutThermalTemp(12053);
																	//dbResult = LutThermalResistor(430);
																	//dbResult = LutSelfDsg(250);
																	//dbResult = LutRCTable(3000, 4000, 230);
																	//dbResult = LutRiFromTemp(250);
					}
					else
					{
						refError = LibErrorCode.IDS_ERR_EGDLL_PROJSET_WRONG_FLAG;
					}
				}
			}
			else
			{   //if (strFileProject != null)
				//those setting value must be filled in SBS4 SFL
				if (bNoCheckProject)
				{
					bInit = OpenTableFiles(ref refError);       //(M140407)Francis, read table accoriding 04/04 meeting conclusion
				}
			}

			return bInit;
		}

		public float LutThermalDK(float fInVolt)
		{
			if ((fInVolt < 0) || (fInVolt > 0x7D8))
			{
				return -999999;
			}

			float fTherR = (fInVolt * dbPullupR) / (dbPullupV - fInVolt);
			return (2730 + LutThermalTemp(fTherR));
		}

		//(A150525)Francis, create a static function for SBS3Panel using
		public static bool CheckTableContent(List<string> tables)
		{
			return false;
		}
		//(E150525)

		private float LutThermalResistor(float inTemp)
		{
			return LutOneWayYtoXLinear(ThermalTable, inTemp);   //return in Ohm format
		}

		private float LutThermalTemp(float inResis)
		{
			return LutOneWayXtoYLinear(ThermalTable, inResis);  //return in 0.1'C format
		}

		public float LutSelfDsg(float inTemp)
		{
			float dbRes = LutOneWayXtoYLinear(SelfDsgTable, inTemp);
			dbRes /= 10000;     //convert to xx%

			return dbRes;
		}

		public float LutOCVbyTSOC(float soc)
		{
			float dbRes = 3000;
			byte nn, mm;
			Int16 part;
			int n = OCVbyTSOCTable.Count; //data size of OCVbyTSOC table

			if (soc == 0)
			{
				dbRes = OCVbyTSOCTable[0].yAxis; //return the 1st table value
				return dbRes;
			}
			if (soc >= 32767)
			{
				dbRes = OCVbyTSOCTable[n - 1].yAxis;  //get the last value in the table
				return dbRes;
			}
			soc = (float)((UInt16)soc >> 1);
			nn = (byte)((UInt16)soc >> 8);  //nn = soc / 256;
			mm = (byte)soc;             //mm = soc % 256;
			if (nn < (byte)(n - 1))
			{
				part = (Int16)OCVbyTSOCTable[(nn + 1)].yAxis;//+1:one next item
				part -= (Int16)OCVbyTSOCTable[(nn)].yAxis;  //minus current item
				if ((part & 0xff00) != 0)   //in case part>255
					part = 255;
				part *= mm;
				part = (Int16)(part >> 8);      //part / 256
				dbRes = OCVbyTSOCTable[(nn)].yAxis + part;//current + part
			}
			else
				dbRes = (Int16)OCVbyTSOCTable[(nn - 1)].yAxis;  //get the last value in the table
			return dbRes;   //return in mV format
		}

		public float LutCapFromOCVTable(float ocv)
		{
			float fPer = LutTSOCbyOCV(ocv);

			fPer /= 32768F;
			fPer *= dbDesignCp;

			return fPer;
		}

		public float LutTSOCbyOCV(float ocv)
		{
			float dbRes = 0;
			UInt16 nn, mm;
			float part, b;
			float ocv_min, ocv_max;

			ocv_min = TSOCbyOCVTable[0].xAxis;
			ocv_max = TSOCbyOCVTable[TSOCbyOCVTable.Count - 1].xAxis;

			if (TSOCbyOCVTable.Count >= 80)
			{
				if (ocv > ocv_max)  //bigger than maximum voltage
					return 99;
				ocv = ocv - ocv_min;    // subtrace minimum
				if (ocv < 0)
					return 0; // < minimum voltage
				nn = (UInt16)(((UInt16)ocv) >> 4);      //nn = ocv / 16;
				mm = (UInt16)(((UInt16)ocv) & 0x000F);  //mm = ocv % 16;

				b = TSOCbyOCVTable[(nn + 1)].yAxis; //next one x
				b -= (TSOCbyOCVTable[(nn)].yAxis);  //substract current x
				part = b * mm;
				part = (float)((UInt16)part >> 4);          //part = part / 16;
				dbRes = (TSOCbyOCVTable[(nn)].yAxis) + (Int16)part;
				dbRes /= 32768;
				dbRes *= 100;       //convert to xx% 
			}
			else
			{
				dbRes = TSOCbyOCVTable[TSOCbyOCVTable.Count - 1].yAxis;
				for (int ii = 0; ii < TSOCbyOCVTable.Count; ii++)
				{
					if (TSOCbyOCVTable[ii].xAxis == ocv)
					{
						dbRes = TSOCbyOCVTable[(ii)].yAxis;
						break;
					}
					else if (TSOCbyOCVTable[ii].xAxis > ocv)
					{
						if (ii == 0)
						{
							dbRes = TSOCbyOCVTable[(ii)].yAxis;
						}
						else
						{
							float fwe = (ocv - TSOCbyOCVTable[ii - 1].xAxis)
								/ (TSOCbyOCVTable[ii].xAxis - TSOCbyOCVTable[ii - 1].xAxis);
							fwe *= (TSOCbyOCVTable[ii].yAxis - TSOCbyOCVTable[ii - 1].yAxis);
							dbRes = fwe + TSOCbyOCVTable[ii].yAxis;
						}
						break;
					}
				}
				dbRes /= 32768;
				dbRes *= 100;       //convert to xx% 
			}

			return dbRes;
		}

		public float LutRiFromTemp(float inTemp)
		{
			float dbRes = LutOneWayXtoYLinear(RITable, (inTemp / 10));
			//dbRes /= 10000;		//convert to xx%

			return dbRes;   //return in % value
		}

		public float LutChargeTable(float fvolt)
		{
			return LutOneWayXtoYLinear(ChgTable, fvolt);    //return in 0.1'C format
		}

		public float GetRCTableHighVolt()
		{
			return RCXaxis[RCXaxis.Count - 1];
		}

		public float GetRCTableLowVolt()
		{
			return RCXaxis[0];
		}

		public short GetRCCrateFactor()
		{
			short sDefa = -100;

			foreach (float fw in RCWaxis)
			{
				if (fw >= 10000)
				{
					sDefa = -10000;
					break;
				}
			}

			return sDefa;
		}

		//(A150714)Francis, to get number of point for each Axis in RC table
		public int GetOCVPointsNo()
		{
			if (OCVbyTSOCTable != null)
				return OCVbyTSOCTable.Count;
			else
				return 0;
		}

		public int GetXAxisLengthofRCTable()
		{
			if (RCXaxis != null)
				return RCXaxis.Count;
			else
				return 0;
		}

		public int GetWAxisLengthofRCTable()
		{
			if (RCWaxis != null)
				return RCWaxis.Count;
			else
				return 0;
		}

		public int GetVAxisLengthofRCTable()
		{
			if (RCVaxis != null)
				return RCVaxis.Count;
			else
				return 0;
		}

		public int GetChargePointsNo()
		{
			if (ChgTable != null)
				return ChgTable.Count;
			else
				return 0;
		}

		public int GetThermalPointsNo()
		{
			if (ThermalTable != null)
				return ThermalTable.Count;
			else
				return 0;
		}

		public float GetRCTableHighCurr()
		{
			return (RCWaxis[RCWaxis.Count - 1] * dbDesignCp / 10000);
		}

		public float GetRCTableLowCurr()
		{
			return (RCWaxis[0] * dbDesignCp / 10000);
		}

		public float GetRCTableHighCurrCRate()
		{
			return RCWaxis[RCWaxis.Count - 1];
		}

		public float GetRCTableLowCurrCRate()
		{
			return RCWaxis[0];
		}

		public float GetTSOCbyOCVLowVolt()
		{
			float fzero = -1, ftop = -1;

			if (TSOCbyOCVTable != null)
			{
				fzero = TSOCbyOCVTable[0].xAxis;
				ftop = TSOCbyOCVTable[TSOCbyOCVTable.Count - 1].xAxis;
				if (ftop < fzero)
					fzero = ftop;
			}
			return fzero;
		}

		public float GetTSOCbyOCVHighVolt()
		{
			float fzero = -1, ftop = -1;

			if (TSOCbyOCVTable != null)
			{
				fzero = TSOCbyOCVTable[0].xAxis;
				ftop = TSOCbyOCVTable[TSOCbyOCVTable.Count - 1].xAxis;
				if (ftop < fzero)
					ftop = fzero;
			}
			return ftop;
		}

		public float GetChargeLowVolt()
		{
			float fzero = -1, ftop = -1;

			if (ChgTable != null)
			{
				fzero = ChgTable[0].xAxis;
				ftop = ChgTable[ChgTable.Count - 1].xAxis;
				if (ftop < fzero)
					fzero = ftop;
			}
			return fzero;
		}

		public float GetChargeHighVolt()
		{
			float fzero = -1, ftop = -1;

			if (ChgTable != null)
			{
				fzero = ChgTable[0].xAxis;
				ftop = ChgTable[ChgTable.Count - 1].xAxis;
				if (ftop < fzero)
					ftop = fzero;
			}
			return ftop;
		}


		//(E150714)

		//dbVolt is in mV format
		//dbCurr is in C rate format
		//dbTemp is in 10'C format
		public float LutRCTable(float dbVolt, float dbCurr, float dbTemp, bool bConvertmAhr = true)
		{
			int iT1 = 0, iT2 = 0, iC1 = 0, iC2 = 0, iV1 = 0, iV2 = 0;
			int i, j;
			float fracV = 1.0F, fracC = 1.0F, fracT = 1.0F;
			float dbNum1 = 0, dbNum2 = 0, dbNum3 = 0, dbNum4 = 0;
			float dbRes = -1F;

			//(A150730)Francis, prevent Voltage is not in RC table
			if (dbVolt > RCXaxis[RCXaxis.Count - 1])
			{
				if (bConvertmAhr)
				{
					dbRes = dbDesignCp; //convert to mAhr
				}
				else
				{
					dbRes = 10000;
				}
				return dbRes;
			}
			//(E150730)
			#region find where interval dbTemp is located
			for (i = 0; i < RCVaxis.Count; i++)
			{
				if (RCVaxis[i] > dbTemp)
				{
					if (i == 0)
					{
						iT1 = 0;
						iT2 = iT1;
					}
					else
					{
						iT1 = i - 1;
						iT2 = i;
					}
					break;
				}
				else if (RCVaxis[i] == dbTemp)
				{
					iT1 = i;
					iT2 = iT1;
					break;
				}
			}
			if (i >= RCVaxis.Count)     //input temperature is bigger than maximum RC temperature 
			{
				iT1 = RCVaxis.Count - 1;
				iT2 = iT1;
			}
			#endregion
			#region find where interval dbCurr is located
			for (i = 0; i < RCWaxis.Count; i++)
			{
				if (RCWaxis[i] > dbCurr)
				{
					if (i == 0)
					{
						iC1 = 0;
						iC2 = iC1;
					}
					else
					{
						iC1 = i - 1;
						iC2 = i;
					}
					break;
				}
				else if (RCWaxis[i] == dbCurr)
				{
					iC1 = i;
					iC2 = iC1;
					break;
				}
			}
			if (i >= RCWaxis.Count)             //input current is bigger than maximum RC current 
			{
				iC1 = RCWaxis.Count - 1;
				iC2 = iC1;
			}
			#endregion
			#region find where intervalu dbVolt is located
			for (i = 0; i < RCXaxis.Count; i++)
			{
				if (RCXaxis[i] > dbVolt)
				{
					if (i == 0)
					{
						iV1 = 0;
						iV2 = iV1;
					}
					else
					{
						iV1 = i - 1;
						iV2 = i;
					}
					break;
				}
				else if (RCXaxis[i] == dbVolt)
				{
					iV1 = i;
					iV2 = iV1;
					break;
				}
			}
			if (i >= RCXaxis.Count)
			{
				iV1 = RCXaxis.Count - 1;
				iV2 = iV1;
			}
			#endregion

			i = (iT1 * RCXaxis.Count * RCWaxis.Count) + (iC1 * RCXaxis.Count) + iV1;
			j = (iT1 * RCXaxis.Count * RCWaxis.Count) + (iC1 * RCXaxis.Count) + iV2;
			if ((RCXaxis[iV2] - RCXaxis[iV1]) != 0)
			{
				fracV = (float)(dbVolt - RCXaxis[iV1]) / (float)(RCXaxis[iV2] - RCXaxis[iV1]);
			}
			else
			{
				fracV = 0;
			}
			dbNum1 = (RCYaxis[j] - RCYaxis[i]) * fracV + RCYaxis[i];
			i = (iT1 * RCXaxis.Count * RCWaxis.Count) + (iC2 * RCXaxis.Count) + iV1;
			j = (iT1 * RCXaxis.Count * RCWaxis.Count) + (iC2 * RCXaxis.Count) + iV2;
			//fracV = (float)(dbVolt - RCXaxis[iV1]) / (float)(RCXaxis[iV2] - RCXaxis[iV1]);
			dbNum2 = (RCYaxis[j] - RCYaxis[i]) * fracV + RCYaxis[i];
			i = (iT2 * RCXaxis.Count * RCWaxis.Count) + (iC1 * RCXaxis.Count) + iV1;
			j = (iT2 * RCXaxis.Count * RCWaxis.Count) + (iC1 * RCXaxis.Count) + iV2;
			//fracV = (float)(dbVolt - RCXaxis[iV1]) / (float)(RCXaxis[iV2] - RCXaxis[iV1]);
			dbNum3 = (RCYaxis[j] - RCYaxis[i]) * fracV + RCYaxis[i];
			i = (iT2 * RCXaxis.Count * RCWaxis.Count) + (iC2 * RCXaxis.Count) + iV1;
			j = (iT2 * RCXaxis.Count * RCWaxis.Count) + (iC2 * RCXaxis.Count) + iV2;
			//fracV = (float)(dbVolt - RCXaxis[iV1]) / (float)(RCXaxis[iV2] - RCXaxis[iV1]);
			dbNum4 = (RCYaxis[j] - RCYaxis[i]) * fracV + RCYaxis[i];

			if ((RCWaxis[iC2] - RCWaxis[iC1]) != 0)
			{
				fracC = (float)(dbCurr - RCWaxis[iC1]) / (float)(RCWaxis[iC2] - RCWaxis[iC1]);
			}
			else
			{
				fracC = 0;
			}
			dbNum1 = (dbNum2 - dbNum1) * fracC + dbNum1;
			dbNum3 = (dbNum4 - dbNum3) * fracC + dbNum3;

			if ((RCVaxis[iT2] - RCVaxis[iT1]) != 0)
			{
				fracT = (float)(dbTemp - RCVaxis[iT1]) / (float)(RCVaxis[iT2] - RCVaxis[iT1]);
			}
			else
			{
				fracT = 0;
			}
			dbRes = (dbNum3 - dbNum1) * fracT + dbNum1;
			if (bConvertmAhr)
				dbRes *= (dbDesignCp / 10000);  //convert to mAhr

			return dbRes;
		}

		//(A150907)Francis, lookup current by input of volt, rsoc, temp
		public float LutRCTableCurrent(float dbVolt, float dbRSOC, float dbTemp)
		{
			int i, j, indexX, indexY, indexZ;   //volt,curr,temp
			float cvX0L, cvX0R, cvY0L, cvY0R, cvX1L, cvX1R, cvY1L, cvY1R;
			float[] idY = new float[4];
			float[] fcv = new float[4];
			float fintrX, fintrZ, fintrY, fswap;
			float ftemp1 = 0, ftemp2 = 0;
			float fl0, fr0, fl1, fr1;
			float fy0, fy1;
			float dbRes = -1F;

			idY[0] = -1F;
			idY[1] = -1F;
			idY[2] = -1F;
			idY[3] = -1F;

			//(A150730)Francis, prevent Voltage is not in RC table
			if ((dbRSOC >= 10000) || (dbVolt > RCXaxis[RCXaxis.Count - 1]))
			{
				dbRes = GetRCTableLowCurr();
				return dbRes;
			}
			if (dbVolt < RCXaxis[0])
			{
				return dbRes;
			}
			#region find where intervalu dbVolt is located
			for (indexX = 1; indexX < RCXaxis.Count; indexX++)
			{
				if ((RCXaxis[indexX - 1] <= dbVolt) && (RCXaxis[indexX] > dbVolt))
					break;
			}
			if (indexX >= RCXaxis.Count) indexX = RCXaxis.Count - 1;
			#endregion
			#region find where interval dbTemp is located
			for (indexZ = 1; indexZ < RCVaxis.Count; indexZ++)
			{
				if ((RCVaxis[indexZ - 1] <= dbTemp) && (RCVaxis[indexZ] > dbTemp))
					break;
			}
			if (indexZ >= RCVaxis.Count)
			{
				indexZ = RCVaxis.Count - 1;
				//Check boundary for zval
				if (dbTemp < RCVaxis[0])
					indexZ = 1;
			}
			#endregion
			for (indexY = 0; indexY < RCWaxis.Count; indexY++)
			{
				cvX0L = RCYaxis[((indexY) + (indexZ - 1) * RCWaxis.Count) * RCXaxis.Count + (indexX - 1)];
				cvY0L = RCYaxis[((indexY + 1) + (indexZ - 1) * RCWaxis.Count) * RCXaxis.Count + (indexX - 1)];
				cvX0R = RCYaxis[((indexY) + (indexZ - 1) * RCWaxis.Count) * RCXaxis.Count + (indexX)];
				cvY0R = RCYaxis[((indexY + 1) + (indexZ - 1) * RCWaxis.Count) * RCXaxis.Count + (indexX)];
				cvX1L = RCYaxis[((indexY) + (indexZ) * RCWaxis.Count) * RCXaxis.Count + (indexX - 1)];
				cvY1L = RCYaxis[((indexY + 1) + (indexZ) * RCWaxis.Count) * RCXaxis.Count + (indexX - 1)];
				cvX1R = RCYaxis[((indexY) + (indexZ) * RCWaxis.Count) * RCXaxis.Count + (indexX)];
				cvY1R = RCYaxis[((indexY + 1) + (indexZ) * RCWaxis.Count) * RCXaxis.Count + (indexX)];
				if ((cvX0L <= dbRSOC) && (cvY0L >= dbRSOC))
				{
					if (idY[0] < 0)
						idY[0] = indexY;
				}
				if ((cvX0R <= dbRSOC) && (cvY0R >= dbRSOC))
				{
					if (idY[1] < 0)
						idY[1] = indexY;
				}
				if ((cvX1L <= dbRSOC) && (cvY1L >= dbRSOC))
				{
					if (idY[2] < 0)
						idY[2] = indexY;
				}
				if ((cvX1R <= dbRSOC) && (cvY1R >= dbRSOC))
				{
					if (idY[3] < 0)
						idY[3] = indexY;
				}
			}
			for (i = 0; i < 4; i++)
			{
				if (idY[i] < 0)
				{
					if ((i & 1) != 0)
					{
						if (idY[i - 1] > 0)
							idY[i] = idY[i - 1];
						else if ((i < 3) && (idY[i + 1] > 0))
							idY[i] = idY[i + 1];
						else
							idY[i] = 0;
					}
					else
					{
						if (idY[i + 1] > 0)
							idY[i] = idY[i + 1];
						else if ((i > 0) && (idY[i - 1] > 0))
							idY[i] = idY[i - 1];
						else
							idY[i] = 0;
					}
				}
			}
			fintrX = ((float)(dbVolt - RCXaxis[indexX - 1]) /
				(float)(RCXaxis[indexX] - RCXaxis[indexX - 1]));

			fintrZ = ((float)(dbTemp - RCVaxis[indexZ - 1]) /
				(float)(RCVaxis[indexZ] - RCVaxis[indexZ - 1]));

			for (i = 0; i < 4; i++)
			{
				for (j = 1; j < (4 - i); j++)
				{
					if (idY[j] < idY[j - 1])
					{
						//iSwap(&idY[j], &idY[j - 1]);
						fswap = idY[j];
						fswap = idY[j - 1];
						idY[j - 1] = fswap;
					}
				}
			}
			for (i = 0; i < 4; i++)
			{
				fl0 = RCYaxis[(int)(idY[i] + (indexZ - 1) * RCWaxis.Count) * RCXaxis.Count + (indexX - 1)];
				fr0 = RCYaxis[(int)(idY[i] + (indexZ - 1) * RCWaxis.Count) * RCXaxis.Count + (indexX)];
				fl1 = RCYaxis[(int)(idY[i] + (indexZ) * RCWaxis.Count) * RCXaxis.Count + (indexX - 1)];
				fr1 = RCYaxis[(int)(idY[i] + (indexZ) * RCWaxis.Count) * RCXaxis.Count + (indexX)];

				fy0 = fl0 + fintrX * (fr0 - fl0);
				fy1 = fl1 + fintrX * (fr1 - fl1);

				fcv[i] = fy0 + fintrZ * (fy1 - fy0);
			}
			if (dbRSOC < fcv[0])
			{
				dbRes = RCWaxis[(int)idY[0]];
				return dbRes;
			}
			else
			{
				if (dbRSOC >= fcv[3])
				{
					dbRes = RCWaxis[(int)idY[3]];
					return dbRes;
				}
			}

			for (i = 1; i < 4; i++)
			{
				if ((dbRSOC >= fcv[i - 1]) && (dbRSOC < fcv[i]))
				{
					fintrY = (dbRSOC - fcv[i - 1]) / (fcv[i] - fcv[i - 1]);
					ftemp1 = RCWaxis[(int)idY[i - 1]] + fintrY * (RCWaxis[(int)idY[i]] - RCWaxis[(int)idY[i - 1]]);

					if ((ftemp2 == 0) || (ftemp1 < ftemp2))
						ftemp2 = ftemp1;
				}
			}
			dbRes = ftemp2;

			return dbRes;
		}

		private float LutOneWayXtoYLinear(List<GG2WayTable> searchTable, float x1)
		{
			float dbFound = 0;
			float y1 = 0, y2 = 0;
			float dbValue = 1.0F;
			int i = 0;

			for (i = 0; i < searchTable.Count; i++)
			{
				// x axii value of content is supposing to start from the smallest value to the biggest value
				if (searchTable[i].xAxis > x1)  //samller than fisrt one, use expolation
				{
					if (i == 0)
					{
						y1 = searchTable[i].yAxis;
						y2 = y1;
						dbValue = 1.0F;
						break;
					}
					else
					{
						y1 = searchTable[i - 1].yAxis;
						y2 = searchTable[i].yAxis;
						dbValue = (float)(x1 - searchTable[i - 1].xAxis) / (float)(searchTable[i].xAxis - searchTable[i - 1].xAxis);
						break;
					}
				}
				else if (searchTable[i].xAxis == x1)
				{
					//dbFound = searchTable[i].yAxis;
					y1 = searchTable[i].yAxis;
					y2 = y1;
					dbValue = 1.0F;
					break;
				}
			}
			if (i >= searchTable.Count)
			{
				//x1 is biigger than the last one xAxis
				y1 = searchTable[searchTable.Count - 1].yAxis;
				y2 = y1;
				dbValue = 1.0F;
			}
			dbFound = (y2 - y1) * dbValue + y1;

			return dbFound;
		}

		private float LutOneWayYtoXLinear(List<GG2WayTable> searchTable, float y1)
		{
			float dbFound = 0;
			float x1 = 0, x2 = 0;
			float dbValue = 1.0F;
			int i = 0;

			searchTable.Sort((x, y) => { return x.yAxis.CompareTo(y.yAxis); });
			for (i = 0; i < searchTable.Count; i++)
			{
				// x axii value of content is supposing to start from the smallest value to the biggest value
				if (searchTable[i].yAxis > y1)  //samller than fisrt one, use expolation
				{
					if (i == 0)
					{
						x1 = searchTable[i].xAxis;
						x2 = x1;
						dbValue = 1.0F;
						break;
					}
					else
					{
						x1 = searchTable[i - 1].xAxis;
						x2 = searchTable[i].xAxis;
						dbValue = (float)(y1 - searchTable[i - 1].yAxis) / (float)(searchTable[i].yAxis - searchTable[i - 1].yAxis);
						break;
					}
				}
				else if (searchTable[i].yAxis == y1)
				{
					//dbFound = searchTable[i].yAxis;
					x1 = searchTable[i].xAxis;
					x2 = x1;
					dbValue = 1.0F;
					break;
				}
			}
			if (i >= searchTable.Count)
			{
				//y1 is biigger than the last one xAxis
				x1 = searchTable[searchTable.Count - 1].xAxis;
				x2 = x1;
				dbValue = 1.0F;
			}
			dbFound = (x2 - x1) * dbValue + x1;
			searchTable.Sort((x, y) => { return x.xAxis.CompareTo(y.xAxis); });

			return dbFound;
		}

		private bool ReadTwoAxisTableContent(ref List<GG2WayTable> targetTable, string strFilePath, SettingFlag tabletype)
		{
			bool bTable = false;
			//FileStream tablefs = null;
			StreamReader stmContent = null;
			string strTemp = null, strNum = null;
			bool bStartswith = false, bFoundX = false, bFoundY = false;
			char[] ch = null;
			int iTmpHead = 0, iRead = 0;
			int iTableHeader = 0, iTableCotrol = 0, iNumAxii = 0, iNumXPoint = 0, iNumYPoint = 0, iNumFAC = 0;
			string[] strArrayX = null, strArrayY = null;

			if (targetTable != null)
			{
				targetTable.Clear();
			}
			targetTable = new List<GG2WayTable>();

			if (!File.Exists(strFilePath))
			{
				//refError = LibErrorCode.IDS_ERR_EGDLL_PROJSET_VALUE;
				return bTable;
			}

			#region read file, and save content into strArrayX, strArrayY, and all header value
			stmContent = new StreamReader(strFilePath);
			iTmpHead = 0;
			while (!stmContent.EndOfStream)
			{
				strTemp = stmContent.ReadLine();
				strTemp.TrimStart(' ');
				if (strTemp.Length <= 2) continue;  //skip blank line
				bStartswith = strTemp.StartsWith("//".ToString());
				if (!bStartswith)       //skip strarting of "//" lines, it is only comments
				{
					//if ((iTableHeader == 0) || (iTableCotrol == 0)
					//|| (iNumAxii == 0) || (iNumXPoint == 0) || (iNumYPoint == 0))
					if ((iTableHeader == 0) || ((iTableHeader != 0) && (iTmpHead < iTableHeader)))
					{   //read Header
						if (strNum != null) strNum = null;
						//strNum = new string(
						ch = new char[strTemp.Length];
						ch = strTemp.ToCharArray();

						#region read digital value before "//"
						for (int i = 0; i < strTemp.Length; i++)
						{
							if ((ch[i] <= 0x39) && (ch[i] >= 0x30))
							{
								if (i == 0)
								{
									strNum = new string(ch[i], 1);
								}
								else
								{
									strNum += ch[i].ToString();
								}
							}
							else
							{
								if ((ch[i] == '/') && (ch[i + 1] == '/'))
								{
									break;      // found comments, break for loop
								}
							}
						}
						if (!int.TryParse(strNum, out iRead))
						{
							//refError = LibErrorCode.IDS_ERR_EGDLL_RCTABLE_CONTENT;
							return bTable;
						}
						#endregion

						#region save table header in varaiable
						switch (iTmpHead)
						{
							case 0:
								{
									iTableHeader = int.Parse(strNum);
									iTmpHead++;
									break;
								}
							case 1:
								{
									iTableCotrol = int.Parse(strNum);
									iTmpHead++;
									break;
								}
							case 2:
								{
									iNumAxii = int.Parse(strNum);
									iTmpHead++;
									break;
								}
							case 3:
								{
									iNumXPoint = int.Parse(strNum);
									iTmpHead++;
									break;
								}
							case 4:
								{
									iNumYPoint = int.Parse(strNum);
									iTmpHead++;
									break;
								}
							case 5:
								{
									iNumFAC = int.Parse(strNum);
									iTmpHead++;
									break;
								}
						}
						#endregion

					}
					else
					{
						#region read x, and y value
						if (iTmpHead != iTableHeader)
						{
							//refError = LibErrorCode.IDS_ERR_EGDLL_RCTABLE_CONTENT;
							return false;
						}
						else
						{
							if (!bFoundX)
							{
								strArrayX = strTemp.Split(',');
								bFoundX = true;
							}
							else if (!bFoundY)
							{
								strArrayY = strTemp.Split(',');
								bFoundY = true;
							}
							else
							{
								//refError = LibErrorCode.IDS_ERR_EGDLL_RCTABLE_CONTENT;
								return bTable;
							}
						}
						#endregion
					}
				}
			}
			stmContent.Close();
			#endregion

			#region do simple check of header value and x,y value, save x/y value inot List<GG2WayTable>
			if ((strArrayX != null) && (strArrayY != null))
			{
				if ((strArrayX.Length != iNumXPoint) || (strArrayY.Length != iNumXPoint))
				{
					//refError = LibErrorCode.IDS_ERR_EGDLL_RCTABLE_CONTENT;
					return bTable;
				}
				else
				{
					targetTable = new List<GG2WayTable>();
					float d1, d2;
					for (int i = 0; i < iNumXPoint; i++)
					{
						if ((float.TryParse(strArrayX[i], out d1)) && (float.TryParse(strArrayY[i], out d2)))
						{
							if (tabletype == SettingFlag.FTHERM) d1 *= iTableCotrol;
							//GG2WayTable tTemp = new GG2WayTable(float.Parse(strArrayX[i]), float.Parse(strArrayY[i]));
							GG2WayTable tTemp = new GG2WayTable(d1, d2);
							targetTable.Add(tTemp);
						}
					}
				}
			}
			else
			{
				//refError = LibErrorCode.IDS_ERR_EGDLL_RCTABLE_CONTENT;
				bTable = false;
			}
			if (targetTable.Count == iNumXPoint)
			{
				bTable = true;
			}
			else
			{
				//refError = LibErrorCode.IDS_ERR_EGDLL_RCTABLE_CONTENT;
				bTable = false;
			}
			#endregion
			//tablefs = File.OpenRead(strFilePath);
			//tablefs.re

			return bTable;
		}

		private bool ReadRCTableContent(string strFilePath)
		{
			bool bRC = false;
			StreamReader stmContent = null;
			string strTemp = null, strNum = null;
			bool bStartswith = false;
			bool bFoundX = false, bFoundW = false, bFoundV = false, bFoundY = false;
			char[] ch = null;
			int iTmpHead = 0, iRead = 0;
			int iTableHeader = 0, iTableCotrol = 0, iNumAxii = 0;
			int iNumXPoint = 0, iNumWPoint = 0, iNumVPoint = 0, iNumYPoint = 0, iNumFAC;
			float dbRead = 0;
			string[] strArrayX = null;

			if (RCXaxis != null)
			{
				RCXaxis.Clear();
			}
			if (RCWaxis != null)
			{
				RCWaxis.Clear();
			}
			if (RCVaxis != null)
			{
				RCVaxis.Clear();
			}
			if (RCYaxis != null)
			{
				RCYaxis.Clear();
			}
			RCXaxis = new List<float>();
			RCWaxis = new List<float>();
			RCVaxis = new List<float>();
			RCYaxis = new List<float>();

			if (!File.Exists(strFilePath))
			{
				//refError = LibErrorCode.IDS_ERR_EGDLL_RCTABLE_CONTENT;
				return bRC;
			}

			#region read file, and save content into strArrayX, strArrayY, and all header value
			stmContent = new StreamReader(strFilePath);
			iTmpHead = 0;
			while (!stmContent.EndOfStream)
			{
				strTemp = stmContent.ReadLine();
				strTemp.TrimStart(' ');
				if (strTemp.Length <= 2) continue;  //skip blank line
				bStartswith = strTemp.StartsWith("//".ToString());
				if (!bStartswith)       //skip strarting of "//" lines, it is only comments
				{
					//if ((iTableHeader == 0) || (iNumAxii == 0) ||
					//(iNumXPoint == 0) || (iNumWPoint == 0) || (iNumVPoint == 0) || (iNumYPoint == 0))
					if ((iTableHeader == 0) || ((iTableHeader != 0) && (iTmpHead < iTableHeader)))
					{   //read Header
						if (strNum != null) strNum = null;
						//strNum = new string(
						ch = new char[strTemp.Length];
						ch = strTemp.ToCharArray();

						#region read digital value before "//"
						for (int i = 0; i < strTemp.Length; i++)
						{
							if ((ch[i] <= 0x39) && (ch[i] >= 0x30))
							{
								if (i == 0)
								{
									strNum = new string(ch[i], 1);
								}
								else
								{
									strNum += ch[i].ToString();
								}
							}
							else
							{
								if ((ch[i] == '/') && (ch[i + 1] == '/'))
								{
									break;      // found comments, break for loop
								}
							}
						}
						if (!int.TryParse(strNum, out iRead))
						{
							//refError = LibErrorCode.IDS_ERR_EGDLL_RCTABLE_CONTENT;
							return bRC;
						}
						#endregion

						#region save table header in varaiable
						switch (iTmpHead)
						{
							case 0:
								{
									iTableHeader = int.Parse(strNum);
									iTmpHead++;
									break;
								}
							case 1:
								{
									iTableCotrol = int.Parse(strNum);
									iTmpHead++;
									break;
								}
							case 2:
								{
									iNumAxii = int.Parse(strNum);
									iTmpHead++;
									break;
								}
							case 3:
								{
									iNumXPoint = int.Parse(strNum);
									iTmpHead++;
									break;
								}
							case 4:
								{
									iNumWPoint = int.Parse(strNum);
									iTmpHead++;
									break;
								}
							case 5:
								{
									iNumVPoint = int.Parse(strNum);
									iTmpHead++;
									break;
								}
							case 6:
								{
									iNumYPoint = int.Parse(strNum);
									iTmpHead++;
									break;
								}
							case 7:
								{
									iNumFAC = int.Parse(strNum);
									iTmpHead++;
									break;
								}
						}
						#endregion

					}
					else
					{
						#region read x, and y value
						if (iTmpHead != iTableHeader)
						{
							//refError = LibErrorCode.IDS_ERR_EGDLL_RCTABLE_CONTENT;
							return false;
						}
						else
						{
							if (!bFoundX)
							{
								strArrayX = strTemp.Split(',');
								foreach (string strX in strArrayX)
								{
									if (!float.TryParse(strX, out dbRead))
									{
										//refError = LibErrorCode.IDS_ERR_EGDLL_RCTABLE_CONTENT;
										return bRC;
									}
									RCXaxis.Add(dbRead);
								}
								bFoundX = true;
							}
							else if (!bFoundW)
							{
								strArrayX = strTemp.Split(',');
								foreach (string strX in strArrayX)
								{
									if (!float.TryParse(strX, out dbRead))
									{
										//refError = LibErrorCode.IDS_ERR_EGDLL_RCTABLE_CONTENT;
										return bRC;
									}
									RCWaxis.Add(dbRead);
								}
								bFoundW = true;
							}
							else if (!bFoundV)
							{
								strArrayX = strTemp.Split(',');
								foreach (string strX in strArrayX)
								{
									if (!float.TryParse(strX, out dbRead))
									{
										//refError = LibErrorCode.IDS_ERR_EGDLL_RCTABLE_CONTENT;
										return bRC;
									}
									RCVaxis.Add(dbRead);
								}
								bFoundV = true;
							}
							else if (!bFoundY)
							{
								strArrayX = strTemp.Split(',');
								foreach (string strX in strArrayX)
								{
									if (!float.TryParse(strX, out dbRead))
									{
										//refError = LibErrorCode.IDS_ERR_EGDLL_RCTABLE_CONTENT;
										return bRC;
									}
									RCYaxis.Add(dbRead);
								}
								//bFoundY = true;
							}
							else
							{
								//refError = LibErrorCode.IDS_ERR_EGDLL_RCTABLE_CONTENT;
								return bRC;
							}
						}
						#endregion
					}
				}
			}
			stmContent.Close();
			#endregion

			#region do simple check for number of RCX, RXW, RCV, and the most important is  RCY
			if (RCXaxis.Count == iNumXPoint)
			{
				if (RCWaxis.Count == iNumWPoint)
				{
					if (RCVaxis.Count == iNumVPoint)
					{
						if (RCYaxis.Count == (float)(iNumXPoint * iNumWPoint * iNumVPoint * iNumYPoint))
						{
							bRC = true;
						}
					}
				}
			}
			#endregion

			return bRC;

		}

		private string GetXElementValueByName(XElement snode, string strname)
		{
			if (snode.Element(strname) == null) return null;        //only no element
																	//else if (String.IsNullOrEmpty(snode.Element(strname).Value)) return null;	//if string null
			else return snode.Element(strname).Value;
		}

		//no used
		private bool ParseFileStringFromProjectXML(XElement elIn, ref UInt32 refError)
		{
			bool bFile = false;
			ushort uNum = 0;
			ushort uID = 0;

			IEnumerable<XElement> xeln = from myTarget in elIn.Elements("Element") where myTarget.HasElements select myTarget;
			foreach (XElement fnode in xeln)
			{
				if (fnode.Attribute("ID") != null)
				{
					if (GetXElementValueByName(fnode, "FilePath") == null)
					{
						refError = LibErrorCode.IDS_ERR_EGDLL_PROJECT_SET;
						return bFile;
					}
					uID = Convert.ToUInt16(fnode.Attribute("ID").Value, 16);

					switch (uID)
					{
						case (ushort)SettingFlag.FDLL:
							{
								strFileGGDll = GetXElementValueByName(fnode, "FilePath");
								uNum++;
								break;
							}
						case (ushort)SettingFlag.FOCVTSOC:
							{
								strOCVbyTSOCTable = GetXElementValueByName(fnode, "FilePath");
								if (strOCVbyTSOCTable != null)
								{
									bFile = ReadTwoAxisTableContent(ref OCVbyTSOCTable, strOCVbyTSOCTable, SettingFlag.FOCVTSOC);
									if (!bFile)
									{
										refError = LibErrorCode.IDS_ERR_EGDLL_OCVBYTSOC_CONTENT;
										return bFile;   //read table faile, return false
									}
									uNum++;
								}
								break;
							}
						case (ushort)SettingFlag.FTSOCOCV:
							{
								strTSOCbyOCVTable = GetXElementValueByName(fnode, "FilePath");
								if (strTSOCbyOCVTable != null)
								{
									bFile = ReadTwoAxisTableContent(ref TSOCbyOCVTable, strTSOCbyOCVTable, SettingFlag.FTSOCOCV);
									if (!bFile)
									{
										refError = LibErrorCode.IDS_ERR_EGDLL_TSOCBYOCV_CONTENT;
										return bFile;   //read table faile, return false
									}
									uNum++;
								}
								break;
							}
						case (ushort)SettingFlag.FRC:
							{
								strRCTable = GetXElementValueByName(fnode, "FilePath");
								if (strRCTable != null)
								{
									bFile = ReadRCTableContent(strRCTable);
									if (!bFile)
									{
										refError = LibErrorCode.IDS_ERR_EGDLL_RCTABLE_CONTENT;
										return bFile;   //read table faile, return false
									}
									uNum++;
								}
								break;
							}
						case (ushort)SettingFlag.FTHERM:
							{
								strThermTable = GetXElementValueByName(fnode, "FilePath");
								if (strThermTable != null)
								{
									bFile = ReadTwoAxisTableContent(ref ThermalTable, strThermTable, SettingFlag.FTHERM);
									if (!bFile)
									{
										refError = LibErrorCode.IDS_ERR_EGDLL_THERMALTABLE_CONTENT;
										return bFile;   //read table faile, return false
									}
									uNum++;
								}
								break;
							}
						case (ushort)SettingFlag.FSELFDSG:
							{
								strSelfDsgTable = GetXElementValueByName(fnode, "FilePath");
								if (strSelfDsgTable != null)
								{
									bFile = ReadTwoAxisTableContent(ref SelfDsgTable, strSelfDsgTable, SettingFlag.FSELFDSG);
									if (!bFile)
									{
										refError = LibErrorCode.IDS_ERR_EGDLL_SELFDSGTABLE_CONTENT;
										return bFile;   //read table faile, return false
									}
									uNum++;
								}
								break;
							}
						case (ushort)SettingFlag.FRITable:
							{
								strRITable = GetXElementValueByName(fnode, "FilePath");
								if (strRITable != null)
								{
									bFile = ReadTwoAxisTableContent(ref RITable, strRITable, SettingFlag.FRITable);
									if (!bFile)
									{
										refError = LibErrorCode.IDS_ERR_EGDLL_RITABLE_CONTENT;
										return bFile;   //read table faile, return false
									}
									uNum++;
								}
								break;
							}
					}
				}
				else
				{
					break;
				}
			}

			if (uNum == (ushort)SettingFlag.FileNum)
			{
				bFile = true;
			}
			else if ((uNum + 1) == (ushort)SettingFlag.FileNum)     //if there is no TSOCbyOCV file, OK to go
			{
				if (strTSOCbyOCVTable == null)
				{
					bFile = true;
				}
			}

			return bFile;
		}

		private bool ParseParamValueFromProjectXML(XElement elIn, ref UInt32 refError)
		{
			bool bParam = false;
			ushort uNum = 0;
			ushort uID = 0;
			float ftmp;
			int itmp;

			IEnumerable<XElement> xeln = from myTarget in elIn.Elements("Element") where myTarget.HasElements select myTarget;
			foreach (XElement fnode in xeln)
			{
				if (fnode.Attribute("ID") != null)
				{
					if (GetXElementValueByName(fnode, "Value") == null)
					{
						refError = LibErrorCode.IDS_ERR_EGDLL_PROJSET_VALUE;
						return bParam;
					}

					uID = Convert.ToUInt16(fnode.Attribute("ID").Value, 16);
					switch (uID)
					{
						case (ushort)SettingFlag.PDC:
							{
								//dbDesignCp = Convert.ToDouble(GetXElementValueByName(fnode, "Value"));
								if (float.TryParse(GetXElementValueByName(fnode, "Value"), out ftmp))
								{
									dbDesignCp = ftmp;
									uNum++;
								}
								break;
							}
						case (ushort)SettingFlag.PRS:
							{
								//dbRsense = Convert.ToDouble(GetXElementValueByName(fnode, "Value"));
								if (float.TryParse(GetXElementValueByName(fnode, "Value"), out ftmp))
								{
									dbRsense = (float)(ftmp * 0.001);
									uNum++;
								}
								break;
							}
						case (ushort)SettingFlag.PXTPR:
							{
								//dbPullupR = Convert.ToDouble(GetXElementValueByName(fnode, "Value"));
								if (float.TryParse(GetXElementValueByName(fnode, "Value"), out ftmp))
								{
									dbPullupR = ftmp;
									uNum++;
								}
								break;
							}
						case (ushort)SettingFlag.PXTPV:
							{
								//dbPullupV = Convert.ToDouble(GetXElementValueByName(fnode, "Value"));
								if (float.TryParse(GetXElementValueByName(fnode, "Value"), out ftmp))
								{
									dbPullupV = ftmp;
									uNum++;
								}
								break;
							}
						case (ushort)SettingFlag.PCHGCV:
							{
								//dbChgCVVolt = Convert.ToDouble(GetXElementValueByName(fnode, "Value"));
								if (float.TryParse(GetXElementValueByName(fnode, "Value"), out ftmp))
								{
									dbChgCVVolt = ftmp;
									uNum++;
								}
								break;
							}
						case (ushort)SettingFlag.PCHGCUR:
							{
								//dbChgEndCurr = Convert.ToDouble(GetXElementValueByName(fnode, "Value"));
								if (float.TryParse(GetXElementValueByName(fnode, "Value"), out ftmp))
								{
									dbChgEndCurr = ftmp;
									uNum++;
								}
								break;
							}
						case (ushort)SettingFlag.PDSGV:
							{
								//dbDsgEndVolt = Convert.ToDouble(GetXElementValueByName(fnode, "Value"));
								if (float.TryParse(GetXElementValueByName(fnode, "Value"), out ftmp))
								{
									dbDsgEndVolt = ftmp;
									uNum++;
								}
								break;
							}
						case (ushort)SettingFlag.PCellNum:
							{
								if (int.TryParse(GetXElementValueByName(fnode, "Value"), out itmp))
								{
									iCellNum = itmp;
								}
								break;
							}
						case (ushort)SettingFlag.PRBAT:
							{
								//dbRsense = Convert.ToDouble(GetXElementValueByName(fnode, "Value"));
								if (float.TryParse(GetXElementValueByName(fnode, "Value"), out ftmp))
								{
									dbRbat = (float)(ftmp * 0.001);
									uNum++;
								}
								break;
							}
						case (ushort)SettingFlag.PRCON:
							{
								//dbRsense = Convert.ToDouble(GetXElementValueByName(fnode, "Value"));
								if (float.TryParse(GetXElementValueByName(fnode, "Value"), out ftmp))
								{
									dbRcon = (float)(ftmp * 0.001);
									uNum++;
								}
								break;
							}
					}
				}
				else
				{
					break;
				}
			}

			if (uNum == (ushort)SettingFlag.ParamNum)
			{
				bParam = true;
			}
			else
			{
				refError = LibErrorCode.IDS_ERR_EGDLL_PROJSET_VALUE;
				bParam = false;
			}

			return bParam;
		}

		//(M140407)Francis, according to 4/4 meeting, table file path will not be saved in Project.xml, so need to read it individually.
		private bool OpenTableFiles(ref UInt32 refError)
		{
			bool bOpen = true;
			refError = LibErrorCode.IDS_ERR_SUCCESSFUL;

			if ((strOCVbyTSOCTable != null) && (OCVbyTSOCTable == null))
			{
				bOpen &= ReadTwoAxisTableContent(ref OCVbyTSOCTable, strOCVbyTSOCTable, SettingFlag.FOCVTSOC);
				if (!bOpen)
				{
					refError = LibErrorCode.IDS_ERR_EGDLL_OCVBYTSOC_CONTENT;
					return bOpen;
				}
			}
			if ((strTSOCbyOCVTable != null) && (TSOCbyOCVTable == null))
			{
				bOpen &= ReadTwoAxisTableContent(ref TSOCbyOCVTable, strTSOCbyOCVTable, SettingFlag.FTSOCOCV);
				if (!bOpen)
				{
					refError = LibErrorCode.IDS_ERR_EGDLL_TSOCBYOCV_CONTENT;
					return bOpen;
				}
			}
			if ((strRCTable != null) && (RCYaxis == null))
			{
				bOpen = ReadRCTableContent(strRCTable);
				if (!bOpen)
				{
					refError = LibErrorCode.IDS_ERR_EGDLL_RCTABLE_CONTENT;
					return bOpen;
				}
			}
			if ((strThermTable != null) && (ThermalTable == null))
			{
				bOpen = ReadTwoAxisTableContent(ref ThermalTable, strThermTable, SettingFlag.FTHERM);
				if (!bOpen)
				{
					refError = LibErrorCode.IDS_ERR_EGDLL_THERMALTABLE_CONTENT;
					return bOpen;
				}
			}
			if ((strSelfDsgTable != null) && (SelfDsgTable == null))
			{
				bOpen = ReadTwoAxisTableContent(ref SelfDsgTable, strSelfDsgTable, SettingFlag.FSELFDSG);
				if (!bOpen)
				{
					refError = LibErrorCode.IDS_ERR_EGDLL_SELFDSGTABLE_CONTENT;
					return bOpen;
				}
			}
			if ((strRITable != null) && (RITable == null))
			{
				bOpen = ReadTwoAxisTableContent(ref RITable, strRITable, SettingFlag.FRITable);
				if (!bOpen)
				{
					refError = LibErrorCode.IDS_ERR_EGDLL_CHGTABLE_CONTENT;
					return bOpen;
				}
			}
			//(A150714)Francis, open charge table
			if ((strChgTable != null) && (ChgTable == null))
			{
				bOpen = ReadTwoAxisTableContent(ref ChgTable, strChgTable, SettingFlag.FCHGTable);
				if (!bOpen)
				{
					refError = LibErrorCode.IDS_ERR_EGDLL_CHGTABLE_CONTENT;
					return bOpen;
				}
			}
			//(E150714)Francis

			if ((OCVbyTSOCTable == null) || (TSOCbyOCVTable == null) || (RCYaxis == null) ||
				(ThermalTable == null) || (SelfDsgTable == null))// ||
																 //(ChgTable == null))// || (RITable == null))					//(A150714)Francis, charge table
				bOpen = false;

			return bOpen;
		}
		//(E140407)

		static public bool CheckTableFiles(ref UInt32 refError, List<string> projtable, bool bForceChg = false, bool bNoCheckProject = false)
		{
			bool bRet = true;
			GasGaugeProject prjTmp = new GasGaugeProject(projtable);

			bRet = prjTmp.InitializeProject(ref refError, bForceChg, bNoCheckProject);

			return bRet;
		}

	}
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
