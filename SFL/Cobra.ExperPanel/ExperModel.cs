using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Globalization;
using Cobra.Common;

namespace Cobra.ExperPanel
{
	//Data structure for XML parsing <Element>/<Private> node, for Operation Register definition
	public class ExperXMLData
	{
		public bool bCustMode { get; set; }		//if true, able to show in Customer mode
		public byte yTotal { get; set; }
		
		//(M191220)Francis, modify for 16bit index display
		//public byte yIndex { get; set; }
		public UInt32 u32Index { get; set; }
		public byte yBitStart { get; set; }
		public byte yLength { get; set; }
		public byte yValue { get; set; }
		public bool bRead { get; set; }
		public bool bWrite { get; set; }
		public string strDescrip { get; set; }
        public string strBitTips { get; set; }
		public string strTestMode { get; set; }
		public Parameter pmrXDParent { get; set; }
		public UInt32 u32Guid { get; set; }
		public string strGroup { get; set; }
		public string strRegName { get; set; }		//(A141218)Francis, for enhancement
		public string strUnit { get; set; }				//(A141218)Francis, for enhancement
		public bool bPhyDataFromList { get; set; }      //(ISSUE:2203)Guo, for enhancement
		public ExperXMLData()
		{
			bCustMode = false;
			yTotal = 8;
			//(M191220)Francis, modify for 16bit index display
			//yIndex = 0xFF;
			u32Index = 0xFFFFFFFF;
			yBitStart = 0xFF;
			yLength = 0xFF;
			yValue = 0xFF;
			bRead = false;
			bWrite = false;
			strDescrip = string.Empty;
            strBitTips = string.Empty;
			pmrXDParent = null;
			u32Guid = 0xFFFFFFFF;
			strGroup = string.Empty;
			strUnit = string.Empty;
			strRegName = string.Empty;
			bPhyDataFromList = false;
		}
	}

	//Data structure of each bit
	public class ExperBitComponent : INotifyPropertyChanged
	{
		static public string BitDescrpDefault = "--";
		static public string RegDescrpDefault = "-- --";

		public event PropertyChangedEventHandler PropertyChanged;
		public void OnPropertyChanged(string propName)
		{
			if (PropertyChanged != null)
				PropertyChanged(this, new PropertyChangedEventArgs(propName));
		}

		public Parameter pmrBitParent { get; set; }		//save sudo Parameter, it's fake and hexref/phyref are all 1
		public ExperXMLData expXMLdataParent { get; set; }		//save ExperXMLData node
		public UInt32 u32Guid { get; set; }	//save GUID
		public string strBit { get; set; }	//save "bit_0" string, simly dispaly on UI

		//save readable,
		private bool m_bRead;
		public bool bRead 
		{
			get { return m_bRead; }
			set { m_bRead = value; OnPropertyChanged("bRead"); }
		}

		//save writable,
		private bool m_bWrite;
		public bool bWrite 
		{
			get { return m_bWrite; }
			set { m_bWrite = value; OnPropertyChanged("bWrite"); }
		}

		//save bit value 0 or 1, binding to UI display value and combine into Register Value
		private byte m_yBitValue;
		public byte yBitValue 
		{
			get { return m_yBitValue; }
			set { m_yBitValue = value; OnPropertyChanged("yBitValue");  }
		}

		//save bit is operatable or not, binding to UI, bit_description and bit_operation, bBitEnable = (bRead & bWrite);
		private bool m_bBitEnable;
		public bool bBitEnable
		{
			get { return m_bBitEnable; }
			set { m_bBitEnable = value; OnPropertyChanged("bBitEnable"); }
		}

		//save the length of bit, 
		private byte m_yDescripVisLgth;
		public byte yDescripVisiLgth
		{
			get { return m_yDescripVisLgth; }
			set { m_yDescripVisLgth = value; OnPropertyChanged("yDescripVisiLgth"); }
		}

		//save Bit description
		private string m_strBitDescrip;
		public string strBitDescrip
		{
			get { return m_strBitDescrip; }
			set { m_strBitDescrip = value; OnPropertyChanged("strBitDescrip"); }
		}

        //save Bit description
        private string m_strBitTips;
        public string strBitTips
		{
            get { return m_strBitTips; }
            set { m_strBitTips = value; OnPropertyChanged("strBitTips"); }
		}

		//save Group string
		public string strGroupBit { get; set; }

		//(A141218)Francis, for enhancement
		//save Unit string from xml
		public string strUnit { get; set; }

		private double m_dbPhyValue;
		public double dbPhyValue 
		{
			get { return m_dbPhyValue; }
			set 
			{ 
				m_dbPhyValue = value;
                //(M180821)Francis, issue_id=865, sync solution that don't convert physical value from DEM, jsut convert DEC to HEX simply
                if (bShowPhysical)
                {
					if ((pmrBitParent != null) && (pmrBitParent.itemlist.Count != 0) && (pmrBitParent.itemlist.Count > m_dbPhyValue)&&(m_dbPhyValue >= 0)
						&&(expXMLdataParent !=null)&&expXMLdataParent.bPhyDataFromList)
					{
						strPhysicalValue = pmrBitParent.itemlist[(int)m_dbPhyValue].Trim();
					}
					else
					{
						if (strUnit.Length == 0)
						{
							strPhysicalValue = string.Format("{0} {1}", m_dbPhyValue, strUnit);
						}
						else
						{
							strPhysicalValue = string.Format("{0:F1} {1}", m_dbPhyValue, strUnit);
						}
					}
                }
                else
                {
					if (pmrBitParent != null)
						strPhysicalValue = string.Format("{0:X}", pmrBitParent.hexdata);
                }
            }
		}

		private string m_strPhysicalValue;
		public string strPhysicalValue
		{
			get { return m_strPhysicalValue; }
			set { m_strPhysicalValue = value; OnPropertyChanged("strPhysicalValue"); }
		}
		//(E141218)

        //(M180821)Francis, issue_id=865, sync solution that don't convert physical value from DEM, jsut convert DEC to HEX simply
        public bool bShowPhysical { get; set; }
       
        //Construction
		public ExperBitComponent()
		{
			ResetBitContent();
		}

		// <summary>
		// Reset bit content as initialization value
		// </summary>
		public void ResetBitContent()
		{
			yDescripVisiLgth = 1;
			strBitDescrip = string.Format(BitDescrpDefault);
			strPhysicalValue = string.Format(BitDescrpDefault);
			pmrBitParent = null;
			u32Guid = 0;
			strUnit = string.Empty;
			strPhysicalValue = string.Empty;
		}
	}

	//Data structure of each register, mapping to XML, we have new public XML node to describe each register
	//Original public XML node are built by THRESHOLD catalog, each threshold has public node
	//We will have new public XML node, and are built by REGISTER catalog
	public class ExperModel : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;
		public void OnPropertyChanged(string propName)
		{
			if (PropertyChanged != null)
				PropertyChanged(this, new PropertyChangedEventArgs(propName));
		}

		private Parameter m_pmrExpMdlParent = new Parameter();
		public Parameter pmrExpMdlParent 
		{
			get { return m_pmrExpMdlParent; }
			set { m_pmrExpMdlParent = value; }
		}
//		public UInt32 u32Guid { get; set; }

		//string of register name, binding to UI, TBD: need to set incode or from xml
		private string m_strRegName;
		public string strRegName
		{
			get { return m_strRegName; }
			set { m_strRegName = value; OnPropertyChanged("strRegName"); }
		}		//save string of Register name

		private UInt32 m_u32RegVal;
		public UInt32 u32RegVal
		{
			get { return m_u32RegVal; }
			set
			{
				if (m_u32RegVal != value)
				{
					bValueChange = true;
				}
				else
				{
					bValueChange = false;
				}
				m_u32RegVal = value;
				//(M131226)if length = 16 bit length, force it display 4 digit value
				if (yRegLength == 0x10)
				{
					//m_u16RegVal &= 0xFFFF;
					strRegVal = string.Format("0x{0:X4}", m_u32RegVal);
				}
				else if(yRegLength == 0x20)
				{
					//m_u16RegVal &= 0xFFFF;
					strRegVal = string.Format("0x{0:X8}", m_u32RegVal);
				}
				else //otherwise, default is 2 digit value
				{
					//m_u16RegVal &= 0xFFFF;
					foreach (KeyValuePair<string, Reg> inreg in m_pmrExpMdlParent.reglist)
					{
						if (inreg.Value.address == u32RegNum)
						{
							if (inreg.Key.Equals("Low"))
							{
								m_u32RegVal &= 0x00FF;
								break;
							}
							else if (inreg.Key.Equals("High"))
							{
								m_u32RegVal &= 0xFF00;
								m_u32RegVal >>= 8;
								break;
							}
						}
					}
					strRegVal = string.Format("0x{0:X2}", m_u32RegVal);
				}
				ArrangeBitPhyValue();
			}
		}		//save value of register,

		//string of register value, binding to UI
		private string m_strRegVal;
		public string strRegVal
		{
			get { return m_strRegVal; }
			set 
			{ 
				string strtmp;
				int	itmp;
				UInt32 utmp;
				m_strRegVal = value; 
				OnPropertyChanged("strRegVal"); 
				itmp = m_strRegVal.IndexOf("0x");
				if (itmp != -1)
				{
					strtmp = m_strRegVal.Substring(itmp+2);
				}
				else
				{
					strtmp = m_strRegVal;
				}
				if (!UInt32.TryParse(strtmp, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out utmp))
				{
					utmp = 0;
				}
				if (m_u32RegVal != utmp)
				{
					m_u32RegVal = utmp;
					SeperateRegValueToBit();
					ArrangeBitPhyValue();
					bValueChange = true;
				}
			}
		}

		private bool m_bValueChange;
		public bool bValueChange
		{
			get { return m_bValueChange; }
			set { m_bValueChange = value; OnPropertyChanged("bValueChange"); }
		}

		//save readable, binding to UI
		private bool m_bRead;
		public bool bRead 
		{
			get { return m_bRead; }
			set { m_bRead = value; OnPropertyChanged("bRead"); }
		}

		//save bEnable, binding to UI to control TextBox editable
		private bool m_bEnable = false;
		public bool bEnable
		{
			get { return m_bEnable; }
			set { m_bEnable = value; OnPropertyChanged("bEnable"); }
		}

		//save writable, binding to UI
		private bool m_bWrite;
		public bool bWrite
		{
			get { return m_bWrite; }
			set { m_bWrite = value; OnPropertyChanged("bWrite"); }
		}

		//Binding to UI, to control ListBoxItem, each register, show or hide
		private bool m_bXprRegShow;
		public bool bXprRegShow
		{
			get { return m_bXprRegShow; }
			set { m_bXprRegShow = value; OnPropertyChanged("bXprRegShow"); }
		}

		public UInt32 u32RegNum { get; set; }	//save index value, as Tag parameter in button_click action
		public string strRegNum { get; set; }		//save index string value, simply display on UI
		public byte yRegLength { get; set; }		//8 or 16 bit length
		public string strTestXpr { get; set; }		// save TestMode string in ExperModel
		public string strGroupReg { get; set; }	//Save Group string for each register belongs to

		private bool m_bMarkReg;
		public bool bMarkReg
		{
			get { return m_bMarkReg; }
			set { m_bMarkReg = value; OnPropertyChanged("bMarkReg"); }
		}

		#region Public Property declaration of each bit component,ebcBit0~F, and ArrRegComponet[16] array 
		public ExperBitComponent[] ArrRegComponet = new ExperBitComponent[32];
		private ExperBitComponent m_ebcBit0 = new ExperBitComponent();
		public ExperBitComponent ebcBit0 { get { return m_ebcBit0; } set { m_ebcBit0 = value; } }
		private ExperBitComponent m_ebcBit1 = new ExperBitComponent();
		public ExperBitComponent ebcBit1 { get { return m_ebcBit1; } }
		private ExperBitComponent m_ebcBit2 = new ExperBitComponent();
		public ExperBitComponent ebcBit2 { get { return m_ebcBit2; } }
		private ExperBitComponent m_ebcBit3 = new ExperBitComponent();
		public ExperBitComponent ebcBit3 { get { return m_ebcBit3; } }
		private ExperBitComponent m_ebcBit4 = new ExperBitComponent();
		public ExperBitComponent ebcBit4 { get { return m_ebcBit4; } }
		private ExperBitComponent m_ebcBit5 = new ExperBitComponent();
		public ExperBitComponent ebcBit5 { get { return m_ebcBit5; } }
		private ExperBitComponent m_ebcBit6 = new ExperBitComponent();
		public ExperBitComponent ebcBit6 { get { return m_ebcBit6; } }
		private ExperBitComponent m_ebcBit7 = new ExperBitComponent();
		public ExperBitComponent ebcBit7 { get { return m_ebcBit7; } }
		private ExperBitComponent m_ebcBit8 = new ExperBitComponent();
		public ExperBitComponent ebcBit8 { get { return m_ebcBit8; } }
		private ExperBitComponent m_ebcBit9 = new ExperBitComponent();
		public ExperBitComponent ebcBit9 { get { return m_ebcBit9; } }
		private ExperBitComponent m_ebcBitA = new ExperBitComponent();
		public ExperBitComponent ebcBitA { get { return m_ebcBitA; } }
		private ExperBitComponent m_ebcBitB = new ExperBitComponent();
		public ExperBitComponent ebcBitB { get { return m_ebcBitB; } }
		private ExperBitComponent m_ebcBitC = new ExperBitComponent();
		public ExperBitComponent ebcBitC { get { return m_ebcBitC; } }
		private ExperBitComponent m_ebcBitD = new ExperBitComponent();
		public ExperBitComponent ebcBitD { get { return m_ebcBitD; } }
		private ExperBitComponent m_ebcBitE = new ExperBitComponent();
		public ExperBitComponent ebcBitE { get { return m_ebcBitE; } }
		private ExperBitComponent m_ebcBitF = new ExperBitComponent();
		public ExperBitComponent ebcBitF { get { return m_ebcBitF; } }
		private ExperBitComponent m_ebcBit10 = new ExperBitComponent();
		public ExperBitComponent ebcBit10 { get { return m_ebcBit10; }}
		private ExperBitComponent m_ebcBit11 = new ExperBitComponent();
		public ExperBitComponent ebcBit11 { get { return m_ebcBit11; } }
		private ExperBitComponent m_ebcBit12 = new ExperBitComponent();
		public ExperBitComponent ebcBit12 { get { return m_ebcBit12; } }
		private ExperBitComponent m_ebcBit13 = new ExperBitComponent();
		public ExperBitComponent ebcBit13 { get { return m_ebcBit13; } }
		private ExperBitComponent m_ebcBit14 = new ExperBitComponent();
		public ExperBitComponent ebcBit14 { get { return m_ebcBit14; } }
		private ExperBitComponent m_ebcBit15 = new ExperBitComponent();
		public ExperBitComponent ebcBit15 { get { return m_ebcBit15; } }
		private ExperBitComponent m_ebcBit16 = new ExperBitComponent();
		public ExperBitComponent ebcBit16 { get { return m_ebcBit16; } }
		private ExperBitComponent m_ebcBit17 = new ExperBitComponent();
		public ExperBitComponent ebcBit17 { get { return m_ebcBit17; } }
		private ExperBitComponent m_ebcBit18 = new ExperBitComponent();
		public ExperBitComponent ebcBit18 { get { return m_ebcBit18; } }
		private ExperBitComponent m_ebcBit19 = new ExperBitComponent();
		public ExperBitComponent ebcBit19 { get { return m_ebcBit19; } }
		private ExperBitComponent m_ebcBit1A = new ExperBitComponent();
		public ExperBitComponent ebcBit1A { get { return m_ebcBit1A; } }
		private ExperBitComponent m_ebcBit1B = new ExperBitComponent();
		public ExperBitComponent ebcBit1B { get { return m_ebcBit1B; } }
		private ExperBitComponent m_ebcBit1C = new ExperBitComponent();
		public ExperBitComponent ebcBit1C { get { return m_ebcBit1C; } }
		private ExperBitComponent m_ebcBit1D = new ExperBitComponent();
		public ExperBitComponent ebcBit1D { get { return m_ebcBit1D; } }
		private ExperBitComponent m_ebcBit1E = new ExperBitComponent();
		public ExperBitComponent ebcBit1E { get { return m_ebcBit1E; } }
		private ExperBitComponent m_ebcBit1F = new ExperBitComponent();
		public ExperBitComponent ebcBit1F { get { return m_ebcBit1F; } }
		#endregion

		// <summary>
		// Constructor, assign ArrRegComponent[] array point to ebcBit, and "bit_0" string initialization
		// </summary>
		public ExperModel()
		{
			//RegisterComponent = new ExperBitComponent[8];
			ArrRegComponet[0] = ebcBit0;
			ArrRegComponet[1] = ebcBit1;
			ArrRegComponet[2] = ebcBit2;
			ArrRegComponet[3] = ebcBit3;
			ArrRegComponet[4] = ebcBit4;
			ArrRegComponet[5] = ebcBit5;
			ArrRegComponet[6] = ebcBit6;
			ArrRegComponet[7] = ebcBit7;
			ArrRegComponet[8] = ebcBit8;
			ArrRegComponet[9] = ebcBit9;
			ArrRegComponet[10] = ebcBitA;
			ArrRegComponet[11] = ebcBitB;
			ArrRegComponet[12] = ebcBitC;
			ArrRegComponet[13] = ebcBitD;
			ArrRegComponet[14] = ebcBitE;
			ArrRegComponet[15] = ebcBitF; 
			ArrRegComponet[16] = ebcBit10;
			ArrRegComponet[17] = ebcBit11;
			ArrRegComponet[18] = ebcBit12;
			ArrRegComponet[19] = ebcBit13;
			ArrRegComponet[20] = ebcBit14;
			ArrRegComponet[21] = ebcBit15;
			ArrRegComponet[22] = ebcBit16;
			ArrRegComponet[23] = ebcBit17;
			ArrRegComponet[24] = ebcBit18;
			ArrRegComponet[25] = ebcBit19;
			ArrRegComponet[26] = ebcBit1A;
			ArrRegComponet[27] = ebcBit1B;
			ArrRegComponet[28] = ebcBit1C;
			ArrRegComponet[29] = ebcBit1D;
			ArrRegComponet[30] = ebcBit1E;
			ArrRegComponet[31] = ebcBit1F;
			yRegLength = 0x08;
			for (int i = 0; i < 32; i++)
			{
				ArrRegComponet[i].strBit = string.Format("{0:D}", i);
			}
			bXprRegShow = true;
			strGroupReg = "";
			bValueChange = false;
			bMarkReg = false;
			strRegName = string.Empty;
		}

		// <summary>
		// Summarize value of all bit and update in Register Value,
		// </summary>
		// <param name="bRdIn">if true, also summarize all bRead property default true</param>
		// <param name="bWtIn">if true, also summarize all bWrite propery; default true</param>
		public void SumRegisterValue(bool bRdIn = true, bool bWtIn = true)
		{
			UInt32 u32tmp;
			bool brtmp, bwtmp;

			u32tmp = 0; brtmp = false; bwtmp = false;
			for (int i = 0; i < yRegLength; i++)
			{
				u32tmp += Convert.ToUInt32((UInt32)(ArrRegComponet[i].yBitValue << i));
				brtmp |= ArrRegComponet[i].bRead;
				bwtmp |= ArrRegComponet[i].bWrite;
			}

			foreach (KeyValuePair<string, Reg> inreg in m_pmrExpMdlParent.reglist)
			{
				if (inreg.Value.address == u32RegNum)
				{
					if (inreg.Key.Equals("Low"))
					{
						//m_u16RegVal &= 0x00FF;
					}
					else
					{
						//m_u16RegVal &= 0xFF00;
						u32tmp <<= 8;
					}
				}
			}

			u32RegVal = u32tmp;
			if(bRdIn)	 bRead = brtmp;
			if(bWtIn)	 bWrite = bwtmp;
		}

		// <summary>
		// Parse register value to each bit value,
		// </summary>
		public void SeperateRegValueToBit()
		{
			UInt32 u32tmp = u32RegVal;

			for (int i = 0; i < yRegLength; i++)
			{
				ArrRegComponet[i].yBitValue = Convert.ToByte((u32tmp >> i) & 0x01);
			}
		}

		// <summary>
		// Search all bit contents that Parameter datastructure
		// </summary>
		// <returns>if found, return Parameter data structure; otherwise return null</returns>
		public Parameter GetParentParameter()
		{
			Parameter pmrtmp = null;
			//Reg regtmp = null;

			foreach (ExperBitComponent expbittmp in ArrRegComponet)
			{
				if ((expbittmp.u32Guid != 0) && (expbittmp.pmrBitParent != null))
				{
					pmrtmp = expbittmp.pmrBitParent;
					break;
				}
			}

			//pmrExpMdlParent = pmrtmp;
			//pmrExpMdlParent.guid = pmrtmp.guid;
            if ((pmrtmp.guid & ExperViewMode.SectionElementFlag) == ExperViewMode.NonvolatileElement)
                pmrExpMdlParent.guid = pmrtmp.guid;
            else
                pmrExpMdlParent.guid = (pmrtmp.guid & ~ExperViewMode.SectionElementFlag) | ExperViewMode.OperationElement;
			pmrExpMdlParent.phyref = 1;
			pmrExpMdlParent.regref = 1;
			pmrExpMdlParent.subtype = 0;		//COBRA_PARAM_SUBTYPE
			pmrExpMdlParent.subsection = pmrtmp.subsection;	//(A140409)Francis
            pmrExpMdlParent.sfllist = pmrtmp.sfllist;
			pmrExpMdlParent.reglist.Clear();
			//foreach (KeyValuePair<string, Reg> dicreg in pmrExpMdlParent.reglist)
			foreach (KeyValuePair<string, Reg> tmpreg in pmrtmp.reglist)
			{
				////if (tmpreg.Key.Equals("High"))
				////{
				//	//if (tmpreg.Value.address != u16RegNum)
				//	//{
				//		//break;
				//	//}
				////}
				//if (tmpreg.Value.address == u16RegNum)
				//{
				//	Reg newReg = new Reg();
				//	newReg.address = tmpreg.Value.address;
				//	newReg.bitsnumber = tmpreg.Value.bitsnumber;
				//	newReg.startbit = tmpreg.Value.startbit;
				//	//regtmp = dicreg.Value;
				//	//regtmp.startbit = 0;
				//	//regtmp.bitsnumber = yRegLength;	//force 8-bits or 16-bits length
				//	//dicreg.Value.address = pmrtmp.reglist
				//	//dicreg.Value.startbit = 0;
				//	//dicreg.Value.bitsnumber = yRegLength;
				//	//pmrExpMdlParent.reglist.Add(tmpreg.Key, newReg);
				//	pmrExpMdlParent.reglist.Add("Low", newReg);
				//}
				if (tmpreg.Key.ToLower().Equals("low"))
				{
					//debug usage
					//if ((xmlDatatg.yIndex != SharedFormula.LoByte(inreg.Value.address)) ||
					//(xmlDatatg.yBitStart != SharedFormula.LoByte(inreg.Value.startbit)) ||
					//(xmlDatatg.yLength != SharedFormula.LoByte(inreg.Value.bitsnumber)))
					//{
					//MessageBox.Show(string.Format("index=0x{0:2X}, bit=0x{1:2X}, length=0x{2:2X}", xmlDatatg.yIndex, xmlDatatg.yBitStart, xmlDatatg.yLength));
					//}
					Reg newReg_low = new Reg();
					newReg_low.address = tmpreg.Value.address;
					newReg_low.u32Address = tmpreg.Value.u32Address;
					newReg_low.bitsnumber = tmpreg.Value.bitsnumber;
					newReg_low.startbit = tmpreg.Value.startbit;
					pmrExpMdlParent.reglist.Add("Low", newReg_low);
				}
				else if (tmpreg.Key.ToLower().Equals("high"))
				{
					Reg newReg_hi = new Reg();
					newReg_hi.address = tmpreg.Value.address;
					newReg_hi.u32Address = tmpreg.Value.u32Address;
					newReg_hi.bitsnumber = tmpreg.Value.bitsnumber;
					newReg_hi.startbit = tmpreg.Value.startbit;
					pmrExpMdlParent.reglist.Add("High", newReg_hi);
				}
			}
			foreach (KeyValuePair<string, Reg> dicreg in pmrExpMdlParent.reglist)
			{
				dicreg.Value.startbit = 0;
				dicreg.Value.bitsnumber = yRegLength;
			}

			//return pmrtmp;
			return pmrExpMdlParent;
		}

		public void ArrangeBitPhyValue(AsyncObservableCollection<ExperModel> expregListIn = null)
		{
			UInt32 utmp, uMask;
			UInt16 uaddr = 0x00;
			int i =0;

			foreach (ExperBitComponent expbittmp in ArrRegComponet)
			{
				uMask = 0xFFFFFFFF;
				if ((expbittmp.u32Guid != 0) && (expbittmp.pmrBitParent != null))
				{
                    if ((expbittmp.u32Guid & ExperViewMode.SectionElementFlag) == ExperViewMode.OperationElement || (expbittmp.u32Guid & ExperViewMode.SectionElementFlag) == ExperViewMode.NonvolatileElement)
					{
						expbittmp.dbPhyValue = expbittmp.pmrBitParent.phydata;
						if(expregListIn != null)	//need to find another ExperModel(another Low or High byte of 1 physical value) in List,
						{
							if (expbittmp.pmrBitParent.reglist.Count > 1)
							{
								foreach(KeyValuePair<string, Reg> kvptmp in expbittmp.pmrBitParent.reglist)
								{
									if(kvptmp.Value.address != u32RegNum)
									{
										uaddr = kvptmp.Value.address;
										break;
									}
								}
								foreach (ExperModel epx in expregListIn)
								{
									if (epx.u32RegNum == uaddr)
									{
										foreach (ExperBitComponent ebctmp in epx.ArrRegComponet)
										{
											if ((ebctmp.u32Guid != 0) && 
                                                (ebctmp.pmrBitParent != null) &&
                                                ((((ebctmp.u32Guid & ExperViewMode.SectionElementFlag) == ExperViewMode.OperationElement)) || ((ebctmp.u32Guid & ExperViewMode.SectionElementFlag) == ExperViewMode.NonvolatileElement))
                                                )
											ebctmp.dbPhyValue = expbittmp.pmrBitParent.phydata;
										}
										break;
									}
								}
							}
						}
					}
					else if ((expbittmp.u32Guid & ExperViewMode.SectionElementFlag) == ExperViewMode.TesTOperElement)
					{
						utmp = u32RegVal;
						for (int j = 0; j < i; j++)
						{
							utmp = (UInt16)(utmp >> 1);
						}
						for(int k=16; k > expbittmp.yDescripVisiLgth; k--)
						{
							uMask = (UInt16)(uMask >> 1);
						}
						utmp &=uMask;
						expbittmp.dbPhyValue = utmp;
					}
				}
				i += 1;
			}
		}
	}

	//Data structure of TestMode, mapping to XML, 
	public class ExperTestButtonModel : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;
		public void OnPropertyChanged(string propName)
		{
			if (PropertyChanged != null)
				PropertyChanged(this, new PropertyChangedEventArgs(propName));
		}

		private Parameter m_pmrTestMdlParent = new Parameter();
		public Parameter pmrTestMdlParent
		{
			get { return m_pmrTestMdlParent; }
			set { m_pmrTestMdlParent = value; }
		}

		public Parameter pmrXDParent { get; set; }		//save Parameter node reference pointer, that is from Shell
		public UInt32 u32Guid { get; set; }						//save GUID UInt32  value
		public string strTestTrim { get; set; }					//save from <TestMode> in xml private section; also binding to UI, string of button
		public byte ySubValue { get; set; }						//save from <SubValue> in xml private section; value of test mode command, that is defined in spec
		private bool m_bTestBtnEnable;							//save from <Enable> in xml private section; 
		public bool bTestBtnEnable
		{
			get { return m_bTestBtnEnable; }
			set { m_bTestBtnEnable = value; OnPropertyChanged("bTestBtnEnable"); }
		}	//binding to UI, enable/disable the button; actively switch En/Disable when running
		public bool bTestBtnVisible { get; set; }				//save from <CustomerMode> currently it's equal to (bdata | ctrParent.bEngModeRunning);  binding to UI, display the button; it should only be mapped to UI once
		//(M150105)Francis, create <Host> node in xml, to indicate Host or not
		public bool bHost { get; set; }								//save the normal mode button; that is indicated from xml, if there is <Enable> node.
		public bool bReadBack { get; set; }						//save from <ReadBack> in xml private section, indicate TestMode should be read register back to confirm after command to device; default is true
		public UInt32 u32Group { get; set; }						//save from <Group> in xml private section, which Normal Mode button should be enable that after going into TestMode
		public byte yOrder { get; set; }								//save from <Order> in xml private section, sequence of display order
		public bool bRegReadFrom { get; set; }				//save from <RegReadFrom> in xml private section, default is false; if true, indicate that Test Mode Register should be read after Test Mode command
		public bool bRegWriteTo { get; set; }					//save from <RegWriteTo> in xml private section, default is false; if true indicate that Test Mode Register should be wrote before Test Mode command
        public UInt16 wSubTask { get; set; }                    //(A161024)Francis, save <SubTask> in xml private section, it is used for TM_COMMAND combining with multiple sub_task function.

		public ExperTestButtonModel()
		{
			bTestBtnEnable = true;
			bTestBtnVisible = true;
			bHost = false;	//default is not normal mode button, and there could be more than 1 normal mode button defined in xml
			bReadBack = true;
			bRegReadFrom = false;
			bRegWriteTo = false;
            wSubTask = 0x00;
		}
	}
}
