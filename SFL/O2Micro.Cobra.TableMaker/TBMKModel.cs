using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.ComponentModel;
using System.Windows;
using System.Xml;
using System.Xml.Linq;
using O2Micro.Cobra.Common;
//using System.Data.SQLite;

namespace O2Micro.Cobra.TableMaker
{
	//public part for external usage

	public enum TypeEnum : int
    {
        RCRawType = 0,
        OCVRawType = 1,
        ChargeRawType = 2,
        FalconLYType = 3,
        ErrorType = -9999,
    }

	public enum MakeParamEnum : byte
	{
		MakeVersion = 0,
		MakeDate = 1,
		MakeComment = 2,
	}

	public class TableError
	{
		public string strFilePath;					//SourceData file path
		public UInt32 uiSerialNumber;			//SerialNumber that occur error
		public float fVoltage;							//Voltage reading when occur error
		public float fCurrent;							//Current from SourceData header
		public float fTemperature;					//Temperature from SourceData header
		public UInt32 uiErrorCode;				//ErrorCode of what error happened
		public string strErrorDescri;				//Error description, coming from LibErrorCode.cs
		//public string GetErrorDescrip() {return LibErrorCode.GetErrorDescription(uiErrorCode); }

		public TableError()
		{
			strFilePath = null;
			uiSerialNumber = 0;
			fVoltage = 0F;
			fCurrent = 0F;
			fTemperature = 0F;
			uiErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
			//strErrorDescri = null;
		}

		public TableError(string iFilePath, UInt32 iSerialNumb, float iVoltage, float iCurrent, float iTemperature, UInt32 iErrorCode)
		{
			/*
			ErrorLogStruct newRecord = new ErrorLogStruct();
			newRecord.strFilePath = iFilePath;
			newRecord.uiSerialNumber = iSerialNumb;
			newRecord.fVoltage = iVoltage;
			newRecord.fCurrent = iCurrent;
			newRecord.fTemperature = iTemperature;
			newRecord.uiErrorCode = iErrorCode;
			newRecord.strErrorDescri = LibErrorCode.GetErrorDescription(iErrorCode);
			lstErrorLog.Add(newRecord);
			*/
			strFilePath = iFilePath;
			uiSerialNumber = iSerialNumb;
			fVoltage = iVoltage;
			fCurrent = iCurrent;
			fTemperature = iTemperature;
			uiErrorCode = iErrorCode;
			strErrorDescri = LibErrorCode.GetErrorDescription(iErrorCode);
		}

		/*
		public bool GenerateLogFile(string strOutFolder, bool bClear = false)
		{
			bool bReturn = false;

			if (bClear)
			{
				lstErrorLog.Clear();
			}

			return bReturn;
		}
		*/
	}



	public class TableSample
	{
		public static string strTBMVersion = "V006";

		private List<string> mySourceFilePath = new List<string>();
		//public List<SourceDataSample> mySourceData = new List<SourceDataSample>();
        //public List<UInt32> myVoltagePoints = new List<UInt32>();
		private Table.TableInterface myTable = null;
        private Table.FalconLYSample myFalconLYTable = new Table.FalconLYSample();
		private TypeEnum myType = TypeEnum.ErrorType;

		private List<TableError> myErrorLog = null;	//has its owned instance
		private string strErrorLogFolder; 
		private string strErrorLogFilePath = null;
		private UInt32 uTotalErrorNum = 0;

		public TableSample()
		{
			mySourceFilePath.Clear();
			myErrorLog = new List<TableError>();
			myErrorLog.Clear();
		}

		public bool AddSourceFile(string strInFile, ref UInt32 uErr, out TypeEnum oType)
		{
			bool bReturn = false;
			Table.SourceDataSample tmpSource = null;

			//(M140917)Francis, bugid=15206, as Guoyan request, ignore 2 same file, 
			#region Check existing opened fils path and skip repeated file and do nothing
			if (mySourceFilePath.Count != 0)
			{
				foreach (string strfptmp in mySourceFilePath)
				{
					if (strfptmp.Equals(strInFile, StringComparison.OrdinalIgnoreCase))	//same file string
					{
						oType = myType;	//cause oType must be assigned and mySourceFilePath has content; so used old type, it must be equal
						return true;
					}
				}
			}
			//(E140917)
			#endregion

			tmpSource = new Table.SourceDataSample(strInFile);
			/* (D141121)Francis, seperate Header/Experiment parsing into 2 functions.
			//(M140717)Francis, modify as Guoyan request, new raw data format
			//bReturn = tmpSource.ParseRawData(ref uErr);
			//bReturn = tmpSource.ParseRawDataNewFormat(ref uErr);
			//(E140717)
			 * */
			//(A141121)Francis, Parse Header first
			if (tmpSource.ParseSourceHeader(ref uErr))
			{
				bReturn = true;	//assign return true temporarily
			}
			else
			{	//return false, because it's unable to continue
				oType = TypeEnum.ErrorType;
				myErrorLog = tmpSource.srcError;
				return bReturn;
			}

			//Able to parse Header from file then parse experiment data
			#region Parse Experiment file and create myTable instance
			if (bReturn)
			{
				//Create Error Log file at first time
				if (myTable == null)
				{
					CreateErrorLogFileHeader(tmpSource);
				}
				//Parse Experiment Data Error code is arranged in ParseRawDataNewFormat()
				bReturn &= tmpSource.ParseRawDataNewFormat(ref uErr);
				if (!bReturn)	//if false, copy error log by ParseRawDataNewFormat() generating
				{
					if (myErrorLog == null)
					{
						myErrorLog = new List<TableError>();	//no error log instance create it.
					}

					foreach (TableError tberin in tmpSource.srcError)
					{
						myErrorLog.Add(tberin);
					}
					CreateErrorLogFileContent();
				}	//if (!bReturn)	//if false copy error log by ParseRawDataNewFormat() generating

				//(M141121)Francis, by Guoyan's request, if experiment data has error, still keep going
				//it's able get this Source Data Type from Header
				tmpSource.GetSourceType(out myType);

				if (myTable == null)
				{
					if (myType == TypeEnum.OCVRawType)
					{
						myTable = new Table.OCVSample();
					}
					else if (myType == TypeEnum.RCRawType)
					{
						myTable = new Table.RCSample();
					}
					else if (myType == TypeEnum.ChargeRawType)
					{
						myTable = new Table.ChargeSample();
					}
					else
					{
						#region //should not go here
						uErr = LibErrorCode.IDS_ERR_TMK_TBL_FILE_FORMAT;
						if (myErrorLog == null)
						{
							myErrorLog = new List<TableError>();
						}
						TableError teNever = new TableError(strInFile, UInt32.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, uErr);
						myErrorLog.Add(teNever);
						oType = TypeEnum.ErrorType;
						CreateErrorLogFileContent();
						return false;
						#endregion
					}
				}
				else
				{
					//already has myTable instance, check Type consistency
					if (myTable.GetTableType() != myType)
					{	//Type is not match with previous opened Source File
						uErr = LibErrorCode.IDS_ERR_TMK_TBL_FORMAT_MATCH;
						if (myErrorLog == null)
						{
							myErrorLog = new List<TableError>();
						}
						TableError teNever = new TableError(strInFile, UInt32.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, uErr);
						myErrorLog.Add(teNever);
						oType = TypeEnum.ErrorType;
						CreateErrorLogFileContent();
						return false;	//terrible error happened, cannot continue, return immediately 
					}
				}	//if(myTable == null)

				//Successfully create myTable or check Type consistency
				myTable.AddSourceData(tmpSource);	//add Source File into myTable, and copy error log
				mySourceFilePath.Add(strInFile);	//add File Path
				bReturn &= true;
			}	//if (bReturn), able to parse Header, not definitely need this if(bReturn)

			#endregion

			oType = myType;

			#region //bFranTestMode
			if (false)
			{
				List<UInt32> uCurrent = new List<UInt32>();
				uCurrent.Add(60);
				uCurrent.Add(120);
				uCurrent.Add(180);
				uCurrent.Add(240);
				uCurrent.Add(300);
				uCurrent.Add(360);
				uCurrent.Add(420);
				uCurrent.Add(480);
				uCurrent.Add(560);
				uCurrent.Add(600);
				MakeTable(uCurrent, ref uErr);
			}
			#endregion

			return bReturn;
		}

		public bool AddSourceFiles(List<string> strInFiles, ref UInt32 uErr, out TypeEnum oType)
		{
			bool bReturn = true;
			oType = TypeEnum.ErrorType;

			foreach (string strOne in strInFiles)
			{
				bReturn = AddSourceFile(strOne, ref uErr, out oType);
				if (!bReturn)
					break;
			}

			return bReturn;
		}

		public void ClearFiles()
		{
			mySourceFilePath.Clear();
			myTable = null;
			myType = TypeEnum.ErrorType;
			myErrorLog.Clear();	//clear file, also clear error log
		}

		public void GetSourceFiles(ref List<string> strOutFiles)
		{
			strOutFiles = mySourceFilePath;
		}

		public bool GetVoltageBoundry(ref UInt32 uLowVolt, ref UInt32 uHighVolt)
		{
			if (myTable != null)
			{
				return myTable.GetExpermentVoltBoundry(ref uLowVolt, ref uHighVolt);
			}
			else
			{
				return false;
			}
		}

		public bool MakeTable(List<UInt32> uVoltage, ref UInt32 uErr, string strFolder = null, List<string> mkParamString = null, bool bVTRboth = true)
		{
			bool bReturn = false;

			#region copy User Keyin comment
			if (mkParamString == null)
			{
				mkParamString = new List<string>();
			}
			if (mkParamString.Count < Enum.GetNames(typeof(MakeParamEnum)).Length)
			{
				mkParamString.Clear();
				mkParamString.Add("01");
				mkParamString.Add("102414");
				mkParamString.Add("Francis Testing");
			}
			#endregion

			if(myErrorLog != null)
				myErrorLog.Clear();	//(A141121)Francis, as Leon request, if start build table, clear old error log

			if (myTable != null)	
			{
				//(M141121)Francis, if error happened, copy error log and continue BuildTable
				/*
				if (myTable.InitializeTable(uVoltage, ref uErr, strFolder))
				{
					if (myTable.BuildTable(ref uErr, mkParamString))
					{
						bReturn = true;
					}
					else
					{
						//error log copy
					}
				}*/
				bReturn = myTable.InitializeTable(uVoltage, ref uErr, strFolder);
				//if (!bReturn)
				{
					//(M141124)Francis, it will be cleared when calling MakeTable(), so copy directly
					//foreach (TableError tbeB in myTable.tInsError)
					//{
						//myErrorLog.Add(tbeB);
					//}
					myErrorLog = myTable.tInsError;
				}
                myTable.bVTRboth = bVTRboth;        //(A170228)Francis, save from user input for VTR and TR table of new Gas Gauge algorithm
				bReturn &= myTable.BuildTable(ref uErr, mkParamString);
				if (!bReturn)
				{
					CreateErrorLogFileContent();
				}
                if (!bReturn)	//had error
                    CopyErrorLogToTarget(false);	//not to clear temp log
            }
			else
			{
				//should not happened
				uErr = LibErrorCode.IDS_ERR_TMK_TBL_BUILD_SEQUENCE;
				if (myErrorLog == null)
				{
					myErrorLog = new List<TableError>();
				}
				myErrorLog.Add(new TableError(strFolder, UInt32.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, uErr));
				CreateErrorLogFileContent();
			}


			return bReturn;
		}

		public bool GenerateFile(ref UInt32 uErr)
		{
			bool bReturn;
			myErrorLog.Clear();	
			bReturn =  myTable.GenerateFile(ref uErr);
			CopyErrorLogToTarget();

			return bReturn;
		}

		public void GetErrorLog(ref List<TableError> refLog)
		{
			refLog = myErrorLog; 
			//myTable.GetRawErrorLog(ref refLog);
		}

        //(A170314)Francis, read old tables, OCV txt file, RC txt file, and C/H Files, then convert to new IR table
        public void initTRTable(ref UInt32 uErr)
        {
            myFalconLYTable.InitializeTable(null, ref uErr);
        }

        public bool readOCVtxtFileContent(string strInFile, ref UInt32 uErr)
        {
            bool bReturn = false;

            if (!myFalconLYTable.readNCheckOCVtxtFile(strInFile, ref uErr))
                myErrorLog.Add(new TableError(strInFile, UInt32.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, uErr));
            else
                bReturn = true;

            return bReturn;
        }

        public bool readRCtxtFileContent(string strInFile, ref UInt32 uErr)
        {
            bool bReturn = false;

            if (!myFalconLYTable.readNCheckRCtxtFile(strInFile, ref uErr))
                myErrorLog.Add(new TableError(strInFile, UInt32.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, uErr));
            else
                bReturn = true;

            return bReturn;
        }

        public bool readCHFilesContent(List<string> strInFiles, ref UInt32 uErr)
        {
            bool bReturn = false;

            if (!myFalconLYTable.readNCheckCHFiles(strInFiles, ref uErr))
            {
                foreach (string strone in strInFiles)
                {
                    myErrorLog.Add(new TableError(strone, UInt32.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, uErr));
                }
            }
            else
                bReturn = true;

            return bReturn;
        }
        
        public bool makeFalconLYTabe(string strOutputFolder, List<string> lstrUserIn, ref UInt32 uErr)
        {
            bool bReturn = false;

            if(!myFalconLYTable.InitializeTable(null, ref uErr, strOutputFolder))
            {
                myErrorLog.Add(new TableError(strOutputFolder, UInt32.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, uErr));
            }
            else
            {
                if(!myFalconLYTable.BuildTable(ref uErr, lstrUserIn))
                {
                    myErrorLog.Add(new TableError(string.Empty, UInt32.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, uErr));
                }
                else
                {
                    bReturn = true;
                }
            }

            return bReturn;
        }
        //(E170314)

		//below are no use
		public bool CheckRawFile(string strRawFile, ref UInt32 uErr)
		{
			bool bReturn = false;
			Table.SourceDataSample tmpRaw;

			if (strRawFile == null)
			{
				uErr = LibErrorCode.IDS_ERR_TMK_SD_FILEPATH_NULL;
			}
			else
			{
				if (!File.Exists(strRawFile))
				{
					uErr = LibErrorCode.IDS_ERR_TMK_SD_FILE_NOT_EXIST;
				}
				else
				{
					tmpRaw = new Table.SourceDataSample(strRawFile);
					if (!System.IO.Path.GetExtension(strRawFile).ToLower().Equals(".csv"))
					{
						uErr = LibErrorCode.IDS_ERR_TMK_SD_FILE_EXTENSION;
					}
					else
					{
						//error code will assigned
						//if (tmpRaw.OpenCSVFile(strRawFile, ref uErr))
						bReturn = tmpRaw.ParseRawDataNewFormat(ref uErr, null, false);
						if (!bReturn)
						{
							myType = TypeEnum.ErrorType;
							//Error code is arranged in ParseRawData()
						}
						else
						{
						}
					}
				}
			}

			return bReturn;
		}

		public bool AddHeaderInfor()
		{
			bool bReturn = false;

			return bReturn;
		}

		public bool LoadRawFile()
		{
			bool bReturn = false;

			return bReturn;
		}

		public bool MakeSource()
		{
			bool bReturn = false;

			return bReturn;
		}

		private void CreateErrorLogFileHeader(Table.SourceDataSample inSample = null)
		{
			string strTmpToWrite;
			FileStream fsErrLog = null;
			StreamWriter stmErrLog = null;
			Table.SourceDataHeader parsHdSample = null ;

			if (inSample == null)
			{
				if (myTable != null)
				{
					parsHdSample = myTable.TableSourceHeader;
				}
			}
			else
			{
				parsHdSample = inSample.myHeader;
			}

			if (parsHdSample == null)		//should no go here
			{
				fsErrLog = File.Open(strErrorLogFolder + "CriticalError", FileMode.Create, FileAccess.Write, FileShare.None);
				fsErrLog.Close();
				return;
			}

			strErrorLogFilePath = parsHdSample.strManufacture + "_" + parsHdSample.strBatteryModel + "_" + parsHdSample.fAbsMaxCap.ToString() + "mAhr.csv";
			strErrorLogFolder = System.IO.Path.Combine(FolderMap.m_currentproj_folder, "Log\\");
			if (!Directory.Exists(strErrorLogFolder))
			{
				Directory.CreateDirectory(strErrorLogFolder);
			}
			try
			{
				fsErrLog = File.Open(strErrorLogFolder+strErrorLogFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
				stmErrLog = new StreamWriter(fsErrLog, Encoding.Unicode);
			}
			catch (Exception ec)
			{
				return;		//should not happened
			}

			if (stmErrLog != null)
			{
				strTmpToWrite = string.Format("File Path \t"+"Serial Numer \t" + "Experiment Current \t" + 
																		"Experiment Temperature \t" + "Experiment Voltage \t" + "Error Code \t" + "Description");
				stmErrLog.WriteLine(strTmpToWrite);
				stmErrLog.Close();
				fsErrLog.Close();
			}
		}

		private void CreateErrorLogFileContent()
		{
			string strTmpToWrite;
			FileStream fsErrLog = null;
			StreamWriter stmErrLog = null;
			string strC, strT, strV, strS, strCode;

			try
			{
				if (!File.Exists(strErrorLogFolder + strErrorLogFilePath))
				{
					CreateErrorLogFileHeader();	//just for case
				}

				fsErrLog = File.Open(strErrorLogFolder + strErrorLogFilePath, FileMode.Append, FileAccess.Write, FileShare.None);
				stmErrLog = new StreamWriter(fsErrLog, Encoding.Unicode);
			}
			catch (Exception ec)
			{
				return;		//should not happened
			}

			if (stmErrLog != null)
			{
				foreach (TableError tbde in myErrorLog)
				{
					if (tbde.uiSerialNumber == UInt32.MaxValue)
					{
						strS = "--";
					}
					else
					{
						strS = string.Format("{0}", tbde.uiSerialNumber);
					}
					if (tbde.fCurrent == float.MaxValue)
					{
						strC = "--";
					}
					else
					{
						strC = string.Format("{0}", tbde.fCurrent);
					}
					if (tbde.fTemperature == float.MaxValue)
					{
						strT = "--";
					}
					else
					{
						strT = string.Format("{0}", tbde.fTemperature);
					}
					if (tbde.fVoltage == float.MaxValue)
					{
						strV = "--";
					}
					else
					{
						strV = string.Format("{0}", tbde.fVoltage);
					}

					strCode = string.Format("0x{0:X8}", tbde.uiErrorCode);

					strTmpToWrite = string.Format(tbde.strFilePath + "\t" + strS + "\t" + strC + "\t" + strT + "\t" + strV + "\t" + strCode + "\t" + tbde.strErrorDescri);
					stmErrLog.WriteLine(strTmpToWrite);
					uTotalErrorNum +=1;
				}
				stmErrLog.Close();
				fsErrLog.Close();
			}
		}

		private void CopyErrorLogToTarget(bool bClear = true)
		{
			if (File.Exists(strErrorLogFolder + strErrorLogFilePath))
			{
				if (uTotalErrorNum != 0)
				{
					File.Copy(strErrorLogFolder + strErrorLogFilePath, myTable.TableOutputFolder + "\\ErrorLog_" + strErrorLogFilePath);
					uTotalErrorNum = 0;
				}
				if (bClear)
				{
					File.Delete(strErrorLogFolder + strErrorLogFilePath);
				}
			}
		}
	}
}





//------ below are new construction ------//



namespace O2Micro.Cobra.TableMaker.Table
{
	public class RawDataNode
	{
		public UInt32 uSerailNum { get; set; }
		public float fVoltage { get; set; }
		public float fCurrent { get; set; }
		public float fTemperature { get; set; }
		public float fAccMah { get; set; }
		public DateTime dtRecord { get; set; }
		public float fSoCAdj { get; set; }

		public RawDataNode(string strN, string strV, string strC, string strT, string strAcc, string strDt, float fUnit = 1)
		{
			float ftmp;
			UInt32 utmp;

			if (!UInt32.TryParse(strN, out utmp))
			{
				uSerailNum = Convert.ToUInt32(-1);
			}
			else
			{
				uSerailNum = utmp;
			}
			if (float.TryParse(strV, out ftmp))
			{
				fVoltage = ftmp * fUnit;
			}
			if (float.TryParse(strC, out ftmp))
			{
				fCurrent = ftmp * fUnit;
			}
			if (float.TryParse(strT, out ftmp))
			{
				fTemperature = ftmp;
			}
			if (float.TryParse(strAcc, out ftmp))
			{
				fAccMah = ftmp * fUnit;
			}

			ConvertStringToDateTime(strDt);
		}

		public RawDataNode(UInt32 uN, string strV, string strC, string strT, string strAcc, string strDt, float fUnit = 1)
		{
			float ftmp;
			UInt32 utmp;

			//if (!UInt32.TryParse(strN, out utmp))
			{
				uSerailNum = uN;
			}
			//else
			//{
				//uSerailNum = utmp;
			//}
			if (float.TryParse(strV, out ftmp))
			{
				fVoltage = ftmp * fUnit;
			}
			if (float.TryParse(strC, out ftmp))
			{
				fCurrent = ftmp * fUnit;
			}
			if (float.TryParse(strT, out ftmp))
			{
				fTemperature = ftmp;
			}
			if (float.TryParse(strAcc, out ftmp))
			{
				fAccMah = ftmp * fUnit;
			}

			ConvertStringToDateTime(strDt);
		}

		public RawDataNode(UInt32 uN, float fV, float fC, float fT, float fAcc, string strDt, float fUnit = 1)
		{
			//float ftmp;
			//UInt32 utmp;

			//if (!UInt32.TryParse(strN, out utmp))
			{
				uSerailNum = uN;
			}
			//else
			//{
			//uSerailNum = utmp;
			//}
			//if (float.TryParse(strV, out ftmp))
			{
				fVoltage = fV * fUnit;
			}
			//if (float.TryParse(strC, out ftmp))
			{
				fCurrent = fC * fUnit;
			}
			//if (float.TryParse(strT, out ftmp))
			{
				fTemperature = fT;
			}
			//if (float.TryParse(strAcc, out ftmp))
			{
				fAccMah = fAcc * fUnit;
                if (fAccMah < -0.0001)
                    fAccMah *= -1.0F;
			}

			ConvertStringToDateTime(strDt);
		}

		private bool ConvertStringToDateTime(string strTime)
		{
			bool bReturn = false;
			int iSlash = 0;		//for date
			int iComm = 0;	//for time
			char[] chr = strTime.ToCharArray();
			string strtmp;
			int iYear = 0, iMonth = 0, iDay = 0, iHour = 0, iMinute = 0, iSecond = 0;

			strtmp = "";
			for (int i = 0; i < chr.Length; i++)
			{
				if (chr[i].Equals('-'))
				{
					iSlash++;
					if (iSlash == 1)
					{
                        //iYear = Convert.ToInt32(strtmp);
						int.TryParse(strtmp, out iYear);
						strtmp = "";
					}
					else if (iSlash == 2)
					{
                        //iMonth = Convert.ToInt32(strtmp);
						int.TryParse(strtmp, out iMonth);
						strtmp = "";
					}
				}
				else if (chr[i].Equals(' '))
				{
                    //iDay = Convert.ToInt32(strtmp);
					int.TryParse(strtmp, out iDay);
					strtmp = "";
				}
				else if (chr[i].Equals(':'))
				{
					iComm++;
					if (iComm == 1)
					{
                        //iHour = Convert.ToInt32(strtmp);
						int.TryParse(strtmp, out iHour);
						if (iHour >= 24) iHour %= 24;
						strtmp = "";
					}
					else if (iComm == 2)
					{
                        //iMinute = Convert.ToInt32(strtmp);
						int.TryParse(strtmp, out iMinute);
						if (iMinute >= 60) iMinute -= 60;
						strtmp = "";
					}
				}
				else
				{
					strtmp += chr[i];
				}
			}
            //iSecond = Convert.ToInt32(strtmp);
			int.TryParse(strtmp, out iSecond);
			if (iSecond >= 60) iSecond -= 60;

			if ((iSlash == 2) && (iComm == 2))
			{
				dtRecord = new DateTime(iYear, iMonth, iDay, iHour, iMinute, iSecond);
				bReturn = true;
			}
			else if (iComm == 2)
			{
				DateTime nowTime = DateTime.Now;
				dtRecord = new DateTime(nowTime.Year, nowTime.Month, nowTime.Day, iHour, iMinute, iSecond);
				bReturn = true;
			}
			else
			{
				//just for case
				DateTime nowTime = DateTime.Now;
				dtRecord = new DateTime(nowTime.Year, nowTime.Month, nowTime.Day, nowTime.Hour, nowTime.Minute, nowTime.Second);
				bReturn = false;
			}
			{
			}
			return bReturn;
		}
	}

	public class SourceDataHeader
	{
		#region prefix of header definition
		private string Line01Title = "Type:";
		private string Line02Title = "Test Time:";
		private string Line03Title = "Equipment:";
		private string Line04Title = "Manufacture Factory:";
		private string Line05Title = "Battery Model:";
		private string Line06Title = "Cycle Count:";
		private string Line07Title = "Temperature(DegC):";
		private string Line08Title = "Current(mA):";
		private string Line09Title = "Measurement Gain:";
		private string Line10Title = "Measurement Offset(mV):";
		private string Line11Title = "Trace Resistance(mohm):";
		private string Line12Title = "Capacity Difference(mAh):";
		private string Line13Title = "Absolute Max Capacity(mAh):";
		private string Line14Title = "Limited Charge Voltage(mV):";
		private string Line15Title = "Cut-off Discharge Voltage(mV):";
		private string Line16Title = "Tester:";
		private string Line17Title = "Battery ID:";
		private string Line18Title = "";
		private string Line19Title = "";
		private string Line20Title = "";
		private string Line21Title = "";
		private string Line22Title = "";
		private string Line23Title = "";
		private string Line24Title = "";
		private string HeaderValueSperate = ":";
		#endregion

		#region postfix of header definition
		private string Line01Tail = "";
		private string Line02Tail = "";
		private string Line03Tail = "";
		private string Line04Tail = "";
		private string Line05Tail = "";
		private string Line06Tail = "";
		private string Line07Tail = "DegC";
		private string Line08Tail = "mA";
		private string Line09Tail = "";
		private string Line10Tail = "mV";
		private string Line11Tail = "mohm";
		private string Line12Tail = "mAh";
		private string Line13Tail = "mAh";
		private string Line14Tail = "";
		private string Line15Tail = "";
		private string Line16Tail = "";
		private string Line17Tail = "";
		private string Line18Tail = "";
		private string Line19Tail = "";
		private string Line20Tail = "";
		private string Line21Tail = "";
		private string Line22Tail = "";
		private string Line23Tail = "";
		private string Line24Tail = "";
		#endregion

		#region content value string of header definition
		public string Line01Content = null;
		public string Line02Content = null;
		public string Line03Content = null;
		public string Line04Content = null;
		public string Line05Content = null;
		public string Line06Content = null;
		public string Line07Content = null;
		public string Line08Content = null;
		public string Line09Content = null;
		public string Line10Content = null;
		public string Line11Content = null;
		public string Line12Content = null;
		public string Line13Content = null;
		public string Line14Content = null;
		public string Line15Content = null;
		public string Line16Content = null;
		public string Line17Content = null;
		public string Line18Content = null;
		public string Line19Content = null;
		public string Line20Content = null;
		public string Line21Content = null;
		public string Line22Content = null;
		public string Line23Content = null;
		public string Line24Content = null;
		#endregion

		#region public header setting value, AKA string, word/int/ or float type declaration

		private float fErrorValue = -9999F;

		private string m_strType;
		public string strType
		{
			get { return m_strType; }
			set
			{
				m_strType = value;
				m_strType = m_strType.ToUpper();
				if (m_strType.Equals("RC") || m_strType.Equals("R"))
				{
					enuTypeValue = TypeEnum.RCRawType;
				}
				else if (m_strType.Equals("OCV") || m_strType.Equals("O"))
				{
					enuTypeValue = TypeEnum.OCVRawType;
				}
				else if (m_strType.Equals("CHARGE") || m_strType.Equals("C"))
				{
					enuTypeValue = TypeEnum.ChargeRawType;
				}
				else
				{
					enuTypeValue = TypeEnum.ErrorType;
				}
			}
		}
		public TypeEnum enuTypeValue;
		public string strTestTime = null;
		public string strEquip = null;
		public string strManufacture = null;
		public string strBatteryModel = null;
		public string strCycleCount = null;
		private string m_strTemperature;
		public string strTemperature
		{
			get { return m_strTemperature; }
			set
			{
				m_strTemperature = value;
				fTemperature = fErrorValue;
				if (m_strTemperature != null)
				{
					if (float.TryParse(m_strTemperature, out fTemperature))
					{
					}
				}
			}
		}
		public float fTemperature = -9999F;
		private string m_strCurrent;
		public string strCurrent
		{
			get { return m_strCurrent; }
			set
			{
				m_strCurrent = value;
				fCurrent = fErrorValue;
				if (m_strCurrent != null)
				{
					if (float.TryParse(m_strCurrent, out fCurrent))
					{
					}
				}
				if ((enuTypeValue == TypeEnum.RCRawType) || (enuTypeValue == TypeEnum.OCVRawType))
				{
					if (fCurrent > 0) fCurrent *= -1;	//adjust to minus value
				}
				else if (enuTypeValue == TypeEnum.ChargeRawType)
				{
					if ((fCurrent < 0) && (fCurrent != fErrorValue))
					{
						fCurrent *= -1;	//adjust to positive value
					}
				}
			}
		}
		public float fCurrent = -500F;
		private string m_strMeasureGain;
		public string strMeasureGain
		{
			get { return strMeasureGain; }
			set
			{
				m_strMeasureGain = value;
				fMeasureGain = fErrorValue;
				if (m_strMeasureGain != null)
				{
					if (float.TryParse(m_strMeasureGain, out fMeasureGain))
					{
					}
				}
			}
		}
		public float fMeasureGain = -9999F;
		private string m_strMeasureOffset;
		public string strMeasureOffset
		{
			get { return m_strMeasureOffset; }
			set
			{
				m_strMeasureOffset = value;
				fMeasureOffset = fErrorValue;
				if (m_strMeasureOffset != null)
				{
					if (float.TryParse(m_strMeasureOffset, out fMeasureOffset))
					{
					}
				}
			}
		}
		public float fMeasureOffset = -9999F;
		private string m_strTraceResis;
		public string strTraceResis
		{
			get { return strTraceResis; }
			set
			{
				m_strTraceResis = value;
				fTraceResis = fErrorValue;
				if (m_strTraceResis != null)
				{
					if (float.TryParse(m_strTraceResis, out fTraceResis))
					{
					}
				}
			}
		}
		public float fTraceResis = -9999F;
		private string m_strCapacityDiff;
		public string strCapacityDiff
		{
			get { return strCapacityDiff; }
			set
			{
				m_strCapacityDiff = value;
				fCapacityDiff = fErrorValue;
				if (m_strCapacityDiff != null)
				{
					if (float.TryParse(m_strCapacityDiff, out fCapacityDiff))
					{
					}
				}
			}
		}
		public float fCapacityDiff = -9999F;
		private string m_strAbsMaxCap;
		public string strAbsMaxCap
		{
			get { return m_strAbsMaxCap; }
			set
			{
				m_strAbsMaxCap = value;
				fAbsMaxCap = fErrorValue;
				if (m_strAbsMaxCap != null)
				{
					if (float.TryParse(m_strAbsMaxCap, out fAbsMaxCap))
					{
					}
				}
			}
		}
		public float fAbsMaxCap = -9999F;
		private string m_strLimitChgVolt;
		public string strLimitChgVolt
		{
			get { return m_strLimitChgVolt; }
			set
			{
				m_strLimitChgVolt = value;
				fLimitChgVolt = fErrorValue;
				if (m_strLimitChgVolt != null)
				{
					if (float.TryParse(m_strLimitChgVolt, out fLimitChgVolt))
					{
					}
				}
			}
		}
		public float fLimitChgVolt = -9999;
		private string m_strCutoffDsgVolt;
		public string strCutoffDsgVolt
		{
			get { return m_strCutoffDsgVolt; }
			set
			{
				m_strCutoffDsgVolt = value;
				fCutoffDsgVolt = fErrorValue;
				if (m_strCutoffDsgVolt != null)
				{
					if (float.TryParse(m_strCutoffDsgVolt, out fCutoffDsgVolt))
					{
					}
				}
			}
		}
		public float fCutoffDsgVolt = -9999;
		public string strTester = null;
		public string strBatteryID = null;

		#endregion

		public float fFullCapacity;
		//public string strRootPath;
		public int iEmptyLineDefine = 8;

		public SourceDataHeader()
		{
			//default fake value
			fFullCapacity = 0F;
			//strRootPath = Environment.CurrentDirectory.ToString();
			Line04Content = "Foxconn";
			Line05Content = "MLP594082";
			iEmptyLineDefine = 8;
			fLimitChgVolt = 0F;
			fCutoffDsgVolt = 0F;
		}

		public bool ReadFiletoHeader(string strDataFile, ref UInt32 uErr)
		{
			bool bReturn = false;
			Stream stmHeader = File.Open(strDataFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			StreamReader sconHeader = new StreamReader(stmHeader);
			string strTemp;
			int iEmp = 0;
			int iHeader = 0;

			do
			{
				strTemp = sconHeader.ReadLine();
				if ((iEmp == iEmptyLineDefine) ||
					(iEmp == -9999))
				{
					break;
				}

				strTemp = strTemp.Replace(",", "");
				if (strTemp.Trim().Length != 0)
				{
					iHeader += 1;
					switch (iHeader)
					{
						case 1:
							{
								//Line01Content = DeletePrefix(strTemp, Line01Title);
								Line01Content = strTemp.Substring(strTemp.IndexOf(HeaderValueSperate) + 1);
								Line01Content = DeletePostix(Line01Content, Line01Tail);
								Line01Content = Line01Content.Replace(" ", "");
								strType = new string(Line01Content.ToCharArray());
								break;
							}
						case 2:
							{
								//Line02Content = DeletePrefix(strTemp, Line02Title);
								Line02Content = strTemp.Substring(strTemp.IndexOf(HeaderValueSperate) + 1);
								Line02Content = DeletePostix(Line02Content, Line02Tail);
								Line02Content = Line02Content.Replace(" ", "");
								strTestTime = new string(Line02Content.ToCharArray());
								break;
							}
						case 3:
							{
								//Line03Content = DeletePrefix(strTemp, Line03Title);
								Line03Content = strTemp.Substring(strTemp.IndexOf(HeaderValueSperate) + 1);
								Line03Content = DeletePostix(Line03Content, Line03Tail);
								Line03Content = Line03Content.Replace(" ", "");
								strEquip = new string(Line03Content.ToCharArray());
								break;
							}
						case 4:
							{
								//Line04Content = DeletePrefix(strTemp, Line04Title);
								Line04Content = strTemp.Substring(strTemp.IndexOf(HeaderValueSperate) + 1);
								Line04Content = DeletePostix(Line04Content, Line04Tail);
								Line04Content = Line04Content.Replace(" ", "");
								strManufacture = new string(Line04Content.ToCharArray());
								break;
							}
						case 5:
							{
								//Line05Content = DeletePrefix(strTemp, Line05Title);
								Line05Content = strTemp.Substring(strTemp.IndexOf(HeaderValueSperate) + 1);
								Line05Content = DeletePostix(Line05Content, Line05Tail);
								Line05Content = Line05Content.Replace(" ", "");
								strBatteryModel = new string(Line05Content.ToCharArray());
								break;
							}
						case 6:
							{
								//Line06Content = DeletePrefix(strTemp, Line06Title);
								Line06Content = strTemp.Substring(strTemp.IndexOf(HeaderValueSperate) + 1);
								Line06Content = DeletePostix(Line06Content, Line06Tail);
								Line06Content = Line06Content.Replace(" ", "");
								strCycleCount = new string(Line06Content.ToCharArray());
								break;
							}
						case 7:
							{
								//Line07Content = DeletePrefix(strTemp, Line07Title);
								Line07Content = strTemp.Substring(strTemp.IndexOf(HeaderValueSperate) + 1);
								Line07Content = DeletePostix(Line07Content, Line07Tail);
								Line07Content = Line07Content.Replace(" ", "");
								strTemperature = new string(Line07Content.ToCharArray());
								break;
							}
						case 8:
							{
								//Line08Content = DeletePrefix(strTemp, Line08Title);
								Line08Content = strTemp.Substring(strTemp.IndexOf(HeaderValueSperate) + 1);
								Line08Content = DeletePostix(Line08Content, Line08Tail);
								Line08Content = Line08Content.Replace(" ", "");
								strCurrent = new string(Line08Content.ToCharArray());
								break;
							}
						case 9:
							{
								//Line09Content = DeletePrefix(strTemp, Line09Title);
								Line09Content = strTemp.Substring(strTemp.IndexOf(HeaderValueSperate) + 1);
								Line09Content = DeletePostix(Line09Content, Line09Tail);
								Line09Content = Line09Content.Replace(" ", "");
								strMeasureGain = new string(Line09Content.ToCharArray());
								break;
							}
						case 10:
							{
								//Line10Content = DeletePrefix(strTemp, Line10Title);
								Line10Content = strTemp.Substring(strTemp.IndexOf(HeaderValueSperate) + 1);
								Line10Content = DeletePostix(Line10Content, Line10Tail);
								Line10Content = Line10Content.Replace(" ", "");
								strMeasureOffset = new string(Line10Content.ToCharArray());
								break;
							}
						case 11:
							{
								//Line11Content = DeletePrefix(strTemp, Line11Title);
								Line11Content = strTemp.Substring(strTemp.IndexOf(HeaderValueSperate) + 1);
								Line11Content = DeletePostix(Line11Content, Line11Tail);
								Line11Content = Line11Content.Replace(" ", "");
								strTraceResis = new string(Line11Content.ToCharArray());
								break;
							}
						case 12:
							{
								//Line12Content = DeletePrefix(strTemp, Line12Title);
								Line12Content = strTemp.Substring(strTemp.IndexOf(HeaderValueSperate) + 1);
								Line12Content = DeletePostix(Line12Content, Line12Tail);
								Line12Content = Line12Content.Replace(" ", "");
								strCapacityDiff = new string(Line12Content.ToCharArray());
								break;
							}
						case 13:
							{
								//Line13Content = DeletePrefix(strTemp, Line13Title);
								Line13Content = strTemp.Substring(strTemp.IndexOf(HeaderValueSperate) + 1);
								Line13Content = DeletePostix(Line13Content, Line13Tail);
								Line13Content = Line13Content.Replace(" ", "");
								strAbsMaxCap = new string(Line13Content.ToCharArray());
								break;
							}
						case 14:
							{
								//Line14Content = DeletePrefix(strTemp, Line14Title);
								Line14Content = strTemp.Substring(strTemp.IndexOf(HeaderValueSperate) + 1);
								Line14Content = DeletePostix(Line14Content, Line14Tail);
								Line14Content = Line14Content.Replace(" ", "");
								strLimitChgVolt = new string(Line14Content.ToCharArray());
								break;
							}
						case 15:
							{
								//Line15Content = DeletePrefix(strTemp, Line15Title);
								Line15Content = strTemp.Substring(strTemp.IndexOf(HeaderValueSperate) + 1);
								Line15Content = DeletePostix(Line15Content, Line15Tail);
								Line15Content = Line15Content.Replace(" ", "");
								strCutoffDsgVolt = new string(Line15Content.ToCharArray());
								break;
							}
						case 16:
							{
								//Line16Content = DeletePrefix(strTemp, Line16Title);
								Line16Content = strTemp.Substring(strTemp.IndexOf(HeaderValueSperate) + 1);
								Line16Content = DeletePostix(Line16Content, Line16Tail);
								Line16Content = Line16Content.Replace(" ", "");
								strTester = new string(Line16Content.ToCharArray());
								break;
							}
						case 17:
							{
								//Line17Content = DeletePrefix(strTemp, Line17Title);
								Line17Content = strTemp.Substring(strTemp.IndexOf(HeaderValueSperate) + 1);
								Line17Content = DeletePostix(Line17Content, Line17Tail);
								Line17Content = Line17Content.Replace(" ", "");
								strBatteryID = new string(Line17Content.ToCharArray());
								break;
							}
						default:
							{
								iEmp = -9999;
								break;
							}
					}
				}
				else
				{
					iEmp += 1;
				}
			} while (strTemp != null);

			if (iEmp == iEmptyLineDefine)
			{
				fFullCapacity = fAbsMaxCap;		//(A140702)Francis, according guoyan's FullChargedCapacity calculation
				//(A140718)Francis,
				LibErrorCode.strVal01 = strDataFile;
				if (Math.Abs(fAbsMaxCap - 0) < fErrorValue*20)	//AbsMaxCap < 100, error
				{
					LibErrorCode.fVal01 = fAbsMaxCap;
					uErr = LibErrorCode.IDS_ERR_TMK_HD_ABSMAX_CAPACITY;
				}
				else if (Math.Abs(fLimitChgVolt - 0) < fErrorValue * 200)	//Charge Volt < 1000, error
				{
					LibErrorCode.fVal01 = fLimitChgVolt;
					uErr = LibErrorCode.IDS_ERR_TMK_HD_CHARGE_VOLTAGE;
				}
				else if (Math.Abs(fCutoffDsgVolt - 0) < fErrorValue * 200) //Cutoff Volt < 1000, error
				{
					LibErrorCode.fVal01 = fCutoffDsgVolt;
					uErr = LibErrorCode.IDS_ERR_TMK_HD_CUTOFF_VOLTAGE;
				}
				else
				{
					bReturn = true;
				}
				//(E140718)
			}
			else
			{
				uErr = LibErrorCode.IDS_ERR_TMK_HD_COLUMN;
			}

			sconHeader.Close();

			return bReturn;
		}

		public bool WriteHeaertoFile(string strTargetFile, ref UInt32 uErr)
		{
			Stream stmHeader = null;
			StreamWriter HdWriter = null;
			string strTemp = "";

			try
			{
				stmHeader  = File.Open(strTargetFile, FileMode.Create, FileAccess.Write, FileShare.None);
			}
			catch (Exception e)
			{
				LibErrorCode.strVal01 = strTargetFile;
				uErr = LibErrorCode.IDS_ERR_TMK_HD_WRITE_FAILED;
				return false;
			}

			HdWriter = new StreamWriter(stmHeader);

			if (enuTypeValue == TypeEnum.OCVRawType)
			{
				strTemp = new string("OCV".ToCharArray());
			}
			else if (enuTypeValue == TypeEnum.RCRawType)
			{
				strTemp = new string("RC".ToCharArray());
			}
			else if (enuTypeValue == TypeEnum.ChargeRawType)
			{
				strTemp = new string("CHARGE".ToCharArray());
			}
			else
			{
				uErr = LibErrorCode.IDS_ERR_TMK_HD_TYPE;
				return false;
			}

			HdWriter.WriteLine(Line01Title + strTemp + Line01Tail);
			HdWriter.WriteLine(Line02Title + strTestTime + Line02Tail);
			HdWriter.WriteLine(Line03Title + strEquip + Line03Tail);
			HdWriter.WriteLine(Line04Title + strManufacture + Line04Tail);
			HdWriter.WriteLine(Line05Title + strBatteryModel + Line05Tail);
			HdWriter.WriteLine(Line06Title + strCycleCount + Line06Tail);
			HdWriter.WriteLine(Line07Title + fTemperature.ToString() + Line07Tail);
			HdWriter.WriteLine(Line08Title + fCurrent.ToString() + Line08Tail);
			HdWriter.WriteLine(Line09Title + fMeasureGain.ToString() + Line09Tail);
			HdWriter.WriteLine(Line10Title + fMeasureOffset.ToString() + Line10Tail);
			HdWriter.WriteLine(Line11Title + fTraceResis.ToString() + Line11Tail);
			HdWriter.WriteLine(Line12Title + fCapacityDiff.ToString() + Line12Tail);
			HdWriter.WriteLine(Line13Title + fAbsMaxCap.ToString() + Line13Tail);
			HdWriter.WriteLine(Line14Title + fLimitChgVolt.ToString() + Line14Tail);
			HdWriter.WriteLine(Line15Title + fCutoffDsgVolt.ToString() + Line15Tail);
			HdWriter.WriteLine(Line16Title + strTester + Line16Tail);
			HdWriter.WriteLine(Line17Title + strBatteryID + Line17Tail);
			HdWriter.WriteLine(Line18Title + Line18Content + Line18Tail);
			HdWriter.WriteLine(Line19Title + Line19Content + Line19Tail);
			HdWriter.WriteLine(Line20Title + Line20Content + Line20Tail);
			HdWriter.WriteLine(Line21Title + Line21Content + Line21Tail);
			HdWriter.WriteLine(Line22Title + Line22Content + Line22Tail);
			HdWriter.WriteLine(Line23Title + Line23Content + Line23Tail);
			HdWriter.WriteLine(Line24Title + Line24Content + Line24Tail);

			HdWriter.Close();

			return true;
		}

		private string DeletePrefix(string strWhole, string strHeader)
		{
			return (strWhole.Replace(strHeader, " ").Trim());
		}

		private string DeletePostix(string strWhole, string strTail)
		{
			//string strTempTail = strTail.Replace("\r\n", " ").Trim();
			if (strTail.Length != 0)
				return (strWhole.Replace(strTail, " ").Trim());
			else
				return strWhole;
		}

	}

	public class SourceDataSample
	{
		#region private members declaration

		private enum O2DBRecord : int
		{
			RecCurrent = 10,
			RecVoltage = 11,
			RecTemperature = 19,
			RecAccMah = 16,
			RecTime = 34,
		}

		private enum O2TXTRecord : int
		{
			TxtSerial = 1,
			TxtChgDsg = 8,
			TxtTime = 9,
			TxtCurrent = 10,
			TxtVoltage = 11,
			TxtTemperature = 12,
			TxtAccMah = 13,
		}

		private enum O2JINFRecord : int
		{
			JFSerial = 0,
			JFTime = 1,
			JFChgDsg = 3,
			JFCurrent = 4,
			JFVoltage = 5,
			//JFTemperature = 
			JFAccMah = 8,
		}

        //(A170612)Francis, add for Chroma new format
        private enum O2ChromaRecord : int
        {
            ChSerial = 2,
            ChTime = 3,
            ChChgDsg = 6,
            ChCurrent = 8,
            ChVoltage = 9,
            ChTemperature = 10,
            ChAccMah = 11,
        }
        //(E170612)

		private char AcuTSeperate = ',';
		private string strSoCFileName { get; set; }	//use to save full path of SoC xls
		private string strSocRowHeader = null;
		private float fSoCStep = 0.05F;
		private float fErrorStep = 2.3F;
		//private bool bFranLearningCheck = false;

		#endregion

		#region public members declaration
		public SourceDataHeader myHeader = null;
		private string m_strSourceFilePath = null;
		public string strSourceFilePath
		{
			get { return m_strSourceFilePath; }
			set
			{
				m_strSourceFilePath = value;
				//int iIndex = m_strSourceFilePath.LastIndexOf('\\');
				//strSoCFilePath = new string(m_strSourceFilePath.Substring(0, iIndex + 1).ToCharArray());
				//strSoCFilePath += "SoC\\";
				//my
				//strSoCFilePath = System.IO.Path.Combine(myHeader.strRootPath, strSoCFilePath);
			}
		}
		private string m_strOutputFolder = null;
		public string strOutputFolder
		{
			get { return m_strOutputFolder; }
			set { m_strOutputFolder = value; }
		}
		//public bool bSuccessful { get; set; }
		public UInt32 uAccZero = 0;		//use to count time of if(AccMah == 0), if too much, bAccTrust will be false
		public bool bAccTrust = true;		//use to check does AccMah value is existing in Raw Data
		public float fMinExpVolt = 99999;
		public float fMaxExpVolt = -99999;

		private List<RawDataNode> m_RawDataCollect = new List<RawDataNode>();
		public List<RawDataNode> TableRawData
		{
			get { return m_RawDataCollect; }
			set { m_RawDataCollect = value; }
		}

		private List<RawDataNode> m_ReservedExpData = new List<RawDataNode>();
		public List<RawDataNode> ReservedExpData
		{
			get { return m_ReservedExpData; }
			set { m_ReservedExpData = value; }
		}

		private List<RawDataNode> m_AdjustedExpData = new List<RawDataNode>();
		public List<RawDataNode> AdjustedExpData
		{
			get { return m_AdjustedExpData; }
			set { m_AdjustedExpData = value; }
		}

		public List<TableError> srcError = null;

		#endregion

		public SourceDataSample(string strFile)
		{
			strSourceFilePath = strFile;
			//srcError = inErrorf;
			srcError = new List<TableError>();
		}

        /*
		// <summary>
		// To open raw file, could be *.db or *.txt, and read its content, save all raw data record into TableRawData,
		// but TableRawData only includes information about, voltage/current/temperature/time/AccMah
		// Opening *.db by calling OpenDBFile(); Opening *.txt by calling OpenTxtFile()
		// </summary>
		// <returns>True: if opening file and reading content both OK; otherwise false</returns>
		//no used
		public bool ParseRawData(ref UInt32 uErr, string strNewRaw = null)
		{
			bool bReturn = false;

			if (strNewRaw != null)
			{
				strSourceFilePath = strNewRaw;
			}

			if (strSourceFilePath != null)
			{
				if (File.Exists(strSourceFilePath))
				{
					myHeader = new SourceDataHeader();
					if (myHeader.ReadFiletoHeader(strSourceFilePath, ref uErr))
					{
						//assign a default output folder path
						strOutputFolder = System.IO.Path.GetDirectoryName(strSourceFilePath);
						if (System.IO.Path.GetExtension(strSourceFilePath).ToLower().Equals(".db"))
						{
							if (OpenDBFile(strSourceFilePath, ref uErr))
							{
								bReturn = true;
							}
						}
						else if (System.IO.Path.GetExtension(strSourceFilePath).ToLower().Equals(".txt"))
						{
							if (OpenTxtFile(strSourceFilePath, ref uErr))
							{
								bReturn = true;
							}
						}
						else if (System.IO.Path.GetExtension(strSourceFilePath).ToLower().Equals(".csv"))
						{
							if (OpenCSVFile(strSourceFilePath, ref uErr))
							{
								bReturn = true;
							}
						}
						else
						{
							uErr = LibErrorCode.IDS_ERR_TMK_SD_FILE_EXTENSION;
						}
					}
					else
					{
						//Error code should be assigned in ReadFiletoHeader
					}
				}
				else
				{
					uErr = LibErrorCode.IDS_ERR_TMK_SD_FILE_NOT_EXIST;
				}
			}
			else
			{
				uErr = LibErrorCode.IDS_ERR_TMK_SD_FILEPATH_NULL;
			}

			if (bReturn)
				PrepareSoCXls();

			return bReturn;
		}
        */

		public void GetSourceType(out TypeEnum outType)
		{
			outType = myHeader.enuTypeValue;
		}

		// <summary>
		// Suppose Experiment Steps are, 1st discharge battery to empty, 2nd charge battery to full, 3rd stay idle a little bit
		// then 4th starts experiment current discharging. The method will bypass records of 1st~3rd steps, and only save
		// records in ReservedExpData.
		// </summary>
		// <returns>True: if bypassing and reserving steps are all OK; otherwise return false</returns>
		//public bool ReserveOCVExpPoints(List<RawDataNode> inRawdata)
        public bool ReserveExpPoints(ref UInt32 uErr, UInt32 ulowVol = 3000)
		{
			bool bReturn = false;
			//float fSteps = (float)(OCVSample.fPerSteps * 0.01 * BattModel.wDesignCap);
			bool bFoundCharge = false;
			bool bFoundIdle = false;
			bool bExperimentStart = false;
			bool bExperimentCurr = false;
			bool bReachDsgEmpty = false;
			float fMaxCurrent = 0;
			float fVoltAdj;//, fCurrAdj, fTempAdj, fAccAdj;
			RawDataNode rdnAdjust = null;
            UInt32 iErrCount = 0;

			uAccZero = 0;
			bAccTrust = true;
			ReservedExpData.Clear();
			AdjustedExpData.Clear();
			//foreach (RawDataNode rdn in inRawdata)
			foreach (RawDataNode rdn in TableRawData)
			{
				if (bFoundCharge)
				{
					if (bFoundIdle)
					{
						if (bExperimentStart)
						{
							LibErrorCode.fVal01 = rdn.fCurrent;
							if ((rdn.fCurrent < 0))
							{
								if ((Math.Abs(rdn.fCurrent - myHeader.fCurrent) < fErrorStep))
								{	//here are useful experiment raw data, add it
									bExperimentCurr = true;
									ReservedExpData.Add(rdn);
									if ((rdn.fAccMah < 1) && (rdn.fAccMah > -1))
									{
										uAccZero += 1;
									}
									//wait Guoyan
									fVoltAdj = (rdn.fVoltage - myHeader.fMeasureOffset) / myHeader.fMeasureGain
										- (myHeader.fCurrent * myHeader.fTraceResis * 0.001F);
									rdnAdjust = new RawDataNode(rdn.uSerailNum.ToString(),
																							fVoltAdj.ToString(),
																							rdn.fCurrent.ToString(),
																							rdn.fTemperature.ToString(),
																							rdn.fAccMah.ToString(),
																							rdn.dtRecord.ToString());
									AdjustedExpData.Add(rdnAdjust);
									if(iErrCount > 0)
										iErrCount = 0;	//reset error counter
									if (rdn.fVoltage <= myHeader.fCutoffDsgVolt + fErrorStep)
									{
										bReachDsgEmpty = true;
										break;
									}
								}
								else
								{	//current reading is out of range of setting value
									//bExperimentCurr = false;
									LibErrorCode.uVal01 = rdn.uSerailNum; ;
									break;
								}
							}
							else
							{	//get current >= 0
								if (rdn.fVoltage <= myHeader.fCutoffDsgVolt + fErrorStep)
								{
									//stop discharging of experiment data, break
									break;
								}
								else if (Math.Abs(rdn.fCurrent) <= fErrorStep)
								{
									if (bReachDsgEmpty) break;
								}
								else
								{	//not reach to Cutoff Discharge Voltage
									iErrCount += 1;	//count continueous error
									if (iErrCount >= fErrorStep)
									{
										LibErrorCode.uVal01 = rdn.uSerailNum;
										bExperimentCurr = false;
										break;
									}
								}
							}
						}
						else
						{	//still in idle, not found experiment data
							if ((rdn.fCurrent < 0))
							{
								bExperimentStart = true;
								if ((Math.Abs(rdn.fCurrent - myHeader.fCurrent) < 5))
								{	//found experiment data
									bExperimentCurr = true;
									ReservedExpData.Add(rdn);	//the first one record
									//wait Guoyan
									fVoltAdj = (rdn.fVoltage - myHeader.fMeasureOffset) / myHeader.fMeasureGain
										- (myHeader.fCurrent * myHeader.fTraceResis * 0.001F);
									rdnAdjust = new RawDataNode(rdn.uSerailNum.ToString(),
																							fVoltAdj.ToString(),
																							rdn.fCurrent.ToString(),
																							rdn.fTemperature.ToString(),
																							rdn.fAccMah.ToString(),
																							rdn.dtRecord.ToString());
									AdjustedExpData.Add(rdnAdjust);
									if ((rdn.fAccMah < 1) && (rdn.fAccMah > -1))
									{
										uAccZero += 1;
									}
								}
							}
							else
							{	//still no start to discharge, ignore 
								continue;
							}
						}
					}
					else
					{
						if (((rdn.fVoltage + 10) >= myHeader.fLimitChgVolt))// && (rdn.fCurrent == 0F))
						{	//found fully charge and keeps in idle state
							bFoundIdle = true;
						}
						else
						{	//skip charging learning cycle
							continue;
						}
					}
				}
				else
				{	//not found charge state, skip
					if (rdn.fCurrent > 0)
					{	//found charge state, it's learning cycle starting
						bFoundCharge = true;
					}
					else if ((rdn.fCurrent < 0) && (Math.Abs(rdn.fCurrent - myHeader.fCurrent) < fErrorStep)) //will it be experiment data
					{	//voltage lower than High Bound, and idle or discharging
						//???
						if (fMaxCurrent < Math.Abs(rdn.fCurrent))
						{
							fMaxCurrent = Math.Abs(rdn.fCurrent);	//save maximum discharging current
						}
						continue;
					}
					else
					{
						continue;		//just for case, should no be here
					}
				}
			}

			if ((bFoundCharge == false) && (bFoundIdle == false) && (bExperimentStart == false))
			{
				//(D140702)Francis, it's dangerous that directly assign, just report false
				//if (Math.Abs(fMaxCurrent - myHeader.fFullCapacity * 0.025) < 10)
				//{
				//ReservedExpData = TableRawData;
				//}
				LibErrorCode.strVal01= strSourceFilePath;
				if (!bFoundCharge) uErr = LibErrorCode.IDS_ERR_TMK_SD_CHARGE_NOT_FOUND;
				if (!bFoundIdle) uErr = LibErrorCode.IDS_ERR_TMK_SD_CHARGE_NOT_FOUND;
				if (!bExperimentStart)
				{
					if (iErrCount != 0)
					{
						uErr = LibErrorCode.IDS_ERR_TMK_SD_EXPERIMENT_NOT_FOUND;
					}
					else
					{
						uErr = LibErrorCode.IDS_ERR_TMK_SD_NOT_CONTINUE;
					}
				}
				return bReturn;
			}

			if (!bExperimentCurr)
			{
				LibErrorCode.fVal02 = myHeader.fCurrent;
				uErr = LibErrorCode.IDS_ERR_TMK_SD_EXPERIMENT_NOT_MATCH;
				return bReturn;
			}

			if (!bReachDsgEmpty)
			{
				uErr = LibErrorCode.IDS_ERR_TMK_SD_NOT_REACH_EMPTY;
				return bReturn;
			}

			if (ReservedExpData.Count > 0)// 30000)	//in 0.025C experiment current and log every 30 second, there should be around 72K raw daa
			{
				bReturn = true;
				if (uAccZero > (ReservedExpData.Count * 0.3F))
				{
					bAccTrust = false;		//Acc has no value, need to count self
				}
			}
			else
			{
				uErr = LibErrorCode.IDS_ERR_TMK_SD_EXPERIMENT_ZERO;	//just for case, should not go here.
			}

			return bReturn;
		}

		//(A140717)Francis, calling ReadRawDataNewFormat() for new format and rules checking
		public bool ParseRawDataNewFormat(ref UInt32 uErr, string strNewRaw = null, bool bRecord = true)
		{
			bool bReturn = false;

			if (strNewRaw != null)
			{
				strSourceFilePath = strNewRaw;
			}

			if (strSourceFilePath != null)
			{
				if (File.Exists(strSourceFilePath))
				{
					myHeader = new SourceDataHeader();
					if (myHeader.ReadFiletoHeader(strSourceFilePath, ref uErr))
					{
						//assign a default output folder path
						strOutputFolder = System.IO.Path.GetDirectoryName(strSourceFilePath);
						if (ReadRawDataNewFormat(ref uErr, strNewRaw, bRecord))
						{
							bReturn = true;
						}
					}
					else
					{
						CreateNewError(UInt32.MaxValue, float.MaxValue, uErr);
						bReturn = false;
					}
				}	//if (File.Exists(strSourceFilePath))
			}	//if (strSourceFilePath != null)

			//if (bReturn)
				PrepareSoCXls();

			return bReturn;
		}

		//(A141121)Francis, seperate pasring Header Information and parsing Experiment data from ParseRawDataNewFormat()
		//ParseRawDataNewFormat() only does parsing Experiment data, ParseSourceHeader() does parsing Header Information
		// <summary>
		// Prasing Header Information 
		// </summary>
		// <param name="uErr">output, erorr code defined in LibErrorCode.cs</param>
		// <param name="strNewRaw">input, if not null, try to open-file it; otherwise, open-file strSourceFilePath</param>
		// <returns>true: if parse header information properly; otherwise return false</returns>
		public bool ParseSourceHeader(ref UInt32 uErr, string strNewRaw = null)
		{
			bool bReturn = false;

			if (strNewRaw != null)
			{
				strSourceFilePath = strNewRaw;
			}

			if (strSourceFilePath != null)
			{
				if (File.Exists(strSourceFilePath))
				{
					myHeader = new SourceDataHeader();
					bReturn = myHeader.ReadFiletoHeader(strSourceFilePath, ref uErr);
				}
			}
			else
			{
				uErr = LibErrorCode.IDS_ERR_TMK_TBL_FILEPATH;
				CreateNewError(UInt32.MaxValue, float.MaxValue, uErr);
			}

			return bReturn;
		}

		//(A140716)Francis, as discussed with Guoyan, Raw data format will be 1st one is idle, 2nd one is experiment, and 3rd one is idle rest;
		//only 3 kinds of log data in CSV file,
		//in this method, it will only fill up ReservedExpData, and AdjustedExpData; not usage of TableRawData
		//uErr: output for error code
		//strRawCSV: input for Source data file path
		//bRecord: true, to record into ReservedExpData List
		public bool ReadRawDataNewFormat(ref UInt32 uErr, string strRawCSV = null, bool bRecord = true)
		{
			bool bReturn = false;
			Stream stmCSV = null; 
			StreamReader stmContent = null;
			string strTemp;
			string[] strToken;
			char[] chSeperate = new char[] { AcuTSeperate };
			//int iAction;
			string sVoltage = "", sCurrent = "", sTemp = "", sAccM = "", sDate = "";
			float fVoltn = -1F, fCurrn = -1F, fTempn = -1F, fAccmn = -1F, fVoltOld = -1, fVoltAdj;
			bool bReachHighVolt = false, bStartExpData = false, bReachLowVolt = false,  bStopExpData = true;
			float ftmp;
			UInt32 iNumColCnt, iNumSrlNow, iNumSrlStart, iNumSrlEnd, iNumSrlLast, iNumZerotmp, iNumLostCount;
			float fVoltageDiff = 10F;

			if(strRawCSV == null)
			{
				//it's ok, use default strSourceFilePath
			}
			else
			{
				if(!File.Exists(strRawCSV))
				{
					uErr = LibErrorCode.IDS_ERR_TMK_SD_FILE_NOT_EXIST;
					CreateNewError(UInt32.MaxValue, float.MaxValue, uErr);
				}
				else
				{
					strSourceFilePath = strRawCSV;
				}
			}

			if(strSourceFilePath == null)
			{
				uErr = LibErrorCode.IDS_ERR_TMK_SD_FILEPATH_NULL;
				CreateNewError(UInt32.MaxValue, float.MaxValue, uErr);
			}
			else
			{
				try
				{
					stmCSV = File.Open(strSourceFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
					stmContent = new StreamReader(stmCSV);
				}
				catch (Exception e)
				{
					LibErrorCode.strVal01 = strSourceFilePath;
					uErr = LibErrorCode.IDS_ERR_TMK_SD_FILE_OPEN_FAILE;
					CreateNewError(UInt32.MaxValue, float.MaxValue, uErr);
					return bReturn;
				}
			}

			//initialization
			uErr = LibErrorCode.IDS_ERR_SUCCESSFUL;
			bReturn = true;
			iNumColCnt = 0; iNumSrlNow = 0; iNumSrlStart = 0;
			iNumSrlLast = iNumSrlStart;
			iNumSrlEnd = iNumSrlStart;
			iNumZerotmp = 0; iNumLostCount = 0;

			while ((strTemp = stmContent.ReadLine()) != null)
			{
				sVoltage.Remove(0, sVoltage.Length);
				sCurrent.Remove(0, sCurrent.Length);
				sTemp.Remove(0, sTemp.Length);
				sAccM.Remove(0, sAccM.Length);
				sDate.Remove(0, sDate.Length);
				//reserve last one value
				iNumSrlLast = iNumSrlNow;
				fVoltOld = fVoltn;

				#region skip other except log data line
				strToken = strTemp.Split(chSeperate, StringSplitOptions.None);	//split column by ',' character
				if (strToken.Length < (int)O2TXTRecord.TxtAccMah)
				{
					if ((strTemp.IndexOf(":") != -1) || (strTemp.Length == 0))
					{	//header file or empty line
						continue;
					}
					else
					{
						LibErrorCode.strVal01 = strSourceFilePath;
						uErr = LibErrorCode.IDS_ERR_TMK_TBL_FILE_FORMAT;
						CreateNewError(UInt32.MaxValue, float.MaxValue, uErr);
						break;
					}
				}
				else
				{
					if (!float.TryParse(strToken[(int)O2TXTRecord.TxtChgDsg], out ftmp))
					{
						//raw data start for next line
						continue;
					}
				}
				#endregion

				#region call format parsing, to get correct value for log line
				if (myHeader.strEquip.ToUpper().IndexOf("JY") != -1)
				{
					iNumSrlNow= 0;
					ftmp = 1.0F;		//JAYOU is in mA/mV/mAhr format
					ParseJYFormat(strToken, out sVoltage, out sCurrent, out sTemp, out sAccM, out sDate, out iNumSrlNow);
				}
				else if (myHeader.strEquip.ToUpper().IndexOf("JF") != -1)
				{
					iNumSrlNow = 0;
					ftmp = 0.001F;		//JINFAN is in A/V/Ahr format
					ParseJFFormat(strToken, out sVoltage, out sCurrent, out sTemp, out sAccM, out sDate, out iNumSrlNow);
				}
                //(A170612)Francis, add for Chroma new format
                else if(myHeader.strEquip.ToUpper().IndexOf("CHRO") != -1)
                {
                    iNumSrlNow = 0;
                    ftmp = 1000F;      //Chroma is in A/V/Ahr format
                    ParseChroFormat(strToken, out sVoltage, out sCurrent, out sTemp, out sAccM, out sDate, out iNumSrlNow);
                }
                //(E170612)
				else
				{
					uErr = LibErrorCode.IDS_ERR_TMK_SD_EQUIPEMNT;
					bReturn = false;
					CreateNewError(UInt32.MaxValue, float.MaxValue, uErr);
					return bReturn;
				}
				#endregion

				if ((sVoltage.Length != 0) && (sCurrent.Length != 0) &&
					(sTemp.Length != 0) && (sAccM.Length != 0) && (sDate.Length != 0) && iNumSrlNow != 0)
				{
					#region check volt/curr/temp/accm conveting to float
					LibErrorCode.uVal01 = iNumSrlNow;
					LibErrorCode.strVal01 = strSourceFilePath;
					if (!float.TryParse(sVoltage, out fVoltn))
					{
						LibErrorCode.strVal02 = sVoltage;
						uErr = LibErrorCode.IDS_ERR_TMK_SD_VOLTAGE_READ;
						CreateNewError(iNumSrlNow, float.MaxValue, uErr);
						break;
					}
					if (!float.TryParse(sCurrent, out fCurrn))
					{
						LibErrorCode.strVal02 = sCurrent;
						uErr = LibErrorCode.IDS_ERR_TMK_SD_CURRENT_READ;
						CreateNewError(iNumSrlNow, fVoltn, uErr);
						break;
					}
					if (!float.TryParse(sTemp, out fTempn))
					{
						LibErrorCode.strVal02 = sTemp;
						uErr = LibErrorCode.IDS_ERR_TMK_SD_TEMPERATURE_READ;
						CreateNewError(iNumSrlNow, fVoltn, uErr);
						break;
					}
					if (!float.TryParse(sAccM, out fAccmn))
					{
						LibErrorCode.strVal02 = sAccM;
						uErr = LibErrorCode.IDS_ERR_TMK_SD_ACCUMULATED_READ;
						CreateNewError(iNumSrlNow, fVoltn, uErr);
						break;
					}
                    fVoltn *= ftmp;
                    fCurrn *= ftmp;
                    fAccmn *= ftmp;
					#endregion

					if (fVoltn > fMaxExpVolt) fMaxExpVolt = fVoltn;//set maximum voltage value from raw data
					if (fVoltn < fMinExpVolt) fMinExpVolt = fVoltn;

					if (myHeader.enuTypeValue != TypeEnum.ChargeRawType)
					{
						#region check all log data about voltage/current, to get bReachHighVolt, bStartExpData, bStopExpData, and, bReachLowVolt value
						if (!bReachHighVolt)
						{		//initially, first time must go here, suppose not reach high voltage
							if ((Math.Abs(fVoltn - myHeader.fLimitChgVolt) < (fErrorStep * 10)))	//maybe log will only be idle stage after charge_to_full, set higher hysteresis
							{	//check voltage is reached high voltage, no matter charge/discharge/or idle mode
								bReachHighVolt = true;
							}
						}
						//if reached high voltage, wait for get experiment data
						else if ((bReachHighVolt) && (!bStartExpData))
						{
							if (fCurrn < 0)
							{
								if (Math.Abs(fCurrn - myHeader.fCurrent) < fErrorStep)
								{	//current is familiar with header setting
									bStartExpData = true;
									bStopExpData = false;
								}
								else
								{
									//not experiment current value
								}
							}
							//else wait for discharging value
						}
						//else if((bReachHighVolt) && (fCurrn > 0))	//still in charging learning cycle
						else if ((bStartExpData) && (!bReachLowVolt))
						{
                            if (Math.Abs(fCurrn - myHeader.fCurrent) < fErrorStep)
                            {
                                if ((Math.Abs(fVoltn - myHeader.fCutoffDsgVolt) < fErrorStep))	//stop discharging to idle
                                {
                                    bReachLowVolt = true;
                                }
                            }
                            else
                            {
                                if((Math.Abs(fCurrn - 0) < 10) && (Math.Abs(fVoltn - myHeader.fCutoffDsgVolt) < 500))
                                {
                                    bReachLowVolt = true;
                                }
                            }
							//else voltage still in range of High_Low voltage
						}
						else if ((bReachLowVolt) && (!bStopExpData))
						{
                            if ((Math.Abs(fCurrn - 0) < 10) || (fCurrn >= 0))
							{
								bStopExpData = true;
							}
						}
						#endregion
					}
					else
					{	//==TypeEnum.ChargeRawType
						#region check all log data about voltage/current in charging log data, to get bReachHighVoltage, bStartExpData, bStopExpData and bReachLowVolt value
						if (!bReachLowVolt)
						{	//initially, first time must go here, suppose not reach low voltage
							//(M140731)Francis, as Guoyan descirbed, that may be only log data from mid-voltage and charge to full
							//and perhaps there may be some idle state log data;
							//therefore use current ~= 0 and voltage is between max/min to check first data
							//if ((Math.Abs(fVoltn - myHeader.fCutoffDsgVolt) < (fErrorStep * 2)))
							if(((Math.Abs(fCurrn - 0) < fErrorStep) || (Math.Abs(fCurrn - myHeader.fCurrent) < fErrorStep))&&
								(fVoltn > myHeader.fCutoffDsgVolt) && 
								(fVoltn < myHeader.fLimitChgVolt))
							{	//check voltage is reached low voltage, from beginning
								bReachLowVolt = true;
								if (Math.Abs(fCurrn - myHeader.fCurrent) < fErrorStep)
								{	//just for case that 1st data is charging experiment data
									if (!bStartExpData)
									{
										bStartExpData = true;
										bStopExpData = false;
									}
								}
							}
						}
						//if reached low voltage, check charging current is same as header definition
						else if((bReachLowVolt) && (!bReachHighVolt))
						{
							if (fCurrn > 0)
							{
								if (!bStartExpData)
								{
									if (Math.Abs(fCurrn - myHeader.fCurrent) < fErrorStep)
									{	//current is familiar with header setting
										bStartExpData = true;
										bStopExpData = false;
									}
								}
								else if ((bStartExpData) && (!bStopExpData))
								{
									if ((Math.Abs(fVoltn - myHeader.fLimitChgVolt) < (fErrorStep * 2)))
									{
										bReachHighVolt = true;
									}
								}
							}	//if (fCurrn > 0)
							else
							{
								if (bStartExpData)	//if already detected experiment data start
								{
									LibErrorCode.fVal01 = fCurrn;
									uErr = LibErrorCode.IDS_ERR_TMK_CHG_DISCHARGE_DETECT;
								}
							}
						}
						else if (bReachHighVolt)
						{
							if ((fCurrn <= 0) && (Math.Abs(fVoltn - myHeader.fLimitChgVolt) < (fErrorStep * 10)))	//(M150127)Francis, as Guoyan request, modify to bigger tolerance
							{
								bStopExpData = true;
							}
						}
						#endregion
					}	//if (myHeader.enuTypeValue != TypeEnum.ChargeRawType) => else

					LibErrorCode.strVal01 = strSourceFilePath;
					if ((bStartExpData) && (!bStopExpData))		//experiment data starts but not stop
					{
						if (iNumSrlNow == iNumSrlLast)
						{
							//(M141201)Francis, as Guoyan request, skip this error and not log it
							//uErr = LibErrorCode.IDS_ERR_TMK_SD_SERIAL_SAME;
							//CreateNewError(iNumSrlNow, fVoltn, uErr);
							//(E141201)
						}
						#region check raw data is resonable or not
						if (iNumColCnt == 0)		//first one record
						{
							iNumSrlStart = iNumSrlNow;
							iNumSrlEnd = iNumSrlNow;
							if (myHeader.enuTypeValue == TypeEnum.ChargeRawType)
							{
								fVoltageDiff = 1.0F;	//positively changing
							}
							else if ((myHeader.enuTypeValue == TypeEnum.OCVRawType) || (myHeader.enuTypeValue == TypeEnum.RCRawType))
							{
								fVoltageDiff = -1.0F;	//negitively changing
							}
						}
						else		//after 2nd one
						{
							if ((iNumSrlNow - iNumSrlLast) > 1)	//jump more than 1
							{
								if ((iNumSrlNow - iNumSrlLast) > (UInt32)fErrorStep)	//jump more than 5
								{
									LibErrorCode.uVal01 = iNumSrlLast;
									LibErrorCode.uVal02 = iNumSrlNow;
									uErr = LibErrorCode.IDS_ERR_TMK_SD_NUMBER_JUMP;
									CreateNewError(iNumSrlNow, fVoltn, uErr);
								}
								else
								{
									//jump less than 5
									if (Math.Abs(fVoltn - fVoltOld) > fErrorStep)
									{
										LibErrorCode.uVal01 = iNumSrlNow;
										LibErrorCode.fVal01 = fVoltOld;
										LibErrorCode.fVal02 = fVoltn;
										uErr = LibErrorCode.IDS_ERR_TMK_SD_VOLTAGE_JUMP;
										CreateNewError(iNumSrlNow, fVoltn, uErr);
									}
									else
									{ //voltage is not jumping more 5mV
										iNumLostCount += (iNumSrlNow - iNumSrlLast - 1); //record not continuous serial 
									}
								}
							}
							else //jumping only 1 serail number, reasonable 
							{
								if (Math.Abs(fCurrn - 0) < fErrorStep)	//nearly zero current value found
								{
									iNumZerotmp += 1;
									if (iNumZerotmp > fErrorStep)	//continue more than 5 times
									{
										LibErrorCode.uVal01 = iNumSrlNow;
										LibErrorCode.strVal01 = strSourceFilePath;
										uErr = LibErrorCode.IDS_ERR_TMK_SD_EXPERIMENT_ZERO_CURRENT;
										CreateNewError(iNumSrlNow, fVoltn, uErr);
									}
								}
								else
								{
									if (iNumZerotmp != 0)
									{
										iNumZerotmp = 0;
									}
									//if (Math.Abs(fVoltn - fVoltOld) > fVoltageDiff )
									//if (Math.Abs(fVoltn - fVoltOld) > 5)
									if(((fVoltn - fVoltOld) * fVoltageDiff) < 0)		//check voltage changing direction, in charging, voltage should increase, vice versa
									{
										if (Math.Abs(fVoltn - fVoltOld) > 10.0F)	//if changing tolence is bigger than 10mV
										{
											LibErrorCode.uVal01 = iNumSrlNow;
											LibErrorCode.fVal01 = fVoltn;
											LibErrorCode.strVal01 = strSourceFilePath;
											uErr = LibErrorCode.IDS_ERR_TMK_SD_VOLTAGE_SEVERE;
											CreateNewError(iNumSrlNow, fVoltn, uErr);
										}
									}
								}
							}
							//if(Math.Abs(fVoltn - fVoltOld) != 0)
								//fVoltageDiff = Math.Abs(fVoltn - fVoltOld);
						}	//if (iNumColCnt == 0)		else	(first one)
						#endregion

						if (uErr != LibErrorCode.IDS_ERR_SUCCESSFUL)
						{
							//(M141117)Francis, if parsing error occurred, just let it keeps going parsing, no to break while loop
							bReturn = false;
							//break;
							//(E141117)
						}
						//if every check is Ok add into <List>
						iNumColCnt += 1;
						//(M141117)Francis, make sure bReturn is true; to make sure parsing is OK then to add inot ReservedExpData
						//if (bRecord)
						if ((bRecord) && (bReturn))		
						{
							//RawDataNode nodeN = new RawDataNode(iNumSrlNow, sVoltage, sCurrent, sTemp, sAccM, sDate, ftmp);
							RawDataNode nodeN = new RawDataNode(iNumSrlNow, fVoltn, fCurrn, fTempn, fAccmn, sDate, 1);  //(M170628)Francis, did multiple before
							//TableRawData.Add(nodeN);
							ReservedExpData.Add(nodeN);
							fVoltAdj = (fVoltn - myHeader.fMeasureOffset) / myHeader.fMeasureGain
								- (myHeader.fCurrent * myHeader.fTraceResis * 0.001F);
							RawDataNode rdnAdjust = new RawDataNode(iNumSrlNow, fVoltAdj, fCurrn, fTempn, fAccmn, sDate);
							AdjustedExpData.Add(rdnAdjust);
						}
					}	//if ((bStartExpData) && (!bStopExpData))
					else if ((bStartExpData) && (bStopExpData))		//experiment data start and stop detect
					{
						if (iNumSrlEnd == iNumSrlStart)
						{
							iNumSrlEnd = iNumSrlNow;
							iNumColCnt += 1;		//force to add last one record
							//(M141117)Francis, make sure bReturn is true; to make sure parsing is OK then to add inot ReservedExpData
							//if (bRecord)
							if ((bRecord) && (bReturn))		
							{
								//(A150806)Francis, if AccMah is jumping too much, skip last one record
								if (Math.Abs(fAccmn - myHeader.fAbsMaxCap) < (myHeader.fAbsMaxCap * 0.05))
								{
									//RawDataNode nodeN = new RawDataNode(iNumSrlNow, sVoltage, sCurrent, sTemp, sAccM, sDate, ftmp);
									RawDataNode nodeN = new RawDataNode(iNumSrlNow, fVoltn, fCurrn, fTempn, fAccmn, sDate, ftmp);
									//TableRawData.Add(nodeN);
									ReservedExpData.Add(nodeN);
									fVoltAdj = (fVoltn - myHeader.fMeasureOffset) / myHeader.fMeasureGain
										- (myHeader.fCurrent * myHeader.fTraceResis * 0.001F);
									RawDataNode rdnAdjust = new RawDataNode(iNumSrlNow, fVoltAdj, fCurrn, fTempn, fAccmn, sDate);
									AdjustedExpData.Add(rdnAdjust);
								}
							}
							break;		//after last one ignore it
						}
					}
				}	//if ((sVoltage.Length != 0) && (sCurrent.Length != 0) &&
			}	//while ((strTemp = stmContent.ReadLine()) != null)
			stmContent.Close();

			//if (iNumColCnt != (iNumSrlEnd - iNumSrlStart - iNumLostCount + 1))		//(A140702)Francis, as guoyan request, check total number
            if (myHeader.strEquip.ToUpper().IndexOf("CHRO") != -1)
            {
                iNumLostCount -= 2;
            }
            if ((iNumColCnt != (iNumSrlEnd - iNumSrlStart - iNumLostCount + 1)) 
				&& (	uErr == LibErrorCode.IDS_ERR_SUCCESSFUL))							//(M140728)Francis, And if there is nothing error in while loop
			{
				LibErrorCode.strVal01 = strSourceFilePath;
				LibErrorCode.uVal01 = iNumColCnt;
				LibErrorCode.uVal02 = (iNumSrlEnd - iNumSrlStart - iNumLostCount + 1);
				uErr = LibErrorCode.IDS_ERR_TMK_SD_NUMBER_MATCH;
				CreateNewError(iNumSrlNow, fVoltn, uErr);
				if ((!bStartExpData) && (bStopExpData))
				{
					uErr = LibErrorCode.IDS_ERR_TMK_SD_EXPERIMENT_NOT_FOUND;
					CreateNewError(iNumSrlNow, fVoltn, uErr);
				}
				else if((bStartExpData) && (!bStopExpData))
				{
					uErr = LibErrorCode.IDS_ERR_TMK_SD_NOT_REACH_EMPTY;
					CreateNewError(iNumSrlNow, fVoltn, uErr);
				}
				bReturn = false;
			}

			return bReturn;
		}

		public bool CreateSoCXls(string TargetFolder, ref UInt32 uErr)
		{
			bool bReturn = false;
			Stream stmWrite = null;
			StreamWriter SoCWriter = null;
			Int32 iCount = 1, iVoltage, iSoCVal;
			float fSoCA;
			string tempstring;
			string strWrite;

			if (TargetFolder == null)
			{
				uErr = LibErrorCode.IDS_ERR_TMK_SD_EMPTY_FOLDER;
				CreateNewError(UInt32.MaxValue, float.MaxValue, uErr);
				return bReturn;
			}
			//tempstring = "SoC" + myHeader.strTestTime.Replace("-", "").Substring(0, 8) + "\\";
			//SoC folder name default is SoC + today 8 number
			tempstring = "SoC" + DateTime.Now.Year.ToString("D4") +
				DateTime.Now.Month.ToString("D2") + DateTime.Now.Day.ToString("D2") + "\\";
			tempstring = System.IO.Path.Combine(TargetFolder, tempstring);

			try
			{
				if (!Directory.Exists(tempstring))
				{
					Directory.CreateDirectory(tempstring);
				}
			}
			catch (Exception e)
			{
				uErr = LibErrorCode.IDS_ERR_TMK_TBL_SOC_CREATE;
				CreateNewError(UInt32.MaxValue, float.MaxValue, uErr);
				return bReturn;
			}

			//tempstring = System.IO.Path.Combine(TargetFolder, tempstring);
			tempstring = System.IO.Path.Combine(tempstring, strSoCFileName);
			if (myHeader.WriteHeaertoFile(tempstring, ref uErr))
			{
				stmWrite = File.Open(tempstring, FileMode.Append, FileAccess.Write, FileShare.None);
				SoCWriter = new StreamWriter(stmWrite);

				SoCWriter.WriteLine(strSocRowHeader);
				//foreach (RawDataNode rdnT in AdjustedExpData)	//(M141107)Francis
				for (iCount = 1; iCount <= AdjustedExpData.Count; iCount++)	//(M141107)Francis
				{
					if (myHeader.enuTypeValue != TypeEnum.ChargeRawType)
					{
						//iSoCVal = (int)((myHeader.fAbsMaxCap - myHeader.fCapacityDiff - rdnT.fAccMah) *		//(M141107)Francis
							////iSoCVal = (int)((myHeader.fFullCapacity - rdnT.fAccMah) *
											//(10000 / myHeader.fAbsMaxCap) + 0.5)
						fSoCA = ((myHeader.fAbsMaxCap - myHeader.fCapacityDiff - AdjustedExpData[iCount - 1].fAccMah) *		//(M141107)Francis
											(10000 / myHeader.fAbsMaxCap));
					}
					else
					{
						//iSoCVal = (int)((myHeader.fCapacityDiff + rdnT.fAccMah) *
											//(10000 / myHeader.fAbsMaxCap) + 0.5);
						fSoCA = ((myHeader.fCapacityDiff + AdjustedExpData[iCount - 1].fAccMah) *
											(10000 / myHeader.fAbsMaxCap));
					}
					AdjustedExpData[iCount - 1].fSoCAdj = fSoCA;
					iVoltage = Convert.ToInt32(Math.Round(AdjustedExpData[iCount - 1].fVoltage, 0));
					iSoCVal = Convert.ToInt32(Math.Round(fSoCA, 0));
					//(M140917)Francis, bugid=15204, remove \t in csv file, so that value is able to calculated by Excel formula
					//strWrite = string.Format(iCount.ToString() + ",\t\t" + iVoltage.ToString() + ",\t\t" + iSoCVal.ToString());
					strWrite = string.Format(iCount.ToString() + "," + iVoltage.ToString() + "," + iSoCVal.ToString());
					//(E140917)
					SoCWriter.WriteLine(strWrite);
					//iCount += 1;		//(D141107)Francis
				}

				SoCWriter.Close();
				bReturn = true;
			}

			return bReturn;
		}

		//due to spec_2014-07-11, more raw data check, change OpenXXXFile() as public for TableSample used
		public bool OpenDBFile(string RawDB, ref UInt32 uErr)
		{
			uErr = LibErrorCode.IDS_ERR_TMK_SD_FILE_EXTENSION;
			return false;
		}

		// <summary>
		// Opening */txt file of raw data, read its content and save voltage/current/temperature/time/AccMah 
		// according to Bat 760B raw data format. 
		// Note that in Bat 760B raw data format, current value is always positive value, it needs to check type to see
		// it's charging or discharging.
		// </summary>
		// <returns>True, if voltage/current/temperature/time/AccMah are all OK; otherwise, return false</returns>
		public bool OpenTxtFile(string strRawTxt, ref UInt32 uErr)
		{
			bool bReturn = false;
			Stream stmTxt = File.Open(strRawTxt, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			StreamReader stmContent = new StreamReader(stmTxt);
			string strTemp;
			char[] ch = null;
			char[] digi = null;
			int i;
			int iNumCham, iLastI, iMinus,iNumCol;
			UInt32 iNumSerial, iNumSerStart;
			string sVoltage, sCurrent, sTemp, sAccM, sDate;

			iNumCol = 0; iNumSerial = 0; iNumSerStart = 99999999;
			while (!stmContent.EndOfStream)
			{
				strTemp = stmContent.ReadLine();
				ch = strTemp.ToCharArray();

				//not only digital char, having word char, skip
				iNumCham = 0;
				iLastI = 0;
				sVoltage = null;
				sCurrent = null;
				sTemp = null;
				sAccM = null;
				sDate = null;
				iMinus = 1;
				for (i = 0; i < ch.Length; i++)
				{
					//digi = null;
					if (((ch[i] > 0x39) || (ch[i] < 0x30)) && (ch[i] != AcuTSeperate) && (ch[i] != '.') & (ch[i] != ':'))
					{
						//word character
						//if (ch[i] != ',')
						//{
						break;
						//}
					}
					else
					{
						if (ch[i] == AcuTSeperate)
						{
							iNumCham += 1;	//found how many coma
							digi = strTemp.ToCharArray(iLastI, (i - iLastI));
							iLastI = i + 1;
							if (iNumCham == (int)O2TXTRecord.TxtChgDsg + 1)
							{
								if (digi[0] == '0')
								{
									iMinus = 0;		//idel
								}
								else if (digi[0] == '1')
								{
									iMinus = -1;	//discharge
								}
								else if (digi[0] == '2')
								{
									iMinus = 1;		//charge
								}
								else
								{
									iMinus = 0;		//just for case
								}
							}
							else if (iNumCham == (int)O2TXTRecord.TxtTime + 1)
							{
								sDate = new string(digi);
							}
							else if (iNumCham == (int)O2TXTRecord.TxtCurrent + 1)
							{
								if (iMinus < 0)
								{
									sCurrent = "-";
								}
								else
								{
									sCurrent = "";
								}
								sCurrent += new string(digi);
							}
							else if (iNumCham == (int)O2TXTRecord.TxtVoltage + 1)
							{
								sVoltage = new string(digi);
							}
							else if (iNumCham == (int)O2TXTRecord.TxtTemperature + 1)
							{
								sTemp = new string(digi);
							}
							else if (iNumCham == (int)O2TXTRecord.TxtAccMah + 1)
							{
								sAccM = new string(digi);
							}
							else if (iNumCham == (int)O2TXTRecord.TxtSerial + 1)
							{
								if (!UInt32.TryParse(digi.ToString(), out iNumSerial))
								{
									iNumSerial = 0;			//(A140702)Francis, as guoyan request, check total number
									break;
								}
								else
								{
									if (iNumSerial < iNumSerStart)
									{
										iNumSerStart = iNumSerial;
									}
								}
							}
							else
							{
								if ((sVoltage != null) && (sCurrent != null) && (sTemp != null) && (sAccM != null) && (sDate != null))
								{
									i = ch.Length;
									break;		//found all useful data, break for loop
								}
							}
						}
					}
				}
				if (i < ch.Length)	//found word character, next line
				{
					continue;
				}
				else if ((sVoltage != null) && (sCurrent != null) && (sTemp != null) && (sAccM != null) && (sDate != null))
				{
					RawDataNode nodeN = new RawDataNode(iNumSerial.ToString(), sVoltage, sCurrent, sTemp, sAccM, sDate);
					float fVoltage = 0.0F;
					//(A140708)Francis, as leon request, get voltage high/low boundry
					if (float.TryParse(sVoltage, out fVoltage))
					{
						if (fVoltage > fMaxExpVolt) fMaxExpVolt = fVoltage;
						if (fVoltage < fMinExpVolt) fMinExpVolt = fVoltage;
					}
					else
					{
						fMaxExpVolt = -99999.0F;
						fMinExpVolt = 99999.0F;
					}
					//(E140708)
					TableRawData.Add(nodeN);
				}
			}

			stmContent.Close();
			if (TableRawData.Count != 0)
			{
				if (iNumCol == (iNumSerial - iNumSerStart + 1))		//(A140702)Francis, as guoyan request, check total number
				{
					bReturn = true;
				}
			}

			return bReturn;
		}

		public bool OpenCSVFile(string strRawCSV, ref UInt32 uErr)
		{
			bool bReturn = true;

			Stream stmCSV = File.Open(strRawCSV, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			StreamReader stmContent = new StreamReader(stmCSV);
			string strTemp;
			string[] strToken;
			char[] chSeperate = new char[] { AcuTSeperate };
			//char[] digi = null;
			int iErrCount = 0;
			int iNumCol, iNumSub;//, iMinus;
			UInt32 iNumSerial, iNumSerStart, iNumSerLast;
			string sVoltage = "", sCurrent = "", sTemp = "", sAccM = "", sDate = "";
			float fLastVolt, fNowVolt, fLastCurr, fNowCurr;
			float ftmp;
			bool bReachChgFull = false;
			bool bReachDsgEmpty = false;
			bool bInitailStart = true;

			uErr = LibErrorCode.IDS_ERR_SUCCESSFUL;
			iNumCol = 0; iNumSerial = 0; iNumSerStart = 99999999; iNumSub = 0;
			iNumSerLast = 0;
			fLastVolt = -1F;
			fNowVolt = -1F;
			fLastCurr = -1F;
			while ((strTemp = stmContent.ReadLine()) != null)
			{
				strToken = strTemp.Split(chSeperate, StringSplitOptions.None);
				if (strToken.Length < (int)O2TXTRecord.TxtAccMah)
				{
					if ((strTemp.IndexOf(":") != -1) || (strTemp.Length == 0))
					{	//header file or empty line
						continue;
					}
					else
					{
						break;
					}
				}
				else
				{
					if (!float.TryParse(strToken[(int)O2TXTRecord.TxtChgDsg], out ftmp))
					//if (!int.TryParse(strToken[(int)O2TXTRecord.TxtChgDsg], out iMinus))
					{
						//raw data start for next line
						continue;
					}
				}
				if(myHeader.strEquip.ToUpper().IndexOf("JY") != -1)
				{
					iNumSerial = 0;
					ftmp = 1.0F;		//JAYOU is in mA/mV/mAhr format
					ParseJYFormat(strToken, out sVoltage, out sCurrent, out sTemp, out sAccM, out sDate, out iNumSerial);
				}
				else if (myHeader.strEquip.ToUpper().IndexOf("JF") != -1)
				{
					iNumSerial = 0;
					ftmp = 0.001F;		//JINFAN is in A/V/Ahr format
					ParseJFFormat(strToken, out sVoltage, out sCurrent, out sTemp, out sAccM, out sDate, out iNumSerial);
				}
#region /*
				/*
				if (!int.TryParse(strToken[(int)O2TXTRecord.TxtChgDsg], out iMinus))
				{
					if (strToken[(int)O2TXTRecord.TxtChgDsg].ToUpper().Equals("ACTION"))
					{
						//raw data start for next line
						continue;
					}
					else
					{
						continue;
					}
				}
				sDate = new string(strToken[(int)O2TXTRecord.TxtTime].ToCharArray());
				if (iMinus == 1)
				{
					sCurrent = "-";
				}
				else //if (iMinus == 2 or 0)
				{
					sCurrent = "";
				}

				sCurrent += strToken[(int)O2TXTRecord.TxtCurrent];
				sVoltage = new string(strToken[(int)O2TXTRecord.TxtVoltage].ToCharArray());
				sTemp = new string(strToken[(int)O2TXTRecord.TxtTemperature].ToCharArray());
				sAccM = new string(strToken[(int)O2TXTRecord.TxtAccMah].ToCharArray());
*/			
#endregion
				//if ((sVoltage != null) && (sCurrent != null) && (sTemp != null) && (sAccM != null) && (sDate != null))
				if ((sVoltage.Length != 0) && (sCurrent.Length != 0) && 
					(sTemp.Length != 0) &&  (sAccM.Length !=0) && (sDate.Length != 0))
				{
					RawDataNode nodeN = new RawDataNode(iNumSerial.ToString(), sVoltage, sCurrent, sTemp, sAccM, sDate, ftmp);
					TableRawData.Add(nodeN);
					iNumCol += 1;		//(A140702)Francis, as guoyan request, check total number
					//if (!UInt32.TryParse(strToken[(int)O2TXTRecord.TxtSerial], out iNumSerial))
					//{
						//iNumSerial = 0;			//(A140702)Francis, as guoyan request, check total number
						//break;
					//}
					//else

					#region				//(A140715)Francis, as Guoyan request, spec_07_11 define, add voltage check;
					fNowVolt = nodeN.fVoltage;
					fNowCurr = nodeN.fCurrent;
					if (fLastVolt == -1F)
					{
						fLastVolt = nodeN.fVoltage;
						fLastCurr = nodeN.fCurrent;
						iNumSerStart = iNumSerial;
						iNumSerLast = iNumSerial;
					}
					else
					{
						if (iNumSerial > iNumSerLast)
						{
							if (Math.Abs(iNumSerial - iNumSerLast) <= fErrorStep)
							{
								if (Math.Abs(iNumSerial - iNumSerLast) == 1)
								{
									iNumSerLast = iNumSerial;
								}
								else if (Math.Abs(fLastVolt - fNowVolt) <= fErrorStep)
								{
									iNumSub += (int)(Math.Abs(iNumSerial - iNumSerStart) - 1);
									iNumSerLast = iNumSerial;
								}
								else
								{
									//idle change to chg/dsg, or chg/dsg change to idle, it's OK data, or keeping idle
									if (((Math.Abs(fLastCurr) <= fErrorStep) && (Math.Abs(fNowCurr - fLastCurr) >= fErrorStep * 20)) ||
										((Math.Abs(fLastCurr) >= fErrorStep*20) && (Math.Abs(fNowCurr) <= fErrorStep)) ||
										((Math.Abs(fLastCurr) <= fErrorStep) && (Math.Abs(fNowCurr) <= fErrorStep)))
									{
										iNumSub += (int)(Math.Abs(iNumSerial - iNumSerStart) - 1);
										iNumSerLast = iNumSerial;
									}
									else
									{
										LibErrorCode.fVal01 = fLastVolt;
										LibErrorCode.fVal02 = fNowVolt;
										uErr = LibErrorCode.IDS_ERR_TMK_SD_VOLTAGE_JUMP;
									}
								}
							}
							else
							{
								LibErrorCode.uVal01 = iNumSerLast;
								LibErrorCode.uVal02 = iNumSerial;
								uErr = LibErrorCode.IDS_ERR_TMK_SD_NUMBER_JUMP;
							}
						}
						else
						{
							LibErrorCode.uVal01 = iNumSerLast;
							LibErrorCode.uVal02 = iNumSerial;
							uErr = LibErrorCode.IDS_ERR_TMK_SD_NUMBER_BACK;
						}
					}
					//(E140715)
					#endregion

					#region					//(A140715)Francis, as Guoyan spec_07_11 request, add invalid current check
					//(A140708)Francis, as leon request, get voltage high/low boundry
					if (fNowVolt > fMaxExpVolt) fMaxExpVolt = fNowVolt;//set maximum voltage value from raw data
					if (fNowVolt < fMinExpVolt) fMinExpVolt = fNowVolt;
					//(E140708)
/*
					ftmp = Math.Abs(fNowCurr - fLastCurr);
					if (fNowVolt >= (myHeader.fLimitChgVolt - fErrorStep))	//if reaching chage_full
					{
						if (fNowCurr > 0)	//if charging
						{
							if (!bReachChgFull)
							{
								bReachChgFull = true;	//charge to full
								bInitailStart = true;
							}
						}
						else if (fNowCurr < 0)		//if discharging
						{
							if (bReachChgFull)
							{
								bReachChgFull = false;	//reset
							}
						}
						else //idle
						{
							if (ftmp > fErrorStep)
							{
								iErrCount += 1;
								//uErr = LibErrorCode.IDS_ERR_TMK_SD_NOT_CONTINUE;
							}
						}
					}
					else if (fNowVolt <= (myHeader.fCutoffDsgVolt + fErrorStep))	//if reaching dischage_empty
					{
						if (fNowCurr < 0)	//if charging
						{
							if (!bReachDsgEmpty)
							{
								bReachDsgEmpty = true;	//discharge to empty
								bInitailStart = true;
							}
						}
						else if (fNowCurr > 0)		//if charging
						{
							if (bReachDsgEmpty)
							{
								bReachDsgEmpty = false;	//reset
							}
						}
						else //idle
						{
						}
					}
					else		//voltage is between Max/Min interval
					{
						if (ftmp > (fErrorStep * 10))
						{
							if (bInitailStart)
							{
								bInitailStart = false;		//first time that from idle to CHG/DSG action
							}
							else
							{
								if ((!bReachChgFull) || (!bReachDsgEmpty))
								{
									//uErr = LibErrorCode.IDS_ERR_TMK_SD_NOT_CONTINUE;
									iErrCount += 1;
								}
							}
						}
					}
*/
					//(E140715)
					#endregion

					fLastVolt = fNowVolt;		//save current as last
					fLastCurr = fNowCurr;
					sVoltage.Remove(0, sVoltage.Length);
					sCurrent.Remove(0, sCurrent.Length);
					sTemp.Remove(0, sTemp.Length);
					sAccM.Remove(0, sAccM.Length);
					sDate.Remove(0, sDate.Length);
				}
				else
				{
					UInt32.TryParse((iNumCol + 1).ToString(), out LibErrorCode.uVal01);
					if (sVoltage.Length == 0)
					{
						LibErrorCode.strVal02 = sVoltage;
						uErr = LibErrorCode.IDS_ERR_TMK_SD_VOLTAGE_READ;
					}
					else if (sCurrent.Length == 0)
					{
						LibErrorCode.strVal02 = sCurrent;
						uErr = LibErrorCode.IDS_ERR_TMK_SD_CURRENT_READ;
					}
					else if (sTemp.Length == 0)
					{
						LibErrorCode.strVal02 = sTemp;
						uErr = LibErrorCode.IDS_ERR_TMK_SD_TEMPERATURE_READ;
					}
					else if (sAccM.Length == 0)
					{
						LibErrorCode.strVal02 = sAccM;
						uErr = LibErrorCode.IDS_ERR_TMK_SD_ACCUMULATED_READ;
					}
					else if (sDate.Length == 0)
					{
						LibErrorCode.strVal02 = sDate;
						uErr = LibErrorCode.IDS_ERR_TMK_SD_DATE_READ;
					}
				}
			}

			stmContent.Close();
			if (uErr == LibErrorCode.IDS_ERR_SUCCESSFUL)
			{
				if (TableRawData.Count != 0)
				{
					iNumCol -= iNumSub;		//(A140715)Francis, 
					if (iNumCol == (iNumSerial - iNumSerStart + 1))		//(A140702)Francis, as guoyan request, check total number
					{
						bReturn = true;
					}
					else
					{
						LibErrorCode.uVal01 = (UInt32)iNumCol;
						LibErrorCode.uVal02 = (iNumSerial - iNumSerStart + 1);
						LibErrorCode.strVal01 = strSourceFilePath;
						uErr = LibErrorCode.IDS_ERR_TMK_SD_NUMBER_MATCH;
						bReturn = false;
					}
				}
			}
			else
			{
				bReturn = false;
			}

			return bReturn;
		}

		private void ParseJYFormat(string[] inToken, out string outVolt, out string outCurr, out string outTemp, out string outAccm, out string outDate, out UInt32 outSerial)
		{
			int iMinus = 0;

			outVolt = "";
			outCurr = "";
			outTemp = "";
			outAccm = "";
			outDate = "";
			outSerial = 0;
			if (!int.TryParse(inToken[(int)O2TXTRecord.TxtChgDsg], out iMinus))
			{
				return;
			}
			outDate = new string(inToken[(int)O2TXTRecord.TxtTime].ToCharArray());
			if (iMinus == 1)
			{
				outCurr = "-";
			}
			else //if (iMinus == 2 or 0)
			{
				outCurr = "";
			}
			outCurr += inToken[(int)O2TXTRecord.TxtCurrent];
			outVolt = new string(inToken[(int)O2TXTRecord.TxtVoltage].ToCharArray());
			outTemp = new string(inToken[(int)O2TXTRecord.TxtTemperature].ToCharArray());
			outAccm = new string(inToken[(int)O2TXTRecord.TxtAccMah].ToCharArray());
			if (!UInt32.TryParse(inToken[(int)O2TXTRecord.TxtSerial], out outSerial))
			{
				outSerial = 0;			//(A140702)Francis, as guoyan request, check total number
			}
		}

		private void ParseJFFormat(string[] inToken, out string outVolt, out string outCurr, out string outTemp, out string outAccm, out string outDate,out UInt32 outSerial)
		{
			//int iMinus = 0;
			string strtmp;

			outVolt = "";
			outCurr = "";
			outTemp = "";
			outAccm = "";
			outDate = "";
			outSerial = 0;
			strtmp = inToken[(int)O2JINFRecord.JFChgDsg];
			if (strtmp.ToUpper().IndexOf("DISCHARGE") != -1)
			{
				outCurr = "-";
			}
			else //if charge or idle
			{
				outCurr = "";
			}
			outCurr += inToken[(int)O2JINFRecord.JFCurrent];
			outVolt = new string(inToken[(int)O2JINFRecord.JFVoltage].ToCharArray());
			outTemp = "-99.9".ToString(); //new string(inToken[(int)O2TXTRecord.TxtTemperature].ToCharArray());
			outAccm = new string(inToken[(int)O2JINFRecord.JFAccMah].ToCharArray());
			outDate = new string(inToken[(int)O2JINFRecord.JFTime].ToCharArray());
		}

        //(A170612)Francis, add for Chroma new format
        private void ParseChroFormat(string[] inToken, out string outVolt, out string outCurr, out string outTemp, out string outAccm, out string outDate, out UInt32 outSerial)
        {
            string sStepCtlg = "";
            int iMinus = 0;

            outVolt = "";
            outCurr = "";
            outTemp = "";
            outAccm = "";
            outDate = "";
            outSerial = 0;

            sStepCtlg = inToken[(int)O2ChromaRecord.ChChgDsg]; 
            if (sStepCtlg.ToLower().IndexOf("rest") == -1)
            {
                if(sStepCtlg.ToLower().IndexOf("discharge") != -1)
                {
                    iMinus = -1;
                }
                else
                {
                    iMinus = 1;
                }
            }
            outDate = new string(inToken[(int)O2ChromaRecord.ChTime].ToCharArray());
            outCurr = inToken[(int)O2ChromaRecord.ChCurrent];
            if (iMinus == -1)
            {
                if(outCurr.IndexOf("-") == -1)
                {
                    outCurr = "";
                    return;
                }
            }
            else if(iMinus == 1)    //if (iMinus == 1)
            {
                if (outCurr.IndexOf("-") != -1)
                {
                    outCurr = "";
                    return;
                }
            }
            else { }                //if (iMinus == 0)
            outVolt = new string(inToken[(int)O2ChromaRecord.ChVoltage].ToCharArray());
            outTemp = new string(inToken[(int)O2ChromaRecord.ChTemperature].ToCharArray());
            outAccm = new string(inToken[(int)O2ChromaRecord.ChAccMah].ToCharArray());
            if (!UInt32.TryParse(inToken[(int)O2ChromaRecord.ChSerial], out outSerial))
            {
                outSerial = 0;			//(A140702)Francis, as guoyan request, check total number
            }
            else
            {
                outSerial = outSerial / 1000;
            }
        }
        //(E170612)

		private void PrepareSoCXls()
		{
			//string tempstring;
			//tempstring = "SoC" + myHeader.strTestTime.Replace("-", "").Substring(0, 8) + "\\";
			//tempstring = System.IO.Path.Combine(strOutputFolder, tempstring);

			//if (!Directory.Exists(tempstring))
			//{
			//Directory.CreateDirectory(tempstring);
			//}

			string strTimetmp = myHeader.strTestTime;

			strTimetmp = strTimetmp.Replace(":", "-");
			strTimetmp = strTimetmp.Replace("/", "-");
			strTimetmp = strTimetmp.Replace("\\", "-");
			//SoC file name is as definition
			strSoCFileName += myHeader.strManufacture + "_" +
				myHeader.strBatteryModel + "_" +
				myHeader.fTemperature.ToString() + "DegC_" +
				myHeader.fCurrent.ToString() + "mA_" +
				//myHeader.strTestTime.Replace(":", "-") + ".csv";
				strTimetmp + ".csv";

			strSocRowHeader = string.Format("No.,\t\t" + "Cell (mV), \t\t" + "Soc");
		}

		/*  Currently not support DB file
				// <summary>
				// Opening *.db file of raw data, read its content and save voltage/current/temperature/time/AccMah 
				// according to JinFan raw data format. 
				// Note that in there is no full record 
				// </summary>
				// <returns>True, if voltage/current/temperature/time/AccMah are all OK; otherwise, return false</returns>
				private bool OpenDBFile(string RawDB)
				{
					bool bReturn = true;
					string str0_s, str1_s, str2_s, str3_s, str4_s;

					SQLiteConnection myconn = new SQLiteConnection("Data Source=" + RawDB);
					myconn.Open();
					try
					{
						String strSQL = "select * from Record";
						SQLiteCommand mycom = myconn.CreateCommand();
						mycom.CommandText = strSQL;
						SQLiteDataReader reader = mycom.ExecuteReader();
						while (reader.Read())
						{
							str0_s = reader.GetValue((int)O2DBRecord.RecVoltage).ToString();
							str1_s = reader.GetValue((int)O2DBRecord.RecCurrent).ToString();
							str2_s = reader.GetValue((int)O2DBRecord.RecTemperature).ToString();
							str3_s = reader.GetValue((int)O2DBRecord.RecAccMah).ToString();
							str4_s = reader.GetValue((int)O2DBRecord.RecTime).ToString();
							RawDataNode nodeN = new RawDataNode(str0_s, str1_s, str2_s, str3_s, str4_s);
							TableRawData.Add(nodeN);
						}
					}
					catch (Exception ex)
					{
						bReturn = false;
						Console.WriteLine(ex);
					}
					finally
					{
						myconn.Close();
					}
					return bReturn;
				}
		*/

		//(A20141114)Francis, add new error API
		public void CreateNewError(UInt32 inSerialNumber, float inVoltage,  UInt32 inErrorCode)
		{
			TableError newError = new TableError(strSourceFilePath, inSerialNumber, inVoltage, myHeader.fCurrent, myHeader.fTemperature, inErrorCode);
			srcError.Add(newError);
		}
	}

	public abstract class TableInterface
	{
        public static string strFalconLY = "FalconLY";

		public enum VersionEnum : ushort
		{
			VerEnmOCV = 0x01,
			VerEnmRC = 0x02,
			VerEnmCHG = 0x04,
			VerEnmTable = 0x08,
		}

		public List<SourceDataSample> TableSourceData;
		public List<UInt32> TableVoltagePoints;             //voltage points from user input
		public TypeEnum TableType;
		public SourceDataHeader TableSourceHeader = null;	//for conveint usage
		public string TableOutputFolder = null;		//note that, in theory, it's same forlder no matter RC or OCV table making
		public string strTester = "";
		public string strBatteryID = "";			//(A140729)Francis
		public Int32 iMinVoltage = 99999;
		public Int32 iMaxVoltage = -99999;
		public  AndroidDriverSample myAndroidDriver = null;

		//(A141023)Francis, for version control
		public string TableConfigFile = Path.Combine(FolderMap.m_root_folder, "Settings\\TMConfig.xml");
		public UInt32 uOCVVer = 0;
		public UInt32 uRCVer = 0;
		public UInt32 uCHGVer = 0;
        public UInt32 uTableVer = 0;
		public string strUserVersion = "";
		public string strUserDate = "";
		public string strUserComment = "";
		public string strTableVersion = "";

        //(A170302)Francis, for VTR/TR table calculation,
        #region public constant definition; \\t, \\r\\n ...etc character definition, used in OCV table, RC table
        public string sFileSeperator = "_";
        public string sCommentTab = "\t";
        public string sValueSeperator = ",";
        public string sChangeLine = "\r\n";
        public string sComment = "//";
        #endregion

        //FalconOCVFileName
        public string strFalconOCVFileName { get; set; }   //(A170308)Francis, file name of falconly used file 
        //for saving OCV value, copy(move)from OCVSample class
        public List<Int32> iOCVVolt = new List<Int32>();            //save voltage value in OCVbyTSOC, V
        public List<Int32> iSOCVolt = new List<Int32>();            //save voltage value in TSOCbyOCV
        public List<Int32> iSOCBit = new List<Int32>();             //save bit value in TSOCbyOCV
        public List<Int32> uOCVPercentPoints = new List<Int32>();   //save percentage value from OCVbyTSOC, V
        //public List<UInt32> uOCVPercentPoints;                    //save percentage points of OCV table
        //for saving RC value, copy(move) from RCSample class
        public List<float> listfTemp = new List<float>();		    //save all temp value of raw data, in 'C format, V
        public List<float> listfCurr = new List<float>();			//save all curr value of raw data, in mA, minus is discharge, V
        public List<List<Int32>> iYPointsall = new List<List<Int32>>();     //,V
        public List<UInt32> uRCVoltagePoints;                       //save voltage points of RC, V
        //for saving VTR/TR
        public List<string> strOCVHeader = new List<string>();
        public List<string> strOCVContent = new List<string>();
        public List<string> strRCHeader = new List<string>();
        public List<string> strRCContent = new List<string>();
        public List<string> strFalconLYOCVContent = new List<string>();
        public List<Int32> ilistVTRPoints = new List<Int32>();
        public List<Int32> ilistTRPoints = new List<Int32>();
        public SourceDataHeader SourceHeader2nd = null;
        //(E170302)

		private XmlElement xmlEmtRoot;
		private XmlDocument xmlConfig = new XmlDocument();
		private bool bConfigOK = true;
		//(E141023)
		public bool bBuildOK = false;	//(A141120)Francis, to make BuildTable() API had been called and worked properly

		public List<TableError> tInsError = null;		//(A20141117)Francis

        public bool bVTRboth = true;    //(A170228)Francis, save from user input for VTR and TR table of new Gas Gauge algorithm
        #region TR table content string
        private string strTRFileName { get; set; }
        private List<string> strTRHeader = new List<string>();
        private List<string> strTRContent = new List<string>();
        #endregion

		public TableInterface()
		{
			//Stream fsconfig = null;

			tInsError = new List<TableError>();
			tInsError.Clear();

			if (!File.Exists(TableConfigFile))	//TableMaker config xml file is not existing
			{
				try
				{
					#region create a new TableMaker config XML
					xmlConfig.AppendChild(xmlConfig.CreateXmlDeclaration("1.0", "UTF-8", "yes"));
					xmlEmtRoot = xmlConfig.CreateElement("Root");
					xmlConfig.AppendChild(xmlEmtRoot);
					XmlElement xeOCV = xmlConfig.CreateElement("Config");
					XmlAttribute xaOCVType = xmlConfig.CreateAttribute("Type");
					xaOCVType.Value = "OCV";
					xeOCV.Attributes.Append(xaOCVType);
					xmlEmtRoot.AppendChild(xeOCV);
					XmlElement xeOCVVer = xmlConfig.CreateElement("Version");
					xeOCVVer.InnerText = uOCVVer.ToString();
					xeOCV.AppendChild(xeOCVVer);

					XmlElement xeRC = xmlConfig.CreateElement("Config");
					XmlAttribute xaRCType = xmlConfig.CreateAttribute("Type");
					xaRCType.Value = "RC";
					xeRC.Attributes.Append(xaRCType);
					xmlEmtRoot.AppendChild(xeRC);
					XmlElement xeRCVer = xmlConfig.CreateElement("Version");
					xeRCVer.InnerText = uRCVer.ToString();
					xeRC.AppendChild(xeRCVer);

					XmlElement xeCHG = xmlConfig.CreateElement("Config");
					XmlAttribute xaCHGType = xmlConfig.CreateAttribute("Type");
					xaCHGType.Value = "CHG";
					xeCHG.Attributes.Append(xaCHGType);
					xmlEmtRoot.AppendChild(xeCHG);
					XmlElement xeCHGVer = xmlConfig.CreateElement("Version");
					xeCHGVer.InnerText = uCHGVer.ToString();
					xeCHG.AppendChild(xeCHGVer);
					xmlConfig.Save(TableConfigFile);

					XmlElement xeTable = xmlConfig.CreateElement("Config");
					XmlAttribute xaTableType = xmlConfig.CreateAttribute("Type");
					xaTableType.Value = "Table";
					xeTable.Attributes.Append(xaTableType);
					xmlEmtRoot.AppendChild(xeTable);
					XmlElement xeTableVer = xmlConfig.CreateElement("Version");
					xeTableVer.InnerText = uTableVer.ToString();
					xeTable.AppendChild(xeTableVer);
					xmlConfig.Save(TableConfigFile);
					#endregion
				}
				catch (Exception ef)
				{
					//should not happen
					bConfigOK = false;
					CreateNewErrorLog(TableConfigFile, UInt32.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, LibErrorCode.IDS_ERR_TMK_TBL_CONFIG_NO_EXIT);
				}
			}
			else
			{
				#region read TableMaker Config
				xmlConfig.Load(TableConfigFile);
				xmlEmtRoot = xmlConfig.DocumentElement;
				XmlNode xnOCV = xmlEmtRoot.SelectSingleNode("//Config[@Type= 'OCV']/Version");
				string strOCVV = xnOCV.InnerText;
				if (!UInt32.TryParse(strOCVV, out uOCVVer))
				{
					uOCVVer = 0;
				}
				XmlNode xnRC = xmlEmtRoot.SelectSingleNode("//Config[@Type= 'RC']/Version");
				string strRCV = xnRC.InnerText;
				if (!UInt32.TryParse(strRCV, out uRCVer))
				{
					uRCVer = 0;
				}
				XmlNode xnCHG = xmlEmtRoot.SelectSingleNode("//Config[@Type= 'CHG']/Version");
				string strCHGV = xnCHG.InnerText;
				if (!UInt32.TryParse(strCHGV, out uCHGVer))
				{
					uCHGVer = 0;
				}
				XmlNode xnTable = xmlEmtRoot.SelectSingleNode("//Config[@Type= 'Table']/Version");
				string strTableV = xnTable.InnerText;
                if (!UInt32.TryParse(strTableV, out uTableVer))
				{
					uTableVer = 0;
				}
				#endregion
			}
			//do pre-adding 1
			uOCVVer += 1;
			uRCVer += 1;
			uCHGVer += 1;
			uTableVer += 1;
			//TbsErrorLog = new List<TableError>();
		}

		public void PrepareUserInput(UInt32 uTargetV, List<string> mkParamString)
		{
			int j = Enum.GetNames(typeof(MakeParamEnum)).Length;
			for (int i = 0; i < j; i++)
			{
				switch (i)
				{
					case ((int)MakeParamEnum.MakeVersion):
					{
                        UInt32 uOvF;
						if (uTargetV > 255)
                            uOvF = (UInt32)((uTargetV & 0xFF00) >> 8);
						else
							uOvF = 0;
						strUserVersion = string.Format("V{0:X2}.{1:X2}.{2}", uOvF, (byte)uTargetV, mkParamString[i]);
						if (uTableVer > 255)
                            uOvF = (UInt32)((uTableVer & 0xFF00) >> 8);
						else
							uOvF = 0;
						strTableVersion = string.Format("V{0:X2}.{1:X2}.{2}", uOvF, (byte)uTableVer, mkParamString[i]);
						break;
					}
					case ((int)MakeParamEnum.MakeDate):
					{
						strUserDate = string.Format("{0}", mkParamString[i]);
						break;
					}
					case ((int)MakeParamEnum.MakeComment):
					{
						strUserComment = string.Format("{0}", mkParamString[i]);
						break;
					}
				}
			}
		}

		//+1 is done after finished reading from xml.
		public void Record4Versions(VersionEnum vnSh)
		{
			if ((vnSh & VersionEnum.VerEnmOCV) != 0)
			{
				XmlNode xnOCV = xmlEmtRoot.SelectSingleNode("//Config[@Type= 'OCV']/Version");
				xnOCV.InnerText = uOCVVer.ToString();
				uOCVVer += 1;
			}
			if ((vnSh & VersionEnum.VerEnmRC) != 0)
			{
				XmlNode xnRC = xmlEmtRoot.SelectSingleNode("//Config[@Type= 'RC']/Version");
				xnRC.InnerText = uRCVer.ToString();
				uRCVer += 1;
			}
			if ((vnSh & VersionEnum.VerEnmCHG) != 0)
			{
				XmlNode xnCHG = xmlEmtRoot.SelectSingleNode("//Config[@Type= 'CHG']/Version");
				xnCHG.InnerText = uCHGVer.ToString();
				uCHGVer += 1;
			}
			if ((vnSh & VersionEnum.VerEnmTable) != 0)
			{
				XmlNode xnTable = xmlEmtRoot.SelectSingleNode("//Config[@Type= 'Table']/Version");
				xnTable.InnerText = uTableVer.ToString();
				uTableVer += 1;
			}
			xmlConfig.Save(TableConfigFile);
		}

		~TableInterface()
		{
			//xmlConfig.Save(TableConfigFile);
		}
		//(E141023)

		public abstract bool InitializeTable(List<UInt32> inVoltPoints, ref UInt32 uErr, string strOutputFolder = null);
		public abstract bool BuildTable(ref UInt32 uErr, List<string> mkParamString);
		public abstract bool GenerateFile(ref UInt32 uErr);		//creating a file will have error??

		public void AddSourceData(SourceDataSample inSourceData)
		{
			TableSourceData.Add(inSourceData);
			if (iMinVoltage > inSourceData.fMinExpVolt)
			{
				iMinVoltage = (int)(inSourceData.fMinExpVolt+0.5F);
			}
			if (iMaxVoltage < inSourceData.fMaxExpVolt)
			{
				iMaxVoltage = (int)inSourceData.fMaxExpVolt;
			}
			//foreach (TableError tbsdd in inSourceData.srcError)
			//{
				//tInsError.Add(tbsdd);
			//}
		}

		public void AddVoltagePoints(List<UInt32> inVoltPoints)
		{
			TableVoltagePoints = inVoltPoints;
		}

		public bool GetExpermentVoltBoundry(ref UInt32 uLowVolt, ref UInt32 uHighVolt)
		{
			bool bReturn = false;

			uLowVolt = 0;
			uHighVolt = 0;
			if ((iMinVoltage != 99999) && (iMaxVoltage != -99999))
			{
				bReturn = UInt32.TryParse(iMinVoltage.ToString(), out uLowVolt);
				bReturn &= UInt32.TryParse(iMaxVoltage.ToString(), out uHighVolt);
			}

			return bReturn;
		}

		public bool InitializeAndroidDriver(ref UInt32 uErr, TableInterface tblCaller)
		{
			bool bReturn = true;

			if (myAndroidDriver == null)
			{
				List<string> ltmp = new List<string>();
				ltmp.Add(strTableVersion);
				ltmp.Add(strUserDate);
				ltmp.Add(strUserComment);
				myAndroidDriver = new AndroidDriverSample(TableType, TableSourceData, ltmp, tblCaller);
			}

			bReturn &= myAndroidDriver.InitializeHeaderInfor(ref uErr, ref tInsError);
			if (bReturn)
			{
				myAndroidDriver.InitializedTableH();
				myAndroidDriver.InitializedTableC();
			}

			return bReturn;
		}

		public void CreateNewErrorLog(string iFilePath, UInt32 iSerialNumb, float iVoltage, float iCurrent, float iTemperature, UInt32 iErrorCode)
		{
			TableError newErrLog = new TableError(iFilePath, iSerialNumb, iVoltage, iCurrent, iTemperature, iErrorCode);
			tInsError.Add(newErrLog);
		}

		public TypeEnum GetTableType()
		{
			return TableType;
		}

		/*
		public void AddErrorIntoLog(string inFilePath, UInt32 inSerialNumber, float inVoltage, float inCurrent, float inTemperature, UInt32 inErrorCode)
		{
			//TableError newError = new TableError(inFilePath, inSerialNumber, inVoltage, inCurrent, inTemperature, inErrorCode);
			
			TbsErrorLog.Add(newError);
		}

		public void GetRawErrorLog(ref List<TableError> tbse)
		{
			tbse = TbsErrorLog;
		}
		*/

		/* no need, cause if create new one instance of TableInterface, it will auto create a new TbsErrorLog
		public void ClearRawErrorLog()
		{
		}
		*/

        //(A170228)Francis, add for VTR and TR table for new Gas Gauge algorithm
        public void InitializeOCVFalconLYTable(string strOCVFalconLYOutFolder)
        {
            //(A170308)Francis, falconly use file output folder
            strFalconOCVFileName = "OCV" + sFileSeperator + TableSourceHeader.strManufacture +
                sFileSeperator + TableSourceHeader.strBatteryModel +
                sFileSeperator + TableSourceHeader.fFullCapacity.ToString() + "mAhr" +
                //sFileSeperator + wHighBound.ToString() + "mV" +
                //sFileSeperator + wLowBound.ToString() + "mV" +
                //sFileSeperator + iMaxVoltage.ToString() + "mV" +
                //sFileSeperator + iMinVoltage.ToString() + "mV" +
                sFileSeperator + TableSourceHeader.strLimitChgVolt + "mV" +
                sFileSeperator + TableSourceHeader.strCutoffDsgVolt + "mV" +
                //sFileSeperator + "V003" +
                sFileSeperator + TableMaker.TableSample.strTBMVersion +
                //sFileSeperator + batinfo.strVersion +
                sFileSeperator + DateTime.Now.Year.ToString("D4") +
                DateTime.Now.Month.ToString("D2") + DateTime.Now.Day.ToString("D2") +
                "_FalconLY.txt";
            strFalconOCVFileName = System.IO.Path.Combine(strOCVFalconLYOutFolder, strFalconOCVFileName);
            //(E170308)

        }

        public void ConvertOCVFalconLYContent(List<Int32> inputData)
        {
            float fStep = 0;
            float fTemp = 0;
            Int32 iTemp = 0;
            string strXt;
            string OCVXValuesTmp;

            //(A170308)Francis,
            strFalconLYOCVContent.Clear();
            strFalconLYOCVContent.Add(string.Format(""));
            strFalconLYOCVContent.Add(string.Format("//table header"));
            strFalconLYOCVContent.Add(string.Format(""));
            strFalconLYOCVContent.Add(string.Format("6 \t\t //DO NOT CHANGE: word length of header(including this length)"));
            strFalconLYOCVContent.Add(string.Format("1 \t\t //DO NOT CHANGE: control, use as scale control "));
            strFalconLYOCVContent.Add(string.Format("1 \t\t //DO NOT CHANGE: number of axis"));
            //strFalconLYOCVContent.Add(string.Format("{0} \t\t //x axis points: maximum 65 points", TableVoltagePoints.Count));
            strFalconLYOCVContent.Add(string.Format("{0} \t\t //x axis points: maximum 65 points", inputData.Count));
            strFalconLYOCVContent.Add(string.Format("1 \t\t //DO NOT CHANGE: y axis entries per x axis"));
            strFalconLYOCVContent.Add(string.Format("{0} \t\t //DO NOT CHANGE: total length in points", inputData.Count * 2 + 6));
            strFalconLYOCVContent.Add(string.Format(""));
            strFalconLYOCVContent.Add(string.Format("//x (independent) axis: low cell open circuit millivolts:"));
            strFalconLYOCVContent.Add(string.Format("// (this is the cell voltage read after 24 hours \"rest\": no charge or discharge)"));
            strFalconLYOCVContent.Add(string.Format("//must be in increasing order: need not be evenly spaced"));
            strFalconLYOCVContent.Add(string.Format(""));
            strXt = "";
            foreach (Int32 idata in inputData)
            {
                strXt += string.Format("{0}, ", idata);
            }
            //(A140917)Francis, bugid=15206, delete last comma ','
            strXt = strXt.Substring(0, strXt.Length - 2);
            //(E140917)
            strFalconLYOCVContent.Add(strXt);
            strFalconLYOCVContent.Add(string.Format(""));
            strFalconLYOCVContent.Add(string.Format(""));
            strFalconLYOCVContent.Add(string.Format(""));
            strFalconLYOCVContent.Add(string.Format("//y (dependent) axis: 10000 * full capacity (at .02C or less) for above voltages "));
            strFalconLYOCVContent.Add(string.Format(""));
            fStep = (float)(OCVSample.iMaxPercent - OCVSample.iMinPercent) /
                    (float)(OCVSample.iNumOfPoints - 1);
            //strXt = "";
            OCVXValuesTmp = "";

            iTemp = 0;
            fTemp = 0F;
            for (int i = 0; i < OCVSample.iNumOfPoints; i++)
            {
                //iTemp = Int32.Parse((fStep * i).ToString());
                OCVXValuesTmp += string.Format("{0:D5}, ", iTemp);
                fTemp += (float)(fStep);
                iTemp = Convert.ToInt32(Math.Round(fTemp, 0));
            }
            //(A140917)Francis, bugid=15206, delete last comma ','
            OCVXValuesTmp = OCVXValuesTmp.Substring(0, OCVXValuesTmp.Length - 2);
            //(E140917)
            strFalconLYOCVContent.Add(OCVXValuesTmp);
            //(E170308)
        }

        public bool GenerateOCVFalconLYTableFile(ref UInt32 uErr)
        {
            //int iline = 0;
            bool bReturn = false;
            FileStream fsOCV = null;
            StreamWriter stmOCV = null;

            try
            {
                fsOCV = File.Open(strFalconOCVFileName, FileMode.Create, FileAccess.Write, FileShare.None);
                stmOCV = new StreamWriter(fsOCV, Encoding.Unicode);
            }
            catch (Exception ec)
            {
                LibErrorCode.strVal01 = strFalconOCVFileName;
                uErr = LibErrorCode.IDS_ERR_TMK_OCV_CREATE_FILE;
                CreateNewErrorLog(TableSourceData[0].strSourceFilePath, UInt32.MaxValue, float.MaxValue, TableSourceData[0].myHeader.fCurrent, TableSourceData[0].myHeader.fTemperature, uErr);
                return bReturn;
            }

            foreach (string stocvh in strOCVHeader)
            {
                stmOCV.WriteLine(stocvh);
            }
            foreach (string stocvc in strFalconLYOCVContent)
            {
                stmOCV.WriteLine(stocvc);
            }

            stmOCV.Close();
            fsOCV.Close();

            bReturn = true;

            return bReturn;
        }

        public bool ParseHeaderInforFromFileComments(List<string> strHeaderCommentsFrom, bool bSet2nd = true)
        {
            bool bReturn = true;
            string strTmp = "";

            if (SourceHeader2nd == null)
                SourceHeader2nd = new SourceDataHeader();

            foreach (string strLine in strHeaderCommentsFrom)
            {
                if(strLine.IndexOf("//Manufacturer") != -1)
                {
                    strTmp = strLine.Substring(strLine.IndexOf("= ") + 2);
                    if(!bSet2nd)
                    {
                        TableSourceHeader.strManufacture = strTmp;
                    }
                    else
                    {
                        SourceHeader2nd.strManufacture = strTmp;
                    }
                }
                else if(strLine.IndexOf("//Battery Type") != -1)
                {
                    strTmp = strLine.Substring(strLine.IndexOf("= ") + 2);
                    if (!bSet2nd)
                    {
                        TableSourceHeader.strBatteryModel = strTmp;
                    }
                    else
                    {
                        SourceHeader2nd.strBatteryModel = strTmp;
                    }
                }
                else if(strLine.IndexOf("//Equipment") != -1)
                {
                    strTmp = strLine.Substring(strLine.IndexOf("= ") + 2);
                    if (!bSet2nd)
                    {
                        TableSourceHeader.strEquip = strTmp;
                    }
                    else
                    {
                        SourceHeader2nd.strEquip = strTmp;
                    }
                }
                else if(strLine.IndexOf("//MinimalVoltage") != -1)
                {
                    strTmp = strLine.Substring(strLine.IndexOf("= ") + 2);
                    if (!bSet2nd)
                    {
                        TableSourceHeader.strCutoffDsgVolt = strTmp;
                    }
                    else
                    {
                        SourceHeader2nd.strCutoffDsgVolt = strTmp;
                    }
                }
                else if(strLine.IndexOf("//MaximalVoltage") != -1)
                {
                    strTmp = strLine.Substring(strLine.IndexOf("= ") + 2);
                    if (!bSet2nd)
                    {
                        TableSourceHeader.strLimitChgVolt = strTmp;
                    }
                    else
                    {
                        SourceHeader2nd.strLimitChgVolt = strTmp;
                    }
                }
                else if(strLine.IndexOf("//FullAbsoluteCapacity") != -1)
                {
                    strTmp = strLine.Substring(strLine.IndexOf("= ") + 2);
                    if (!bSet2nd)
                    {
                        TableSourceHeader.strAbsMaxCap = strTmp;
                        strTmp = strTmp.Replace("mAhr", "");
                        if (!float.TryParse(strTmp, out TableSourceHeader.fFullCapacity))
                        {
                            TableSourceHeader.fFullCapacity = 1;
                        }
                    }
                    else
                    {
                        SourceHeader2nd.strAbsMaxCap = strTmp;
                        strTmp = strTmp.Replace("mAhr", "");
                        if (!float.TryParse(strTmp, out SourceHeader2nd.fFullCapacity))
                        {
                            SourceHeader2nd.fFullCapacity = 1;
                        }
                    }
                }
                else if(strLine.IndexOf("//Age") != -1)
                {
                    strTmp = strLine.Substring(strLine.IndexOf("= ") + 2);
                    if (!bSet2nd)
                    {
                        TableSourceHeader.strCycleCount = strTmp;
                    }
                    else
                    {
                        SourceHeader2nd.strCycleCount = strTmp;
                    }
                }
                else if(strLine.IndexOf("//Tester") != -1)
                {
                    strTmp = strLine.Substring(strLine.IndexOf("= ") + 2);
                    if (!bSet2nd)
                    {
                        TableSourceHeader.strTester = strTmp;
                    }
                    else
                    {
                        SourceHeader2nd.strTester = strTmp;
                    }
                }
                else if(strLine.IndexOf("//Battery ID") != -1)
                {
                    strTmp = strLine.Substring(strLine.IndexOf("= ") + 2);
                    if (strBatteryID.Length < 1)
                        strBatteryID = string.Format("{0}", strTmp);
                    else
                        strBatteryID += string.Format(", {0}", strTmp);
                    if (!bSet2nd)
                    {
                        TableSourceHeader.strBatteryID = strTmp;
                    }
                    else
                    {
                        SourceHeader2nd.strBatteryID = strTmp;
                    }
                }
                else if(strLine.IndexOf("//Comment") != -1)
                {
                    strTmp = strLine.Substring(strLine.IndexOf("= ") + 2);
                    if (strUserComment.Length < 1)
                        strUserComment = string.Format("{0}", strTmp);
                    else
                        strUserComment += string.Format(", {0}", strTmp);
                }
            }

            return bReturn;
        }

        public bool ParseCommentInforFromCHFiles(List<string> strCommentsFromCH)
        {

            bool bReturn = true;
            string strTmp = "";

            //if (SourceHeader2nd == null)
                //SourceHeader2nd = new SourceDataHeader();

            foreach (string strLine in strCommentsFromCH)
            {
                if (strLine.IndexOf("Battery Manufacture:") != -1)
                {
                    strTmp = strLine.Substring(strLine.IndexOf(": ") + 2);
                    TableSourceHeader.strManufacture = strTmp;
                }
                else if (strLine.IndexOf("Battery Model:") != -1)
                {
                    strTmp = strLine.Substring(strLine.IndexOf(": ") + 2);
                    TableSourceHeader.strBatteryModel = strTmp;
                }
                else if (strLine.IndexOf("Equipment:") != -1)
                {
                    strTmp = strLine.Substring(strLine.IndexOf(": ") + 2);
                    strTmp = strTmp.Replace("\"", "");
                    TableSourceHeader.strEquip = strTmp;
                }
                else if (strLine.IndexOf("Cutoff Discharge Voltage") != -1)
                {
                    strTmp = strLine.Substring(strLine.IndexOf(": ") + 2);
                    TableSourceHeader.strCutoffDsgVolt = strTmp;
                }
                else if (strLine.IndexOf("Limited Charge Voltage") != -1)
                {
                    strTmp = strLine.Substring(strLine.IndexOf(": ") + 2);
                    TableSourceHeader.strLimitChgVolt = strTmp;
                }
                else if (strLine.IndexOf("Absolute Max Capacity") != -1)
                {
                    strTmp = strLine.Substring(strLine.IndexOf(": ") + 2);
                    TableSourceHeader.strAbsMaxCap = strTmp;
                    strTmp = strTmp.Replace("mAhr", "");
                    if (!float.TryParse(strTmp, out TableSourceHeader.fFullCapacity))
                    {
                        TableSourceHeader.fFullCapacity = 1;
                    }
                }
                //else if (strLine.IndexOf("//Age") != -1)
                //{
                    //strTmp = strLine.Substring(strLine.IndexOf("= ") + 2);
                    //if (!bSet2nd)
                    //{
                        //TableSourceHeader.strCycleCount = strTmp;
                    //}
                    //else
                    //{
                        //SourceHeader2nd.strCycleCount = strTmp;
                    //}
                //}
                else if (strLine.IndexOf("Tester:") != -1)
                {
                    strTmp = strLine.Substring(strLine.IndexOf(": ") + 2);
                    strTmp = strTmp.Replace("\"", "");
                    TableSourceHeader.strTester = strTmp;
                }
                else if (strLine.IndexOf("Battery ID:") != -1)
                {
                    strTmp = strLine.Substring(strLine.IndexOf(": ") + 2);
                    strTmp = strTmp.Replace("\"", "");
                    if (strBatteryID.Length < 1)
                        strBatteryID = string.Format("{0}", strTmp);
                    else
                        strBatteryID += string.Format(", {0}", strTmp);
                    TableSourceHeader.strBatteryID = strTmp;
                }
                else if (strLine.IndexOf("Comment") != -1)
                {
                    strTmp = strLine.Substring(strLine.IndexOf("= ") + 2);
                    if (strUserComment.Length < 1)
                        strUserComment = string.Format("{0}", strTmp);
                    else
                        strUserComment += string.Format(", {0}", strTmp);
                }
            }

            //no age information in C/H file, compensate it
            TableSourceHeader.strCycleCount = "1";
            SourceHeader2nd = TableSourceHeader;

            return bReturn;
        }

        public bool ConvertVTRandTR()
        {
            bool bReturn = true;

            float ftmp = 0F, fOCV1 = 0F;
            List<List<float>> VTRTmp = new List<List<float>>();
            List<Int32> listTmp;
            int iCurrMedian = 0;

            ilistVTRPoints.Clear();
            ilistTRPoints.Clear();
            for (int i = 0; i < VTRTmp.Count; i++ )
            {
                VTRTmp[i].Clear();
            }
            VTRTmp.Clear();
            for (int it = 0; it < listfTemp.Count; it++)
            {
                //strRCContent.Add(string.Format("//temp = {0} ^C", listfTemp[it]));
                for (int ic = 0; ic < listfCurr.Count; ic++)
                {
                    //strrctmp = "";
                    //ConvertRCDataToString(ref strrctmp, rcYval[it * listfCurr.Count + ic]);
                    //strRCContent.Add(strrctmp);
                    listTmp = iYPointsall[it * listfCurr.Count + ic];
                    List<float> VTRoneline = new List<float>();
                    for (int iv = 0; iv < uRCVoltagePoints.Count; iv++)
                    {
                        ftmp = listTmp[iv];
                        GetOCVfromOCVTablebySoCRc1(out fOCV1, ftmp);//100);
                        CalculateVTRfactor(out ftmp, fOCV1, uRCVoltagePoints[iv], listfCurr[ic]);
                        VTRoneline.Add(ftmp);
                    }
                    //VTRoneline.Sort();                      //do we need this? just in case
                    VTRTmp.Add(VTRoneline);
                }
                //strRCContent.Add(string.Format(""));
            }
            iCurrMedian = (int)(listfCurr.Count / 2);
            for (int it2 = 0; it2 < listfTemp.Count; it2++)
            {
                for (int iv2 = 0; iv2 < uRCVoltagePoints.Count; iv2++)
                {
                    //calculate median value
                    if ((listfCurr.Count % 2) == 0)
                    {
                        ftmp = VTRTmp[it2 * listfCurr.Count + iCurrMedian-1][iv2];
                        ftmp += VTRTmp[it2 * listfCurr.Count + iCurrMedian][iv2];
                        ftmp /= 2;
                    }
                    else
                    {
                        iCurrMedian += 1;
                        ftmp = VTRTmp[it2 * listfCurr.Count + iCurrMedian-1][iv2];
                    }
                    ilistVTRPoints.Add(Convert.ToInt32(Math.Round(ftmp, 0)));
                }
            }
            for(int it3 = 0; it3<listfTemp.Count; it3++)
            {
                ftmp = 0F;
                for (int iv3 = 0; iv3 < uRCVoltagePoints.Count; iv3++)
                {
                    ftmp += ilistVTRPoints[it3 * uRCVoltagePoints.Count + iv3];
                }
                ilistTRPoints.Add(Convert.ToInt32(Math.Round(ftmp / uRCVoltagePoints.Count, 0)));
            }

            return bReturn;
        }

        public void GetOCVfromOCVTablebySoCRc1(out float fOCVResult, float fSoCRC)
        {
            //int index1, index2;
            float ftmp = 0;

            for (int i = 0; i < uOCVPercentPoints.Count; i++)
            {
                if((i == 0) && (fSoCRC <= uOCVPercentPoints[i]))
                {
                    ftmp = iOCVVolt[i];
                }
                else
                {
                    if(fSoCRC == uOCVPercentPoints[i])
                    {
                        ftmp = iOCVVolt[i];
                    }
                    else
                    {
                        if(fSoCRC < uOCVPercentPoints[i])
                        {
                            ftmp = fSoCRC - uOCVPercentPoints[i - 1];
                            ftmp /= (uOCVPercentPoints[i] - uOCVPercentPoints[i - 1]);
                            ftmp *= (iOCVVolt[i] - iOCVVolt[i - 1]);
                            ftmp += iOCVVolt[i - 1];
                            
                            break;
                        }
                    }
                }
            }
            fOCVResult = ftmp;
        }

        public void CalculateVTRfactor(out float fVTRResult, float fOCV1, float fCellVolt, float fCurr)
        {
            fVTRResult = (fOCV1 - fCellVolt) / Math.Abs(fCurr) * 10000;
            if (fVTRResult <= 0F)
                fVTRResult = 0F;
        }
        //(E170228)

        //(A170309)Francis, handle TR table txt file output, header comment and content
        public void InitializeTRTable()
        {
            strTRFileName = "TR" + sFileSeperator + TableSourceHeader.strManufacture +
                            sFileSeperator + TableSourceHeader.strBatteryModel +
                            sFileSeperator + TableSourceHeader.fFullCapacity.ToString() + "mAhr" +
                            sFileSeperator + TableSourceHeader.strLimitChgVolt + "mV" +
                            sFileSeperator + TableSourceHeader.strCutoffDsgVolt + "mV" + 
                            sFileSeperator + TableMaker.TableSample.strTBMVersion +
                            sFileSeperator + DateTime.Now.Year.ToString("D4") + 
                            DateTime.Now.Month.ToString("D2") + DateTime.Now.Day.ToString("D2") +
                            ".txt";
            strTRFileName = System.IO.Path.Combine(System.IO.Path.Combine(TableOutputFolder, strTRFileName));

            strTRHeader.Clear();
            strTRHeader.Add(string.Format("//[Description]"));
            strTRHeader.Add(string.Format("// TR as a function of cell capacity"));
            strTRHeader.Add(string.Format("// This table is used during discharging mode, to determine remaining cell capacity in "));
            strTRHeader.Add(string.Format("// particular condition, based on cell voltage, discharging current and cell temperature."));
            strTRHeader.Add(string.Format(""));
            strTRHeader.Add(string.Format("// Please note that the cell must in discharging mode and  voltage is under table "));
            strTRHeader.Add(string.Format("// definition range, otherwise it will be considerable in error. "));
            strTRHeader.Add(string.Format(""));
            strTRHeader.Add(string.Format("//Table Header Information:"));
            strTRHeader.Add(string.Format(""));
            if(TableSourceHeader.strManufacture.Equals(SourceHeader2nd.strManufacture, StringComparison.OrdinalIgnoreCase))
                strTRHeader.Add(string.Format("//Manufacturer = {0}", TableSourceHeader.strManufacture));
            else
                strTRHeader.Add(string.Format("//Manufacturer = {0}, {1}", TableSourceHeader.strManufacture, SourceHeader2nd.strManufacture));
            if (TableSourceHeader.strBatteryModel.Equals(SourceHeader2nd.strBatteryModel, StringComparison.OrdinalIgnoreCase))
                strTRHeader.Add(string.Format("//Battery Type = {0}", TableSourceHeader.strBatteryModel));
            else
                strTRHeader.Add(string.Format("//Battery Type = {0}, {1}", TableSourceHeader.strBatteryModel, SourceHeader2nd.strBatteryModel));
            if (TableSourceHeader.strEquip.Equals(SourceHeader2nd.strEquip, StringComparison.OrdinalIgnoreCase))
                strTRHeader.Add(string.Format("//Equipment = {0}", TableSourceHeader.strEquip));
            else
                strTRHeader.Add(string.Format("//Equipment = {0}, {1}", TableSourceHeader.strEquip, SourceHeader2nd.strEquip));
            strTRHeader.Add(string.Format("//Built Date = {0} {1} {2}", DateTime.Now.Year.ToString("D4"), DateTime.Now.Month.ToString("D2"), DateTime.Now.Day.ToString("D2")));
            if (TableSourceHeader.strCutoffDsgVolt.Equals(SourceHeader2nd.strCutoffDsgVolt, StringComparison.OrdinalIgnoreCase))
                strTRHeader.Add(string.Format("//MinimalVoltage = {0}", TableSourceHeader.strCutoffDsgVolt));
            else
                strTRHeader.Add(string.Format("//MinimalVoltage = {0}, {1}", TableSourceHeader.strCutoffDsgVolt, SourceHeader2nd.strCutoffDsgVolt));
            if (TableSourceHeader.strLimitChgVolt.Equals(SourceHeader2nd.strLimitChgVolt, StringComparison.OrdinalIgnoreCase))
                strTRHeader.Add(string.Format("//MaximalVoltage = {0}", TableSourceHeader.strLimitChgVolt));
            else
                strTRHeader.Add(string.Format("//MaximalVoltage = {0}, {1}", TableSourceHeader.strLimitChgVolt, SourceHeader2nd.strLimitChgVolt));
            if (TableSourceHeader.strAbsMaxCap.Equals(SourceHeader2nd.strAbsMaxCap, StringComparison.OrdinalIgnoreCase))
                strTRHeader.Add(string.Format("//FullAbsoluteCapacity = {0}", TableSourceHeader.strAbsMaxCap));
            else
                strTRHeader.Add(string.Format("//FullAbsoluteCapacity = {0}, {1}", TableSourceHeader.strAbsMaxCap, SourceHeader2nd.strAbsMaxCap));
            if (TableSourceHeader.strCycleCount.Equals(SourceHeader2nd.strCycleCount, StringComparison.OrdinalIgnoreCase))
                strTRHeader.Add(string.Format("//Age = {0}", TableSourceHeader.strCycleCount));
            else
                strTRHeader.Add(string.Format("//Age = {0}, {1}", TableSourceHeader.strCycleCount, SourceHeader2nd.strCycleCount));
            strTRHeader.Add(string.Format("//Tester = {0}", strTester));
            strTRHeader.Add(string.Format("//Battery ID = {0}", strBatteryID));
            //(A141024)Francis, add Version/Date/Comment 3 string into header comment of txt file
            strTRHeader.Add(string.Format("//Version = {0}", strUserVersion));
            strTRHeader.Add(string.Format("//Date = {0}", strUserDate));
            //strOCVHeader.Add(string.Format("//Comment = {0}", strUserComment));
            strTRHeader.Add(string.Format("//Comment = "));
            string[] var = strUserComment.Split('\n');
            foreach (string str in var)
            {
                strTRHeader.Add(string.Format("//          {0}", str.Replace('\r', ' ')));
            }
            //(E141024)
            strTRHeader.Add(string.Format(""));

        }

        public void ConvertTRvalueToString()
        {
            string strXt = "";

            strTRContent.Clear();
            strTRContent.Add(string.Format(""));
            strTRContent.Add(string.Format("//Table 1 header: TR factor lookup"));
            strTRContent.Add(string.Format("6 \t\t //DO NOT CHANGE: word length of header(including this length)"));
            strTRContent.Add(string.Format("0 \t\t //DO NOT CHANGE: control, use as scale control "));
            strTRContent.Add(string.Format("1 \t\t //DO NOT CHANGE: number of axis"));
            strTRContent.Add(string.Format("{0} \t\t //x axis points: maximum 65 points", ilistTRPoints.Count));
            strTRContent.Add(string.Format("1 \t\t //DO NOT CHANGE: y axis entries per x axis"));
            strTRContent.Add(string.Format("{0} \t\t //DO NOT CHANGE: total length in points", ilistTRPoints.Count * 2 + 6));
            strTRContent.Add(string.Format(""));
            strTRContent.Add(string.Format("//[Data]"));
            strTRContent.Add(string.Format("//'x' axis: temperature in 0.1 degrees C (decicentigrade)"));
            strXt = "";
            foreach(float ft in listfTemp)
            {
                strXt += string.Format("{0}, ", Convert.ToInt32(Math.Round(ft, 0) * 10));
            }
            strXt = strXt.Substring(0, strXt.Length - 2);   //delete last comma ','
            strTRContent.Add(strXt);
            strTRContent.Add(string.Format(""));
            strTRContent.Add(string.Format("//'y' axis: resistor factor in (10mohm)"));
            strXt = "";
            foreach(Int32 ir in ilistTRPoints)
            {
                strXt += string.Format("{0}, ", ir);
            }
            strXt = strXt.Substring(0, strXt.Length - 2);   //delete last comma ','
            strTRContent.Add(strXt);
        }

        public bool GenerateTRTableFile(ref UInt32 uErr, string strOutputFolder)
        {
            bool bReturn = false;
            FileStream fsTR = null;
            StreamWriter stmTR = null;

            InitializeTRTable();
            ConvertTRvalueToString();

            try
            {
                fsTR = File.Open(strTRFileName, FileMode.Create, FileAccess.Write, FileShare.None);
                stmTR = new StreamWriter(fsTR, Encoding.Unicode);
            }
            catch (Exception ec)
            {
                LibErrorCode.strVal01 = strTRFileName;
                uErr = LibErrorCode.IDS_ERR_TMK_TR_CREATE_FILE;
                CreateNewErrorLog(TableSourceData[0].strSourceFilePath, UInt32.MaxValue, float.MaxValue, TableSourceData[0].myHeader.fCurrent, TableSourceData[0].myHeader.fTemperature, uErr);
                return bReturn;
            }
            foreach(string strtrh in strTRHeader)
            {
                stmTR.WriteLine(strtrh);
            }
            foreach(string strtrc in strTRContent)
            {
                stmTR.WriteLine(strtrc);
            }

            stmTR.Close();
            fsTR.Close();
            uErr = LibErrorCode.IDS_ERR_SUCCESSFUL;
            bReturn = true;

            return bReturn;
        }
        //(E170228)
 
    }

	public class OCVSample : TableInterface
	{
		#region static public constant definition; OCVbyTSOC X/Y points definition
		static public int iNumOfPoints = 65;
		//static public int iInterval = 64;
		static public int iMinPercent = 0;
		static public int iMaxPercent = 10000;
		static public float fPerSteps = 1.5625F;
		static public int iSOCStepmV = 16;
		#endregion

		#region private member definition; string saving content to write into file

		#region OCVtyTSOC table content string
		private string strOCVFileName { get; set; }
//		private string strOCVHeader01 { get; set; }
//		private string strOCVHeader02 { get; set; }
//		private string strOCVHeader03 { get; set; }
//		private string strOCVHeader04 { get; set; }
//		private string strOCVHeader05 { get; set; }
//		private string strOCVHeader06 { get; set; }
//		private string strOCVHeader07 { get; set; }
//		private string strOCVHeader08 { get; set; }
//		private string strOCVHeader09 { get; set; }
//		private string strOCVTableHeader { get; set; }

//		private int iOCVHeaderNum = 5;
//		private string strOCVHeaderComment { get; set; }
//		private int iOCVControl = 1;
//		private string strOCVControlComment { get; set; }
//		private int iOCVAxii = 1;
//		private string strOCVAxiiComment { get; set; }
//		private int iOCVXPoints = OCVSample.iNumOfPoints;// = 65;
//		private string strOCVXPointComment { get; set; }
//		private int iOCVYPoints = 1;
//		private string strOCVYPointComment { get; set; }

//		private string strOCVXLine01 { get; set; }
//		private string strOCVXLine02 { get; set; }
//		private string strOCVXLine03 { get; set; }
		private string OCVXValues = ",,,,,,";

//		private string strOCVYLine01 { get; set; }
//		private string OCVYValues = ",,,,,,";
		#endregion

		#region TSOCbyOCV table content string
		private string strSOCFileName { get; set; }
//		private string strSOCHeader01 { get; set; }
//		private string strSOCHeader02 { get; set; }
//		private string strSOCHeader03 { get; set; }
//		private string strSOCHeader04 { get; set; }
//		private string strSOCHeader05 { get; set; }
//		private string strSOCHeader06 { get; set; }
//		private string strSOCHeader07 { get; set; }
//		private string strSOCHeader08 { get; set; }
//		private string strSOCHeader09 { get; set; }
//		private string strSOCTableHeader { get; set; }
		private List<string> strSOCHeader = new List<string>();
		private List<string> strSOCContent = new List<string>();

//		private int iSOCHeaderNum = 5;
//		private string strSOCHeaderComment { get; set; }
//		private int iSOCControl = 1;
//		private string strSOCControlComment { get; set; }
//		private int iSOCAxii = 1;
//		private string strSOCAxiiComment { get; set; }
		private int iSOCXPoints = 87;
//		private string strSOCXPointComment { get; set; }
//		private int iSOCYPoints = 1;
//		private string strSOCYPointComment { get; set; }

//		private string strSOCXLine01 { get; set; }
//		private string strSOCXLine02 { get; set; }
//		private string strSOCXLine03 { get; set; }
//		private string SOCXValues = ",,,,,,";

//		private string strSOCYLine01 { get; set; }
//		private string SOCYValues = ",,,,,,";
		#endregion

		#endregion

		#region public members definition, List to save value
        //(D170302)Francis, move to base class,TableInterface
		//public List<Int32> iOCVVolt = new List<Int32>();
		///public List<Int32> iSOCVolt = new List<Int32>();
		//public List<Int32> iSOCBit = new List<Int32>();
		#endregion

		public OCVSample()
		{
			TableSourceData = new List<SourceDataSample>();
			TableSourceData.Clear();
			TableVoltagePoints = new List<UInt32>();
			TableVoltagePoints.Clear();
			TableType = TypeEnum.OCVRawType;
			TableSourceHeader = null;
			TableOutputFolder = "";
		}

		public override bool InitializeTable(List<UInt32> uVoltPoints, ref UInt32 uErr, string strOutputFolder = null)
		{
			bool bReturn = false;

			if (TableSourceData.Count == 0)
			{
				uErr = LibErrorCode.IDS_ERR_TMK_OCV_SOURCE_EMPTY;
				CreateNewErrorLog(" ", UInt32.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, uErr);
			}
			//(M141107)Francis, OCV table is coming from 2 source file, request by Guoyan
			//else if (TableSourceData.Count == 1)	//do we allow 1 source file to create OCV table? TBD, temporarily support 1 OCV SourceData file
			//{
				//uErr = LibErrorCode.IDS_ERR_TMK_OCV_SOURCE_MANY;
			//}
			else if (TableSourceData.Count > 2)
			{
				uErr = LibErrorCode.IDS_ERR_TMK_OCV_SOURCE_MANY;
				foreach (SourceDataSample sdssi in TableSourceData)
				{
					CreateNewErrorLog(sdssi.strSourceFilePath, UInt32.MaxValue, float.MaxValue, sdssi.myHeader.fCurrent, sdssi.myHeader.fTemperature, uErr);
				}
			}
			//(E141107)
			else if (uVoltPoints.Count != 2)
			{
				uErr = LibErrorCode.IDS_ERR_TMK_OCV_VOLTAGE_MANY;
				CreateNewErrorLog(" ", UInt32.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, uErr);
			}
			else
			{	//source file is 1 or 2, and VoltagePoints is 2, OK to continue
				TableVoltagePoints = uVoltPoints;
				TableVoltagePoints.Sort();
				if ((iMinVoltage < TableVoltagePoints[0]) && (TableVoltagePoints[0] >0))
				{
					iMinVoltage = Convert.ToInt32(TableVoltagePoints[0]);
				}
				if ((iMaxVoltage > TableVoltagePoints[1]) && (TableVoltagePoints[1] > TableVoltagePoints[0]))
				{
					iMaxVoltage = Convert.ToInt32(TableVoltagePoints[1]);
				}
				//(D140717)Francis, remove due to Guoyan request for new format of source data, and 
				//ReservedExpData() already be filled up when parsing log data
				//bReturn = TableSourceData[0].ReserveExpPoints(ref uErr, TableVoltagePoints[0]);
				bReturn = true;
				//(E140717)

				//if (bReturn)
				//{
					//copy Output Folder
					TableSourceHeader = TableSourceData[0].myHeader;
					if ((strOutputFolder != null) && (Directory.Exists(strOutputFolder)))
					{
						TableOutputFolder = strOutputFolder;
					}
					else
					{
						TableOutputFolder = TableSourceData[0].strOutputFolder;
					}

					//(D141024)Francis, due to some comments are added when user is pressing 'Make' button. 
					// Move Initialize FileName and Comment content to BuildTable() funciton
					//InitializeOCVbyTSOCTable();
					//InitializeTSOCbyOCVTable();
					//bReturn = InitializeAndroidDriver(ref uErr);	//(A140722)Francis, support Android Driver
					//(E141024)
					//copy Tester and BatteryID string
					strTester = TableSourceHeader.strTester;
					strBatteryID = TableSourceHeader.strBatteryID;		//(A140729)Francis, add battery id
				//}
				//else
				//{
					//Error code has beed assigned above
				//}
			}	//every check

			return bReturn;
		}

		public override bool BuildTable(ref UInt32 uErr, List<string> mkParamString)
		{
			bool bReturn = false;

			//(A141024)Francis, due to some comments are added when user is pressing 'Make' button. 
			// Move Initialize FileName and Comment content to BuildTable() funciton
			PrepareUserInput(uOCVVer, mkParamString);
			InitializeOCVbyTSOCTable();
			InitializeTSOCbyOCVTable();
			bReturn = InitializeAndroidDriver(ref uErr, this);	//(A140722)Francis, support Android Driver, basically return true, only return false when creating(opening) file
			//if (!bReturn)
				//return bReturn;
			//(E141024)

			//(M140724)Francis, move created SOC/OCV points to public members of class, could be re-used in Android Driver
			iOCVVolt.Clear();
			iSOCVolt.Clear();
			iSOCBit.Clear();
			//(M141107)Francis, 2 source files to generate OCV table, request from Guoyan
			if (TableSourceData.Count == 1)
			{
				TableSourceData[0].CreateSoCXls(TableOutputFolder, ref uErr);
				//if (CreateOCVPoints(ref iResult, TableSourceData[0].AdjustedExpData, ref uErr))
				//if (CreateOCVPoints(ref iOCVVolt, TableSourceData[0].AdjustedExpData, ref uErr))
				bReturn &= CreateOCVPoints(ref iOCVVolt, TableSourceData[0].AdjustedExpData, ref uErr);
				{
					//if (GenerateOCVbyTSOCTable(iResult))	//basically, there will no FALSE be returned
					//if (GenerateOCVbyTSOCTable(iOCVVolt, ref uErr))	//basically, there will no FALSE be returned
					{
						//iResult.Clear();
						//if (CreateTSOCPoints(ref iResult, ref iSOC, TableSourceData[0].AdjustedExpData, ref uErr))			//(M140702)Francis, according to guoyan calculation, voltage will be adjusted
						//if (CreateTSOCPoints(ref iSOCVolt, ref iSOCBit, TableSourceData[0].AdjustedExpData, ref uErr))			//(M140702)Francis, according to guoyan calculation, voltage will be adjusted
						bReturn &= CreateTSOCPoints(ref iSOCVolt, ref iSOCBit, TableSourceData[0].AdjustedExpData, ref uErr);
						{
							//(M141120)Francis, seperate create data points and generate txt file into 2 API
							//bReturn = true;
							/*
							if (GenerateTSOCbyOCVTable(iSOCVolt, iSOCBit, ref uErr))	//basically, there will no FALSE be returned
							{
								Record4Versions(VersionEnum.VerEnmOCV);
								myAndroidDriver.MakeOCVContent(OCVXValues, iOCVVolt);
								bReturn = myAndroidDriver.MakeDriver(ref uErr, TableOutputFolder);
								if (bReturn)
								{
									//uErr = LibErrorCode.IDS_ERR_SUCCESSFUL;
									//(A141024)Francis, check bMakeTable value, if true, means Android Driver C/H file are created
									if (myAndroidDriver.GetMakeTableStatus())
									{
										Record4Versions(VersionEnum.VerEnmTable);
									}
									//(E141024)
								}
							}	//if (GenerateTSOCbyOCVTable(iSOCVolt, iSOCBit, ref uErr))	//basically, there will no FALSE be returned
							*/
						}	//if (CreateTSOCPoints(ref iSOCVolt, ref iSOCBit, TableSourceData[0].AdjustedExpData, ref uErr))
					}	//if (GenerateOCVbyTSOCTable(iOCVVolt, ref uErr))	//basically, there will no FALSE be returned
				}	//if (CreateOCVPoints(ref iOCVVolt, TableSourceData[0].AdjustedExpData, ref uErr))
			}	//if(TableSourceData.Count == 1)
			else
			{	// here is TableSourceData.Count == 2
				TableSourceData[0].CreateSoCXls(TableOutputFolder, ref uErr);
				TableSourceData[1].CreateSoCXls(TableOutputFolder, ref uErr);
				//if (CreateNewOCVPoints(TableSourceData, ref uErr))
				bReturn &= CreateNewOCVPoints(TableSourceData, ref uErr);
				{
					//sort all value from low to high
					iOCVVolt.Sort();
					iSOCVolt.Sort();
					iSOCBit.Sort();
					//(M141120)Francis, seperate create data points and generate txt file into 2 API
					//bReturn = true;
					/*
					if (GenerateOCVbyTSOCTable(iOCVVolt, ref uErr))	//basically, there will no FALSE be returned
					{
						if (GenerateTSOCbyOCVTable(iSOCVolt, iSOCBit, ref uErr))	//basically, there will no FALSE be returned
						{
							Record4Versions(VersionEnum.VerEnmOCV);
							myAndroidDriver.MakeOCVContent(OCVXValues, iOCVVolt);
							bReturn = myAndroidDriver.MakeDriver(ref uErr, TableOutputFolder);
							if (bReturn)
							{
								//uErr = LibErrorCode.IDS_ERR_SUCCESSFUL;
								//(A141024)Francis, check bMakeTable value, if true, means Android Driver C/H file are created
								if (myAndroidDriver.GetMakeTableStatus())
								{
									Record4Versions(VersionEnum.VerEnmTable);
								}
								//(E141024)
							}
						}	//if (GenerateTSOCbyOCVTable(iSOCVolt, iSOCBit, ref uErr))	//basically, there will no FALSE be returned
					}	//if (GenerateOCVbyTSOCTable(iOCVVolt, ref uErr))	//basically, there will no FALSE be returned
					*/
				}	//if (CreateNewOCVPoints(TableSourceData, ref uErr))
			}	//if (TableSourceData.Count == 1)

			//(M141120)make bBuildOK
			//if (bReturn)
				bBuildOK = true;

			return bReturn;
		}

		public override bool GenerateFile(ref uint uErr)
		{
			bool bReturn = false;

			//(M141120)Francis, if true, make bBuildOK
			if (!bBuildOK)
			{
				uErr = LibErrorCode.IDS_ERR_TMK_TBL_BUILD_SEQUENCE;
				CreateNewErrorLog(strOCVFileName, UInt32.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, uErr);
				return bReturn;
			}
			bBuildOK = false;	//set as false, no matter Generate successfully or not
			//(E141120)
			if (GenerateOCVbyTSOCTable(iOCVVolt, ref uErr))	//basically, there will no FALSE be returned
			{
				if (GenerateTSOCbyOCVTable(iSOCVolt, iSOCBit, ref uErr))	//basically, there will no FALSE be returned
				{
					Record4Versions(VersionEnum.VerEnmOCV);
					myAndroidDriver.MakeOCVContent(OCVXValues, iOCVVolt);
                    if(bVTRboth)
                    {
                        GenerateOCVbyTSOCTable(iOCVVolt, ref uErr, true);
                    }
                    bReturn = myAndroidDriver.MakeDriver(ref uErr, TableOutputFolder, bVTRboth);	//basically return true, false will only be happened when create(open) file
					if (bReturn)
					{
						//uErr = LibErrorCode.IDS_ERR_SUCCESSFUL;
						//(A141024)Francis, check bMakeTable value, if true, means Android Driver C/H file are created
						if (myAndroidDriver.GetMakeTableStatus())
						{
							Record4Versions(VersionEnum.VerEnmTable);
						}
						//(E141024)
					}
				}	//if (GenerateTSOCbyOCVTable(iSOCVolt, iSOCBit, ref uErr))	//basically, there will no FALSE be returned
			}	//if (GenerateOCVbyTSOCTable(iOCVVolt, ref uErr))	//basically, there will no FALSE be returned

			return bReturn;
		}

		// <summary>
		// Create OCVbyTSOC points, first X points are 1.5625% in 10000 format,  this is not changable
		// second Y points are voltage value according to Coulomb counting of SoC and save <List>ipoints
		// </summary>
		// <param name="ipoints">output, Voltage points value</param>
		// <returns>True: number of ipoints is same as SoC points, otherwise it will return false</returns>
		private bool CreateOCVPoints(ref List<Int32> ipoints, List<RawDataNode> inListRaw, ref UInt32 uErr)
		{
			bool bReturn = false;
			//float fACCStep = OCVSample.fPerSteps * 0.01F * batteryheader.fDesignCapacity;
			float fACCStep = OCVSample.fPerSteps * 0.01F * (TableSourceData[0].myHeader.fFullCapacity - TableSourceData[0].myHeader.fCapacityDiff);
			int i = 0;
			Int32 vpoint = 0;

			//if (bAccTrust)	//means accmah has value
			{
				//vpoint = wHighBound;
				vpoint = iMaxVoltage;
				ipoints.Add(vpoint);
				i = 1;
				//foreach (RawDataNode rds in mySourceData[0].ReservedExpData)
				foreach (RawDataNode rds in inListRaw)
				{
					if (rds.fAccMah < (i * fACCStep))
					{
						vpoint = Convert.ToInt32(Math.Round(rds.fVoltage, 0));
					}
					else
					{
						//if (vpoint > wHighBound)
							//vpoint = (Int32)wHighBound;
						//if (vpoint < wLowBound)
							//vpoint = (Int32)wLowBound;
						if (vpoint > iMaxVoltage)
							vpoint = iMaxVoltage;
						if (vpoint < iMinVoltage)
							vpoint = iMinVoltage;
						ipoints.Add(vpoint);
						i += 1;
					}
					if (i == OCVSample.iNumOfPoints)
					{
						break;
					}
				}
			}

			//(M140812)Francis, according to Guoyan discussion, if points is not match, still generate table
			if (true)
			{
				if ((i + 1) < OCVSample.iNumOfPoints)
				{
					for (; i < OCVSample.iNumOfPoints; i++)
					{
						vpoint = iMinVoltage;
						ipoints.Add(vpoint);
					}
				}
			}
			//else
			{
				if ((i + 1) == OCVSample.iNumOfPoints)
				{	//cannot discharge to DesignCapacity
					//vpoint = wLowBound;
					vpoint = iMinVoltage;
					ipoints.Add(vpoint);
					i += 1;
				}
			}

			if (i == OCVSample.iNumOfPoints)
			{
				ipoints.Sort();
				bReturn = true;
			}
			else
			{
				ipoints.Sort();		//still add table even error happened, by Guoyan request
				LibErrorCode.uVal01 = (UInt32)i;
				LibErrorCode.uVal02 = (UInt32)OCVSample.iNumOfPoints;
				uErr = LibErrorCode.IDS_ERR_TMK_OCV_TSOC_POINT;
				CreateNewErrorLog(TableSourceData[0].strSourceFilePath, UInt32.MaxValue, float.MaxValue, TableSourceData[0].myHeader.fCurrent, TableSourceData[0].myHeader.fTemperature, uErr);
			}

			return bReturn;
		}

		private bool GenerateOCVbyTSOCTable(List<Int32> inputData, ref UInt32 uErr, bool bInsertFalconuse = false)
		{
			bool bReturn = false;
			FileStream fsOCV = null;
			StreamWriter stmOCV = null;

			//if (File.Exists(strFileName))
			//{
			//File.Delete(strFileName);
			//}

			//File.Create(strFileName);
            if (!bInsertFalconuse)
            {
                if (!ConvertOCVYDataToString(inputData))	//normally it will return true default
                {
                    return bReturn;
                }

                try
                {
                    fsOCV = File.Open(strOCVFileName, FileMode.Create, FileAccess.Write, FileShare.None);
                    stmOCV = new StreamWriter(fsOCV, System.Text.Encoding.GetEncoding("big5"));
                }
                catch (Exception ec)
                {
                    LibErrorCode.strVal01 = strOCVFileName;
                    uErr = LibErrorCode.IDS_ERR_TMK_OCV_CREATE_FILE;
                    CreateNewErrorLog(TableSourceData[0].strSourceFilePath, UInt32.MaxValue, float.MaxValue, TableSourceData[0].myHeader.fCurrent, TableSourceData[0].myHeader.fTemperature, uErr);
                    return bReturn;
                }

                foreach (string stocvh in strOCVHeader)
                {
                    stmOCV.WriteLine(stocvh);
                }
                foreach (string stocvc in strOCVContent)
                {
                    stmOCV.WriteLine(stocvc);
                }

                stmOCV.Close();
                fsOCV.Close();
                uErr = LibErrorCode.IDS_ERR_SUCCESSFUL;
                bReturn = true;
            }
            else
            {
                bReturn = GenerateOCVFalconLYTableFile(ref uErr);
                //uErr = LibErrorCode.IDS_ERR_SUCCESSFUL;
            }

//			FileContent = new StreamWriter(strOCVFileName, false, Encoding.Unicode);
//			FileContent.WriteLine(strOCVHeader01);
//			FileContent.WriteLine(strOCVHeader02);
//			FileContent.WriteLine();
//			FileContent.WriteLine(strOCVHeader03);
//			FileContent.WriteLine(strOCVHeader04);
//			FileContent.WriteLine();
//			FileContent.WriteLine(strOCVHeader05);
//			FileContent.WriteLine(strOCVHeader06);
//			FileContent.WriteLine(strOCVHeader07);
//			FileContent.WriteLine(strOCVHeader08);
//			FileContent.WriteLine(strOCVHeader09);
//			FileContent.WriteLine();
//			FileContent.WriteLine();
//			FileContent.WriteLine(strOCVTableHeader);
//			FileContent.WriteLine();
//			FileContent.WriteLine(strOCVHeaderComment);
//			FileContent.WriteLine(strOCVControlComment);
//			FileContent.WriteLine(strOCVAxiiComment);
//			FileContent.WriteLine(strOCVXPointComment);
//			FileContent.WriteLine(strOCVYPointComment);
//			FileContent.WriteLine();
//			FileContent.WriteLine(strOCVXLine01);
//			FileContent.WriteLine(strOCVXLine02);
//			FileContent.WriteLine(strOCVXLine03);
//			FileContent.WriteLine();
//			FileContent.WriteLine(OCVXValues);
//			FileContent.WriteLine();
//			FileContent.WriteLine(strOCVYLine01);
//			FileContent.WriteLine();
//			FileContent.WriteLine(OCVYValues);

//			FileContent.Close();

			//bReturn = true;

			return bReturn;
		}

		private bool ConvertOCVYDataToString(List<Int32> inputData)
		{
			bool bReturn = true;
			float fStep = 0;
			float fTemp = 0;
            Int32 iTemp = 0;
			string strXt;
			//int i=0;

            /*
            if (inputData.Count == OCVSample.iNumOfPoints)
            {
                OCVXValues = "";
                OCVYValues = "";
                fStep = (float)(OCVSample.iMaxPercent - OCVSample.iMinPercent) /
                    (float)(OCVSample.iNumOfPoints - 1);
                foreach (Int32 idata in inputData)
                {
                    //iTemp = Int32.Parse((fStep * i).ToString());
                    strXt = string.Format("{0:D5}", iTemp);
                    OCVXValues += (strXt + sValueSeperator);
                    OCVYValues += (idata.ToString() + sValueSeperator);
                    //i++;
                    fTemp += (float)(fStep);
                    iTemp = (int)(fTemp + 0.5);
                }
                OCVXValues = OCVXValues.Substring(0, OCVXValues.Length - 1);
                OCVYValues = OCVYValues.Substring(0, OCVYValues.Length - 1);
                bReturn = true;
            }
             */

            strOCVContent.Clear();
			strOCVContent.Add(string.Format(""));
			strOCVContent.Add(string.Format("//data header"));
			strOCVContent.Add(string.Format(""));
			strOCVContent.Add(string.Format("5 \t\t //DO NOT CHANGE: word length of header(including this length)"));
			strOCVContent.Add(string.Format("1 \t\t //DO NOT CHANGE: control, use as scale control "));
			strOCVContent.Add(string.Format("1 \t\t //DO NOT CHANGE: number of axis"));
			//strOCVContent.Add(string.Format("{0} \t\t //x axis points: maximum 65 points", TableVoltagePoints.Count));
			strOCVContent.Add(string.Format("{0} \t\t //x axis points: maximum 65 points", inputData.Count));
			strOCVContent.Add(string.Format("1 \t\t //DO NOT CHANGE: y axis entries per x axis"));
			strOCVContent.Add(string.Format(""));
			strOCVContent.Add(string.Format("//x (independent) axis: SOC 0% = 0, 100% = 10000:"));
			strOCVContent.Add(string.Format("// (this is the cell voltage read after 24 hours \"rest\": no charge or discharge)"));
			strOCVContent.Add(string.Format("//must be in increasing order: need not be evenly spaced"));
			strOCVContent.Add(string.Format(""));
			fStep = (float)(OCVSample.iMaxPercent - OCVSample.iMinPercent) /
				(float)(OCVSample.iNumOfPoints - 1);
			//strXt = "";
			OCVXValues = "";

            //(A170228)Francis, copy voltage points of OCV
            uOCVPercentPoints.Clear();      //(A170228)Francis, copy voltage points of OCV
            for (int i = 0; i < OCVSample.iNumOfPoints; i++)
			{
                //iTemp = Int32.Parse((fStep * i).ToString());
				OCVXValues += string.Format("{0:D5}, ", iTemp);
                uOCVPercentPoints.Add(iTemp);       ////(A170228)Francis, copy voltage points of OCV
				fTemp += (float)(fStep);
                iTemp = Convert.ToInt32(Math.Round(fTemp, 0));
			}
            //(E170228)

			//(A140917)Francis, bugid=15206, delete last comma ','
			OCVXValues = OCVXValues.Substring(0, OCVXValues.Length - 2);
			//(E140917)
			strOCVContent.Add(OCVXValues);
			strOCVContent.Add(string.Format(""));
			strOCVContent.Add(string.Format(""));
			strOCVContent.Add(string.Format(""));
			strOCVContent.Add(string.Format("//y (dependent axis: "));
			strOCVContent.Add(string.Format(""));
			strXt = "";
			foreach (Int32 idata in inputData)
			{
				strXt += string.Format("{0}, ", idata);
			}
			//(A140917)Francis, bugid=15206, delete last comma ','
			strXt = strXt.Substring(0, strXt.Length - 2);
			//(E140917)
			strOCVContent.Add(strXt);
			strOCVContent.Add(string.Format(""));

            ConvertOCVFalconLYContent(inputData);

			return bReturn;
		}

		// <summary>
		// Create TSOCbyOCV points, first X points are voltage current from lowbound, and every 16mV each steps
		// second Y points are SoC points present in 32767 format. X points will be saved in ixpo, Y points will be saved
		// in iypo.
		// </summary>
		// <param name="ixpo">output, X points voltage value</param>
		// <param name="iypo">output, Y points SoC 32767 format</param>
		// <returns>True: if number of points are match; otherwise return false</returns>
		private bool CreateTSOCPoints(ref List<Int32> ixpo, ref List<Int32> iypo, List<RawDataNode> inListRaw, ref UInt32 uErr)
		{
			bool bReturn = false;
			int fmulti = (int)(((float)(iMaxVoltage - iMinVoltage)) * 10F / OCVSample.iSOCStepmV);
			int ileft = (int)fmulti % 10;
			//int ftpp = (BattModel.wTSOCPoints - 1) * OCVSample.iSOCStepmV + myOCV.iMinVoltage;
			int ftpp, i, itemp, iSumVolt;
			int iHundred = 32767;
			float fTemp;

			fmulti /= 10;
			ftpp = fmulti * OCVSample.iSOCStepmV + iMinVoltage;

			if ((ileft != 0) || (ftpp != iMaxVoltage))
			{
				if (ftpp < iMaxVoltage)	//should not bigger than iMaxVoltage
				{
					iMaxVoltage = ftpp;
				}
			}

			//if (bAccTrust)	//means accmah has value
			{
				i = 0;
				//foreach (RawDataNode rds in mySourceData[0].ReservedExpData)
				foreach (RawDataNode rds in inListRaw)
				{
					iSumVolt = (iMaxVoltage - (i * OCVSample.iSOCStepmV));
					if (rds.fVoltage <= iSumVolt)
					{
						fTemp = (float)(TableSourceHeader.fFullCapacity - rds.fAccMah) / TableSourceHeader.fFullCapacity;
						fTemp *= (float)iHundred;
                        itemp = Convert.ToInt32(Math.Round(fTemp, 0));
						if (itemp <= 0) itemp = 0;	//keep it positiv, or 0
						ixpo.Add(iSumVolt);
						iypo.Add(itemp);
						i += 1;
					}
					else
					{
						continue;	//find next record
					}
				}
				if (ixpo.Count == fmulti)
				{
					//RawDataNode lastone = mySourceData[0].ReservedExpData[mySourceData[0].ReservedExpData.Count - 1];
					RawDataNode lastone = inListRaw[inListRaw.Count - 1];
					fTemp = (float)(TableSourceHeader.fFullCapacity - lastone.fAccMah) / TableSourceHeader.fFullCapacity;
					fTemp *= (float)iHundred;
                    itemp = Convert.ToInt32(Math.Round(fTemp, 0));
                    ixpo.Add(Convert.ToInt32(Math.Round(lastone.fVoltage, 0)));
					iypo.Add(itemp);
					bReturn = true;
				}
				else if (ixpo.Count == (fmulti + 1))
				{
					bReturn = true;
				}
				else
				{
					LibErrorCode.uVal01 = (UInt32)ixpo.Count;
					LibErrorCode.uVal02 = (UInt32)fmulti;
					uErr = LibErrorCode.IDS_ERR_TMK_OCV_SOC_POINT;
					CreateNewErrorLog(TableSourceData[0].strSourceFilePath, UInt32.MaxValue, float.MaxValue, TableSourceData[0].myHeader.fCurrent, TableSourceData[0].myHeader.fTemperature, uErr);
				}
			}

			//if (bReturn)	//still add table even error happened, by Guoyan request
			{
				ixpo.Sort();
				iypo.Sort();
			}

			return bReturn;
		}

		private bool GenerateTSOCbyOCVTable(List<Int32> inputXData, List<Int32> inputYData, ref UInt32 uErr)
		{
			bool bReturn = false;
			FileStream fssoc = null;
			StreamWriter stmsoc = null;

			if (!ConvertSOCYDataToString(inputXData, inputYData))	//normally it will return true default
			{
				return bReturn;
			}
			try
			{
				fssoc = File.Open(strSOCFileName, FileMode.Create, FileAccess.Write, FileShare.None);
                stmsoc = new StreamWriter(fssoc, System.Text.Encoding.GetEncoding("big5"));
			}
			catch (Exception es)
			{
				LibErrorCode.strVal01 = strOCVFileName;
				uErr = LibErrorCode.IDS_ERR_TMK_OCV_CREATE_FILE;
				CreateNewErrorLog(TableSourceData[0].strSourceFilePath, UInt32.MaxValue, float.MaxValue, TableSourceData[0].myHeader.fCurrent, TableSourceData[0].myHeader.fTemperature, uErr);
				return bReturn;
			}

			foreach (string stsoch in strSOCHeader)
			{
				stmsoc.WriteLine(stsoch);
			}
			foreach (string stsoc in strSOCContent)
			{
				stmsoc.WriteLine(stsoc);
			}
			stmsoc.Close();
			fssoc.Close();
			uErr = LibErrorCode.IDS_ERR_SUCCESSFUL;

			/*
			FileContent = new StreamWriter(strSOCFileName, false, Encoding.Unicode);
			FileContent.WriteLine(strSOCHeader01);
			FileContent.WriteLine(strSOCHeader02);
			FileContent.WriteLine();
			FileContent.WriteLine(strSOCHeader03);
			FileContent.WriteLine(strSOCHeader04);
			FileContent.WriteLine();
			FileContent.WriteLine(strSOCHeader05);
			FileContent.WriteLine(strSOCHeader06);
			FileContent.WriteLine(strSOCHeader07);
			FileContent.WriteLine(strSOCHeader08);
			FileContent.WriteLine(strSOCHeader09);
			FileContent.WriteLine();
			FileContent.WriteLine();
			FileContent.WriteLine(strSOCTableHeader);
			FileContent.WriteLine();
			FileContent.WriteLine(strSOCHeaderComment);
			FileContent.WriteLine(strSOCControlComment);
			FileContent.WriteLine(strSOCAxiiComment);
			strSOCXPointComment = iSOCXPoints.ToString() + sCommentTab + " //x axis points: maximum of 87 points";
			FileContent.WriteLine(strSOCXPointComment);
			FileContent.WriteLine(strSOCYPointComment);
			FileContent.WriteLine();
			FileContent.WriteLine(strSOCXLine01);
			FileContent.WriteLine(strSOCXLine02);
			FileContent.WriteLine(strSOCXLine03);
			FileContent.WriteLine();
			FileContent.WriteLine(SOCXValues);
			FileContent.WriteLine();
			FileContent.WriteLine(strSOCYLine01);
			FileContent.WriteLine();
			FileContent.WriteLine(SOCYValues);

			FileContent.Close();
			*/

			bReturn = true;

			return bReturn;
		}

		private bool ConvertSOCYDataToString(List<Int32> inputXData, List<Int32> inputYData)
		{
			bool bReturn = false;
			string strXt;//, strYt;
			int iStartP = 0;

			if (inputXData.Count > iSOCXPoints) //longer than 87 poinst
			{
				iStartP = inputXData.Count - iSOCXPoints;
			}
			else		//shorter or equal to 87 points
			{
				iSOCXPoints = inputXData.Count;
				iStartP = 0;
			}
			strSOCContent.Clear();
			strSOCContent.Add(string.Format(""));
			strSOCContent.Add(string.Format("//data header"));
			strSOCContent.Add(string.Format(""));
			strSOCContent.Add(string.Format("5 \t\t //DO NOT CHANGE: word length of header(including this length)"));
			strSOCContent.Add(string.Format("1 \t\t //DO NOT CHANGE: control, use as scale control "));
			strSOCContent.Add(string.Format("1 \t\t //DO NOT CHANGE: number of axis"));
			strSOCContent.Add(string.Format("{0} \t\t //x axis points: maximum 87 points", inputXData.Count));
			strSOCContent.Add(string.Format("1 \t\t //DO NOT CHANGE: y axis entries per x axis"));
			strSOCContent.Add(string.Format(""));
			strSOCContent.Add(string.Format("//x (independent) axis: low cell open circuit millivolts: "));
			strSOCContent.Add(string.Format("// (this is the cell voltage read after 24 hours \"rest\": no charge or discharge)"));
			strSOCContent.Add(string.Format("//must be in increasing order: need not be evenly spaced"));
			strSOCContent.Add(string.Format(""));
			strXt = "";
			for (int i = 0; i < iSOCXPoints; i++)
			{
				strXt += string.Format("{0:D4}, ", inputXData[i + iStartP]);
			}
			//(A140917)Francis, bugid=15206, delete last comma ','
			strXt = strXt.Substring(0, strXt.Length - 2);
			//(E140917)
			strSOCContent.Add(strXt);
			strSOCContent.Add(string.Format(""));
			strSOCContent.Add(string.Format(""));
			strSOCContent.Add(string.Format(""));
			strSOCContent.Add(string.Format("//y (dependent axis: "));
			strSOCContent.Add(string.Format(""));
			strXt = "";
			for (int i = 0; i < iSOCXPoints; i++)
			{
				strXt += string.Format("{0:D}, ", inputYData[i + iStartP]);
			}
			//(A140917)Francis, bugid=15206, delete last comma ','
			strXt = strXt.Substring(0, strXt.Length - 2);
			//(E140917)
			strSOCContent.Add(strXt);
			strSOCContent.Add(string.Format(""));

			/*
			SOCXValues = "";
			SOCYValues = "";
			for (int i = 0; i < iSOCXPoints; i++)
			{
				strXt = string.Format("{0:D4}", inputXData[i + iStartP]);
				strYt = string.Format("{0:D}", inputYData[i + iStartP]);
				SOCXValues += (strXt + sValueSeperator);
				SOCYValues += (strYt + sValueSeperator);
			}
			SOCXValues = SOCXValues.Substring(0, SOCXValues.Length - 1);
			SOCYValues = SOCYValues.Substring(0, SOCYValues.Length - 1);
			*/

			bReturn = true;

			return bReturn;
		}

		private void InitializeOCVbyTSOCTable()
		{
			strOCVFileName = "OCVbyTSOC" + sFileSeperator + TableSourceHeader.Line04Content +
				sFileSeperator + TableSourceHeader.Line05Content +
				sFileSeperator + TableSourceHeader.fFullCapacity.ToString() + "mAhr" +
				//sFileSeperator + wHighBound.ToString() + "mV" +
				//sFileSeperator + wLowBound.ToString() + "mV" +
				//sFileSeperator + iMaxVoltage.ToString() + "mV" +
				//sFileSeperator + iMinVoltage.ToString() + "mV" +
				sFileSeperator + TableSourceHeader.strLimitChgVolt + "mV" +
				sFileSeperator + TableSourceHeader.strCutoffDsgVolt + "mV" +
				//sFileSeperator + "V003" +
				sFileSeperator + TableMaker.TableSample.strTBMVersion +
				//sFileSeperator + batinfo.strVersion +
				sFileSeperator + DateTime.Now.Year.ToString("D4") +
				DateTime.Now.Month.ToString("D2") + DateTime.Now.Day.ToString("D2") +
				".txt";
			strOCVFileName = System.IO.Path.Combine(TableOutputFolder, strOCVFileName);

            //(A170317)Francis
            if (!Directory.Exists(System.IO.Path.Combine(TableOutputFolder, TableInterface.strFalconLY)))
            {
                Directory.CreateDirectory(System.IO.Path.Combine(TableOutputFolder, TableInterface.strFalconLY));
            }
            InitializeOCVFalconLYTable(System.IO.Path.Combine(TableOutputFolder, TableInterface.strFalconLY));
            //(E170317)

			strOCVHeader.Clear();
			strOCVHeader.Add(string.Format("//[Description]"));
			strOCVHeader.Add(string.Format("// Open Circuit Voltage as a function of cell capacity"));
			strOCVHeader.Add(string.Format("// This table is used at initial startup only to determine remaining cell capacity as "));
			strOCVHeader.Add(string.Format("// a fraction of full capacity, based on the open curcuit (no load, rested) cell voltage. "));
			strOCVHeader.Add(string.Format(""));
			strOCVHeader.Add(string.Format("// Please note that the cell must not have been charged or discharged for several "));
			strOCVHeader.Add(string.Format("// hours prior to this remaining capacity determination, or remaining capacity may "));
			strOCVHeader.Add(string.Format("// be considerable in error"));
			strOCVHeader.Add(string.Format(""));
			strOCVHeader.Add(string.Format("//Table Header Information:"));
			strOCVHeader.Add(string.Format(""));
			strOCVHeader.Add(string.Format("//Manufacturer = {0}", TableSourceHeader.strManufacture));
			strOCVHeader.Add(string.Format("//Battery Type = {0}", TableSourceHeader.strBatteryModel));
			strOCVHeader.Add(string.Format("//Equipment = {0}", TableSourceHeader.strEquip));
			strOCVHeader.Add(string.Format("//Built Date = {0} {1} {2}", DateTime.Now.Year.ToString("D4"), DateTime.Now.Month.ToString("D2"), DateTime.Now.Day.ToString("D2")));
			strOCVHeader.Add(string.Format("//MinimalVoltage = {0}", TableSourceHeader.strCutoffDsgVolt));
			strOCVHeader.Add(string.Format("//MaximalVoltage = {0}", TableSourceHeader.strLimitChgVolt));
			strOCVHeader.Add(string.Format("//FullAbsoluteCapacity = {0}", TableSourceHeader.strAbsMaxCap));
			strOCVHeader.Add(string.Format("//Age = {0}", TableSourceHeader.strCycleCount));
			strOCVHeader.Add(string.Format("//Tester = {0}", strTester));
			strOCVHeader.Add(string.Format("//Battery ID = {0}", strBatteryID));
			//(A141024)Francis, add Version/Date/Comment 3 string into header comment of txt file
			strOCVHeader.Add(string.Format("//Version = {0}", strUserVersion));
			strOCVHeader.Add(string.Format("//Date = {0}", strUserDate));
			//strOCVHeader.Add(string.Format("//Comment = {0}", strUserComment));
			strOCVHeader.Add(string.Format("//Comment = "));
			string[] var = strUserComment.Split('\n');
			foreach (string str in var)
			{
				strOCVHeader.Add(string.Format("//          {0}", str.Replace('\r', ' ')));
			}
			//(E141024)
			strOCVHeader.Add(string.Format(""));

//			strOCVHeader01 = "// Open Circuit Voltage as a function of cell capacity";
//			strOCVHeader02 = "// for " + TableSourceHeader.Line04Content + " " + TableSourceHeader.Line05Content + " cell ";// +sChangeLine;
//			strOCVHeader03 = "// This table is used at initial startup only to determine remaining cell capacity as ";// + sChangeLine;
//			strOCVHeader04 = "// a fraction of full capacity, based on the open curcuit (no load, rested) cell voltage. ";// + sChangeLine;
//			strOCVHeader05 = "// Please note that the cell must not have been charged or discharged for several ";// + sChangeLine;
//			strOCVHeader06 = "// hours prior to this remaining capacity determination, or remaining capacity may ";// + sChangeLine;
//			strOCVHeader07 = "// be considerable in error ";// + sChangeLine + sChangeLine;
//			strOCVHeader08 = "//Tester= " + TableSourceHeader.strTester;
//			strOCVHeader09 = "//Battery ID= " + TableSourceHeader.strBatteryID;
//			strOCVTableHeader = "//table header";// + sChangeLine + sChangeLine;

//			strOCVHeaderComment = iOCVHeaderNum.ToString() + sCommentTab + " //DO NOT CHANGE: word length of header (including this length)";
//			strOCVControlComment = iOCVControl.ToString() + sCommentTab + " //DO NOT CHANGE: control & 1: if input out of table, return table min/max as appropriate.";
//			strOCVAxiiComment = iOCVAxii.ToString() + sCommentTab + " //DO NOT CHANGE: number of axii";
//			strOCVXPointComment = iOCVXPoints.ToString() + sCommentTab + " //x axis points: maximum of 65 points";
//			strOCVYPointComment = iOCVYPoints.ToString() + sCommentTab + " //DO NOT CHANGE: 'y' axis entries per x axis";// + sChangeLine + sChangeLine;

//			strOCVXLine01 = "//x (independent) axis: SOC 0% = 0, 100% = 10000:";// + sChangeLine;
//			strOCVXLine02 = "// (this is the cell voltage read after 24 hours \"rest\": no charge or discharge)";// + sChangeLine;
//			strOCVXLine03 = "//must be in increasing order: need not be evenly spaced";// + sChangeLine + sChangeLine;

//			strOCVYLine01 = sChangeLine + sChangeLine + "//y (dependent) axis: ";// + sChangeLine + sChangeLine;
		}

		private void InitializeTSOCbyOCVTable()
		{
			strSOCFileName = "TSOCbyOCV" + sFileSeperator + TableSourceHeader.Line04Content +
				sFileSeperator + TableSourceHeader.Line05Content +
				sFileSeperator + TableSourceHeader.fFullCapacity.ToString() + "mAhr" +
				//sFileSeperator + wHighBound.ToString() + "mV" +
				//sFileSeperator + wLowBound.ToString() + "mV" +
				//sFileSeperator + iMaxVoltage.ToString() + "mV" +
				//sFileSeperator + iMinVoltage.ToString() + "mV" +
				sFileSeperator + TableSourceHeader.strLimitChgVolt + "mV" +
				sFileSeperator + TableSourceHeader.strCutoffDsgVolt + "mV" +
				//sFileSeperator + "V003" +
				sFileSeperator + TableMaker.TableSample.strTBMVersion + 
				//sFileSeperator + batinfo.strVersion +
				sFileSeperator + DateTime.Now.Year.ToString("D4") +
				DateTime.Now.Month.ToString("D2") + DateTime.Now.Day.ToString("D2") +
				".txt";
			strSOCFileName = System.IO.Path.Combine(TableOutputFolder, strSOCFileName);

			strSOCHeader.Clear();
			strSOCHeader.Add(string.Format("//[Description]"));
			strSOCHeader.Add(string.Format("// cell capacity as a function of Open Circuit Voltage"));
			strSOCHeader.Add(string.Format("// This table is used at initial startup only to determine remaining cell capacity as "));
			strSOCHeader.Add(string.Format("// a fraction of full capacity, based on the open curcuit (no load, rested) cell voltage."));
			strSOCHeader.Add(string.Format(""));
			strSOCHeader.Add(string.Format("// Please note that the cell must not have been charged or discharged for several "));
			strSOCHeader.Add(string.Format("// hours prior to this remaining capacity determination, or remaining capacity may "));
			strSOCHeader.Add(string.Format("// be considerable in error "));
			strSOCHeader.Add(string.Format(""));
			strSOCHeader.Add(string.Format("//Table Header Information:"));
			strSOCHeader.Add(string.Format(""));
			strSOCHeader.Add(string.Format("//Manufacturer = {0}", TableSourceHeader.strManufacture));
			strSOCHeader.Add(string.Format("//Battery Type = {0}", TableSourceHeader.strBatteryModel));
			strSOCHeader.Add(string.Format("//Equipment = {0}", TableSourceHeader.strEquip));
			strSOCHeader.Add(string.Format("//Built Date = {0} {1} {2}", DateTime.Now.Year.ToString("D4"), DateTime.Now.Month.ToString("D2"), DateTime.Now.Day.ToString("D2")));
			strSOCHeader.Add(string.Format("//MinimalVoltage = {0}", TableSourceHeader.strCutoffDsgVolt));
			strSOCHeader.Add(string.Format("//MaximalVoltage = {0}", TableSourceHeader.strLimitChgVolt));
			strSOCHeader.Add(string.Format("//FullAbsoluteCapacity = {0}", TableSourceHeader.strAbsMaxCap));
			strSOCHeader.Add(string.Format("//Age = {0}", TableSourceHeader.strCycleCount));
			strSOCHeader.Add(string.Format("//Tester = {0}", strTester));
			strSOCHeader.Add(string.Format("//Battery ID = {0}", strBatteryID));
			//(A141024)Francis, add Version/Date/Comment 3 string into header comment of txt file
			strSOCHeader.Add(string.Format("//Version = {0}", strUserVersion));
			strSOCHeader.Add(string.Format("//Date = {0}", strUserDate));
			//strSOCHeader.Add(string.Format("//Comment = {0}", strUserComment));
			strSOCHeader.Add(string.Format("//Comment = "));
			string[] var = strUserComment.Split('\n');
			foreach (string str in var)
			{
				strSOCHeader.Add(string.Format("//          {0}", str.Replace('\r', ' ')));
			}
			//(E141024)
			strSOCHeader.Add(string.Format(""));

			/*
			strSOCHeader01 = "// cell capacity as a function of Open Circuit Voltage";
			strSOCHeader02 = "// for " + TableSourceHeader.Line04Content + " " + TableSourceHeader.Line05Content + " cell ";// +sChangeLine;
			strSOCHeader03 = "// This table is used at initial startup only to determine remaining cell capacity as ";// + sChangeLine;
			strSOCHeader04 = "// a fraction of full capacity, based on the open curcuit (no load, rested) cell voltage.";// + sChangeLine;
			strSOCHeader05 = "// Please note that the cell must not have been charged or discharged for several ";// + sChangeLine;
			strSOCHeader06 = "// hours prior to this remaining capacity determination, or remaining capacity may ";// + sChangeLine;
			strSOCHeader07 = "// be considerable in error ";// + sChangeLine + sChangeLine;
			strSOCHeader08 = "//Tester= " + TableSourceHeader.strTester;
			strSOCHeader09 = "//Battery ID= " + TableSourceHeader.strBatteryID;
			strSOCTableHeader = "//table header";// + sChangeLine + sChangeLine;

			strSOCHeaderComment = iSOCHeaderNum.ToString() + sCommentTab + " //DO NOT CHANGE: word length of header (including this length)";
			strSOCControlComment = iSOCControl.ToString() + sCommentTab + " //DO NOT CHANGE: control & 1: if input out of table, return table min/max as appropriate.";
			strSOCAxiiComment = iSOCAxii.ToString() + sCommentTab + " //DO NOT CHANGE: number of axii";
			//due to iSOCXPoints is calculated after MaxVoltage/MinVoltage, so move it to GenerateTSOCbyOCVTable() before writing to file
			//strSOCXPointComment = iSOCXPoints.ToString() + sCommentTab + " //x axis points: maximum of 87 points";
			strSOCYPointComment = iSOCYPoints.ToString() + sCommentTab + " //DO NOT CHANGE: 'y' axis entries per x axis";// + sChangeLine + sChangeLine;

			strSOCXLine01 = "//x (independent) axis: low cell open circuit millivolts: ";// + sChangeLine;
			strSOCXLine02 = "// (this is the cell voltage read after 24 hours \"rest\": no charge or discharge)";// + sChangeLine;
			strSOCXLine03 = "//must be in increasing order: need not be evenly spaced";// + sChangeLine + sChangeLine;

			strSOCYLine01 = sChangeLine + sChangeLine + "//y (dependent) axis: ";// + sChangeLine + sChangeLine;

			*/

			//iMinVoltage = (int)wLowBound;
			//iMaxVoltage = (int)wHighBound;
		}

		//(A141107)Francis, new OCV calculation by Guoyan request
		private bool CreateNewOCVPoints(List<SourceDataSample> lstSample2, ref UInt32 uErr, List<float> lstfPoints = null)
		{
			bool bReturn = false;
			//float fACCStep = OCVSample.fPerSteps * 0.01F * (TableSourceData[0].myHeader.fFullCapacity - TableSourceData[0].myHeader.fCapacityDiff);
			float fSoCStep = fPerSteps * 100;		//=156.25
			int iSoCCount = iNumOfPoints;		//=65
			int /*iSoCAdjLow = 0, iSoCAdjHigh = 0,*/ indexLow = 0, indexHigh = 0;
			SourceDataSample lowcurSample, higcurSample;
			//float fHdAbsMaxCap, fHdCapacityDiff;
			float fSoCVoltLow, fSoCVoltHigh, fTmpSoC1, fTmpSoC2, fTmpVolt1, /*fTmpVolt2,*/ fSoCbk, fVoltbk;
			float fRsocn = 1.0F;
			int fmulti = (int)(((float)(iMaxVoltage - iMinVoltage)) * 10F / OCVSample.iSOCStepmV);
			int ileft = (int)fmulti % 10;
			//int ftpp = (BattModel.wTSOCPoints - 1) * OCVSample.iSOCStepmV + myOCV.iMinVoltage;
			int itemp, iSumVolt;
			int iHundred = 32767;
			int ii = 0, jj = 0;

			#region assign low/high current sample, and assign SoC points (as default or input)
			//lstSample2.Count definitely is 2
			if (Math.Abs(lstSample2[0].myHeader.fCurrent) < Math.Abs(lstSample2[1].myHeader.fCurrent))
			{
				lowcurSample = lstSample2[0];
				higcurSample = lstSample2[1];
			}
			else
			{
				lowcurSample = lstSample2[1];
				higcurSample = lstSample2[0];
			}
			//fHdAbsMaxCap = lstSample2[0].myHeader.fAbsMaxCap;
			//fHdCapacityDiff = lstSample2[0].myHeader.fCapacityDiff;

			//if it is null, use default SoC points, each 1.5625% record voltage
			//to be compatible that if AE like user to input SoC points from UI
			//in such that case it can call with 3rd parameter as SoC points input
			if (lstfPoints == null)
			{
				lstfPoints = new List<float>();
				for (int i = 0; i < iSoCCount; i++)
				{
					lstfPoints.Add(fSoCStep * i);
				}
			}
			else
			{
				iSoCCount = lstfPoints.Count;
			}
			#endregion

			//calculate TSOCbyOCV, high/low voltage is coming from user input
			fmulti /= 10;
			fTmpVolt1 = fmulti * OCVSample.iSOCStepmV + iMinVoltage;

			if ((ileft != 0) || (fTmpVolt1 != iMaxVoltage))
			{
				if (fTmpVolt1 < iMaxVoltage)	//should not bigger than iMaxVoltage
				{
					iMaxVoltage = (int)(fTmpVolt1+0.5);
				}
			}

			lstfPoints.Sort();
			lstfPoints.Reverse();		//from high to low SoC
			iOCVVolt.Clear();
			iSOCVolt.Clear();
			iSOCBit.Clear();
			foreach (float fSoC1Point in lstfPoints)
			{
				fSoCVoltLow = 0; fSoCVoltHigh = 0;	//default value

				#region find <SoC, Volt> from low current data
				fTmpSoC1 = 0; //fTmpSoC2 = 0; //default value
				fSoCbk = 0; fVoltbk = 0;	//default value
				for (; indexLow < lowcurSample.AdjustedExpData.Count; indexLow++)
				{
					fTmpVolt1 = lowcurSample.AdjustedExpData[indexLow].fVoltage;
					fTmpSoC1 = lowcurSample.AdjustedExpData[indexLow].fSoCAdj;
					if (fSoC1Point >= fTmpSoC1)
					{
						if (indexLow == 0)	//first record
						{
							fSoCVoltLow = fTmpVolt1;
							fSoCbk = fTmpSoC1;
							fVoltbk = fTmpVolt1;
						}
						else
						{
							if (fSoC1Point == fTmpSoC1)
							{
								fSoCVoltLow = fTmpVolt1;
								fSoCbk = fTmpSoC1;
								fVoltbk = fTmpVolt1;
							}
							else
							{
								fSoCVoltLow = fTmpVolt1;	//no modify fSoCbk, fVoltbk
							}
						}
						break;
					}
					else
					{
						fSoCbk = fTmpSoC1;
						fVoltbk = fTmpVolt1;
					}
				}	//for(;
				if (indexLow < lowcurSample.AdjustedExpData.Count)		//in experiment range
				{
					if ((Math.Abs(fSoCbk - fSoC1Point) > 5)	 ||
						(Math.Abs(fSoC1Point - fTmpSoC1) > 5))//SoC difference is bigger than 5, error
					{
						LibErrorCode.uVal01 = (UInt32)lowcurSample.AdjustedExpData[indexLow].uSerailNum;
						LibErrorCode.strVal01 = lowcurSample.strSourceFilePath;
						uErr = LibErrorCode.IDS_ERR_TMK_OCVNEW_SOC_OVER5;
						CreateNewErrorLog(LibErrorCode.strVal01, LibErrorCode.uVal01, fTmpSoC1, lowcurSample.myHeader.fCurrent, lowcurSample.myHeader.fTemperature, uErr);
					}
					else
					{
						//if in 5 (10000C unit) range, use linear interpolation
						if (fSoCbk != fTmpSoC1)		//must use this!!
						{
							fSoCVoltLow += (fVoltbk - fSoCVoltLow) * (fSoC1Point - fTmpSoC1) / (fSoCbk - fTmpSoC1);
						}
					}
				}
				else		//out of experiment range
				{
					//use linear extrapolation, use last one point copy currently and temporarily 
					if (fVoltbk != 0)
					{
						fSoCVoltLow = fVoltbk;
					}
					else
					{
						fSoCVoltLow = lowcurSample.AdjustedExpData[lowcurSample.AdjustedExpData.Count - 1].fVoltage;
					}
				}
				#endregion

				#region find <SoC, Volt> from high current data
				fTmpSoC1 = 0; //fTmpSoC2 = 0; //default value
				fSoCbk = 0; fVoltbk = 0;	//default value
				for (; indexHigh < higcurSample.AdjustedExpData.Count; indexHigh++)
				{
					fTmpVolt1 = higcurSample.AdjustedExpData[indexHigh].fVoltage;
					fTmpSoC1 = higcurSample.AdjustedExpData[indexHigh].fSoCAdj;
					if (fSoC1Point >= fTmpSoC1)
					{
						if (indexHigh == 0)	//first record
						{
							fSoCVoltHigh = fTmpVolt1;
							fSoCbk = fTmpSoC1;
							fVoltbk = fTmpVolt1;
						}
						else
						{
							if (fSoC1Point == fTmpSoC1)
							{
								fSoCVoltHigh = fTmpVolt1;
								fSoCbk = fTmpSoC1;
								fVoltbk = fTmpVolt1;
							}
							else
							{
								fSoCVoltHigh = fTmpVolt1;	//no modify fSoCbk, fVoltbk
							}
						}
						break;
					}
					else
					{
						fSoCbk = fTmpSoC1;
						fVoltbk = fTmpVolt1;
					}
				}	//for(;
				if (indexHigh < higcurSample.AdjustedExpData.Count)		//in experiment range
				{
					if ((Math.Abs(fSoCbk - fSoC1Point) > 5) ||
						(Math.Abs(fSoC1Point - fTmpSoC1) > 5))//SoC difference is bigger than 5, error
					{
						LibErrorCode.uVal01 = (UInt32)higcurSample.AdjustedExpData[indexHigh].uSerailNum;
						LibErrorCode.strVal01 = higcurSample.strSourceFilePath;
						uErr = LibErrorCode.IDS_ERR_TMK_OCVNEW_SOC_OVER5;
						CreateNewErrorLog(LibErrorCode.strVal01, LibErrorCode.uVal01, fTmpSoC1, lowcurSample.myHeader.fCurrent, lowcurSample.myHeader.fTemperature, uErr);
					}
					else
					{
						//if in 5 (10000C unit) range, use linear interpolation
						if (fSoCbk != fTmpSoC1)		//must use this!!
						{
							fSoCVoltHigh += (fVoltbk - fSoCVoltHigh) * (fSoC1Point - fTmpSoC1) / (fSoCbk - fTmpSoC1);
						}
					}
				}
				else		//out of experiment range
				{
					//use linear extrapolation, use last one point copy currently and temporarily 
					if (fVoltbk != 0)
					{
						fSoCVoltHigh = fVoltbk;
					}
					else
					{
						fSoCVoltHigh = higcurSample.AdjustedExpData[higcurSample.AdjustedExpData.Count - 1].fVoltage;
					}
				}
				#endregion

				//found 2 sets <SoC, Volt> from low/high current experiment data, add inot OCV list according Guoyan calculation
				fRsocn = (fSoCVoltLow - fSoCVoltHigh) / (Math.Abs(higcurSample.myHeader.fCurrent) - Math.Abs(lowcurSample.myHeader.fCurrent));
				fTmpVolt1 = fSoCVoltLow + Math.Abs(lowcurSample.myHeader.fCurrent) * fRsocn;
				if (fTmpVolt1 < 0) MessageBox.Show("Minus Voltage Got");	//Fran debug
				iOCVVolt.Add((int)(fTmpVolt1+0.5F));
			}

			//check iOCVVolt points number
			if (iOCVVolt.Count != iNumOfPoints)
			{
				LibErrorCode.strVal01 = lowcurSample.strSourceFilePath;
				LibErrorCode.uVal01 = (UInt32)iOCVVolt.Count;
				LibErrorCode.uVal02 = (UInt32)OCVSample.iNumOfPoints;
				uErr = LibErrorCode.IDS_ERR_TMK_OCVNEW_OCV_POINTS;
				CreateNewErrorLog(LibErrorCode.strVal01, UInt32.MaxValue, float.MaxValue, lowcurSample.myHeader.fCurrent, lowcurSample.myHeader.fTemperature, uErr);
				return bReturn;
			}
			else
			{
				iOCVVolt.Sort();
				iOCVVolt.Reverse();
			}

			//if (bAccTrust)	//means accmah has value
			{
				//iOCVVolt and lstfPoints are sorting from bigger to smaller value
				jj = 0;
				for(ii = 0; ii < fmulti; ii++)
				{
					iSumVolt = (iMaxVoltage - (ii * OCVSample.iSOCStepmV));
					for (; jj < iOCVVolt.Count;)
					{
						if (iOCVVolt[jj] <= iSumVolt)
						{
							if (jj == 0)
							{
								//get SoC in 10000C format
								fTmpSoC2 = lstfPoints[jj];
							}
							else
							{
								//use interpolation to get SoC in 10000C format
								fTmpSoC1 = (float)(iSumVolt - iOCVVolt[jj]);
								fTmpSoC1 /= (float)(iOCVVolt[jj - 1] - iOCVVolt[jj]);
								fTmpSoC2 = fTmpSoC1 * (lstfPoints[jj - 1] - lstfPoints[jj]) + lstfPoints[jj];
							}
							//convert to 32767 format
							fTmpSoC2 *= ((float)iHundred / 10000);
                            itemp = Convert.ToInt32(Math.Round(fTmpSoC2, 0));
							if (itemp <= 0) itemp = 0;	//keep it positiv, or 0
							iSOCVolt.Add(iSumVolt);
							iSOCBit.Add(itemp);
							break;
						}
						else
						{
							jj++;
						}
					}	//for (int jj = 0; jj < iOCVVolt.Count; )
					if (jj >= iOCVVolt.Count) break;
				}	//for(int ii = 0; ii < fmulti; ii++)
				if (iSOCVolt.Count <= fmulti)	//not enough points selected in last SoC, compensate it with linear extrapolation
				{
					/*	//as discussed with Jon, linear extrapolation will make value are all zero, use another method to compensate
					fTmpSoC1 = (iSOCBit[iSOCBit.Count - 2] - iSOCBit[iSOCBit.Count - 1]);
					fTmpSoC1 /= (iSOCVolt[iSOCVolt.Count - 2] - iSOCVolt[iSOCVolt.Count - 1]);	//get a in y=ax+b 
					fTmpSoC2 = iSOCBit[iSOCBit.Count -1] - fTmpSoC1 * iSOCVolt[iSOCVolt.Count-1];	//get b in y=ax+b
					*/
					fTmpSoC1 = (float)(iSOCBit[iSOCBit.Count - 1] - 0);
					fTmpSoC1 /= iSOCVolt[iSOCVolt.Count - 1] - iMinVoltage;
					fTmpSoC2 = 0 - fTmpSoC1 * iMinVoltage;
					for (jj = iSOCVolt.Count; jj <= fmulti; jj++)
					{
						itemp = (int)(iMaxVoltage - (jj * OCVSample.iSOCStepmV));
						iSOCVolt.Add(itemp);
                        itemp = Convert.ToInt32(Math.Round((float)itemp * fTmpSoC1 + fTmpSoC2, 0));
						if (itemp <= 0) itemp = 0;
						iSOCBit.Add(itemp);
					}
					//fTmpSoC2 = lstfPoints[lstfPoints.Count - 1];
					//fTmpSoC2 *= ((float)iHundred / 10000);
					//itemp = (int)(fTmpSoC2 + 0.5F);
					//iSOCVolt.Add((int)(iOCVVolt[iOCVVolt.Count - 1] + 0.5));
					//iSOCBit.Add(itemp);
					bReturn = true;
				}
				else if (iSOCVolt.Count == (fmulti + 1))
				{
					bReturn = true;
				}
				else
				{
					//should not happen
					LibErrorCode.strVal01 = lowcurSample.strSourceFilePath;
					LibErrorCode.uVal01 = (UInt32)iSOCVolt.Count;
					LibErrorCode.uVal02 = (UInt32)fmulti;
					uErr = LibErrorCode.IDS_ERR_TMK_OCV_SOC_POINT;
					CreateNewErrorLog(LibErrorCode.strVal01, UInt32.MaxValue, float.MaxValue, lowcurSample.myHeader.fCurrent, lowcurSample.myHeader.fTemperature, uErr);
				}
			}


			return bReturn;
		}
		//(E141107)

	}

	public class RCSample : TableInterface
	{
		#region private constant definition

		//private string sFileSeperator = "_";
		//private string sCommentTab = "\t";
		//private string sValueSeperator = ",";
		//private string sChangeLine = "\r\n";
		//private string sComment = "//";

		#endregion

		#region private members definition

		#region RC table content definition
		private string strRCFileName { get; set; }
//		private string strRCHeader01 { get; set; }	////[Description]
//		private string strRCHeader02 { get; set; }
//		private string strRCHeader03 { get; set; }
//		private string strRCHeader04 { get; set; }
//		private string strRCHeader05 { get; set; }
//		private string strRCHeader06 { get; set; }
//		private string strRCHeader07 { get; set; }
//		private string strRCHeader08 { get; set; }
//		private string strRCHeader09 { get; set; }
//		private string strRCHeader10 { get; set; }
//		private string strRCHeader11 { get; set; }
//		private string strRCHeader12 { get; set; }
		private List<string> strRCHeader = new List<string>();
		private List<string> strRCContent = new List<string>();

//		private string strRCTableHeaderComment { get; set; }	////Table 1 header: residual capacity lookup
//		private int iRCHeaderLength = 7;
//		private string strRCHeadLengthComment { get; set; }
//		private int iRCControl = 1;
//		private string strRCControlComment { get; set; }
//		private int iRCAxii = 3;
//		private string strRCAxiiComment { get; set; }
//		private int iRCXPoints = 25;		//a default value, Voltage Points maybe change by user input
//		private string strRCXPointComment { get; set; }
//		private int iRCWPoints = 7;			//a default value, Current Points depends on experiment current data
//		private string strRCWPointComment { get; set; }
//		private int iRCVPoints = 8;			//a default value, Temperature Points depends on experiment temperature data
//		private string strRCVPointComment { get; set; }
//		private int iRCYPoints = 1;			// fixed value
//		private string strRCYPointComment { get; set; }

//		private string strRCDataComment { get; set; }
//		private string strRCXLine01 { get; set; }
//		private string strRCXValues = ",,,,,,,,,,,,,,,,,,,,,,,,";

//		private string strRCWLine01 { get; set; }
//		private string strRCWValues = ",,,,,,,";

//		private string strRCVLine01 { get; set; }
//		private string strRCVValues = ",,,,,,,,";

//		private string strRCYLine01 { get; set; }
//		private string strRCYLine02Repeat { get; set; }	 ////temp = xxx'C
//		private float fRCYLine02Temp = -7.5F;
//		private string strRCYLine02Tail { get; set; }
//		private string strRCYValues = ",,,";
		#endregion

		#region battery header information
		private string strManufacture = "";
		private string strBatteryModel = "";
		private float fDesignCapacity = -9999.0F;
		private float fCapaciDiff = -9999.0F;
		private bool bInitialized = false;			//for record that InitializeTable() function is working OK
		#endregion

		#endregion

		#region public members definition

//		private List<SourceDataSample> m_bdsBatRCSource = null;
//		public List<SourceDataSample> bdsBatRCSource
//		{
//			get { return m_bdsBatRCSource; }
//			set { m_bdsBatRCSource = value; }
//		}

        //		private List<UInt32> m_uRCVolt = null;
        //		public List<UInt32> uRCVolt
//		{
//			get { return m_uRCVolt; }
//			set { m_uRCVolt = value; }
//		}

//		public string strRCOutputFolder = null;

        //(D170302)Francis, move to base class,TableInterface
		//public List<float> listfTemp = new List<float>();		//save all temp value of raw data, in 'C format
		//public List<float> listfCurr = new List<float>();			//save all curr value of raw data, in mA, minus is discharge
        //public List<List<Int32>> iYPointsall = new List<List<Int32>>();

		#endregion

		public RCSample()
		{
			TableSourceData = new List<SourceDataSample>();
			TableSourceData.Clear();
			TableVoltagePoints = new List<UInt32>();
			TableVoltagePoints.Clear();
			TableType = TypeEnum.RCRawType;
			TableSourceHeader = null;
			TableOutputFolder = "";
			bInitialized = false;
		}

		public override bool InitializeTable(List<UInt32> uVoltPoints, ref UInt32 uErr, string strOutputFolder = null)
		{
			bool bReturn = false;
			float fTmpTemp, fTmpCurr, fTmpDCap;
			string strTmpManu, strTmpBatM, strTmpTester, strTmpBattID;

            TableOutputFolder = strOutputFolder;
			if (TableSourceData.Count <= 2)
			{
				LibErrorCode.uVal01 = (UInt32)TableSourceData.Count;
				uErr = LibErrorCode.IDS_ERR_TMK_RC_SOURCE_LESS;
				CreateNewErrorLog(" ", UInt32.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, uErr);
			}
			else if (uVoltPoints.Count <= 2)
			{
				LibErrorCode.uVal01 = (UInt32)uVoltPoints.Count;
				uErr = LibErrorCode.IDS_ERR_TMK_RC_VOLTAGE_LESS;
				CreateNewErrorLog(" ", UInt32.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, uErr);
			}
			else
			{	//SourceData > 2 and VoltagePoint > 2
				bReturn = true;
				TableVoltagePoints = uVoltPoints;
				TableVoltagePoints.Sort();
				foreach (SourceDataSample sdsi in TableSourceData)
				{
					//(D140717)Francis, remove due to Guoyan request for new format of source data, and 
					//ReservedExpData() already be filled up when parsing log data
					//bReturn = sdsi.ReserveExpPoints(ref uErr, TableVoltagePoints[0]);
					//if (!bReturn)
					//{
						//break;
					//}
					//else
					//(E140717)
					{
						fTmpTemp = sdsi.myHeader.fTemperature;
						fTmpCurr = sdsi.myHeader.fCurrent;
						fTmpDCap = sdsi.myHeader.fFullCapacity;
						strTmpManu = sdsi.myHeader.strManufacture;
						strTmpBatM = sdsi.myHeader.strBatteryModel;
						strTmpTester = sdsi.myHeader.strTester;
						strTmpBattID = sdsi.myHeader.strBatteryID;
						if (!listfTemp.Contains(fTmpTemp))	//not exist in list add it
						{
							listfTemp.Add(fTmpTemp);
						}
						if (!listfCurr.Contains(fTmpCurr))			//not exis in list add it
						{
							listfCurr.Add(fTmpCurr);
						}
						if (fDesignCapacity == -9999.0F)
						{
							fDesignCapacity = fTmpDCap;
						}
						else if (fDesignCapacity != fTmpDCap)
						{
							LibErrorCode.strVal01= sdsi.strSourceFilePath;
							uErr = LibErrorCode.IDS_ERR_TMK_RC_DCAP_NOT_MATCH;
							CreateNewErrorLog(sdsi.strSourceFilePath, UInt32.MaxValue, float.MaxValue, sdsi.myHeader.fCurrent, sdsi.myHeader.fTemperature, uErr);
							//break;	//not break, as Guoyan reqeust if error, keep going
						}
						if (fCapaciDiff == -9999.0F)
						{
							LibErrorCode.strVal01= sdsi.strSourceFilePath;
							fCapaciDiff = sdsi.myHeader.fCapacityDiff;
						}
						else if (fCapaciDiff != sdsi.myHeader.fCapacityDiff)
						{
							LibErrorCode.strVal01 = sdsi.strSourceFilePath;
							uErr = LibErrorCode.IDS_ERR_TMK_RC_CAPDIFF_NOT_MATCH;
							CreateNewErrorLog(sdsi.strSourceFilePath, UInt32.MaxValue, float.MaxValue, sdsi.myHeader.fCurrent, sdsi.myHeader.fTemperature, uErr);
							//break;	//not break, as Guoyan reqeust if error, make false keep going
							bReturn = false;
						}
						if (strManufacture.Length == 0)
						{
							strManufacture = string.Format(strTmpManu.ToString());
						}
						else
						{
							if (!object.Equals(strManufacture, strTmpManu))
							{
								uErr = LibErrorCode.IDS_ERR_TMK_RC_MANUFA_NOT_MATCH;
								CreateNewErrorLog(sdsi.strSourceFilePath, UInt32.MaxValue, float.MaxValue, sdsi.myHeader.fCurrent, sdsi.myHeader.fTemperature, uErr);
								//break;	//not break, as Guoyan reqeust if error, make false keep going
								bReturn = false;
							}
						}
						if (strBatteryModel.Length == 0)
						{
							strBatteryModel = string.Format(strTmpBatM.ToString());
						}
						else
						{
							if (!object.Equals(strBatteryModel, strTmpBatM))
							{
								uErr = LibErrorCode.IDS_ERR_TMK_RC_BAT_MODEL_NOT_MATCH;
								CreateNewErrorLog(sdsi.strSourceFilePath, UInt32.MaxValue, float.MaxValue, sdsi.myHeader.fCurrent, sdsi.myHeader.fTemperature, uErr);
								//break;	//not break, as Guoyan reqeust if error, make false keep going
								bReturn = false;
							}
						}
						if (strTester.Length == 0)
						{
							strTester = string.Format("{0}", strTmpTester);
						}
						else
						{
							if(strTester.ToLower().IndexOf(strTmpTester.ToLower(), 0) == -1)
							{
								strTester += string.Format(", {0}", strTmpTester);
							}
						}
						if (strBatteryID.Length == 0)
						{
							strBatteryID = string.Format("{0}", strTmpBattID);
						}
						else
						{
							if (strBatteryID.ToLower().IndexOf(strTmpBattID.ToLower(), 0) == -1)
							{
								strBatteryID += string.Format(", {0}", strTmpBattID);
							}
						}
					}
				}		//foreach (SourceDataSample sdsi in TableSourceData)
				//if (bReturn)	//(D141121)Francis, no matter true or false, check listfCurre, listfTemp, and TableSourceData.Count
				{
					if (listfCurr.Count <= 1)
					{
						LibErrorCode.uVal01 = (UInt32)listfCurr.Count;
						uErr = LibErrorCode.IDS_ERR_TMK_RC_EXP_CURRENT_LESS;
						CreateNewErrorLog(" ", UInt32.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, uErr);
						//return false;
						bReturn = false;
					}
					if (listfTemp.Count <= 1)
					{
						LibErrorCode.uVal01 = (UInt32)listfTemp.Count;
						uErr = LibErrorCode.IDS_ERR_TMK_RC_EXP_TEMPERATURE_LESS;
						CreateNewErrorLog(" ", UInt32.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, uErr);
						//return false;
						bReturn = false;
					}
					if (TableSourceData.Count != (listfCurr.Count * listfTemp.Count))
					{
						LibErrorCode.uVal01 = (UInt32)TableSourceData.Count;
						LibErrorCode.uVal02 = (UInt32)(listfTemp.Count);
						LibErrorCode.uVal03 = (UInt32)(listfCurr.Count);
						uErr = LibErrorCode.IDS_ERR_TMK_RC_EXP_SOURCE_NUM_NOT_MATCH;
						CreateNewErrorLog(" ", UInt32.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, uErr);
						//return false;
						bReturn = false;
					}
					//everything is OK, sort temperature and current from small to big
					listfTemp.Sort();
					listfCurr.Sort();

					TableSourceHeader = TableSourceData[0].myHeader;
					if ((strOutputFolder != null) && (Directory.Exists(strOutputFolder)))
					{
						TableOutputFolder = strOutputFolder;
					}
					else
					{
						TableOutputFolder = TableSourceData[0].strOutputFolder;
					}
					if(bReturn)
						uErr = LibErrorCode.IDS_ERR_SUCCESSFUL;
					//(D141024)Francis, due to some comments are added when user is pressing 'Make' button. 
					// Move Initialize FileName and Comment content to BuildTable() funciton
					//InitializeRCTable();
					//bReturn = InitializeAndroidDriver(ref uErr);	//(A140722)Francis, support Android Driver
					//(E141024)
					//bReturn = true;
					//bInitialized = true;
				}
			}	//SourceData > 2 and VoltagePoint > 2

			if(bReturn)	bInitialized = true;		//bInitialized is used by other method to check initialization steps are OK or not

			return bReturn;
		}

		public override bool BuildTable(ref UInt32 uErr, List<string> mkParamString)
		{
			bool bReturn = false;

			if (bInitialized)
			{
				//(A141024)Francis, due to some comments are added when user is pressing 'Make' button. 
				// Move Initialize FileName and Comment content to BuildTable() funciton
				PrepareUserInput(uRCVer, mkParamString);
				InitializeRCTable();
				bReturn = InitializeAndroidDriver(ref uErr, this);	//(A140722)Francis, support Android Driver
				//if (!bReturn)
					//return bReturn;
				//(E141024)

				//if (CreateRCPoints(ref iYPointsall, ref uErr))
				bReturn &= CreateRCPoints(ref iYPointsall, ref uErr);
				{
					//(D141120)Francis, seperate create data points and generate txt file into 2 API
					//bReturn = true;
					/*
					if (GenerateRCTable(iYPointsall, ref uErr))
					{
						Record4Versions(VersionEnum.VerEnmRC);
						myAndroidDriver.MakeRCContent(TableVoltagePoints, listfCurr, listfTemp, iYPointsall);
						bReturn = myAndroidDriver.MakeDriver(ref uErr, TableOutputFolder);
						if (bReturn)
						{
							//(A141024)Francis, check bMakeTable value, if true, means Android Driver C/H file are created
							if (myAndroidDriver.GetMakeTableStatus())
							{
								Record4Versions(VersionEnum.VerEnmTable);
							}
							//(E141024)
						}
						//bReturn = true;
					}
					*/
				}
			}
			else
			{
				uErr = LibErrorCode.IDS_ERR_TMK_RC_INITIALZIED_FAILED;
				CreateNewErrorLog(" ", UInt32.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, uErr);
			}

			//if (bReturn)
				bBuildOK = true;

			return bReturn;
		}

		public override bool GenerateFile(ref uint uErr)
		{
			bool bReturn = false;

			//(M141120)Francis, if true, make bBuildOK
			if (!bBuildOK)
			{
				uErr = LibErrorCode.IDS_ERR_TMK_TBL_BUILD_SEQUENCE;
				CreateNewErrorLog(strRCFileName, UInt32.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, uErr);
				return bReturn;
			}
			bBuildOK = false;	//set as false, no matter Generate successfully or not
			//(E141120)

			if (GenerateRCTable(iYPointsall, ref uErr))
			{
				Record4Versions(VersionEnum.VerEnmRC);
                uRCVoltagePoints = TableVoltagePoints;          //(A170228)Francis, copy voltage points from user to variable for calculation
                myAndroidDriver.MakeRCContent(TableVoltagePoints, listfCurr, listfTemp, iYPointsall);
				bReturn = myAndroidDriver.MakeDriver(ref uErr, TableOutputFolder, bVTRboth);
				if (bReturn)
				{
					//(A141024)Francis, check bMakeTable value, if true, means Android Driver C/H file are created
					if (myAndroidDriver.GetMakeTableStatus())
					{
						Record4Versions(VersionEnum.VerEnmTable);
					}
					//(E141024)
				}
				//bReturn = true;
            }

			return bReturn;
		}

		private void InitializeRCTable()
		{
			string strEqAll = "";
			//string strPeAll = "";		//not person record currently
			float fAge = 0;
			float fTmp;
			//int iCRate = 10000;
			//bool bCRate = false;

			strRCFileName = "RC" + sFileSeperator + strManufacture +
											sFileSeperator + strBatteryModel +
											sFileSeperator + fDesignCapacity.ToString() + "mAhr" +
											//sFileSeperator + TableVoltagePoints[0].ToString() + "mV" +
											//sFileSeperator + TableVoltagePoints[TableVoltagePoints.Count - 1] + "mV" +
											sFileSeperator + TableSourceHeader.strLimitChgVolt + "mV" +
											sFileSeperator + TableSourceHeader.strCutoffDsgVolt + "mV" +
											sFileSeperator + "V002" +
											sFileSeperator + DateTime.Now.Year.ToString("D4") +
											DateTime.Now.Month.ToString("D2") + DateTime.Now.Day.ToString("D2") +
											".txt";
			strRCFileName = System.IO.Path.Combine(TableOutputFolder, strRCFileName);

			foreach (SourceDataSample rds in TableSourceData)
			{
				strEqAll += rds.myHeader.strEquip + ", ";
				//strPeAll += rds.myHeader.strPerson + ", ";
				if (float.TryParse(rds.myHeader.strCycleCount, out fTmp))
				{
					if (fTmp > fAge)
					{
						fAge = fTmp;
					}
				}
			}

			strRCHeader.Clear();
			strRCHeader.Add(string.Format("//[Description]"));
			strRCHeader.Add(string.Format("// Remaining Capacity as a function of cell capacity"));
			strRCHeader.Add(string.Format("// This table is used during discharging mode, to determine remaining cell capacity in "));
			strRCHeader.Add(string.Format("// particular condition, based on cell voltage, discharging current and cell temperature. "));
			strRCHeader.Add(string.Format(""));
			strRCHeader.Add(string.Format("// Please note that the cell must in discharging mode and  voltage is under table "));
			strRCHeader.Add(string.Format("// definition range, otherwise it will be considerable in error. "));
			strRCHeader.Add(string.Format(""));
			strRCHeader.Add(string.Format("//Table Header Information:"));
			strRCHeader.Add(string.Format(""));
			strRCHeader.Add(string.Format("//Manufacturer = {0}", strManufacture));
			strRCHeader.Add(string.Format("//Battery Type = {0}", strBatteryModel));
			strRCHeader.Add(string.Format("//Equipment = {0}", strEqAll));
			strRCHeader.Add(string.Format("//Built Date = {0} {1} {2}", DateTime.Now.Year.ToString("D4"), DateTime.Now.Month.ToString("D2"), DateTime.Now.Day.ToString("D2")));
			strRCHeader.Add(string.Format("//MinimalVoltage = {0}", TableSourceHeader.strCutoffDsgVolt));
			strRCHeader.Add(string.Format("//MaximalVoltage = {0}", TableSourceHeader.strLimitChgVolt));
			strRCHeader.Add(string.Format("//FullAbsoluteCapacity = {0}", TableSourceHeader.strAbsMaxCap));
			strRCHeader.Add(string.Format("//Age = {0}", TableSourceHeader.strCycleCount));
			strRCHeader.Add(string.Format("//Tester = {0}", strTester));
			strRCHeader.Add(string.Format("//Battery ID = {0}", strBatteryID));
			//(A141024)Francis, add Version/Date/Comment 3 string into header comment of txt file
			strRCHeader.Add(string.Format("//Version = {0}", strUserVersion));
			strRCHeader.Add(string.Format("//Date = {0}", strUserDate));
			//strRCHeader.Add(string.Format("//Comment = {0}", strUserComment));
			strRCHeader.Add(string.Format("//Comment = "));
			string[] var = strUserComment.Split('\n');
			foreach (string str in var)
			{
				strRCHeader.Add(string.Format("//          {0}", str.Replace('\r', ' ')));
			}
			//(E141024)
			strRCHeader.Add(string.Format(""));
			/*
			strRCHeader01 = "//[Description]";
			strRCHeader02 = "//Manufacturer = " + strManufacture;
			strRCHeader03 = "//Battery Type = " + strBatteryModel;
			strRCHeader04 = "//Equipment = " + strEqAll;
			strRCHeader05 = "//Persons = ";
			strRCHeader06 = "//Date = " + DateTime.Now.Year.ToString("D4") + DateTime.Now.Month.ToString("D2") + DateTime.Now.Day.ToString("D2");
			//strRCHeader07 = "//MinimalVoltage = " + TableVoltagePoints[0].ToString();
			//strRCHeader08 = "//MaximalVoltage = " + TableVoltagePoints[TableVoltagePoints.Count - 1].ToString();
			strRCHeader07 = "//MinimalVoltage = " + TableSourceHeader.strCutoffDsgVolt;
			strRCHeader08 = "//MaximalVoltage = " + TableSourceHeader.strLimitChgVolt;
			strRCHeader09 = "//FullAbsoluteCapacity = " + fDesignCapacity.ToString();
			strRCHeader10 = "//Age = " + fAge;
			strRCHeader11 = "//Tester = " + strTester;
			strRCHeader12 = "//Battery ID = " + strBatteryID;
			*/
            /*
            strRCTableHeaderComment = "//Table 1 header: residual capacity lookup";
            strRCHeadLengthComment = iRCHeaderLength.ToString() + sCommentTab + "//word length of header (including this length)";
            strRCControlComment = iRCControl.ToString() + sCommentTab + "//control word";
            strRCAxiiComment = iRCAxii.ToString() + sCommentTab + "//number of axii";
            strRCXPointComment = iRCXPoints.ToString() + sCommentTab + "//'x' voltage axis entries";
            strRCWPointComment = iRCWPoints.ToString() + sCommentTab + "//'w' current axis entries";
            strRCVPointComment = iRCVPoints.ToString() + sCommentTab + "//'v' temp axis entries";
            strRCYPointComment = iRCYPoints.ToString() + sCommentTab + "//'y' axis entries per 'x' axis entries";

            strRCDataComment = "//[Data]";
            strRCXLine01 = "//'x' axis: voltage in mV ";
            strRCXValues = "";
            TableVoltagePoints.Sort();
            foreach (UInt32 uvol in TableVoltagePoints)
            {
                strRCXValues += uvol.ToString() + ",";
            }

            strRCWValues = "";
            listfCurr.Sort();				//no need, just for case
            listfCurr.Reverse();
            foreach (float fcur in listfCurr)
            {
                fTmp = ((fcur * (-1)) / fDesignCapacity) * iCRate;
                if (fTmp > 32767)
                {
                    iCRate = 100;
                    bCRate = true;
                    break;
                }
                strRCWValues += ((int)(fTmp + 0.5)).ToString() + ",";
            }
            if (bCRate)	//need to change to 100C
            {
                strRCWValues = "";
                foreach (float fcur2 in listfCurr)
                {
                    fTmp = ((fcur2 * (-1)) / fDesignCapacity) * iCRate;
                    strRCWValues += ((int)(fTmp + 0.5)).ToString() + ",";
                }
            }
            strRCWLine01 = "//'w' axis: current in " + iCRate.ToString() + "*C (minor axis of 2d  lookup)";

            strRCVLine01 = "//'v' axis: temperature (major axis of 2d lookup) in .1 degrees C";
            strRCVValues = "";
            listfTemp.Sort();		//no need, just for case
            foreach (float ftemper in listfTemp)
            {
                if(ftemper >=0)
                    strRCVValues += ((int)(ftemper * 10 + 0.5)).ToString() + ",";
                else
                    strRCVValues += ((int)(ftemper * 10 - 0.5)).ToString() + ",";
            }

            strRCYLine01 = "//capacity in 10000*C";
            //start to repeat strRCYLine02Repeat + fRCYLine02Temp.ToString() + strRCYLine02Tail
            // and strRCYValues
            */
        }

		private bool CreateRCPoints(ref List<List<Int32>> outYValue, ref UInt32 uErr)
		{
			bool bReturn = true;	//cause below will use bReturn &= xxxx

			foreach (List<Int32> il in outYValue)
			{
				il.Clear();
			}
			outYValue.Clear();
			listfTemp.Sort();
			listfCurr.Sort();
			listfCurr.Reverse();
			foreach (float ft in listfTemp)		//from low temperature to list
			{
				foreach (float fc in listfCurr)		//from low current to list
				{
					foreach (SourceDataSample sds in TableSourceData)
					{
						if ((sds.myHeader.fTemperature == ft) &&
							(sds.myHeader.fCurrent == fc))
						{
							//bReturn &= sds.CreateSoCXls(TableOutputFolder, ref uErr);	//create SoC file in foreach loop
							if(!sds.CreateSoCXls(TableOutputFolder, ref uErr))
							{
								//error code is assign in CreateSoCXls();
								//return bReturn;
								CreateNewErrorLog(sds.strSourceFilePath, UInt32.MaxValue, LibErrorCode.fVal01, sds.myHeader.fCurrent, sds.myHeader.fTemperature, uErr);
								bReturn &= false;
								break;	//foreach (SourceDataSample sds in bdsBatRCSource)
							}
							else
							{
								List<Int32> il16tmp = new List<Int32>();
								if (sds.AdjustedExpData.Count < 1)
								{
									uErr = LibErrorCode.IDS_ERR_TMK_SD_EXPERIMENT_NOT_FOUND;
									CreateNewErrorLog(sds.strSourceFilePath, UInt32.MaxValue, LibErrorCode.fVal01, sds.myHeader.fCurrent, sds.myHeader.fTemperature, uErr);
									bReturn &= false;
									break;	//foreach (SourceDataSample sds in bdsBatRCSource)
								}
								else
								{
									//bReturn &= CreateYPoints(ref il16tmp, sds.AdjustedExpData, ref uErr);
									if (!CreateYPoints(ref il16tmp, sds.AdjustedExpData, ref uErr))
									{
										CreateNewErrorLog(sds.strSourceFilePath, UInt32.MaxValue, LibErrorCode.fVal01, sds.myHeader.fCurrent, sds.myHeader.fTemperature, uErr);
										bReturn &= false;
									}
									else
									{
									}
									outYValue.Add(il16tmp);	//if error, still add into RCYvalue
									break;	//foreach (SourceDataSample sds in bdsBatRCSource)
								}
								//if (!bReturn)
								//{
									//CreateNewErrorLog(sds.strSourceFilePath, UInt32.MaxValue, LibErrorCode.fVal01, sds.myHeader.fCurrent, sds.myHeader.fTemperature, uErr);
									////return bReturn;
									//break;	//foreach (SourceDataSample sds in bdsBatRCSource)
								//}
								//else
								//{
									//match temperature and current, make Y value
									//outYValue.Add(il16tmp);
									//break;	//foreach (SourceDataSample sds in bdsBatRCSource)
								//}
							}
						}
					}
				}
			}
			//bReturn &= true;

			return bReturn;
		}

		private bool CreateYPoints(ref List<Int32> ypoints, List<RawDataNode> inListRCData, ref UInt32 uErr)
		{
			bool bReturn = false;
			int i = 0, j=0;
			float fPreSoC = -99999, fPreVol = -99999, fAvgSoc = -99999;

			TableVoltagePoints.Sort();
			TableVoltagePoints.Reverse();
			ypoints.Clear();

			//(M140718)Francis,
			i = 0; j = 0;
			for (i = 0; i < TableVoltagePoints.Count; i++)
			{
				for (; j < inListRCData.Count; j++)
				{
					if (TableVoltagePoints[i] < inListRCData[j].fVoltage)
					{
						fPreSoC = inListRCData[j].fAccMah;
						fPreVol = inListRCData[j].fVoltage;
					}
					else
					{
						if ((fPreSoC != -99999) && (fPreVol != -99999))
						{
							fAvgSoc = (fPreVol - TableVoltagePoints[i]) / (fPreVol - inListRCData[j].fVoltage);
							fAvgSoc *= (fPreSoC - inListRCData[j].fAccMah);
							fAvgSoc += inListRCData[j].fAccMah;
							if ((i + 1) < TableVoltagePoints.Count)
							{
								if (TableVoltagePoints[i + 1] > inListRCData[j].fVoltage)
								{
								}
								else
								{
									fPreSoC = -99999;
									fPreVol = -99999;
								}
							}
							else
							{
								fPreSoC = -99999;
								fPreVol = -99999;
							}
						}
						else
						{
							if (j == 0)
							{
								fAvgSoc = 0F;
							}
							else
							{
								j += 1;
							}
						}
						break; //for(; j<)
					}
				}
				if(j <  inListRCData.Count)
				{
					if (fAvgSoc != -99999)
					{
						fAvgSoc = (fDesignCapacity - fCapaciDiff - fAvgSoc);	//convert to remaining
						fAvgSoc *= (10000 / fDesignCapacity);		//convert to 10000C
					}
				}
				else
				{
					RawDataNode rdtmp = null;
					for (int ij = inListRCData.Count - 1; ij >= 0; ij--)
					{
						if (Math.Abs(inListRCData[ij].fCurrent - 0) > 5)
						{
							rdtmp = inListRCData[ij];
							break;
						}
					}
					if(rdtmp !=null)
					{
						if (Math.Abs(TableVoltagePoints[i] - rdtmp.fVoltage) < 5)
						{
							fAvgSoc = (fDesignCapacity - fCapaciDiff - inListRCData[inListRCData.Count - 1].fAccMah);	//convert to remaining
							fAvgSoc *= (10000 / fDesignCapacity);			//convert to 10000C
						}
						else
						{
							LibErrorCode.uVal01 = (UInt32)TableVoltagePoints[i];
							LibErrorCode.fVal01 = rdtmp.fVoltage;
							uErr = LibErrorCode.IDS_ERR_TMK_RC_LAST_ONE_YPOINT;
							//because leakage of information, CreateNewErrorLog() need to be done outside of this funciton
							return bReturn;
						}
					}
					else
					{
							LibErrorCode.uVal01 = (UInt32)TableVoltagePoints[i];
							LibErrorCode.fVal01 = inListRCData[inListRCData.Count - 1].fVoltage;
							uErr = LibErrorCode.IDS_ERR_TMK_RC_LAST_ONE_YPOINT;
							//because leakage of information, CreateNewErrorLog() need to be done outside of this funciton
							return bReturn;
					}
				}
				if (fAvgSoc > 10000) fAvgSoc = 10000.0F;
				//if (fAvgSoc < 0) fAvgSoc = -9999.0F;
				if (fAvgSoc < 0) fAvgSoc -= 1.0F;
				ypoints.Add(Convert.ToInt32(Math.Round(fAvgSoc, 0)));
				//i += 1;
				if (i >= TableVoltagePoints.Count)
				{
					break;
				}

			}
			/**
			foreach (RawDataNode rad in inListRCData)
			{
				if (rad.fVoltage >= TableVoltagePoints[i])
				{
					fPreSoC = rad.fAccMah;
					fPreVol = rad.fVoltage;
				}
				else
				{
					if ((fPreSoC != -99999) && (fPreVol != -99999))
					{
						fAvgSoc = (fPreVol - TableVoltagePoints[i]) / (fPreVol - rad.fVoltage);
						fAvgSoc *= (fPreSoC - rad.fAccMah);
						fAvgSoc += rad.fAccMah;
						fPreSoC = -99999;
						fPreVol = -99999;
					}
					else
					{
						fAvgSoc = rad.fAccMah;
					}
					if (fAvgSoc != -99999)
					{
						fAvgSoc = (fDesignCapacity - fCapaciDiff - fAvgSoc);	//convert to remaining
						fAvgSoc *= (10000 / fDesignCapacity);		//convert to 10000C
						if (fAvgSoc > 10000) fAvgSoc = 10000.0F;
						//if (fAvgSoc < 0) fAvgSoc = -9999.0F;
						if (fAvgSoc < 0) fAvgSoc -= 1.0F;
						ypoints.Add((Int32)(int)(fAvgSoc + 0.5));
						i += 1;
						if (i >= TableVoltagePoints.Count)
							break;
					}
				}
			}
			*/

			if (ypoints.Count == TableVoltagePoints.Count - 1)
			{
				fAvgSoc = inListRCData[inListRCData.Count - 1].fAccMah;
				fAvgSoc = (fDesignCapacity - fCapaciDiff - fAvgSoc);	//convert to remaining
				fAvgSoc *= (10000 / fDesignCapacity);		//convert to 10000C
				if (fAvgSoc > 10000) fAvgSoc = 10000.0F;
				if (fAvgSoc < 0) fAvgSoc = -9999.0F;
				ypoints.Add(Convert.ToInt32(Math.Round(fAvgSoc, 0)));
				i += 1;
			}

			if (ypoints.Count == TableVoltagePoints.Count)
			{
				ypoints.Sort();
				bReturn = true;
			}
			else
			{
				LibErrorCode.uVal01 = (UInt32)ypoints.Count;
				LibErrorCode.uVal02 = (UInt32)TableVoltagePoints.Count;
				LibErrorCode.fVal01 = float.MaxValue;
				uErr = LibErrorCode.IDS_ERR_TMK_RC_Y_POINTS_NOT_MATCH;
				//because leakage of information, CreateNewErrorLog() need to be done outside of this funciton
			}

			return bReturn;
		}

		private bool GenerateRCTable(List<List<Int32>> rcYval, ref UInt32 uErr)
		{
			bool bReturn = false;
			FileStream fsRC = null;
			StreamWriter stmRC = null;

			if (rcYval.Count != (listfCurr.Count * listfTemp.Count))
			{
				LibErrorCode.uVal01 = (UInt32)rcYval.Count;
				LibErrorCode.uVal02 = (UInt32)listfTemp.Count;
				LibErrorCode.uVal03 = (UInt32)listfCurr.Count;
				uErr = LibErrorCode.IDS_ERR_TMK_RC_SOC_POINTS_ERROR;
				CreateNewErrorLog(" ", UInt32.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, uErr);
				return bReturn;
			}

			try
			{
				fsRC = File.Open(strRCFileName, FileMode.Create, FileAccess.Write, FileShare.None);
                stmRC = new StreamWriter(fsRC, System.Text.Encoding.GetEncoding("big5"));
			}
			catch (Exception erc)
			{
				LibErrorCode.strVal01 = strRCFileName;
				uErr = LibErrorCode.IDS_ERR_TMK_RC_CREATE_FILE;
				CreateNewErrorLog(strRCFileName, UInt32.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, uErr);
				return bReturn;
			}
			ConvertRCAllToString(rcYval);

			foreach (string strrch in strRCHeader)
			{
				stmRC.WriteLine(strrch);
			}
			foreach (string strrcc in strRCContent)
			{
				stmRC.WriteLine(strrcc);
			}
			stmRC.Close();
			fsRC.Close();
			uErr = LibErrorCode.IDS_ERR_SUCCESSFUL;


			/*
			FileContent = new StreamWriter(strRCFileName, false, Encoding.Unicode);
			FileContent.WriteLine(strRCHeader01);
			FileContent.WriteLine(strRCHeader02);
			FileContent.WriteLine(strRCHeader03);
			FileContent.WriteLine(strRCHeader04);
			FileContent.WriteLine(strRCHeader05);
			FileContent.WriteLine(strRCHeader06);
			FileContent.WriteLine(strRCHeader07);
			FileContent.WriteLine(strRCHeader08);
			FileContent.WriteLine(strRCHeader09);
			FileContent.WriteLine(strRCHeader10);
			FileContent.WriteLine(strRCHeader11);
			FileContent.WriteLine(strRCHeader12);
			FileContent.WriteLine();
			FileContent.WriteLine(strRCTableHeaderComment);
			FileContent.WriteLine(strRCHeadLengthComment);
			FileContent.WriteLine(strRCControlComment);
			FileContent.WriteLine(strRCAxiiComment);
			FileContent.WriteLine(strRCXPointComment);
			FileContent.WriteLine(strRCWPointComment);
			FileContent.WriteLine(strRCVPointComment);
			FileContent.WriteLine(strRCYPointComment);
			FileContent.WriteLine();
			FileContent.WriteLine(strRCDataComment);
			FileContent.WriteLine(strRCXLine01);
			FileContent.WriteLine(strRCXValues);
			FileContent.WriteLine();
			FileContent.WriteLine(strRCWLine01);
			FileContent.WriteLine(strRCWValues);
			FileContent.WriteLine();
			FileContent.WriteLine(strRCVLine01);
			FileContent.WriteLine(strRCVValues);
			FileContent.WriteLine();
			FileContent.WriteLine(strRCYLine01);
			FileContent.WriteLine();
			strRCYLine02Repeat = "//temp = ";
			strRCYLine02Tail = " ^C";
			for (int it = 0; it < listfTemp.Count; it++)
			{
				fRCYLine02Temp = listfTemp[it];
				FileContent.WriteLine(strRCYLine02Repeat + fRCYLine02Temp.ToString() + strRCYLine02Tail);
				for (int ic = 0; ic < listfCurr.Count; ic++)
				{
					ConvertRCDataToString(ref strRCYValues, rcYval[it * listfCurr.Count + ic]);
					FileContent.WriteLine(strRCYValues);
				}

				FileContent.WriteLine();
			}

			FileContent.Close();
			*/

			bReturn = true;

			return bReturn;
		}

		private void ConvertRCDataToString(ref string strOutput, List<Int32> inVal)
		{
			strOutput = "";

			foreach (Int32 iy in inVal)
			{
				strOutput += iy.ToString() + ",";
			}
			//(A140917)Francis, bugid=15206, delete last comma ','
			strOutput = strOutput.Substring(0, strOutput.Length - 1);
			//(E140917)
		}

		private void ConvertRCAllToString(List<List<Int32>> rcYval)
		{
			string strrctmp = "";
			float fTmp;
			int iCRate = 10000;
			bool bCRate = false;

			strRCContent.Clear();
			strRCContent.Add(string.Format(""));
			strRCContent.Add(string.Format("//Table 1 header: residual capacity lookup"));
			strRCContent.Add(string.Format(""));
			strRCContent.Add(string.Format("7 \t\t //DO NOT CHANGE: word length of header (including this length)"));
			strRCContent.Add(string.Format("1 \t\t //control word"));
			strRCContent.Add(string.Format("3 \t\t //number of axii"));
			strRCContent.Add(string.Format("{0} \t\t //'x' voltage axis entries", TableVoltagePoints.Count));
			strRCContent.Add(string.Format("{0} \t\t //'w' current axis entries", listfCurr.Count));
			strRCContent.Add(string.Format("{0} \t\t //'v' temp axis entries", listfTemp.Count));
			strRCContent.Add(string.Format("1 \t\t //'y' axis entries per 'x' axis entries"));
			strRCContent.Add(string.Format(""));
			strRCContent.Add(string.Format("//[Data]"));
			strRCContent.Add(string.Format("//'x' axis: voltage in mV "));
			TableVoltagePoints.Sort();
			strrctmp = "";
			foreach(UInt32 uvol in TableVoltagePoints)
			{
				strrctmp += string.Format("{0}, ", uvol);
			}
			//(A140917)Francis, bugid=15206, delete last comma ','
			strrctmp = strrctmp.Substring(0, strrctmp.Length - 2);
			//(E140917)
			strRCContent.Add(strrctmp);
			strRCContent.Add(string.Format(""));
			strrctmp = "";
			listfCurr.Sort();
			listfCurr.Reverse();
			foreach (float fcur in listfCurr)
			{
				fTmp = ((fcur * (-1)) / fDesignCapacity) * iCRate;
				if (fTmp > 32767)
				{
					iCRate = 100;
					bCRate = true;
					break;
				}
				strrctmp += string.Format("{0}, ", (int)(fTmp+0.5));
			}
			if (bCRate)	//need to change to 100C
			{
				strrctmp = "";
				foreach (float fcur2 in listfCurr)
				{
					fTmp = ((fcur2 * (-1)) / fDesignCapacity) * iCRate;
					strrctmp += string.Format("{0}, ", (int)(fTmp+0.5));
				}
			}
			//(A140917)Francis, bugid=15206, delete last comma ','
			strrctmp = strrctmp.Substring(0, strrctmp.Length - 2);
			//(E140917)
			strRCContent.Add(string.Format("//'w' axis: current in {0}*C (minor axis of 2d  lookup)", iCRate));
			strRCContent.Add(strrctmp);
			strRCContent.Add(string.Format(""));
			strRCContent.Add(string.Format("//'v' axis: temperature (major axis of 2d lookup) in .1 degrees C"));
			listfTemp.Sort();		//no need, just for case
			strrctmp = "";
			foreach (float ftemper in listfTemp)
			{
				if(ftemper >=0)
					strrctmp += string.Format("{0}, ", Convert.ToInt32(Math.Round(ftemper * 10, 0)));
				else
					strrctmp += string.Format("{0}, ", Convert.ToInt32(Math.Round(ftemper * 10, 0)));
			}
			//(A140917)Francis, bugid=15206, delete last comma ','
			strrctmp = strrctmp.Substring(0, strrctmp.Length - 2);
			//(E140917)
			strRCContent.Add(strrctmp);
			strRCContent.Add(string.Format(""));
			strRCContent.Add(string.Format("//capacity in 10000*C"));
			strRCContent.Add(string.Format(""));

			for (int it = 0; it < listfTemp.Count; it++)
			{
				strRCContent.Add(string.Format("//temp = {0} ^C", listfTemp[it]));
				for (int ic = 0; ic < listfCurr.Count; ic++)
				{
					strrctmp = ""; 
					ConvertRCDataToString(ref strrctmp, rcYval[it * listfCurr.Count + ic]);
					strRCContent.Add(strrctmp);
				}
				strRCContent.Add(string.Format(""));
			}

		}

	}

	public class ChargeSample : TableInterface
	{
		#region private constant definition
		//private string sFileSeperator = "_";
		//private string sCommentTab = "\t";
		//private string sValueSeperator = ",";
		//private string sChangeLine = "\r\n";
		#endregion

		#region static public constant definition, 
		//static public int iNumOfPoints = 65;
		//static public int iInterval = 64;
		//static public int iMinPercent = 0;
		//static public int iMaxPercent = 10000;
		//static public float fPerSteps = 1.5625F;
		//static public int iSOCStepmV = 16;
		#endregion

		#region private member definition
		private string strChgFileName { get; set; }
		#region  Charge table content string
		private List<string> strChgHeader = new List<string>();
		private List<string> strChgContent = new List<string>();
		#endregion
		#endregion

		#region public members definition
		public List<UInt32> iCHCurr = new List<UInt32>();
		public List<UInt32> iCHSOC = new List<UInt32>();
		#endregion

		public ChargeSample()
		{
			TableSourceData = new List<SourceDataSample>();
			TableSourceData.Clear();
			TableVoltagePoints = new List<UInt32>();
			TableVoltagePoints.Clear();
			TableType = TypeEnum.ChargeRawType;
			TableSourceHeader = null;
			TableOutputFolder = "";
		}

		public override bool InitializeTable(List<UInt32> inVoltChgPnts, ref uint uErr, string strOutputFolder = null)
		{
			bool bReturn = false;

			if (TableSourceData.Count == 0)
			{
				uErr = LibErrorCode.IDS_ERR_TMK_OCV_SOURCE_EMPTY;
				CreateNewErrorLog(" ", UInt32.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, uErr);
			}
			else if (TableSourceData.Count > 1)
			{
				uErr = LibErrorCode.IDS_ERR_TMK_OCV_SOURCE_MANY;
				CreateNewErrorLog(" ", UInt32.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, uErr);
			}
			else if (inVoltChgPnts.Count < 2)
			{
				LibErrorCode.uVal02 = (UInt32)inVoltChgPnts.Count;
				uErr = LibErrorCode.IDS_ERR_TMK_CHG_INPUT_CURRENT_LESS;
				CreateNewErrorLog(" ", UInt32.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, uErr);
			}
			else
			{
				TableVoltagePoints = inVoltChgPnts;	//in charge table, TableVoltagePoints is using to save current value that user key in input box
				TableVoltagePoints.Sort();
				iMinVoltage = Convert.ToInt32(TableVoltagePoints[0]);	//no used in charge table
				iMaxVoltage = Convert.ToInt32(TableVoltagePoints[1]);	//no used in charge table
				bReturn = true;
				uErr = LibErrorCode.IDS_ERR_SUCCESSFUL;
				TableSourceHeader = TableSourceData[0].myHeader;
				if ((strOutputFolder != null) && (Directory.Exists(strOutputFolder)))
				{
					TableOutputFolder = strOutputFolder;
				}
				else
				{
					TableOutputFolder = TableSourceData[0].strOutputFolder;
				}
				//(D141024)Francis, due to some comments are added when user is pressing 'Make' button. 
				// Move Initialize FileName and Comment content to BuildTable() funciton
				//InitializeChargeTable();
				//bReturn = InitializeAndroidDriver(ref uErr);	//(A140722)Francis, support Android Driver
				//(E141024)
				strTester = TableSourceHeader.strTester;
				strBatteryID = TableSourceHeader.strBatteryID;		//(A140729)Francis, add battery id
			}

			return bReturn;
		}

		public override bool BuildTable(ref UInt32 uErr, List<string> mkParamString)
		{
			bool bReturn = false;

			//(A141024)Francis, due to some comments are added when user is pressing 'Make' button. 
			// Move Initialize FileName and Comment content to BuildTable() funciton
			PrepareUserInput(uCHGVer, mkParamString);
			InitializeChargeTable();
			bReturn = InitializeAndroidDriver(ref uErr, this);	//(A140722)Francis, support Android Driver, basically return true
			//if (!bReturn)
				//return bReturn;
			//(E141024)

			iCHCurr.Clear();
			iCHSOC.Clear();
			TableSourceData[0].CreateSoCXls(TableOutputFolder, ref uErr);
			//if (CreateCHGSOCPoints(TableSourceData[0].AdjustedExpData, ref uErr))
			bReturn = CreateCHGSOCPoints(TableSourceData[0].AdjustedExpData, ref uErr);
			{
				//(D141120)Francis, seperate create data points and generate txt file into 2 API
				//bReturn = true;
				/*
				if (GenerateChargeTable(ref uErr))
				{
					Record4Versions(VersionEnum.VerEnmCHG);
					myAndroidDriver.MakeChgContent(iCHCurr, iCHSOC);
					bReturn = myAndroidDriver.MakeDriver(ref uErr, TableOutputFolder);
					if (bReturn)
					{
						//(A141024)Francis, check bMakeTable value, if true, means Android Driver C/H file are created
						if (myAndroidDriver.GetMakeTableStatus())
						{
							Record4Versions(VersionEnum.VerEnmTable);
						}
						//(E141024)
						//uErr = LibErrorCode.IDS_ERR_SUCCESSFUL;
					}
				}
				*/
			}

			//if (bReturn)
				bBuildOK = true;

			return bReturn;
		}

		public override bool GenerateFile(ref uint uErr)
		{
			bool bReturn = false;

			//(M141120)Francis, if true, make bBuildOK
			if (!bBuildOK)
			{
				uErr = LibErrorCode.IDS_ERR_TMK_TBL_BUILD_SEQUENCE;
				CreateNewErrorLog(strChgFileName, UInt32.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, uErr);
				return bReturn;
			}
			bBuildOK = false;	//set as false, no matter Generate successfully or not
			//(E141120)
			if (GenerateChargeTable(ref uErr))
			{
				Record4Versions(VersionEnum.VerEnmCHG);
				myAndroidDriver.MakeChgContent(iCHCurr, iCHSOC);
                bReturn = myAndroidDriver.MakeDriver(ref uErr, TableOutputFolder, bVTRboth);
				if (bReturn)
				{
					//(A141024)Francis, check bMakeTable value, if true, means Android Driver C/H file are created
					if (myAndroidDriver.GetMakeTableStatus())
					{
						Record4Versions(VersionEnum.VerEnmTable);
					}
					//(E141024)
					//uErr = LibErrorCode.IDS_ERR_SUCCESSFUL;
				}
			}

			return bReturn;
		}

        private void InitializeChargeTable()
		{
			strChgFileName = "Charge" + sFileSeperator + TableSourceHeader.Line04Content +
				sFileSeperator + TableSourceHeader.Line05Content +
				sFileSeperator + TableSourceHeader.fFullCapacity.ToString() + "mAhr" +
				sFileSeperator + TableSourceHeader.strLimitChgVolt + "mV" +
				sFileSeperator + TableSourceHeader.strCutoffDsgVolt + "mV" +
				//sFileSeperator + "V003" +
				sFileSeperator + TableMaker.TableSample.strTBMVersion + 
				sFileSeperator + DateTime.Now.Year.ToString("D4") +
				DateTime.Now.Month.ToString("D2") + DateTime.Now.Day.ToString("D2") +
				".txt";
			strChgFileName = System.IO.Path.Combine(TableOutputFolder, strChgFileName);

			strChgHeader.Clear();
			strChgHeader.Add(string.Format("//[Description] "));
			strChgHeader.Add(string.Format("//Charge Table as a reference of cell capacity"));
			strChgHeader.Add(string.Format("//This table is used charging stage to determine remaining cell capacity as a fraction of full capacity."));
			strChgHeader.Add(string.Format(""));
			strChgHeader.Add(string.Format("//Please note that cell must in charging mode to using this table."));
			strChgHeader.Add(string.Format(""));
			strChgHeader.Add(string.Format("//Table Header Information:"));
			strChgHeader.Add(string.Format(""));
			strChgHeader.Add(string.Format("//Manufacturer = {0}", TableSourceHeader.strManufacture));
			strChgHeader.Add(string.Format("//Battery Type = {0}", TableSourceHeader.strBatteryModel));
			strChgHeader.Add(string.Format("//Equipment = {0}", TableSourceHeader.strEquip));
			strChgHeader.Add(string.Format("//Built Date = {0} {1} {2}", DateTime.Now.Year.ToString("D4"), DateTime.Now.Month.ToString("D2"), DateTime.Now.Day.ToString("D2")));
			strChgHeader.Add(string.Format("//MinimalVoltage = {0}", TableSourceHeader.strCutoffDsgVolt));
			strChgHeader.Add(string.Format("//MaximalVoltage = {0}", TableSourceHeader.strLimitChgVolt));
			strChgHeader.Add(string.Format("//FullAbsoluteCapacity = {0}", TableSourceHeader.strAbsMaxCap));
			strChgHeader.Add(string.Format("//Age = {0}", TableSourceHeader.strCycleCount));
			strChgHeader.Add(string.Format("//Tester = {0}", strTester));
			strChgHeader.Add(string.Format("//Battery ID = {0}", strBatteryID));
			//(A141024)Francis, add Version/Date/Comment 3 string into header comment of txt file
			strChgHeader.Add(string.Format("//Version = {0}", strUserVersion));
			strChgHeader.Add(string.Format("//Date = {0}", strUserDate));
			//strChgHeader.Add(string.Format("//Comment = {0}", strUserComment));
			strChgHeader.Add(string.Format("//Comment = "));
			string[] var = strUserComment.Split('\n');
			foreach (string str in var)
			{
				strChgHeader.Add(string.Format("//          {0}", str.Replace('\r', ' ')));
			}
			//(E141024)
			strChgHeader.Add(string.Format(""));
		}

		private bool CreateCHGSOCPoints(List<RawDataNode> inListRaw, ref UInt32 uErr)
		{
			bool bReturn = false;
			float fcur1, fcur2, facc1, facc2, ftacc;
			float fmincur = 1000000F, fmaxcur = 0;
			int idxRDN = 0;
			float fExpMaxAccmah = 0F;

			TableVoltagePoints.Sort();
			inListRaw.Sort(delegate(RawDataNode x, RawDataNode y)
				{
					return -x.uSerailNum.CompareTo(y.uSerailNum); 
				});

			//(M140917)Francis, bugid=15206, SoC calculation in Charge Table calculation, is divided by Max Accumulated Capacity in Experiment data, instead of AbosuteMaxCap in header
			foreach (RawDataNode rdnlist in inListRaw)
			{
				if (rdnlist.fAccMah > fExpMaxAccmah)
				{
					fExpMaxAccmah = rdnlist.fAccMah;
				}
			}
			//(E140917)
			uErr = LibErrorCode.IDS_ERR_SUCCESSFUL;
			foreach (UInt32 ucur in TableVoltagePoints)
			{
				fcur1 = 0F; fcur2 = 0F; facc1 = 0F; facc2 = 0F; ftacc = 0F;
				for(;idxRDN<inListRaw.Count; idxRDN++)
				{
					#region get max/min current in experiment log data
					if ((Math.Abs(inListRaw[idxRDN].fVoltage - TableSourceHeader.fLimitChgVolt) < 10F) && 
						(inListRaw[idxRDN].fCurrent > 0))
					{
						if(fmincur > inListRaw[idxRDN].fCurrent)
						{
							fmincur = inListRaw[idxRDN].fCurrent;
						}
					}
					if((Math.Abs(inListRaw[idxRDN].fVoltage - TableSourceHeader.fCutoffDsgVolt) < 10F) &&
						(inListRaw[idxRDN].fCurrent > 0))
					{
						if(fmaxcur < inListRaw[idxRDN].fCurrent)
						{
							fmaxcur = inListRaw[idxRDN].fCurrent;
						}
					}
					#endregion
					#region check input current is between experiment log data
					if (fmincur < 1000000F)
					{
						if (ucur < fmincur)
						{
							LibErrorCode.uVal01 = ucur;
							LibErrorCode.fVal01 = fmincur;
							uErr = LibErrorCode.IDS_ERR_TMK_CHG_INPUT_CURRENT_SMALL;
							break;
						}
					}
					if (fmaxcur > 0)
					{
						if (ucur > fmaxcur)
						{
							LibErrorCode.uVal01 = ucur;
							LibErrorCode.fVal01 = fmincur;
							uErr = LibErrorCode.IDS_ERR_TMK_CHG_INPUT_CURRENT_BIG;
							break;
						}
					}
					#endregion
					if (inListRaw[idxRDN].fCurrent >= ucur)
					{
						fcur2 = inListRaw[idxRDN].fCurrent;
						facc2 = inListRaw[idxRDN].fAccMah;
						break;		//break for(;idxRDN<inListRaw.Count; idxRDN++)
					}
					else
					{
						fcur1 = inListRaw[idxRDN].fCurrent;
						facc1 = inListRaw[idxRDN].fAccMah;
					}
				}	//foreach (RawDataNode rdex in inListRaw)
				if (uErr != LibErrorCode.IDS_ERR_SUCCESSFUL)
				{
					CreateNewErrorLog(TableSourceData[0].strSourceFilePath, UInt32.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, uErr);
					break;	////break foreach (UInt32 ucur in TableVoltagePoints)
				}
				else if (fcur2 == facc2)	//initialize fcur2 and facc2 are zero, if equal, means not found current is bigger than input current (in TableVoltagePoints)
				{
					LibErrorCode.uVal01 = ucur;
					LibErrorCode.fVal01 = fmincur;
					LibErrorCode.fVal02 = fmaxcur;
					uErr = LibErrorCode.IDS_ERR_TMK_CHG_INPUT_CURRENT_NOTFOUND;
					CreateNewErrorLog(TableSourceData[0].strSourceFilePath, UInt32.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, uErr);
					break;
				}
				else
				{
					ftacc = facc1;
					if( fcur1 != facc1)	//fcur1 and facc1 intialization are all zero
					{	//had found one experiment point, that current is less than input currrent point; therefore use interpolation
						fcur1 = (ucur - fcur1) / (fcur2 - fcur1);
						fcur1 *= (facc2 - facc1);
						ftacc += fcur1;
					}
					else
					{
						ftacc = facc2;	//replace 1st point
					}
					ftacc *= 10000;
					//(M140917)Francis, bugid=15206, SoC calculation in Charge Table calculation, is divided by Max Accumulated Capacity in Experiment data, instead of AbosuteMaxCap in header
					//ftacc /=  TableSourceHeader.fAbsMaxCap;
					ftacc /= fExpMaxAccmah;
					//(E140917)
					iCHCurr.Add(ucur);
					iCHSOC.Add(Convert.ToUInt32(Math.Round(ftacc, 0)));
				}	//	if (uErr != LibErrorCode.IDS_ERR_SUCCESSFUL) else if (fcur2 == facc2)
			}	//foreach (UInt32 ucur in TableVoltagePoints)

			//restore its origianl sequence
			inListRaw.Sort(delegate(RawDataNode x, RawDataNode y)
			{
				return x.uSerailNum.CompareTo(y.uSerailNum);
			});

			if (uErr == LibErrorCode.IDS_ERR_SUCCESSFUL)
			{
				bReturn = true;
			}

			return bReturn;
		}

		private bool GenerateChargeTable(ref UInt32 uErr)
		{
			bool bReturn = false;
			FileStream fsChgT = null;
			StreamWriter stmChgT = null;

			ConvertChgValueToString();
			try
			{
				fsChgT = File.Open(strChgFileName, FileMode.Create, FileAccess.Write, FileShare.None);
				stmChgT = new StreamWriter(fsChgT, Encoding.Unicode);
			}
			catch (Exception eh)
			{
				LibErrorCode.strVal01 = strChgFileName;
				uErr = LibErrorCode.IDS_ERR_TMK_CHG_CREATE_FILE;
				CreateNewErrorLog(TableSourceData[0].strSourceFilePath, UInt32.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, uErr);
				return bReturn;
			}

			foreach (string stch in strChgHeader)
			{
				stmChgT.WriteLine(stch);
			}
			foreach (string stcc in strChgContent)
			{
				stmChgT.WriteLine(stcc);
			}

			stmChgT.Close();
			fsChgT.Close();
			uErr = LibErrorCode.IDS_ERR_SUCCESSFUL;
			bReturn = true;

			return bReturn;
		}

		private void ConvertChgValueToString()
		{
			string strctmp = "";

			strChgContent.Clear();
			strChgContent.Add(string.Format(""));
			strChgContent.Add(string.Format("//data header"));
			strChgContent.Add(string.Format(""));
			strChgContent.Add(string.Format("5 \t\t //DO NOT CHANGE: word length of header (including this length)"));
			strChgContent.Add(string.Format("1 \t\t //DO NOT CHANGE: control, use as scale control "));
			strChgContent.Add(string.Format("1 \t\t //DO NOT CHANGE: number of axis"));
			strChgContent.Add(string.Format("{0} \t\t //x axis points: maximum 65 points", TableVoltagePoints.Count));
			strChgContent.Add(string.Format("1 \t\t //DO NOT CHANGE: y axis entries per x axis"));
			strChgContent.Add(string.Format(""));
			strChgContent.Add(string.Format("//x (independent) axis: Current(mA)"));
			strChgContent.Add(string.Format(""));
			strctmp = "";
			foreach (UInt32 icr in iCHCurr)
			{
				strctmp += string.Format("{0},", icr);
			}
			//(A140917)Francis, bugid=15206, delete last comma ','
            if(strctmp.Length > 1)
    			strctmp = strctmp.Substring(0, strctmp.Length - 1);
			//(E140917)
			strChgContent.Add(strctmp);
			strChgContent.Add(string.Format(""));
			strChgContent.Add(string.Format(""));
			strChgContent.Add(string.Format(""));
			strChgContent.Add(string.Format("//y (dependent) axis: "));
			strChgContent.Add(string.Format(""));
			strctmp = "";
			foreach (UInt32 ica in iCHSOC)
			{
				strctmp += string.Format("{0},", ica);
			}
			//(A140917)Francis, bugid=15206, delete last comma ','
			strctmp = strctmp.Substring(0, strctmp.Length - 1);
			//(E140917)
			strChgContent.Add(strctmp);
			strChgContent.Add(string.Format(""));
		}
	}

    //(A170313)Francis,
    public class FalconLYSample : TableInterface
    {
        #region private members definition

        #region OCV table header information
        //private string strOCVHdManufacturer = "";
        //private string strOCVHdBatteryType = "";
        //private string strOCVHdEquipment = "";
        //private string strOCVHdBuiltDate = "";
        //private string strOCVHdMinimalVolt = "";
        //private string strOCVHdMaximalVolt = "";
        //private string strOCVHdFullAbsCapacity = "";
        //private string strOCVHdAge = "";
        //private string strOCVHdTester = "";
        //private string strOCVHdBatteryID = "";
        //private string strOCVHdVersion = "";
        //private string strOCVHdDate = "";
        //private string strOCVHdComment = "";

        //private UInt32 uOCVHeaderLength = 0;
        //private UInt32 uOCVHControlValue = 0;
        //private UInt32 uOCVHNumofAxis = 0;
        //private UInt32 uOCVHPointsofAxis = 0;
        //private UInt32 uOCVHYEntryPerAxis = 0;
        private UInt32 uOCVTableXPointNum = 0;
        private UInt32 uOCVTableYPointNum = 0;
        #endregion

        #region RC table header information
        //private UInt32 uRCHeaderLength = 0;
        //private UInt32 uRCHControlValue = 0;
        //private UInt32 uRCHNumofAxis = 0;
        //private UInt32 uRCHXAxisVoltNum = 0;
        //private UInt32 uRCHWAxisCurrNum = 0;
        //private UInt32 uRCHVAxisTempNum = 0;
        //private UInt32 uRCHYAxisEntriesNum = 0;
        private UInt32 uRCTableXPointNum = 0;
        private UInt32 uRCTableWPointNum = 0;
        private UInt32 uRCTableVPointNum = 0;
        private UInt32 uRCTableYPointNum = 0;
        #endregion

        #region CH file content information
        private List<string> strHFileFalconLYContent = new List<string>();
        private List<string> strCFileFalconLYContent = new List<string>();
        #endregion

        #region battery header information
        private string strManufacture = "";
        private string strBatteryModel = "";
        private float fDesignCapacity = -9999.0F;
        private float fCapaciDiff = -9999.0F;
        private bool bInitialized = false;			//for record that InitializeTable() function is working OK
        #endregion

        private bool bOCVTxtFileReady;
        private bool bRCTxtFileReady;
        private bool bCHFilesReady;

        private List<SourceDataHeader> sdhFromMyOldTable;
        private List<string> lstrFromUser;
        private string strDriverTempFileFullPath = string.Empty;

        #endregion

        public FalconLYSample() : base()
		{
			//TableSourceData = new List<SourceDataSample>();
			//TableSourceData.Clear();
            //TableVoltagePoints = new List<UInt32>();
			//TableVoltagePoints.Clear();
			//TableType = TypeEnum.RCRawType;
			//TableSourceHeader = null;
			TableOutputFolder = "";
			bInitialized = false;
            //in case we are using TableSourceHeader
            TableSourceHeader = new SourceDataHeader();
            bOCVTxtFileReady = false;
            bRCTxtFileReady = false;
            bCHFilesReady = false;
            sdhFromMyOldTable = new List<SourceDataHeader>();
            sdhFromMyOldTable.Clear();
            lstrFromUser = new List<string>();
            lstrFromUser.Clear();
        }

        //override TableInterface function, this function is designed originally to read raw data and user input voltages,
        //then calculate raw data/input voltage to generate table points. We can skip uVoltPoints
        public override bool InitializeTable(List<UInt32> uVoltPoints, ref UInt32 uErr, string strOutputFolder = null)
        {
            bool bReturn = true;

            //InitializeTRTable();
            if ((strOutputFolder != null) && (Directory.Exists(strOutputFolder)))
            {
                TableOutputFolder = strOutputFolder;
            }
            else
            {
                TableOutputFolder = "";
            }
            InitializeOCVFalconLYTable(TableOutputFolder);
            myAndroidDriver = new AndroidDriverSample(TypeEnum.FalconLYType, null, null, this);
            myAndroidDriver.iIndxInFullPathList = 3;    //protend we are 4th type Falconly
            myAndroidDriver.strTmpFullPathList.Clear();
            uErr = LibErrorCode.IDS_ERR_SUCCESSFUL;

            return bReturn;
        }

        //override TableInterface function
        public override bool BuildTable(ref UInt32 uErr, List<string> mkParamString)
        {
            bool bReturn = true;

            if ((bOCVTxtFileReady & bRCTxtFileReady) || (bCHFilesReady))
            {
                myAndroidDriver.SetupUserInput(mkParamString);
                myAndroidDriver.InitializeHeaderbyOldCHFile(ref uErr, ref tInsError, sdhFromMyOldTable);
                myAndroidDriver.InitializedTableH();
                myAndroidDriver.InitializedTableC();

                if (bCHFilesReady)
                {
                    myAndroidDriver.strTmpFullPathList.Add(strDriverTempFileFullPath);
                    myAndroidDriver.strTmpFullPathList.Add(strDriverTempFileFullPath);
                    myAndroidDriver.strTmpFullPathList.Add(strDriverTempFileFullPath);
                    bReturn &= myAndroidDriver.ReadOCVValueIntoVariables(ref uOCVPercentPoints, ref iOCVVolt);
                    for (int i = 0; i < uOCVPercentPoints.Count; i++ )
                    {
                        uOCVPercentPoints[i] *= 100;
                    }
                    bReturn &= myAndroidDriver.ReadRCValueIntoVariables(ref listfTemp, ref listfCurr, ref uRCVoltagePoints, ref iYPointsall);
                    PrepareOCVtxtHeader(mkParamString);
                }

                ConvertVTRandTR();  //always return true
                ConvertOCVFalconLYContent(iOCVVolt);
                bReturn = GenerateOCVFalconLYTableFile(ref uErr);   //generate FalconLY used OCV file
                //if(bReturn)
                bReturn &= GenerateTRTableFile(ref uErr, TableOutputFolder);    //generate TR table file
                if (bCHFilesReady)
                {
                    //prepare Android driver c data, cause there is no chg txt file, so cannot make complete driver c/h file
                    myAndroidDriver.MakeOCVContent(uOCVPercentPoints, iOCVVolt);
                    myAndroidDriver.MakeRCContent(uRCVoltagePoints, listfCurr, listfTemp, iYPointsall);
                    myAndroidDriver.MakeIRRTableContent(uRCVoltagePoints, listfTemp, ilistVTRPoints, ilistTRPoints);
                    bReturn &= myAndroidDriver.GenerateDriverFiles(ref uErr, TableOutputFolder, true, true);
                }
                //clear table OK flag after tables are built
                bOCVTxtFileReady = false;
                bRCTxtFileReady = false;
                bCHFilesReady = false;
                sdhFromMyOldTable.Clear();
            }

            return bReturn;
        }

        public override bool GenerateFile(ref uint uErr)
        {
            bool bReturn = false;

            return bReturn;
        }

        public bool readNCheckOCVtxtFile(string strOCFFileFull, ref UInt32 uErr)
        {
            bool bReturn = false;
            bool bContentStarts = false;
            bool bDoneOne = false;
            int iIndex = -1;
            Stream stmFile = null;
            StreamReader stmreadFile = null;
            string strTemp;
            string[] strSpliter;
            Int32 i16Temp;
            //Int32 i32Temp;

            try
            {
                LibErrorCode.strVal01 = strOCFFileFull;
                stmFile = File.Open(strOCFFileFull, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                stmreadFile = new StreamReader(stmFile);
            }
			catch (Exception ef)
			{
                uErr = LibErrorCode.IDS_ERR_TMK_TR_READ_OCV_FILE;
				return bReturn;
			}

            strOCVHeader.Clear();
            strOCVContent.Clear();
            iOCVVolt.Clear();
            uOCVPercentPoints.Clear();
            bReturn = true;
            uErr = LibErrorCode.IDS_ERR_SUCCESSFUL;
            
            while ((strTemp = stmreadFile.ReadLine()) != null)
			{
                //parseCommentandHeader(ref stmreadFile);
                //strTemp = stmreadFile.ReadLine();
                if (strTemp.StartsWith("//") || (strTemp.Length < 1))
                {
                    //here is comment
                    if (!bContentStarts)
                        strOCVHeader.Add(strTemp);
                    else
                        strOCVContent.Add(strTemp);
                }
                else
                {
                    //here is content
                    bContentStarts = true;
                    iIndex = strTemp.IndexOf("//");
                    if(iIndex != -1)
                    {
                        //here is header value of table
                        //header value is defined format, dont need to read it and parse it, just generate it when table value is created
                    }
                    else
                    {
                        //here is table value
                        strSpliter = strTemp.Split(',');
                        if(!bDoneOne)
                        {
                            foreach(string strone in strSpliter)
                            {
                                if (!Int32.TryParse(strone, out i16Temp))
                                {
                                    i16Temp = -1;
                                    bReturn = false;
                                    uErr = LibErrorCode.IDS_ERR_TMK_TR_OCV_VOLT_DATA;
                                    break;
                                }
                                uOCVPercentPoints.Add(i16Temp);
                            }
                            uOCVTableXPointNum = (UInt32)strSpliter.Length; //?
                            bDoneOne = true;
                        }
                        else
                        {
                            foreach (string strone in strSpliter)
                            {
                                if (!Int32.TryParse(strone, out i16Temp))
                                {
                                    i16Temp = -1;
                                    bReturn = false;
                                    uErr = LibErrorCode.IDS_ERR_TMK_TR_OCV_PERCENT_DATA;
                                    break;
                                }
                                iOCVVolt.Add(i16Temp);
                            }
                            uOCVTableYPointNum = (UInt32)strSpliter.Length;
                        }
                    }
                }

            }

            stmFile.Close();
            stmreadFile.Close();

            if(uOCVTableXPointNum != uOCVTableYPointNum)
            {
                uErr = LibErrorCode.IDS_ERR_TMK_TR_OCV_POINTS_NOMATCH;
                bReturn = false;
            }

            if (!ParseHeaderInforFromFileComments(strOCVHeader, false))
            {
                uErr = LibErrorCode.IDS_ERR_TMK_TR_OCV_HEADER;
                bReturn = false;
            }

            foreach(int voltone in iOCVVolt)
            {
                if((voltone >= 5000) || (voltone <= 1000))
                {
                    uErr = LibErrorCode.IDS_ERR_TMK_TR_OCV_VOLT_OUTBOUND;
                    bReturn = false;
                    break;
                }
            }

            foreach (Int32 percent in uOCVPercentPoints)
            {
                if(percent > 10001)
                {
                    uErr = LibErrorCode.IDS_ERR_TMK_TR_OCV_PERCENT_OUTBOUND;
                    bReturn = false;
                    break;
                }
            }

            bOCVTxtFileReady = bReturn;
            if(bReturn) 
                sdhFromMyOldTable.Add(TableSourceHeader);

            return bReturn;
        }

        public bool readNCheckRCtxtFile(string strRCFileFull, ref UInt32 uErr)
        {
            bool bReturn = false;
            bool bContentStarts = false;
            int iDoneLines = 0;
            int iIndex = -1;
            Stream stmFile = null;
            StreamReader stmreadFile = null;
            string strTemp;
            string[] strSpliter;
            Int32 i16Temp;

            try
            {
                LibErrorCode.strVal01 = strRCFileFull;
                stmFile = File.Open(strRCFileFull, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                stmreadFile = new StreamReader(stmFile);
            }
            catch (Exception ef)
            {
                uErr = LibErrorCode.IDS_ERR_TMK_TR_READ_RC_FILE;
                return bReturn;
            }

            strRCHeader.Clear();
            strRCContent.Clear();
            listfTemp.Clear();
            listfCurr.Clear();
            foreach (List<Int32> yp in iYPointsall)
            {
                yp.Clear();
            }
            iYPointsall.Clear();
            if (uRCVoltagePoints == null)
                uRCVoltagePoints = new List<UInt32>();
            else
                uRCVoltagePoints.Clear();
            bReturn = true;
            uErr = LibErrorCode.IDS_ERR_SUCCESSFUL;

            while ((strTemp = stmreadFile.ReadLine()) != null)
            {
                //parseCommentandHeader(ref stmreadFile);
                //strTemp = stmreadFile.ReadLine();
                if (strTemp.StartsWith("//") || (strTemp.Length < 1))
                {
                    //here is comment
                    if (!bContentStarts)
                        strRCHeader.Add(strTemp);
                    else
                        strRCContent.Add(strTemp);
                }
                else
                {
                    //here is content
                    bContentStarts = true;
                    iIndex = strTemp.IndexOf("//");
                    if (iIndex != -1)
                    {
                        //here is header value of table
                        //header value is defined format, dont need to read it and parse it, just generate it when table value is created
                    }
                    else
                    {
                        //here is table value
                        strSpliter = strTemp.Split(',');
                        if (iDoneLines == 0)
                        {
                            foreach (string strone in strSpliter)
                            {
                                if (!Int32.TryParse(strone, out i16Temp))
                                {
                                    i16Temp = -1;
                                    bReturn = false;
                                    uErr = LibErrorCode.IDS_ERR_TMK_TR_RC_VOLT_DATA;
                                    break;
                                }
                                uRCVoltagePoints.Add((UInt32)i16Temp);
                            }
                            uRCTableXPointNum = (UInt32)strSpliter.Length;
                            iDoneLines += 1;
                        }
                        else if(iDoneLines == 1)
                        {
                            foreach (string strone in strSpliter)
                            {
                                if (!Int32.TryParse(strone, out i16Temp))
                                {
                                    i16Temp = -1;
                                    bReturn = false;
                                    uErr = LibErrorCode.IDS_ERR_TMK_TR_RC_CURR_DATA;
                                    break;
                                }
                                listfCurr.Add(i16Temp);
                            }
                            uRCTableWPointNum = (UInt32)strSpliter.Length;
                            iDoneLines += 1;
                            if(bReturn)
                            {
                                for (int i = 0; i<listfCurr.Count; i++)
                                {
                                    listfCurr[i] = listfCurr[i];// / 10000 * 3384; //should be ABS
                                }
                            }
                        }
                        else if (iDoneLines == 2)
                        {
                            foreach (string strone in strSpliter)
                            {
                                if (!Int32.TryParse(strone, out i16Temp))
                                {
                                    i16Temp = -1;
                                    bReturn = false;
                                    uErr = LibErrorCode.IDS_ERR_TMK_TR_RC_TEMP_DATA;
                                    break;
                                }
                                listfTemp.Add(i16Temp);
                            }
                            uRCTableVPointNum = (UInt32)strSpliter.Length;
                            iDoneLines += 1;
                            if(bReturn)
                            {
                                for (int i = 0; i < listfTemp.Count; i++ )
                                {
                                    listfTemp[i] /= 10;
                                }
                            }
                        }
                        else
                        {
                            List<Int32> lsYTmp = new List<Int32>();
                            foreach (string strone in strSpliter)
                            {
                                if (!Int32.TryParse(strone, out i16Temp))
                                {
                                    i16Temp = -1;
                                    bReturn = false;
                                    uErr = LibErrorCode.IDS_ERR_TMK_TR_RC_RC_DATA;
                                    break;
                                }
                                lsYTmp.Add(i16Temp);
                            }
                            iYPointsall.Add(lsYTmp);
                            //uRCTableVPointNum = (UInt32)strSpliter.Length;
                        }
                    }
                }

            }

            stmFile.Close();
            stmreadFile.Close();
            if (iYPointsall.Count != 0)
            {
                uRCTableYPointNum = (UInt32)(iYPointsall.Count * iYPointsall[0].Count);
                if (uRCTableYPointNum != (uRCTableVPointNum * uRCTableWPointNum * uRCTableXPointNum))
                {
                    uErr = LibErrorCode.IDS_ERR_TMK_TR_RC_POINTS_NOMATCH;
                    bReturn = false;
                }
            }
            else
            {
                uErr = LibErrorCode.IDS_ERR_TMK_TR_RC_RC_DATA;
                bReturn = false;
            }

            if (!ParseHeaderInforFromFileComments(strRCHeader))     //save in 2nd header
            {
                uErr = LibErrorCode.IDS_ERR_TMK_TR_RC_HEADER;
                bReturn = false;
            }

            if (SourceHeader2nd.fFullCapacity != 1)
            {
                for (int i = 0; i < listfCurr.Count; i++)
                {
                    listfCurr[i] = listfCurr[i] / 10000 * SourceHeader2nd.fFullCapacity;
                }
            }
            else
            {
                uErr = LibErrorCode.IDS_ERR_TMK_TR_RC_FULL_CAPACITY;
                bReturn = false;
            }

            bRCTxtFileReady = bReturn;
            if(bReturn) 
                sdhFromMyOldTable.Add(SourceHeader2nd);

            return bReturn;
        }

        public bool readNCheckCHFiles(List<string> strCHFileFull, ref UInt32 uErr)
        {
            bool bReturn = false, bCCommentStart = false, bCCommentEnd = false;
            string strCFileFullPath = string.Empty, strHFileFullPath = string.Empty;
            Stream stmFile = null;
            StreamReader stmreadFile = null;
            //StreamWriter stmwriteFile = null;
            string strTemp;
            List<string> lstrComments = new List<string>();

            for (int i = 0; i < strCHFileFull.Count; i++)
            {
                if(strCHFileFull[i].ToLower().IndexOf(".c") != -1)
                {
                    strCFileFullPath = strCHFileFull[i];
                }
                else if(strCHFileFull[i].ToLower().IndexOf(".h") != -1)
                {
                    strHFileFullPath = strCHFileFull[i];
                }
            }

            if((strCFileFullPath == string.Empty) || (strHFileFullPath == string.Empty))
            {
                LibErrorCode.strVal01 = strCFileFullPath;
                LibErrorCode.strVal02 = strHFileFullPath;
                uErr = LibErrorCode.IDS_ERR_TMK_TR_EMPTY_CH_FILE;
                return bReturn;
            }
            else
            {
                //clear content list
                strCFileFalconLYContent.Clear();
                strHFileFalconLYContent.Clear();

                #region try to open and parse h file,
                //try to open h file, then combine to *.tmp
                try
                {
                    stmFile = File.Open(strHFileFullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    stmreadFile = new StreamReader(stmFile);
                }
                catch (Exception ef)
                {
                    LibErrorCode.strVal01 = strHFileFullPath;
                    uErr = LibErrorCode.IDS_ERR_TMK_TR_READ_H_FILE;
                    return bReturn;
                }
                //and read content
                lstrComments.Clear();
                while ((strTemp = stmreadFile.ReadLine()) != null)
                {
                    if((strTemp.StartsWith("/*")) && (!bCCommentStart))
                    {
                        bCCommentStart = true;
                        bCCommentEnd = false;
                        if (strTemp.EndsWith("*/"))
                        {
                            bCCommentEnd = true;
                            continue;   //one line comment, read next line
                        }
                    }
                    else if((bCCommentStart) && (!bCCommentEnd) && (strTemp.EndsWith("*/")))
                    {
                        bCCommentEnd = true;
                    }
                    else
                    {
                        if((bCCommentStart) && (!bCCommentEnd))
                        {
                            //in comments, save in List to find battery information
                            lstrComments.Add(strTemp);
                        }
                        else if(bCCommentStart && bCCommentEnd)
                        {
                            //in contents
                            strHFileFalconLYContent.Add(strTemp);
                        }
                    }
                }
                stmreadFile.Close();
                stmFile.Close();
                #endregion

                #region try to open and parse c file
                try
                {
                    stmFile = File.Open(strCFileFullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    stmreadFile = new StreamReader(stmFile);
                }
                catch (Exception ef)
                {
                    LibErrorCode.strVal01 = strHFileFullPath;
                    uErr = LibErrorCode.IDS_ERR_TMK_TR_READ_C_FILE;
                    return bReturn;
                }
                //and read content
                //lstrComments.Clear();
                bCCommentStart = false;
                bCCommentEnd = false;
                while ((strTemp = stmreadFile.ReadLine()) != null)
                {
                    if ((strTemp.StartsWith("/*")) && (!bCCommentStart))
                    {
                        bCCommentStart = true;
                        bCCommentEnd = false;
                        if (strTemp.EndsWith("*/"))
                        {
                            bCCommentEnd = true;
                            continue;   //one line comment, read next line
                        }
                    }
                    else if ((bCCommentStart) && (!bCCommentEnd) && (strTemp.EndsWith("*/")))
                    {
                        bCCommentEnd = true;
                    }
                    else
                    {
                        if ((bCCommentStart) && (!bCCommentEnd))
                        {
                            //in comments, save in List to find battery information
                            //c, h files should have same comment, skip this
                            //lstrComments.Add(strTemp);
                        }
                        else if (bCCommentStart && bCCommentEnd)
                        {
                            //in contents
                            if (strTemp.IndexOf("* table_version") != -1)
                            {
                                strTemp = stmreadFile.ReadLine();
                            }
                            else if (strTemp.IndexOf("[YAxis*ZAxis][XAxis]") != -1)
                            {
                                strCFileFalconLYContent.Add(strTemp);
                                strTemp = stmreadFile.ReadLine();
                                strCFileFalconLYContent.Add(strTemp);
                                strCFileFalconLYContent.Add(string.Format("{0}", TableSourceHeader.strEquip));
                                strCFileFalconLYContent.Add(string.Format("{0}", TableSourceHeader.strTester));
                            }
                            else
                            {
                                strCFileFalconLYContent.Add(strTemp);
                            }
                        }
                    }
                }

                stmreadFile.Close();
                stmFile.Close();
                #endregion

                #region write to tmp file for utilizing existing AndroidDriverSample class
                ParseCommentInforFromCHFiles(lstrComments);

                //clear driver.tmp file then create a new one for writing
                strDriverTempFileFullPath = System.IO.Path.Combine(System.IO.Path.Combine(FolderMap.m_currentproj_folder, "Tmp\\"), "Android.driver.tmp");
                if (Directory.Exists(strDriverTempFileFullPath))
                    Directory.Delete(strDriverTempFileFullPath);
                bReturn = FictionTempDriver(strDriverTempFileFullPath, ref uErr);
                if (uRCVoltagePoints == null)
                    uRCVoltagePoints = new List<UInt32>();
                else
                    uRCVoltagePoints.Clear();
                #endregion
            }

            bCHFilesReady = bReturn;

            if (bReturn)
                sdhFromMyOldTable.Add(TableSourceHeader);

            return bReturn;
        }

        public bool FictionTempDriver(string strFileFullPath, ref UInt32 uErr)
        {
            bool bReturn = false;

            Stream stmFileWrite = null;
            StreamWriter stmWriter = null;

            try
            {
                stmFileWrite = File.Open(strFileFullPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                stmWriter = new StreamWriter(stmFileWrite);
            }
            catch (Exception ef)
            {
                LibErrorCode.strVal01 = strFileFullPath;
                uErr = LibErrorCode.IDS_ERR_TMK_TR_DRV_TMP_FILE;
                return bReturn;
            }

            stmWriter.WriteLine(TableSample.strTBMVersion);
            for (int i = 1; i <= 30; i++)
            {
                stmWriter.WriteLine(strHFileFalconLYContent[i]);
            }
            stmWriter.WriteLine(string.Format("#endif"));
            foreach (string strc in strCFileFalconLYContent)
            {
                stmWriter.WriteLine(strc);
            }

            stmWriter.Close();
            stmFileWrite.Close();
            bReturn = true;

            return bReturn;
        }

        /*
        private void parseCommentandHeader(ref StreamReader stmRead, string strCommentStart = "//")
        {
            string strTemp;


            do
            {
                strTemp = stmRead.ReadLine();
                if (strTemp.IndexOf(strCommentStart) != -1)
                {
                    break;
                }
                else
                {
                    if(strTemp.IndexOf())
                    {

                    }
                }
            } while (strTemp != null);
        }
        */

        private void PrepareOCVtxtHeader(List<string> lstrCommentsIn)
        {
            strOCVHeader.Clear();
            strOCVHeader.Add(string.Format("//[Description]"));
            strOCVHeader.Add(string.Format("// Open Circuit Voltage as a function of cell capacity"));
            strOCVHeader.Add(string.Format("// This table is used at initial startup only to determine remaining cell capacity as "));
            strOCVHeader.Add(string.Format("// a fraction of full capacity, based on the open curcuit (no load, rested) cell voltage. "));
            strOCVHeader.Add(string.Format(""));
            strOCVHeader.Add(string.Format("// Please note that the cell must not have been charged or discharged for several "));
            strOCVHeader.Add(string.Format("// hours prior to this remaining capacity determination, or remaining capacity may "));
            strOCVHeader.Add(string.Format("// be considerable in error"));
            strOCVHeader.Add(string.Format(""));
            strOCVHeader.Add(string.Format("//Table Header Information:"));
            strOCVHeader.Add(string.Format(""));
            strOCVHeader.Add(string.Format("//Manufacturer = {0}", TableSourceHeader.strManufacture));
            strOCVHeader.Add(string.Format("//Battery Type = {0}", TableSourceHeader.strBatteryModel));
            strOCVHeader.Add(string.Format("//Equipment = {0}", TableSourceHeader.strEquip));
            strOCVHeader.Add(string.Format("//Built Date = {0} {1} {2}", DateTime.Now.Year.ToString("D4"), DateTime.Now.Month.ToString("D2"), DateTime.Now.Day.ToString("D2")));
            strOCVHeader.Add(string.Format("//MinimalVoltage = {0}", TableSourceHeader.strCutoffDsgVolt));
            strOCVHeader.Add(string.Format("//MaximalVoltage = {0}", TableSourceHeader.strLimitChgVolt));
            strOCVHeader.Add(string.Format("//FullAbsoluteCapacity = {0}", TableSourceHeader.strAbsMaxCap));
            strOCVHeader.Add(string.Format("//Age = {0}", TableSourceHeader.strCycleCount));
            strOCVHeader.Add(string.Format("//Tester = {0}", TableSourceHeader.strTester));
            strOCVHeader.Add(string.Format("//Battery ID = {0}", strBatteryID));
            //(A141024)Francis, add Version/Date/Comment 3 string into header comment of txt file
            strOCVHeader.Add(string.Format("//Version = {0}", lstrCommentsIn[0]));
            strOCVHeader.Add(string.Format("//Date = {0}", lstrCommentsIn[1]));
            //strOCVHeader.Add(string.Format("//Comment = {0}", strUserComment));
            strOCVHeader.Add(string.Format("//Comment = "));
            string[] var = lstrCommentsIn[2].Split('\n');
            foreach (string str in var)
            {
                strOCVHeader.Add(string.Format("//          {0}", str.Replace('\r', ' ')));
            }
            //(E141024)
            strOCVHeader.Add(string.Format(""));
        }


        private void parseHeaderValue()
        {

        }
    }
    //(E170313)

	public class AndroidDriverSample
	{
		#region H/C file content
		private List<string> strHHeaderComments = new List<string>(); //header comment of H & C file are same
		private int iLineCmtHCFile = 4;
		private int iLineCmtEquip = 26;
		private int iLineCmtTester = 27;
		private int iLineCmtBId = 28;
		private List<string> strHFileContents = new List<string>();
		private int iLineOCVNum = 4;
		private int iLineCHGNum = 5;
		private int iLineXNum = 8;
		private int iLineYNum = 9;
		private int iLineZNum = 10;
		private int iLineIDNum = 12;
        private UInt32 m_uOCVNumHF;
        public UInt32 uOCVNumHF
		{
			get { return m_uOCVNumHF;}
			set { 
				m_uOCVNumHF = value;
				strHFileContents[iLineOCVNum] = string.Format("#define OCV_DATA_NUM \t {0}", m_uOCVNumHF);
			}
		}
        private UInt32 m_uChargeNumHF;
        public UInt32 uChargeNumHF
		{
			get { return m_uChargeNumHF;}
			set { 
				m_uChargeNumHF = value;
				strHFileContents[iLineCHGNum] = string.Format("#define CHARGE_DATA_NUM \t\t {0}", m_uChargeNumHF);
			}
		}
        private UInt32 m_uXNumHF;
        public UInt32 uXNumHF
		{
			get { return m_uXNumHF;}
			set { 
				m_uXNumHF = value;
				strHFileContents[iLineXNum] = string.Format("#define XAxis \t\t {0}", m_uXNumHF);
			}
		}
        private UInt32 m_uYNumHF;
        public UInt32 uYNumHF
		{
			get { return m_uYNumHF;}
			set { 
				m_uYNumHF = value;
				strHFileContents[iLineYNum] = string.Format("#define YAxis \t\t {0}", m_uYNumHF);
			}
		}
        private UInt32 m_uZNumHF;
        public UInt32 uZNumHF
		{
			get { return m_uZNumHF;}
			set { 
				m_uZNumHF = value;
				strHFileContents[iLineZNum] = string.Format("#define ZAxis \t\t {0}", m_uZNumHF);
			}
		}
        private UInt32 m_uIDNumHF;
        public UInt32 uIDNumHF
		{
			get { return m_uIDNumHF; }
			set
			{
				m_uIDNumHF = value;
				strHFileContents[iLineIDNum] = string.Format("#define BATTERY_ID_NUM \t\t {0}", m_uIDNumHF);
			}
		}

		private List<string> strCFileContents = new List<string>();
		private int iLineIDCont = 9;
		private int iLineOCVCont = 11;
		private int iLineCHGCont = 14;
		private int iLineXCont = 17;
		private int iLineYCont = 20;
		private int iLineZCont = 23;
		private int iLineRCCont = 27;
		private string m_IDContCF;
		public string IDContCF
		{
			get { return m_IDContCF; }
			set
			{
				m_IDContCF = value;
				strCFileContents[iLineIDCont] = string.Format("const char *battery_id[BATTERY_ID_NUM] = {{") + value + "};";
			}
		}
		private string m_OCVContCF;
		public string OCVContCF
		{
			get { return m_OCVContCF; }
			set
			{
				m_OCVContCF = value;
				strCFileContents[iLineOCVCont] = string.Format("one_latitude_data_t ocv_data[OCV_DATA_NUM] = {{") + value + "};" ;
			}
		}
		private string m_CHGContCF;
		public string CHGContCF
		{
			get { return m_CHGContCF; }
			set
			{
				m_CHGContCF = value;
				strCFileContents[iLineCHGCont] = string.Format("one_latitude_data_t	charge_data[CHARGE_DATA_NUM] = {{") + value + "};";
			}
		}
		private string m_XAxisContCF;
		public string XAxisContCF
		{
			get { return m_XAxisContCF; }
			set
			{
				m_XAxisContCF = value;
				strCFileContents[iLineXCont] = string.Format("int32_t	XAxisElement[XAxis] = {{") + value + "};";
			}
		}
		private string m_YAxisContCF;
		public string YAxisContCF
		{
			get { return m_YAxisContCF; }
			set
			{
				m_YAxisContCF = value;
				strCFileContents[iLineYCont] = string.Format("int32_t	YAxisElement[YAxis] = {{") + value + "};";
			}
		}
		private string m_ZAxisContCF;
		public string ZAxisContCF
		{
			get { return m_ZAxisContCF; }
			set
			{
				m_ZAxisContCF = value;
				strCFileContents[iLineZCont] = string.Format("int32_t	ZAxisElement[ZAxis] = {{") + value + "};";
			}
		}
		private List<string> strRCContent = new List<string>();
		private string strCFileContentLastOne = "";
		private List<string> strExtraCFile = new List<string>();
		private int iLineExtraEquip = 0;
		private int iLineExtraTester = 1;
		#endregion

        #region VTR/TR content
        private List<string> strVTRHFileContents = new List<string>();
        private int iLineIRXAxisNum = 1;
        private int iLineIRYAxisNum = 2;
        private int iLineRDataNum = 4;
        private int iLineVTRShifted = 10;
        private UInt32 m_uIRXAxisNumHF;
        public UInt32 uIRXAxisNumHF
        {
            get { return m_uIRXAxisNumHF; }
            set
            {
                m_uIRXAxisNumHF = value;
                strVTRHFileContents[iLineIRXAxisNum] = string.Format("#define	IRXAxis  \t {0}", m_uIRXAxisNumHF);
            }
        }
        private UInt32 m_uIRYAxisNumHF;
        public UInt32 uIRYAxisNumHF
        {
            get { return m_uIRYAxisNumHF; }
            set
            {
                m_uIRYAxisNumHF = value;
                strVTRHFileContents[iLineIRYAxisNum] = string.Format("#define	IRYAxis  \t {0}", m_uIRYAxisNumHF);
            }
        }
        private UInt32 m_uRDataNumHF;
        public UInt32 uRDataNumHF
        {
            get { return m_uRDataNumHF; }
            set
            {
                m_uRDataNumHF = value;
                strVTRHFileContents[iLineRDataNum] = string.Format("#define	R_DATA_NUM  \t {0}", m_uRDataNumHF);
            }
        }

        private List<string> strVTRCFileContents = new List<string>();
        private int iLineIRXAxisCont = 2;
        private int iLineIRYAxisCont = 4;
        private int iLineIRtableCont = 7;
        private string m_IRXAxisContCF;
        public string IRXAxisContCF
        {
            get { return m_IRXAxisContCF; }
            set
            {
                m_IRXAxisContCF = value;
                strVTRCFileContents[iLineIRXAxisCont] = string.Format("int32_t	IRXAxisElement[IRXAxis] = {{") + value + "};";
            }
        }
        private string m_IRYAxisContCF;
        public string IRYAxisContCF
        {
            get { return m_IRYAxisContCF; }
            set
            {
                m_IRYAxisContCF = value;
                strVTRCFileContents[iLineIRYAxisCont] = string.Format("int32_t	IRYAxisElement[IRYAxis] = {{") + value + "};";
            }
        }
        private List<string> strIRtableContent = new List<string>();
        private string strIRtableCFileContentLastOne = "";

        private List<string> strRTableCFileContents = new List<string>();
        private int iLineRDataCont = 3;
        private List<string> strRtableContent = new List<string>();
        private string strRTableCFileContentLastOne = "";
        #endregion

        #region File path definition
        //private string strDriverTableFileN = null;
		private string strRCExtendTmp = string.Format(".rc.tmp");
		private string strOCVExtendTmp = string.Format(".ocv.tmp");
		private string strCHGExtendTmp = string.Format(".chg.tmp");
		private string strTargetTmpFileFullPath = "";
		private string strCFileFullName = ""; //= string.Format(".c");
		private string strHFileFullName =""; //= string.Format(".h");
        private string strFalconLYCFileFullName = ""; //= string.Format(".c");
        private string strFalconLYHFileFullName = ""; //= string.Format(".h");
        //(M170318)Francis, modify these 2 as public, for calling by FalconLy conveniently.
        public List<string> strTmpFullPathList = new List<string>();		//sequency of tmpfullpathlist must be 1.RC, 2.OCV, 3.CHG
        public int iIndxInFullPathList = -1;
        public string strTargetOutDrvFolder = "";
        #endregion

		#region private members
		private List<SourceDataSample> sdHeaderInformation;
		private TypeEnum TableTypeMk;
		private string strAndroidEquip;
		private List<string> strAndroidEqpList = new List<string>();
		private List<string> strAndroidEqpTmp = new List<string>();
		private string strAndroidManufacture;
		private string strAndroidBatteryModel;
		private float fAndroidFullCap;
		private float fAndroidLimitChgVoltage;
		private float fAndroidCutoffDsgVoltage;
		private string strAndroidTester;
		private List<string> strAndroidTstList = new List<string>();
		private List<string> strAndroidTstTmp = new List<string>();
		private string strAndroidBatteryID;
		private List<string> strAndroidBIDList = new List<string>();		//(A140805)Francis, saving BatteryID string array from SourceDataSample
		private List<string> strAndroidBIDTmp = new List<string>();		//(A140805)Francis, saving BatteryID string array from tmp file
		private List<string> strUserInput = null;											//(A141024)Francis, saving User Input
        private bool bMakeTable;                                        //(A141805)Francis, if true(default), that means other 2 tmp files existing, then do generate .c/.h

		private List<TableError> drvError = null;											//(A141118)Francis, only a reference point, has no new instance

        public bool bAndDrvShortShow = false;                           //(A170706)Francis, if true, show alert message that cannot make Android Driver. Only when if current table is Charge, or found Charge tmp file.

        #endregion

        #region public members
        
        public bool bVTRable;                                          //(A170228)Francis, if true(default), that means another ocv or rc table existing, then generate VTR/TR table
        public bool bVTRfromUser;
        public TableInterface tblSample;

        #endregion

        public AndroidDriverSample(TypeEnum inTableType, List<SourceDataSample> insdsamples, List<string> linput, TableInterface tblcaller)
		{
			TableTypeMk = inTableType;
			sdHeaderInformation = insdsamples;
			strUserInput = linput;
            tblSample = tblcaller;
		}

//		public bool AddRCContent(TableInterface inTable

		public bool InitializeHeaderInfor(ref UInt32 uErr, ref List<TableError> intber)
		{
			bool bReturn = false;
			string strTmpFile = "";

			drvError = intber;		//(A141118)Francis, assign reference to 

			if(sdHeaderInformation.Count != 0)
			{
				strAndroidManufacture = sdHeaderInformation[0].myHeader.strManufacture;
				strAndroidBatteryModel = sdHeaderInformation[0].myHeader.strBatteryModel;
				fAndroidFullCap = sdHeaderInformation[0].myHeader.fAbsMaxCap;
				fAndroidLimitChgVoltage = sdHeaderInformation[0].myHeader.fLimitChgVolt;
				fAndroidCutoffDsgVoltage = sdHeaderInformation[0].myHeader.fCutoffDsgVolt;
				strAndroidEquip = "";
				strAndroidTester = "";
				strAndroidBatteryID = "";
				strAndroidEqpList.Clear();
				strAndroidEqpTmp.Clear();
				strAndroidTstList.Clear();
				strAndroidTstTmp.Clear();
				strAndroidBIDList.Clear();
				strAndroidBIDTmp.Clear();
				//strTargetOutDrvFolder = sdHeaderInformation[0].strOutputFolder;
				strTargetOutDrvFolder = System.IO.Path.Combine(FolderMap.m_currentproj_folder, "Tmp\\");
				if (!Directory.Exists(strTargetOutDrvFolder))
				{
					Directory.CreateDirectory(strTargetOutDrvFolder);
				}
				foreach (SourceDataSample sceda in sdHeaderInformation)
				{
					strAndroidEquip += sceda.myHeader.strEquip + ",";
					strAndroidEqpList.Add(sceda.myHeader.strEquip);
					strAndroidTester = sceda.myHeader.strTester + ",";
					strAndroidTstList.Add(sceda.myHeader.strTester);
					strAndroidBatteryID += sceda.myHeader.strBatteryID + ",";
					strAndroidBIDList.Add(sceda.myHeader.strBatteryID);		//(A140805)Francis, for Battery string array
				}

				strExtraCFile.Clear();
				strExtraCFile.Add(":" + strAndroidEquip);
				strExtraCFile.Add(":" + strAndroidTester);

				#region add comment header string content for C and H file
				strHHeaderComments.Add(string.Format("/*****************************************************************************"));
				strHHeaderComments.Add(string.Format("* Copyright(c) O2Micro, 2014. All rights reserved."));
				strHHeaderComments.Add(string.Format("*"));
				strHHeaderComments.Add(string.Format("* O2Micro battery gauge driver"));
				strHHeaderComments.Add( string.Format("* File: table"));	//4
				strHHeaderComments.Add(string.Format("*"));
				strHHeaderComments.Add(string.Format("* $Source: /data/code/CVS"));
				strHHeaderComments.Add(string.Format("* $Revision: 4.00.01 $"));
				strHHeaderComments.Add(string.Format("*"));
				strHHeaderComments.Add(string.Format("* This program is free software and can be edistributed and/or modify"));
				strHHeaderComments.Add(string.Format("* it under the terms of the GNU General Public License version 2 as"));
				strHHeaderComments.Add(string.Format("* published by the Free Software Foundation."));
				strHHeaderComments.Add(string.Format("*"));
				strHHeaderComments.Add(string.Format("* This Source Code Reference Design for O2MICRO Battery Gauge access (\\u201cReference Design\\u201d) "));
				strHHeaderComments.Add(string.Format("* is sole for the use of PRODUCT INTEGRATION REFERENCE ONLY, and contains confidential "));
				strHHeaderComments.Add(string.Format("* and privileged information of O2Micro International Limited. O2Micro shall have no "));
				strHHeaderComments.Add(string.Format("* liability to any PARTY FOR THE RELIABILITY, SERVICEABILITY FOR THE RESULT OF PRODUCT "));
				strHHeaderComments.Add(string.Format("* INTEGRATION, or results from: (i) any modification or attempted modification of the "));
				strHHeaderComments.Add(string.Format("* Reference Design by any party, or (ii) the combination, operation or use of the "));
				strHHeaderComments.Add(string.Format("* Reference Design with non-O2Micro Reference Design."));
				strHHeaderComments.Add( string.Format("*"));
				strHHeaderComments.Add(string.Format("* Battery Manufacture: {0}", strAndroidManufacture));
				strHHeaderComments.Add(string.Format("* Battery Model: {0}", strAndroidBatteryModel));
				strHHeaderComments.Add(string.Format("* Absolute Max Capacity(mAhr): {0}", fAndroidFullCap));
				strHHeaderComments.Add(string.Format("* Limited Charge Voltage(mV): {0}", fAndroidLimitChgVoltage));
				strHHeaderComments.Add(string.Format("* Cutoff Discharge Voltage(mV): {0}", fAndroidCutoffDsgVoltage));
				strHHeaderComments.Add(string.Format("* Equipment: {0}", strAndroidEquip));	//26
				strHHeaderComments.Add(string.Format("* Tester: {0}", strAndroidTester));	//27
				strHHeaderComments.Add(string.Format("* Battery ID: {0}", strAndroidBatteryID));	//28
				//(A141024)Francis, add Version/Date/Comment 3 string into header comment of txt file
				strHHeaderComments.Add(string.Format("* Version = {0}", strUserInput[(int)MakeParamEnum.MakeVersion]));
				strHHeaderComments.Add(string.Format("* Date = {0}", strUserInput[(int)MakeParamEnum.MakeDate]));
				//strHHeaderComments.Add(string.Format("* Comment = {0}", strUserInput[(int)MakeParamEnum.MakeComment]));
				strHHeaderComments.Add(string.Format("* Comment = "));
				string[] var = strUserInput[(int)MakeParamEnum.MakeComment].Split('\n');
				foreach (string str in var)
				{
					strHHeaderComments.Add(string.Format("*           {0}", str.Replace('\r', ' ')));
				}
				//(E141024)
				strHHeaderComments.Add(string.Format("*****************************************************************************/"));
				strHHeaderComments.Add(string.Format(""));
				#endregion

				//use to make final xxxx_yyyy_zzz.c .h file
				//strDriverTableFileN = strAndroidManufacture + "_" + strAndroidBatteryModel + "_" + fAndroidFullCap.ToString() + "mAhr";
				strTmpFile = strAndroidManufacture + "_" + strAndroidBatteryModel + "_" + fAndroidFullCap.ToString() + "mAhr";

				bMakeTable = false;
                bVTRable = false;       //(A170228)Francis, default cannot make VTR/TR tables
				bReturn = true;
				strTmpFullPathList.Clear();
				//strTmpFullPathList.Add(strDriverTableFileN + strRCExtendTmp);
				//strTmpFullPathList.Add(strDriverTableFileN + strOCVExtendTmp);
				//strTmpFullPathList.Add(strDriverTableFileN + strCHGExtendTmp);
				//strTargetTmpFileFullPath = strDriverTableFileN;
				strTmpFullPathList.Add(strTmpFile + strRCExtendTmp);
				strTmpFullPathList.Add(strTmpFile + strOCVExtendTmp);
				strTmpFullPathList.Add(strTmpFile + strCHGExtendTmp);
				strTargetTmpFileFullPath = strTmpFile;
				strCFileFullName = strTmpFile + ".c";
				strHFileFullName = strTmpFile + ".h";
                strFalconLYCFileFullName = strTmpFile + "_FalconLY.c";
                strFalconLYHFileFullName = strTmpFile + "_FalconLY.h";
				if (TableTypeMk == TypeEnum.ChargeRawType)
				{
					strTargetTmpFileFullPath += strCHGExtendTmp;
				}
				else if (TableTypeMk == TypeEnum.OCVRawType)
				{
					strTargetTmpFileFullPath += strOCVExtendTmp;
				}
				else if (TableTypeMk == TypeEnum.RCRawType)
				{
					strTargetTmpFileFullPath += strRCExtendTmp;
				}
				else
				{
					uErr = LibErrorCode.IDS_ERR_TMK_DRV_TYPE_NOT_SUPPORT;	//should no happen
					strTmpFullPathList.Clear();
					strTargetTmpFileFullPath = "";
					CreateNewDrvError(strTargetTmpFileFullPath, uErr);
					bReturn = false;
				}
                bAndDrvShortShow = false;
			}
			else
			{
				uErr = LibErrorCode.IDS_ERR_TMK_DRV_HEADER_NOT_FOUND;	//should not happen
				CreateNewDrvError(strTargetTmpFileFullPath, uErr);
			}
			
			return bReturn;
		}
		
        //(A170320)Francis
        public void InitializeHeaderbyOldCHFile(ref UInt32 uErr, ref List<TableError> intber, List<SourceDataHeader> inOldTable)
        {
			string strTmpFile = "";

			drvError = intber;		//(A141118)Francis, assign reference to 

            if (inOldTable.Count != 0)
			{
                strAndroidManufacture = inOldTable[0].strManufacture;
                strAndroidBatteryModel = inOldTable[0].strBatteryModel;
                fAndroidFullCap = inOldTable[0].fAbsMaxCap;
                fAndroidLimitChgVoltage = inOldTable[0].fLimitChgVolt;
                fAndroidCutoffDsgVoltage = inOldTable[0].fCutoffDsgVolt;
				strAndroidEquip = "";
				strAndroidTester = "";
				strAndroidBatteryID = "";
				strAndroidEqpList.Clear();
				strAndroidEqpTmp.Clear();
				strAndroidTstList.Clear();
				strAndroidTstTmp.Clear();
				strAndroidBIDList.Clear();
				strAndroidBIDTmp.Clear();
				//strTargetOutDrvFolder = sdHeaderInformation[0].strOutputFolder;
                //tmp folder, don't need
				//strTargetOutDrvFolder = System.IO.Path.Combine(FolderMap.m_currentproj_folder, "Tmp\\");
				//if (!Directory.Exists(strTargetOutDrvFolder))
				//{
					//Directory.CreateDirectory(strTargetOutDrvFolder);
				//}
                foreach (SourceDataHeader scdhdr in inOldTable)
				{
                    strAndroidEquip += scdhdr.strEquip + ",";
                    strAndroidEqpList.Add(scdhdr.strEquip);
                    strAndroidTester = scdhdr.strTester + ",";
                    strAndroidTstList.Add(scdhdr.strTester);
                    strAndroidBatteryID += scdhdr.strBatteryID + ",";
                    strAndroidBIDList.Add(scdhdr.strBatteryID);		//(A140805)Francis, for Battery string array
				}

				strExtraCFile.Clear();
				strExtraCFile.Add(":" + strAndroidEquip);
				strExtraCFile.Add(":" + strAndroidTester);

				#region add comment header string content for C and H file
				strHHeaderComments.Add(string.Format("/*****************************************************************************"));
				strHHeaderComments.Add(string.Format("* Copyright(c) O2Micro, 2014. All rights reserved."));
				strHHeaderComments.Add(string.Format("*"));
				strHHeaderComments.Add(string.Format("* O2Micro battery gauge driver"));
				strHHeaderComments.Add( string.Format("* File: table"));	//4
				strHHeaderComments.Add(string.Format("*"));
				strHHeaderComments.Add(string.Format("* $Source: /data/code/CVS"));
				strHHeaderComments.Add(string.Format("* $Revision: 4.00.01 $"));
				strHHeaderComments.Add(string.Format("*"));
				strHHeaderComments.Add(string.Format("* This program is free software and can be edistributed and/or modify"));
				strHHeaderComments.Add(string.Format("* it under the terms of the GNU General Public License version 2 as"));
				strHHeaderComments.Add(string.Format("* published by the Free Software Foundation."));
				strHHeaderComments.Add(string.Format("*"));
				strHHeaderComments.Add(string.Format("* This Source Code Reference Design for O2MICRO Battery Gauge access (\\u201cReference Design\\u201d) "));
				strHHeaderComments.Add(string.Format("* is sole for the use of PRODUCT INTEGRATION REFERENCE ONLY, and contains confidential "));
				strHHeaderComments.Add(string.Format("* and privileged information of O2Micro International Limited. O2Micro shall have no "));
				strHHeaderComments.Add(string.Format("* liability to any PARTY FOR THE RELIABILITY, SERVICEABILITY FOR THE RESULT OF PRODUCT "));
				strHHeaderComments.Add(string.Format("* INTEGRATION, or results from: (i) any modification or attempted modification of the "));
				strHHeaderComments.Add(string.Format("* Reference Design by any party, or (ii) the combination, operation or use of the "));
				strHHeaderComments.Add(string.Format("* Reference Design with non-O2Micro Reference Design."));
				strHHeaderComments.Add( string.Format("*"));
				strHHeaderComments.Add(string.Format("* Battery Manufacture: {0}", strAndroidManufacture));
				strHHeaderComments.Add(string.Format("* Battery Model: {0}", strAndroidBatteryModel));
				strHHeaderComments.Add(string.Format("* Absolute Max Capacity(mAhr): {0}", fAndroidFullCap));
				strHHeaderComments.Add(string.Format("* Limited Charge Voltage(mV): {0}", fAndroidLimitChgVoltage));
				strHHeaderComments.Add(string.Format("* Cutoff Discharge Voltage(mV): {0}", fAndroidCutoffDsgVoltage));
				strHHeaderComments.Add(string.Format("* Equipment: {0}", strAndroidEquip));	//26
				strHHeaderComments.Add(string.Format("* Tester: {0}", strAndroidTester));	//27
				strHHeaderComments.Add(string.Format("* Battery ID: {0}", strAndroidBatteryID));	//28
				//(A141024)Francis, add Version/Date/Comment 3 string into header comment of txt file
				strHHeaderComments.Add(string.Format("* Version = {0}", strUserInput[(int)MakeParamEnum.MakeVersion]));
				strHHeaderComments.Add(string.Format("* Date = {0}", strUserInput[(int)MakeParamEnum.MakeDate]));
				//strHHeaderComments.Add(string.Format("* Comment = {0}", strUserInput[(int)MakeParamEnum.MakeComment]));
				strHHeaderComments.Add(string.Format("* Comment = "));
				string[] var = strUserInput[(int)MakeParamEnum.MakeComment].Split('\n');
				foreach (string str in var)
				{
					strHHeaderComments.Add(string.Format("*           {0}", str.Replace('\r', ' ')));
				}
				//(E141024)
				strHHeaderComments.Add(string.Format("*****************************************************************************/"));
				strHHeaderComments.Add(string.Format(""));
				#endregion

				//use to make final xxxx_yyyy_zzz.c .h file
				//strDriverTableFileN = strAndroidManufacture + "_" + strAndroidBatteryModel + "_" + fAndroidFullCap.ToString() + "mAhr";
				strTmpFile = strAndroidManufacture + "_" + strAndroidBatteryModel + "_" + fAndroidFullCap.ToString() + "mAhr";
                strFalconLYCFileFullName = strTmpFile + "_FalconLY.c";
                strFalconLYHFileFullName = strTmpFile + "_FalconLY.h";

				strTmpFullPathList.Clear();
			}
        }

        public void SetupUserInput(List<string> linput)
        {
            strUserInput = linput;
        }
        //(E170320)

		//Initialize content of H file, currently using hard coding in code, but hopely we can read it from file, a sample file in particular folder
		public void InitializedTableH()
		{
			#region add content string for H file
			strHFileContents.Clear();
			strHFileContents.Add(string.Format("#ifndef _TABLE_H_"));
			strHFileContents.Add(string.Format("#define _TABLE_H_"));
			strHFileContents.Add(string.Format(""));
			strHFileContents.Add(string.Format(""));
			strHFileContents.Add(string.Format("#define OCV_DATA_NUM  \t 44"));
			strHFileContents.Add(string.Format("#define CHARGE_DATA_NUM \t 51"));
			strHFileContents.Add(string.Format(""));
			strHFileContents.Add(string.Format(""));
			strHFileContents.Add(string.Format("#define XAxis \t\t 25"));
			strHFileContents.Add(string.Format("#define YAxis \t\t 7"));
			strHFileContents.Add(string.Format("#define ZAxis \t\t 8"));
			strHFileContents.Add(string.Format(""));															//(A140805)Francis, for battery id string array
			strHFileContents.Add(string.Format("#define BATTERY_ID_NUM \t 2"));	//(A140805)Francis, for battery id string array
			strHFileContents.Add(string.Format(""));
			strHFileContents.Add(string.Format(""));
			strHFileContents.Add(string.Format("/****************************************************************************"));
			strHFileContents.Add(string.Format("* Struct section"));
			strHFileContents.Add(string.Format("*  add struct #define here if any"));
			strHFileContents.Add(string.Format("***************************************************************************/"));
			strHFileContents.Add(string.Format("typedef struct tag_one_latitude_data {{"));
			strHFileContents.Add(string.Format(" \t int32_t \t\t\t x;//"));
			strHFileContents.Add(string.Format(" \t int32_t \t\t\t y;//"));
			strHFileContents.Add(string.Format("}} one_latitude_data_t;"));
			strHFileContents.Add(string.Format(""));
			strHFileContents.Add(string.Format(""));
			strHFileContents.Add(string.Format("/****************************************************************************"));
			strHFileContents.Add(string.Format("* extern variable declaration section"));
			strHFileContents.Add(string.Format("***************************************************************************/"));
			strHFileContents.Add(string.Format("extern const char *battery_id[] ;"));
			strHFileContents.Add(string.Format(""));
			strHFileContents.Add(string.Format("#endif"));
			strHFileContents.Add(string.Format(""));
			#endregion

            #region add content string for IR/R table in H file
            strVTRHFileContents.Clear();
            strVTRHFileContents.Add(string.Format("//------------------IR_TABLE------------------------------------------"));
            strVTRHFileContents.Add(string.Format("#define	IRXAxis  \t 40"));
            strVTRHFileContents.Add(string.Format("#define	IRYAxis  \t 6"));
            strVTRHFileContents.Add(string.Format("//------------------R_TABLE------------------------------------------"));
            strVTRHFileContents.Add(string.Format("#define	R_DATA_NUM \t 6"));
            #endregion
        }

		public void InitializedTableC()
		{
			#region add content string for C file
			strCFileContents.Clear();
			strCFileContents.Add(string.Format("#include <linux/kernel.h>"));
			strCFileContents.Add(string.Format("#include \"table.h\" "));
			strCFileContents.Add(string.Format(""));
			strCFileContents.Add(string.Format("/*****************************************************************************"));
			strCFileContents.Add(string.Format("* Global variables section - Exported"));
			strCFileContents.Add(string.Format("* add declaration of global variables that will be exported here"));
			strCFileContents.Add(string.Format("* e.g."));
			strCFileContents.Add(string.Format("*	int8_t foo;"));
			strCFileContents.Add(string.Format("****************************************************************************/"));
			strCFileContents.Add(string.Format("const char * battery_id[BATTERY_ID_NUM] = {{ \"XXXX\", \"YYYY\" }};"));
			strCFileContents.Add(string.Format(""));
			strCFileContents.Add(string.Format("one_latitude_data_t ocv_data[OCV_DATA_NUM] = {{ }};"));
			strCFileContents.Add(string.Format(""));
			strCFileContents.Add(string.Format("//real current to soc "));
			strCFileContents.Add(string.Format("one_latitude_data_t	charge_data[CHARGE_DATA_NUM] = {{ }};"));
			strCFileContents.Add(string.Format(""));
			strCFileContents.Add(string.Format("//RC table X Axis value, in mV format"));
			strCFileContents.Add(string.Format("int32_t	XAxisElement[XAxis] = {{ }};"));
			strCFileContents.Add(string.Format(""));
			strCFileContents.Add(string.Format("//RC table Y Axis value, in mA format"));
			strCFileContents.Add(string.Format("int32_t	YAxisElement[YAxis] = {{ }};"));
			strCFileContents.Add(string.Format(""));
			strCFileContents.Add(string.Format("// RC table Z Axis value, in 10*'C format"));
			strCFileContents.Add(string.Format("int32_t	ZAxisElement[ZAxis] = {{ }};"));
			strCFileContents.Add(string.Format(""));
			strCFileContents.Add(string.Format("// contents of RC table, its unit is 10000C, 1C = DesignCapacity"));
			strCFileContents.Add(string.Format("int32_t	RCtable[YAxis*ZAxis][XAxis]={{"));
			strCFileContents.Add(string.Format(""));
			strCFileContentLastOne = string.Format("}};");
			#endregion

            #region add content string for IR/R table in C file
            strVTRCFileContents.Clear();
            strVTRCFileContents.Add(string.Format("//------------------------------------------------------------IR_TABLE------------------------------------------"));
            strVTRCFileContents.Add(string.Format("//IR table X Axis value, in mV format"));
            strVTRCFileContents.Add(string.Format("int32_t	IRXAxisElement[IRXAxis] = {{ }};"));
            strVTRCFileContents.Add(string.Format("//IR table Y Axis value, in 10*'C format"));
            strVTRCFileContents.Add(string.Format("int32_t	IRYAxisElement[IRYAxis] = {{ }}"));
            strVTRCFileContents.Add(string.Format("// contents of IR table, its unit is 10*mOhm"));
            strVTRCFileContents.Add(string.Format("int32_t	IRtable[IRYAxis][IRXAxis]={{"));
            strIRtableCFileContentLastOne = string.Format("}};");
            strRTableCFileContents.Add(string.Format(""));
            strRTableCFileContents.Add(string.Format("//------------------R_TABLE------------------------------------------"));
            strRTableCFileContents.Add(string.Format("//temperature *10 to AVG_R-factor@10mOhm"));
            strRTableCFileContents.Add(string.Format("one_latitude_data_t	avg_r_data[R_DATA_NUM] = {{"));
            strRTableCFileContentLastOne = string.Format("}};");
            #endregion
        }

		public void CheckTmpFileExist(ref bool myTmp)
		{
            bool bExistS = true, bTmpExistS = false;
            bool bExistOCV = false, bExistRC = false;   //(A170228)Francis, use to mark OCV or RC tmp file exist 
			string strFull = "";
			int itmp = 0;

			iIndxInFullPathList = -1;
			myTmp = false;

			/*(A140730)Francis, add a fake Charge table to experiment
			if (false)		//bFranTestMode
			{
				string strBak = strTargetTmpFileFullPath;

				strTargetTmpFileFullPath = strTmpFullPathList[2];		//3rd one is charge table
				//MakeChgContent();
				GenerateTemperaryFile();
				strTargetTmpFileFullPath = strBak;
			}
			*///(E140730)

			foreach (string stTmp in strTmpFullPathList)
			{
				if (stTmp.ToLower().IndexOf(strTargetTmpFileFullPath.ToLower()) != -1)	//the target file string, chg, OCV, or RC
				{
					iIndxInFullPathList = itmp;
					strFull = System.IO.Path.Combine(strTargetOutDrvFolder, stTmp);
					myTmp = File.Exists(strFull);
					//there is tmp file for type, that means operator maybe make same OCV/RC/Charge table more than twice, so overwrite originalone
				}
				else
				{
					strFull = System.IO.Path.Combine(strTargetOutDrvFolder, stTmp);
                    bTmpExistS = File.Exists(strFull);
                    //(A170228)Francis, check RC/OCV exist
                    if (bTmpExistS)
                    {
                        if(strFull.ToLower().IndexOf("rc") != -1)
                        {
                            bExistRC = true;
                        }
                        else if(strFull.ToLower().IndexOf("ocv") != -1)
                        {
                            bExistOCV = true;
                        }
                        else if(strFull.ToLower().IndexOf("chg") != -1)
                        {
                            bAndDrvShortShow = true;
                        }
                    }
                    //(E170228)
                    else
                    {

                    }
                    bExistS &= bTmpExistS;
				}
				itmp += 1;
			}
			bMakeTable = bExistS;		//if other 2 tables tmp file existing, that means we need to make Android C/H file
            //(A170228)Francis, check RC/OCV exist, then mark bVTRable
            if (TableTypeMk == TypeEnum.ChargeRawType)
            {
                bAndDrvShortShow = true;
            }
            else if (TableTypeMk == TypeEnum.OCVRawType)
            {
                bExistOCV = true;
            }
            else if (TableTypeMk == TypeEnum.RCRawType)
            {
                bExistRC = true;
            }

            bVTRable = (bExistOCV & bExistRC);
            //(E170228)
		}

		public bool MakeDriver(/*TableInterface inObj,*/ ref UInt32 uErr, string OutFolder, bool bVTRfromInput = false)
		{
			bool bReturn = true;
			bool bMyTarget = false;
			string strTemp = "";

            bVTRfromUser = bVTRfromInput;
			CheckTmpFileExist(ref bMyTarget);
			//no matter what, generate tmp file first
			if (!GenerateTemperaryFile(ref uErr))		//basically return true; except temperary file open error, but temperary file path is default and no chagned
			{
				//LibErrorCode.strVal01 = System.IO.Path.Combine(strTargetOutDrvFolder, strTargetTmpFileFullPath);
				//uErr = LibErrorCode.IDS_ERR_TMK_DRV_TEMP_FILE_CREATE;
				CreateNewDrvError(strTargetTmpFileFullPath, uErr);
				bReturn = false;
			}

            //(A170228)Francis, if user input for VTR and TR table of new Gas Gauge algorithm
            if (tblSample.bVTRboth)
            {
                //(A170228)Francis, checck are we able to do VTR/TR table
                if (bVTRable)
                {
                    if (TableTypeMk == TypeEnum.RCRawType)
                    {
                        //tblSample.uOCVPercentPoints = new List<UInt32>();     //create variable
                        ReadOCVValueIntoVariables(ref tblSample.uOCVPercentPoints, ref tblSample.iOCVVolt);
                        tblSample.ConvertVTRandTR();
                        MakeIRRTableContent(tblSample.uRCVoltagePoints,
                                            tblSample.listfTemp,
                                            tblSample.ilistVTRPoints,
                                            tblSample.ilistTRPoints);
                    }
                    else if (TableTypeMk == TypeEnum.OCVRawType)
                    {
                        tblSample.uRCVoltagePoints = new List<UInt32>();
                        ReadRCValueIntoVariables(ref tblSample.listfTemp, ref tblSample.listfCurr, ref tblSample.uRCVoltagePoints, ref tblSample.iYPointsall);
                        tblSample.ConvertVTRandTR();
                        MakeIRRTableContent(tblSample.uRCVoltagePoints,
                                            tblSample.listfTemp,
                                            tblSample.ilistVTRPoints,
                                            tblSample.ilistTRPoints);
                    }
                    else if (TableTypeMk == TypeEnum.ChargeRawType)
                    {

                    }
                    //bVTRable = false;
                }
            }
            //(E170228)

			if ((bMakeTable))// && (!bMyTarget))
			{
				if ((iIndxInFullPathList < 0)  || (iIndxInFullPathList >= strTmpFullPathList.Count))
				{
					uErr = LibErrorCode.IDS_ERR_TMK_DRV_TEMP_FILE_STRING; //should not happen
					CreateNewDrvError(strTargetTmpFileFullPath, uErr);
				}
				else
				{
					//read other 2 temp file then create C/H file
                    //if (!bVTRable)  //((A170228)Francis, if never read, read it
                    //{
                    ReadRCContentFromTmp();
                    ReadOCVContentFromTmp();
                    //}
					ReadChgContentFromTmp();
				}
				//create C/H file

				//merge Equipment string together
				strTemp = "";
				foreach (string streqp in strAndroidEqpList)
				{
					strTemp += string.Format("\"{0}\",", streqp);
				}
				foreach (string streqp in strAndroidEqpTmp)
				{
					strTemp += string.Format("\"{0}\",", streqp);
				}
				strTemp = strTemp.Substring(0, strTemp.Length - 1);
				strHHeaderComments[iLineCmtEquip] = "* Equipment: " + strTemp;	//update in comments

				//merge Tester string together
				strTemp = "";
				foreach (string strtest in strAndroidTstList)
				{
					strTemp += string.Format("\"{0}\",", strtest);
				}
				foreach (string strtest in strAndroidTstTmp)
				{
					strTemp += string.Format("\"{0}\",", strtest);
				}
				strTemp = strTemp.Substring(0, strTemp.Length - 1);
				strHHeaderComments[iLineCmtTester] = "* Tester: " + strTemp;	//update in comments

				//merge Battery ID string together 
				strTemp = "";
				foreach (string strbidd in strAndroidBIDList)
				{
					strTemp += string.Format("\"{0}\",", strbidd);
				}
				foreach (string strbidd in strAndroidBIDTmp)
				{
					strTemp += string.Format("\"{0}\",", strbidd);
				}
				strTemp = strTemp.Substring(0, strTemp.Length - 1);
                uIDNumHF = (UInt32)(strAndroidBIDList.Count + strAndroidBIDTmp.Count);
				IDContCF = strTemp;
				strHHeaderComments[iLineCmtBId] = "* Battery ID: " + strTemp;	//update in comments

				bReturn = GenerateDriverFiles(ref uErr, OutFolder);	//basically return true; except C/H file create error, but C/H file path is default and no chagned
                if ((tblSample.bVTRboth) && (bVTRable))
                {
                    bReturn &= GenerateDriverFiles(ref uErr, OutFolder, true);
                    if (bReturn)
                    {
                        bReturn &= tblSample.GenerateTRTableFile(ref uErr, Path.Combine(OutFolder, TableInterface.strFalconLY));
                    }
                    bVTRable = false;       //clear content read flag
                }
			}
			else
			{
				////creat temp file
				////ReadRCContentFromTmp();

				//if (!GenerateTemperaryFile())
				//{
					//LibErrorCode.strVal01 = System.IO.Path.Combine(strTargetOutDrvFolder, strTargetTmpFileFullPath);
					//uErr = LibErrorCode.IDS_ERR_TMK_DRV_TEMP_FILE_CREATE;
					//bReturn = false;
				//}
                if(bAndDrvShortShow)
                {
                    uErr = LibErrorCode.IDS_ERR_TMK_DRV_SHORT_TABLES; //should not happen
                    CreateNewDrvError(strTargetTmpFileFullPath, uErr);
                    bReturn = false;
                }
			}

			return bReturn;
		}

		public void MakeRCContent(List<UInt32> inXList, List<float> inYList, List<float> inZList, List<List<Int32>> inRCCtt)
		{
			//Note that, in Eason's definition YAxis means W Axis in RC table file
			string strRCtmp02;
			int itTmp = 0, icnt = 0;

            uXNumHF = Convert.ToUInt32(inXList.Count);
            uYNumHF = Convert.ToUInt32(inYList.Count);
            uZNumHF = Convert.ToUInt32(inZList.Count);

			strRCtmp02 = "";
            foreach (UInt32 u1 in inXList)
			{
				strRCtmp02 += string.Format("{0}, ", u1);
			}
			XAxisContCF = strRCtmp02.Substring(0, strRCtmp02.Length - 2);

			strRCtmp02 = "";
			foreach (float f1 in inYList)
			{
				if (f1 < 0)
				{
					strRCtmp02 += string.Format("{0}, ", Convert.ToInt32(Math.Round(f1 * (-1), 0)));
				}
				else
				{
					strRCtmp02 += string.Format("{0}, ", Convert.ToInt32(Math.Round(f1, 0)));
				}
			}
			YAxisContCF = strRCtmp02.Substring(0, strRCtmp02.Length - 2);

			strRCtmp02 = "";
			foreach (float f2 in inZList)
			{
                strRCtmp02 += string.Format("{0}, ", (Int32)(f2 * 10 + 0.5));
			}
			ZAxisContCF = strRCtmp02.Substring(0, strRCtmp02.Length - 2);

			strRCContent.Clear();
            foreach (List<Int32> lit in inRCCtt)
			{
				if((icnt % inYList.Count) == 0)
				{
					strRCContent.Add(string.Format(""));
					strRCContent.Add(string.Format("//temp = {0:F1} ^C ", inZList[itTmp]));
					itTmp+=1;
				}
				strRCtmp02 = "{";
                foreach (Int32 i2 in lit)
				{
					strRCtmp02 += string.Format("{0}, ", i2);
				}
				strRCtmp02 = strRCtmp02.Substring(0, strRCtmp02.Length - 2);
				strRCtmp02 += "},";
				strRCContent.Add(strRCtmp02);
				icnt += 1;
			}

			strRCtmp02 = "";
			foreach (string strbidd in strAndroidBIDList)
			{
				strRCtmp02 += string.Format("\"{0}\",", strbidd);
			}
            if(strRCtmp02.Length > 0)
    			strRCtmp02 = strRCtmp02.Substring(0, strRCtmp02.Length - 1);

            uIDNumHF = (UInt32)strAndroidBIDList.Count;
			IDContCF = strRCtmp02;
		}

		public void MakeOCVContent(string strOCVPercent, List<Int32> inOCVVolt)
		{
			char[] chSeperate = new char[] { ',' };
			string[] strOC = strOCVPercent.Split(chSeperate, StringSplitOptions.None);
			string strocvtmp, strpointtmp;
			Int32 iptmp;
			float fptmp;

			if ((strOC.Length == inOCVVolt.Count) ||(strOC.Length == inOCVVolt.Count + 1))
			{
                uOCVNumHF = (UInt32)inOCVVolt.Count;
				strocvtmp = "";
				for (int i = 0; i < inOCVVolt.Count; i++)
				{
					if ((!float.TryParse(strOC[i].Trim(), out fptmp)) || strOC[i].Trim().Length < 1)
					{
						fptmp = 0F;
					}
					iptmp = Convert.ToInt32(Math.Round(fptmp / 100F, 0));
					strpointtmp = string.Format("{{{0}, {1}", inOCVVolt[i] ,iptmp) + "},";
					strocvtmp += strpointtmp;
				}
				strocvtmp = strocvtmp.Substring(0, strocvtmp.Length - 1);
				OCVContCF = strocvtmp;
			}
			else
			{
				OCVContCF = "";
			}
			strpointtmp = "";
			foreach (string strbidd in strAndroidBIDList)
			{
				strpointtmp += string.Format("\"{0}\",", strbidd);
			}
			strpointtmp = strpointtmp.Substring(0, strpointtmp.Length - 1);

            uIDNumHF = (UInt32)strAndroidBIDList.Count;
			IDContCF = strpointtmp;
		}

        public void MakeOCVContent(List<Int32> inOCVPercent, List<Int32> inOCVVolt)
        {
            char[] chSeperate = new char[] { ',' };
            //string[] strOC = strOCVPercent.Split(chSeperate, StringSplitOptions.None);
            string strocvtmp, strpointtmp;
            Int32 iptmp;
            float fptmp;

            //if ((strOC.Length == inOCVVolt.Count) || (strOC.Length == inOCVVolt.Count + 1))
            if(inOCVPercent.Count == inOCVVolt.Count)
            {
                uOCVNumHF = (UInt32)inOCVVolt.Count;
                strocvtmp = "";
                for (int i = 0; i < inOCVVolt.Count; i++)
                {
                    //if ((!float.TryParse(strOC[i].Trim(), out fptmp)) || strOC[i].Trim().Length < 1)
                    //{
                        //fptmp = 0F;
                    //}
                    fptmp = (float)inOCVPercent[i];
                    iptmp = Convert.ToInt32(Math.Round(fptmp / 100F, 0));
                    strpointtmp = string.Format("{{{0}, {1}", inOCVVolt[i], iptmp) + "},";
                    strocvtmp += strpointtmp;
                }
                strocvtmp = strocvtmp.Substring(0, strocvtmp.Length - 1);
                OCVContCF = strocvtmp;
            }
            else
            {
                OCVContCF = "";
            }
            strpointtmp = "";
            foreach (string strbidd in strAndroidBIDList)
            {
                strpointtmp += string.Format("\"{0}\",", strbidd);
            }
            if (strpointtmp.Length > 0)
                strpointtmp = strpointtmp.Substring(0, strpointtmp.Length - 1);

            uIDNumHF = (UInt32)strAndroidBIDList.Count;
            IDContCF = strpointtmp;
        }

        public void MakeChgContent(List<UInt32> inXList, List<UInt32> inYList)
		{
			string strcctmp, strccFinal;
			//inXList and inYList suppose to have same Count, due to it added together; no need to check length 
			//uChargeNumHF = 51;
			//CHGContCF = "{100, 10000},{158, 9989},{163, 9988},{169, 9987},{172, 9986},{178, 9965},{183,9943},	{189, 9921},{198, 9882},{201, 9873},{207, 9847},{213, 9825},{219, 9803},{225, 9777}, {233, 9747},{239, 9725},{247, 9694},{254, 9668},{262, 9638},{269, 9612},{278, 9577},{295, 9507},{313, 9429},{333, 9410},{353,9409},{375, 9400},{399, 9380},{424, 9350},{450, 9259},{479, 9163},{509, 9063},{539, 8967},{556, 8910},{574, 8854},{590, 8801},{626, 8684},{665, 8557},{706, 8409},{749, 8350},{770, 8300},{794, 8280},{816, 8250},	{840, 8248},{864, 8174},{889, 8091},{914, 8013},{938, 7943},{965, 7856},{991, 7747},{1018, 7686},{1044, 7599},	}";
            uChargeNumHF = Math.Min((UInt32)inXList.Count, (UInt32)inYList.Count);
			strccFinal = "";
			for (int i = 0; i < uChargeNumHF; i++)
			{
				strcctmp = string.Format("{{{0}, {1}}},", inXList[i], inYList[i]);
				strccFinal += strcctmp;
			}
			strccFinal = strccFinal.Substring(0, strccFinal.Length - 1);
			CHGContCF = strccFinal;

			strcctmp = "";
			foreach (string strbidd in strAndroidBIDList)
			{
				strcctmp += string.Format("\"{0}\",", strbidd);
			}
			strcctmp = strcctmp.Substring(0, strcctmp.Length - 1);

            uIDNumHF = (UInt32)strAndroidBIDList.Count;
			IDContCF = strcctmp;
		}

        public void MakeIRRTableContent(List<UInt32> inIRXList, List<float> inIRYList, List<Int32> inVTRList, List<Int32> inTRList)
        {
            string strVTRtmp = "";
            //string strTRCmbtmp = "";

            uIRXAxisNumHF = Convert.ToUInt32(inIRXList.Count);
            uIRYAxisNumHF = Convert.ToUInt32(inIRYList.Count);
            uRDataNumHF = Convert.ToUInt32(inIRYList.Count);

            foreach (UInt32 u1 in inIRXList)
            {
                strVTRtmp += string.Format("{0}, ", u1);
            }
            IRXAxisContCF = strVTRtmp.Substring(0, strVTRtmp.Length - 2);

            strVTRtmp = "";
            foreach(float f2 in inIRYList)
            {
                strVTRtmp += string.Format("{0}, ", Convert.ToInt32(Math.Round(f2, 0) * 10));
            }
            IRYAxisContCF = strVTRtmp.Substring(0, strVTRtmp.Length - 2);

            strIRtableContent.Clear();
            for (Int32 i2 = 0; i2 < inVTRList.Count; i2++)
            {
                if (i2 % uIRXAxisNumHF == 0)
                {
                    strVTRtmp = string.Format("{{");
                }
                else if ((i2 % uIRXAxisNumHF) == (uIRXAxisNumHF - 1))
                {
                    strVTRtmp = strVTRtmp.Substring(0, strVTRtmp.Length - 2);   //ignore last 2 character, ", "
                    strVTRtmp += string.Format("}},");
                    strIRtableContent.Add(strVTRtmp);
                }
                else
                {
                    strVTRtmp += string.Format("{0}, ", inVTRList[i2]);
                }
            }

            strRtableContent.Clear();
            for (int i3 = 0; i3 < uIRYAxisNumHF; i3++)
            {
                strVTRtmp = string.Format("{{") + string.Format("{0}, {1}", Convert.ToInt32(inIRYList[i3]) * 10, inTRList[i3]) + string.Format("}},");
                strRtableContent.Add(strVTRtmp);
            }
        }

		//(A141024)Francis, get the status that bMakeTable, it indicates that .C.H files are created or not
		public bool GetMakeTableStatus()
		{
			return bMakeTable;
		}
		//(E141024)

        //(A170228)Francis, support VTR/TR table calculation
        public bool ReadRCValueIntoVariables(ref List<float> listTemp, ref List<float> listCurr, ref List<UInt32> listVolt, ref List<List<Int32>> listYpoints, string strTargetFile = null)
        {
            bool bReturn = true;
            FileStream fsRead = null;
            StreamReader stmRead = null;
            string strtmpfu = "";
            string strcont = "";
            int iNUMstrLoc;
            UInt32 uXAxisNum = 0, uYAxisNum = 0, uZAxisNum = 0;
            string strXAxis = "", strYAxis = "", strZAxis = "";
            List<string> strRCTemp = new List<string>();
            UInt32 uline = 0;
            string[] strArrTmp;

            if (iIndxInFullPathList != 0)	//we are having new other data, so need to read RC tmp file
            {
                if (strTargetFile == null)
                    strtmpfu = System.IO.Path.Combine(strTargetOutDrvFolder, strTmpFullPathList[0]);
                else
                    strtmpfu = strTargetFile;
                try
                {
                    fsRead = File.Open(strtmpfu, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    stmRead = new StreamReader(strtmpfu);
                }
                catch (Exception er)
                {
                    LibErrorCode.strVal01 = strtmpfu;
                    CreateNewDrvError(strtmpfu, LibErrorCode.IDS_ERR_TMK_DRV_FILE_READ);
                    return bReturn;
                }

                strcont = stmRead.ReadLine();	//skip one line for version string
                while ((strcont = stmRead.ReadLine()) != null)
                {
                    if (uline == iLineXNum)
                    {   //update h file content
                        iNUMstrLoc = strcont.IndexOf("XAxis");
                        iNUMstrLoc += "XAxis".Length;
                        if (!UInt32.TryParse(strcont.Substring(iNUMstrLoc), out uXAxisNum))
                        {
                            uXAxisNum = 0;
                        }
                    }
                    else if (uline == iLineYNum)
                    {   //update h file content
                        iNUMstrLoc = strcont.IndexOf("YAxis");
                        iNUMstrLoc += "YAxis".Length;
                        if (!UInt32.TryParse(strcont.Substring(iNUMstrLoc), out uYAxisNum))
                        {
                            uYAxisNum = 0;
                        }
                    }
                    else if(uline == iLineZNum)
                    {	//update h file content
                        iNUMstrLoc = strcont.IndexOf("ZAxis");
                        iNUMstrLoc += "ZAxis".Length;
                        if (!UInt32.TryParse(strcont.Substring(iNUMstrLoc), out uZAxisNum))
                        {
                            uZAxisNum = 0;
                        }
                    }
                    else if (uline == (iLineXCont + strHFileContents.Count))
                    {
                        strXAxis = strcont.Substring(strcont.IndexOf('{'));
                        strXAxis = strXAxis.Replace('{', ' ');
                        strXAxis = strXAxis.Replace('}', ' ');
                        strXAxis = strXAxis.Replace(';', ' ');
                    }
                    else if (uline == (iLineYCont + strHFileContents.Count))
                    {
                        strYAxis = strcont.Substring(strcont.IndexOf('{'));
                        strYAxis = strYAxis.Replace('{', ' ');
                        strYAxis = strYAxis.Replace('}', ' ');
                        strYAxis = strYAxis.Replace(';', ' ');
                    }
                    else if (uline == (iLineZCont + strHFileContents.Count))
                    {
                        strZAxis = strcont.Substring(strcont.IndexOf('{'));
                        strZAxis = strZAxis.Replace('{', ' ');
                        strZAxis = strZAxis.Replace('}', ' ');
                        strZAxis = strZAxis.Replace(';', ' ');
                    }
                    else if (uline == iLineIDNum)
                    {
                        //strtmpIDNum = strcont;
                    }
                    else if (uline == (iLineIDCont + strHFileContents.Count))
                    {
                        //strtmpIDContent = strcont;
                    }
                    else if (uline > (iLineRCCont + strHFileContents.Count + strExtraCFile.Count))
                    {
                        if((strcont.IndexOf('{') != -1) && (strcont.IndexOf('}') != -1))
                            strRCTemp.Add(strcont);
                    }
                    else if (uline == (iLineExtraEquip + strHFileContents.Count + strCFileContents.Count))
                    {
                        //strtmpEquip = strcont;
                    }
                    else if (uline == (iLineExtraTester + strHFileContents.Count + strCFileContents.Count))
                    {
                        //strtmpTester = strcont;
                    }
                    uline += 1;
                }
                stmRead.Close();
                fsRead.Close();

                if((uXAxisNum != 0) && (uYAxisNum != 0) && (uZAxisNum != 0))
                {
                    listVolt.Clear();
                    listCurr.Clear();
                    listTemp.Clear();
                    strArrTmp = strXAxis.Split(',');
                    UInt32 utmp;
                    float ftmp;
                    if (strArrTmp.Length == uXAxisNum)    
                    {
                        for (int i = 0; i < strArrTmp.Length; i += 1)
                        {
                            if (!UInt32.TryParse(strArrTmp[i], out utmp))
                                utmp = 0;
                            listVolt.Add(utmp);
                        }
                        bReturn = true;
                    }
                    strArrTmp = strYAxis.Split(',');
                    if (strArrTmp.Length == uYAxisNum)
                    {
                        for (int i = 0; i < strArrTmp.Length; i += 1)
                        {
                            if (!float.TryParse(strArrTmp[i], out ftmp))
                                ftmp = 0;
                            listCurr.Add(ftmp);
                        }
                        bReturn &= true;
                    }
                    strArrTmp = strZAxis.Split(',');
                    if (strArrTmp.Length == uZAxisNum)
                    {
                        for (int i = 0; i < strArrTmp.Length; i += 1)
                        {
                            if (!float.TryParse(strArrTmp[i], out ftmp))
                                ftmp = 0;
                            listTemp.Add(ftmp / 10);
                        }
                        bReturn &= true;
                    }
                }
                if (strRCTemp.Count == (uYAxisNum * uZAxisNum))
                {
                    Int32 itmp = 0;
                    foreach (List<Int32> lston in listYpoints)
                    {
                        lston.Clear();
                    }
                    listYpoints.Clear();
                    for (int irc = 0; irc < strRCTemp.Count; irc++)
                    {
                        strtmpfu = strRCTemp[irc];
                        strtmpfu = strtmpfu.Replace('{', ' ');
                        strtmpfu = strtmpfu.Replace('}', ' ');
                        strtmpfu = strtmpfu.Replace(';', ' ');
                        strArrTmp = strtmpfu.Split(',');
                        List<Int32> lstOne = new List<Int32>();
                        for (int i = 0; i < strArrTmp.Length-1; i += 1)
                        {
                            if (!Int32.TryParse(strArrTmp[i], out itmp))
                                itmp = 0;
                            lstOne.Add(itmp);
                        }
                        listYpoints.Add(lstOne);
                    }
 
                    bReturn &= true;
                }
            }

            return bReturn;
        }

        public bool ReadOCVValueIntoVariables(ref List<Int32> listOCVPercent, ref List<Int32> listOCVvolt, string strTargetFile = null)
        {
            bool bReturn = false;
            FileStream fsRead = null;
            StreamReader stmRead = null;
            string strtmpfu = "";
            string strcont = "";
            UInt32 uline = 0;
            UInt32 uOCVNumber = 0;
            string strOCVContenttable = "";

            if (iIndxInFullPathList != 1)	//we are having new other data, so need to read OCV tmp file
            {
                if (strTargetFile == null)
                    strtmpfu = System.IO.Path.Combine(strTargetOutDrvFolder, strTmpFullPathList[1]);
                else
                    strtmpfu = strTargetFile;
                try
                {
                    fsRead = File.Open(strtmpfu, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    stmRead = new StreamReader(strtmpfu);
                }
                catch (Exception er)
                {
                    LibErrorCode.strVal01 = strtmpfu;
                    CreateNewDrvError(strtmpfu, LibErrorCode.IDS_ERR_TMK_DRV_FILE_READ);
                    return bReturn;
                }

                strcont = stmRead.ReadLine();	//skip one line for version string
                while ((strcont = stmRead.ReadLine()) != null)
                {
                    if ((uline == iLineOCVNum))
                    {	//find OCV_DATA_NUM line
                        //strHFileContents[uline] = strcont;
                        int iNUMstrLoc = strcont.IndexOf("OCV_DATA_NUM");
                        iNUMstrLoc += "OCV_DATA_NUM".Length;
                        if (!UInt32.TryParse(strcont.Substring(iNUMstrLoc), out uOCVNumber))
                        {
                            uOCVNumber = 0;
                        }
                    }
                    else if ((uline == (iLineOCVCont + strHFileContents.Count)))
                    {
                        //strCFileContents[uline - strHFileContents.Count] = strcont;
                        //found OCV content table
                        strOCVContenttable = strcont;
                        break;      //found OCV content, then we can break while loop
                    }
                    uline += 1;
                }
                stmRead.Close();
                fsRead.Close();
                if (uOCVNumber != 0)
                {
                    string strTmp = strOCVContenttable.Substring(strOCVContenttable.IndexOf('{'));
                    strTmp = strTmp.Replace('{', ' ');
                    strTmp = strTmp.Replace('}', ' ');
                    strTmp = strTmp.Replace(';', ' ');
                    var strArrTmp = strTmp.Split(',');
                    Int32 utmp;
                    Int32 itmp;
                    if (strArrTmp.Length == uOCVNumber * 2)    
                    {
                        for (int i = 0; i < strArrTmp.Length; i += 2)
                        {
                            //strTmp = strOCVPointArr[i].Replace('{', ' ');
                            //strTmp = strTmp.Replace('}', ' ');
                            //strArrTmp = strTmp.Split(',');
                            if (!Int32.TryParse(strArrTmp[i], out utmp))
                                utmp = 0;
                            listOCVvolt.Add(utmp);
                            if (!Int32.TryParse(strArrTmp[i + 1], out itmp))
                                itmp = 0;
                            listOCVPercent.Add(itmp);
                        }
                        bReturn = true;
                    }
                }
            }

            return bReturn;
        }

        public bool CalculateVTRandGenerateFile(ref UInt32 uErr, string OutFolder = null)
        {
            bool bReturn = false;

            return bReturn;
        }
        //(E170228)

        private bool ReadRCContentFromTmp()
		{
			bool bReturn = false;
			FileStream fsRead = null;
			StreamReader stmRead = null;
			string strtmpfu = "";
			string strcont = "";
			string strtmpIDNum = "", strtmpIDContent = "";
			string strtmpEquip = "", strtmpTester = "";
            UInt16 uline = 0;	

			if (iIndxInFullPathList != 0)	//we are having new other data, so need to read RC tmp file
			{
				strtmpfu = System.IO.Path.Combine(strTargetOutDrvFolder, strTmpFullPathList[0]);
				try
				{
					fsRead = File.Open(strtmpfu, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
					stmRead = new StreamReader(strtmpfu);
				}
				catch (Exception er)
				{
					LibErrorCode.strVal01 = strtmpfu;
					CreateNewDrvError(strtmpfu, LibErrorCode.IDS_ERR_TMK_DRV_FILE_READ);
					return bReturn;
				}

				strRCContent.Clear();
				strcont = stmRead.ReadLine();	//skip one line for version string
				while ((strcont = stmRead.ReadLine()) != null)
				{
					if ((uline == iLineXNum) ||
						(uline == iLineYNum) ||
						(uline == iLineZNum))
					{	//update h file content
						strHFileContents[uline] = strcont;
					}
					else if ((uline == (iLineXCont + strHFileContents.Count)) ||
								(uline == (iLineYCont + strHFileContents.Count)) ||
								(uline == (iLineZCont + strHFileContents.Count)))
					{
						strCFileContents[uline - strHFileContents.Count] = strcont;
					}
					else if (uline == iLineIDNum)
					{
						strtmpIDNum = strcont;
					}
					else if (uline == (iLineIDCont + strHFileContents.Count))
					{
						strtmpIDContent = strcont;
					}
					else if (uline > (iLineRCCont + strHFileContents.Count + strExtraCFile.Count))
					{
						strRCContent.Add(strcont);
					}
					else if (uline == (iLineExtraEquip + strHFileContents.Count+strCFileContents.Count))
					{
						strtmpEquip = strcont;
					}
					else if (uline == (iLineExtraTester + strHFileContents.Count + strCFileContents.Count))
					{
						strtmpTester = strcont;
					}
					uline += 1;
				}
				stmRead.Close();
				fsRead.Close();
				bReturn = true;
			}

			CombineBatteryIDString(strtmpIDContent);
			CombineEquipmentString(strtmpEquip);
			CombineTesterString(strtmpTester);

			return bReturn;
		}

		private bool ReadOCVContentFromTmp()
		{
			bool bReturn = false;
			FileStream fsRead = null;
			StreamReader stmRead = null;
			string strtmpfu = "";
			string strcont = "";
			string strtmpIDNum = "", strtmpIDContent = "";
			string strtmpEquip = "", strtmpTester = "";
            int uline = 0;

			if (iIndxInFullPathList != 1)	//we are having new other data, so need to read OCV tmp file
			{
				strtmpfu = System.IO.Path.Combine(strTargetOutDrvFolder, strTmpFullPathList[1]);
				try
				{
					fsRead = File.Open(strtmpfu, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
					stmRead = new StreamReader(strtmpfu);
				}
				catch (Exception er)
				{
					LibErrorCode.strVal01 = strtmpfu;
					CreateNewDrvError(strtmpfu, LibErrorCode.IDS_ERR_TMK_DRV_FILE_READ);
					return bReturn;
				}

				strcont = stmRead.ReadLine();	//skip one line for version string
				while ((strcont = stmRead.ReadLine()) != null)
				{
					if ((uline == iLineOCVNum))
					{	//update h file content
						strHFileContents[uline] = strcont;
					}
					else if ((uline == (iLineOCVCont + strHFileContents.Count)))
					{
						strCFileContents[uline - strHFileContents.Count ] = strcont;
					}
					else if (uline == iLineIDNum)
					{
						strtmpIDNum = strcont;
					}
					else if (uline == (iLineIDCont + strHFileContents.Count))
					{
						strtmpIDContent = strcont;
					}
					else if (uline == (iLineExtraEquip + strHFileContents.Count + strCFileContents.Count))
					{
						strtmpEquip = strcont;
					}
					else if (uline == (iLineExtraTester + strHFileContents.Count + strCFileContents.Count))
					{
						strtmpTester = strcont;
					}
					uline += 1;
				}
				stmRead.Close();
				fsRead.Close();
				bReturn = true;
			}

			CombineBatteryIDString(strtmpIDContent);
			CombineEquipmentString(strtmpEquip);
			CombineTesterString(strtmpTester);

			return bReturn;
		}

		private bool ReadChgContentFromTmp()
		{
			bool bReturn = false;
			FileStream fsRead = null;
			StreamReader stmRead = null;
			string strtmpfu = "";
			string strcont = "";
			string strtmpIDNum = "", strtmpIDContent = "";
			string strtmpEquip = "", strtmpTester = "";
            int uline = 0;

			if (iIndxInFullPathList != 2)	//we are having new other data, so need to read Charge tmp file
			{
				strtmpfu = System.IO.Path.Combine(strTargetOutDrvFolder, strTmpFullPathList[2]);
				try
				{
					fsRead = File.Open(strtmpfu, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
					stmRead = new StreamReader(strtmpfu);
				}
				catch (Exception er)
				{
					LibErrorCode.strVal01 = strtmpfu;
					CreateNewDrvError(strtmpfu, LibErrorCode.IDS_ERR_TMK_DRV_FILE_READ);
					return bReturn;
				}

				strcont = stmRead.ReadLine();	//skip one line for version string
				while ((strcont = stmRead.ReadLine()) != null)
				{
					if ((uline == iLineCHGNum))
					{	//update h file content
						strHFileContents[uline] = strcont;
					}
					else if ((uline == (iLineCHGCont + strHFileContents.Count)))
					{
						strCFileContents[uline - strHFileContents.Count] = strcont;
					}
					else if (uline == iLineIDNum)
					{
						strtmpIDNum = strcont;
					}
					else if (uline == (iLineIDCont + strHFileContents.Count))
					{
						strtmpIDContent = strcont;
					}
					else if (uline == (iLineExtraEquip + strHFileContents.Count + strCFileContents.Count))
					{
						strtmpEquip = strcont;
					}
					else if (uline == (iLineExtraTester + strHFileContents.Count + strCFileContents.Count))
					{
						strtmpTester = strcont;
					}
					uline += 1;
				}
				stmRead.Close();
				fsRead.Close();
				bReturn = true;
			}

			CombineBatteryIDString(strtmpIDContent);
			CombineEquipmentString(strtmpEquip);
			CombineTesterString(strtmpTester);

			return bReturn;
		}

		private bool GenerateTemperaryFile(ref UInt32 uErr)
		{
			bool bReturn = false;
			FileStream fswrite = null;
			StreamWriter FileContent = null;
			string strFullP = System.IO.Path.Combine(strTargetOutDrvFolder, strTargetTmpFileFullPath);

			try
			{
				fswrite = File.Open(strFullP, FileMode.Create, FileAccess.Write, FileShare.None);
				FileContent = new StreamWriter(fswrite, Encoding.Unicode);
			}
			catch (Exception ef)
			{
				LibErrorCode.strVal01 = strFullP;
				uErr = LibErrorCode.IDS_ERR_TMK_DRV_TEMP_FILE_CREATE;
				CreateNewDrvError(strFullP, uErr);
				return bReturn;
			}

			FileContent.WriteLine(TableSample.strTBMVersion);

			//foreach (string shh in strHHeaderComments)
			//{
				//FileContent.WriteLine(shh);
			//}

			foreach (string shf in strHFileContents)
			{
				FileContent.WriteLine(shf);
			}

			foreach (string scf in strCFileContents)
			{
				FileContent.WriteLine(scf);
			}

			foreach (string stex in strExtraCFile)
			{
				FileContent.WriteLine(stex);
			}

			foreach (string src in strRCContent)
			{
				FileContent.WriteLine(src);
			}

			FileContent.Close();
			bReturn = true;
			uErr = LibErrorCode.IDS_ERR_TMK_DRV_TABLE_CRATE;
			LibErrorCode.strVal01 = strTargetOutDrvFolder;

			return bReturn;
		}

        //(A170228)Francis, support VTR/TR table calculation
        public bool GenerateTemperaryFile(ref UInt32 uErr, List<string> lstrInHFile, List<string> lstrInCFile, string strTargetFileFull = null)
        {
            bool bReturn = false;
            FileStream fswrite = null;
            StreamWriter FileContent = null;
            //string strFullP = System.IO.Path.Combine(strTargetOutDrvFolder, strTargetTmpFileFullPath);

            try
            {
                fswrite = File.Open(strTargetFileFull, FileMode.Create, FileAccess.Write, FileShare.None);
                FileContent = new StreamWriter(fswrite, Encoding.Unicode);
            }
            catch (Exception ef)
            {
                LibErrorCode.strVal01 = strTargetFileFull;
                uErr = LibErrorCode.IDS_ERR_TMK_DRV_TEMP_FILE_CREATE;
                CreateNewDrvError(strTargetFileFull, uErr);
                return bReturn;
            }

            FileContent.WriteLine(TableSample.strTBMVersion);

            //foreach (string shh in strHHeaderComments)
            //{
            //FileContent.WriteLine(shh);
            //}

            foreach (string shf in lstrInHFile)
            {
                FileContent.WriteLine(shf);
            }

            foreach (string scf in lstrInCFile)
            {
                FileContent.WriteLine(scf);
            }

            foreach (string stex in strExtraCFile)
            {
                FileContent.WriteLine(stex);
            }

            foreach (string src in strRCContent)
            {
                FileContent.WriteLine(src);
            }

            FileContent.Close();
            bReturn = true;
            uErr = LibErrorCode.IDS_ERR_TMK_DRV_TABLE_CRATE;
            LibErrorCode.strVal01 = strTargetOutDrvFolder;

            return bReturn;
        }

		public bool GenerateDriverFiles(ref UInt32 uErr, string OutFolder = null, bool bCreateFalconLy = false, bool bFromOld = false)
		{
			bool bReturn = false;
			FileStream fswrite = null;
			StreamWriter FileContent = null;
			string strFullC = "";
			string strFullH = "";
			int i = 0;

			if (OutFolder != null)
			{
                if (!bCreateFalconLy)
                {
                    strFullC = System.IO.Path.Combine(OutFolder, strCFileFullName);
                    strFullH = System.IO.Path.Combine(OutFolder, strHFileFullName);
                }
                else
                {
                    if (bFromOld)
                    {
                        strFullC = System.IO.Path.Combine(OutFolder, strFalconLYCFileFullName);
                        strFullH = System.IO.Path.Combine(OutFolder, strFalconLYHFileFullName);
                    }
                    else
                    {
                        strFullC = System.IO.Path.Combine(System.IO.Path.Combine(OutFolder, TableInterface.strFalconLY), strFalconLYCFileFullName);
                        strFullH = System.IO.Path.Combine(System.IO.Path.Combine(OutFolder, TableInterface.strFalconLY), strFalconLYHFileFullName);
                    }
                }
			}
			else
			{
                if (!bCreateFalconLy)
                {
                    strFullC = System.IO.Path.Combine(strTargetOutDrvFolder, strFalconLYCFileFullName);
                    strFullH = System.IO.Path.Combine(strTargetOutDrvFolder, strFalconLYHFileFullName);
                }
                else
                {
                    if (bFromOld)
                    {
                        strFullC = System.IO.Path.Combine(strTargetOutDrvFolder, strFalconLYCFileFullName);
                        strFullH = System.IO.Path.Combine(strTargetOutDrvFolder, strFalconLYHFileFullName);
                    }
                    else
                    {
                        strFullC = System.IO.Path.Combine(System.IO.Path.Combine(strTargetOutDrvFolder, TableInterface.strFalconLY), strCFileFullName);
                        strFullH = System.IO.Path.Combine(System.IO.Path.Combine(strTargetOutDrvFolder, TableInterface.strFalconLY), strHFileFullName);
                    }
                }
			}

			try
			{
				fswrite = File.Open(strFullH, FileMode.Create, FileAccess.Write, FileShare.None);
				FileContent = new StreamWriter(fswrite, Encoding.Default);
			}
			catch (Exception eh)
			{
				LibErrorCode.strVal01 = strFullH;
				uErr = LibErrorCode.IDS_ERR_TMK_DRV_H_FILE_CREATE;
				CreateNewDrvError(strFullH, uErr);
				return bReturn;
			}

			i = 0;
			foreach (string shc in strHHeaderComments)
			{
				if (i == iLineCmtHCFile)
				{
					FileContent.WriteLine(shc + ".h");
				}
				else
				{
					FileContent.WriteLine(shc);
				}
				i++;
			}

			//(M141027)Francis,
			i = 0;
			foreach (string shf in strHFileContents)
			{
				//(M141125)
				//(A141027)Francis
				if (i == strHFileContents.Count - 2)	//add at last 2 line
				{
					AppendTableHFile(FileContent);
				}
				//(E141027)
				FileContent.WriteLine(shf);
                //(A170307)Francis, append VTR/TR table to H file
                if (bCreateFalconLy)
                {
                    if(i == iLineVTRShifted)
                    {
                        AppendTableHFileVTR(FileContent);
                    }
                }
                //(E170307)
				i += 1;
			}
			//(E141027)
			FileContent.Close();
			fswrite.Close();

			try
			{
				fswrite = File.Open(strFullC, FileMode.Create, FileAccess.Write, FileShare.None);
				FileContent = new StreamWriter(fswrite, Encoding.Default);
			}
			catch (Exception ec)
			{
				LibErrorCode.strVal01 = strFullC;
				uErr = LibErrorCode.IDS_ERR_TMK_DRV_C_FILE_CREATE;
				CreateNewDrvError(strFullC, uErr);
				return bReturn;
			}

			i = 0;
			foreach (string scc in strHHeaderComments)
			{
				if (i == iLineCmtHCFile)
				{
					FileContent.WriteLine(scc + ".c");
				}
				else
				{
					FileContent.WriteLine(scc);
				}
				i++;
			}
			i = 0;
			foreach (string scf in strCFileContents)
			{
				//(A141125)Francis
				if (i == iLineIDCont)
				{
					AppendTableCFile(FileContent);
				}
				//(E141125)
				FileContent.WriteLine(scf);
				i += 1;
			}

			foreach (string src in strRCContent)
			{
				FileContent.WriteLine(src);
			}

			FileContent.WriteLine(strCFileContentLastOne);
            
            //(A170307)Francis, append VTR/TR table to end of C file
            if (bCreateFalconLy)
            {
                AppendTableCFileVTR(FileContent);
            }
            //(E170307)

            FileContent.Close();
			fswrite.Close();

			bReturn = true;
			uErr = LibErrorCode.IDS_ERR_TMK_DRV_FILES_CRATE;
			LibErrorCode.strVal01 = OutFolder;

			return bReturn;
		}

		private void CombineBatteryIDString(string inidbat)
		{
			char[] chSeperate = new char[] { ',' };
			string sttmp;
			string[] sttmparr;
			int unumMy = 0, unumIn = 0;

			unumMy = inidbat.IndexOf('{');
			unumIn = inidbat.IndexOf('}');
			if ((unumIn != -1) && (unumMy != -1))
			{
				sttmp = inidbat.Substring(unumMy + 1, unumIn - unumMy - 1);
				sttmp = sttmp.Replace('"', ' ');
				sttmp = sttmp.Trim();
				sttmparr = sttmp.Split(chSeperate, StringSplitOptions.None);
				for (int i = 0; i < sttmparr.Length; i++)
				{
					if(sttmparr[i].Length >=1)
					{
						strAndroidBIDTmp.Add(sttmparr[i].Trim());
					}
				}
				unumMy = 1;
			}
		}

		private void CombineEquipmentString(string inequip)
		{
			char[] chSeperate = new char[] { ',' };
			string sttmp;
			string[] sttmparr;
			int unumMy = 0, unumIn = 0;

			unumMy = inequip.IndexOf(':');
			//unumIn = inequip.IndexOf('}');
			if ((unumIn != -1) && (unumMy != -1))
			{
				sttmp = inequip.Substring(unumMy + 1);
				sttmp = sttmp.Replace('"', ' ');
				sttmp = sttmp.Trim();
				sttmparr = sttmp.Split(chSeperate, StringSplitOptions.None);
				for (int i = 0; i < sttmparr.Length; i++)
				{
					if (sttmparr[i].Length >= 1)
					{
						strAndroidEqpTmp.Add(sttmparr[i].Trim());
					}
				}
				//unumMy = 1;
			}
		}

		private void CombineTesterString(string intester)
		{
			char[] chSeperate = new char[] { ',' };
			string sttmp;
			string[] sttmparr;
			int unumMy = 0, unumIn = 0;

			unumMy = intester.IndexOf(':');
			//unumIn = intester.IndexOf('}');
			if ((unumIn != -1) && (unumMy != -1))
			{
				sttmp = intester.Substring(unumMy + 1);
				sttmp = sttmp.Replace('"', ' ');
				sttmp = sttmp.Trim();
				sttmparr = sttmp.Split(chSeperate, StringSplitOptions.None);
				for (int i = 0; i < sttmparr.Length; i++)
				{
					if (sttmparr[i].Length >= 1)
					{
						strAndroidTstTmp.Add(sttmparr[i].Trim());
					}
				}
				//unumMy = 1;
			}
		}

		private void AppendTableHFile(StreamWriter swTarget)
		{
			/*(M141119)Francis, bugid=15257
			swTarget.WriteLine(string.Format("#define TABLEVERSION\t\t\t\"{0}-{1}\"",
				strUserInput[(int)MakeParamEnum.MakeVersion],
				strUserInput[(int)MakeParamEnum.MakeDate]));
			*/
			/*(M141125)Francis, as Eason request
			swTarget.WriteLine(string.Format("#define TABLEVERSION\t\t\t\"{0}-{1}-{2}\"",
				strUserInput[(int)MakeParamEnum.MakeVersion],
				strUserInput[(int)MakeParamEnum.MakeDate],
				strAndroidBatteryModel));
			swTarget.WriteLine();
			*/
			swTarget.WriteLine(string.Format("extern const char *table_version;"));
			swTarget.WriteLine();
		}

		private void AppendTableCFile(StreamWriter swTarget)
		{
			swTarget.WriteLine(string.Format("const char * table_version = \"{0}-{1}-{2}\";",
				strUserInput[(int)MakeParamEnum.MakeVersion],
				strUserInput[(int)MakeParamEnum.MakeDate],
				strAndroidBatteryModel));
			swTarget.WriteLine();
		}

        //(A170307)Francis, append VTR/TR table to H file
        private void AppendTableHFileVTR(StreamWriter swTarget)
        {
            foreach(string svt in strVTRHFileContents)
            {
                swTarget.WriteLine(svt);
            }
        }

        private void AppendTableCFileVTR(StreamWriter swTarget)
        {
            foreach(string svtc in strVTRCFileContents)
            {
                swTarget.WriteLine(svtc);
            }

            foreach (string scvr in strIRtableContent)
            {
                swTarget.WriteLine(scvr);
            }

            swTarget.WriteLine(strIRtableCFileContentLastOne);

            foreach(string srt in strRTableCFileContents)
            {
                swTarget.WriteLine(srt);
            }

            foreach(string srtc in strRtableContent)
            {
                swTarget.WriteLine(srtc);
            }

            swTarget.WriteLine(strRTableCFileContentLastOne);
        }
        //(E170307)

		//(A141118)Francis, add for create error log when generating Driver
		private void CreateNewDrvError(string strtagfile, UInt32 iErrorCode)
		{
			TableError newdrverr = new TableError(strtagfile, UInt32.MaxValue, float.MaxValue, fAndroidFullCap, float.MaxValue, iErrorCode);

			drvError.Add(newdrverr);
		}
		//(E141118)

	}
}
