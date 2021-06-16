using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Globalization;
using System.Xml;
using Cobra.Common;
using Cobra.EM;

namespace Cobra.ExperPanel
{
	public class ExperViewMode
	{
        //static public UInt32 SectionElementFlag = 0xFFFF0000;
        static public UInt32 SectionElementFlag = 0x00FF0000;
        static public UInt32 NonvolatileElement = 0x00020000;   //Leon: Efuse, EEPROM, YFLASH and etc.
		static public  UInt32 OperationElement = 0x00030000;
		static public  UInt32 TestButtonElement = 0x00040000;
		static public  UInt32 TesTOperElement = 0x00050000;
        static public UInt16 uUISmoothMax = 0x0032;
        static private string strTemp = string.Empty;
		
		private bool m_canGroup = false;
		public bool CanGroup
		{
			get { return m_canGroup; }
			set { m_canGroup = value;}
		}
		public Device devParent { get; set; }
		public ExperControl ctrParent { get; set; }
		public string strSFLName { get; set; }
		public byte yBitNum { get; set; }           //8: 8-bit length of each register; 16: 16-bit length
		public bool bEngModeFromXML { get; set; }   //false: in Customer Mode; true: in Engineer Mode
		public bool bTrimModeFromXML { get; set; }  //false: no trimming mode support in XMl setting; true: otherwise

        private ParamContainer m_pmcntDMParameterList = new ParamContainer();
        public ParamContainer pmcntDMParameterList
        {
            get { return m_pmcntDMParameterList; }
            set { m_pmcntDMParameterList = value; }
        }

		private AsyncObservableCollection<ExperModel> m_ExpRegisterList = new AsyncObservableCollection<ExperModel>();
		public AsyncObservableCollection<ExperModel> ExpRegisterList
		{
			get { return m_ExpRegisterList; }
			set { m_ExpRegisterList = value; }
		}

		private AsyncObservableCollection<ExperModel> m_ExpProList = new AsyncObservableCollection<ExperModel>();
		public AsyncObservableCollection<ExperModel> ExpProList
		{
			get { return m_ExpProList; }
			set { m_ExpProList = value; }
		}

		private AsyncObservableCollection<ExperModel> m_ExpTestRegList = new AsyncObservableCollection<ExperModel>();
		public AsyncObservableCollection<ExperModel> ExpTestRegList
		{
			get { return m_ExpTestRegList; }
			set { m_ExpTestRegList = value; }
		}

		private AsyncObservableCollection<ExperTestButtonModel> m_ExpTestBtnList = new AsyncObservableCollection<ExperTestButtonModel>();
		public AsyncObservableCollection<ExperTestButtonModel> ExpTestBtnList
		{
			get { return m_ExpTestBtnList; }
			set { m_ExpTestBtnList = value; }
		}

		public ListCollectionView lstclRegisterList { get; set; } //
		public ListCollectionView lstclTestRegList { get; set; }

		//private List<ExperXMLData> m_XMLDataOpRegList = new List<ExperXMLData>();

		/// <summary>
		/// Constructor, parse xml definition and create/save in DataStructure
		/// </summary>
		/// <param name="pParent"></param>
		/// <param name="parent"></param>
		public ExperViewMode(object pParent, object parent)
		{
			bool bPrepare = true;
			#region Initialization of Device / ExperControl / SFLname

			devParent = (Device)pParent;
			if (devParent == null) return;

			ctrParent = (ExperControl)parent;
			if (ctrParent == null) return;

			strSFLName = ctrParent.sflname;
			if (String.IsNullOrEmpty(strSFLName)) return;

			#endregion

			bEngModeFromXML = true;	//default as true; due to we will use AND  operator to collect XML
			bTrimModeFromXML = false; //default as flase, cause it will be much easier on bit--AND/OR operation
            uUISmoothMax = 0x0028;      //issue_id=895, adjust Smooth threshold
            strTemp = string.Empty;
            pmcntDMParameterList = devParent.GetParamLists(strSFLName);
			//m_XMLDataOpRegList.Clear();	//make sure it is clear
			foreach (Parameter param in pmcntDMParameterList.parameterlist)
			{
				if (param == null) continue;
				if ((param.guid & SectionElementFlag) == TestButtonElement)
					bPrepare &= ParseTestModeParamToData(param);
				else if (((param.guid & SectionElementFlag) == OperationElement) || ((param.guid & SectionElementFlag) == NonvolatileElement) ||
							((param.guid & SectionElementFlag) == TesTOperElement))
					bPrepare &= ParseOpRegParameterToData(param);
				else
				{	//Therefore, if there are some Element in XML with Expert private section, but are not in abover 3 ElementDefine
					//here wiil be ran
					bPrepare = false;
				}

				if(!bPrepare)
				{
					MessageBox.Show(LibErrorCode.GetErrorDescription(LibErrorCode.IDS_ERR_EXPSFL_XML));
					return;
				}
			}
			CalculateModelRegister();
			BackExpertUI();

			lstclRegisterList = new ListCollectionView(ExpRegisterList);
			if (m_ExpRegisterList.Count <= uUISmoothMax)
			{
				if(CanGroup)	lstclRegisterList.GroupDescriptions.Add(new PropertyGroupDescription("strGroupReg"));
			}

			lstclTestRegList = new ListCollectionView(ExpTestRegList);
			lstclTestRegList.GroupDescriptions.Add(new PropertyGroupDescription("strGroupReg"));
		}

		/// <summary>
		/// Parse XML node data into ExperXMLData, a temporary data structure, to store XML data
		/// </summary>
		/// <param name="paramIn">Parameter data structure, input value</param>
		/// <returns>true: if read xml node data successful; otherwise return false</returns>
		private bool ParseOpRegParameterToData(Parameter paramIn)
		{
			byte ydata = 0;
			bool bdata = false;
            byte yValid = 0;            //id=647

			ExperXMLData xmlData = new ExperXMLData();
//			model.PropertyChanged += new PropertyChangedEventHandler(SFL_Parameter_PropertyChanged);
			foreach (DictionaryEntry de in paramIn.sfllist[strSFLName].nodetable)
			{
				switch (de.Key.ToString())
				{
					case "CustomerMode":
						{
							if (!Boolean.TryParse(de.Value.ToString(), out bdata))
							{
								xmlData.bCustMode = true;
							}
							else
							{
								xmlData.bCustMode = bdata;
							}
							bEngModeFromXML &= bdata;	//if there is any false in XML, means X version of XML is used, set as false
							break;
						}
					case "BitTotal":
						{
							if (!Byte.TryParse(de.Value.ToString(), NumberStyles.HexNumber, null as IFormatProvider, out ydata))
							{
								xmlData.yTotal = 0x08;
							}
							else
							{
								xmlData.yTotal = Convert.ToByte(de.Value.ToString(), 16);
							}
                            //overwrite by setting in ExtensionDescriptor.xml
                            if (ctrParent.yBitTotal != 0x00)
                            {
                                xmlData.yTotal = ctrParent.yBitTotal;
                            }
							break;
						}
					case "Index":
						{
							//(M191220)Francis, modify for 16bit index display
							//xmlData.yIndex = Convert.ToByte(de.Value.ToString(), 16);
							xmlData.u32Index = Convert.ToUInt32(de.Value.ToString(), 16);
							yValid++;
							break;
						}
					case "BitStart":
						{
							xmlData.yBitStart = Convert.ToByte(de.Value.ToString(), 16);
                            yValid++;
							break;
						}
					case "Length":
						{
							xmlData.yLength = Convert.ToByte(de.Value.ToString(), 16);
                            yValid++;
							break;
						}
					case "Value":		//looks like we don't need "Value" in xml
						{
							//xmlData.yValue = Convert.ToByte(de.Value.ToString(), 16);
							break;
						}
					case "Read":
						{
							if (!Boolean.TryParse(de.Value.ToString(), out bdata))
							{
								xmlData.bRead = false;
							}
							else
							{
								xmlData.bRead = bdata;
							}
							break;
						}
					case "Write":
						{
							if (!Boolean.TryParse(de.Value.ToString(), out bdata))
							{
								xmlData.bWrite = false;
							}
							else
							{
								xmlData.bWrite = bdata;
							}
							break;
						}
					case "Description":
						{
							xmlData.strDescrip = de.Value.ToString();
							break;
                        }
                    case "Tips":
                        {
                            xmlData.strBitTips = de.Value.ToString();
                            break;
                        }
					case "TestMode":
						{
							xmlData.strTestMode = de.Value.ToString();
							break;
						}
					case "Group":
						{
							xmlData.strGroup = de.Value.ToString();
                            if(strTemp == string.Empty)
                            {
                                strTemp = xmlData.strGroup;
                            }
                            else
                            {
                                if(!strTemp.Equals(xmlData.strGroup))
                                {
                                    uUISmoothMax = 0xFFFF;
                                }
                            }
							break;
						}
					case "Unit":
						{
							xmlData.strUnit = de.Value.ToString();
							break;
						}
					case "RegisterName":
						{
							xmlData.strRegName = de.Value.ToString();
							break;
						}
					case "bPhyDataFromList":
						{
							if (!Boolean.TryParse(de.Value.ToString(), out bdata))
							{
								xmlData.bPhyDataFromList = false;
							}
							else
							{
								xmlData.bPhyDataFromList = bdata;
							}
							break;
						}

				}	//switch
			}

			xmlData.pmrXDParent = paramIn;
			xmlData.u32Guid = paramIn.guid;
			if (xmlData.strGroup == string.Empty) CanGroup |= false;
			else CanGroup = true;

			if ((yValid < 3) || (ctrParent.bSharedPublic))
            {
                ParsePublicInfor(ref xmlData, ref yValid);
            }

            ConvertXMLDataToModel(ref xmlData);

			return true;
		}

		/// <summary>
		/// Convert temporary data structure to UI binding data structure 
		/// </summary>
		/// <param name="xmldataIn">ExperXMLData data structure, input value</param>
		private void ConvertXMLDataToModel(ref ExperXMLData xmldataIn)
		{
			ExperModel mdltemp;
			bool bAdd = false;
			byte yBitStartLoc = xmldataIn.yBitStart;
			byte yBitLength = 0;
			byte yTotalLeng = (byte)(xmldataIn.yTotal & 0x38);
			byte yLoHi = (byte)(xmldataIn.yTotal & 0xE0);
			int iLoopParse = 0;
			//(M191220)Francis, modify for 16bit index display
			//byte yTargetIndex = 0;
			UInt32 u32TargetIndex = 0;
			byte i, j;
			AsyncObservableCollection<ExperModel> Listtmp = null;

            if ((xmldataIn.u32Guid & SectionElementFlag) == OperationElement || (xmldataIn.u32Guid & SectionElementFlag) == NonvolatileElement)
			{
				xmldataIn.strTestMode = "Normal Mode";
				Listtmp = ExpRegisterList;
				//m_XMLDataOpRegList.Add(xmldataIn);						//gather Operation register together
			}
			else if ((xmldataIn.u32Guid & SectionElementFlag) == TesTOperElement)
			{
				Listtmp = ExpTestRegList;
			}

			//if bit_length in xml is not 8-bits or 16-bits long, force it as 8-bits
			if ((yTotalLeng != 0x08) && (yTotalLeng != 0x10) && (yTotalLeng != 0x20))
			{
				yTotalLeng = 0x08;
			}

			iLoopParse = (int)((xmldataIn.yLength -1) / yTotalLeng);
			//for test
			if (ctrParent.bFranTestMode && (iLoopParse > 0))
			{
				System.Windows.Forms.Application.DoEvents();
			}

			if (iLoopParse >= 1)
			{
				System.Windows.Forms.Application.DoEvents();
			}

			for (j = 0; j <= iLoopParse; j++)
			{
				//if bit 7 of BitTotal == 0; high part of threshold are increasing
				if (yLoHi == 0)
				{
					//(M191220)Francis, modify for 16bit index display
					//yTargetIndex = (byte)(xmldataIn.yIndex + j);
					u32TargetIndex = (UInt32)(xmldataIn.u32Index + j);
				}
				else
				{   //if bit 7 of BitTotal == 1; high part of threshold are decreasing
					//(M191220)Francis, modify for 16bit index display
					//yTargetIndex = (byte)(xmldataIn.yIndex - j);
					u32TargetIndex = (UInt32)(xmldataIn.u32Index - j);
				}

				//mdltemp = SearchExpModelByIndex((UInt16)(yTargetIndex), Listtmp);
				//(M191220)Francis, modify for 16bit index display
				//mdltemp = SearchExpModelByIndex((UInt16)(yTargetIndex), xmldataIn, Listtmp);
				mdltemp = SearchExpModelByIndex(u32TargetIndex, xmldataIn, Listtmp);

				//test
				//(M191220)Francis, modify for 16bit index display
				if ((u32TargetIndex >= 0x30) && (u32TargetIndex <= 0x3f))
				{
					u32TargetIndex -= 1;
					u32TargetIndex += 1;
				}

				if (mdltemp == null)
				{
					mdltemp = new ExperModel();
					bAdd = true;
				}

				if( xmldataIn.yLength <= ((j+1)*yTotalLeng - yBitStartLoc))
				{	//length <= total length - start location; ie lenght is samller than remaining vacancy
					yBitLength = (byte)(xmldataIn.yLength - yBitLength);
				}
				else
				{	//if not
					if(j ==0)
					{
						yBitLength = (byte)(yTotalLeng - yBitStartLoc);
					}
					else
					{
						if(xmldataIn.yLength <= ((j+1)*yTotalLeng - xmldataIn.yBitStart))
						{
							yBitLength = (byte)(xmldataIn.yLength - (yTotalLeng - xmldataIn.yBitStart));
						}
						else
						{
							yBitLength = yTotalLeng;
						}
					}
				}
				if(((int)yBitLength < 0) || (yBitLength > yTotalLeng))
				{
					//Error, should not be here
					MessageBox.Show("XML Error detected by Expert", "Expert SFL");
					return;
				}

//				if (yBitLength > yTotalLeng) yBitLength = yTotalLeng;
				for (i = 0; i < yBitLength; i++)
				{
//					mdltemp.ArrRegComponet[i + yBitStartLoc].strBit = string.Format("bit__{0:X1}", i + yBitStartLoc);
					mdltemp.ArrRegComponet[i + yBitStartLoc].yBitValue = 0;
					//mdltemp.ArrRegComponet[i + yBitStartLoc].bBitEnable = (xmldataIn.bRead & xmldataIn.bWrite);
					mdltemp.ArrRegComponet[i + yBitStartLoc].bBitEnable =
						((xmldataIn.bRead | xmldataIn.bWrite) & (xmldataIn.bCustMode | ctrParent.bEngModeRunning));
					mdltemp.ArrRegComponet[i + yBitStartLoc].bRead = xmldataIn.bRead;
					mdltemp.ArrRegComponet[i + yBitStartLoc].bWrite = 
						(xmldataIn.bWrite & (xmldataIn.bCustMode | ctrParent.bEngModeRunning));
					if(mdltemp.ArrRegComponet[i + yBitStartLoc].bBitEnable)
						mdltemp.ArrRegComponet[i + yBitStartLoc].strBitDescrip = xmldataIn.strDescrip;
					else
						mdltemp.ArrRegComponet[i + yBitStartLoc].strBitDescrip = ExperBitComponent.BitDescrpDefault;
                    mdltemp.ArrRegComponet[i + yBitStartLoc].strBitTips = xmldataIn.strBitTips;
					mdltemp.ArrRegComponet[i + yBitStartLoc].strUnit = xmldataIn.strUnit;
					if (i == 0)
					{
						mdltemp.ArrRegComponet[i + yBitStartLoc].yDescripVisiLgth = yBitLength;
						mdltemp.ArrRegComponet[i + yBitStartLoc].pmrBitParent = xmldataIn.pmrXDParent;
						mdltemp.ArrRegComponet[i + yBitStartLoc].u32Guid = xmldataIn.u32Guid;
						mdltemp.ArrRegComponet[i + yBitStartLoc].strGroupBit = xmldataIn.strGroup;
						mdltemp.ArrRegComponet[i + yBitStartLoc].dbPhyValue = 0.0;	//(A141219)Francis, assign physical value a default value
						mdltemp.ArrRegComponet[i + yBitStartLoc].expXMLdataParent = xmldataIn;
					}
					else
					{
						mdltemp.ArrRegComponet[i + yBitStartLoc].yDescripVisiLgth = 0;
						mdltemp.ArrRegComponet[i + yBitStartLoc].pmrBitParent = null;
						mdltemp.ArrRegComponet[i + yBitStartLoc].u32Guid = 0;
						mdltemp.ArrRegComponet[i + yBitStartLoc].strGroupBit = "";
					}
					//mdltemp.ArrRegComponet[i + yBitStartLoc].yDescripVisiLgth = (i == yBitStartLoc) ? yBitLength : 0;
                    //(M180821)Francis, issue_id=865, sync solution that don't convert physical value from DEM, jsut convert DEC to HEX simply
                    mdltemp.ArrRegComponet[i + yBitStartLoc].bShowPhysical = !ctrParent.bForceHidePro;
                }

				if (bAdd)
				{
					//(M191220)Francis, modify for 16bit index display
					//mdltemp.u16RegNum = yTargetIndex;
					mdltemp.u32RegNum = u32TargetIndex;
					mdltemp.strRegNum = string.Format("0x{0:X8}", mdltemp.u32RegNum);
					mdltemp.yRegLength = yTotalLeng;
					mdltemp.strTestXpr = xmldataIn.strTestMode;
					mdltemp.strGroupReg = xmldataIn.strGroup;
					//					ExpRegisterList.Add(mdltemp);
					Listtmp.Add(mdltemp);
					bAdd = false;
					//(A150106)Francis
					if ((yTotalLeng == xmldataIn.yLength) && (xmldataIn.strRegName.Length == 0))
					{
						xmldataIn.strRegName = xmldataIn.strDescrip;
						mdltemp.strRegName = xmldataIn.strDescrip;
					}
					else if (yTotalLeng < xmldataIn.yLength)
					{
						xmldataIn.strRegName = xmldataIn.strDescrip;
						if (j != iLoopParse)
						{
							mdltemp.strRegName = xmldataIn.strDescrip + "__L";
						}
						else
						{
							mdltemp.strRegName = xmldataIn.strDescrip + "__H";
						}
					}
					else
					{
						mdltemp.strRegName = xmldataIn.strRegName;
					}
					//(E150106)
				}	//if (bAdd)
				else
				{
                    if (mdltemp.strRegName.Length == 0)
    					mdltemp.strRegName = ExperBitComponent.RegDescrpDefault;
				}
				yBitStartLoc = 0;	//after resigning to ArrRegComponet[], yBitStartLoc start from 0 if threshold length is bigger than total length
			}	//for (j = 0; j <= iLoopParse; j++)
		}

		/// <summary>
		/// XML is saving data as threshold base format, threshold would be 1-bit length or up to 13,14,15-bits length
		/// After parsing xml data, data saved in ExpRegister only finish ArrRegComponent[] data and 
		/// ExperModel.u16RegNum, and ExperModel.strRegNum, and ExperModel.yRegLength
		/// Here needs to combine bit information to calculate bRead, bWrite, and u16RegVal
		/// </summary>
		public void CalculateModelRegister()
		{
			int iNonReserved;
			ExperBitComponent ebcRecord = null;

			ExpRegisterList.Sort(x => x.u32RegNum);
			foreach (ExperModel expm in ExpRegisterList)
			{
				iNonReserved = 0;
				ebcRecord = null;
				expm.SumRegisterValue();
				//(A150113)Francis
				if ((expm.strRegName.Equals(ExperBitComponent.RegDescrpDefault)) ||
					(expm.strRegName.Length == 0)) 
				{
					foreach (ExperBitComponent ebctmp in expm.ArrRegComponet)
					{
						if (!ebctmp.strBitDescrip.Equals(ExperBitComponent.BitDescrpDefault))
						{
							iNonReserved += 1;
							if (ebctmp.yDescripVisiLgth != 0)
							{
								ebcRecord = ebctmp;
							}
						}
					}
					if ((iNonReserved != 0) && (ebcRecord != null))
					{
						if (iNonReserved == ebcRecord.yDescripVisiLgth)
						{
							expm.strRegName = ebcRecord.strBitDescrip;
						}
					}
				}
				//(E150113)
			}

			//ExpTestBtnList.Sort(x => x.u32Guid);
			ExpTestBtnList.Sort(x => x.yOrder);

			ExpTestRegList.Sort(x => x.u32RegNum);
			foreach (ExperModel expm in ExpTestRegList)
			{
				expm.SumRegisterValue();
			}
		}

		/// <summary>
		/// Find ExperModel data structure according to Index value
		/// </summary>
		/// <param name="yIndexIn">input index value, must be register index</param>
		/// <returns>return ExperModel data structure if found; otherwise null</returns>
		/*
		public ExperModel SearchExpModelByIndex(UInt16 yIndexIn)
		{
			ExperModel expmdltmp = null;

//			if (tagList == null)
//				return null;
			foreach (ExperModel expm in ExpRegisterList)
//			foreach(ExperModel expm in tagList)
			{
				if (expm.u16RegNum == yIndexIn)
				{
					expmdltmp = expm;
					break;
				}
			}

			return expmdltmp;
		}
		*/

		/// <summary>
		/// Find ExperModel data structure according to Index value
		/// </summary>
		/// <param name="u32Tag">input, Index value of target parameter</param>
		/// <param name="xmlDataIn">input, ExperXMLData that try to find</param>
		/// <param name="tagList">input, AsyncObservalbeColletion list that will be searched for 1st input object</param>
		/// <returns></returns>
		public ExperModel SearchExpModelByIndex(UInt32 u32Tag, ExperXMLData xmlDataIn, AsyncObservableCollection<ExperModel> tagList)
		{
			ExperModel expmdltmp = null;

			if (tagList == null)
				return null;
			//foreach (ExperModel expm in ExpRegisterList)
			foreach (ExperModel expm in tagList)
			{
				if (expm.u32RegNum == u32Tag)
				{
					if ((expm.strTestXpr.Equals(xmlDataIn.strTestMode)) &&
						(expm.strGroupReg.Equals(xmlDataIn.strGroup)))
					{
						expmdltmp = expm;
						break;
					}
				}
			}

			return expmdltmp;
		}

		/// <summary>
		/// Read 1 or all Register  from chip, it will read expmIn target register. If expmIn is null, it will read all register
		/// Default of expmIn is null
		/// </summary>
		/// <param name="tskmsgExper">in/out param,TASKMessage to communicate with DEM</param>
		/// <param name="expmIn">input param, the ExperModel structure of target register to read. Default is read all register if null </param>
		/// <returns>true: if read OK; otherwise return false</returns>
		public bool ReadRegFromDevice(ref TASKMessage tskmsgExper, ExperModel expmIn = null)
		{
			//UInt32 u32Return = LibErrorCode.IDS_ERR_SUCCESSFUL;
			Parameter pmrtmp = null;
			ParamContainer pmCtntmp = new ParamContainer();
			AsyncObservableCollection<ExperModel> explisttmp = new AsyncObservableCollection<ExperModel>();
			ParamContainer pmPhytmp = new ParamContainer();

			if (devParent.bBusy)
			{
				tskmsgExper.errorcode = LibErrorCode.IDS_ERR_EM_THREAD_BKWORKER_BUSY;
				return false;
			}
			else
			{
                ctrParent.WaitControlExperSetup(true, 10, "Trying to communicate chip.", 10);
                if (!ctrParent.bFranTestMode)
                {
                    devParent.bBusy = true;
                    if (!GetDeviceInfor(ref tskmsgExper))		//errorcode will be assigned in GetDeviceInfor
                    {
                        devParent.bBusy = false;
                        ctrParent.ExperWaitControlClear();
                        return false;
                    }
				}
			}

            ctrParent.WaitControlExperSetup(true, 20, "gathering register.", 10);
			if (expmIn != null)
			{
				pmrtmp = expmIn.GetParentParameter();
				if (pmrtmp == null)
				{	//cannot find parameter in ExperBitComponent
					//return u32Return;
					devParent.bBusy = false;
					tskmsgExper.errorcode = LibErrorCode.IDS_ERR_EXPSFL_OPREG_NOT_FOUND;
                    ctrParent.ExperWaitControlClear();
					return false;
				}
				pmCtntmp.parameterlist.Add(pmrtmp);
				explisttmp.Add(expmIn);
//				foreach(ExperXMLData xmda in m_XMLDataOpRegList)
//				{
//					if(((xmda.pmrXDParent.guid & 0x0000FF00) >> 8) == expmIn.u16RegNum)
//					{
//						pmPhytmp.parameterlist.Add(xmda.pmrXDParent);
//					}
//				}
				foreach (ExperBitComponent ebcArr in expmIn.ArrRegComponet)
				{
					if (ebcArr.yDescripVisiLgth != 0)
					{
						if (ebcArr.expXMLdataParent != null)
						{
							//break;		//perhaps ebcArr is bitx8~bitxF or Reserved
							pmPhytmp.parameterlist.Add(ebcArr.expXMLdataParent.pmrXDParent);
						}
					}
				}
			}
			else
			{
				//AsyncObservableCollection<ExperModel> asTmp =
				//	ctrParent.dtgRegistersPresent.ItemsSource as AsyncObservableCollection<ExperModel>;
				ListCollectionView lstt = ctrParent.dtgRegistersPresent.ItemsSource as ListCollectionView;
				IList<ExperModel> list = lstt.SourceCollection as IList<ExperModel>;
				AsyncObservableCollection<ExperModel> asTmp = null; 
				if (list != null)
				{
					asTmp = new AsyncObservableCollection<ExperModel>();
					asTmp.Clear();
					foreach (ExperModel item in list)
					{
						if (item.bXprRegShow)
						{
							asTmp.Add((ExperModel)item);
						}
					}
				}

				if (asTmp == null)
				{
					asTmp = ExpRegisterList;
//					foreach(ExperXMLData xmld in m_XMLDataOpRegList)
//					{
//						pmPhytmp.parameterlist.Add(xmld.pmrXDParent);
//					}
				}
				//foreach (ExperModel expmeach in ExpRegisterList)
				foreach (ExperModel expmeach in asTmp)
				{
					pmrtmp = expmeach.GetParentParameter();
					if (pmrtmp == null)
					{	//cannot find parameter in ExperBitComponent
						//return u32Return;
						devParent.bBusy = false;
						tskmsgExper.errorcode = LibErrorCode.IDS_ERR_EXPSFL_OPREG_NOT_FOUND;
                        ctrParent.ExperWaitControlClear();
						return false;
					}
					if (expmeach.bXprRegShow)
					{
						pmCtntmp.parameterlist.Add(pmrtmp);
						explisttmp.Add(expmeach);
						foreach (ExperBitComponent ebcArr in expmeach.ArrRegComponet)
						{
							if (ebcArr.yDescripVisiLgth != 0)
							{
								if (ebcArr.expXMLdataParent != null)
								{
									//break;		//perhaps ebcArr is bitx8~bitxF
									pmPhytmp.parameterlist.Add(ebcArr.expXMLdataParent.pmrXDParent);
								}
							}
						}
					}
				}
			}
			tskmsgExper.gm.controls = strSFLName;
			tskmsgExper.task_parameterlist = pmCtntmp;
			tskmsgExper.task = TM.TM_READ;
            ctrParent.WaitControlExperSetup(true, 40, "Reading registers.", 10);
			devParent.AccessDevice(ref tskmsgExper);
			while (tskmsgExper.bgworker.IsBusy)
				System.Windows.Forms.Application.DoEvents();
			System.Windows.Forms.Application.DoEvents();
			//u32Return = tskmsgExper.errorcode;
			//if (u32Return == LibErrorCode.IDS_ERR_SUCCESSFUL)
			if (tskmsgExper.errorcode == LibErrorCode.IDS_ERR_SUCCESSFUL)
			{
				tskmsgExper.task = TM.TM_CONVERT_HEXTOPHYSICAL;
                ctrParent.WaitControlExperSetup(true, 60, "Converting registers.", 10);
                devParent.AccessDevice(ref tskmsgExper);
				while (tskmsgExper.bgworker.IsBusy)
					System.Windows.Forms.Application.DoEvents();
				//
				//
				pmCtntmp.parameterlist.Clear();
				foreach (Parameter pmInCont in pmPhytmp.parameterlist)
				{
					if (pmInCont.reglist.Count > 1)
					{
						pmCtntmp.parameterlist.Add(pmInCont);
					}
				}
				if (pmCtntmp.parameterlist.Count > 0)
				{
					tskmsgExper.task_parameterlist = pmCtntmp;
					tskmsgExper.task = TM.TM_READ;
					devParent.AccessDevice(ref tskmsgExper);
					while (tskmsgExper.bgworker.IsBusy)
						System.Windows.Forms.Application.DoEvents();
					System.Windows.Forms.Application.DoEvents();
				}
				tskmsgExper.task_parameterlist = pmPhytmp;
				tskmsgExper.task = TM.TM_CONVERT_HEXTOPHYSICAL;
                ctrParent.WaitControlExperSetup(true, 80, "Dispatching registers.", 10);
                devParent.AccessDevice(ref tskmsgExper);
				while (tskmsgExper.bgworker.IsBusy)
					System.Windows.Forms.Application.DoEvents();
				foreach (ExperModel expmeach in explisttmp)
				{
					//expmeach.u16RegVal = (UInt16)Convert.ToInt32(expmeach.pmrExpMdlParent.phydata.ToString(), 10);
					if(expmeach.yRegLength == 0x20)
						expmeach.u32RegVal = expmeach.pmrExpMdlParent.u32hexdata;
					else
						expmeach.u32RegVal = expmeach.pmrExpMdlParent.hexdata;
					expmeach.SeperateRegValueToBit();
					/*
					foreach (ExperXMLData xmld in m_XMLDataOpRegList)
					{
						if(((xmld.pmrXDParent.guid & 0x0000FF00) >> 8) == expmeach.u16RegNum)
						{
							pmCtntmp.parameterlist.Add(xmld.pmrXDParent);
						}
					}*/
					//(E141219)
				}
				//foreach (ExperModel expmeach in explisttmp)
				//{
					//expmeach.u16RegVal = (UInt16)Convert.ToInt32(expmeach.pmrExpMdlParent.phydata.ToString(), 10);
					//expmeach.u16RegVal = expmeach.pmrExpMdlParent.hexdata;
					//expmeach.SeperateRegValueToBit();
					//expmeach.ArrangeBitPhyValue();
				//}
			}
			else
			{
				devParent.bBusy = false;
                ctrParent.ExperWaitControlClear();
				return false;
			}

			devParent.bBusy = false;
            ctrParent.ExperWaitControlClear();
			return true;
		}

		/// <summary>
		/// Write 1 or all Register  from chip, it will write expmIn target register. If expmIn is null, it will write all register
		/// Default of expmIn is null
		/// </summary>
		/// <param name="tskmsgExper">in/out param,TASKMessage to communicate with DEM</param>
		/// <param name="expmIn">input param, the ExperModel structure of target register to write. Default is read all register if null </param>
		/// <returns>true: if read OK; otherwise return false</returns>
		public bool WriteRegToDevice(ref TASKMessage tskmsgExper, ExperModel expmIn = null)
		{
			//UInt32 u32Return = LibErrorCode.IDS_ERR_SUCCESSFUL;
			Parameter pmrtmp = null;
			ParamContainer pmCtntmp = new ParamContainer();
			bool bReadAnother = false;
			UInt16 uReadValueFlag = 0xFFFF;
//			AsyncObservableCollection<ExperModel> explisttmp = new AsyncObservableCollection<ExperModel>();

			if (devParent.bBusy)
			{
				tskmsgExper.errorcode = LibErrorCode.IDS_ERR_EM_THREAD_BKWORKER_BUSY;
				return false;
			}
			else
			{
                ctrParent.WaitControlExperSetup(true, 10, "Trying to communicate chip.", 10);
                if (!ctrParent.bFranTestMode)
                {
                    devParent.bBusy = true;
                    if (!GetDeviceInfor(ref tskmsgExper))		//errorcode will be assigned in GetDeviceInfor
                    {
                        devParent.bBusy = false;
                        ctrParent.ExperWaitControlClear();
                        return false;
                    }
                }
			}

            ctrParent.WaitControlExperSetup(true, 20, "gathering register.", 10);
			if (expmIn != null)
			{
				pmrtmp = expmIn.GetParentParameter();
				if (pmrtmp == null)
				{	//cannot find parameter in ExperBitComponent
					//return u32Return;
					devParent.bBusy = false;
					tskmsgExper.errorcode = LibErrorCode.IDS_ERR_EXPSFL_OPREG_NOT_FOUND; //should be Exper Error message
                    ctrParent.ExperWaitControlClear();
					return false;
				}
				//(M140411)Francis
				if (pmrtmp.reglist.Count > 1)	//has Low High, address
				{
					bReadAnother = true;
					foreach (KeyValuePair<string, Reg> kwri in pmrtmp.reglist)
					{
						if (expmIn.u32RegNum == kwri.Value.address)
						{
							if (kwri.Key.Equals("Low"))
							{
								uReadValueFlag = 0xFF00;		//if I'm Low, read value should clear lower part
								break;
							}
							else if (kwri.Key.Equals("High"))
							{
								uReadValueFlag = 0x00FF;		//if I'm High, read value should clear higher part
								break;
							}
						}
					}
				}
				pmCtntmp.parameterlist.Add(pmrtmp);
				//(M140411)Francis, read whole value before write command to Device; bugid=15057
				if (bReadAnother)
				{
					tskmsgExper.gm.controls = strSFLName;
					tskmsgExper.task_parameterlist = pmCtntmp;
					tskmsgExper.task = TM.TM_READ;
                    ctrParent.WaitControlExperSetup(true, 40, "Reading registers.", 10);
					devParent.AccessDevice(ref tskmsgExper);
					while (tskmsgExper.bgworker.IsBusy)
						System.Windows.Forms.Application.DoEvents();
					System.Windows.Forms.Application.DoEvents();
					//u32Return = tskmsgExper.errorcode;
					//if (u32Return == LibErrorCode.IDS_ERR_SUCCESSFUL)
					if (tskmsgExper.errorcode == LibErrorCode.IDS_ERR_SUCCESSFUL)
					{
						tskmsgExper.task = TM.TM_CONVERT_HEXTOPHYSICAL;
						devParent.AccessDevice(ref tskmsgExper);
						while (tskmsgExper.bgworker.IsBusy)
							System.Windows.Forms.Application.DoEvents();
					}
					else
					{
						devParent.bBusy = false;
                        ctrParent.ExperWaitControlClear();
						return false;
					}
					pmrtmp.phydata = (float)((ushort)pmrtmp.phydata & uReadValueFlag);
					if (uReadValueFlag == 0xFF00)
					{
						pmrtmp.phydata += (expmIn.u32RegVal);
					}
					else if (uReadValueFlag == 0x00FF)
					{
						pmrtmp.phydata += (expmIn.u32RegVal << 8);
					}
					else
					{
						pmrtmp.phydata += (expmIn.u32RegVal);	//should never go here
					}
				}
				//(E140411)
				else
				{
					pmrtmp.phydata = expmIn.u32RegVal;
				}
//				explisttmp.Add(expmIn);
			}
			else
			{
				//AsyncObservableCollection<ExperModel> asTmp =
				//ctrParent.dtgRegistersPresent.ItemsSource as AsyncObservableCollection<ExperModel>;
				ListCollectionView lstt = ctrParent.dtgRegistersPresent.ItemsSource as ListCollectionView;
				IList<ExperModel> list = lstt.SourceCollection as IList<ExperModel>;
				AsyncObservableCollection<ExperModel> asTmp = null;
				if (list != null)
				{
					asTmp = new AsyncObservableCollection<ExperModel>();
					asTmp.Clear();
					foreach (ExperModel item in list)
					{
						if (item.bXprRegShow)
						{
							asTmp.Add((ExperModel)item);
						}
					}
				}

				if (asTmp == null)
				{
					asTmp = ExpRegisterList;
				}
                ctrParent.WaitControlExperSetup(true, 40, "Calculating registers.", 10);
				//foreach (ExperModel expmeach in ExpRegisterList)
				foreach (ExperModel expmeach in asTmp)
				{
					pmrtmp = expmeach.GetParentParameter();
					if (pmrtmp == null)
					{	//cannot find parameter in ExperBitComponent
						//return u32Return;
						devParent.bBusy = false;
						tskmsgExper.errorcode = LibErrorCode.IDS_ERR_EXPSFL_OPREG_NOT_FOUND; //should be Exper Error message
                        ctrParent.ExperWaitControlClear();
						return false;
					}
					if (expmeach.bXprRegShow)
					{
						pmrtmp.phydata = expmeach.u32RegVal;
						pmCtntmp.parameterlist.Add(pmrtmp);
						//explisttmp.Add(expmeach);
					}
				}
			}
			tskmsgExper.gm.controls = strSFLName;
			tskmsgExper.task_parameterlist = pmCtntmp;
			tskmsgExper.task = TM.TM_CONVERT_PHYSICALTOHEX;
            ctrParent.WaitControlExperSetup(true, 60, "Converting registers.", 10);
			devParent.AccessDevice(ref tskmsgExper);
			while (tskmsgExper.bgworker.IsBusy)
				System.Windows.Forms.Application.DoEvents();
			System.Windows.Forms.Application.DoEvents();
			tskmsgExper.task = TM.TM_WRITE;
            ctrParent.WaitControlExperSetup(true, 80, "Writing registers.", 10);
			devParent.AccessDevice(ref tskmsgExper);
			while (tskmsgExper.bgworker.IsBusy)
				System.Windows.Forms.Application.DoEvents();
			System.Windows.Forms.Application.DoEvents();
			//u32Return = tskmsgExper.errorcode;
			devParent.bBusy = false;
            ctrParent.ExperWaitControlClear();
			if (tskmsgExper.errorcode == LibErrorCode.IDS_ERR_SUCCESSFUL)
			{
				return true;
			}
			else
			{
				return false;
			}
		}

		/// <summary>
		/// DEM special function, to get device information
		/// </summary>
		/// <param name="tskmsgExper">TASKMessage to communicate with DEM</param>
		/// <returns>true: if communication OK; otherwise return false</returns>
		private bool GetDeviceInfor(ref TASKMessage tskmsgExper)
		{
			tskmsgExper.task = TM.TM_SPEICAL_GETREGISTEINFOR;
			devParent.AccessDevice(ref tskmsgExper);
			while (tskmsgExper.bgworker.IsBusy)
				System.Windows.Forms.Application.DoEvents();
			System.Windows.Forms.Application.DoEvents();
			if (tskmsgExper.errorcode == LibErrorCode.IDS_ERR_SUCCESSFUL)
			{
				//return true;
			}
			else
			{
				return false;
			}
			tskmsgExper.task = TM.TM_SPEICAL_GETDEVICEINFOR;
			devParent.AccessDevice(ref tskmsgExper);
			while (tskmsgExper.bgworker.IsBusy)
				System.Windows.Forms.Application.DoEvents();
			System.Windows.Forms.Application.DoEvents();
			if (tskmsgExper.errorcode == LibErrorCode.IDS_ERR_SUCCESSFUL)
			{
				return true;
			}
			else
			{
				return false;
			}
		}

		/// <summary>
		/// Parse TestMode button parameter in XML, and save in ExpTestBtnList
		/// Note that due to co-working with DEM parsing ElementDefine setting, the GUID will be overwrite by
		/// OperationElement, instead of original mask TestButtonElement
		/// </summary>
		/// <param name="paramIn"></param>
		/// <returns>true if parsing successfully; false if any error occurs</returns>
		private bool ParseTestModeParamToData(Parameter paramIn)
		{
			byte ydata = 0;
            UInt16 wdata = 0;
			bool bdata = false;
			//UInt32 u32data = 0;
			ExperTestButtonModel mdltest = new ExperTestButtonModel();
            mdltest.wSubTask = 00;      //(A161024)Francis, assign a default value in case

			foreach (DictionaryEntry de in paramIn.sfllist[strSFLName].nodetable)
			{
				switch (de.Key.ToString())
				{
					case "TestMode":
						{
							mdltest.strTestTrim = de.Value.ToString();
							break;
						}
					case "SubValue":
						{
							if (!Byte.TryParse(de.Value.ToString(), NumberStyles.HexNumber, null as IFormatProvider, out ydata))
							{
								mdltest.ySubValue = 0x00;
							}
							else
							{
								mdltest.ySubValue = Convert.ToByte(de.Value.ToString(), 16);
							}
							break;
						}
                    case "SubTask":
                        {
                            if(!UInt16.TryParse(de.Value.ToString(), NumberStyles.HexNumber, null as IFormatProvider, out wdata))
                            {
                                mdltest.wSubTask = 00;
                            }
                            else
                            {
                                mdltest.wSubTask = Convert.ToUInt16(de.Value.ToString(), 16);
                            }
                            break;
                        }
					case "CustomerMode":
						{
							if (!Boolean.TryParse(de.Value.ToString(), out bdata))
							{
								mdltest.bTestBtnVisible = false;
							}
							else
							{
								mdltest.bTestBtnVisible = (bdata | ctrParent.bEngModeRunning);	//false if both ar false, otherwise are all true
							}
							//bEngModeFromXML &= bdata;	//if there is any false in XML, means X version of XML is used, set as false
							bTrimModeFromXML |= bdata;	//if there is any trimming mode support in XML, set as true
							break;
						}
					case "Enable":
						{
							if (!Boolean.TryParse(de.Value.ToString(), out bdata))
							{
								mdltest.bTestBtnEnable = true;		//default is enable
								mdltest.bHost = false;
							}
							else
							{
								mdltest.bTestBtnEnable = bdata;
								mdltest.bHost = true;
							}
							break;
						}
					case "Host":
						{
							if (!Boolean.TryParse(de.Value.ToString(), out bdata))
							{
								mdltest.bHost = false;		//default is false
							}
							else
							{
								mdltest.bHost = bdata;
							}
							break;
						}
					case "ReadBack":
						{
							if (!Boolean.TryParse(de.Value.ToString(), out bdata))
							{
								mdltest.bReadBack = true;		//default is enable
							}
							else
							{
								mdltest.bReadBack = bdata;
							}
							break;
						}
					case "Group":
						{
							mdltest.u32Group =	Convert.ToUInt32(de.Value.ToString(), 16);
							break;
						}
					case "Order":
						{
							if (!Byte.TryParse(de.Value.ToString(), NumberStyles.HexNumber, null as IFormatProvider, out ydata))
							{
								mdltest.yOrder = 0xFF;
							}
							else
							{
								mdltest.yOrder = Convert.ToByte(de.Value.ToString(), 16);
							}
							break;
						}
					case "RegReadFrom":
						{
							if (!Boolean.TryParse(de.Value.ToString(), out bdata))
							{
								mdltest.bRegReadFrom = false;		//default is enable
							}
							else
							{
								mdltest.bRegReadFrom = bdata;
							}
							break;
						}
					case "RegWriteTo":
						{
							if (!Boolean.TryParse(de.Value.ToString(), out bdata))
							{
								mdltest.bRegWriteTo = false;		//default is enable
							}
							else
							{
								mdltest.bRegWriteTo = bdata;
							}
							break;
						}
				}	//switch
			}
			mdltest.pmrXDParent = paramIn;
			mdltest.u32Guid = paramIn.guid;
			ExpTestBtnList.Add(mdltest);

			//make a copy of Parameter, including phyref,regref, subtype, and reglist; but adjust guid value to Operation
			mdltest.pmrTestMdlParent.guid = (paramIn.guid & ~ExperViewMode.SectionElementFlag) 
				| ExperViewMode.OperationElement;	//assign as OperationElement;
			mdltest.pmrTestMdlParent.phyref = 1;
			mdltest.pmrTestMdlParent.regref = 1;
			mdltest.pmrTestMdlParent.subtype = 0;		//COBRA_PARAM_SUBTYPE
			mdltest.pmrTestMdlParent.subsection = paramIn.subsection;		//(A140409)Francis
			mdltest.pmrTestMdlParent.reglist.Clear();
			foreach (KeyValuePair<string, Reg> tmpreg in paramIn.reglist)
			{
				Reg newReg = new Reg();
				newReg.address = tmpreg.Value.address;
				newReg.bitsnumber = tmpreg.Value.bitsnumber;
				newReg.startbit = tmpreg.Value.startbit;
				mdltest.pmrTestMdlParent.reglist.Add(tmpreg.Key, newReg);
			}

			return true;
		}

		/// <summary>
		/// Search ExperModel in ExpTestRegList list
		/// </summary>
		/// <param name="yIndexIn">register value</param>
		/// <returns>true if foundy; false if find nothing</returns>
		public ExperModel SearchTestModelByIndex(UInt16 yIndexIn)
		{
			ExperModel expmdltmp = null;

			foreach (ExperModel expm in ExpTestRegList)
			{
				if (expm.u32RegNum == yIndexIn)
				{
					expmdltmp = expm;
					break;
				}
			}

			return expmdltmp;
		}

		/// <summary>
		/// Switch bTestBtnEnable value that is bindded on UI to show/hide
		/// </summary>
		/// <param name="bNormal">input, if true, disable normal mode button and enable other mode button
		/// if false, enable normal mode button and disable other mode button</param>
		public void EnableTestButton(bool bNormal = false, ExperTestButtonModel XtstPress = null)
		{
			foreach (ExperTestButtonModel Xtstbtn in ExpTestBtnList)
			{
				if (Xtstbtn.bHost)
				{
					if (XtstPress != null)
					{
						if (XtstPress.u32Group == Xtstbtn.u32Group)
						{
							Xtstbtn.bTestBtnEnable = !bNormal;
						}
						else
						{
							Xtstbtn.bTestBtnEnable = false;
						}
					}
					else
					{
						Xtstbtn.bTestBtnEnable = !bNormal;
					}
				}
				else
					Xtstbtn.bTestBtnEnable = bNormal;
				//Xtstbtn.bTestBtnEnable = !Xtstbtn.bTestBtnEnable;
			}
		}

		/// <summary>
		/// Check witch TestButton is pressed and set up its corresponding register show or hide
		/// </summary>
		/// <param name="Xptsttmp">Input, which TestButton is pressed</param>
		public void DisplayTestRegister(ExperTestButtonModel Xptsttmp)
		{
			foreach (ExperModel expm in ExpTestRegList)
			{
				if (Xptsttmp.strTestTrim.Equals(expm.strTestXpr))
				{
					expm.bXprRegShow = true;
				}
				else
				{
					expm.bXprRegShow = false;
				}
				expm.strGroupReg = Xptsttmp.strTestTrim;
			}
			lstclTestRegList.GroupDescriptions.Clear();
			lstclTestRegList.GroupDescriptions.Add(new PropertyGroupDescription("strGroupReg"));
		}

		/// <summary>
		/// According input 2nd XpTestBtnIn, try to Write value to Device, to switch mode;
		/// Then read back that the value in chip is equals to value in XpTestBtnIn
		/// </summary>
		/// <param name="tskmsgExper">in/out param,TASKMessage to communicate with DEM</param>
		/// <param name="XpTestBtnIn">input, which TestButton is pressed</param>
		/// <returns></returns>
		public bool CommandTestToDevice(ref TASKMessage tskmsgExper, ExperTestButtonModel XpTestBtnIn)
		{
			Parameter pmrtmp = null;
			ParamContainer pmCtntmp = new ParamContainer();
			bool bReturn = true;

			if (XpTestBtnIn == null)
			{
				return false;
			}

			if (devParent.bBusy)
			{
				tskmsgExper.errorcode = LibErrorCode.IDS_ERR_EM_THREAD_BKWORKER_BUSY;
				return false;
			}
			else
			{
				devParent.bBusy = true;
				if (!GetDeviceInfor(ref tskmsgExper))		//errorcode will be assigned in GetDeviceInfor
				{
					devParent.bBusy = false;
					return false;
				}
			}

			pmrtmp = XpTestBtnIn.pmrTestMdlParent;
			if (pmrtmp == null)
			{	//cannot find parameter in ExperBitComponent
				//return u32Return;
				devParent.bBusy = false;
				tskmsgExper.errorcode = LibErrorCode.IDS_ERR_EXPSFL_OPREG_NOT_FOUND; //should be Exper Error message
				return false;
			}

			/*
			foreach (KeyValuePair<string, Reg> tmpreg in pmrtmp.reglist)
			{
				uGuidTmp += (UInt32)tmpreg.Value.address << 8;
				uGuidTmp += (UInt32)tmpreg.Value.startbit;
			}
			foreach (ExperModel Xptemp in ExpRegisterList)
			{
				if ((Xptemp.gui & 0x0000FFFF) == uGuidTmp)
				{
				}
			}
			 */
			//(A150105)Francis, if bRegWriteTo, write all TestMode register to DM and chip
			if (XpTestBtnIn.bRegWriteTo)
			{
				if (!WriteRegToDevice(ref tskmsgExper))
				{
					//tskmsgExper.errorcode = LibErrorCode.IDS_ERR_EXPSFL_OPREG_NOT_FOUND; //should be Exper Error message
					//TBD: should no need to assign error code
					return false;
				}
			}
			//(E150105)

			pmrtmp.phydata = XpTestBtnIn.ySubValue;
			pmCtntmp.parameterlist.Add(pmrtmp);
			//explisttmp.Add(XpTestBtnIn);
			tskmsgExper.gm.controls = strSFLName;
			tskmsgExper.task_parameterlist = pmCtntmp;
			tskmsgExper.task = TM.TM_CONVERT_PHYSICALTOHEX;
			devParent.AccessDevice(ref tskmsgExper);
			while (tskmsgExper.bgworker.IsBusy)
				System.Windows.Forms.Application.DoEvents();
			System.Windows.Forms.Application.DoEvents();
			tskmsgExper.task = TM.TM_COMMAND;
            tskmsgExper.sub_task = XpTestBtnIn.wSubTask;        //(A161024)Francis, TM_COMMAND combining with Sub_Taks
			devParent.AccessDevice(ref tskmsgExper);
			while (tskmsgExper.bgworker.IsBusy)
				System.Windows.Forms.Application.DoEvents();
			System.Windows.Forms.Application.DoEvents();
			//u32Return = tskmsgExper.errorcode;
			devParent.bBusy = false;
			if (tskmsgExper.errorcode == LibErrorCode.IDS_ERR_SUCCESSFUL)
			{
				//(A150105)Francis, if bRegReadFrom, read all TestMode register from chip and DM
				if (XpTestBtnIn.bRegReadFrom)
				{
					if (!ReadRegFromDevice(ref tskmsgExper))
					{
						//tskmsgExper.errorcode = LibErrorCode.IDS_ERR_EXPSFL_OPREG_NOT_FOUND; //should be Exper Error message
						//TBD: should no need to assign error code
						bReturn = false;
					}
				}
				//else
				//{
					//return false;
				//}
				//(E150105)
				if (XpTestBtnIn.bReadBack)	//if test mode needes to read back to confirm, read back
				{
					tskmsgExper.task = TM.TM_READ;
					devParent.AccessDevice(ref tskmsgExper);
					while (tskmsgExper.bgworker.IsBusy)
						System.Windows.Forms.Application.DoEvents();
					System.Windows.Forms.Application.DoEvents();
					tskmsgExper.task = TM.TM_CONVERT_HEXTOPHYSICAL;
					devParent.AccessDevice(ref tskmsgExper);
					while (tskmsgExper.bgworker.IsBusy)
						System.Windows.Forms.Application.DoEvents();
					System.Windows.Forms.Application.DoEvents();
					if (tskmsgExper.errorcode == LibErrorCode.IDS_ERR_SUCCESSFUL)
					{
						if (pmrtmp.phydata == XpTestBtnIn.ySubValue)
							bReturn = true;
					}
					else
					{
						bReturn = false;
					}
				}
				else
				{
					bReturn = true;
				}
			}
			//if (tskmsgExper.errorcode == LibErrorCode.IDS_ERR_SUCCESSFUL)
			else
			{
				bReturn =  false;
			}

			return bReturn;
		}

		/// <summary>
		/// Check what mode (trimming / normal) of chip, and sync with UI. Note that it will parse ExpTestBtnList from 
		/// beginning and try to read its register/value; and if found first matched ExperTestButtonModel, then will return
		/// </summary>
		/// <param name="tskmsgExper">in/ouput TASKMessage, to communicate with Device</param>
		/// <param name="TargetCollect">output, the target ExperModel List; ExpTestRegList or ExpRegisterList </param>
		/// <returns>true, if buttons are able to enable; otherwise return false</returns>
		public bool SyncModeWithDev(ref TASKMessage tskmsgExper, ref AsyncObservableCollection<ExperModel> TargetCollect)
		{
			Double dbValue;
			bool bRet = true;
			Parameter pmrtmp = null;
			ParamContainer pmCtntmp = new ParamContainer();

			TargetCollect = null;	//default is ListCollectionView containing ExpRegisterList
			if (devParent.bBusy)
			{
				tskmsgExper.errorcode = LibErrorCode.IDS_ERR_SUCCESSFUL;
				//TargetCollect = ExpRegisterList;
				return true; ;	//if other SFL is communicating, just keep normal mode display
			}
			else
			{
				devParent.bBusy = true;
				if (!GetDeviceInfor(ref tskmsgExper))		//errorcode will be assigned in GetDeviceInfor
				{
					//TargetCollect = ExpRegisterList;
					devParent.bBusy = false;
					return false;	//return false to disable buttons operation, and display normal mode register
				}
			}

			//TargetCollect = ExpRegisterList;
			foreach (ExperTestButtonModel XpTestBtnIn in ExpTestBtnList)
			{
				pmrtmp = XpTestBtnIn.pmrTestMdlParent;
				if (pmrtmp == null)
				{
					//TargetCollect = ExpRegisterList;	//maybe there is no TestMode definition, return normal mode register
					bRet = true;
					break;
				}
				if (!XpTestBtnIn.bReadBack)
					continue;
				//dbValue = pmrtmp.phydata;
				dbValue = XpTestBtnIn.ySubValue;
				pmCtntmp.parameterlist.Add(pmrtmp);
				tskmsgExper.gm.controls = strSFLName;
				tskmsgExper.task_parameterlist = pmCtntmp;
				tskmsgExper.task = TM.TM_READ;
				devParent.AccessDevice(ref tskmsgExper);
				while (tskmsgExper.bgworker.IsBusy)
					System.Windows.Forms.Application.DoEvents();
				System.Windows.Forms.Application.DoEvents();
				tskmsgExper.task = TM.TM_CONVERT_HEXTOPHYSICAL;
				devParent.AccessDevice(ref tskmsgExper);
				while (tskmsgExper.bgworker.IsBusy)
					System.Windows.Forms.Application.DoEvents();
				System.Windows.Forms.Application.DoEvents();
				if (tskmsgExper.errorcode == LibErrorCode.IDS_ERR_SUCCESSFUL)
				{
					if (pmrtmp.phydata == dbValue)
					{
						if (XpTestBtnIn.bHost)
						{
							//TargetCollect = ExpRegisterList;
							EnableTestButton(XpTestBtnIn.bHost);
						}
						else
						{
							DisplayTestRegister(XpTestBtnIn);
							TargetCollect = ExpTestRegList;
							EnableTestButton(XpTestBtnIn.bHost, XpTestBtnIn);
						}
						bRet = true;
						break;
					}
				}
				else
				{	//cannot communicate, display normal mode register and disable buttons
					//TargetCollect = ExpRegisterList;
					bRet = false;	//return false to disable buttons operation, and display normal mode register
					break;
				}
			}

			devParent.bBusy = false;
			return bRet;
		}

		/// <summary>
		/// Backup all ExperModel in ExpRegisterList, to ExpProList. This is formal List<ExperMode> data.
		/// Due to ExpRegisterList will be changed if user clicked Pro button. ExpProList is used to restore original data to ExpRegisterList
		/// </summary>
		private void BackExpertUI()
		{
			ExperModel mdlPro;

			foreach (ExperModel XExpUI in ExpRegisterList)
			{
				mdlPro = new ExperModel();

				mdlPro.u32RegNum = XExpUI.u32RegNum;
				mdlPro.bRead = XExpUI.bRead;
				mdlPro.bWrite = XExpUI.bWrite;
				for(int i = 0; i < XExpUI.ArrRegComponet.Length; i++)
				{
					mdlPro.ArrRegComponet[i].bWrite = XExpUI.ArrRegComponet[i].bWrite;
					mdlPro.ArrRegComponet[i].bRead = XExpUI.ArrRegComponet[i].bRead;
				}
				ExpProList.Add(mdlPro);
			}
		}

		/// <summary>
		/// Turn on (enable) all UI control ExperModel in ExpRegisterList, when user clicks 'Pro' button
		/// </summary>
		public void OpenExpertUI()
		{
			foreach (ExperModel XExpUI in ExpRegisterList)
			{
				for (int i = 0; i < XExpUI.ArrRegComponet.Length; i++)
				{
					XExpUI.ArrRegComponet[i].bWrite = true;
					XExpUI.ArrRegComponet[i].bRead = true;
				}
				XExpUI.bRead = true;
				XExpUI.bWrite = true;
				XExpUI.bEnable = true;
			}
		}

		/// <summary>
		/// Restore ExpProList to ExpRegisterList, ExpProList is saved origianl List<ExperModel> it is formal data
		/// </summary>
		public void RestoreExpertUI()
		{
			foreach (ExperModel XExpUI in ExpRegisterList)
			{
				foreach (ExperModel XProUI in ExpProList)
				{
					if (XExpUI.u32RegNum == XProUI.u32RegNum)
					{
						XExpUI.bRead = XProUI.bRead;
						XExpUI.bWrite = XProUI.bWrite;
						XExpUI.bEnable = false;
						for (int i = 0; i < XExpUI.ArrRegComponet.Length; i++)
						{
							XExpUI.ArrRegComponet[i].bWrite = XProUI.ArrRegComponet[i].bWrite;
							XExpUI.ArrRegComponet[i].bRead = XProUI.ArrRegComponet[i].bRead;
							if (!XExpUI.ArrRegComponet[i].bWrite)// |(XExpUI.ArrRegComponet[i].pmrBitParent == null))
							{
								XExpUI.ArrRegComponet[i].yBitValue = 0; 
								XExpUI.ArrRegComponet[i].dbPhyValue = 0;
								if (XExpUI.ArrRegComponet[i].pmrBitParent != null)
								{
									XExpUI.ArrRegComponet[i].pmrBitParent.hexdata = 0;
									XExpUI.ArrRegComponet[i].pmrBitParent.phydata = 0;
								}
							}
						}
						XExpUI.SumRegisterValue();
						break;
					}
				}
			}
		}

        /// <summary>
        /// Save value that user operates on UI in ExperModel
        /// </summary>
        /// <param name="tskmsgExper">in/ouput TASKMessage, to communicate with Device</param>
        /// <param name="expmIn">input, ExperModel that is binding to UI which user is clicking on</param>
        /// <returns></returns>
		public bool AdjustPhyValueByUser(ref TASKMessage tskmsgExper, ExperModel expmIn)
		{
			bool bReturn = false;
			Parameter pmrtmp = null;
			ParamContainer pmCtntmp = new ParamContainer();
			AsyncObservableCollection<ExperModel> explisttmp = new AsyncObservableCollection<ExperModel>();
			ParamContainer pmPhytmp = new ParamContainer();
            UInt16 uMask = 0, uTempM = 1;

			//devParent.bBusy = true;

			if (expmIn != null)
			{
				pmrtmp = expmIn.GetParentParameter();
				if (pmrtmp == null)
				{	//cannot find parameter in ExperBitComponent
					//return u32Return;
					//devParent.bBusy = false;
					tskmsgExper.errorcode = LibErrorCode.IDS_ERR_EXPSFL_OPREG_NOT_FOUND;
					return bReturn;
				}
                /*foreach (ExperBitComponent ebcArr in expmIn.ArrRegComponet)
                {
                    if ((ebcArr.bWrite) && (ebcArr.expXMLdataParent != null))
                    {
                        uTempM = 0;
                        for (Byte jk = 0; jk<ebcArr.expXMLdataParent.yLength; jk++)
                        {
                            uTempM |= (UInt16)(1 << (jk+ebcArr.expXMLdataParent.yBitStart));
                        }
                        uMask |= uTempM;
                    }
                }
                expmIn.u16RegVal &= uMask;
                expmIn.SeperateRegValueToBit();*/
				pmrtmp.phydata = (double)expmIn.u32RegVal;	//assign phydata as Summerized value
				pmCtntmp.parameterlist.Add(pmrtmp);
				explisttmp.Add(expmIn);
//				foreach (ExperXMLData xmda in m_XMLDataOpRegList)
//				{
//					if (((xmda.pmrXDParent.guid & 0x0000FF00) >> 8) == expmIn.u16RegNum)
//					{
//						pmPhytmp.parameterlist.Add(xmda.pmrXDParent);
//						break;
//					}
//				}
				foreach (ExperBitComponent ebcArr in expmIn.ArrRegComponet)
				{
					if (ebcArr.yDescripVisiLgth != 0)
					{
						if (ebcArr.expXMLdataParent != null)
						{
							//break;		//perhaps ebcArr is bitx8~bitxF
							pmPhytmp.parameterlist.Add(ebcArr.expXMLdataParent.pmrXDParent);
						}
					}
				}
			}
			else
			{
				//TBD
				tskmsgExper.errorcode = LibErrorCode.IDS_ERR_EXPSFL_XML;
				return bReturn;
			}
			//write to DM
			tskmsgExper.task_parameterlist = pmCtntmp;
			tskmsgExper.task = TM.TM_CONVERT_PHYSICALTOHEX;
			devParent.AccessDevice(ref tskmsgExper);
			while (tskmsgExper.bgworker.IsBusy)
				System.Windows.Forms.Application.DoEvents();
			//get conversion from DM
			tskmsgExper.task_parameterlist = pmPhytmp;
			tskmsgExper.task = TM.TM_CONVERT_HEXTOPHYSICAL;
			devParent.AccessDevice(ref tskmsgExper);
			while (tskmsgExper.bgworker.IsBusy)
				System.Windows.Forms.Application.DoEvents();

			devParent.bBusy = false;
			expmIn.ArrangeBitPhyValue(ExpRegisterList);
			return bReturn;
		}

        /// <summary>
        /// Copy public address/startbit/length to ExperXMLData object
        /// </summary>
        /// <param name="xmlDatatg">in/output ExperXMLData object; read from xml</param>
        /// <param name="yValid">output, change to 3 after setup</param>
        public void ParsePublicInfor(ref ExperXMLData xmlDatatg, ref byte yValid)
        {
            //bool bReturn = false;
            foreach (KeyValuePair<string, Reg> inreg in xmlDatatg.pmrXDParent.reglist)
            {
                if (inreg.Key.ToLower().Equals("low"))
                {
                    //debug usage
                    //if ((xmlDatatg.yIndex != SharedFormula.LoByte(inreg.Value.address)) ||
                        //(xmlDatatg.yBitStart != SharedFormula.LoByte(inreg.Value.startbit)) ||
                        //(xmlDatatg.yLength != SharedFormula.LoByte(inreg.Value.bitsnumber)))
                    //{
                        //MessageBox.Show(string.Format("index=0x{0:2X}, bit=0x{1:2X}, length=0x{2:2X}", xmlDatatg.yIndex, xmlDatatg.yBitStart, xmlDatatg.yLength));
                    //}
					//(M191220)Francis, modify for 16bit index display
                    //xmlDatatg.yIndex = SharedFormula.LoByte(inreg.Value.address);
					xmlDatatg.u32Index = (UInt32)SharedFormula.LoByte(inreg.Value.address);
                    xmlDatatg.yBitStart = SharedFormula.LoByte(inreg.Value.startbit);
                    xmlDatatg.yLength = SharedFormula.LoByte(inreg.Value.bitsnumber);
                    yValid = 3;
                }
                else if(inreg.Key.ToLower().Equals("high"))
                {
                    xmlDatatg.yLength += SharedFormula.LoByte(inreg.Value.bitsnumber);
                }
            }

            xmlDatatg.yTotal = ctrParent.yBitTotal;

            //return bReturn;
        }
	}

	public class MaterialDescriptionGroupConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			string repository = value as string;

			return repository;

		}

		public object ConvertBack(object value, Type targetType,
			   object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();

		}
	}

}
