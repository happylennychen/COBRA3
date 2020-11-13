using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Threading;
using Cobra.Common;
using Cobra.Communication;

namespace Cobra.Communication.I2C
{
	public class CO2USBI2CAdapter : CInterfaceI2C
	{
		#region O2USBtoI2C constant member and constant value definition

		private static readonly Guid O2USBI2CGuid = new Guid("{95b4dec8-d7a6-465d-b797-6a5a66c9205f}");

		//O2 I2C Pullups and O2 I2C Power
		private const byte I2CAdapterFlagPowerUp = 0x01;
		private const byte I2CAdapterFlagPullUp = 0x02;
		private const byte MPTAdapterFlagPullUp = 0x03;
		private const byte I2CFlagTargetConnect = 0x04;

		private const byte O2_I2C_NO_FLAGS = 0x00;
		private const byte O2_I2C_10_BIT_ADDR = 0x01;
		private const byte O2_I2C_NO_REPEATED_START = 0x02;
		private const byte O2_I2C_NO_STOP = 0x04;
		private const byte O2_I2C_NO_SMB_BLOCK_READ = 0x08;
		private const byte O2_I2C_SMB_PEC_ENABLE = 0x10;

		#endregion

		// Constructor
		public CO2USBI2CAdapter()
		{
			CloseDevice();
			m_Locker = new Semaphore(0, 1);
		}

		#region Public Method, Override CInterfaceBase and CInterfaceI2C 2 mother class

		public static Guid GetGuid()
		{
			return O2USBI2CGuid;
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
				ErrorCode = LibErrorCode.IDS_ERR_I2C_INVALID_PARAMETER;
				return bReturn;
			}

			//if handler instance is alreday exist, close handler first
			if (DeviceHandler != null)
			{
				CloseDevice();
			}

			//enumerate all connected interface device
			bReturn = EnumerateO2Adaptor(ref iPortNum, yPortIndex);

			//if call EnumerateO2Adaptor() != true, error code will be done in FindO2Adapter(), don't need  do it again.
			if (bReturn)
			{
				IntPtr hFile;

				bReturn = false;		//default false return
				for (int i = 0; i < DeviceNumber; i++)
				{
					if ((byte)i == yPortIndex)	//find target index
					{
						hFile = NativeMethods.CreateFile(SymbolicLinkName,
															NativeMethods.GENERIC_WRITE | NativeMethods.GENERIC_READ,
															NativeMethods.FILE_SHARE_WRITE | NativeMethods.FILE_SHARE_READ,
															0,
															NativeMethods.OPEN_EXISTING,
															NativeMethods.FILE_FLAG_OVERLAPPED | NativeMethods.FILE_FLAG_NO_BUFFERING,
															0);
						SafeFileHandle sfh = new SafeFileHandle(hFile, true);		//creat Stream handler
						if (!sfh.IsInvalid)
						{
							try
							{
								m_Locker.Release();													//if FileHandle ok, release semaphore
							}
							catch (Exception)
							{
							}
							DeviceHandler = new FileStream(sfh, FileAccess.ReadWrite, 256, true);			//new FileStream
							PortIndex = yPortIndex;
							m_SendBuffer = new byte[CCommunicateManager.MAX_RWBUFFER];			//new send buffer
							m_ReceiveBuffer = new byte[CCommunicateManager.MAX_RWBUFFER];		//new receive buffer
							bReturn = true;
							bReturn = SetFrequency(400);									//default frequency is 400KHz
							//UInt16 ui = I2CFrequence;
							I2CFrequence = 400;
							bReturn = O2I2CAdapterTargetConnect(true);
							bReturn = O2I2CAdapterTargetPower(true);
							bReturn = O2I2CAdapterPullups(true);
							//bReturn = ResetInf();
						}		//if (!sfh.IsInvalid)
						else
						{
							ErrorCode = LibErrorCode.IDS_ERR_I2C_INVALID_HANDLE;
							return false;
						}
					}		//if ((byte)i == yPortIndex)
				}		//for (int i = 0; i < DeviceNumber; i++)

				//not found yPortIndex target device, report error
				if (DeviceHandler == null)
				{
					ErrorCode = LibErrorCode.IDS_ERR_I2C_INVALID_INDEX;
					return false;
				}
			}		//if (bReturn) = EnumerateO2Adaptor, ErrorCode will be set in EnumerateO2Adaptor() function

			//if all successful, set successful error code and release semaphore
			if (bReturn)
			{
				ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
			}

			return bReturn;
		}

		public override bool OpenDevice(AsyncObservableCollection<string> strName, byte yPortIndex)
		{
			bool bReturn = false;
			IntPtr hFile;

			if (strName.Count <= yPortIndex)
			{
				ErrorCode = LibErrorCode.IDS_ERR_I2C_INVALID_INDEX;
				return bReturn;
			}

			SymbolicLinkName = strName[yPortIndex];

			hFile = NativeMethods.CreateFile(SymbolicLinkName,
												NativeMethods.GENERIC_WRITE | NativeMethods.GENERIC_READ,
												NativeMethods.FILE_SHARE_WRITE | NativeMethods.FILE_SHARE_READ,
												0,
												NativeMethods.OPEN_EXISTING,
												NativeMethods.FILE_FLAG_OVERLAPPED | NativeMethods.FILE_FLAG_NO_BUFFERING,
												0);
			SafeFileHandle sfh = new SafeFileHandle(hFile, true);		//creat Stream handler
			if (!sfh.IsInvalid)
			{
				try
				{
					m_Locker.Release();													//if FileHandle ok, release semaphore
				}
				catch (Exception)
				{
				}
				DeviceHandler = new FileStream(sfh, FileAccess.ReadWrite, 256, true);			//new FileStream
				PortIndex = yPortIndex;
				m_SendBuffer = new byte[CCommunicateManager.MAX_RWBUFFER];			//new send buffer
				m_ReceiveBuffer = new byte[CCommunicateManager.MAX_RWBUFFER];		//new receive buffer
				bReturn = true;
				bReturn = SetFrequency(400);									//default frequency is 100KHz
				//UInt16 ui = I2CFrequence;
				I2CFrequence = 400;
				bReturn = O2I2CAdapterTargetConnect(true);
				bReturn = O2I2CAdapterTargetPower(true);
				bReturn = O2I2CAdapterPullups(true);
				//bReturn = ResetInf();
			}		//if (!sfh.IsInvalid)
			else
			{
				ErrorCode = LibErrorCode.IDS_ERR_I2C_INVALID_HANDLE;
				return false;
			}

			//not found yPortIndex target device, report error
			if (DeviceHandler == null)
			{
				ErrorCode = LibErrorCode.IDS_ERR_I2C_INVALID_INDEX;
				return false;
			}

			//if all successful, set successful error code and release semaphore
			if (bReturn)
			{
				ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
			}

			return bReturn;
		}

		// <summary>
		// Close handler of interface device, release instance
		// </summary>
		// <returns>Always return true</returns>
		public override bool CloseDevice(bool bClearName = true)
		{
			if (DeviceHandler != null)
			{
				try
				{
					DeviceHandler.Close();
				}
				catch (Exception)
				{
				}
				finally
				{
					DeviceHandler = null;
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

			//check output data buffer size is enough
			if (yDataOut.GetLength(0) < wDataInLength)
			{
				ErrorCode = LibErrorCode.IDS_ERR_MGR_INVALID_OUTPUT_BUFFER;
			}

			//check input data buffer size, at least having Slave Address and Command 2 bytes
			if (yDataIn.GetLength(0) < 2)
			{
				ErrorCode = LibErrorCode.IDS_ERR_MGR_INVALID_INPUT_BUFFER;
			}
			//check input data buffer size, at least having Slave Address and Command 2 bytes
			if (yDataIn.GetLength(0) < wDataInWrite + 1)
			{
				ErrorCode = LibErrorCode.IDS_ERR_MGR_INVALID_INPUT_BUFFER;
			}

			System.Array.Clear(yDataOut, 0, yDataOut.GetLength(0));		//clear data buffer
			if( DeviceHandler != null)
			{
				if (wDataInLength == 0)
				{
					bReturn = O2I2CReadSingle(ref yDataIn, ref yDataOut, ref wDataOutLength, wDataInLength);
				}
				else if (wDataInLength == 1)
				{
                    bReturn = O2I2CReadByte(ref yDataIn, ref yDataOut, ref wDataOutLength, wDataInLength, wDataInWrite);
				}
				else if (wDataInLength == 2)
				{
                    bReturn = O2I2CReadWord(ref yDataIn, ref yDataOut, ref wDataOutLength, wDataInLength, wDataInWrite);
				}
				else if ((wDataInLength >= 3) && (wDataInLength <= 59))
				{
                    bReturn = O2I2CReadBlock(ref yDataIn, ref yDataOut, ref wDataOutLength, wDataInLength, wDataInWrite);
				}
				else
				{
					ErrorCode = LibErrorCode.IDS_ERR_I2C_INVALID_LENGTH;
                    bReturn = false;
				}
			}
			else
			{
				ErrorCode = LibErrorCode.IDS_ERR_I2C_INVALID_HANDLE;
				return false;
			}

            //FolderMap.WriteFile(string.Format("Exit O2USBtoI2C ReadDevice for returning {0}, errorcode = 0x{1:X8}. ", bReturn, ErrorCode));

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
				ErrorCode = LibErrorCode.IDS_ERR_I2C_INVALID_LENGTH;
			}

			//yDataIn length should be equal or bigger that wDataInLength + 2 (Slave Address and Command Index)
			if (yDataIn.GetLength(0) < wDataInLength + 2)
			{
				ErrorCode = LibErrorCode.IDS_ERR_I2C_INVALID_LENGTH;
			}

			System.Array.Clear(yDataOut, 0, yDataOut.GetLength(0));
			wDataOutLength = 0;
			if(DeviceHandler != null)
			{
				if (wDataInLength == 0)
				{
					bReturn = O2I2CWriteSingle(ref yDataIn, ref yDataOut, ref wDataOutLength, wDataInLength);
				}
				else if (wDataInLength == 1)
				{
                    bReturn = O2I2CWriteByte(ref yDataIn, ref yDataOut, ref wDataOutLength, wDataInLength);
				}
				else if (wDataInLength == 2)
				{
                    bReturn = O2I2CWriteWord(ref yDataIn, ref yDataOut, ref wDataOutLength, wDataInLength);
				}
				else if ((wDataInLength >= 3) && (wDataInLength <= 59))
				{
                    bReturn = O2I2CWriteBlock(ref yDataIn, ref yDataOut, ref wDataOutLength, wDataInLength);
				}
				else
				{
					ErrorCode = LibErrorCode.IDS_ERR_I2C_INVALID_LENGTH;
				}
			}
			else
			{
				ErrorCode = LibErrorCode.IDS_ERR_I2C_INVALID_HANDLE;
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
			m_SendBuffer[0] = 0x21;
			m_SendSize = 1;
			return O2I2CControlCommand();
			//return true;
		}

		/*
		public override bool SetConfigure(ushort wI2CFrequence)
		{
			return SetFrequency(wI2CFrequence);	//set as 100k
		}

		public override bool GetConfigure(ref ushort wI2CFrequence)
		{
			return GetFrequency(ref wI2CFrequence);
		}

		public override bool SetConfigure(byte ySPIConfig, ushort wSPIRate)
		{
			ErrorCode = LibErrorCode.IDS_ERR_I2C_CFG_INVALID_CONFIG_TYPE;
			return false;
		}

		public override bool GetConfigure(ref byte ySPIConfig, ref ushort wSPIRate)
		{
			ErrorCode = LibErrorCode.IDS_ERR_I2C_CFG_INVALID_CONFIG_TYPE;
			return false;
		}
		*/

		public override bool SetConfigure(List<UInt32> wConfig)
		{
			return SetFrequency((UInt16)wConfig[0]);	//set frequency
		}

		public override bool GetConfigure(ref List<UInt32> wConfig)
		{
			UInt16 utempfre = 0;

			if (GetFrequency(ref utempfre))
			{
				wConfig[0] = (UInt32)utempfre;
				return true;
			}
			else
			{
				return false;
			}
		}

		public override bool SetO2DelayTime(List<UInt32> wDelay)
		{
			//ErrorCode = LibErrorCode.IDS_ERR_MGR_INVALID_INTERFACE_TYPE;
			bool bReturn = true;
			//UInt16 uData = 0x00;

			m_SendBuffer[0] = 0x2B;
			m_SendSize = 2;
			m_ReceiveSize = 2;
			Buffer.BlockCopy(BitConverter.GetBytes(wDelay[0]), 0, m_SendBuffer, 1, 2);
			bReturn = O2I2CControlCommand();
			if (bReturn)
			{
				//uData = BitConverter.ToUInt16(m_ReceiveBuffer, 3);
				//uData = m_ReceiveBuffer[3];
				ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
				bReturn = true;
			}
			else
			{
				I2CFrequence = 0;
				//ErrorCode = LibErrorCode.IDS_ERR_I2C_CFG_FREQUENCY_ERROR;
				bReturn = false;
			}

			return bReturn;
		}

		public override bool SetAdapterCommand(ref byte[] yDataIn, ref byte[] yDataOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 1)
		{
			bool bReturn = false;

			//ErrorCode = LibErrorCode.IDS_ERR_MGR_INVALID_INTERFACE_TYPE;
			//check output data buffer size is enough
			if (yDataIn.GetLength(0) < wDataInLength)
			{
				ErrorCode = LibErrorCode.IDS_ERR_MGR_INVALID_OUTPUT_BUFFER;
				return false;
			}

			if (yDataOut.GetLength(0) < wDataOutLength)
			{
				ErrorCode = LibErrorCode.IDS_ERR_MGR_INVALID_OUTPUT_BUFFER;
				return false;
			}

			m_SendSize = wDataInLength;
			for (int i = 0; i < wDataInLength; i++)
			{
				m_SendBuffer[i] = yDataIn[i];
			}
			/*
			 0x25, <I2C address low>, < I2C address hi>, <length of read>,  <flags>,<Command to send>,<Command1>…<Command N>. 
			 */

			bReturn = O2I2CControlCommand();
			if (bReturn == true)
			{
				if (m_ReceiveSize >= 0)
				{
					if (m_ReceiveSize <= wDataOutLength)				//(A171122)Francis, if return value length is samller and equal
					{
						Buffer.BlockCopy(m_ReceiveBuffer, 0, yDataOut, 0, m_ReceiveSize);
						ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
					}
					else
					{
						Buffer.BlockCopy(m_ReceiveBuffer, 0, yDataOut, 0, wDataOutLength);		//if larger
						ErrorCode = LibErrorCode.IDS_ERR_MGR_INVALID_OUTPUT_BUFFER;
					}
					wDataOutLength = (UInt16)(m_ReceiveSize);
				}
				else
				{
					ErrorCode = LibErrorCode.IDS_ERR_I2C_BUS_ERROR;
					return false;
				}
			}
			else
			{
				//timeout error code will be set in O2I2CControlCommand()
				//				ErrorCode = LibErrorCode.IDS_ERR_I2C_PROTOCOL_TIMEOUT;
				wDataOutLength = 0;
			}

			return bReturn;
		}

		#endregion

		#region Private Method, O2USBtoI2C adapter self method

		// <summary>
		// Enumberate how many interface device is connected
		// </summary>
		// <param name="iDevNum">after finded successfully, save how many devices is connected</param>
		// <param name="yPortIndex">index value to indicate target device to open</param>
		// <returns>true: found target device and open handler successful; false: open failed</returns>
		private unsafe bool EnumerateO2Adaptor(ref Int16 iDevNum, byte yPortIndex)
		{
			bool bReturn = false;

			bReturn = FindO2Adaptors(ref iDevNum, yPortIndex);

			return bReturn;
		}

		// <summary>
		// Find all connected O2USBtoI2C interface, use yPortIndex to open target interface device
		// </summary>
		// <param name="iDevNum">after finded successfully, save how many devices is connected</param>
		// <param name="yPortIndex">index value to indicate target device to open</param>
		// <returns>true: found target device and open handler successful; false: open failed</returns>
		private unsafe bool FindO2Adaptors(ref Int16 iDevNum, byte yPortIndex)
		{
			Guid tempGuid = O2USBI2CGuid;
			UInt16 wDevNum = 0;
			int hDevInfoList = 0;
			bool bPresent = false;

			ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
			hDevInfoList = NativeMethods.SetupDiGetClassDevs(ref tempGuid, null, null, NativeMethods.ClassDevsFlags.DIGCF_PRESENT | NativeMethods.ClassDevsFlags.DIGCF_DEVICEINTERFACE);
			if (hDevInfoList != 0)
			{
				NativeMethods.SP_DEVICE_INTERFACE_DATA deviceInterfaceData = new NativeMethods.SP_DEVICE_INTERFACE_DATA();
				for (int i = 0; i < CCommunicateManager.MAX_COMM_DEVICES; i++)
				{
					deviceInterfaceData.cbSize = (UInt32)Marshal.SizeOf(deviceInterfaceData);
					bPresent = NativeMethods.SetupDiEnumDeviceInterfaces(hDevInfoList, 0, ref tempGuid, i, ref deviceInterfaceData);
					if (bPresent)
					{
						int requiredLength = 0;
						NativeMethods.SetupDiGetDeviceInterfaceDetail(hDevInfoList,
																	  ref deviceInterfaceData,
																	  null,		// Not yet allocated
																	  0,			// Set output Buffer length to zero 
																	  ref requiredLength,	// Find out memory requirement
																	  null);
						ErrorCode = NativeMethods.GetLastError();
						if (ErrorCode != 0)
						{
							ErrorCode = LibErrorCode.IDS_ERR_I2C_INVALID_HANDLE;
						}

						int predictedLength = requiredLength;
						NativeMethods.PSP_DEVICE_INTERFACE_DETAIL_DATA deviceInterfaceDetailData = new NativeMethods.PSP_DEVICE_INTERFACE_DETAIL_DATA();
						switch (sizeof(IntPtr))
						{
							case 8: deviceInterfaceDetailData.cbSize = 8; break;
							default: deviceInterfaceDetailData.cbSize = 5; break;
						}
						NativeMethods.SP_DEVINFO_DATA devInfoData = new NativeMethods.SP_DEVINFO_DATA();
						devInfoData.cbSize = (UInt32)Marshal.SizeOf(devInfoData);

						// Second, get the detailed information
						if (NativeMethods.SetupDiGetDeviceInterfaceDetail(hDevInfoList,
																			  ref deviceInterfaceData,
																			  ref deviceInterfaceDetailData,
																			  predictedLength,
																			  ref requiredLength,
							//                            null) !=0)
																			  ref devInfoData) != 0)
						{
							//DeviceDescriptor dev = new DeviceDescriptor();
							NativeMethods.DATA_BUFFER friendlyNameBuffer = new NativeMethods.DATA_BUFFER();
							string strTempFriendName = "";
							// Try by friendly name first.
							if (NativeMethods.SetupDiGetDeviceRegistryProperty(hDevInfoList,
																			   ref devInfoData,
																			   NativeMethods.RegPropertyType.
																				   SPDRP_FRIENDLYNAME,
																			   null,
																			   ref friendlyNameBuffer,
																			   Marshal.SizeOf(friendlyNameBuffer),
																			   ref requiredLength) == 0)
							{
								// Try by device description if friendly name fails.
								//dev.FriendName = NativeMethods.SetupDiGetDeviceRegistryProperty(hDevInfoList,
								strTempFriendName = NativeMethods.SetupDiGetDeviceRegistryProperty(
																					hDevInfoList,
																					ref devInfoData,
																					NativeMethods.RegPropertyType.SPDRP_DEVICEDESC,
																					null,
																					ref friendlyNameBuffer,
																					Marshal.SizeOf(friendlyNameBuffer),
																					ref requiredLength) == 0
																		? deviceInterfaceDetailData.DevicePath
																		: friendlyNameBuffer.Buffer;
							}

							if (wDevNum == (UInt32)yPortIndex)
							{
								SymbolicLinkName = deviceInterfaceDetailData.DevicePath;
								FriendName = strTempFriendName;
							}
							wDevNum++;
							//strDevLinkName[wDevNum - 1] = dev.m_strFriendName;
						}
					}		//if (bPresent)
					else
					{
						ErrorCode = NativeMethods.GetLastError();
						if (ErrorCode == NativeMethods.ERROR_NO_MORE_ITEMS)
						{
							if (i == 0)
							{
								ErrorCode = LibErrorCode.IDS_ERR_I2C_INVALID_HANDLE;
							}
							break;
						}
					}
				}		//for (int i = 0; i < CCommunicateManager.MAX_COMM_DEVICES; i++)

				bPresent = NativeMethods.SetupDiDestroyDeviceInfoList(hDevInfoList);
				if (!bPresent)
				{
					ErrorCode = LibErrorCode.IDS_ERR_MGR_UNABLE_LOAD_FUNCTION;
				}
			}
			else		//if (hDevInfoList != 0)
			{
				ErrorCode = LibErrorCode.IDS_ERR_I2C_INVALID_HANDLE;
			}
			iDevNum = (Int16)wDevNum;
			DeviceNumber = (Int16)wDevNum;

			return bPresent;
		}

		// <summary>
		// Set up Configuration value through driver command 0x20
		// </summary>
		// <param name="wFlag">configure value</param>
		// <returns>true: operation successfully; false: operation failed</returns>
		private bool O2I2CSetConfiguration(ref UInt16 wFlag)
		{
			bool bReturn = false;

			ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
			m_SendBuffer[0] = 0x20;
			m_SendSize = 3;
			//copy wFlag[0] to m_SendBuffer[1], copy 2 bytes
			Buffer.BlockCopy(BitConverter.GetBytes(wFlag), 0, m_SendBuffer, 1, 2);
			bReturn = O2I2CControlCommand();
			if (bReturn)
			{
				wFlag = BitConverter.ToUInt16(m_ReceiveBuffer, 1);
			}
			else
			{
				ErrorCode = LibErrorCode.IDS_ERR_I2C_INVALID_COMMAND;
			}

			return bReturn;
		}

		// <summary>
		// Get Configuration value through driver command 0x20
		// </summary>
		// <param name="wFlag">configure value from device</param>
		// <returns>true: operation successfully; false: operation failed</returns>
		private bool O2I2CGetConfiguration(ref UInt16 wFlag)
		{
			bool bReturn = false;

			ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
			m_SendBuffer[0] = 0x27;
			m_SendSize = 1;
			bReturn = O2I2CControlCommand();
			if (bReturn)
			{
				wFlag = BitConverter.ToUInt16(m_ReceiveBuffer, 1);
			}
			else
			{
				ErrorCode = LibErrorCode.IDS_ERR_I2C_INVALID_COMMAND;
			}

			return bReturn;
		}

		// <summary>
		// Toggle flag of O2I2C interface device difined, do not operate unless you know what you are doing
		// </summary>
		// <param name="wPosition">Indicate toggle function</param>
		// <param name="bValue">Indicate toggle value</param>
		// <returns>true: operation successfully; false: operation failed</returns>
		private bool O2I2CToggleFlag(UInt16 wPosition, bool bValue)
		{
			bool bReturn = false;
			UInt16 wFlags = 0;

			//ErrorCode is set up in function seperately
			bReturn = O2I2CGetConfiguration(ref wFlags);
			if(bReturn)
			{
				wFlags = (UInt16)(bValue ? (wFlags | wPosition) : (wFlags & (~wPosition)));
				bReturn = O2I2CSetConfiguration(ref wFlags);
				bReturn = ((wFlags & wPosition) == wPosition);
			}

			return bReturn;
		}

		// <summary>
		// Turn on/off line of O2USBtoI2C adapter
		// </summary>
		// <param name="poweron">true: turn on; false: turn off</param>
		// <returns>true: operation successfully; false: operation failed</returns>
		private bool O2I2CAdapterTargetPower(bool poweron)
		{
			return O2I2CToggleFlag(I2CAdapterFlagPowerUp, poweron);
		}

		// <summary>
		// Connect/Disconnect external slave device to/from the adapter
		// </summary>
		// <param name="connect">true: Connect; false: Disconnect</param>
		// <returns>true: operation successfully; false: operation failed</returns>
		private bool O2I2CAdapterTargetConnect(bool connect)
		{
			return O2I2CToggleFlag(I2CFlagTargetConnect, connect);
		}

		// <summary>
		// Set up/down Pullup resistor of O2USBtoI2C adapter
		// </summary>
		// <param name="pu">true: pullup; false: no pullup</param>
		// <returns>true: operation successfully; false: operation failed</returns>
		private bool O2I2CAdapterPullups(bool pu)
		{
			return O2I2CToggleFlag(I2CAdapterFlagPullUp, pu);
		}

		// <summary>
		// Set up/down Pullup resistor of O2MPT adapter
		// </summary>
		// <param name="pu">true: pullup; false: no pullup</param>
		// <returns>true: operation successfully; false: operation failed</returns>
		private bool O2I2CMPTAdapterPullups(bool pu)
		{
			return O2I2CToggleFlag(MPTAdapterFlagPullUp, pu);
		}

		// <summary>
		// Set up delay time of sending wave of O2USBtoI2C, yDelayTime = 76 is recommanded
		// </summary>
		// <param name="yDelayTime">delay time; 38 = 23us, 76=46us(recommanded)</param>
		// <returns>true: operation successfully; false: operation failed</returns>
		private bool O2I2CSetByteDelay(ref byte yDelayTime)
		{
			bool bReturn = false;

			ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
			m_SendBuffer[0] = 0x2B;
			m_SendSize = 2;
			m_SendBuffer[1] = yDelayTime;
			bReturn = O2I2CControlCommand();
			if (bReturn)
			{
				yDelayTime = m_ReceiveBuffer[2];
			}
			else
			{
				ErrorCode = LibErrorCode.IDS_ERR_I2C_INVALID_COMMAND;
			}

			return bReturn;
		}

		// <summary>
		// ReadSingle function, try to communicate with Slave Address (yDataIn[0] indicate) device and read command index (data) only
		// </summary>
		// <param name="yDataIn">buffer of input data, write to interface device byte by byte sequentially</param>
		// <param name="yDataOut">buffer of output data</param>
		// <param name="wDataOutLength">output value indicate the number of output data</param>
		// <param name="wDataInLength">indicate number to read, should be 1</param>
		// <returns>true: operation successful; false: operation failed</returns>
		private bool O2I2CReadSingle(ref byte[] yDataIn, ref byte[] yDataOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 0)
		{
			bool bReturn = false;

			m_SendBuffer[0] = 0x25;
			m_SendSize = 7;
			m_SendBuffer[1] = (byte)(yDataIn[0] >> 1);
			m_SendBuffer[2] = 0x00;								//HIByte of slave address, it should be 0x00, I2C address is byte type
			m_SendBuffer[3] = 2;									//length of read
			m_SendBuffer[4] = O2_I2C_NO_FLAGS;
			m_SendBuffer[5] = 0;									//length of write
			m_SendBuffer[6] = yDataIn[1];
			bReturn = O2I2CControlCommand();
			if (bReturn)
			{
				if (m_ReceiveSize >= 5)
				{
					//(M130118)As Guo request, skip I2C addr and RegIndex saving
					wDataOutLength = (UInt16)(m_ReceiveSize - 5);
					if (wDataOutLength != 1)
					{
						ErrorCode = LibErrorCode.IDS_ERR_I2C_INVALID_HARDWARE;		//should not happen
						return false;
					}
					//yDataOut[0] = (byte)(m_ReceiveBuffer[3] << 1);
					//yDataOut[1] = yDataIn[1];
					//Buffer.BlockCopy(m_ReceiveBuffer, 5, yDataOut, 2, wDataOutLength);
					//wDataOutLength = 1;
					Buffer.BlockCopy(m_ReceiveBuffer, 5, yDataOut, 0, wDataOutLength);
					//(E130118)
					ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
				}
				else
				{
					ErrorCode = LibErrorCode.IDS_ERR_I2C_BUS_ERROR;
					return false;
				}
			}
			else
			{
				//timeout error code will be set in O2I2CControlCommand()
				//ErrorCode = LibErrorCode.IDS_ERR_I2C_BB_TIMEOUT;
				wDataOutLength = 0;
			}

			return bReturn;
		}

		// <summary>
		// ReadByte function, try to communicate with Slave Address (yDataIn[0] indicate) device and read command index (yDataIn[1] indicate) a byte
		// </summary>
		// <param name="yDataIn">buffer of input data, write to interface device byte by byte sequentially</param>
		// <param name="yDataOut">buffer of output data</param>
		// <param name="wDataOutLength">output value indicate the number of output data</param>
		// <param name="wDataInLength">indicate number to read, should be 1</param>
		// <returns>true: operation successful; false: operation failed</returns>
		private bool O2I2CReadByte(ref byte[] yDataIn, ref byte[] yDataOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 1, UInt16 wDataInWrite = 1)
		{
			bool bReturn = false;

			m_SendBuffer[0] = 0x25;
			//m_SendSize = 7;
			m_SendSize = 6 + wDataInWrite;
			m_SendBuffer[1] = (byte)(yDataIn[0] >> 1);
			m_SendBuffer[2] = 0x00;								//HIByte of slave address, it should be 0x00, I2C address is byte type
			m_SendBuffer[3] = 2;									//length of read
			m_SendBuffer[4] = O2_I2C_NO_FLAGS;
			//m_SendBuffer[5] = 1;									//length of write
			//m_SendBuffer[6] = yDataIn[1];
			m_SendBuffer[5] = (Byte)wDataInWrite;
			for (int i = 0; i < wDataInWrite; i++)
			{
				m_SendBuffer[6 + i] = yDataIn[1 + i];
			}

			bReturn = O2I2CControlCommand();
			if (bReturn)
			{
				if (m_ReceiveSize >= 5)
				{
					//(M130118)As Guo request, skip I2C addr and RegIndex saving
					wDataOutLength = (UInt16)(m_ReceiveSize - 5);
					if (wDataOutLength != wDataInLength)
					{
						ErrorCode = LibErrorCode.IDS_ERR_I2C_INVALID_HARDWARE;		//should not happen
						return false;
					}
					//yDataOut[0] = (byte)(m_ReceiveBuffer[3] << 1);
					//yDataOut[1] = yDataIn[1];
					//Buffer.BlockCopy(m_ReceiveBuffer, 5, yDataOut, 2, wDataOutLength);
					Buffer.BlockCopy(m_ReceiveBuffer, 5, yDataOut, 0, wDataOutLength);
					//(E130118)
					ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
				}
				else
				{
					ErrorCode = LibErrorCode.IDS_ERR_I2C_BUS_ERROR;
					return false;
				}
			}
			else
			{
				//timeout error code will be set in O2I2CControlCommand()
//				ErrorCode = LibErrorCode.IDS_ERR_I2C_BB_TIMEOUT;
				wDataOutLength = 0;
			}

			return bReturn;
		}

		// <summary>
		// ReadWord function, try to communicate with Slave Address (yDataIn[0] indicate) device and read command index (yDataIn[1] indicate) 2 bytes
		// </summary>
		// <param name="yDataIn">buffer of input data, write to interface device byte by byte sequentially</param>
		// <param name="yDataOut">buffer of output data</param>
		// <param name="wDataOutLength">output value indicate the number of output data</param>
		// <param name="wDataInLength">indicate number to read, should be 2</param>
		// <returns>true: operation successful; false: operation failed</returns>
		private bool O2I2CReadWord(ref byte[] yDataIn, ref byte[] yDataOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 1, UInt16 wDataInWrite = 1)
		{
			bool bReturn = false;

			m_SendBuffer[0] = 0x25;
//			m_SendSize = 7;
			m_SendSize = 6 + wDataInWrite;
			m_SendBuffer[1] = (byte)(yDataIn[0] >> 1);
			m_SendBuffer[2] = 0x00;								//HIByte of slave address, it should be 0x00, I2C address is byte type
			m_SendBuffer[3] = 3;
			m_SendBuffer[4] = O2_I2C_NO_FLAGS;
			//m_SendBuffer[5] = 1;
			//m_SendBuffer[6] = yDataIn[1];
			m_SendBuffer[5] = (Byte)wDataInWrite;
			for (int i = 0; i < wDataInWrite; i++)
			{
				m_SendBuffer[6 + i] = yDataIn[1 + i];
			}
			bReturn = O2I2CControlCommand();
			if (bReturn)
			{
				if (m_ReceiveSize >= 5)
				{
					//(M130118)As Guo request, skip I2C addr and RegIndex saving
					wDataOutLength = (UInt16)(m_ReceiveSize - 5);
					if (wDataOutLength != wDataInLength)
					{
						ErrorCode = LibErrorCode.IDS_ERR_I2C_INVALID_HARDWARE;		//should not happen
						return false;
					}
					//yDataOut[0] = (byte)(m_ReceiveBuffer[3] << 1);
					//yDataOut[1] = yDataIn[1];
					//Buffer.BlockCopy(m_ReceiveBuffer, 5, yDataOut, 2, wDataOutLength);
					Buffer.BlockCopy(m_ReceiveBuffer, 5, yDataOut, 0, wDataOutLength);
					//(E130118)
					ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
				}
				else
				{
					ErrorCode = LibErrorCode.IDS_ERR_I2C_BUS_ERROR;
					return false;
				}
			}
			else
			{
				//timeout error code will be set in O2I2CControlCommand()
//				ErrorCode = LibErrorCode.IDS_ERR_I2C_PIN_TIMEOUT;
				wDataOutLength = 0;
			}

			return bReturn;
		}

		// <summary>
		// ReadBlcok function, try to communicate with Slave Address (yDataIn[0] indicate) device and read command index (yDataIn[1] indicate) couple bytes
		// </summary>
		// <param name="yDataIn">buffer of input data, write to interface device byte by byte sequentially</param>
		// <param name="yDataOut">buffer of output data</param>
		// <param name="wDataOutLength">output value indicate the number of output data</param>
		// <param name="wDataInLength">indicate number to read, should be around 3~31</param>
		// <returns>true: operation successful; false: operation failed</returns>
		private bool O2I2CReadBlock(ref byte[] yDataIn, ref byte[] yDataOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 1, UInt16 wDataInWrite = 1)
		{
			bool bReturn = false;

			m_SendBuffer[0] = 0x25;
			//m_SendSize = 7;
			m_SendSize = 6 + wDataInWrite;
			m_SendBuffer[1] = (byte)(yDataIn[0] >> 1);
			m_SendBuffer[2] = 0x00;								//HIByte of slave address, it should be 0x00, I2C address is byte type
			m_SendBuffer[3] = (byte)(wDataInLength + 1);
			m_SendBuffer[4] = O2_I2C_SMB_PEC_ENABLE;			//special in Block communication
			//m_SendBuffer[5] = 1;
            //m_SendBuffer[6] = yDataIn[1];
			m_SendBuffer[5] = (Byte)wDataInWrite;
			for (int i = 0; i < wDataInWrite; i++)
            {
                m_SendBuffer[6+i] = yDataIn[1+i];
            }
            /*
             0x25, <I2C address low>, < I2C address hi>, <length of read>,  <flags>,<Command to send>,<Command1>…<Command N>. 
             */

            bReturn = O2I2CControlCommand();
			if (bReturn == true)
			{
				if (m_ReceiveSize >= 5)
				{
					//(M130118)As Guo request, skip I2C addr and RegIndex saving
					wDataOutLength = (UInt16)(m_ReceiveSize - 5);
					if (wDataOutLength < wDataInLength)		//(M13125)As Guo test, wDataOutLength will be = wDataInLength
					{
						ErrorCode = LibErrorCode.IDS_ERR_I2C_INVALID_HARDWARE;		//should not happen
						return false;
					}
					//yDataOut[0] = (byte)(m_ReceiveBuffer[3] << 1);
					//yDataOut[1] = yDataIn[1];
					//Buffer.BlockCopy(m_ReceiveBuffer, 5, yDataOut, 2, wDataOutLength);
					Buffer.BlockCopy(m_ReceiveBuffer, 5, yDataOut, 0, wDataOutLength);
					//(E130118)
					ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
				}
				else
				{
					ErrorCode = LibErrorCode.IDS_ERR_I2C_BUS_ERROR;
					return false;
				}
			}
			else
			{
				//timeout error code will be set in O2I2CControlCommand()
//				ErrorCode = LibErrorCode.IDS_ERR_I2C_PROTOCOL_TIMEOUT;
				wDataOutLength = 0;
			}

			return bReturn;
		}

		// <summary>
		// WriteSingle function, try to communicate with Slave Address (yDataIn[0] indicate) device and write command index (byte) only
		// </summary>
		// <param name="yDataIn">buffer of input data, write to interface device byte by byte sequentially</param>
		// <param name="yDataOut">buffer of output data, useless</param>
		// <param name="wDataOutLength">output value indicate the number of output data</param>
		// <param name="wDataInLength">indicate number to read, should be 0</param>
		// <returns>true: operation successful; false: operation failed</returns>
		private bool O2I2CWriteSingle(ref byte[] yDataIn, ref byte[] yDataOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 0)
		{
			bool bReturn = false;

			m_SendBuffer[0] = 0x26;
			m_SendSize = 7;
			m_SendBuffer[1] = (byte)(yDataIn[0] >> 1);
			m_SendBuffer[2] = 0x00;								//HIByte of slave address, it should be 0x00, I2C address is byte type
			m_SendBuffer[3] = 1;
			m_SendBuffer[4] = O2_I2C_NO_FLAGS;
			m_SendBuffer[5] = yDataIn[1];
			m_SendBuffer[6] = 0x00;
			bReturn = O2I2CControlCommand();
			wDataOutLength = 0;
			/*
			if ((bReturn == true) && (m_ReceiveSize >= 5))
			{
				wDataOutLength = (UInt16)(m_ReceiveSize - 5);
				yDataOut[0] = (byte)(m_ReceiveBuffer[3] << 1);
				yDataOut[1] = yDataIn[1];
				Buffer.BlockCopy(m_ReceiveBuffer, 5, yDataOut, 2, wDataOutLength);
			}
			else
			{
				wDataOutLength = 0;
			}
			 * */
			if (bReturn)
				ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
			//timeout error code will be set in O2I2CControlCommand()
			//			else
			//				ErrorCode = LibErrorCode.IDS_ERR_I2C_BB_TIMEOUT;

			return bReturn;
		}

		// <summary>
		// WriteByte function, try to communicate with Slave Address (yDataIn[0] indicate) device and write command index (yDataIn[1] indicate) a byte data
		// </summary>
		// <param name="yDataIn">buffer of input data, write to interface device byte by byte sequentially</param>
		// <param name="yDataOut">buffer of output data, useless</param>
		// <param name="wDataOutLength">output value indicate the number of output data</param>
		// <param name="wDataInLength">indicate number to read, should be 1</param>
		// <returns>true: operation successful; false: operation failed</returns>
		private bool O2I2CWriteByte(ref byte[] yDataIn, ref byte[] yDataOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 1)
		{
			bool bReturn = false;

			m_SendBuffer[0] = 0x26;
			m_SendSize = 7;
			m_SendBuffer[1] = (byte)(yDataIn[0] >> 1);
			m_SendBuffer[2] = 0x00;								//HIByte of slave address, it should be 0x00, I2C address is byte type
			m_SendBuffer[3] = 2;
			m_SendBuffer[4] = O2_I2C_NO_FLAGS;
			m_SendBuffer[5] = yDataIn[1];
			m_SendBuffer[6] = yDataIn[2];
			bReturn = O2I2CControlCommand();
			wDataOutLength = 0;
			/*
			if ((bReturn == true) && (m_ReceiveSize >= 5))
			{
				wDataOutLength = (UInt16)(m_ReceiveSize - 5);
				yDataOut[0] = (byte)(m_ReceiveBuffer[3] << 1);
				yDataOut[1] = yDataIn[1];
				Buffer.BlockCopy(m_ReceiveBuffer, 5, yDataOut, 2, wDataOutLength);
			}
			else
			{
				wDataOutLength = 0;
			}
			 * */
			if (bReturn)
				ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
			//timeout error code will be set in O2I2CControlCommand()
//			else
//				ErrorCode = LibErrorCode.IDS_ERR_I2C_BB_TIMEOUT;

			return bReturn;
		}

		// <summary>
		// WriteWord function, try to communicate with Slave Address (yDataIn[0] indicate) device and write command index (yDataIn[1] indicate) 2 bytes data
		// </summary>
		// <param name="yDataIn">buffer of input data, write to interface device byte by byte sequentially</param>
		// <param name="yDataOut">buffer of output data, useless</param>
		// <param name="wDataOutLength">output value indicate the number of output data</param>
		// <param name="wDataInLength">indicate number to read, should be 2</param>
		// <returns>true: operation successful; false: operation failed</returns>
		private bool O2I2CWriteWord(ref byte[] yDataIn, ref byte[] yDataOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 1)
		{
			bool bReturn = false;

			m_SendBuffer[0] = 0x26;
			m_SendSize = 8;
			m_SendBuffer[1] = (byte)(yDataIn[0] >> 1);
			m_SendBuffer[2] = 0x00;								//HIByte of slave address, it should be 0x00, I2C address is byte type
			m_SendBuffer[3] = 3;
			m_SendBuffer[4] = O2_I2C_NO_FLAGS;
			m_SendBuffer[5] = yDataIn[1];
			m_SendBuffer[6] = yDataIn[2];
			m_SendBuffer[7] = yDataIn[3];
			bReturn = O2I2CControlCommand();
			wDataOutLength = 0;
			if (bReturn)
				ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
			//timeout error code will be set in O2I2CControlCommand()
//			else
//				ErrorCode = LibErrorCode.IDS_ERR_I2C_PIN_TIMEOUT;

			return bReturn;
		}

		// <summary>
		// WriteBlock function, try to communicate with Slave Address (yDataIn[0] indicate) device and write command index (yDataIn[1] indicate) couple bytes data
		// </summary>
		// <param name="yDataIn">buffer of input data, write to interface device byte by byte sequentially</param>
		// <param name="yDataOut">buffer of output data, useless</param>
		// <param name="wDataOutLength">output value indicate the number of output data</param>
		// <param name="wDataInLength">indicate number to read, should be 3~31</param>
		// <returns>true: operation successful; false: operation failed</returns>
		private bool O2I2CWriteBlock(ref byte[] yDataIn, ref byte[] yDataOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 1)
		{
			bool bReturn = false;

			m_SendBuffer[0] = 0x26;
			m_SendSize = (byte)(wDataInLength + 6);
			m_SendBuffer[1] = (byte)(yDataIn[0] >> 1);
			m_SendBuffer[2] = 0x00;								//HIByte of slave address, it should be 0x00, I2C address is byte type
			m_SendBuffer[3] = (byte)(wDataInLength + 1);
			m_SendBuffer[4] = O2_I2C_SMB_PEC_ENABLE;
			m_SendBuffer[5] = yDataIn[1];
			//m_SendBuffer[6] = yDataIn[2];
			if (yDataIn.GetLength(0) < wDataInLength + 2)
			{
				ErrorCode = LibErrorCode.IDS_ERR_I2C_INVALID_BUFFER;
				return false;
			}
			Buffer.BlockCopy(yDataIn, 2, m_SendBuffer, 6, wDataInLength);
			bReturn = O2I2CControlCommand();
			wDataOutLength = 0;
			if (bReturn)
				ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
			//timeout error code will be set in O2I2CControlCommand()
//			else
//				ErrorCode = LibErrorCode.IDS_ERR_I2C_PROTOCOL_TIMEOUT;

			return bReturn;
		}

		// <summary>
		// End Transaction function
		// </summary>
		// <param name="result"></param>
		private void PortStreamEndOfTransaction(IAsyncResult result)
		{
			ManualResetEvent e = result.AsyncState as ManualResetEvent;
			if (e != null) e.Set();
		}

		// <summary>
		// Send command through driver function call
		// </summary>
		// <returns>true: operation successful; false: operation failed</returns>
		private bool O2I2CControlCommand()
		{
			bool bReturn = false;
			IAsyncResult aRes = null;

			//wait semaphore timeout
			if (!m_Locker.WaitOne(1500))
			{
				ErrorCode = LibErrorCode.IDS_ERR_I2C_BB_TIMEOUT;
				return bReturn;
			}

			ManualResetEvent streamDoneEvent = new ManualResetEvent(false);
			const int writeLength = 64;

			ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
			streamDoneEvent.Reset();
			try
			{
				aRes = DeviceHandler.BeginWrite(m_SendBuffer, 0, m_SendSize, PortStreamEndOfTransaction, streamDoneEvent);
			}
			catch (Exception)
			{
			}
			finally
			{
				//if driver timeout, return false and release semaphore
				if (!streamDoneEvent.WaitOne(1000, false))
				{
					ErrorCode = LibErrorCode.IDS_ERR_I2C_EPP_TIMEOUT;
					try
					{
						m_Locker.Release();
					}
					catch (Exception)
					{
					}
					bReturn = false;
				};
				if ((DeviceHandler != null) && (aRes != null))
				{
				}
				else
				{
					if (DeviceHandler != null)
					{
						DeviceHandler.Close();
					}
					DeviceHandler = null;
					m_ReceiveSize = 0;
					bReturn = false;
				}
			}

			//if driver timeout, return false and release semaphore
			if (!streamDoneEvent.WaitOne(1000, false))
			{
				ErrorCode = LibErrorCode.IDS_ERR_I2C_EPP_TIMEOUT;
				try
				{
					m_Locker.Release();
				}
				catch (Exception)
				{
				}
				return false;
			}
//			Thread.Sleep(10);
			streamDoneEvent.Reset();
			try
			{
				bReturn = true;
				aRes = DeviceHandler.BeginRead(m_ReceiveBuffer, 0, writeLength, PortStreamEndOfTransaction, streamDoneEvent);
			}
			catch (Exception)
			{
			}
			finally
			{
				//if driver timeout, return false and release semaphore
				if (!streamDoneEvent.WaitOne(1000, false))
				{
					ErrorCode = LibErrorCode.IDS_ERR_I2C_EPP_TIMEOUT;
					try
					{
						m_Locker.Release();
					}
					catch (Exception)
					{
					}
					bReturn = false;
				};
				if ((DeviceHandler != null) && (aRes != null))
				{
					try
					{
						m_ReceiveSize = DeviceHandler.EndRead(aRes);
						if ((m_SendBuffer[0] == 0x25) || (m_SendBuffer[0] == 0x26))		//(M170927)Francis, only read/write need to check last error
						{
							if (!GetI2CLastErr(m_SendBuffer[0], m_ReceiveBuffer))
							{
								try
								{
									m_Locker.Release();
								}
								catch (Exception)
								{
								}
								bReturn = false;
							}
						}
						else
						{
							bReturn = true;
						}
					}
					catch (Exception)
					{
					}
				}
				else
				{
					if (DeviceHandler != null)
					{
						DeviceHandler.Close();
					}
					DeviceHandler = null;
					m_ReceiveSize = 0;
					bReturn = false;
				}
			}
//			if (m_ReceiveSize != 0)
			if(bReturn)
			{
				try
				{
					m_Locker.Release();
				}
				catch (Exception)
				{
				}
//				bReturn = true;
			}

			return bReturn;
		}

		// <summary>
		// Set I2C transaction frequency
		// </summary>
		// <param name="wFrequence">input of I2C frequency value</param>
		// <returns>true: operation successful; false: operation failed</returns>
		private bool SetFrequency(UInt16 wFrequence)
		{
			bool bReturn = false;

			//O2 USBtoI2C supports 63~400 KHz transaction, other value is out of range
			if ((wFrequence < 63) || (wFrequence > 400))
			{
				ErrorCode = LibErrorCode.IDS_ERR_I2C_CFG_FREQUENCY_LIMIT;
				return bReturn;
			}
			m_SendBuffer[0] = 0x22;
			m_SendSize = 3;
			m_ReceiveSize = 2;
			Buffer.BlockCopy(BitConverter.GetBytes(wFrequence), 0, m_SendBuffer, 1, 2);
			bReturn = O2I2CControlCommand();
			if (bReturn)
			{
				//I2CFrequence = SharedFormula.MAKEWORD(m_ReceiveBuffer[3], m_ReceiveBuffer[4]); //BitConverter.ToUInt16(m_ReceiveBuffer, 3);
				I2CFrequence = wFrequence;
				ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
				bReturn = true;
			}
			else
			{
				I2CFrequence = 0;
				ErrorCode = LibErrorCode.IDS_ERR_I2C_CFG_FREQUENCY_ERROR;
				bReturn = false;
			}

			return bReturn;
		}

		//<summary>
		// Get I2C transaction frequency
		// </summary>
		// <param name="wFrequence">output of I2C frequency value</param>
		// <returns>true: operation successful; false: operation failed</returns>
		private bool GetFrequency(ref UInt16 wFrequence)
		{
			bool bReturn = true;

			if (DeviceHandler != null)
			{
				wFrequence = I2CFrequence;
			}
			else
			{
				wFrequence = 0;
				bReturn = false;
			}

			return bReturn;
		}

		#endregion
	}
}
