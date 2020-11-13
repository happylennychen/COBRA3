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

namespace Cobra.Communication.SPI
{
	public class CO2USBSPIAdapter : CInterfaceSPI
	{
		#region O2USBtoSPI constant member and constant value definition

		private static readonly Guid O2USBSPIGuid = new Guid("{95b4dec8-d7a6-465d-b797-6a5a66c9205f}");

		//O2 I2C Pullups and O2 I2C Power
		private const byte I2CAdapterFlagPowerUp = 0x01;
		private const byte I2CAdapterFlagPullUp = 0x02;
		private const byte MPTAdapterFlagPullUp = 0x03;
		private const byte I2CFlagTargetConnect = 0x04;

		private const byte SPIPatternLength = 0x10;
		private const byte SPIOneTimeMax = 0x20;

		#endregion


		public CO2USBSPIAdapter()
		{
			CloseDevice();
			m_Locker = new Semaphore(0, 1);
		}

		#region Public Method, Override CInterfaceBase and CInterfaceI2C 2 mother class

		public static Guid GetGuid()
		{
			return O2USBSPIGuid;
		}

		public override bool OpenDevice(ref Int16 iPortNum, byte yPortIndex = 0)
		{
			bool bReturn = false;

			//if input index of target device is out of supported range, error report
			if (((int)yPortIndex < 0) || ((int)yPortIndex >= CCommunicateManager.MAX_COMM_DEVICES))
			{
				ErrorCode = LibErrorCode.IDS_ERR_SPI_INVALID_PARAMETER;
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
							bReturn = O2I2CAdapterTargetConnect(true);
							bReturn = O2I2CAdapterTargetPower(true);
							bReturn = O2I2CAdapterPullups(true);
							bReturn = O2SPISetBaudRate(1000);									//default baud rate is 1000K
							//bReturn = ResetInf();
						}		//if (!sfh.IsInvalid)
						else
						{
							ErrorCode = LibErrorCode.IDS_ERR_SPI_INVALID_HANDLE;
							return false;
						}
					}		//if ((byte)i == yPortIndex)
				}		//for (int i = 0; i < DeviceNumber; i++)

				//not found yPortIndex target device, report error
				if (DeviceHandler == null)
				{
					ErrorCode = LibErrorCode.IDS_ERR_SPI_INVALID_INDEX;
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
				bReturn = O2I2CAdapterTargetConnect(true);
				bReturn = O2I2CAdapterTargetPower(true);
				bReturn = O2I2CAdapterPullups(true);
				bReturn = O2SPISetBaudRate(1000);									//default baud rate is 1000K
				//bReturn = ResetInf();
			}		//if (!sfh.IsInvalid)
			else
			{
				ErrorCode = LibErrorCode.IDS_ERR_SPI_INVALID_HANDLE;
				return false;
			}

			//not found yPortIndex target device, report error
			if (DeviceHandler == null)
			{
				ErrorCode = LibErrorCode.IDS_ERR_SPI_INVALID_INDEX;
				return false;
			}

			//if all successful, set successful error code and release semaphore
			if (bReturn)
			{
				ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
			}

			return bReturn;
		}

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

		public override bool ReadDevice(ref byte[] yDataIn, ref byte[] yDataOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 1, UInt16 wDataInWrite = 1)
		{
			UInt16 u16Len, u16Indx, u16Patt = SPIPatternLength;
			byte[] yInBuffer = new byte[256];
			byte[] yOutBuffer = new byte[256];
			bool bRet = false;
			//check yDataIn array, must have Slave Address and Command Index, 2 byte values
			if (yDataIn.GetLength(0) < 2)
			{
				ErrorCode = LibErrorCode.IDS_ERR_SPI_INVALID_LENGTH;
				return false;
			}

			//yDataIn length should be equal or bigger that wDataInLength + 2 (Slave Address and Command Index)
			if (yDataIn.GetLength(0) < wDataInLength + 2)
			{
				ErrorCode = LibErrorCode.IDS_ERR_SPI_INVALID_LENGTH;
				return false;
			}

			System.Array.Clear(yDataOut, 0, yDataOut.GetLength(0));
			if (wDataOutLength == 0)
			{
				ErrorCode = LibErrorCode.IDS_ERR_SPI_INVALID_LENGTH;
				return false;
			}
			wDataInLength += 2;		//add command and register
			if (DeviceHandler != null)
			{
				if (wDataInLength <= SPIOneTimeMax)		//<= 0x20
				{
					return O2SPIAccess(ref yDataIn, ref yDataOut, 2, ref wDataOutLength, wDataInLength);
				}
				else
				{
					u16Len = wDataInLength;
					u16Indx = 0;
					while (u16Len > 0)
					{
						if (u16Len > SPIPatternLength)		//> 0x10
						{
							Buffer.BlockCopy(yDataIn, u16Indx, yInBuffer, 0, SPIPatternLength);
							bRet = O2SPIAccessBuffer(ref yInBuffer, ref yOutBuffer, 0, 0, ref u16Patt, u16Indx);
							if (!bRet)
								return bRet;
							u16Indx += u16Patt;
							u16Len -= u16Patt;
						}
						else
						{
							Buffer.BlockCopy(yDataIn, u16Indx, yInBuffer, 0, u16Len);
							bRet = O2SPIAccessBuffer(ref yInBuffer, ref yOutBuffer, 1, 1, ref u16Len, u16Indx);
							if (!bRet)
								return bRet;
							u16Len = 0;
						}
					}

					bRet = O2SPIAccessLargeData(ref yInBuffer, ref yOutBuffer, 0, ref wDataOutLength, wDataInLength);

					u16Len = wDataInLength;
					u16Indx = 0;
					while (u16Len > 0)
					{
						if (u16Len > SPIPatternLength)		//> 0x10
						{
							bRet = O2SPIAccessBuffer(ref yInBuffer, ref yOutBuffer, 1, 1, ref u16Patt, u16Indx);
							if (!bRet)
								return bRet;
							Buffer.BlockCopy(yOutBuffer, 7, yDataOut, u16Indx, SPIPatternLength);
							u16Indx += u16Patt;
							u16Len -= u16Patt;
						}
						else
						{
							bRet = O2SPIAccessBuffer(ref yInBuffer, ref yOutBuffer, 1, 1, ref u16Len, u16Indx);
							if (!bRet)
								return bRet;
							Buffer.BlockCopy(yOutBuffer, 7, yDataOut, u16Indx, u16Len);
							u16Len = 0;
						}
					}
					ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
					return true;
				}	//if (wDataInLength <= SPIOneTimeMax)		//<= 0x20
			}
			else
			{
				ErrorCode = LibErrorCode.IDS_ERR_SPI_INVALID_HANDLE;
				return false;
			}
		}

		// <summary>
		//
		// </summary>
		// <param name="yDataIn">buffer of input data, write to interface device byte by byte sequentially, first 2 bytes must be target command then target register</param>
		// <param name="yDataOut">buffer of output data, useless</param>
		// <param name="wDataOutLength">output value indicate the number of output data, if 0, will not read, otherwise will read wDataOutLength number of byte</param>
		// <param name="wDataInLength">indicate number of data to read, excluding targer command  and target register</param>
		// <returns>true: operation successful; false: operation failed</returns>
		public override bool WriteDevice(ref byte[] yDataIn, ref byte[] yDataOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 1)
		{
			UInt16 u16Len, u16Indx, u16Patt = SPIPatternLength;
			byte[] yInBuffer = new byte[256]; 
			byte[] yOutBuffer = new byte[256];
			bool bRet = false;
			//check yDataIn array, must have Slave Address and Command Index, 2 byte values
			if (yDataIn.GetLength(0) < 2)
			{
				ErrorCode = LibErrorCode.IDS_ERR_SPI_INVALID_LENGTH;
				return false;
			}

			//yDataIn length should be equal or bigger that wDataInLength + 2 (Slave Address and Command Index)
			if (yDataIn.GetLength(0) < wDataInLength + 2)
			{
				ErrorCode = LibErrorCode.IDS_ERR_SPI_INVALID_LENGTH;
				return false;
			}

			System.Array.Clear(yDataOut, 0, yDataOut.GetLength(0));
			//wDataOutLength = 0;	//force wDataOutLength as 0
			wDataInLength += 2;		//add command and register
			if (DeviceHandler != null)
			{
				if (wDataInLength <= SPIOneTimeMax)		//<= 0x20
				{
					//write length should include target I2C address an 
					return O2SPIAccess(ref yDataIn, ref yDataOut, 0, ref wDataOutLength, wDataInLength);
				}
				else
				{
					u16Len = wDataInLength;
					u16Indx = 0;
					while (u16Len > 0)
					{
						if (u16Len > SPIPatternLength)		//> 0x10
						{
							//u16Patt initialize as SPIPatternLength, 0x10
							Buffer.BlockCopy(yDataIn, u16Indx, yInBuffer, 0, SPIPatternLength);
							bRet = O2SPIAccessBuffer(ref yInBuffer, ref yOutBuffer, 0, 0, ref u16Patt, u16Indx);
							if (!bRet)
								return bRet;
							u16Indx += u16Patt;
							u16Len -= u16Patt;
						}
						else
						{
							Buffer.BlockCopy(yDataIn, u16Indx, yInBuffer, 0, u16Len);
							bRet = O2SPIAccessBuffer(ref yInBuffer, ref yOutBuffer, 0, 0, ref u16Len, u16Indx);
							if (!bRet)
								return bRet;
							u16Len = 0;
						}
					}

					bRet = O2SPIAccessLargeData(ref yInBuffer, ref yOutBuffer, 0, ref wDataOutLength, wDataInLength);

					u16Len = wDataOutLength;
					u16Indx = 0;
					while (u16Len > 0)
					{
						if (u16Len > SPIPatternLength)		//> 0x10
						{
							bRet = O2SPIAccessBuffer(ref yInBuffer, ref yOutBuffer, 1, 1, ref u16Patt, u16Indx);
							if (!bRet)
								return bRet;
							Buffer.BlockCopy(yOutBuffer, 7, yDataOut, u16Indx, SPIPatternLength);
							u16Indx += u16Patt;
							u16Len -= u16Patt;
						}
						else
						{
							bRet = O2SPIAccessBuffer(ref yInBuffer, ref yOutBuffer, 1, 1, ref u16Len, u16Indx);
							if (!bRet)
								return bRet;
							Buffer.BlockCopy(yOutBuffer, 7, yDataOut, u16Indx, u16Len);
							u16Len = 0;
						}
					}
					ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
					return true;
				}	//if (wDataInLength <= SPIOneTimeMax)		//<= 0x20
			}
			else
			{
				ErrorCode = LibErrorCode.IDS_ERR_SPI_INVALID_HANDLE;
				return false;
			}
		}
/*
		public override bool ConfigureDevice(ref byte[] yCfgInOut, bool bRW = false)
		{
			bool bRet = false;
			byte ySPIFlag = 0;

			if (bRW)
			{
				ySPIFlag = yCfgInOut[0];
				bRet = O2SPISetConfiguration(ref ySPIFlag);
			}
			else
			{
				bRet = O2SPIGetConfiguration(ref ySPIFlag);
				if (bRet)
				{
					yCfgInOut[0] = ySPIFlag;
				}
			}
			return bRet;
		}
*/
		public override bool ResetInf()
		{
			m_SendBuffer[0] = 0x21;
			m_SendSize = 1;
			return ControlCommand();
			//return true;
		}

		public override bool SetConfigure(List<UInt32> wConfig)
		{
			bool bRet = false;

			bRet = O2SPISetConfiguration((byte)wConfig[0]);
			if (bRet)
			{
				bRet = O2SPISetBaudRate(wConfig[1]);
			}

			return bRet;
		}

		public override bool GetConfigure(ref List<UInt32> wConfig)
		{
			bool bRet = false;
			byte btempsp = 0;
			UInt32 wtempsp = 0;

			bRet = O2SPIGetConfiguration(ref btempsp);
			if (bRet)
			{
				bRet = O2SPIGetBaudRate(ref wtempsp);
				if (bRet)
				{
					wConfig[0] = (UInt16)btempsp;
					wConfig[1] = wtempsp;
				}
			}

			return bRet;
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

		#region Private Method, O2USBtoI2C adapter self method

		private unsafe bool EnumerateO2Adaptor(ref Int16 iDevNum, byte yPortIndex)
		{
			bool bReturn = false;

			bReturn = FindO2Adaptors(ref iDevNum, yPortIndex);

			return bReturn;
		}

		private unsafe bool FindO2Adaptors(ref Int16 iDevNum, byte yPortIndex)
		{
			Guid tempGuid = O2USBSPIGuid;
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
							ErrorCode = LibErrorCode.IDS_ERR_SPI_INVALID_HANDLE;
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
								ErrorCode = LibErrorCode.IDS_ERR_SPI_INVALID_HANDLE;
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
				ErrorCode = LibErrorCode.IDS_ERR_SPI_INVALID_HANDLE;
			}
			iDevNum = (Int16)wDevNum;
			DeviceNumber = (Int16)wDevNum;

			return bPresent;
		}

		private void PortStreamEndOfTransaction(IAsyncResult result)
		{
			ManualResetEvent e = result.AsyncState as ManualResetEvent;
			if (e != null) e.Set();
		}

		private bool ControlCommand()
		{
			bool bReturn = false;
			IAsyncResult aRes = null;

			//wait semaphore timeout
			if (!m_Locker.WaitOne(1500))
			{
				ErrorCode = LibErrorCode.IDS_ERR_SPI_BB_TIMEOUT;
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
					ErrorCode = LibErrorCode.IDS_ERR_SPI_EPP_TIMEOUT;
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
				ErrorCode = LibErrorCode.IDS_ERR_SPI_EPP_TIMEOUT;
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
					ErrorCode = LibErrorCode.IDS_ERR_SPI_EPP_TIMEOUT;
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
			if (bReturn)
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

		private bool O2I2CSetConfiguration(ref UInt16 wFlag)
		{
			bool bReturn = false;

			ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
			m_SendBuffer[0] = 0x20;
			m_SendSize = 3;
			//copy wFlag[0] to m_SendBuffer[1], copy 2 bytes
			Buffer.BlockCopy(BitConverter.GetBytes(wFlag), 0, m_SendBuffer, 1, 2);
			bReturn = ControlCommand();
			if (bReturn)
			{
				wFlag = BitConverter.ToUInt16(m_ReceiveBuffer, 1);
			}
			else
			{
				ErrorCode = LibErrorCode.IDS_ERR_SPI_INVALID_COMMAND;
			}

			return bReturn;
		}

		private bool O2I2CGetConfiguration(ref UInt16 wFlag)
		{
			bool bReturn = false;

			ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
			m_SendBuffer[0] = 0x27;
			m_SendSize = 1;
			bReturn = ControlCommand();
			if (bReturn)
			{
				wFlag = BitConverter.ToUInt16(m_ReceiveBuffer, 1);
			}
			else
			{
				ErrorCode = LibErrorCode.IDS_ERR_SPI_INVALID_COMMAND;
			}

			return bReturn;
		}

		private bool O2I2CToggleFlag(UInt16 wPosition, bool bValue)
		{
			bool bReturn = false;
			UInt16 wFlags = 0;

			//ErrorCode is set up in function seperately
			bReturn = O2I2CGetConfiguration(ref wFlags);
			if (bReturn)
			{
				wFlags = (UInt16)(bValue ? (wFlags | wPosition) : (wFlags & (~wPosition)));
				bReturn = O2I2CSetConfiguration(ref wFlags);
				bReturn = ((wFlags & wPosition) == wPosition);
			}

			return bReturn;
		}

		private bool O2I2CAdapterTargetPower(bool poweron)
		{
			return O2I2CToggleFlag(I2CAdapterFlagPowerUp, poweron);
		}

		private bool O2I2CAdapterTargetConnect(bool connect)
		{
			return O2I2CToggleFlag(I2CFlagTargetConnect, connect);
		}

		private bool O2I2CAdapterPullups(bool pu)
		{
			return O2I2CToggleFlag(I2CAdapterFlagPullUp, pu);
		}

/* combine into O2SPIControlCommand()
		private bool O2SPIWrite()
		{
			bool bReturn = false;

			return bReturn;
		}

		private bool O2SPIRead()
		{
			bool bRet = false;

			return bRet;
		}
*/

		private bool O2SPIControlCommand()
		{
			bool bReturn = false;
			IAsyncResult aRes = null;

			//wait semaphore timeout
			if (!m_Locker.WaitOne(1500))
			{
				ErrorCode = LibErrorCode.IDS_ERR_SPI_BB_TIMEOUT;
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
					ErrorCode = LibErrorCode.IDS_ERR_SPI_EPP_TIMEOUT;
					try
					{
						m_Locker.Release();
					}
					catch (Exception)
					{
					}
					bReturn = false;
				}
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
				ErrorCode = LibErrorCode.IDS_ERR_SPI_EPP_TIMEOUT;
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
					ErrorCode = LibErrorCode.IDS_ERR_SPI_EPP_TIMEOUT;
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
			if (bReturn)
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

		private bool O2SPISetBaudRate(UInt32 wBaudrate)
		{
			bool bReturn = false;

			//O2 USBtoSPI supports 
			//if ((wBaudrate != 125) && (wBaudrate != 250) && (wBaudrate != 500) && (wBaudrate != 1000) &&
				//(wBaudrate != 2000) && (wBaudrate != 4000) && (wBaudrate != 6000) && (wBaudrate != 8000))
			if(wBaudrate > 8000)
			{
				ErrorCode = LibErrorCode.IDS_ERR_SPI_CFG_BAURATE_LIMIT;
				return bReturn;
			}
			m_SendBuffer[0] = 0x43;
			m_SendSize = 3;
			m_ReceiveSize = 2;
			Buffer.BlockCopy(BitConverter.GetBytes(wBaudrate), 0, m_SendBuffer, 1, 2);
			bReturn = O2SPIControlCommand();
			if (bReturn)
			{
				SPIBaudRate = BitConverter.ToUInt16(m_ReceiveBuffer, 1);
				ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
				bReturn = true;
			}
			else
			{
				SPIBaudRate = 0;
				ErrorCode = LibErrorCode.IDS_ERR_SPI_CFG_BAURATE_ERROR;
				bReturn = false;
			}

			return bReturn;
		}

		private bool O2SPIGetBaudRate(ref UInt32 wBaud)
		{
			bool bReturn = true;

			if (DeviceHandler != null)
			{
				wBaud = SPIBaudRate;
			}
			else
			{
				wBaud = 0;
				bReturn = false;
			}

			return bReturn;
		}

		private bool O2SPISetConfiguration(byte yFlag)
		{
			bool bReturn = false;

			ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
			if ((yFlag & 0x08) != 0)
			{
				ErrorCode = LibErrorCode.IDS_ERR_SPI_CFG_CONFIG_VALUE_ERROR;
				return false;
			}
			m_SendBuffer[0] = 0x41;
			//Buffer.BlockCopy(BitConverter.GetBytes(wFlag), 0, m_SendBuffer, 1, 2);
			m_SendBuffer[1] = yFlag;
			m_SendSize = 2;
			bReturn = O2SPIControlCommand();
			if (bReturn)
			{
				//wFlag = BitConverter.ToUInt16(m_ReceiveBuffer, 1);
				ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
			}
			else
			{
				ErrorCode = LibErrorCode.IDS_ERR_SPI_CFG_CONFIG_ERROR;
			}

			return bReturn;
		}

		private bool O2SPIGetConfiguration(ref byte yFlag)
		{
			bool bReturn = false;

			ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
			m_SendBuffer[0] = 0x42;
			m_SendSize = 1;
			bReturn = O2SPIControlCommand();
			if (bReturn)
			{
				yFlag = m_ReceiveBuffer[1];
				ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
			}
			else
			{
				yFlag = 0x00;
				ErrorCode = LibErrorCode.IDS_ERR_SPI_CFG_CONFIG_ERROR;
			}

			return bReturn;
		}

		private bool O2SPIAccess(ref byte[] yDataIn, ref byte[] yDataOut, byte yflag, ref UInt16 wReadLen, UInt16 wWriteLen = 3)
		{
			bool bReturn = false;

			m_SendBuffer[0] = 0x45;
			m_SendBuffer[1] = yflag;
			m_SendBuffer[2] = Convert.ToByte(wReadLen);		//read_len
			m_SendBuffer[3] = Convert.ToByte(wWriteLen);		//wrtie_len
			Buffer.BlockCopy(yDataIn, 0, m_SendBuffer, 4, Convert.ToByte(wWriteLen));
			m_SendSize = m_SendBuffer[3] + 4;
			bReturn = O2SPIControlCommand();
			//wDataOutLength = 0;
			if (bReturn)
			{
				ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
				if ((yflag != 0) || (wReadLen != 0))
				{
					Buffer.BlockCopy(m_ReceiveBuffer, 4, yDataOut, 0, Convert.ToByte(wReadLen));
				}
				else
				{
					Buffer.BlockCopy(m_ReceiveBuffer, 3, yDataOut, 0, Convert.ToByte(wWriteLen));
				}
			}
			//else
			//	ErrorCode = LibErrorCode.IDS_ERR_SPI_BB_TIMEOUT;

			return bReturn;
		}

		private bool O2SPIAccessLargeData(ref byte[] yDataIn, ref byte[] yDataOut, byte yflag, ref UInt16 wReadLen, UInt16 wWriteLen = 3)
		{
			bool bReturn = false;

			//When call this function, we have to set write buffer into SPI write buffer first.
			m_SendBuffer[0] = 0x4A;
			m_SendBuffer[1] = yflag;
			m_SendBuffer[2] = (byte)(wReadLen >> 8);			//high byte of read length
			m_SendBuffer[3] = (byte)(wReadLen & 0xFF);		//low byte of read length
			m_SendBuffer[4] = (byte)(wWriteLen >> 8);			//high byte of write length
			m_SendBuffer[5] = (byte)(wWriteLen & 0xFF);		//low byte of write length
			m_SendSize = 6;
			bReturn = O2SPIControlCommand();
			if (bReturn)
			{
				ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
			}

			return bReturn;
		}

		private bool O2SPIAccessBuffer(ref byte[] yDataIn, ref byte[] yDataOut, byte yflagIO, byte yflagRW, ref UInt16 wRWLen, UInt16 wIndex)
		{
			bool bReturn = false;

			//	yflagIO:	1,Read  function selected.
			//					0,Write function selected.
			//	yflagRW:	1,use Read buffer
			//					0,use Write buffer.
			// wRWLen:	should 32 or less.
			// wIndex:		0~512
			m_SendBuffer[0] = 0x4B;
			m_SendBuffer[1] = yflagIO;
			m_SendBuffer[2] = yflagRW;
			m_SendBuffer[3] = (byte)(wRWLen >> 8);				//high byte of length
			m_SendBuffer[4] = (byte)(wRWLen & 0xFF);		//low byte of length
			m_SendBuffer[5] = (byte)(wIndex >> 8);					//high byte of index
			m_SendBuffer[6] = (byte)(wIndex & 0xFF);			//low byte of index
			if(yflagIO == 0)
				Buffer.BlockCopy(yDataIn, 0, m_SendBuffer, 7, wRWLen);
			m_SendSize = wRWLen + 7;
			bReturn = O2SPIControlCommand();
			//wDataOutLength = 0;
			if (bReturn)
			{
				ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
				Buffer.BlockCopy(m_ReceiveBuffer, 0, yDataOut, 0, wRWLen + 7);
			}
			//else
			//	ErrorCode = LibErrorCode.IDS_ERR_SPI_BB_TIMEOUT;

			return bReturn;
		}

		#endregion
	}
}
