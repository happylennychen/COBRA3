using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Win32.SafeHandles;
using System.Threading;
using O2Micro.Cobra.Common;
using O2Micro.Cobra.AutoMationTest;
using O2Micro.Cobra.Communication;

namespace O2Micro.Cobra.Communication.Simulation
{
    public class CO2SimAdapter : CInterfaceSim
    {
        //Implementation of abstract function

		// Constructor
        public CO2SimAdapter()
		{
			//CloseDevice();
			m_Locker = new Semaphore(0, 1);
		}

        #region Public Method, Override CInterfaceBase and CInterfaceI2C 2 mother class

        public static Guid GetGuid()
        {
            return System.Guid.Empty;
        }

        public override bool OpenDevice(ref Int16 iPortNum, byte yPortIndex = 0)
        {
            bool bReturn = true;

            //if anything needs to be initialized when Opening Simulation Adapter, call private method
            //AutoMationTest.AutoMationTest.GetATMSetting();
            if (bReturn)
            {
                ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
            }

            return bReturn;
        }

        public override bool OpenDevice(AsyncObservableCollection<string> strName, byte yPortIndex)
        {
            bool bReturn = true;

            //if anything needs to be initialized when Opening Simulation Adapter, call private method
            //AutoMationTest.AutoMationTest.GetATMSetting();
            if (bReturn)
            {
                ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
            }

            return bReturn;
        }

        public override bool CloseDevice(bool bClearName = true)
        {
            ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;

            return true;
        }

        public override bool ReadDevice(ref byte[] yDataIn, ref byte[] yDataOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 1, UInt16 wDataInWrite = 1)
        {
            bool bReturn = true;
            UInt32 u32Err = LibErrorCode.IDS_ERR_SUCCESSFUL;

            /* (D151228)Francis, fake data preparation move to AutoMationTest.cs
            //if all successful, set successful error code and release semaphore
            bReturn = PrepareReadData(ref yDataIn, ref yDataOut, ref wDataOutLength, wDataInLength);
            if (bReturn)
            {
                ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
            }
            */
            //if ((m_busopDev.BusType == BUS_TYPE.BUS_TYPE_I2C) || (m_busopDev.BusType == BUS_TYPE.BUS_TYPE_I2C2))
            //{
                bReturn = AutoMationTest.AutoMationTest.GetDatabyRegIndex(ref u32RandId, yDataIn, ref yDataOut, ref u32Err, ref wDataOutLength, wDataInLength);
            //}
            ErrorCode = u32Err;

            return bReturn;
        }

        public override bool WriteDevice(ref byte[] yDataIn, ref byte[] yDataOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 1)
        {
            bool bReturn = true;
            UInt32 u32Err = LibErrorCode.IDS_ERR_SUCCESSFUL;

            bReturn = AutoMationTest.AutoMationTest.WriteDatatoReg(ref u32RandId, yDataIn, ref yDataOut, ref u32Err, ref wDataOutLength, wDataInLength);
            ErrorCode = u32Err;

            return bReturn;
        }

        public override bool ResetInf()
        {
            return true;
        }

        public override bool SetConfigure(List<UInt32> wConfig)
        {
            return true;
        }

        public override bool GetConfigure(ref List<UInt32> wConfig)
        {
            return true;
        }

        public override bool SetO2DelayTime(List<UInt32> wDelay)
        {
            return true;
        }

		public override bool SetAdapterCommand(ref byte[] yDataIn, ref byte[] yDataOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 1)
		{
			bool bReturn = false;

			ErrorCode = LibErrorCode.IDS_ERR_MGR_INVALID_INTERFACE_TYPE;
			return bReturn;
		}

		#endregion

        #region Private methods

        /* (D151229)Francis, move to AutoMationTest class, delete it
        private bool PrepareReadData(ref byte[] yDataIn, ref byte[] yDataOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 1)
        {
            bool bReturn = true;
            //byte yTmp = 0;

            for (int i = 0; i < wDataInLength; i++)
            {
                yDataOut[i] = RandomValue(ref yDataIn, wDataInLength);
            }

            wDataOutLength = wDataInLength;

            return bReturn;
        }

        private byte RandomValue(ref byte[] yDataIn, UInt16 wDataLength, byte yMin = byte.MinValue, byte yMax = byte.MaxValue)
        {
            byte yTmp = 0;
            int iData = 0;
            Random rndTmp = new Random(Guid.NewGuid().GetHashCode());

            yTmp = (byte)rndTmp.Next(yMin, yMax + 1);
            if (bErrGenerate)
            {
                iData = rndTmp.Next(0, 10000);  //0~9999 range
                if (iData < (u16Sensor * 100))
                {
                    //if (bErrOutMax || bErrOutMax || bErrOutMin)
                    {
                        do
                        {
                            iData = rndTmp.Next(0, 3000);
                            iData = iData % 3;      //get reminder value
                        } while (((iData == 0) && bErrOutMax) ||
                                    ((iData == 1) && bErrOutMin) ||
                                    ((iData == 2) && bErrPEC));
                        if(iData == 0)
                        {
                            if(yMax != byte.MaxValue)
                            {
                                yTmp = (byte)(yMax + 0x01);
                            }
                        }
                        else if(iData == 1)
                        {
                            if(yMin != byte.MinValue)
                            {
                                yTmp = (byte)(yMin - 1);
                            }
                        }
                        else if(iData == 2)
                        {
                            yTmp = (byte)(CalculateReadPEC(ref yDataIn, wDataLength) + 1);
                        }
                    }
                }
            }

            return yTmp;
        }

        private byte CalculateReadPEC(ref byte[] yDataIn, UInt16 wDataLength)
        {
            byte yPEC = 0;
            byte[] pdata = new byte[wDataLength+3];

            pdata[0] = yDataIn[0];
            pdata[1] = yDataIn[1];
            pdata[2] = (byte)(yDataIn[0] | 0x01);
            for (int i = 0; i < wDataLength; i++)
            {
                pdata[3+i] = yDataIn[2+i];
            }

            return yPEC;
        }
         * */

        #endregion

    }
}
