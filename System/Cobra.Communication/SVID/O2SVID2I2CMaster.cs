using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System.Threading;
using Cobra.Common;
using Cobra.Communication;

namespace Cobra.Communication.SVID
{
	public class CO2SVID2I2CMaster : CInterfaceSVID
	{
		#region CO2SVID2I2CMaster constant member and constant value definition

//		private static readonly Guid O2SVID2I2CGUID = new Guid("{374d4f43-f000-baad-eefe-abababababab}");
		private static readonly Guid O2SVID2I2CGUID = new Guid("{004f0043-004d-0037-0000-000000005580}");
		private static readonly string strVID = "10c4";
		private static readonly string strPID = "ea60";

		//O2 SVID setup 
		//private const byte I2CAdapterFlagPowerUp = 0x01;
		//private const byte I2CAdapterFlagPullUp = 0x02;
		//private const byte MPTAdapterFlagPullUp = 0x03;
		//private const byte I2CFlagTargetConnect = 0x04;

		//private const byte O2_I2C_NO_FLAGS = 0x00;
		//private const byte O2_I2C_10_BIT_ADDR = 0x01;
		//private const byte O2_I2C_NO_REPEATED_START = 0x02;
		//private const byte O2_I2C_NO_STOP = 0x04;
		//private const byte O2_I2C_NO_SMB_BLOCK_READ = 0x08;
		//private const byte O2_I2C_SMB_PEC_ENABLE = 0x10;
		// Constructor
		#endregion

		#region CO2SVID2I2CMaster private member definition

		//Serial port
		private SerialPort SVIDSerial = new SerialPort();

		//COM port setting and I2C frequency
		private UInt16 wSVIDI2CFrequence = 400;
		private UInt16 wSVIDCOMBaudrate = 9600;
		private System.IO.Ports.Parity pSVIDCOMParity = System.IO.Ports.Parity.None;
		private UInt16 wSVIDCOMDatabits = 8;
		//private UInt16 wSVIDCOMStopbit = 1;
		private System.IO.Ports.StopBits tSVIDCOMStop = System.IO.Ports.StopBits.One;
		private float fBlankTimeScale = 12.5f;		//comes from VB code

		#endregion
		
		public CO2SVID2I2CMaster()
		{
			CloseDevice();
			m_Locker = new Semaphore(0, 1);
			m_SendBuffer = new byte[CCommunicateManager.MAX_RWBUFFER];			//new send buffer
			m_ReceiveBuffer = new byte[CCommunicateManager.MAX_RWBUFFER];		//new receive buffer
		}

		#region Public Method, Override CInterfaceBase and CInterfaceSVID 2 mother class

		public static Guid GetGuid()
		{
			return O2SVID2I2CGUID;
		}

		public static List<string> GetComPortLinkName()
		{
			String pattern = String.Format("^VID_{0}.PID_{1}", strVID, strPID);
			Regex _rx = new Regex(pattern, RegexOptions.IgnoreCase);
			List<string> comports = new List<string>();
			RegistryKey rk1 = Registry.LocalMachine;
            if (rk1 == null) return comports;
			RegistryKey rk2 = rk1.OpenSubKey("SYSTEM\\CurrentControlSet\\Enum");
            if (rk2 == null) return comports;
			foreach (String s3 in rk2.GetSubKeyNames())
			{
				RegistryKey rk3 = rk2.OpenSubKey(s3);
                if(rk3 == null) continue;
				foreach (String s in rk3.GetSubKeyNames())
				{
					if (_rx.Match(s).Success)
					{
						RegistryKey rk4 = rk3.OpenSubKey(s);
                        if (rk4 == null) continue;
                        foreach (String s2 in rk4.GetSubKeyNames())
						{
                            RegistryKey rk5 = rk4.OpenSubKey(s2);
                            if (rk5 != null)
                            {
                                RegistryKey rk6 = rk5.OpenSubKey("Device Parameters");
                                if (rk6 != null)
                                {
                                    comports.Add((string)rk6.GetValue("PortName"));
                                }
                            }
						}
					}
				}
			}
			return comports;
		}

		// <summary>
		// Open devices, function will enumerate all connected devices and save in iPortNum. 
		// After successfully opened, function will try to open indicated device by yPortIndex value
		// Currently, we are supporting total 4 devices simultaneously connected, so yPortIndex allow 0~3 input
		// </summary>
		// <param name="iPortNum">after opened successfully, save how many devices is connected</param>
		// <param name="yPortIndex">index value to indicate which device to open; currently support 0~3</param>
		// <returns>true: opened successfully; false: opened failed</returns>
		public override bool OpenDevice(ref Int16 iPortNum, byte yPortIndex = 0)
		{
			bool bReturn = false;

			//if input index of target device is out of supported range, error report
			if (((int)yPortIndex < 0) || ((int)yPortIndex >= CCommunicateManager.MAX_COMM_DEVICES))
			{
				ErrorCode = LibErrorCode.IDS_ERR_SVID_INVALID_PARAMETER;
				return bReturn;
			}

			//if handler instance is alreday exist, close handler first
			if (SVIDSerial != null)
			{
				CloseDevice();
			}

			//enumerate all connected interface device
			bReturn = EnumerateSVIDMaster(ref iPortNum, yPortIndex);

			//if (SVIDSerial.IsOpen)
			//{
				//SVIDSerial.Close();
			//}

			//m_SendBuffer = new byte[CCommunicateManager.MAX_RWBUFFER];			//new send buffer
			//m_ReceiveBuffer = new byte[CCommunicateManager.MAX_RWBUFFER];		//new receive buffer
			if (bReturn)
			{
				SetCOMport();
				SVIDSerial.PortName = FriendName;

				//if all successful, set successful error code and release semaphore
				try
				{
					SVIDSerial.Open();
					if (SVIDSerial.IsOpen)
					{
						ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
						bReturn = true;
						SVIDSerial.Close();		//if ok to open then close it. COM port device is not allowed to open permanently
					}	//if (SVIDSerial.IsOpen)
					else
					{
						ErrorCode = LibErrorCode.IDS_ERR_SVID_OPEN_FAILED;
						bReturn = false;
					}	//if (SVIDSerial.IsOpen)
				}
				catch (Exception e)
				{
					ErrorCode = LibErrorCode.IDS_ERR_SVID_OPEN_FAILED;
					bReturn = false;
				}
			}	//if (bReturn)
			else
			{
				bReturn = false;
				//ErrorCode = LibErrorCode.IDS_ERR_SVID_INDEX_OUT;
			}	//if (bReturn)

			return bReturn;
		}

		public override bool OpenDevice(AsyncObservableCollection<string> strName, byte yPortIndex)
		{
			bool bReturn = false;

			//if input index of target device is out of supported range, error report
			//if (((int)yPortIndex < 0) || ((int)yPortIndex >= CCommunicateManager.MAX_COMM_DEVICES))
			//{
			//	ErrorCode = LibErrorCode.IDS_ERR_SVID_INVALID_PARAMETER;
			//	return bReturn;
			//}
			if (strName.Count <= yPortIndex)
			{
				ErrorCode = LibErrorCode.IDS_ERR_SVID_INDEX_OUT;
				return bReturn;
			}

			//if handler instance is alreday exist, close handler first
			if (SVIDSerial != null)
			{
				CloseDevice();
			}

			//enumerate all connected interface device
			//bReturn = EnumerateSVIDMaster(ref iPortNum, yPortIndex);
			FriendName = strName[yPortIndex];

			//if (SVIDSerial.IsOpen)
			//{
				//SVIDSerial.Close();
			//}

			//m_SendBuffer = new byte[CCommunicateManager.MAX_RWBUFFER];			//new send buffer
			//m_ReceiveBuffer = new byte[CCommunicateManager.MAX_RWBUFFER];		//new receive buffer
			SetCOMport();
			SVIDSerial.PortName = FriendName;

			try
			{
				SVIDSerial.Open();
				//if all successful, set successful error code and release semaphore
				if (SVIDSerial.IsOpen)
				{
					ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
					bReturn = true;
					SVIDSerial.Close();		//if ok to open then close it. COM port device is not allowed to open permanently
				}
				else
				{
					ErrorCode = LibErrorCode.IDS_ERR_SVID_OPEN_FAILED;
					bReturn = false;
				}
			}
			catch (Exception e)
			{
				ErrorCode = LibErrorCode.IDS_ERR_SVID_OPEN_FAILED;
				bReturn = false;
			}

			return bReturn;
		}

		// <summary>
		// Close handler of interface device, release instance
		// </summary>
		// <returns>Always return true</returns>
		public override bool CloseDevice(bool bClearName = true)
		{
			if (SearchCOMList(SVIDSerial.PortName))
			{
				try
				{
					if (SVIDSerial.IsOpen)
					{
						SVIDSerial.Close();
					}
				}
				catch (Exception e)
				{
					ErrorCode = LibErrorCode.IDS_ERR_SVID_OPEN_FAILED;
					return false;
				}
			}

			if (bClearName)
			{
				SymbolicLinkName = "";
				FriendName = "";
				DisplayName = "";
				DeviceNumber = 0;
				PortIndex = 0xFF;
			}
			ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;

			return true;
		}

		// <summary>
		// Read data, call ReadByte(), ReadWord(), or ReadBlock individually depends on wDataInLength; 
		// wDataInLength = 1 means ReadByte() protocal; wDataInLength = 2 means ReadWord() protocal; wDataInLength = others means ReadBlock() protocal
		// </summary>
		// <param name="yDataIn">buffer of input data, write to interface device byte by byte sequentially</param>
		// <param name="yDataOut">buffer of output data</param>
		// <param name="wDataOutLength">output value indicate the number of output data</param>
		// <param name="wDataInLength">indicate number to read</param>
		// <returns>true: operation successful; false: operation failed</returns>
		public override bool ReadDevice(ref byte[] yDataIn, ref byte[] yDataOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 1, UInt16 wDataInWrite = 1)
		{
			bool bReturn = false;

			if (yDataOut.GetLength(0) < wDataInLength)
			{
				ErrorCode = LibErrorCode.IDS_ERR_SVID_READ_BUFFER_NOT_ENOUGH;
				return bReturn;
			}

			//check input data buffer size, at least having Slave Address and Command 2 bytes
			if (yDataIn.GetLength(0) < 2)
			{
				ErrorCode = LibErrorCode.IDS_ERR_SVID_IN_PARAMETER_INVALID;
				return bReturn;
			}

			System.Array.Clear(yDataOut, 0, yDataOut.GetLength(0));		//clear data buffer
			if (SVIDSerial != null)
			{
				//check SVIDSerial COM port setting is existing
				if (!SearchCOMList(SVIDSerial.PortName))
				{
					SVIDSerial = null;
					ErrorCode = LibErrorCode.IDS_ERR_SVID_COM_NOT_EXIST;
					return false;
				}

				//try to open COM port
				try
				{
					SVIDSerial.Open();
					if (!SVIDSerial.IsOpen)
					{
						ErrorCode = LibErrorCode.IDS_ERR_SVID_OPEN_FAILED;
						return bReturn;
					}
				}
				catch (Exception e)
				{
					ErrorCode = LibErrorCode.IDS_ERR_SVID_OPEN_FAILED;
					return bReturn;
				}

				if (SVIDAccessMethod == SVIDMethodEnum.SVIDI2C)
				{
					#region I2C read protocol on SVID master board, supports I2C read byte and I2C read word
					if (wDataInLength == 0)
					{
						ErrorCode = LibErrorCode.IDS_ERR_SVID_INVALID_READI2CSINGLE;
					}
					else if (wDataInLength == 1)
					{
						bReturn = O2SVIDReadI2CByte(ref yDataIn, ref yDataOut, ref wDataOutLength, wDataInLength);
					}
					else if (wDataInLength == 2)
					{
						bReturn =  O2SVIDReadI2CWord(ref yDataIn, ref yDataOut, ref wDataOutLength, wDataInLength);
					}
					else if ((wDataInLength == 3))
					{
						//bReturn =  O2SVIDReadI2CWord(ref yDataIn, ref yDataOut, ref wDataOutLength, wDataInLength);
						ErrorCode = LibErrorCode.IDS_ERR_SVID_INVALID_READI2CBLOCK;
					}
					else
					{
						ErrorCode = LibErrorCode.IDS_ERR_SVID_INVALID_READI2CBLOCK;
					}	//if (wDataInLength == 0) == 1, ==2...etc
					#endregion
				}
				else if (SVIDAccessMethod == SVIDMethodEnum.SVIDVR)
				{
					#region SVID read protocol on SVID master board, only support SVID read byte protocol
					if (wDataInLength == 0)
					{
						ErrorCode = LibErrorCode.IDS_ERR_SVID_INVALID_READVRSINGLE;
					}
					else if (wDataInLength == 1)
					{
						bReturn = O2SVIDReadVRByte(ref yDataIn, ref yDataOut, ref wDataOutLength, wDataInLength);
					}
					else if (wDataInLength == 2)
					{
						//bReturn = O2SVIDReadVRWord(ref yDataIn, ref yDataOut, ref wDataOutLength, wDataInLength);
						ErrorCode = LibErrorCode.IDS_ERR_SVID_INVALID_READVRWORD;
					}
					else if ((wDataInLength >= 3))
					{
						ErrorCode = LibErrorCode.IDS_ERR_SVID_INVALID_READVRBLOCK;
					}
					else
					{
						ErrorCode = LibErrorCode.IDS_ERR_SVID_INVALID_READVRBLOCK;
					}
					#endregion
				}
				else
				{
					ErrorCode = LibErrorCode.IDS_ERR_SVID_INVALID_ENUMMETHOD;
				}	//if (SVIDAccessMethod == SVIDMethodEnum.SVIDI2C)
			}
			else
			{
				ErrorCode = LibErrorCode.IDS_ERR_SVID_NULL_COM_HANDLER;
				return bReturn;
			}	//if (SVIDSerial != null)

			try
			{
				if (SVIDSerial.IsOpen)
					SVIDSerial.Close();
			}
			catch (Exception e)
			{
				ErrorCode = LibErrorCode.IDS_ERR_SVID_OPEN_FAILED;
				bReturn = false;
			}

			return bReturn;
		}

		// <summary>
		// Wrte data, call WriteByte(), WriteWord(), or WriteBlock individually depends on wDataInLength; 
		// wDataInLength = 1 means WriteByte() protocal; wDataInLength = 2 means WriteWord() protocal; wDataInLength = others means WriteBlock() protocal
		// </summary>
		// <param name="yDataIn">buffer of input data, write to interface device byte by byte sequentially, first 2 bytes must be target I2C address then target register</param>
		// <param name="yDataOut">buffer of output data, useless</param>
		// <param name="wDataOutLength">output value indicate the number of output data</param>
		// <param name="wDataInLength">indicate number of data to read, excluding targer I2C address and target register</param>
		// <returns>true: operation successful; false: operation failed</returns>
		public override bool WriteDevice(ref byte[] yDataIn, ref byte[] yDataOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 1)
		{
			bool bReturn = false;

			//check yDataIn array, must have Slave Address and Command Index, 2 byte values
			if (yDataIn.GetLength(0) < 2)
			{
				ErrorCode = LibErrorCode.IDS_ERR_SVID_IN_PARAMETER_INVALID;
				return bReturn;
			}

			//yDataIn length should be equal or bigger that wDataInLength + 2 (Slave Address and Command Index)
			if (yDataIn.GetLength(0) < wDataInLength + 2)
			{
				ErrorCode = LibErrorCode.IDS_ERR_SVID_IN_PARAMETER_INVALID;
				return bReturn;
			}

			System.Array.Clear(yDataOut, 0, yDataOut.GetLength(0));
			wDataOutLength = 0;
			if (SVIDSerial != null)
			{
				//check SVIDSerial COM port setting is existing
				if (!SearchCOMList(SVIDSerial.PortName))
				{
					SVIDSerial = null;
					ErrorCode = LibErrorCode.IDS_ERR_SVID_COM_NOT_EXIST;
					return false;
				}

				//try to opne COM port
				try
				{
					SVIDSerial.Open();
					if (!SVIDSerial.IsOpen)
					{
						ErrorCode = LibErrorCode.IDS_ERR_SVID_OPEN_FAILED;
						return bReturn;
					}
				}
				catch (Exception e)
				{
					ErrorCode = LibErrorCode.IDS_ERR_SVID_OPEN_FAILED;
					return bReturn;
				}

				if (SVIDAccessMethod == SVIDMethodEnum.SVIDI2C)
				{
					#region I2C write protocol on SVID master board, only support I2C write byte protocol
					if (wDataInLength == 0)
					{
						ErrorCode = LibErrorCode.IDS_ERR_SVID_INVALID_WRITEI2CSINGLE;
					}
					else if (wDataInLength == 1)
					{
						bReturn = O2SVIDWriteI2CByte(ref yDataIn, ref yDataOut, ref wDataOutLength, wDataInLength);
					}
					else if (wDataInLength == 2)
					{
						//bReturn = O2I2CWriteWord(ref yDataIn, ref yDataOut, ref wDataOutLength, wDataInLength);
						ErrorCode = LibErrorCode.IDS_ERR_SVID_INVALID_WRITEI2CWORD;
					}
					else if ((wDataInLength >= 3) && (wDataInLength <= 31))
					{
						//bReturn = O2I2CWriteBlock(ref yDataIn, ref yDataOut, ref wDataOutLength, wDataInLength);
						ErrorCode = LibErrorCode.IDS_ERR_SVID_INVALID_WRITEI2CBLOCK;
					}
					else
					{
						ErrorCode = LibErrorCode.IDS_ERR_SVID_INVALID_WRITEI2CBLOCK;
					}//if (wDataInLength == 0) == 1, ==2...etc
					#endregion
				}
				else if (SVIDAccessMethod == SVIDMethodEnum.SVIDVR)
				{
					/* SVID write command must follow below data format
					 * if only 1 byte write, it will be
					 * [0]: VR Address
					 * [1]: 1st VR Command
					 * [2]: 1st VR Payload
					 * if 2 and 3 bytes write command it must be
					 * [0]: VR Address
					 * [1]: 1st VR Command
					 * [2]: 1st VR Payload
					 * [3]: 1st blank time byte_0
					 * [4]: 1st blank time byte_1
					 * [5]: 1st blank time byte_2
					 * [6]: 2nd VR Command
					 * [7]: 2nd VR Payload
					 * [8]: 2nd blank time byte_0
					 * [9]: 2nd blank time byte_1
					 * [10]: 3rd blank time byte_2
					 * [11]: 3rd VR Command
					 * [12]: 3rd VR Payload
					 * [13]: 3rd blank time byte_0
					 * [14]: 3rd blank time byte_1
					 * [15]: 3rd blank time byte_2
					*/
					#region SVID write protocol on SVID master board, supports SVID write byte , SVID write word, and SVID write 3 bytes
					if (wDataInLength == 0)
					{
						ErrorCode = LibErrorCode.IDS_ERR_SVID_INVALID_WRITEVRSINGLE;
					}
					else if (wDataInLength == 1)
					{
						if (yDataIn.GetLength(0) < 3)
						{
							ErrorCode = LibErrorCode.IDS_ERR_SVID_IN_PARAMETER_INVALID;
						}
						else
						{
							bReturn = O2SVIDWriteVRByte(ref yDataIn, ref yDataOut, ref wDataOutLength, wDataInLength);
						}
					}
					else if (wDataInLength == 2)
					{
						if (yDataIn.GetLength(0) < 11)
						{
							ErrorCode = LibErrorCode.IDS_ERR_SVID_IN_PARAMETER_INVALID;
						}
						else
						{
							bReturn = O2SVIDWriteVRWord(ref yDataIn, ref yDataOut, ref wDataOutLength, wDataInLength);
						}
					}
					else if ((wDataInLength == 3))
					{
						if (yDataIn.GetLength(0) < 16)
						{
							ErrorCode = LibErrorCode.IDS_ERR_SVID_IN_PARAMETER_INVALID;
						}
						else
						{
							bReturn = O2SVIDWriteVRBlock(ref yDataIn, ref yDataOut, ref wDataOutLength, wDataInLength);
						}
					}
					else
					{
						ErrorCode = LibErrorCode.IDS_ERR_SVID_INVALID_WRITEVRBLOCK;
					}
					#endregion
				}
				else
				{
					ErrorCode = LibErrorCode.IDS_ERR_SVID_INVALID_ENUMMETHOD;
				}	//if (SVIDAccessMethod == SVIDMethodEnum.SVIDI2C)
			}
			else
			{
				ErrorCode = LibErrorCode.IDS_ERR_SVID_INVALID_ENUMMETHOD;
				return bReturn;
			}

			try
			{
				if (SVIDSerial.IsOpen)
					SVIDSerial.Close();
			}
			catch (Exception e)
			{
				ErrorCode = LibErrorCode.IDS_ERR_SVID_OPEN_FAILED;
				bReturn = false;
			}

			return bReturn;
		}

		/*		public override bool ConfigureDevice(ref byte[] yCfgInOut, bool bRW = false)
				{
					SetFrequency((ushort)100);	//set as 100k
					return true;
				}*/

		public override bool ResetInf()
		{
			//check SVIDSerial COM port setting is existing
			if (SVIDSerial != null)
			{
				if (!SearchCOMList(SVIDSerial.PortName))
				{
					SVIDSerial = null;
					ErrorCode = LibErrorCode.IDS_ERR_SVID_COM_NOT_EXIST;
					return false;
				}

				try
				{
					if (SVIDSerial.IsOpen)
						SVIDSerial.Close();
				}
				catch (Exception e)
				{
					ErrorCode = LibErrorCode.IDS_ERR_SVID_OPEN_FAILED;
					return false;
				}
				SetCOMport();
			}
			return true;
		}

		public override bool SetConfigure(List<UInt32> wConfig)
		{
			if (wConfig.Count != 5) return false;

			wSVIDI2CFrequence = (UInt16)wConfig[0];
			wSVIDCOMBaudrate = (UInt16)wConfig[1];
			wSVIDCOMDatabits = (UInt16)wConfig[3];
			//wSVIDCOMStopbit = wConfig[4];
			if (wConfig[2] == 0)
			{
				pSVIDCOMParity = System.IO.Ports.Parity.None;
			}
			else if (wConfig[2] == 1)
			{
				pSVIDCOMParity = System.IO.Ports.Parity.Odd;
			}
			else if (wConfig[2] == 2)
			{
				pSVIDCOMParity = System.IO.Ports.Parity.Even;
			}
			else if (wConfig[2] == 3)
			{
				pSVIDCOMParity = System.IO.Ports.Parity.Mark;
			}
			else if (wConfig[2] == 4)
			{
				pSVIDCOMParity = System.IO.Ports.Parity.Space;
			}
			else
			{
				return false;
			}

			if (wConfig[4] == 0)
			{
				tSVIDCOMStop = System.IO.Ports.StopBits.None;
			}
			else if (wConfig[4] == 1)
			{
				tSVIDCOMStop = System.IO.Ports.StopBits.One;
			}
			else if (wConfig[4] == 2)
			{
				tSVIDCOMStop = System.IO.Ports.StopBits.Two;
			}
			else if (wConfig[4] == 3)
			{
				tSVIDCOMStop = System.IO.Ports.StopBits.OnePointFive;
			}
			else
			{
				return false;
			}

			SetCOMport();

			return true;
		}

		public override bool GetConfigure(ref List<UInt32> wConfig)
		{
			if (wConfig.Count != 5) return false;

			GetCOMport();
			wConfig[0] = wSVIDI2CFrequence;
			wConfig[1] = wSVIDCOMBaudrate;
			wConfig[3] = wSVIDCOMDatabits;
			//wConfig[4] = wSVIDCOMStopbit;
			if (pSVIDCOMParity == System.IO.Ports.Parity.None)
			{
				wConfig[2] = 0;
			}
			else if (pSVIDCOMParity == System.IO.Ports.Parity.Odd)
			{
				wConfig[2] = 1;
			}
			else if (pSVIDCOMParity == System.IO.Ports.Parity.Even)
			{
				wConfig[2] = 2;
			}
			else if (pSVIDCOMParity == System.IO.Ports.Parity.Mark)
			{
				wConfig[2] = 3;
			}
			else if (pSVIDCOMParity == System.IO.Ports.Parity.Space)
			{
				wConfig[2] = 4;
			}
			else
			{
				wConfig[2] = 0;
				//return false;
			}

			if (tSVIDCOMStop == System.IO.Ports.StopBits.None)
			{
				wConfig[4] = 0;
			}
			else if (tSVIDCOMStop == System.IO.Ports.StopBits.One)
			{
				wConfig[4] = 1;
			}
			else if (tSVIDCOMStop == System.IO.Ports.StopBits.Two)
			{
				wConfig[4] = 2;
			}
			else if (tSVIDCOMStop == System.IO.Ports.StopBits.OnePointFive)
			{
				wConfig[4] = 3;
			}
			else
			{
				wConfig[4] = 0;
				//return false;
			}

			return true;
		}

		public override bool SetO2DelayTime(List<UInt32> wDelay)
		{
			ErrorCode = LibErrorCode.IDS_ERR_MGR_INVALID_INTERFACE_TYPE;
			return false;
		}

		public override bool SetAdapterCommand(ref byte[] yDataIn, ref byte[] yDataOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 1)
		{
			bool bReturn = false;

			ErrorCode = LibErrorCode.IDS_ERR_MGR_INVALID_INTERFACE_TYPE;
			return bReturn;
		}

		#endregion

		#region Private Method, O2SVID2I2CMaster adapter self method

		private bool SearchCOMList(string inStrCOMName)
		{
			string[] strCOMArr;
			bool bReturn = false;

			if (inStrCOMName.Length != 0)
			{
				strCOMArr = SerialPort.GetPortNames();
				for (int i=0; i<strCOMArr.Length; i++)
				{
					strCOMArr[i] = strCOMArr[i].ToUpper();
					inStrCOMName = inStrCOMName.ToUpper();
					if(string.Equals(strCOMArr[i], inStrCOMName))
					{
						bReturn = true;
						break;
					}
				}
			}

			return bReturn;
		}

		// <summary>
		// Enumberate how many interface device is connected
		// </summary>
		// <param name="iDevNum">after finded successfully, save how many devices is connected</param>
		// <param name="yPortIndex">index value to indicate target device to open</param>
		// <returns>true: found target device and open handler successful; false: open failed</returns>
		private unsafe bool EnumerateSVIDMaster(ref Int16 iDevNum, byte yPortIndex)
		{
			bool bReturn = true;	//basically, it will have no chance that error happened
			string[] strSerialNames;
			int iNum;

			strSerialNames = SerialPort.GetPortNames();
			iNum = strSerialNames.Length;
			iDevNum = (Int16)iNum;
			if (iDevNum != 0)
			{
				if (yPortIndex <= iDevNum)
				{
					SymbolicLinkName = strSerialNames[yPortIndex];
					FriendName = strSerialNames[yPortIndex];
				}
				else
				{
					bReturn = false;
				}
			}

			return bReturn;
		}

		private void SetCOMport()
		{
			SVIDSerial.BaudRate = wSVIDCOMBaudrate;
			SVIDSerial.Parity = pSVIDCOMParity;
			SVIDSerial.DataBits = wSVIDCOMDatabits;
			SVIDSerial.StopBits = tSVIDCOMStop;

			//SVIDSerial.Open();			//it looks like no need to open it?
		}

		private void GetCOMport()
		{
			wSVIDCOMBaudrate = (UInt16)SVIDSerial.BaudRate;
			pSVIDCOMParity = SVIDSerial.Parity;
			wSVIDCOMDatabits = (UInt16)SVIDSerial.DataBits;
			tSVIDCOMStop = SVIDSerial.StopBits;
		}

		/*
		private bool O2SVIDReadWriteCommand(ref byte[] yDataIn, ref byte[] yDataOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 1, byte bRW = 0/*default write)
		{
			bool bReturn = false;

			bReturn = O2SVIDControlCommand();

			return bReturn;
		}
		*/

		/* I2C communication protocol, copied from source code comment of SVID master board
		 * //				|  bit7	|  bit6	|  bit5	|  bit4	|  bit3	|  bit2	|  bit1	|  bit0	|  
		 * //	----------------------------------------------------------------------------------    
		 * //	| cntr0	|  10/7	|   HS	|  R/W	| data number	|  structure number		|  
		 * //	----------------------------------------------------------------------------------    
		 * //	| cntr1	| hs frequency	|      fs frequency			| trim_f	| ext address	|
		 * //	----------------------------------------------------------------------------------    
		 * //	| cntr2	|                7bits-addr + W  (or 8bits-addr)							|
		 * //	--------------------------------------------------------------------------------------
		 * //
		 * //  data format: | cntr0 | cntr1 | cntr2 | reg_addr1 | data1 ... | reg_addr2 | data1 ... | ... ... | check |
		 * //															 ----------------------------   ---------------------------   -------
		 * //																	structure 1						structure 2          structure n
		 * //																				| data number ? |
		*/

		private bool O2SVIDControlCommand(out Int32 iRetLeng)
		{
			bool bReturn = false;
			Int32 iRet = 0;
			int iRetry = 6;

			SVIDSerial.ReadTimeout = 500;
			SVIDSerial.WriteTimeout = 500;
			SVIDSerial.Write(m_SendBuffer, 0, m_SendSize);
			//Thread.Sleep(1000);
			while ((SVIDSerial.BytesToRead == 0) || (SVIDSerial.BytesToWrite > 0))
			{
				Thread.Sleep(50);
				if (iRetry > 0) iRetry--;
				else break;	//break while loop; avoid dead loop
			}
			iRetLeng = 0;
			if (iRetry >= 0)
			{
				iRet = SVIDSerial.Read(m_ReceiveBuffer, 0, SVIDSerial.BytesToRead);
				//if (iRet >= m_ReceiveSize + 5)
				if(iRet > 0)
				{
					iRetLeng = iRet;
					ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
					bReturn = true;
				}
				else
				{
					ErrorCode = LibErrorCode.IDS_ERR_SVID_COM_READ_ZERO;
				}
			}
			else
			{
				ErrorCode = LibErrorCode.IDS_ERR_SVID_COM_TIMEOUT;
			}

			return bReturn;
		}

		//tested OK
		private bool O2SVIDReadI2CByte(ref byte[] yDataIn, ref byte[] yDataOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 1)
		{
			bool bReturn = false;
			Int32 iRetLength = 0;

			if (yDataIn.GetLength(0) < 2)
				return false;	//to prevent data is not enough to copy

			m_SendSize = 7;
			m_SendBuffer[0] = 0x14;								//byte 0: indicate I2C operation
			m_SendBuffer[1] = 0x00;								//byte1: [7]=0 means 7-bit address
			//if (wSVIDI2CFrequence == 3400)
			//{
				//m_SendBuffer[1] |= 0x40;									//byte1: [6]=1 means high-speed mode 3.4M
			//}
			m_SendBuffer[1] |= 0x20;										//byte1 :[5]=1 means I2C read
			if (wDataInLength == 1)
			{
				m_SendBuffer[1] |= 0x09;									//byte1: [4:3]= data number;		//byte1:[2:0] structure number, how much index+data combination
			}
			else if (wDataInLength == 2)
			{
				m_SendBuffer[1] |= 0x11;
			}
			//m_SendBuffer[2] = 0x98;										//byte2:[7:6] hs frequency; [5:3] fs frequency; [2] trim_f; [1:0]ext address
			SetI2CFreqByteValue(ref m_SendBuffer[1], ref m_SendBuffer[2]);
			m_SendBuffer[3] = (byte)(yDataIn[0]+0x01);		//byte3: i2c address in 8-bits format 
			m_SendBuffer[4] = yDataIn[1];								//byte4: index value
			m_SendBuffer[5] = 0;											//byte5: data; 0 for read
			m_SendBuffer[6] = 0x14;										//byte6: 0x14
			m_ReceiveSize = wDataInLength;
			bReturn = O2SVIDControlCommand(out iRetLength);
			if (bReturn)
			{
				
				if (iRetLength >= m_ReceiveSize + 5)
				{
					wDataOutLength = wDataInLength;
					Buffer.BlockCopy(m_ReceiveBuffer, 4, yDataOut, 0, wDataOutLength);
					ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
				}
				else
				{
					wDataOutLength = 0;
					ErrorCode = LibErrorCode.IDS_ERR_SVID_READ_NOT_ENOUGH;
					bReturn = false;
				}
			}
			else
			{
				//ErrorCode = LibErrorCode.;
				//ErrorCode is assigned in O2SVIDControlCommand()
				wDataOutLength = 0;
			}

			return bReturn;
		}

		//tested OK preliminarily, Sunchaser don't support I2C word Read, but this function acts as VB did
		private bool O2SVIDReadI2CWord(ref byte[] yDataIn, ref byte[] yDataOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 2)
		{
			bool bReturn = false;
			Int32 iRetLength = 0;

			if (yDataIn.GetLength(0) < 2)
				return false;	//to prevent data is not enough to copy

			m_SendSize = 8;
			m_SendBuffer[0] = 0x14;								//byte 0: indicate I2C operation
			m_SendBuffer[1] = 0x00;								//byte1: [7]=0 means 7-bit address
			//if (wSVIDI2CFrequence == 3400)
			//{
				//m_SendBuffer[1] |= 0x40;									//byte1: [6]=1 means high-speed mode 3.4M
			//}
			m_SendBuffer[1] |= 0x20;										//byte1 :[5]=1 means I2C read
			if (wDataInLength == 1)
			{
				m_SendBuffer[1] |= 0x09;									//byte1: [4:3]= data number;		//byte1:[2:0] structure number, how much index+data combination
			}
			else if (wDataInLength == 2)
			{
				//no tested
				m_SendBuffer[1] |= 0x11;
			}
			//m_SendBuffer[2] = 0x98;										//byte2:[7:6] hs frequency; [5:3] fs frequency; [2] trim_f; [1:0]ext address
			SetI2CFreqByteValue(ref m_SendBuffer[1], ref m_SendBuffer[2]);
			m_SendBuffer[3] = (byte)(yDataIn[0] + 0x01);		//byte3: i2c address in 8-bits format 
			m_SendBuffer[4] = yDataIn[1];								//byte4: index value
			m_SendBuffer[5] = 0;											//byte5: data; 0 for read
			m_SendBuffer[6] = 0;											//byte6: 0x00
			m_SendBuffer[7] = 0x14;										//byte7: 0x14
			m_ReceiveSize = wDataInLength;
			bReturn = O2SVIDControlCommand(out iRetLength);
			if (bReturn)
			{

				if (iRetLength >= m_ReceiveSize + 5)
				{
					wDataOutLength = wDataInLength;
					Buffer.BlockCopy(m_ReceiveBuffer, 4, yDataOut, 0, wDataOutLength);
					ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
				}
				else
				{
					wDataOutLength = 0;
					ErrorCode = LibErrorCode.IDS_ERR_SVID_READ_NOT_ENOUGH;
					bReturn = false;
				}
			}
			else
			{
				//ErrorCode is assigned in O2SVIDControlCommand()
				wDataOutLength = 0;
			}

			return bReturn;
		}

		//tested OK
		private bool O2SVIDWriteI2CByte(ref byte[] yDataIn, ref byte[] yDataOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 1)
		{
			bool bReturn = false;
			Int32 iRetLength = 0;

			if (yDataIn.GetLength(0) < 3)
				return false;	//to prevent data is not enough to copy

			m_SendSize = 7;
			m_SendBuffer[0] = 0x14;								//byte 0: indicate I2C operation
			m_SendBuffer[1] = 0x00;								//byte1: [7]=0 means 7-bit address
			//if (wSVIDI2CFrequence == 3400)
			//{
				//m_SendBuffer[1] |= 0x40;									//byte1: [6]=1 means high-speed mode 3.4M
			//}
			//m_SendBuffer[1] |= 0x20;										//byte1 :[5]=1 means I2C read
			if (wDataInLength == 1)
			{
				m_SendBuffer[1] |= 0x09;									//byte1: [4:3]= data number;		//byte1:[2:0] structure number, how much index+data combination
			}
			else if (wDataInLength == 2)
			{
				m_SendBuffer[1] |= 0x11;
			}
			//m_SendBuffer[2] = 0x98;										//byte2:[7:6] hs frequency; [5:3] fs frequency; [2] trim_f; [1:0]ext address
			SetI2CFreqByteValue(ref m_SendBuffer[1], ref m_SendBuffer[2]);
			m_SendBuffer[3] = yDataIn[0];								//byte3: i2c address in 8-bits format 
			m_SendBuffer[4] = yDataIn[1];								//byte4: index value
			m_SendBuffer[5] = yDataIn[2];											//byte5: data; 0 for read
			m_SendBuffer[6] = 0x14;										//byte6: 0x14
			m_ReceiveSize = wDataInLength;
			bReturn = O2SVIDControlCommand(out iRetLength);
			wDataOutLength = 0;
			if (bReturn)
			{
				ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
			}
			else
			{
				//ErrorCode is assigned in O2SVIDControlCommand()
				//ErrorCode = LibErrorCode.;
			}

			return bReturn;
		}

		//To be tested
		private bool O2SVIDWriteI2CWord(ref byte[] yDataIn, ref byte[] yDataOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 2)
		{
			bool bReturn = false;
			Int32 iRetLength = 0;

			if (yDataIn.GetLength(0) < 4)
				return false;	//to prevent data is not enough to copy

			m_SendSize = 7;
			m_SendBuffer[0] = 0x14;										//byte 0: indicate I2C operation
			m_SendBuffer[1] = 0x00;										//byte1: [7]=0 means 7-bit address
			//if (wSVIDI2CFrequence == 3400)
			//{
				//m_SendBuffer[1] |= 0x40;									//byte1: [6]=1 means high-speed mode 3.4M
			//}
			//m_SendBuffer[1] |= 0x20;									//byte1 :[5]=0 means I2C write
			if (wDataInLength == 1)
			{
				m_SendBuffer[1] |= 0x09;									//byte1: [4:3]= data number;		//byte1:[2:0] structure number, how much index+data combination
			}
			else if (wDataInLength == 2)
			{
				m_SendBuffer[1] |= 0x11;
			}
			//m_SendBuffer[2] = 0x98;										//byte2:[7:6] hs frequency; [5:3] fs frequency; [2] trim_f; [1:0]ext address
			SetI2CFreqByteValue(ref m_SendBuffer[1], ref m_SendBuffer[2]);
			m_SendBuffer[3] = yDataIn[0];								//byte3: i2c address in 8-bits format 
			m_SendBuffer[4] = yDataIn[1];								//byte4: index value
			m_SendBuffer[5] = yDataIn[2];								//byte5: data; 0 for read
			m_SendBuffer[6] = yDataIn[3];								//byte6: data; 0 for read
			m_SendBuffer[7] = 0x14;										//byte7: 0x14
			m_ReceiveSize = wDataInLength;
			bReturn = O2SVIDControlCommand(out iRetLength);
			wDataOutLength = 0;
			if (bReturn)
			{
				ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
			}
			else
			{
				//ErrorCode is assigned in O2SVIDControlCommand()
				//ErrorCode = LibErrorCode.;
			}

			return bReturn;
		}

		/***********************************************************
		 * This is copied from source code's comment in SVID master board 
		 * 	SVI2 data format 
		 * 	byte0. 13H, indicate SVI2 data
		 * 	byte1. [6:4] operation mode; [1:0] SVI2 packet number
		 * 	byte2. byte3. byte4. SVI2 first packet
		 * 	byte5. byte6. byte7 interval counter between packet 1 and packet 2
		 * 	byte8. if only packet 1, that is end
		 * 	byte8. byte9. byte10. SVI2 second packet
		 * 	byte11. byte12. byte13. reserved
		 * 	byte14. end
		*************************************************************/

		//tested OK
		private bool O2SVIDReadVRByte(ref byte[] yDataIn, ref byte[] yDataOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 1)
		{
			bool	bReturn = false;
			Int32	iRetLength = 0;

			m_SendSize =9;																					//don't know why 9 bytes, last 3 data are all 0xFF
			m_SendBuffer[0] = 0x13;																		//byte 0: indicate I2C operation
			m_SendBuffer[1] = 0x01;																		//byte1: command number
			m_SendBuffer[2] = (byte)(0xF4 + ((yDataIn[0] & 0x08) >> 3));		//byte2: 0xF4 + VR address bit-3; 0xF means nothing, 0x04 means SVID start signal b010
			m_SendBuffer[3] = (byte)(yDataIn[0] & 0x07);									//byte3: byte3[7:5]=VR_addr[2:0], byte3[4:0]=SVID_CMD
			m_SendBuffer[3] <<= 5;
			m_SendBuffer[3] += (byte)(yDataIn[1] & 0x1F);									//SVID_CMD only 5-bits long
			m_SendBuffer[4] = yDataIn[2];																//byte4: payload; it's data
			m_SendBuffer[5] = O2SVIDCalculateParity(yDataIn, 3);					//byte5: [7]parity
			m_SendBuffer[5] = (byte)((m_SendBuffer[5] & 0x01) << 7);
			m_SendBuffer[5] += 0x3F;																	//byte5: [6:4]=011(stop), byte5[3:0]=1111
			m_SendBuffer[6] = 0xFF;																		//byte6: 0xFF
			m_SendBuffer[7] = 0xFF;																		//byte7: 0xFF
			m_SendBuffer[8] = 0xFF;																		//byte8: 0xFF
			m_ReceiveSize = 3;
			bReturn = O2SVIDControlCommand(out iRetLength);
			if (bReturn)
			{
				if(iRetLength >= m_ReceiveSize)
				{
					wDataOutLength = wDataInLength;
					Buffer.BlockCopy(m_ReceiveBuffer, 1, yDataOut, 0, wDataOutLength);
					ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
				}
				else
				{
					wDataOutLength = 0;
					ErrorCode = LibErrorCode.IDS_ERR_SVID_READ_NOT_ENOUGH;
				}
			}
			else
			{
				//ErrorCode is assigned in O2SVIDControlCommand()
				//ErrorCode = LibErrorCode.;
				wDataOutLength = 0;
			}

			return bReturn;
		}

		//tested OK
		private bool O2SVIDWriteVRByte(ref byte[] yDataIn, ref byte[] yDataOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 1)
		{
			bool	bReturn = false;
			Int32	iRetLength = 0;

			m_SendSize =9;																					//don't know why 9 bytes, last 3 data are all 0xFF
			m_SendBuffer[0] = 0x13;																		//byte 0: indicate I2C operation
			m_SendBuffer[1] = 0x01;																		//byte1: command number
			m_SendBuffer[2] = (byte)(0xF4 + ((yDataIn[0] & 0x08)>>3));			//byte2: 0xF4 + VR address bit-3; 0xF means nothing, 0x04 means SVID start signal b010
			m_SendBuffer[3] = (byte)(yDataIn[0] & 0x07);									//byte3: byte3[7:5]=VR_addr[2:0], byte3[4:0]=SVID_CMD
			m_SendBuffer[3] <<= 5;
			m_SendBuffer[3] += (byte)(yDataIn[1] & 0x1F);									//SVID_CMD only 5-bits long
			m_SendBuffer[4] = yDataIn[2];																//byte4: payload; it's data
			m_SendBuffer[5] = O2SVIDCalculateParity(yDataIn, 3);					//byte5: [7]parity
			m_SendBuffer[5] = (byte)((m_SendBuffer[5] & 0x01) << 7);
			m_SendBuffer[5] += 0x3F;																	//byte5: [6:4]=011(stop), byte5[3:0]=1111
			m_SendBuffer[6] = 0xFF;																		//byte6: 0xFF
			m_SendBuffer[7] = 0xFF;																		//byte7: 0xFF
			m_SendBuffer[8] = 0xFF;																		//byte8: 0xFF
			m_ReceiveSize = 3;
			bReturn = O2SVIDControlCommand(out iRetLength);
			if (bReturn)
			{
				if(iRetLength >= m_ReceiveSize)
				{
					//Buffer.BlockCopy(m_ReceiveBuffer, 4, yDataOut, 0, wDataOutLength);
					ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
				}
				else
				{
					ErrorCode = LibErrorCode.IDS_ERR_SVID_READ_NOT_ENOUGH;
				}
			}
			else
			{
				//ErrorCode is assigned in O2SVIDControlCommand()
				//ErrorCode = LibErrorCode.;
				wDataOutLength = 0;
			}

			return bReturn;
		}

		//tested OK
		private bool O2SVIDWriteVRWord(ref byte[] yDataIn, ref byte[] yDataOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 2)
		{
			bool	bReturn = false;
			Int32	iRetLength = 0;
			byte[] paritCal = new byte[3];
			double dbBlank = 0;

			m_SendSize =15;																					//don't know why 9 bytes, last 3 data are all 0xFF
			m_SendBuffer[0] = 0x13;																		//byte 0: indicate I2C operation
			m_SendBuffer[1] = 0x02;																		//byte1: command number
			m_SendBuffer[2] = (byte)(0xF4 + ((yDataIn[0] & 0x08)>>3));			//byte2: 0xF4 + VR address bit-3; 0xF means nothing, 0x04 means SVID start signal b010
			m_SendBuffer[3] = (byte)(yDataIn[0] & 0x07);									//byte3: byte3[7:5]=VR_addr[2:0], byte3[4:0]=SVID_CMD
			m_SendBuffer[3] <<= 5;
			m_SendBuffer[3] += (byte)(yDataIn[1] & 0x1F);									//SVID_CMD only 5-bits long
			m_SendBuffer[4] = yDataIn[2];																//byte4: payload; it's data
			m_SendBuffer[5] = O2SVIDCalculateParity(yDataIn, 3);					//byte5: [7]parity
			m_SendBuffer[5] = (byte)((m_SendBuffer[5] & 0x01) << 7);
			m_SendBuffer[5] += 0x30;																	//byte5: [6:4]=011(stop)
			dbBlank = yDataIn[5];																			//copy blank time [5] * 65536 + [4] * 256 + [3]
			for (int i = 4; i > 2; i--)
			{
				dbBlank *= 256;
				dbBlank += yDataIn[i];
			}
			if(dbBlank != 16777215)
				dbBlank *= fBlankTimeScale;															//O2 SVID master board definition, user input blank time * 12.5
			m_SendBuffer[7] = (byte)((UInt32)dbBlank & 0xFF);						//byte7: low byte of blank time
			dbBlank /= 256;
			m_SendBuffer[6] = (byte)((UInt32)dbBlank & 0xFF);						//byte6: high byte of blank time
			dbBlank /= 256;
			m_SendBuffer[5] += (byte)((UInt32)dbBlank & 0x0F);						//byte 5: [3:0]add on 4-bits of blank time
			//below arrange 2nd command
			m_SendBuffer[8] = (byte)(0xF4 + ((yDataIn[0] & 0x08)>>3));			//byte8: 0xF4 + VR address bit-3; 0xF means nothing, 0x04 means SVID start signal b010
			m_SendBuffer[9] = (byte)(yDataIn[0] & 0x07);									//byte9: byte9[7:5]=VR_addr[2:0], byte3[4:0]=SVID_CMD for 2nd package
			m_SendBuffer[9] <<= 5;
			m_SendBuffer[9] += (byte)(yDataIn[6] & 0x1F);									//SVID_CMD only 5-bits long
			m_SendBuffer[10] = yDataIn[7];															//byte10: payload for 2nd package; it's data
			paritCal[0] = yDataIn[0];																		//copy VR address, SVID command, and payload to calculate parity for 2nd package
			for (int i = 1; i < 3; i++)
				paritCal[i] = yDataIn[i + 5];
			m_SendBuffer[11] = O2SVIDCalculateParity(paritCal, 3);				//byte11: [7]parity for 2nd package
			m_SendBuffer[11] = (byte)((m_SendBuffer[11] & 0x01) << 7);
			m_SendBuffer[11] += 0x30;																	//byte11: [6:4]=011(stop), byte5[3:0]=1111
			dbBlank = yDataIn[10];																			//copy blank time [5] * 65536 + [4] * 256 + [3]
			for (int i = 9; i > 7; i--)
			{
				dbBlank *= 256;
				dbBlank += yDataIn[i];
			}
			if (dbBlank != 16777215)
				dbBlank *= fBlankTimeScale;															//O2 SVID master board definition, user input blank time * 12.5
			m_SendBuffer[13] = (byte)((UInt32)dbBlank & 0xFF);						//byte13: low byte of blank time of 2nd package
			dbBlank /= 256;
			m_SendBuffer[12] = (byte)((UInt32)dbBlank & 0xFF);						//byte12: high byte of blank time of 2nd package
			dbBlank /= 256;
			m_SendBuffer[11] += (byte)((UInt32)dbBlank & 0x0F);					//byte11: [3:0]add on 4-bits of blank time of 2nd package
			m_SendBuffer[14] = 0xFF;																	//byte14: 0xFF
			m_ReceiveSize = 3;
			bReturn = O2SVIDControlCommand(out iRetLength);
			if (bReturn)
			{
				if(iRetLength >= m_ReceiveSize)
				{
					//Buffer.BlockCopy(m_ReceiveBuffer, 4, yDataOut, 0, wDataOutLength);
					ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
				}
				else
				{
					ErrorCode = LibErrorCode.IDS_ERR_SVID_READ_NOT_ENOUGH;
				}
			}
			else
			{
				//ErrorCode is assigned in O2SVIDControlCommand()
				//ErrorCode = LibErrorCode.;
				wDataOutLength = 0;
			}

			return bReturn;
		}

		//to be tested
		private bool O2SVIDWriteVRBlock(ref byte[] yDataIn, ref byte[] yDataOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 3)
		{
			bool bReturn = false;
			Int32 iRetLength = 0;
			byte[] paritCal = new byte[3];
			double dbBlank = 0;

			m_SendSize = 21;																				//don't know why 9 bytes, last 3 data are all 0xFF
			m_SendBuffer[0] = 0x13;																		//byte 0: indicate I2C operation
			m_SendBuffer[1] = 0x03;																		//byte1: command number
			m_SendBuffer[2] = (byte)(0xF4 + ((yDataIn[0] & 0x08) >> 3));			//byte2: 0xF4 + VR address bit-3; 0xF means nothing, 0x04 means SVID start signal b010
			m_SendBuffer[3] = (byte)(yDataIn[0] & 0x07);									//byte3: byte3[7:5]=VR_addr[2:0], byte3[4:0]=SVID_CMD
			m_SendBuffer[3] <<= 5;
			m_SendBuffer[3] += (byte)(yDataIn[1] & 0x1F);									//SVID_CMD only 5-bits long
			m_SendBuffer[4] = yDataIn[2];																//byte4: payload; it's data
			m_SendBuffer[5] = O2SVIDCalculateParity(yDataIn, 3);					//byte5: [7]parity
			m_SendBuffer[5] = (byte)((m_SendBuffer[5] & 0x01) << 7);
			m_SendBuffer[5] += 0x30;																	//byte5: [6:4]=011(stop), byte5[3:0]=1111
			dbBlank = yDataIn[5];																			//copy blank time [5] * 65536 + [4] * 256 + [3]
			for (int i = 4; i > 2; i--)
			{
				dbBlank *= 256;
				dbBlank += yDataIn[i];
			}
			if (dbBlank != 16777215)
				dbBlank *= fBlankTimeScale;															//O2 SVID master board definition, user input blank time * 12.5
			m_SendBuffer[7] = (byte)((UInt32)dbBlank & 0xFF);						//byte7: low byte of blank time
			dbBlank /= 256;
			m_SendBuffer[6] = (byte)((UInt32)dbBlank & 0xFF);						//byte6: high byte of blank time
			dbBlank /= 256;
			m_SendBuffer[5] += (byte)((UInt32)dbBlank & 0x0F);						//byte 5: [3:0]add on 4-bits of blank time
			//below arrange 2nd command
			m_SendBuffer[8] = (byte)(0xF4 + ((yDataIn[0] & 0x08) >> 3));			//byte8: 0xF4 + VR address bit-3; 0xF means nothing, 0x04 means SVID start signal b010
			m_SendBuffer[9] = (byte)(yDataIn[0] & 0x07);									//byte9: byte9[7:5]=VR_addr[2:0], byte3[4:0]=SVID_CMD for 2nd package
			m_SendBuffer[9] <<= 5;
			m_SendBuffer[9] += (byte)(yDataIn[6] & 0x1F);									//SVID_CMD only 5-bits long
			m_SendBuffer[10] = yDataIn[7];															//byte10: payload for 2nd package; it's data
			paritCal[0] = yDataIn[0];																		//copy VR address, SVID command, and payload to calculate parity for 2nd package
			for (int i = 1; i < 3; i++)
				paritCal[i] = yDataIn[i + 5];
			m_SendBuffer[11] = O2SVIDCalculateParity(paritCal, 3);				//byte11: [7]parity for 2nd package
			m_SendBuffer[11] = (byte)((m_SendBuffer[11] & 0x01) << 7);
			m_SendBuffer[11] += 0x30;																	//byte11: [6:4]=011(stop), byte5[3:0]=1111
			dbBlank = yDataIn[10];																			//copy blank time [5] * 65536 + [4] * 256 + [3]
			for (int i = 9; i > 7; i--)
			{
				dbBlank *= 256;
				dbBlank += yDataIn[i];
			}
			if (dbBlank != 16777215)
				dbBlank *= fBlankTimeScale;															//O2 SVID master board definition, user input blank time * 12.5
			m_SendBuffer[13] = (byte)((UInt32)dbBlank & 0xFF);						//byte13: low byte of blank time of 2nd package
			dbBlank /= 256;
			m_SendBuffer[12] = (byte)((UInt32)dbBlank & 0xFF);						//byte12: high byte of blank time of 2nd package
			dbBlank /= 256;
			m_SendBuffer[11] += (byte)((UInt32)dbBlank & 0x0F);					//byte11: [3:0]add on 4-bits of blank time of 2nd package
			//below arrange 3rd command
			m_SendBuffer[14] = (byte)(0xF4 + ((yDataIn[0] & 0x08) >> 3));		//byte14: 0xF4 + VR address bit-3; 0xF means nothing, 0x04 means SVID start signal b010
			m_SendBuffer[15] = (byte)(yDataIn[0] & 0x07);									//byte15: byte9[7:5]=VR_addr[2:0], byte3[4:0]=SVID_CMD for 2nd package
			m_SendBuffer[15] <<= 5;
			m_SendBuffer[15] += (byte)(yDataIn[11] & 0x1F);								//SVID_CMD only 5-bits long
			m_SendBuffer[16] = yDataIn[12];															//byte16: payload for 2nd package; it's data
			paritCal[0] = yDataIn[0];																		//copy VR address, SVID command, and payload to calculate parity for 3rd package
			for (int i = 1; i < 3; i++)
				paritCal[i] = yDataIn[i + 10];
			m_SendBuffer[17] = O2SVIDCalculateParity(paritCal, 3);				//byte17: [7]parity for 2nd package
			m_SendBuffer[17] = (byte)((m_SendBuffer[17] & 0x01) << 7);
			m_SendBuffer[17] += 0x30;																	//byte17: [6:4]=011(stop), byte5[3:0]=1111
			dbBlank = yDataIn[15];																			//copy blank time [5] * 65536 + [4] * 256 + [3]
			for (int i = 14; i > 12; i--)
			{
				dbBlank *= 256;
				dbBlank += yDataIn[i];
			}
			if (dbBlank != 16777215)
				dbBlank *= fBlankTimeScale;															//O2 SVID master board definition, user input blank time * 12.5
			m_SendBuffer[19] = (byte)((UInt32)dbBlank & 0xFF);						//byte19: low byte of blank time of 3rd package
			dbBlank /= 256;
			m_SendBuffer[18] = (byte)((UInt32)dbBlank & 0xFF);						//byte18: high byte of blank time of 3rd package
			dbBlank /= 256;
			m_SendBuffer[17] += (byte)((UInt32)dbBlank & 0x0F);					//byte17: [3:0]add on 4-bits of blank time of 3rd package
			m_SendBuffer[20] = 0xFF;																	//byte20: 0xFF
			m_ReceiveSize = 3;
			bReturn = O2SVIDControlCommand(out iRetLength);
			if (bReturn)
			{
				if (iRetLength >= m_ReceiveSize)
				{
					//Buffer.BlockCopy(m_ReceiveBuffer, 4, yDataOut, 0, wDataOutLength);
					ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
				}
				else
				{
					ErrorCode = LibErrorCode.IDS_ERR_SVID_READ_NOT_ENOUGH;
				}
			}
			else
			{
				//ErrorCode is assigned in O2SVIDControlCommand()
				//ErrorCode = LibErrorCode.;
				wDataOutLength = 0;
			}

			return bReturn;
		}

		//tested OK
		private byte O2SVIDCalculateParity(byte[] yDataIn, UInt16 wDataLength)
		{
			byte yParity = 0;
			byte y2Pow = 1;

			for(int i=0; i<wDataLength; i++)
			{
				y2Pow = 1;
				for(int j=0; j<8; j++)
				{
					if((yDataIn[i] & y2Pow) != 0) 
					{
						yParity += 1;
					}
					y2Pow *= 2;
				}
			}

			return yParity;
		}

		private void SetI2CFreqByteValue(ref byte cntr0, ref byte cntr1)
		{
			if (wSVIDI2CFrequence == 3400)
			{
				cntr0 |= 0x40;
				cntr1 |= 0x98;		//??
			}
			else if (wSVIDI2CFrequence == 800)
			{
				cntr1 |= 0xA0;
			}
			else if (wSVIDI2CFrequence == 400)
			{
				cntr1 |= 0x98;
			}
			else if (wSVIDI2CFrequence == 200)
			{
				cntr1 |= 0x90;
			}
			else if (wSVIDI2CFrequence == 100)
			{
				cntr1 |= 0x88;
			}
		}

		#endregion
	}
}
