using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cobra.Common;

namespace Cobra.Communication.I2C
{
	public abstract class CInterfaceI2C : CInterfaceBase
	{
		#region Public Member Declaration

		public enum AdaptorReturn : byte
		{
			ES_DRIVER = 0,
			ES_CONTROLLER = 1,
			ES_I2C = 2,
		}

		public enum AdaptorErrCode : byte
		{
			O2_I2C_STATUS_OK = 0,
			O2_I2C_STATUS_BUS_ERROR = 1,
			O2_I2C_STATUS_SLA_ACK = 2,
			O2_I2C_STATUS_SLA_NACK = 3,
			O2_I2C_STATUS_DATA_NACK = 4,
			O2_I2C_STATUS_ARB_LOST = 5,
			O2_I2C_STATUS_BUS_BUSY = 6,
			O2_I2C_STATUS_LAST_DATA_ACK = 7,
			O2_I2C_STATUS_INVALID_PEC = 8,
			O2_I2C_NOT_AVAILABLE = 0x9C,//(byte)-100,
			O2_I2C_NOT_ENABLED = 0x9B,//(byte)-101,
			O2_I2C_READ_ERROR = 0x9A,//(byte)-102,
			O2_I2C_WRITE_ERROR = 0x99,//(byte)-103,
			O2_I2C_SLAVE_BAD_CONFIG = 0x98,//(byte)-104,
			O2_I2C_SLAVE_READ_ERROR = 0x97,//(byte)-105,
			O2_I2C_SLAVE_TIMEOUT = 0x96,//(byte)-106,
			O2_I2C_DROPPED_EXCESS_BYTES = 0x95,//(byte)-107,
			O2_I2C_BUS_ALREADY_FREE = 0x94,//(byte)-108,
			O2_I2C_INVALID_HANDLE = 0x93,//(byte)-109,
			O2_I2C_INVALID_PARAMETER = 0x92,//(byte)-110,
			O2_I2C_INVALID_LENGTH = 0x91,//(byte)-111,
			O2_I2C_INVALID_COMMAND = 0x90,//(byte)-112,
			O2_I2C_COMMAND_DISMATCH = 0x8F,//(byte)-113,
		}

		// <summary>
		// I2C frequence value
		// </summary>
		private UInt16 m_Frequence;
		public UInt16 I2CFrequence { get { return m_Frequence; } set { m_Frequence = value; } }

		#endregion

		#region Public Method

		public bool  GetI2CLastErr(byte yAdptorCmd, byte[] yDataArry)
		{
			//byte yReturn = (byte)AdaptorErrCode.O2_I2C_STATUS_OK;
			bool bReturn = true;

			ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
			if (yAdptorCmd != yDataArry[0])
			{
				bReturn = false;
				ErrorCode = LibErrorCode.IDS_ERR_I2C_CMD_DISMATCH;
			}
			else
			{
				switch (yDataArry[1])
				{
					case (byte)AdaptorReturn.ES_DRIVER:
						{
							bReturn = true;
							break;
						}
					case (byte)AdaptorReturn.ES_CONTROLLER:
						{
							bReturn = false;
							break;
						}
					case (byte)AdaptorReturn.ES_I2C:
					default:
						{
							bReturn = false;
							switch (yDataArry[2])
							{
								case (byte)AdaptorErrCode.O2_I2C_STATUS_OK:
									{
										bReturn = true;
										ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
										break;
									}
								case (byte)AdaptorErrCode.O2_I2C_STATUS_BUS_BUSY:
									{
										ErrorCode = LibErrorCode.IDS_ERR_I2C_BB_TIMEOUT; 
										break;
									}
								case (byte)AdaptorErrCode.O2_I2C_STATUS_SLA_ACK:
									{
										ErrorCode = LibErrorCode.IDS_ERR_I2C_LOST_ARBITRATION;
										break;
									}
								case (byte)AdaptorErrCode.O2_I2C_STATUS_SLA_NACK:
									{
										ErrorCode = LibErrorCode.IDS_ERR_I2C_SLA_NACK;
										break;
									}
								case (byte)AdaptorErrCode.O2_I2C_STATUS_DATA_NACK:
									{
										ErrorCode = LibErrorCode.IDS_ERR_I2C_SLA_NACK;
										break;
									}
								case (byte)AdaptorErrCode.O2_I2C_STATUS_ARB_LOST:
									{
										ErrorCode = LibErrorCode.IDS_ERR_I2C_LOST_ARBITRATION;
										break;
									}
								case (byte)AdaptorErrCode.O2_I2C_STATUS_BUS_ERROR:
									{
										ErrorCode = LibErrorCode.IDS_ERR_I2C_BUS_ERROR;
										break;
									}
								default:
									{
										ErrorCode = LibErrorCode.IDS_ERR_I2C_BUS_ERROR;
										break;
									}
							}
							break;
						}
				}
			}

			return bReturn;// yReturn;
		}

		#endregion
	}
}
