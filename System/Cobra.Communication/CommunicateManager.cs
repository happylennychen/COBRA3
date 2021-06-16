using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using Cobra.Common;
using Cobra.Communication.I2C;
using Cobra.Communication.SPI;
using Cobra.Communication.SVID;
using Cobra.Communication.RS232;

namespace Cobra.Communication
{
	[Guid("FBB3BDBF-18DA-4EBA-888A-D72A3C87A6C4")]
	//[InterfaceType(ComInterfaceType.InterfaceIsDual)]
	public interface ICCommunicateInterface
	{
		[DispId(1)]
		bool OpenI2CAdapter();
		[DispId(2)]
		bool CloseDevice();
		[DispId(3)]
		void GetVersion(ref UInt32 dwCommVe);
		[DispId(4)]
		bool ReadDevice(byte[] yDataIn, ref byte[] yDataOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 1, UInt16 wDataInWrite = 1);
		[DispId(5)]
		bool WriteDevice(byte[] yDataIn, ref byte[] yDataOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 1);
		[DispId(6)]
		void GetLastErrorCode(ref UInt32 dwErrorCode);
		[DispId(7)]
		bool SetI2CConfigure(UInt32 wI2CFrequence);
		[DispId(8)]
		bool GetI2CConfigure(ref UInt32 wI2CFrequence);
		[DispId(9)]
		bool ResetInterface();
        [DispId(10)]
        bool OpenSPIAdapter();
        [DispId(11)]
        bool SetSPIConfigure(UInt32 wSPIBaudRate, byte ySPIPolariy, byte ySPIPhase, byte ySPIBitOrder, byte ySPISSPolarity, byte ySPIWire);
        [DispId(12)]
        bool GetSPIConfigure(ref byte ySPIPolariy, ref byte ySPIPhase, ref byte ySPIBitOrder, ref byte ySPISSPolarity, ref byte ySPIWire);
		[DispId(13)]
		bool SendCommandtoAdapter(byte[] yDataIn, ref byte[] yDataOut, ref UInt16 wDataOutLength, UInt16 wDataInLength);
		[DispId(14)]
		string GetLastErrorDescrip(ref UInt32 dwErrorCode);
	}

	[Guid("1A0E116F-4F89-441A-B3F5-817180E2A1A8")]
	[ClassInterface(ClassInterfaceType.None)]
	/// <remark>
	/// Communication Manage class, manage hardware interface device and communicate through interface. 
	/// User can utiliz this Object to communicate with chip device through adaptor interface. 
	/// <example>
	/// <code>
	/// // create the class
	/// CCommunicateManager myHWInterface = null;;
	/// myHWInterface = new CCommunicateManager();
	/// </code>
	/// </example>
	/// </remark>
	public class CCommunicateManager : ICCommunicateInterface
	{
		#region Private Constant Definition

		// <summary>
		// Version description, constructed by [Major].[Middle].[Minor].[Test]. But only [Major].[Middle].[Minor] will be used in formal release
		// <permission cref="System.Security.PermissionSet">Private Access</permission>
		// </summary>
		private const byte m_MajorVer = 0x02;
		private const byte m_MiddleVer = 0x00;
		private const byte m_MinorVer = 0x03;
		private const byte m_TestVer = 0x00;

		private string[] m_supported = new string[] {"O2USB".ToString(), "Aadvark".ToString()};
		private const byte m_o2Index = 0;
		private const byte m_aadIndex = 1;

		private const byte m_linkshift1 = 3;
		private const byte m_linkshift2 = 4;
		private const byte m_linkshift3 = 3;
		

		#endregion

		#region Private Members Declaration

		// <summary>
		// Instance of Error Code, save last error code of communication
		// </summary>
		private UInt32 m_dwErrCode;

		// <summary>
		// Instance of I2C/SPI Interface object, will be created by calling OpenDevice()
		// </summary>
		private CInterfaceBase m_InfDev = null;

		// <summary>
		// Instance of interface type value, save used type
		// </summary>
/*		private HWInterfaceType m_myHWType = HWInterfaceType.HW_Disconnected;*/

		// <summary>
		// Handler of RegisterDeviceNotification() used
		// </summary>
		private IntPtr gPtrNotifyDevNode;

		// <summary>
		// Handle Source, a fake window to hook Windows Broadcast Message
		// </summary>
		private HwndSource hwndSource = new HwndSource(0, 0, 0, 0, 0, "fake", IntPtr.Zero);

		// <summary>
		// Symbolic name of all connected devices
		// </summary>
		private static AsyncObservableCollection<string> m_USBlinkname = new AsyncObservableCollection<string>();	//save USBtoI2C (SPI) string
		private static AsyncObservableCollection<string> m_SVIDlinkname = new AsyncObservableCollection<string>();	//save SVID string
		private static AsyncObservableCollection<string> m_COMlinkname = new AsyncObservableCollection<string>();	//save COM port string
		/*		public AsyncObservableCollection<string> PortLinkName
				{
					get
					{
						return m_USBlinkname;
					}
				}
		*/

		private BusOptions m_DevBus = null;

		private bool bCustomer = false;		//hide some code not run for customer release version

        private string PortStringSelected = string.Empty;

		private bool bFromExternal = false;		//External usage of DLL

		#endregion

		#region Public Constant Definition

		// <summary>
		// Definition type of Hardware Interface, we're having O2USBtoI2C, AardvarkI2C(not supported), O2USBtoSPI(not supported), and AardvarkSPI(not supported)
		// </summary>
//		public enum HWInterfaceType : byte
//		{
//			HW_Disconnected = 0x00,
//			HW_O2USBtoI2C,					// = 0x01
//			HW_AardvarkI2C,						// = 0x02
//			HW_O2USBtoSPI,					// = 0x03
//			HW_AardvarkSPI,						// = 0x04
//		}

		// <summary>
		// Definition of configure type, to indicate what configure value to set/get to/from interface adapter
		// </summary>
//		public enum ConfigureType : byte
//		{
//			CFG_CONFIGUREALL		= 0x00,
//			CFG_I2CFREQUENCY		= 0x10,
//			CFG_SPIBAUDRATE			= 0x20,
//			CFG_SPICONFIG				= 0x21,
//		}

		/// <summary>
		/// Maximum number of interface connected, maximum is 4
		/// </summary>
		public const UInt16 MAX_COMM_DEVICES = 64;

		/// <summary>
		/// Max buffer length of Read/Write data
		/// </summary>
		public const UInt16 MAX_RWBUFFER = 10240;

		/// <summary>
		/// O2 USBtoSPI adapter configure value bit 0. If set up bit 0 as 1, adapter will communicate with MSB; otherwise will communicate with LSB
		/// </summary>
		public const byte O2SPI_CONFIG_MSB = 0x01;

		/// <summary>
		/// O2 USBtoSPI adapter configure value bit 1. If set up bit 1 as 1, adapter will communicate with High Active Phase; otherwise will communicate with Low Active Phase
		/// </summary>
		public const byte O2SPI_CONFIG_PHASE = 0x02;

		/// <summary>
		/// O2 USBtoSPI adapter configure value bit 2. If set up bit 2 as 1, adapter will communicate with High Active Polarity; otherwise will communicate with Low Active Polarity
		/// </summary>
		public const byte O2SPI_CONFIG_POLARITY = 0x04;

		/// <summary>
		/// O2 USBtoSPI adapter configure value bit 4. If set up bit 4 as 1, adapter will work as Master Device; otherwise will will work as Slave Device
		/// </summary>
		public const byte O2SPI_CONFIG_MASTER = 0x10;

		/// <summary>
		/// O2 USBtoSPI adapter configure value bit 5. If set up bit 5 as 1, adapter will communicate in 4-wires architecture; otherwise will communicate in 3-wires architecture
		/// </summary>
		public const byte O2SPI_CONFIG_4WIRE = 0x20;

		private const byte O2SPI_CONFIG_UNKNOW = 0x40;

		/// <summary>
		/// O2 USBtoSPI adapter configure value bit 7. If set up bit 7 as 1, adapter will treat Slave Device like Polarity as High Active; otherwise will treate Slave Device like Polarity as Low Active
		/// </summary>
		public const byte O2SPI_CONFIG_SSPOLARITY = 0x80;

		#endregion

		#region Private Method

		// <summary>
		// Convert SymbolicName to a special unique name that display in BusOption UI; 
		// function will convert all string in <string>m_USBlinkname into BusOptions.ConnectDevice
		// </summary>
		// <param name="tgGuid">target GUID string</param>
		// <param name="opBus">in/out BusOptions class</param>
		private void ConvertSymbolnameToUIPort(Guid tgGuid, ref BusOptions opBus, byte myIndex)
		{
			string strConnect;
			int iSer, iLoc;
            //int index = 1;
			Options opPort = null;// opBus.GetOptionsByGuid(BusOptions.ConnectPort_GUID);

			if (opBus != null)
			{
				if (opBus.BusType != BUS_TYPE.BUS_TYPE_SPI)
				{
					opPort = opBus.GetOptionsByGuid(BusOptions.ConnectPort_GUID);
				}
				else
				{
					opPort = opBus.GetOptionsByGuid(BusOptions.SPIConnectPort_GUID);
				}
			}

            if(opPort == null)
            {
                m_dwErrCode = LibErrorCode.IDS_ERR_MGR_UNABLE_LOAD_DRIVER;
                return;
            }

			foreach (string strnode in m_USBlinkname)
			{
				//strConnect = "O".ToString();
				strConnect = m_supported[myIndex][0].ToString();
				iSer = strnode.IndexOf('#');
				iSer = strnode.IndexOf('#', iSer + 1);
				iSer += m_linkshift1;
				strConnect += strnode.Substring(iSer, 2);
				iSer += m_linkshift2;
				strConnect += strnode.Substring(iSer, 1);
				iLoc = strnode.IndexOf(tgGuid.ToString()) - m_linkshift3;
				strConnect += strnode.Substring(iLoc, 1);
				strConnect += new string(" - ".ToArray());
				strConnect += m_supported[myIndex];
				strConnect = strConnect.ToUpper();

                ComboboxRoad cRoad = new ComboboxRoad();
                cRoad.ID = opPort.LocationSource.Count + 1;
                cRoad.Info = strConnect;
                opPort.LocationSource.Add(cRoad);
                if(string.Equals(strConnect, PortStringSelected))
                {
                    opPort.SelectLocation = cRoad;
                }
                //opPort.itemlist.Add(strConnect);

                //index++;
			}
		}

        private void RemoveSymbolnameFromUIPort(Guid tgGuid, ref BusOptions opBus, byte myIndex)
        {
            string strConnect;
            int iSer, iLoc;
            //int index = 1;
			Options opPort = null;// opBus.GetOptionsByGuid(BusOptions.ConnectPort_GUID);

			if (opBus != null)
			{
				if (opBus.BusType != BUS_TYPE.BUS_TYPE_SPI)
				{
					opPort = opBus.GetOptionsByGuid(BusOptions.ConnectPort_GUID);
				}
				else
				{
					opPort = opBus.GetOptionsByGuid(BusOptions.SPIConnectPort_GUID);
				}
			}

			if (opPort == null)
            {
                m_dwErrCode = LibErrorCode.IDS_ERR_MGR_UNABLE_LOAD_DRIVER;
                return;
            }

            PortStringSelected = string.Format("{0}", opPort.SelectLocation.Info);
            opPort.LocationSource.Clear();
            ComboboxRoad tRoad = new ComboboxRoad();
            tRoad.ID = 0;
            tRoad.Info = "Disconnected";
            opPort.LocationSource.Add(tRoad);
            opPort.SelectLocation = tRoad;

            foreach (string strnode in m_USBlinkname)
            {
                //strConnect = "O".ToString();
                strConnect = m_supported[myIndex][0].ToString();
                iSer = strnode.IndexOf('#');
                iSer = strnode.IndexOf('#', iSer + 1);
                iSer += m_linkshift1;
                strConnect += strnode.Substring(iSer, 2);
                iSer += m_linkshift2;
                strConnect += strnode.Substring(iSer, 1);
                iLoc = strnode.IndexOf(tgGuid.ToString()) - m_linkshift3;
                strConnect += strnode.Substring(iLoc, 1);
                strConnect += new string(" - ".ToArray());
                strConnect += m_supported[myIndex];
                strConnect = strConnect.ToUpper();

                ComboboxRoad cRoad = new ComboboxRoad();
                cRoad.ID = opPort.LocationSource.Count + 1;
                cRoad.Info = strConnect;
                opPort.LocationSource.Add(cRoad);
                if (string.Equals(strConnect, PortStringSelected))
                {
                    opPort.SelectLocation = cRoad;
                }
                //opPort.itemlist.Add(strConnect);

                //index++;
            }
        }

		// <summary>
		// Convert Com Symbol to a special unique name that display in BusOption UI; 
		// function will convert all string in <string>m_SVIDlinkname into BusOptions.ConnectDevice
		// </summary>
		// <param name="opBus">in/out BusOptions class</param>
		private void ConvertSVIDSymbolToUIPort(ref BusOptions opBus)
		{
			string strComCon;
			//int index = opBus.ConnectPort.Count;
			Options opPort = null;// opBus.GetOptionsByGuid(BusOptions.ConnectPort_GUID);

			if (opBus != null)
			{
				if (opBus.BusType != BUS_TYPE.BUS_TYPE_SPI)
				{
					opPort = opBus.GetOptionsByGuid(BusOptions.ConnectPort_GUID);
				}
				else
				{
					opPort = opBus.GetOptionsByGuid(BusOptions.SPIConnectPort_GUID);
				}
			}
			
			if (opPort == null)
            {
                m_dwErrCode = LibErrorCode.IDS_ERR_MGR_UNABLE_LOAD_DRIVER;
                return;
            }

			if (opBus.BusType != BUS_TYPE.BUS_TYPE_SVID)
				return;

			foreach (string strcom in m_SVIDlinkname)
			{
				strComCon = strcom + " - O2SVID";
                ComboboxRoad cRoad = new ComboboxRoad();
                cRoad.ID = opPort.LocationSource.Count + 1;
                cRoad.Info = strComCon;
                opPort.LocationSource.Add(cRoad);
                //opPort.itemlist.Add(strComCon);
                if (string.Equals(strComCon, PortStringSelected))
                {
                    opPort.SelectLocation = cRoad;
                }

				//index += 1;
			}
		}

		private void ConvertComSymbolToUIPort(ref BusOptions opBus)
		{
			string strComCon;
			//int index = opBus.ConnectPort.Count;
			Options opPort = null;// opBus.GetOptionsByGuid(BusOptions.ConnectPort_GUID);

			if (opBus != null)
			{
				if (opBus.BusType != BUS_TYPE.BUS_TYPE_SPI)
				{
					opPort = opBus.GetOptionsByGuid(BusOptions.ConnectPort_GUID);
				}
				else
				{
					opPort = opBus.GetOptionsByGuid(BusOptions.SPIConnectPort_GUID);
				}
			}
			
			if (opPort == null)
            {
                m_dwErrCode = LibErrorCode.IDS_ERR_MGR_UNABLE_LOAD_DRIVER;
                return;
            }
            
            //remove same COM port from m_COMlinkname, if that COM port is found in m_SVIDlinkname
			for (int i = 0; i < m_COMlinkname.Count; i++)
			{
				foreach (string strsvid in m_SVIDlinkname)
				{
					if (m_COMlinkname[i].IndexOf(strsvid) != -1)
					{
						m_COMlinkname.Remove(m_COMlinkname[i]);
					}
				}
			}

			foreach (string strcom in m_COMlinkname)
			{
				strComCon = strcom;
                ComboboxRoad cRoad = new ComboboxRoad();
                cRoad.ID = opPort.LocationSource.Count + 1;
                cRoad.Info = strComCon;
                opPort.LocationSource.Add(cRoad);
                //opPort.itemlist.Add(strComCon);
                if (string.Equals(strComCon, PortStringSelected))
                {
                    opPort.SelectLocation = cRoad;
                }
            }
		}

		// <summary>
		// Find index value of current <string>m_USBlinkname. tagIndex is indicating which interface device, method will use its GUID to 
		// filter symbolicname in m_USBlinkname. strConDev is indicating the device nick name (created by CommunicateManager), this 
		// nick name is saved in xml, and meaning which interface that was used. Method will use these 2 parameter to find correct interface 
		// device from m_USBlinkname, and set up index of <m_USBlinkname> in 3rd parameter iPort, if founde; otherwise set up iPort = -1;
		// </summary>
		// <param name="tagIndex">input, index value for O2USB or Aadvark, or other supported interface device</param>
		// <param name="strConDev">input, string of ConnectedDevice that saved in xml file</param>
		// <param name="iPort">output, index value of current <string>m_USBlinkname.</param>
		private void SymbolicSearch(byte tagIndex, string strConDev, ref Int16 iPort)
		{
			string[] strConnect = new string[MAX_COMM_DEVICES];
			int iSer, iLoc, iNumber;
			Guid guidTag;

			iPort = -1;
			if(tagIndex == m_o2Index)
			{
				guidTag = CO2USBI2CAdapter.GetGuid();
			}
			else
			{
				return;			//currently support O2USB only
			}

			iNumber = 0;
			foreach (string strnode in m_USBlinkname)
			{
				iLoc = strnode.IndexOf(guidTag.ToString());
				if(iLoc != -1)
				{
//					strConnect[iNumber] = m_supported[tagIndex] + " - O";
					strConnect[iNumber] = "O";
					iSer = strnode.IndexOf('#');
					iSer = strnode.IndexOf('#', iSer + 1);
					iSer += m_linkshift1;
					strConnect[iNumber] += strnode.Substring(iSer, 2);
					iSer += m_linkshift2;
					strConnect[iNumber] += strnode.Substring(iSer, 1);
					iLoc -= m_linkshift3;
					strConnect[iNumber] += strnode.Substring(iLoc, 1);
					strConnect[iNumber] += " - ".ToString();
					strConnect[iNumber] += m_supported[tagIndex];
				}
				else
				{
					strConnect[iNumber] = strnode;
				}
				iNumber++;
			}

			//(A171122)Francis, id=617, in order to export DLL, we make Connect Port and disalbe hook
			if (bFromExternal)
			{
				for (int i = 0; i < iNumber; i++)
				{
					if (!string.Equals(strConnect[i], "null"))
					{
						iPort = (Int16)i;
						break;
					}
				}
				if (hwndSource != null)  //Attaching to window procedure
					hwndSource.RemoveHook(new HwndSourceHook(this.hwndSourceHook));
			}	//(E171122)Francis
			else
			{
				for (int i = 0; i < iNumber; i++)
				{
					strConnect[i] = strConnect[i].ToUpper();
					if (string.Equals(strConnect[i], strConDev))
					{
						iPort = (Int16)i;
						break;
					}
				}
			}
		}

		// <summary>
		// Find index value of current <string>m_SVIDlinkname. 
		// Method will use COM+number + SVID filter symbolicname in m_SVIDlinkname. 
		// strConDev is indicating the device nick name (created by CommunicateManager), this nick name is saved in xml, 
		// and meaning which interface that was used. Method will use these 2 parameter to find correct interface 
		// device from m_SVIDlinkname, and set up index of <m_USBlinkname> in 3rd parameter iPort, if founde; otherwise set up iPort = -1;
		// </summary>
		// <param name="strConDev">input, string of ConnectedDevice that saved in xml file</param>
		// <param name="iPort">output, index value of current <string>m_USBlinkname.</param>
		private void SVIDSymbolSearch(string strConDev, ref Int16 iPort)
		{
			string[] strConnect = new string[MAX_COMM_DEVICES];
			int iNumber;

			iPort = -1;

			iNumber = 0;
			foreach (string strnode in m_SVIDlinkname)
			{
				strConnect[iNumber] = strnode + " - O2SVID";
				iNumber++;
			}

			for (int i = 0; i < iNumber; i++)
			{
				strConnect[i] = strConnect[i].ToUpper();
				if (string.Equals(strConnect[i], strConDev))
				{
					iPort = (Int16)i;
					break;
				}
			}
		}

		private void COMSymbolSearch(string strConDev, ref Int16 iPort)
		{
			string[] strConnect = new string[MAX_COMM_DEVICES];
			int iNumber;

			iPort = -1;

			iNumber = 0;
			foreach (string strnode in m_COMlinkname)
			{
				strConnect[iNumber] = strnode;
				iNumber++;
			}

			for (int i = 0; i < iNumber; i++)
			{
				strConnect[i] = strConnect[i].ToUpper();
				if (string.Equals(strConnect[i], strConDev))
				{
					iPort = (Int16)i;
					break;
				}
			}
		}

		// <summary>
		// Convert Symbolic link name from Windows Message to readable link name, like stored in InterfaceBase.m_strSymbolicLinkName
		// </summary>
		// <param name="inChar">in/output character buffer, will skip '\0' character after each meaningful character then save in InChar[]</param>
		// <param name="iLength">output, save the length of meaningful character in inChar[] array after parsing</param>
		private void DaisySymbolicConvert(ref char[] inChar, ref Int16 iLength)//, ref string outStr)
		{
			char[] tmpchar = new char[255];
			Int16 i, j;

			j = 0; iLength = 0;
			for (i = 0; i < inChar.GetLength(0); i+=2)
			{
				if (inChar[i] != '\0')
				{
					tmpchar[j] = inChar[i];
					j++;
				}
				else
				{
					break;
				}
			}
//			outStr = tmpchar.ToString();
//			System.Array.Clear(tmpchar, leng, 255 - leng);
			System.Array.Copy(tmpchar, inChar, 255);
			iLength = j;
		}

        // <summary>
        // Take input BusOptions to get what user selected by BusOptions.SelectLocation.Info. Take user selected to check m_USBlinkname has
        // this selected string or not. If having in m_USBlinkname, open it. This method is used to open I2C and I2C2 communication type.
        // </summary>
        // <param name="opBus">in/out BusOptions, </param>
        // <returns>True: if found user selected from connected hardware; Flase: cannot found user selected</returns>
		private bool FindOpenO2USB2I2CAdaptor(ref BusOptions opBus)
		{
			bool bReturn = false;
			Int16 iIndex = -1;// = opBus.PortIndex;
            Options opPort = opBus.GetOptionsByGuid(BusOptions.ConnectPort_GUID);

            if (opPort == null)
            {
                m_InfDev = null;
                bReturn = false;
                m_dwErrCode = LibErrorCode.IDS_ERR_MGR_NULL_PORT_NODE + 1;  //TBD;
                return bReturn;
            }

            SymbolicSearch(m_o2Index, opPort.SelectLocation.Info, ref iIndex);
            //SymbolicSearch(m_o2Index, opPort.itemlist[opPort.listindex], ref iIndex);
			if (iIndex != -1)
			{
				m_InfDev = new CO2USBI2CAdapter();
				bReturn = m_InfDev.OpenDevice(m_USBlinkname, (byte)iIndex);

				//if opened ok
				if (bReturn == true)
				{
                    //(D170206)Francis, move to Constructor
					//NativeMethods.RegisterForDevChange(this.hwndSource.Handle, CO2USBI2CAdapter.GetGuid(), ref gPtrNotifyDevNode);
					//if (hwndSource != null)  //Attaching to window procedure
						//hwndSource.AddHook(new HwndSourceHook(this.hwndSourceHook));
                    //(E170206)
					m_DevBus = opBus;
                    /*
					if (opBus.BusType == BUS_TYPE.BUS_TYPE_I2C)
					{
						//I2CBusOptions newbus = (I2CBusOptions)opBus;
						SetConfigure(BUS_CONFIG.CONFIG_I2C, (UInt16)newbus.SelBusFreq.value);
					}
					else if (opBus.BusType == BUS_TYPE.BUS_TYPE_I2C2)
					{
						//I2C2BusOptions newbus2 = (I2C2BusOptions)opBus;
						SetConfigure(BUS_CONFIG.CONFIG_I2C, (UInt16)newbus2.SelBusFreq.value);
					}
                     * */
                    bReturn = SetConfigure();
				}
				else
				{
					m_InfDev = null;
				}
			}
			else
			{
				m_InfDev = null;
				bReturn = false;
				m_dwErrCode = LibErrorCode.IDS_ERR_MGR_UNABLE_LOAD_DRIVER;
			}
			//second, try to search Aadvark device ,and open it if searching successfully
			//SymbolicSearch(m_aadIndex, opBus.MyDeviceName, ref iIndex);
			//if (iIndex != -1)
			//{
			//}

			return bReturn;
		}

        // <summary>
        // Take input BusOptions to get what user selected by BusOptions.SelectLocation.Info. Take user selected to check m_USBlinkname has
        // this selected string or not. If having in m_USBlinkname, open it. This method is used to open SPI communication type.
        // </summary>
        // <param name="opBus">in/out BusOptions, </param>
        // <returns>True: if found user selected from connected hardware; Flase: cannot found user selected</returns>
        private bool FindOpenO2USB2SPIAdaptor(ref BusOptions opBus)
		{
			bool bReturn = false;
			Int16 iIndex = -1;// = opBus.PortIndex;
            Options opPort = opBus.GetOptionsByGuid(BusOptions.SPIConnectPort_GUID);

			if (bCustomer)
			{
				m_InfDev = null;
				//bReturn = false;
				m_dwErrCode = LibErrorCode.IDS_ERR_MGR_NULL_PORT_NODE;
			}

            if (opPort == null)
            {
                m_InfDev = null;
                //bReturn = false;
                m_dwErrCode = LibErrorCode.IDS_ERR_MGR_NULL_PORT_NODE;
                return bReturn;
            }

            //search O2SPI interface
			SymbolicSearch(m_o2Index, opPort.SelectLocation.Info, ref iIndex);
            //SymbolicSearch(m_o2Index, opPort.itemlist[opPort.listindex], ref iIndex);
			if (iIndex != -1)
			{
				m_InfDev = new CO2USBSPIAdapter();
				bReturn = m_InfDev.OpenDevice(m_USBlinkname, (byte)iIndex);

				//if opened ok
				if (bReturn == true)
                {
                    m_DevBus = opBus;
                    /*
					SPIBusOptions newspi = (SPIBusOptions)opBus;
					byte yspicfg = 0;
					NativeMethods.RegisterForDevChange(this.hwndSource.Handle, CO2USBI2CAdapter.GetGuid(), ref gPtrNotifyDevNode);
					if (hwndSource != null)  //Attaching to window procedure
						hwndSource.AddHook(new HwndSourceHook(this.hwndSourceHook));
					m_DevBus = opBus;
					if (newspi.BitOrder != 0) yspicfg += O2SPI_CONFIG_MSB;
					if (newspi.Phase != 0) yspicfg += O2SPI_CONFIG_PHASE;
					if (newspi.Pol != 0) yspicfg += O2SPI_CONFIG_POLARITY;
					if (newspi.SSPol != 0) yspicfg += O2SPI_CONFIG_SSPOLARITY;
					SetConfigure(BUS_CONFIG.CONFIG_SPI, yspicfg, (ushort)newspi.BaudRate);
                     * */
                    bReturn = SetConfigure();
				}
				else
				{
					m_InfDev = null;
				}
			}
			else
			{
				m_InfDev = null;
				//bReturn = false;
				m_dwErrCode = LibErrorCode.IDS_ERR_MGR_UNABLE_LOAD_DRIVER;
			}
			//second, try to search Aadvark device ,and open it if searching successfully
			//SymbolicSearch(m_aadIndex, opBus.MyDeviceName, ref iIndex);
			//if (iIndex != -1)
			//{
			//}
			return bReturn;
		}

        // <summary>
        // Take input BusOptions to get what user selected by BusOptions.SelectLocation.Info. Take user selected to check m_USBlinkname has
        // this selected string or not. If having in m_USBlinkname, open it. This method is used to find SVID communication type.
        // </summary>
        // <param name="opBus">in/out BusOptions, </param>
        // <returns>True: if found user selected from connected hardware; Flase: cannot found user selected</returns>
        private bool FindOpenO2SVIDMasterBoard(ref BusOptions opBus)
		{
			bool bReturn = false;
			Int16 iIndex = -1;// = opBus.PortIndex;
            Options opPort = opBus.GetOptionsByGuid(BusOptions.ConnectPort_GUID);

            if (opPort == null)
            {
                m_InfDev = null;
                //bReturn = false;
                m_dwErrCode = LibErrorCode.IDS_ERR_MGR_NULL_PORT_NODE;
                return bReturn;
            }

			//try to find index existing in m_SVIDlinkname
			if (m_SVIDlinkname.Count != 0)
			{
				SVIDSymbolSearch(opPort.SelectLocation.Info, ref iIndex);
                //SVIDSymbolSearch(opPort.itemlist[opPort.listindex], ref iIndex);
				if (iIndex != -1)
				{
					m_InfDev = new CO2SVID2I2CMaster();
					bReturn = m_InfDev.OpenDevice(m_SVIDlinkname, (byte)iIndex);

					//if opened ok
					if (bReturn == true)
					{
						/* Don't need, due to COM port is opened when trying to access, instead of always opening its handler
						NativeMethods.RegisterForDevChange(this.hwndSource.Handle, CO2SVID2I2CMaster.GetGuid(), ref gPtrNotifyDevNode);
						if (hwndSource != null)
						{
							hwndSource.AddHook(new HwndSourceHook(this.hwndSourceHook));
						}
						 */
						m_DevBus = opBus;
                        /*
						SVIDBusOptions newsvid = (SVIDBusOptions)opBus;
						SetConfigure(BUS_CONFIG.CONFIG_SVID, (UInt16)newsvid.SelBusFreq.value, 9600, 0);
                        */
                        bReturn = SetConfigure();
					}
					else
					{
						m_InfDev = null;
						//bReturn = false;
						m_dwErrCode = LibErrorCode.IDS_ERR_MGR_UNABLE_LOAD_DRIVER;
					}
				}	//if (iIndex != -1)
			}	//if(m_SVIDlinkname.Count !=0)

			if (iIndex == -1)
			{
				if (m_USBlinkname.Count != 0)
				{
					bReturn = FindOpenO2USB2I2CAdaptor(ref opBus);
				}
				else
				{
					m_InfDev = null;
					bReturn = false;
					m_dwErrCode = LibErrorCode.IDS_ERR_MGR_UNABLE_LOAD_DRIVER;
				}	//if(m_SVIDlinkname.Count !=0)
			}

			return bReturn;
		}

        // <summary>
        // Take input BusOptions to get what user selected by BusOptions.SelectLocation.Info. Take user selected to check m_USBlinkname has
        // this selected string or not. If having in m_USBlinkname, open it. This method is used to find RS232(COM) communication type.
        // </summary>
        // <param name="opBus">in/out BusOptions, </param>
        // <returns>True: if found user selected from connected hardware; Flase: cannot found user selected</returns>
        private bool FindOpenO2RS232Adaptor(ref BusOptions opBus)
		{
			bool bReturn = false;
			//UInt32 ubaudrate = 9600, udata = 8, ustop = 1, uparity = 0;
			Int16 iIndex = -1;// = opBus.PortIndex;
            Options opPort = opBus.GetOptionsByGuid(BusOptions.ConnectPort_GUID);

            if (opPort == null)
            {
                m_InfDev = null;
                //bReturn = false;
                m_dwErrCode = LibErrorCode.IDS_ERR_MGR_NULL_PORT_NODE;
                return bReturn;
            }

			//try to find index existing in m_COMlinkname
			if (m_COMlinkname.Count != 0)
			{
				COMSymbolSearch(opPort.SelectLocation.Info, ref iIndex);
                //COMSymbolSearch(opPort.itemlist[opPort.listindex], ref iIndex);
				if (iIndex != -1)
				{
					m_InfDev = new CO2RS232Master();
					bReturn = m_InfDev.OpenDevice(m_COMlinkname, (byte)iIndex);

					//if opened ok
					if (bReturn == true)
					{
						/* Don't need, due to COM port is opened when trying to access, instead of always opening its handler
						NativeMethods.RegisterForDevChange(this.hwndSource.Handle, CO2SVID2I2CMaster.GetGuid(), ref gPtrNotifyDevNode);
						if (hwndSource != null)
						{
							hwndSource.AddHook(new HwndSourceHook(this.hwndSourceHook));
						}
						 */
						m_DevBus = opBus;
                        /*
						RS232BusOptions newrs232 = (RS232BusOptions)opBus;
						UInt32.TryParse(newrs232.SelBaudRate.value.ToString(), out ubaudrate);
						UInt32.TryParse(newrs232.SelDataBits.value.ToString(), out udata);
						UInt32.TryParse(newrs232.SelStopBit.value.ToString(), out ustop);
						UInt32.TryParse(newrs232.SelParityBit.value.ToString(), out uparity);
						SetConfigure(BUS_CONFIG.CONFIG_RS232, ubaudrate, udata, ustop, uparity);
                         * */
                        bReturn = SetConfigure();
					}
					else
					{
						m_InfDev = null;
						//bReturn = false;
						m_dwErrCode = LibErrorCode.IDS_ERR_MGR_UNABLE_LOAD_DRIVER;
					}
				}	//if (iIndex != -1)
			}	//if(m_COMlinkname.Count !=0)
			else
			{
				m_InfDev = null;
				//bReturn = false;
				m_dwErrCode = LibErrorCode.IDS_ERR_MGR_UNABLE_LOAD_DRIVER;
			}	//if(m_COMlinkname.Count !=0)

			return bReturn;
		}

        private bool InitializeCobraLog()
        {
            bool bReturn = true;

            return bReturn;
        }

        private bool CloseNReCreateLogData()
        {
            bool bReturn = true;

            return bReturn;
        }

        private bool RecordLog(List<string> strLogs)
        {
            bool bReturn = true;

            return bReturn;
        }

		//(M171122)Francis, id=617, in order to export DLL, we make Connect Port and disalbe hook
		private void MakeFakeBusOptions(Byte yType = 0)
		{
			Options OpAdd = null;

			//m_DevBus = new BusOptions();
			//m_DevBus.BusType = BUS_TYPE.BUS_TYPE_I2C;
			//m_DevBus.DeviceIndex = 0;
			//m_DevBus.DeviceIsCheck = true;
			//m_DevBus.Name = "BusOption";
			//m_DevBus.DeviceName = "BusOption";

			OpAdd = new Options();
			if(yType == 0)
				OpAdd.guid = BusOptions.ConnectPort_GUID;
			else
				OpAdd.guid = BusOptions.SPIConnectPort_GUID;
			OpAdd.nickname = "Connect Port:";
			OpAdd.catalog = "Common Configuration";
			OpAdd.maxvalue = 15;
			OpAdd.minvalue = 0;
			OpAdd.format = 1;
			OpAdd.data = 0;
			m_DevBus.optionsList.Add(OpAdd);
			bFromExternal = true;		//(A171122)Francis, id=617, initialization is not calling from external
		}

		#endregion

		#region Public Method

		/// <summary>
		/// Constructor, initialize members, auto-called when instance of class is established 
		/// </summary>
		public CCommunicateManager()
		{
			m_dwErrCode			= LibErrorCode.IDS_ERR_SUCCESSFUL;
			m_InfDev					= null;
			m_DevBus					= new BusOptions();
//			m_supported[m_o2Index] = "O2USB".ToString();
//			m_supported[m_aaIndex] = "Aadvark".ToString();
            //(A170206)Francis, register hook at beginning
            NativeMethods.RegisterForDevChange(this.hwndSource.Handle, CO2USBI2CAdapter.GetGuid(), ref gPtrNotifyDevNode);
            if (hwndSource != null)  //Attaching to window procedure
                hwndSource.AddHook(new HwndSourceHook(this.hwndSourceHook));
            //(E170206)
		}

		/// <summary>
		/// Destructor, release memory, auto-called when instance is expired 
		/// </summary>
		~CCommunicateManager()
		{
			if (gPtrNotifyDevNode != null)
			{
				NativeMethods.UnregisterDeviceNotification(gPtrNotifyDevNode);
			}
			if (m_InfDev != null)
			{
				m_InfDev.CloseDevice();
				m_InfDev = null;
			}
		}

		/// <summary>
		/// Find all connected interface device. Firstly, method will try to find O2USB by O2USB GUID, secodly will try to find Aadvark by Aadvark
		/// supported API, then save fully symbolic name in m_USBlinkname if found any. After found any, method will set up
		/// opBus.ConnectedDevice, that is binding in BusOptionWindow, and show up the nick name of connected devices. Shell will use
		/// this nick name as unique name for recogniation.
		/// </summary>
		/// <param name="opBus">reference parameter, BusOptions type parameter, declare in Common Layer, opBus.SelInterfaceType saved interface type, opBus.ConnectedDevice device string if Found connected devices.</param>
		/// <param name="bForce">input, default is true, if bForce == true, method will re-enumerate connected devices, then set up unique UI string again.</param>
		/// <returns>ture: if found successfully, and fill up opBus.ConnectedDevice. false: if founded failed</returns>
		public bool FindDevices(ref BusOptions opBus, bool bForce = true)
		{
			bool bReturn = true;
			Int16 iNumber = -1;
            Options port = null;

			if (opBus != null)
			{
				if (opBus.BusType != BUS_TYPE.BUS_TYPE_SPI)
				{
					port = opBus.GetOptionsByGuid(BusOptions.ConnectPort_GUID);
				}
				else
				{
					port = opBus.GetOptionsByGuid(BusOptions.SPIConnectPort_GUID);
				}
			}

            if (port == null)
            {
                m_InfDev = null;
                bReturn = false;
                m_dwErrCode = LibErrorCode.IDS_ERR_MGR_NULL_PORT_NODE - 1;
                return bReturn;
            }
            else
            {
                m_DevBus = opBus;       //(A170206)Francis, save it if calling this by DEM
            }

            //port.itemlist.Clear();
            //port.itemlist.Add("Disconnected");
            PortStringSelected = string.Format("{0}",port.SelectLocation.Info);
            port.LocationSource.Clear();
            ComboboxRoad cRoad = new ComboboxRoad();
            cRoad.ID = 0;
            cRoad.Info = "Disconnected";
            port.LocationSource.Add(cRoad);
            port.SelectLocation = cRoad;
            //port.listindex = 0;

			if (bForce)
			{
				m_USBlinkname.Clear();
				m_SVIDlinkname.Clear();
				m_COMlinkname.Clear();

				#region find connected O2USBtoI2C adaptor
				if ((opBus.BusType == BUS_TYPE.BUS_TYPE_I2C) || (opBus.BusType == BUS_TYPE.BUS_TYPE_I2C2) ||
					(opBus.BusType == BUS_TYPE.BUS_TYPE_SPI) || (opBus.BusType == BUS_TYPE.BUS_TYPE_SVID))
				{
					bReturn &= CInterfaceBase.FindO2USBDevice(ref iNumber, ref m_dwErrCode, ref m_USBlinkname);
					if (bReturn)
					{
						ConvertSymbolnameToUIPort(CO2USBI2CAdapter.GetGuid(), ref opBus, m_o2Index);
						m_dwErrCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
					}
					else
					{
						m_dwErrCode = LibErrorCode.IDS_ERR_MGR_UNABLE_FIND_DEVICE;
					}
				}
				#endregion

				#region find connected O2 SVID master board
				//(A141017)Francis, find SVID master board
				if ((opBus.BusType == BUS_TYPE.BUS_TYPE_SVID))// || (opBus.BusType == BUS_TYPE.BUS_TYPE_RS232))
				{
					bReturn &= CInterfaceBase.FindSVIDMasterDevice(ref iNumber, ref m_dwErrCode, ref m_SVIDlinkname);
					if (bReturn)
					{
						ConvertSVIDSymbolToUIPort(ref opBus);
						m_dwErrCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
					}
					else
					{
						m_dwErrCode = LibErrorCode.IDS_ERR_MGR_UNABLE_FIND_DEVICE;
					}
				}
				//(E141017)
				#endregion

				#region find available COM port
				//(A150416)Francis, find SVID master board
				if (opBus.BusType == BUS_TYPE.BUS_TYPE_RS232)
				{
					bReturn &= CInterfaceBase.FindRS232Device(ref iNumber, ref m_dwErrCode, ref m_COMlinkname);
					if (bReturn)
					{
						ConvertComSymbolToUIPort(ref opBus);
						m_dwErrCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
					}
					else
					{
						m_dwErrCode = LibErrorCode.IDS_ERR_MGR_UNABLE_FIND_DEVICE;
					}
				}
				//(E150416)
				#endregion

				#region find connected Aadvark adaptor
				if ((opBus.BusType == BUS_TYPE.BUS_TYPE_I2C) || (opBus.BusType == BUS_TYPE.BUS_TYPE_I2C2) ||
					(opBus.BusType == BUS_TYPE.BUS_TYPE_SPI))
				{
					bReturn |= CInterfaceBase.FindAAUSBDevice(ref iNumber, ref m_dwErrCode, ref m_USBlinkname);
					if (bReturn)
					{
						//ConvertComSymbolToUIPort(ref opBus);
						m_dwErrCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
					}
					else
					{
						m_dwErrCode = LibErrorCode.IDS_ERR_MGR_UNABLE_FIND_DEVICE;
					}
				}
				#endregion
			}
			else		//if(bForce)
			{
				bReturn = true;
			}

			//below will be Aardvark find device
			//bReturn |= CInterfaceBase.FindAardvarkDevice();						//suppose to support Aardvark
			//opBus.PortTotalNum += iNumber;

			return bReturn;
		}

		/// <summary>
		/// Open Hardware interface device by specified device type, note that opBus parameter must be set .ButType, .InterfaceType, and .PortIndex members before using function.
		/// </summary>
		/// <param name="opBus">reference parameter, BusOptions type parameter, declare in Common Layer, opBus.BusType saved bus type, opBus.InterfaceType saved interface manufacture type, opBus.PortIndex saved index number of device to open.</param>
		/// <returns>ture: if opened successfully, and fill up opBus.PortTotalNum. false: if opened failed</returns>
		public bool OpenDevice(ref BusOptions opBus)
		{
			bool bReturn = true;

			//(M141104)Francis, one of linkname is empty, find devices
			//if ((m_USBlinkname.Count == 0) || (m_SVIDlinkname.Count == 0))
			//if ((opBus.BusType == BUS_TYPE.BUS_TYPE_SVID) && (m_SVIDlinkname.Count == 0))
			//(M121211)Francis, SVID BusType can use SVID master board or O2USBtoI2C board
			if ((opBus.BusType == BUS_TYPE.BUS_TYPE_SVID) && ((m_SVIDlinkname.Count == 0) && (m_USBlinkname.Count == 0)))
			{
				bReturn = FindDevices(ref opBus);
			}
			else if (((opBus.BusType == BUS_TYPE.BUS_TYPE_I2C) || (opBus.BusType == BUS_TYPE.BUS_TYPE_I2C2) ||
						(opBus.BusType == BUS_TYPE.BUS_TYPE_SPI) )
						&& (m_USBlinkname.Count == 0))
			{
				bReturn = FindDevices(ref opBus);
			}
			else if ((opBus.BusType == BUS_TYPE.BUS_TYPE_RS232) && (m_COMlinkname.Count == 0))
			{
				bReturn = FindDevices(ref opBus);
			}

			if (!bReturn)
			{
				return bReturn;
			}
			else
			{
				CloseDevice();
			}

			if ((opBus.BusType == BUS_TYPE.BUS_TYPE_I2C) || (opBus.BusType == BUS_TYPE.BUS_TYPE_I2C2))
			{
				bReturn = FindOpenO2USB2I2CAdaptor(ref opBus);
			}	//if ((opBus.BusType == BUS_TYPE.BUS_TYPE_I2C)||(opBus.BusType == BUS_TYPE.BUS_TYPE_I2C2))
			else if (opBus.BusType == BUS_TYPE.BUS_TYPE_SPI)
			{
				bReturn = FindOpenO2USB2SPIAdaptor(ref opBus);
			}	//else if(opBus.BusType == BUS_TYPE.BUS_TYPE_SPI)
			//(A141021)Francis,
			else if (opBus.BusType == BUS_TYPE.BUS_TYPE_SVID)
			{
				bReturn = FindOpenO2SVIDMasterBoard(ref opBus);
			}	//if (opBus.BusType == BUS_TYPE.BUS_TYPE_SVID)
			//(E141021)
			//(A150416)Francis
			else if (opBus.BusType == BUS_TYPE.BUS_TYPE_RS232)
			{
				bReturn = FindOpenO2RS232Adaptor(ref opBus);
			}
			//(E150416)
			else
			{
				m_dwErrCode = LibErrorCode.IDS_ERR_MGR_INVALID_INTERFACE_TYPE ;
				bReturn = false;
			}
			if (m_InfDev != null)
			{
				m_InfDev.m_busopDev = opBus;
				m_InfDev.m_busopDev.db_Manager.NewSession("COM", ref m_InfDev.session_id, DateTime.Now.ToString());
			}
			return bReturn;
		}

		/// <summary>
		/// Open Hardware interface device by specified device type, note that opBus parameter must be set .ButType, .InterfaceType, and .PortIndex members before using function.
		/// </summary>
		public bool OpenI2CAdapter()
		{
            Options opPort = null;
            bool bReturn = false;

			//(M171122)Francis, id=617, in order to export DLL, we make Connect Port and disable hook
            if (m_DevBus.optionsList.Count == 0)
            {
				MakeFakeBusOptions();
            }
			if (FindDevices(ref m_DevBus))
			{
				//m_DevBus.BusType = BUS_TYPE.BUS_TYPE_I2C;
                opPort = m_DevBus.GetOptionsByGuid(BusOptions.ConnectPort_GUID);
                if(opPort !=null)
				{

					//m_DevBus.TmpConnectPort.Info = m_DevBus.ConnectPort[1].Info;
                    //if(opPort.itemlist.Count > 1)   //index 0 is "disconnected"
				}
                else
                {
                   // opPort = new Options();

                    //opPort.guid = BusOptions.ConnectPort_GUID;
                    //opPort.bedit = true;
                    //opPort.berror = false;
                    //opPort.brange = true;
                    //opPort.sdevicename = m_DevBus.DeviceName;
                    //m_dwErrCode = LibErrorCode.IDS_ERR_MGR_UNABLE_FIND_DEVICE;
                }
				if (opPort.LocationSource.Count > 1)
				{
					bReturn = OpenDevice(ref m_DevBus);
				}
				else
				{
					m_dwErrCode = LibErrorCode.IDS_ERR_MGR_UNABLE_FIND_DEVICE;
				}
			}

            return bReturn;
		}

        /// <summary>
        /// Open Hardware interface device by specified device type, note that opBus parameter must be set .ButType, .InterfaceType, and .PortIndex members before using function.
        /// </summary>
        public bool OpenSPIAdapter()
        {
            Options opPort = null;
            bool bReturn = false;

			m_DevBus.BusType = BUS_TYPE.BUS_TYPE_SPI;
			//(M171122)Francis, id=617, in order to export DLL, we make Connect Port and disalbe hook
			if (m_DevBus.optionsList.Count == 0)
			{
				MakeFakeBusOptions(1);
			}
			if (FindDevices(ref m_DevBus))
            {
                //m_DevBus.BusType = BUS_TYPE.BUS_TYPE_SPI;
                opPort = m_DevBus.GetOptionsByGuid(BusOptions.SPIConnectPort_GUID);
                if (opPort != null)
                {
                    if (opPort.LocationSource.Count > 1)
                    {
                        bReturn = OpenDevice(ref m_DevBus);
                    }
					else
					{
						m_dwErrCode = LibErrorCode.IDS_ERR_MGR_UNABLE_FIND_DEVICE;
					}
				}
                else
                {
                    m_dwErrCode = LibErrorCode.IDS_ERR_MGR_UNABLE_FIND_DEVICE;
                }
            }

            return bReturn;
        }

		/// <summary>
		/// Close Device
		/// </summary>
		/// <returns>ture: if close successfully; otherwise false.</returns>
		public bool CloseDevice()
		{
			bool bReturn = true;

			m_dwErrCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
			if (m_InfDev != null)
			{
				bReturn = m_InfDev.CloseDevice();
				m_InfDev = null;
				//m_myHWType = HWInterfaceType.HW_Disconnected;
				if (bReturn != true)
				{
					m_dwErrCode = LibErrorCode.IDS_ERR_MGR_INVALID_INTERFACE_HANDLER;
				}
//				else
//				{
//					m_dwErrCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
//				}
//			}
//			else
//			{
//				bReturn = true;
//				m_dwErrCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
			}

			return bReturn;
		}

		/// <summary>
		/// Get Version in String Format
		/// </summary>
		/// <param name="strCommVer">save Version by string format, in "[Major].[Middle].[Minor]" format</param>
		public void GetVersion(ref string strCommVer)
		{
			strCommVer = m_MajorVer.ToString() + "." +
										m_MiddleVer.ToString() + "."  +
										m_MinorVer.ToString() + "."  +
										m_TestVer.ToString(); 
		}

		/// <summary>
		/// Get Version in DWORD Format
		/// </summary>
		/// <param name="dwCommVer">save Version by UInt32 format, in ([Major] shiftleft 24) + ([Middle] shiftleft 16) + ([Minor] shiftleft 8) + [Test] </param>
		public void GetVersion(ref UInt32 dwCommVer)
		{
			dwCommVer = (UInt32)m_MajorVer;
			dwCommVer <<= 8;
			dwCommVer += (UInt32)m_MiddleVer;
			dwCommVer <<= 8;
			dwCommVer += (UInt32)m_MinorVer;
			dwCommVer <<= 8;
			dwCommVer += (UInt32)m_TestVer;
		}

		/// <summary>
		/// Read data from Device, do ReadByte command if wNumofRDByte = 1, 
		/// do ReadWord if wNumofRDByte = 2, and do ReadBlock if wNumofRDByte >= 3. If wNumofRDByte = 0, will return false
		/// </summary>
		/// <param name="yDataIn">Data buffer in</param>
		/// <param name="yDataOut">Data buffer out, if there is any data should be paased out</param>
		/// <param name="wDataOutLength">Indicate Length of Out Data</param>
		/// <param name="wDataInLength">Indicate Length of In Data, default is 1, ReadByte</param>
		/// <returns></returns>
		public bool ReadDevice(byte[] yDataIn, ref byte[] yDataOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 1, UInt16 wDataInWrite = 1)
		{
			bool bReturn = true;

			if (m_InfDev != null)
			{
				bReturn &= m_InfDev.ReadDevice(ref yDataIn, ref yDataOut, ref wDataOutLength, wDataInLength, wDataInWrite);
                /*bReturn &= */m_InfDev.WriteDataToLog(yDataIn, yDataOut, wDataOutLength, wDataInLength, 1);
			}
			else
			{
				bReturn = false;
				m_dwErrCode = LibErrorCode.IDS_ERR_MGR_INVALID_INTERFACE_HANDLER;
			}

			return bReturn;
		}

		/// <summary>
		/// Write data to Device, do WriteByte command if wNumofWRByte = 1, 
		/// do WriteWord if wNumofWRByte = 2, and do WriteBlock if wNumofWRByte >= 3. If wNumofRDByte = 0, will return false
		/// </summary>
		/// <param name="yDataIn">Data buffer in</param>
		/// <param name="yDataOut">Data buffer out, if there is any data should be paased out</param>
		/// <param name="wDataOutLength">Indicate Length of Out Data</param>
		/// <param name="wDataInLength">Indicate Length of In Data, default is 1, WriteByte</param>
		/// <returns></returns>
		public bool WriteDevice(byte[] yDataIn, ref byte[] yDataOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 1)
		{
			bool bReturn = true;

			if (m_InfDev != null)
			{
				bReturn = m_InfDev.WriteDevice(ref yDataIn, ref yDataOut, ref wDataOutLength, wDataInLength);
                /*bReturn &= */m_InfDev.WriteDataToLog(yDataIn, yDataOut, wDataOutLength, wDataInLength, 0);
			}
			else
			{
				bReturn = false;
				m_dwErrCode = LibErrorCode.IDS_ERR_MGR_INVALID_INTERFACE_HANDLER;
			}

			return bReturn;
		}

		/// <summary>
		/// Reset hardware interface devcie
		/// </summary>
		/// <returns>true: reset command successful; false: reset command failed</returns>
		public bool ResetInterface()
		{
			bool bReturn = true;

			if (m_InfDev != null)
			{
				bReturn = m_InfDev.ResetInf();
			}
			else
			{
				return false;
			}

			if (bReturn)
			{
				m_dwErrCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
			}
			else
			{
				m_dwErrCode = m_InfDev.ErrorCode;
			}

			return bReturn;
		}

		//(M171122)Francis, id=79, in order to export DLL, error description is able to return string and external user can use it to show
		/// <summary>
		/// Get Error code of last operation.
		/// </summary>
		/// <param name="dwErrorCode">out: error code</param>
		/// If HW handler is not null, sync error code with m_dwErrCode
		public void GetLastErrorCode(ref UInt32 dwErrorCode)
		{
			//if Device Handler, sync error code with handler
			if (m_InfDev != null)
			{
				m_dwErrCode = m_InfDev.ErrorCode;
			}

			//set error code up output buffer
			dwErrorCode = m_dwErrCode;
		}

		/// <summary>
		/// Get Error code value and description.
		/// </summary>
		/// <param name="dwErrorCode">out: error code</param>
		/// <param name="strErrorDescription">out: error description</param>
		/// If HW handler is not null, sync error code with m_dwErrCode
		public string GetLastErrorDescrip(ref UInt32 dwErrorCode)
		{
			//if Device Handler, sync error code with handler
			if (m_InfDev != null)
			{
				m_dwErrCode = m_InfDev.ErrorCode;
			}

			//set error code up output buffer
			dwErrorCode = m_dwErrCode;
			return LibErrorCode.GetErrorDescription(m_dwErrCode);
		}
		//(E171122)

        /// <summary>
        /// Set Bus Configure, according to m_DevBus 
        /// </summary>
        /// <returns>true, if setup is successful; otherwise return false</returns>
        public bool SetConfigure()
        {
            bool bReturn = false;
            Options opTmp = null;

            if((m_DevBus.BusType == BUS_TYPE.BUS_TYPE_I2C) || (m_DevBus.BusType == BUS_TYPE.BUS_TYPE_I2C2))
            {
                opTmp = m_DevBus.GetOptionsByGuid(BusOptions.I2CFrequency_GUID);
                if(opTmp != null)
                {
                    UInt32 wfre;
                    UInt32.TryParse(opTmp.sphydata, out wfre);
                    bReturn = SetI2CConfigure(wfre);
                }
                SetO2I2CDelayTime();
            }
            else if(m_DevBus.BusType == BUS_TYPE.BUS_TYPE_SPI)
            {
                byte yPolarity = 0xFF;
                byte yPhase = 0xFF;
                byte yBitOrder = 0xFF;
                byte ySSPolarity = 0xFF;
                byte yWire = 0;
                UInt32 wBaudRate = 0;
                opTmp = m_DevBus.GetOptionsByGuid(BusOptions.SPIPolarity_GUID);
                if(opTmp != null)
                {
                   //byte.TryParse(opTmp.sphydata, out yPolarity); 取Combobox索引，0：Low 1:High
                    yPolarity = (byte)opTmp.data;
                }
                opTmp = m_DevBus.GetOptionsByGuid(BusOptions.SPIPhase_GUID);
                if(opTmp != null)
                {
                    //byte.TryParse(opTmp.sphydata, out yPhase);
                    yPhase = (byte)opTmp.data;
                }
                opTmp = m_DevBus.GetOptionsByGuid(BusOptions.SPIBitOrder_GUID);
                if(opTmp != null)
                {
                    //byte.TryParse(opTmp.sphydata, out yBitOrder);
                    yBitOrder = (byte)opTmp.data;
                }
                opTmp = m_DevBus.GetOptionsByGuid(BusOptions.SPISSPolarity_GUID);
                if(opTmp != null)
                {
                    //byte.TryParse(opTmp.sphydata, out ySSPolarity);
                    ySSPolarity = (byte)opTmp.data;
                }
                opTmp = m_DevBus.GetOptionsByGuid(BusOptions.SPIWire_GUID);
                if(opTmp != null)
                {
                    //byte.TryParse(opTmp.sphydata, out yWire);
                    yWire = (byte)opTmp.data;
                }
                opTmp = m_DevBus.GetOptionsByGuid(BusOptions.SPIBaudRate_GUID);
                if(opTmp != null)
                {
                    UInt32.TryParse(opTmp.sphydata, out wBaudRate);
                }
                //if ((yPolarity != 0xFF) && (yPhase != 0xFF) && (yBitOrder != 0xFF) && (ySSPolarity != 0xFF) && (yWire != 0xFF))
                if ((yPolarity != 0xFF) && (yPhase != 0xFF) && (yBitOrder != 0xFF) && (ySSPolarity != 0xFF) && (wBaudRate != 0))
                {
                    //bReturn = SetSPIConfigure(yPolarity, yPhase, yBitOrder, ySSPolarity, yWire);
                    bReturn = SetSPIConfigure(wBaudRate, yPolarity, yPhase, yBitOrder, ySSPolarity, yWire);
                }
            }
            else if(m_DevBus.BusType == BUS_TYPE.BUS_TYPE_SVID)
            {
                UInt32 wfre = 0;
                UInt32 wBaudRate = 0;
                UInt32 wDataBit = 0;
                UInt32 wStopBit = 0;
                UInt32 wParity = 0;
                opTmp = m_DevBus.GetOptionsByGuid(BusOptions.I2CFrequency_GUID);
                if (opTmp != null)
                {
                    UInt32.TryParse(opTmp.sphydata, out wfre);
                }
                opTmp = m_DevBus.GetOptionsByGuid(BusOptions.SVIDBaudRate_GUID);
                if(opTmp != null)
                {
                    UInt32.TryParse(opTmp.sphydata, out wBaudRate);
                }
                opTmp = m_DevBus.GetOptionsByGuid(BusOptions.SVIDDataBits_GUID);
                if(opTmp != null)
                {
                    UInt32.TryParse(opTmp.sphydata, out wDataBit);
                }
                opTmp = m_DevBus.GetOptionsByGuid(BusOptions.SVIDStopbit_GUID);
                if(opTmp != null)
                {
                    UInt32.TryParse(opTmp.sphydata, out wStopBit);
                }
                opTmp = m_DevBus.GetOptionsByGuid(BusOptions.SVIDParity_GUID);
                {
                    UInt32.TryParse(opTmp.sphydata, out wParity);
                }
                if((wfre != 0) && (wBaudRate != 0))
                {
                    bReturn = SetSVIDConfigure(wfre, wBaudRate, wParity, wDataBit, wStopBit);
                }
            }
            else if(m_DevBus.BusType == BUS_TYPE.BUS_TYPE_RS232)
            {
                UInt32 wBaudRate = 0;
                UInt32 wDataBit = 0;
                UInt32 wStopBit = 0;
                UInt32 wParity = 0;
                opTmp = m_DevBus.GetOptionsByGuid(BusOptions.RS232BaudRate_GUID);
                if (opTmp != null)
                {
                    UInt32.TryParse(opTmp.sphydata, out wBaudRate);
                }
                opTmp = m_DevBus.GetOptionsByGuid(BusOptions.RS232DataBits_GUID);
                if (opTmp != null)
                {
                    UInt32.TryParse(opTmp.sphydata, out wDataBit);
                }
                opTmp = m_DevBus.GetOptionsByGuid(BusOptions.RS232Stopbit_GUID);
                if (opTmp != null)
                {
                    wStopBit = Convert.ToUInt32(opTmp.SelectLocation.ID);
                }
                opTmp = m_DevBus.GetOptionsByGuid(BusOptions.RS232Parity_GUID);
                {
                    wParity = Convert.ToUInt32(opTmp.SelectLocation.ID);
                }
                if ((wBaudRate != 0))
                {
                    bReturn = SetRS232Configure(wBaudRate, wDataBit, wStopBit, wParity);
                }
            }

            return bReturn;
        }

		/// <summary>
		/// Set up frequency of I2C bus configuration, this function is only vaild when creating with I2C interface.
		/// Function will return false and generate error code, if creating SPI interface and using this function.
		/// </summary>
		/// <param name="busI2C">input value, it must be BUS_CONFIG.CONFIG_I2C</param>
		/// <param name="wI2CFrequence">Input value, the frequency value in k format.</param>
		/// <returns>true, if setup is successful; otherwise return false</returns>
		public bool SetConfigure(BUS_CONFIG busI2C, UInt32 wI2CFrequence)
		{
			bool bRet = false;
			List<UInt32> wList = new List<UInt32>();

			if (m_InfDev != null)
			{
				if (((m_DevBus.BusType == BUS_TYPE.BUS_TYPE_I2C) || (m_DevBus.BusType == BUS_TYPE.BUS_TYPE_I2C2) ||
					(m_DevBus.BusType == BUS_TYPE.BUS_TYPE_SVID))
					&& 	(busI2C == BUS_CONFIG.CONFIG_I2C))
				{
					wList.Clear();
					wList.Add(wI2CFrequence);
					//bRet = m_InfDev.SetConfigure(wI2CFrequence);
					bRet = m_InfDev.SetConfigure(wList);
				}
				else
				{
					m_dwErrCode = LibErrorCode.IDS_ERR_I2C_CFG_INVALID_CONFIG_TYPE;
				}
			}
			else
			{
				m_dwErrCode = LibErrorCode.IDS_ERR_MGR_INVALID_INTERFACE_HANDLER;
			}

			return bRet;
		}

		/// <summary>
		/// Set up frequency of I2C bus configuration, this function is only vaild when creating with I2C interface.
		/// Function will return false and generate error code, if creating SPI interface and using this function.
		/// </summary>
		/// <param name="wI2CFrequence">Input value, the frequency value in k format.</param>
		/// <returns>true, if setup is successful; otherwise return false</returns>
		public bool SetI2CConfigure(UInt32 wI2CFrequence)
		{
			return SetConfigure(BUS_CONFIG.CONFIG_I2C, wI2CFrequence);
		}

		/// <summary>
		/// Set up configuration and BaudRate of SPI bus configuration, this function is only vaild when creating with SPI interface.
		/// Function will return false and generate error code, if creating I2C interface and using this function.
		/// </summary>
		/// <param name="busSPI">input value, it must be BUS_CONFIG.CONFIG_SPI</param>
		/// <param name="ySPIConfig">input value, configure value of SPI bus, please refer to definition</param>
		/// <param name="wSPIRate">input value, the baud rate value in k format</param>
		/// <returns>true, if setup is successful; otherwise return false</returns>
		public bool SetConfigure(BUS_CONFIG busSPI, byte ySPIConfig, UInt32 wSPIRate = 1000)
		{
			bool bRet = false;
			List<UInt32> wList = new List<UInt32>();

			if (bCustomer)
			{
				m_dwErrCode = LibErrorCode.IDS_ERR_SPI_CFG_INVALID_CONFIG_TYPE;
				return bRet;
			}

			if (m_InfDev != null)
			{
				if ((m_DevBus.BusType == BUS_TYPE.BUS_TYPE_SPI) &&
					(busSPI == BUS_CONFIG.CONFIG_SPI))
				{
					wList.Clear();
					ySPIConfig |= O2SPI_CONFIG_UNKNOW + O2SPI_CONFIG_4WIRE;	//force 0x40 + 0x20 set
					wList.Add((UInt32)ySPIConfig);
					wList.Add(wSPIRate);
					//bRet = m_InfDev.SetConfigure(ySPIConfig, wSPIRate);
					bRet = m_InfDev.SetConfigure(wList);
				}
				else
				{
					m_dwErrCode = LibErrorCode.IDS_ERR_SPI_CFG_INVALID_CONFIG_TYPE;
				}
			}
			else
			{
				m_dwErrCode = LibErrorCode.IDS_ERR_MGR_INVALID_INTERFACE_HANDLER;
			}

			return bRet;
		}

		/// <summary>
		/// Special function of SPI setup configuration, this function is only vaild when creating with SPI interface.
		/// Function will return false and generate error code, if creating I2C interface and using this function.
		/// Note that this function will force adapter as Master mode
		/// </summary>
		/// <param name="ySPIPolariy">input value, if not zero, it will set up as High Active of Polarity; otherwise is Low Active</param>
		/// <param name="ySPIPhase">input value, if not zero, it will set up as High Active of Phase; otherwise is Low Active</param>
		/// <param name="ySPIBitOrder">input value, if not zero, it will set up as MSB; otherwise is LSB</param>
		/// <param name="ySPISSPolarity">input value, if not zero, it will set up Slave Polarity as High Active; otherwise is Low Active</param>
		/// <param name="ySPIWire">input value, if not zero, it will set up as 4-wires; otherwise is 3-wires</param>
		/// <returns>true, if setup is successful; otherwise return false</returns>
		public bool SetSPIConfigure(UInt32 wSPIBaudRate, byte ySPIPolariy, byte ySPIPhase, byte ySPIBitOrder, byte ySPISSPolarity, byte ySPIWire)
		{
            bool bRet = true;

			//if (bCustomer)
			//{
				//m_dwErrCode = LibErrorCode.IDS_ERR_SPI_CFG_INVALID_CONFIG_TYPE;
				//return false;
			//}

			byte ySPICofig = O2SPI_CONFIG_UNKNOW + O2SPI_CONFIG_MASTER; 

			if (ySPIPolariy != 0) ySPICofig |= O2SPI_CONFIG_POLARITY;
            else ySPICofig &= (byte)Convert.ToInt16(~O2SPI_CONFIG_POLARITY);

			if (ySPIPhase != 0) ySPICofig |= O2SPI_CONFIG_PHASE;
            else ySPICofig &= (byte)Convert.ToInt16(~O2SPI_CONFIG_PHASE);

			if (ySPIBitOrder != 0) ySPICofig |= O2SPI_CONFIG_MSB;
            else ySPICofig &= (byte)Convert.ToInt16(~O2SPI_CONFIG_MSB);

			if (ySPISSPolarity != 0) ySPICofig |= O2SPI_CONFIG_SSPOLARITY;
            else ySPICofig &= (byte)Convert.ToInt16(~O2SPI_CONFIG_SSPOLARITY);

			if (ySPIWire != 0) ySPICofig |= O2SPI_CONFIG_4WIRE;
            else ySPICofig &= (byte)Convert.ToInt16(~O2SPI_CONFIG_4WIRE);

			bRet &= SetConfigure(BUS_CONFIG.CONFIG_SPI, ySPICofig);
            bRet &= SetSPIBaudRate(wSPIBaudRate);

            return bRet;
		}

		/// <summary>
		/// Special function of SPI baud rate setup, this function is only valid when creating with SPI interface.
		/// Function will return fasle and generate error code, if creating I2C interface and using this function.
		/// </summary>
		/// <param name="wSPIBaudRate">input value, indicates baud rate in k format</param>
		/// <returns>true, if setup is successful; otherwise return false</returns>
		public bool SetSPIBaudRate(UInt32 wSPIBaudRate)
		{
			bool bRet = false;
			byte ySPIcfg = O2SPI_CONFIG_UNKNOW;
			UInt32 wSPIRate = 1000;

			if (bCustomer)
			{
				m_dwErrCode = LibErrorCode.IDS_ERR_SPI_CFG_INVALID_CONFIG_TYPE;
				return bRet;
			}

			bRet = GetConfigure(BUS_CONFIG.CONFIG_SPI, ref ySPIcfg, ref wSPIRate);
			if (bRet)
			{
				bRet = SetConfigure(BUS_CONFIG.CONFIG_SPI, ySPIcfg, wSPIBaudRate);
			}

			return bRet;
		}

		/// <summary>
		/// Set baudrate and configuration of COM port and frequency of I2C bus, this function is only vaild when creating with SVID interface.
		/// Function will return false and generate error code, if creating no SVID interface and using this function.
		/// </summary>
		/// <param name="busSVID">input value, it must be BUS_CONFIG.CONFIG_SVID</param>
		/// <param name="wI2CFrequence">input value, baud rate value of COM port</param>
		/// <param name="wBaudRate">input value, baud rate value of COM port</param>
		/// <param name="pParity">input value, one of System.IO.Ports.Parity enum value</param>
		/// <param name="wDataBits">input value, data bits setting of COM port</param>
		/// <param name="wStopbit">input value, stop bit setting of COM port</param>
		/// <returns>true, if setup is successful; otherwise return false</returns>
		public bool SetConfigure(BUS_CONFIG busSVID,
													UInt32 wI2CFrequence,
													UInt32 wBaudRate,
													UInt32 wParity,
													UInt32 wDataBits = 8,
													UInt32 wStopbit = 1)
		{
			bool bRet = false;
			List<UInt32> wList = new List<UInt32>();

			if (m_InfDev != null)
			{
				if ((m_DevBus.BusType == BUS_TYPE.BUS_TYPE_SVID) && (busSVID == BUS_CONFIG.CONFIG_SVID))
				{
					wList.Clear();
					wList.Add(wI2CFrequence);
					wList.Add(wBaudRate);
					wList.Add(wParity);
					wList.Add(wDataBits);
					wList.Add(wStopbit);
					bRet = m_InfDev.SetConfigure(wList);
				}
				else
				{
					m_dwErrCode = LibErrorCode.IDS_ERR_MGR_CONFIG_SVID_BUSTYPE;
				}
			}
			else
			{
				m_dwErrCode = LibErrorCode.IDS_ERR_MGR_INVALID_INTERFACE_HANDLER;
			}

			return bRet;
		}

		/// <summary>
		/// Set baudrate and configuration of COM port and frequency of I2C bus, this function is only vaild when creating with SVID interface.
		/// Function will return false and generate error code, if creating no SVID interface and using this function.
		/// </summary>
		/// <param name="wBaudRate">output value, baud rate value of COM port</param>
		/// <param name="pParity">output value, one of System.IO.Ports.Parity enum value</param>
		/// <param name="wDataBits">output value, data bits setting of COM port</param>
		/// <param name="wStopbit">output value, stop bit setting of COM port</param>
		/// <returns>true, if setup is successful; otherwise return false</returns>
		public bool SetSVIDConfigure(UInt32 wI2CFrequence, 
															UInt32 wBaudRate,
															UInt32 wParity,
															UInt32 wDataBits = 8,
															UInt32 wStopbit = 1)
		{
			return SetConfigure(BUS_CONFIG.CONFIG_SVID, wI2CFrequence, wBaudRate, wParity, wDataBits, wStopbit);
		}


		public bool SetConfigure(BUS_CONFIG busRS232,
													UInt32 wBaudRate,
													UInt32 wDataBits,
													UInt32 wStopbit,
													UInt32 wParity)
		{
			bool bRet = false;
			List<UInt32> wList = new List<UInt32>();

			if (m_InfDev != null)
			{
				if ((m_DevBus.BusType == BUS_TYPE.BUS_TYPE_RS232) && (busRS232 == BUS_CONFIG.CONFIG_RS232))
				{
					wList.Clear();
					wList.Add(wBaudRate);
					wList.Add(wDataBits);
					wList.Add(wStopbit);
					wList.Add(wParity);
					bRet = m_InfDev.SetConfigure(wList);
				}
				else
				{
					m_dwErrCode = LibErrorCode.IDS_ERR_MGR_CONFIG_SVID_BUSTYPE;		//TBD
				}
			}
			else
			{
				m_dwErrCode = LibErrorCode.IDS_ERR_MGR_INVALID_INTERFACE_HANDLER;
			}

			return bRet;
		}

		public bool SetRS232Configure(UInt32 wBaudRate,
															UInt32 wDataBits,
															UInt32 wStopbit,
															UInt32 wParity)
		{
			return SetConfigure(BUS_CONFIG.CONFIG_RS232, wBaudRate, wDataBits, wStopbit, wParity);
		}


		/// <summary>
		/// Get frequency of I2C bus configuration, this function is only vaild when creating with I2C interface.
		/// Function will return false and generate error code, if creating SPI interface and using this function.
		/// </summary>
		/// <param name="busI2C">input value, it must be BUS_CONFIG.CONFIG_I2C</param>
		/// <param name="wI2CFrequence">output value, the frequency value in k format.</param>
		/// <returns>true, if setup is successful; otherwise return false</returns>
		/// <returns></returns>
		public bool GetConfigure(BUS_CONFIG busI2C, ref UInt32 wI2CFrequence)
		{
			bool bRet = false;
			List<UInt32> wList = new List<UInt32>();

			if (m_InfDev != null)
			{
				if (((m_DevBus.BusType == BUS_TYPE.BUS_TYPE_I2C) || (m_DevBus.BusType == BUS_TYPE.BUS_TYPE_I2C))
					&& (busI2C == BUS_CONFIG.CONFIG_I2C))
				{
					wList.Clear();
					wList.Add(0);
					//bRet = m_InfDev.GetConfigure(ref wI2CFrequence);
					bRet = m_InfDev.GetConfigure(ref wList);
					if ((bRet) &&  (wList.Count == 1))
					{
						wI2CFrequence = wList[0];
						m_dwErrCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
					}
					else
					{
						wI2CFrequence = 0;
						m_dwErrCode = LibErrorCode.IDS_ERR_I2C_CFG_INVALID_CONFIG_TYPE;
					}
				}
				else
				{
					m_dwErrCode = LibErrorCode.IDS_ERR_I2C_CFG_INVALID_CONFIG_TYPE;
				}
			}
			else
			{
				m_dwErrCode = LibErrorCode.IDS_ERR_MGR_INVALID_INTERFACE_HANDLER;
			}

			return bRet;
		}

		/// <summary>
		/// Get frequency of I2C bus configuration, this function is only vaild when creating with I2C interface.
		/// Function will return false and generate error code, if creating SPI interface and using this function.
		/// </summary>
		/// <param name="wI2CFrequence">output value, the frequency value in k format.</param>
		/// <returns>true, if setup is successful; otherwise return false</returns>
		/// <returns></returns>
		public bool GetI2CConfigure(ref UInt32 wI2CFrequence)
		{
			return GetConfigure(BUS_CONFIG.CONFIG_I2C, ref wI2CFrequence);
		}

		/// <summary>
		/// Get  configuration and BaudRate of SPI bus configuration, this function is only vaild when creating with SPI interface.
		/// Function will return false and generate error code, if creating I2C interface and using this function.
		/// </summary>
		/// <param name="busSPI">input value, it must be BUS_CONFIG.CONFIG_SPI</param>
		/// <param name="ySPIConfig">output value, configure value of SPI bus, please refer to definition</param>
		/// <param name="wSPIRate">output value, the baud rate value in k format</param>
		/// <returns>true, if setup is successful; otherwise return false</returns>
		public bool GetConfigure(BUS_CONFIG busSPI, ref byte ySPIConfig, ref UInt32 wSPIRate)
		{
			bool bRet = false;
			List<UInt32> wList = new List<UInt32>();

			if (bCustomer)
			{
				m_dwErrCode = LibErrorCode.IDS_ERR_SPI_CFG_INVALID_CONFIG_TYPE;
				return bRet;
			}

			if (m_InfDev != null)
			{
				if (m_DevBus.BusType == BUS_TYPE.BUS_TYPE_SPI)
				{
					wList.Add(0);
					wList.Add(1);
					//bRet = m_InfDev.GetConfigure(ref ySPIConfig, ref wSPIRate);
					bRet = m_InfDev.GetConfigure(ref wList);
					if ((bRet) && (wList.Count == 2))
					{
						ySPIConfig = (byte)wList[0];
						wSPIRate = wList[1];
					}
					else
					{
						m_dwErrCode = LibErrorCode.IDS_ERR_SPI_CFG_INVALID_CONFIG_TYPE;
					}
				}
				else
				{
					m_dwErrCode = LibErrorCode.IDS_ERR_SPI_CFG_INVALID_CONFIG_TYPE;
				}
			}
			else
			{
				m_dwErrCode = LibErrorCode.IDS_ERR_MGR_INVALID_INTERFACE_HANDLER;
			}

			return bRet;
		}

		/// <summary>
		/// Special function of SPI getting configuration, this function is only vaild when creating with SPI interface.
		/// Function will return false and generate error code, if creating I2C interface and using this function.
		/// Note that this function will force adapter as Master mode
		/// </summary>
		/// <param name="ySPIPolariy">output value, if getting non-zero value after calling this function, it means High Active of Polarity; otherwise is Low Active</param>
		/// <param name="ySPIPhase">output value, if getting non-zero value after calling this function, it means High Active of Phase; otherwise is Low Active</param>
		/// <param name="ySPIBitOrder">output value, if getting non-zero value after calling this function, it means bit-order is MSB; otherwise is LSB</param>
		/// <param name="ySPISSPolarity">output value,if getting non-zero value after calling this function, it means Slave Polarity is High Active; otherwise is Low Active</param>
		/// <param name="ySPIWire">output value, if not zero, it will set up as 4-wires; otherwise is 3-wires</param>
		/// <returns>true, if setup is successful; otherwise return false</returns>
		public bool GetSPIConfigure(ref byte ySPIPolariy, ref byte ySPIPhase, ref byte ySPIBitOrder, ref byte ySPISSPolarity, ref byte ySPIWire)
		{
			bool bRet = false;
			byte ySPIcfg = O2SPI_CONFIG_UNKNOW;
			UInt32 wSPIBRate = 1000;

			if (bCustomer)
			{
				m_dwErrCode = LibErrorCode.IDS_ERR_SPI_CFG_INVALID_CONFIG_TYPE;
				return bRet;
			}

			bRet = GetConfigure(BUS_CONFIG.CONFIG_SPI, ref ySPIcfg, ref wSPIBRate);
			if (bRet)
			{
				if ((ySPIcfg & O2SPI_CONFIG_MSB) != 0) ySPIBitOrder = 0x01;
				else ySPIBitOrder = 0x00;

				if ((ySPIcfg & O2SPI_CONFIG_PHASE) != 0) ySPIPhase = 0x01;
				else ySPIPhase = 0x00;

				if ((ySPIcfg & O2SPI_CONFIG_POLARITY) != 0) ySPIPolariy = 0x01;
				else ySPIPolariy = 0x00;

				if ((ySPIcfg & O2SPI_CONFIG_SSPOLARITY) != 0) ySPISSPolarity = 0x01;
				else ySPISSPolarity = 0x00;

				if ((ySPIcfg & O2SPI_CONFIG_4WIRE) != 0) ySPIWire = 0x01;
				else ySPIWire = 0x00;
			}

			return bRet;
		}

		/// <summary>
		/// Special function of SPI baud rate getting, this function is only valid when creating with SPI interface.
		/// Function will return fasle and generate error code, if creating I2C interface and using this function.
		/// </summary>
		/// <param name="wSPIBaudRate">output value, return baud rate value in k format</param>
		/// <returns>true, if setup is successful; otherwise return false</returns>
		public bool GetSPIBaudRate(UInt32 wSPIBaudRate)
		{
			bool bRet = false;
			byte ySPIcfg = O2SPI_CONFIG_UNKNOW;

			if (bCustomer)
			{
				m_dwErrCode = LibErrorCode.IDS_ERR_SPI_CFG_INVALID_CONFIG_TYPE;
				return bRet;
			}

			bRet = GetConfigure(BUS_CONFIG.CONFIG_SPI, ref ySPIcfg, ref wSPIBaudRate);
			if (bRet)
			{
			}
			else
			{
				wSPIBaudRate = 0;
			}

			return bRet;
		}

		/// <summary>
		/// Get  configuration and BaudRate of COM port and I2C frequency, this function is only vaild when creating with SVID interface.
		/// Function will return false and generate error code, if creating no SVID interface and using this function.
		/// </summary>
		/// <param name="busSVID">input value, it must be BUS_CONFIG.CONFIG_SVID</param>
		/// <param name="wI2CFrequence">output value, I2C frequency of I2C bus</param>
		/// <param name="wBaudRate">output value, baud rate value of COM port</param>
		/// <param name="pParity">output value, one of System.IO.Ports.Parity enum value</param>
		/// <param name="wDataBits">output value, data bits setting of COM port</param>
		/// <param name="wStopbit">output value, stop bit setting of COM port</param>
		/// <returns>true, if setup is successful; otherwise return false</returns>
		public bool GetConfigure(BUS_CONFIG busSVID,
													ref UInt32 wI2CFrequence,
													ref UInt32 wBaudRate,
													ref UInt32 wParity,
													ref UInt32 wDataBits,
													ref UInt32 wStopbit)
		{
			bool bRet = false;
			List<UInt32> wList = new List<UInt32>();

			if (m_InfDev != null)
			{
				if ((m_DevBus.BusType == BUS_TYPE.BUS_TYPE_SVID) && (busSVID == BUS_CONFIG.CONFIG_SVID))
				{
					wI2CFrequence = 0;
					wBaudRate = 0;
					wParity = 0;
					wDataBits = 0;
					wStopbit = 0;
					wList.Clear();
					wList.Add(wI2CFrequence);
					wList.Add(wBaudRate);
					wList.Add(2);
					wList.Add(wDataBits);
					wList.Add(wStopbit);
					bRet = m_InfDev.GetConfigure(ref wList);
					if (bRet)
					{
						wI2CFrequence = wList[0];
						wBaudRate = wList[1];
						wParity = wList[2];
						wDataBits = wList[3];
						wStopbit = wList[4];
					}
					else
					{
						m_dwErrCode = LibErrorCode.IDS_ERR_MGR_CONFIG_BUFFER_NOT_ENOUGH;
					}
				}
				else
				{
					m_dwErrCode = LibErrorCode.IDS_ERR_MGR_CONFIG_SVID_BUSTYPE;
				}
			}
			else
			{
				m_dwErrCode = LibErrorCode.IDS_ERR_MGR_INVALID_INTERFACE_HANDLER;
			}

			return bRet;
		}

		/// <summary>
		/// Get  configuration and BaudRate of COM port and I2C frequency, this function is only vaild when creating with SVID interface.
		/// Function will return false and generate error code, if creating no SVID interface and using this function.
		/// </summary>
		/// <param name="wI2CFrequence">output value, I2C frequency of I2C bus</param>
		/// <param name="wBaudRate">output value, baud rate value of COM port</param>
		/// <param name="pParity">output value, one of System.IO.Ports.Parity enum value</param>
		/// <param name="wDataBits">output value, data bits setting of COM port</param>
		/// <param name="wStopbit">output value, stop bit setting of COM port</param>
		/// <returns>true, if setup is successful; otherwise return false</returns>
		public bool GetSVIDConfigure(ref UInt32 wI2CFrequence,
															ref UInt32 wBaudRate,
															ref UInt32 wParity,
															ref UInt32 wDataBits,
															ref UInt32 wStopbit)
		{
			return GetConfigure(BUS_CONFIG.CONFIG_SVID, ref wI2CFrequence, ref wBaudRate, ref wParity, ref wDataBits, ref wStopbit);
		}

		public bool GetConfigure(BUS_CONFIG busRS232,
													ref UInt32 wBaudRate,
													ref UInt32 wDataBits,
													ref UInt32 wStopbit,
													ref UInt32 wParity)
		{
			bool bRet = false;
			List<UInt32> wList = new List<UInt32>();

			if (m_InfDev != null)
			{
				if ((m_DevBus.BusType == BUS_TYPE.BUS_TYPE_RS232) && (busRS232 == BUS_CONFIG.CONFIG_RS232))
				{
					wBaudRate = 0;
					wDataBits = 0;
					wStopbit = 0;
					wParity = 0;
					wList.Clear();
					wList.Add(wBaudRate);
					wList.Add(wDataBits);
					wList.Add(wStopbit);
					wList.Add(wParity);
					bRet = m_InfDev.GetConfigure(ref wList);
					if (bRet)
					{
						wBaudRate = wList[0];
						wDataBits = wList[1];
						wStopbit = wList[2];
						wParity = wList[3];
					}
					else
					{
						m_dwErrCode = LibErrorCode.IDS_ERR_MGR_CONFIG_BUFFER_NOT_ENOUGH;
					}
				}
				else
				{
					m_dwErrCode = LibErrorCode.IDS_ERR_MGR_CONFIG_SVID_BUSTYPE;
				}
			}
			else
			{
				m_dwErrCode = LibErrorCode.IDS_ERR_MGR_INVALID_INTERFACE_HANDLER;
			}

			return bRet;
		}

		public bool GetRS232Configure(ref UInt32 wBaudRate,
															ref UInt32 wDataBits,
															ref UInt32 wStopbit,
															ref UInt32 wParity)
		{
			return GetConfigure(BUS_CONFIG.CONFIG_RS232, ref wBaudRate, ref wDataBits, ref wStopbit, ref wParity);
		}

		public bool SendCommandtoAdapter(byte[] yDataIn, ref byte[] yDataOut, ref UInt16 wDataOutLength, UInt16 wDataInLength)
		{
			bool bReturn = true;

			if (m_InfDev != null)
			{
				bReturn = m_InfDev.SetAdapterCommand(ref yDataIn, ref yDataOut, ref wDataOutLength, wDataInLength);
				/*bReturn &= */
				m_InfDev.WriteDataToLog(yDataIn, yDataOut, wDataOutLength, wDataInLength, 0);
			}
			else
			{
				bReturn = false;
				m_dwErrCode = LibErrorCode.IDS_ERR_MGR_INVALID_INTERFACE_HANDLER;
			}

			return bReturn;
		}

/*
				public byte AddTest(ref byte[] inByte)
				{
					inByte[2] = (byte)(inByte[0] + inByte[1]);
					return (byte)(inByte[0] + inByte[1]);
				}
		*/

		/*//temp API to configure SPI
		public void SetSPIConfigure(ref byte[] yCfgInOut)
		{
			m_InfDev.ConfigureDevice(ref yCfgInOut, true);
		}

		public void GetSPIconfigure(ref byte[] yCfgInOut)
		{
			m_InfDev.ConfigureDevice(ref yCfgInOut, false);
		}

		public void SetSPIBaudRate(UInt16 wBaudRate)
		{
			
		}

		public void GetSPIBaudRate(ref UInt16 wBaudRate)
		{
		}
		*/

		//(A141203)Francis, add for SVID master board communication
		/// <summary>
		/// Set up I2C protocol in SVID master board; this function only works if BusType == BUS_TYPE.BUS_TYPE_SVID
		/// </summary>
		/// <returns>true: if set up OK; otherwise will return</returns>
		public bool SetSVIDProtocolI2C()
		{
			bool bReturn = false;

			if (m_InfDev != null)
			{
				if ((m_DevBus.BusType == BUS_TYPE.BUS_TYPE_SVID))
				{
					m_InfDev.SetSVIDAccessI2C();
					m_InfDev.ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
					bReturn = true;
				}
				else
				{
					m_InfDev.ErrorCode = LibErrorCode.IDS_ERR_MGR_CONFIG_SVID_NOT_SUPPORT;
				}
			}
			else
			{
				m_dwErrCode = LibErrorCode.IDS_ERR_MGR_INVALID_INTERFACE_HANDLER;
			}

			return bReturn;
		}

		/// <summary>
		/// Set up SVIDVR protocol in SVID master board; this function only works if BusType == BUS_TYPE.BUS_TYPE_SVID
		/// </summary>
		/// <returns>true: if set up OK; otherwise will return</returns>
		public bool SetSVIDProtocolVR()
		{
			bool bReturn = false;

			if (m_InfDev != null)
			{
				if ((m_DevBus.BusType == BUS_TYPE.BUS_TYPE_SVID))
				{
					m_InfDev.SetSVIDAccessVR();
					m_InfDev.ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
					bReturn = true;
				}
				else
				{
					m_InfDev.ErrorCode = LibErrorCode.IDS_ERR_MGR_CONFIG_SVID_NOT_SUPPORT;
				}
			}
			else
			{
				m_dwErrCode = LibErrorCode.IDS_ERR_MGR_INVALID_INTERFACE_HANDLER;
			}

			return bReturn;
		}
		//(E141203)

		public bool SetO2I2CDelayTime(UInt16 uDelay = 76)
		{
			bool bRet = false;
			List<UInt32> wList = new List<UInt32>();

			if (m_InfDev != null)
			{
				if (((m_DevBus.BusType == BUS_TYPE.BUS_TYPE_I2C) || (m_DevBus.BusType == BUS_TYPE.BUS_TYPE_I2C2) ||
					(m_DevBus.BusType == BUS_TYPE.BUS_TYPE_SVID)))
				{
					wList.Clear();
					wList.Add(uDelay);
					//bRet = m_InfDev.SetConfigure(wI2CFrequence);
					bRet = m_InfDev.SetO2DelayTime(wList);
				}
				else
				{
					m_dwErrCode = LibErrorCode.IDS_ERR_I2C_CFG_INVALID_CONFIG_TYPE;
				}
			}
			else
			{
				m_dwErrCode = LibErrorCode.IDS_ERR_MGR_INVALID_INTERFACE_HANDLER;
			}

			return bRet;
		}

        public bool SetUARTRWDelay(UInt16 uRWDelay = 100)
        {
            bool bRet = false;

            if (m_InfDev != null)
            {
                if (((m_DevBus.BusType == BUS_TYPE.BUS_TYPE_RS232)))
                {
                    if (m_InfDev != null)
                    {
                        m_InfDev.wUARTReadDelay = uRWDelay;
                        m_dwErrCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
                    }
                    else
                    {
                        m_dwErrCode = LibErrorCode.IDS_ERR_MGR_INVALID_INTERFACE_HANDLER;
                    }
                }
                else
                {
                    m_dwErrCode = LibErrorCode.IDS_ERR_I2C_CFG_INVALID_CONFIG_TYPE;
                }
            }
            else
            {
                m_dwErrCode = LibErrorCode.IDS_ERR_MGR_INVALID_INTERFACE_HANDLER;
            }

            return bRet;
        }
		#endregion

        #region System function and handler

/*
		protected override void WndProc(ref Message m)
		{
			//we detect the media arrival event
			switch (m.Msg)
			{
				case NativeMethods.WM_DEVICECHANGE:
					{
						Guid driverGuid;
						//string strName;
						NativeMethods.DEV_BROADCAST_DEVICEINTERFACE mydve;
						switch (m.WParam.ToInt32())
						{
							case NativeMethods.DBT_DEVICEARRIVAL:
								if (m.LParam == (IntPtr)0) break;
								driverGuid = NativeMethods.PtrToDevInfo(m.LParam).dbcc_classguid;
								if (driverGuid == CO2USBI2CAdapter.GetGuid())
								{
								}
								break;
							case NativeMethods.DBT_DEVICEREMOVECOMPLETE:
								if (m.LParam == (IntPtr)0) break;
								mydve = NativeMethods.PtrToDevInfo(m.LParam);
								driverGuid = mydve.dbcc_classguid;
								//strName = new string(mydve.dbcc_name);
								if (driverGuid == CO2USBI2CAdapter.GetGuid())
								{
									if (mydve.dbcc_name.ToString().Equals(m_InfDev.SymbolicLinkName))
										m_InfDev.CloseDevice();
								}
								break;
						}
					}
					break;
				default:
					base.WndProc(ref m);
					break;
			}
		}
*/

/*
		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);

			// Adds the windows message processing hook and registers USB device add/removal notification.
			HwndSource source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
			if (source != null)
			{
				windowHandle = source.Handle;
				source.AddHook(HwndHandler);
				UsbNotification.RegisterUsbDeviceNotification(windowHandle);
			}

			private IntPtr HwndHandler(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam, ref bool handled)
			{
				handled = false;
				return IntPtr.Zero;
			}
		}
*/

		// <summary>
		// Hook function, to register into Windows message hooker and interrupt Broadcase Message and get what we need
		// </summary>
		// <param name="hwnd">input parameter, Hanlder of Windows</param>
		// <param name="msg">input parameter, Windows Message</param>
		// <param name="wParam">input parameter, wParam of message</param>
		// <param name="lParam">input parameter, lParam of message</param>
		// <param name="handled">output parameter; true = ever handled, false = no handle</param>
		// <returns>return handler</returns>
		private IntPtr hwndSourceHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
		{
			if (msg == NativeMethods.WM_DEVICECHANGE)
			{
				Guid driverGuid;
                Int16 iNumber = -1;
				NativeMethods.DEV_BROADCAST_DEVICEINTERFACE mydve;
				if (wParam.ToInt32() == NativeMethods.DBT_DEVICEARRIVAL)
				{
					if (lParam == (IntPtr)0) return IntPtr.Zero;
					mydve = NativeMethods.PtrToDevInfo(lParam);
					driverGuid = mydve.dbcc_classguid;
					if (driverGuid == CO2USBI2CAdapter.GetGuid())
					{
						Int16 iTaLength = 0;
						DaisySymbolicConvert(ref mydve.dbcc_name, ref iTaLength);
						//if(tmpStr.Equals(m_InfDev.SymbolicLinkName))
						string tmpStr = new string(mydve.dbcc_name);
						tmpStr = tmpStr.Substring(0, iTaLength);
						tmpStr = tmpStr.ToLower();
						//if(System.Array.Equals(mydve.dbcc_name, m_InfDev.SymbolicLinkName.ToCharArray()))
                        //FindDevices(ref m_DevBus);
                        //ConvertSymbolnameToUIPort(CO2USBI2CAdapter.GetGuid(), ref m_DevBus, m_InfDev.PortIndex);
                        m_USBlinkname.Clear();
                        //if(CInterfaceBase.FindO2USBDevice(ref iNumber, ref m_dwErrCode, ref m_USBlinkname))
						if(!FindDevices(ref m_DevBus))
							//ConvertSymbolnameToUIPort(CO2USBI2CAdapter.GetGuid(), ref m_DevBus, m_o2Index);
							return IntPtr.Zero;
						if (m_InfDev != null)
						{
							if (tmpStr.Equals(m_InfDev.SymbolicLinkName))
							{
								m_InfDev.OpenDevice(m_USBlinkname, m_InfDev.PortIndex);
								handled = true;
							}
						}
					}
					/* Don't need, due to COM port is opened when trying to access, instead of always opening its handler
					else if (driverGuid == CO2SVID2I2CMaster.GetGuid())
					{
						Int16 iTaLength = 0;
						DaisySymbolicConvert(ref mydve.dbcc_name, ref iTaLength);
						//if(tmpStr.Equals(m_InfDev.SymbolicLinkName))
						string tmpStr = new string(mydve.dbcc_name);
						tmpStr = tmpStr.Substring(0, iTaLength);
						tmpStr = tmpStr.ToLower();
						//if(System.Array.Equals(mydve.dbcc_name, m_InfDev.SymbolicLinkName.ToCharArray()))
						if (tmpStr.Equals(m_InfDev.SymbolicLinkName))
						{
							m_InfDev.OpenDevice(m_USBlinkname, m_InfDev.PortIndex);
							handled = true;
						}
					}
					 */ 
				}
				if (wParam.ToInt32() == NativeMethods.DBT_DEVICEREMOVECOMPLETE)
				{
					if (lParam == (IntPtr)0) return IntPtr.Zero;
					mydve = NativeMethods.PtrToDevInfo(lParam);
					driverGuid = mydve.dbcc_classguid;
					//strName = new string(mydve.dbcc_name);
					if (driverGuid == CO2USBI2CAdapter.GetGuid())
					{
						//if (mydve.dbcc_name.ToString().Equals(m_InfDev.SymbolicLinkName))
						//string tmpStr;
						//DaisySymbolicConvert(mydve.dbcc_name, ref tmpStr);
						Int16 iTaLength = 0;
						DaisySymbolicConvert(ref mydve.dbcc_name, ref iTaLength);
						//if(tmpStr.Equals(m_InfDev.SymbolicLinkName))
						string tmpStr = new string(mydve.dbcc_name);
						tmpStr = tmpStr.Substring(0, iTaLength);
						tmpStr = tmpStr.ToLower();
						//if(System.Array.Equals(mydve.dbcc_name, m_InfDev.SymbolicLinkName.ToCharArray()))
                        //FindDevices(ref m_DevBus);
                        m_USBlinkname.Clear();
                        //if (CInterfaceBase.FindO2USBDevice(ref iNumber, ref m_dwErrCode, ref m_USBlinkname))
						if(FindDevices(ref m_DevBus))
                            RemoveSymbolnameFromUIPort(CO2USBI2CAdapter.GetGuid(), ref m_DevBus, m_o2Index);
						if (m_InfDev != null)
						{
							if (tmpStr.Equals(m_InfDev.SymbolicLinkName))
							{
								m_InfDev.CloseDevice(false);
								handled = true;
							}
						}
					}
				}
			}
			return IntPtr.Zero;
		}

		#endregion
    }
}
