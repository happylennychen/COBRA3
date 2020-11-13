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

namespace Cobra.Communication.RS232
{
    public class CO2RS232Master : CInterfaceRS232
    {
        #region CO2RS232Master constant member and constant value definition

        private static readonly Guid O2RS232GUID = Guid.Empty;
        private static readonly string strVID = "";
        private static readonly string strPID = "";

        #endregion

        #region CO2RS232Master private member definition

        //Serial port object declarration
        //(M180817)Francis, issueid=1113, try to solve slow download speed
        //private SerialPort RS232Serial = new SerialPort();
        private IntPtr ptrUART = System.IntPtr.Zero;
        private string strPortName = string.Empty;      //"COM4"
        private const int ReadDelay = 100;
        private const int ReadTimeout = 100;//40; //10
        private NativeMethods.DCB dcbCommPort = new NativeMethods.DCB();
        private NativeMethods.COMMTIMEOUTS ctoCommPort = new NativeMethods.COMMTIMEOUTS();

        //COM port setting declaration
        private UInt32 wRS232COMBaudrate = 9600;
        private System.IO.Ports.Parity pRS232COMParity = System.IO.Ports.Parity.None;
        private UInt16 wRS232COMDatabits = 8;
        private System.IO.Ports.StopBits tRS232COMStop = System.IO.Ports.StopBits.One;
        private Byte yRS232COMStop = 0;
        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        public CO2RS232Master()
        {
            CloseDevice();
            m_Locker = new Semaphore(0, 1);
            m_SendBuffer = new byte[CCommunicateManager.MAX_RWBUFFER];			//new send buffer
            m_ReceiveBuffer = new byte[CCommunicateManager.MAX_RWBUFFER];		//new receive buffer
            wUARTReadDelay = ReadDelay;
        }

        #region Public Method, Override CInterfaceBase and CInterfaceRS232 mother class

        /// <summary>
        /// Get RS232 GUID, no used, we won't use GUID to recognize RS232 connection
        /// </summary>
        /// <returns>GUID value </returns>
        public static Guid GetGuid()
        {
            return O2RS232GUID;
        }

        /// <summary>
        /// Static function, to enumerate Windows COM all port name 
        /// </summary>
        /// <returns>List of string saved com port name </returns>
        public static List<string> GetComPortLinkName()
        {
            string[] ArrPortName = SerialPort.GetPortNames();
            List<string> comports = new List<string>();

            foreach (string eachport in ArrPortName)
            {
                comports.Add(eachport);
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
                ErrorCode = LibErrorCode.IDS_ERR_COM_INVALID_PARAMETER;
                return bReturn;
            }

            //if handler instance is alreday exist, close handler first
            //(M180817)Francis, issueid=1113, try to solve slow download speed
            //if (RS232Serial != null)
            if (ptrUART != System.IntPtr.Zero)
            {
                CloseDevice();
            }

            //enumerate all connected interface device
            bReturn = EnumerateRS232Master(ref iPortNum, yPortIndex);

            ////if (RS232Serial.IsOpen)
            ////{
            ////RS232Serial.Close();
            ////}

            //m_SendBuffer = new byte[CCommunicateManager.MAX_RWBUFFER];			//new send buffer
            //m_ReceiveBuffer = new byte[CCommunicateManager.MAX_RWBUFFER];		//new receive buffer
            if (bReturn)
            {
                SetCOMport();
                //(M180817)Francis, issueid=1113, try to solve slow download speed
                //RS232Serial.PortName = FriendName;
                strPortName = FriendName;

                try
                {
                    //(M180817)Francis, issueid=1113, try to solve slow download speed
                    //RS232Serial.Open();
                    O2CreateFileCOMport();

                    //if all successful, set successful error code and release semaphore
                    //(M180817)Francis, issueid=1113, try to solve slow download speed
                    //if (RS232Serial.IsOpen)
                    if (ptrUART != System.IntPtr.Zero)
                    {
                        SetCOMport();
                        ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
                        bReturn = true;
                        //(M180817)Francis, issueid=1113, try to solve slow download speed, remember to set timeout
                        //RS232Serial.ReadTimeout = 2;// 4000;
                        //RS232Serial.WriteTimeout = 1000;
                        ////RS232Serial.BreakState = false;
                        ////RS232Serial.DtrEnable = false;
                        ////RS232Serial.RtsEnable = false;
                        ////RS232Serial.Close();		//if ok to open then close it. COM port device is not allowed to open permanently
                    }	//if (RS232Serial.IsOpen)
                    else
                    {
                        ErrorCode = LibErrorCode.IDS_ERR_COM_OPEN_FAILED;
                        bReturn = false;
                    }	//if (RS232Serial.IsOpen)
                }
                catch (Exception e)
                {
                    ErrorCode = LibErrorCode.IDS_ERR_COM_OPEN_FAILED;
                    bReturn = false;
                }
            }	//if (bReturn)
            else
            {
                bReturn = false;
                //ErrorCode = LibErrorCode.IDS_ERR_COM_INDEX_OUT;
            }	//if (bReturn)

            return bReturn;
        }

        // <summary>
        // Open devices, function will enumerate all connected devices and save in iPortNum. 
        // After successfully opened, function will try to open indicated device by yPortIndex value
        // Currently, we are supporting total 4 devices simultaneously connected, so yPortIndex allow 0~3 input
        // </summary>
        // <param name="strName">String list of COM port name
        // <param name="yPortIndex">index value to indicate which device to open; currently support 0~3</param>
        // <returns>true: opened successfully; false: opened failed</returns>
        public override bool OpenDevice(AsyncObservableCollection<string> strName, byte yPortIndex)
        {
            bool bReturn = false;

            //if input index of target device is out of supported range, error report
            //if (((int)yPortIndex < 0) || ((int)yPortIndex >= CCommunicateManager.MAX_COMM_DEVICES))
            //{
            //	ErrorCode = LibErrorCode.IDS_ERR_RS232_INVALID_PARAMETER;
            //	return bReturn;
            //}
            if (strName.Count <= yPortIndex)
            {
                ErrorCode = LibErrorCode.IDS_ERR_COM_INDEX_OUT;
                return bReturn;
            }

            //(M180817)Francis, issueid=1113, try to solve slow download speed
            //if handler instance is alreday exist, close handler first
            //if (RS232Serial != null)
            if (ptrUART != System.IntPtr.Zero)
            {
                CloseDevice();
            }

            //enumerate all connected interface device
            //bReturn = EnumerateRS232Master(ref iPortNum, yPortIndex);
            FriendName = strName[yPortIndex];

            ////if (RS232Serial.IsOpen)
            ////{
            ////RS232Serial.Close();
            ////}

            //m_SendBuffer = new byte[CCommunicateManager.MAX_RWBUFFER];			//new send buffer
            //m_ReceiveBuffer = new byte[CCommunicateManager.MAX_RWBUFFER];		//new receive buffer
            SetCOMport();
            //(M180817)Francis, issueid=1113, try to solve slow download speed
            //RS232Serial.PortName = FriendName;
            strPortName = FriendName;

            try
            {
                //(M180817)Francis, issueid=1113, try to solve slow download speed
                //RS232Serial.Open();
                O2CreateFileCOMport();
                //if all successful, set successful error code and release semaphore
                //if (RS232Serial.IsOpen)
                if (ptrUART != System.IntPtr.Zero)
                {
                    ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
                    bReturn = true;
                    //(M180817)Francis, issueid=1113, try to solve slow download speed
                    //RS232Serial.ReadTimeout = 100;// 4000;
                    //RS232Serial.WriteTimeout = 1000;
                    ////RS232Serial.Close();		//if ok to open then close it. COM port device is not allowed to open permanently
                    //20180705
                    //End
                }
                else
                {
                    ErrorCode = LibErrorCode.IDS_ERR_COM_OPEN_FAILED;
                    bReturn = false;
                }
            }
            catch (Exception e)
            {
                ErrorCode = LibErrorCode.IDS_ERR_COM_OPEN_FAILED;
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
            //(M180817)Francis, issueid=1113, try to solve slow download speed
            //if (SearchCOMList(RS232Serial.PortName))
            if (SearchCOMList(strPortName))
            {
                try
                {
                    //if (RS232Serial.IsOpen)
                    if (ptrUART != System.IntPtr.Zero)
                    {
                        //RS232Serial.DiscardOutBuffer();
                        //RS232Serial.DiscardInBuffer();
                        //RS232Serial.Close();
                        O2CloseFileCOMport();
                    }
                }
                catch (Exception e)
                {
                    ErrorCode = LibErrorCode.IDS_ERR_COM_OPEN_FAILED;
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

            if (yDataOut.GetLength(0) < wDataOutLength)
            {
                ErrorCode = LibErrorCode.IDS_ERR_COM_READ_BUFFER_NOT_ENOUGH;
                return bReturn;
            }

            //check input data buffer size, at least having Slave Address and Command 2 bytes
            if ((yDataIn.GetLength(0) < 1) || (yDataIn.GetLength(0) < wDataInLength))
            {
                ErrorCode = LibErrorCode.IDS_ERR_COM_IN_PARAMETER_INVALID;
                return bReturn;
            }

            //FolderMap.WriteFile(String.Format("RS232 ReadDevice"));
            System.Array.Clear(yDataOut, 0, yDataOut.GetLength(0));		//clear data buffer
            //(M180817)Francis, issueid=1113, try to solve slow download speed
            //if (RS232Serial != null)
            if (ptrUART != System.IntPtr.Zero)
            {
                //check RS232Serial COM port setting is existing
                //if (!SearchCOMList(RS232Serial.PortName))
                if (!SearchCOMList(strPortName))
                {
                    //RS232Serial = null;
                    ptrUART = System.IntPtr.Zero;
                    ErrorCode = LibErrorCode.IDS_ERR_COM_COM_NOT_EXIST;
                    return false;
                }

                //FolderMap.WriteFile(String.Format("Found RS232Serial"));
                //try to open COM port
                /*
                try
                {
                    SetCOMport();
                    RS232Serial.Open();
                    if (!RS232Serial.IsOpen)
                    {
                        ErrorCode = LibErrorCode.IDS_ERR_COM_OPEN_FAILED;
                        return bReturn;
                    }
                }
                catch (Exception e)
                {
                    ErrorCode = LibErrorCode.IDS_ERR_COM_OPEN_FAILED;
                    return bReturn;
                }
                 * */
                //FolderMap.WriteFile(String.Format("Open COM port"));

                #region read protocol from COM port, supports read byte/word/block
                //if (wDataInLength == 0)
                //{
                //ErrorCode = LibErrorCode.IDS_ERR_COM_INVALID_READI2CSINGLE;
                //}
                //else
                {
                    bReturn = O2ReadCOMport(ref yDataIn, ref yDataOut, ref wDataOutLength, wDataInLength);
                }
                #endregion
            }
            else
            {
                ErrorCode = LibErrorCode.IDS_ERR_COM_NULL_COM_HANDLER;
                return bReturn;
            }	//if (RS232Serial != null)

            /*
            try
            {
                if (RS232Serial.IsOpen)
                {
                    RS232Serial.DiscardOutBuffer();
                    RS232Serial.DiscardInBuffer();
                    RS232Serial.Close();
                }
            }
            catch (Exception e)
            {
                ErrorCode = LibErrorCode.IDS_ERR_COM_OPEN_FAILED;
                return false;
            }
             **/

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
            if ((yDataIn.GetLength(0) < 1) || (yDataIn.GetLength(0) < wDataInLength))
            {
                ErrorCode = LibErrorCode.IDS_ERR_COM_IN_PARAMETER_INVALID;
                return bReturn;
            }

            System.Array.Clear(yDataOut, 0, yDataOut.GetLength(0));
            //FolderMap.WriteFile(String.Format("RS232 WriteDevice"));
            wDataOutLength = 0;
            //(M180817)Francis, issueid=1113, try to solve slow download speed
            //if (RS232Serial != null)
            if (ptrUART != System.IntPtr.Zero)
            {
                //check RS232Serial COM port setting is existing
                //if (!SearchCOMList(RS232Serial.PortName))
                if (!SearchCOMList(strPortName))
                {
                    //RS232Serial = null;
                    ptrUART = System.IntPtr.Zero;
                    ErrorCode = LibErrorCode.IDS_ERR_COM_COM_NOT_EXIST;
                    return false;
                }

                //try to opne COM port
                /*
                try
                {
                    RS232Serial.Open();
                    if (!RS232Serial.IsOpen)
                    {
                        ErrorCode = LibErrorCode.IDS_ERR_COM_OPEN_FAILED;
                        return bReturn;
                    }
                }
                catch (Exception e)
                {
                    ErrorCode = LibErrorCode.IDS_ERR_COM_OPEN_FAILED;
                    return bReturn;
                }
                 * */

                #region write protocol to COM port, supports read byte/word/block
                if (wDataInLength == 0)
                {
                    ErrorCode = LibErrorCode.IDS_ERR_COM_INVALID_WRITEI2CSINGLE;
                }
                else
                {
                    bReturn = O2WriteCOMport(ref yDataIn, ref yDataOut, ref wDataOutLength, wDataInLength);
                }
                #endregion
            }
            else
            {
                ErrorCode = LibErrorCode.IDS_ERR_COM_INVALID_ENUMMETHOD;
                return bReturn;
            }

            /*
            try
            {
                if (RS232Serial.IsOpen)
                {
                    RS232Serial.DiscardOutBuffer();
                    RS232Serial.DiscardInBuffer();
                    RS232Serial.Close();
                }
            }
            catch (Exception e)
            {
                ErrorCode = LibErrorCode.IDS_ERR_COM_OPEN_FAILED;
                bReturn = false;
            }
             * */

            return bReturn;
        }

        /*		public override bool ConfigureDevice(ref byte[] yCfgInOut, bool bRW = false)
                {
                    SetFrequency((ushort)100);	//set as 100k
                    return true;
                }*/

        /// <summary>
        /// Reset Serial port, close serial port and re-open
        /// </summary>
        /// <returns></returns>
        public override bool ResetInf()
        {
            //(M180817)Francis, issueid=1113, try to solve slow download speed
            //check RS232Serial COM port setting is existing
            //if (RS232Serial != null)
            if (ptrUART != System.IntPtr.Zero)
            {
                //if (!SearchCOMList(RS232Serial.PortName))
                if (!SearchCOMList(strPortName))
                {
                    //RS232Serial = null;
                    ptrUART = System.IntPtr.Zero;
                    ErrorCode = LibErrorCode.IDS_ERR_COM_COM_NOT_EXIST;
                    return false;
                }

                try
                {
                    //(M180817)Francis, issueid=1113, try to solve slow download speed
                    //try to, if Opened, flush buffer and close Serial port. Wait 1 second then reopen
                    //if (RS232Serial.IsOpen)
                    if (ptrUART != System.IntPtr.Zero)
                    {
                        //RS232Serial.DiscardOutBuffer();
                        //RS232Serial.DiscardInBuffer();
                        //RS232Serial.Close();
                        O2CloseFileCOMport();
                        Thread.Sleep(1000);
                        SetCOMport();
                        //RS232Serial.Open();
                        O2CreateFileCOMport();
                    }
                }
                catch (Exception e)
                {
                    ErrorCode = LibErrorCode.IDS_ERR_COM_OPEN_FAILED;
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Set COM port configure, 
        /// wConfig[0] is baudrate
        /// wConfig[1] is Databits
        /// wCofnig[2] is stop ibt
        /// wCofnig[3] is parity
        /// </summary>
        /// <param name="wConfig">list of configure value</param>
        /// <returns>true if everything is OK, otherwise false</returns>
        public override bool SetConfigure(List<UInt32> wConfig)
        {
            if (wConfig.Count < 4) return false;

            wRS232COMBaudrate = wConfig[0];
            wRS232COMDatabits = (UInt16)wConfig[1];
            if (wConfig[2] == 0)
            {
                tRS232COMStop = System.IO.Ports.StopBits.One;
                yRS232COMStop = 0;
            }
            else if (wConfig[2] == 1)
            {
                tRS232COMStop = System.IO.Ports.StopBits.OnePointFive;
                yRS232COMStop = 1;
            }
            else if (wConfig[2] == 2)
            {
                tRS232COMStop = System.IO.Ports.StopBits.Two;
                yRS232COMStop = 2;
            }
            else
            {
                tRS232COMStop = System.IO.Ports.StopBits.One;
                yRS232COMStop = 0;
            }

            if (wConfig[3] == 0)
            {
                pRS232COMParity = System.IO.Ports.Parity.None;
            }
            else if (wConfig[3] == 1)
            {
                pRS232COMParity = System.IO.Ports.Parity.Odd;
            }
            else if (wConfig[3] == 2)
            {
                pRS232COMParity = System.IO.Ports.Parity.Even;
            }
            else if (wConfig[3] == 3)
            {
                pRS232COMParity = System.IO.Ports.Parity.Mark;
            }
            else if (wConfig[3] == 4)
            {
                pRS232COMParity = System.IO.Ports.Parity.Space;
            }
            else
            {
                pRS232COMParity = System.IO.Ports.Parity.None;
            }

            try
            {
                //(M180817)Francis, issueid=1113, try to solve slow download speed
                //if (RS232Serial.IsOpen)
                if (ptrUART != System.IntPtr.Zero)
                {
                    //RS232Serial.DiscardOutBuffer();
                    //RS232Serial.DiscardInBuffer();
                    //RS232Serial.Close();
                    O2CloseFileCOMport();
                    Thread.Sleep(1);
                    SetCOMport();
                    //RS232Serial.Open();
                    O2CreateFileCOMport();
                }
            }
            catch (Exception e)
            {
                ErrorCode = LibErrorCode.IDS_ERR_COM_OPEN_FAILED;
                return false;
            }
            //SetCOMport();

            return true;
        }

        /// <summary>
        /// Get COM port configure, 
        /// wConfig[0] is baudrate
        /// wConfig[1] is Databits
        /// wCofnig[2] is stop ibt
        /// wCofnig[3] is parity
        /// </summary>
        /// <param name="wConfig">list of configure value</param>
        /// <returns>true if everything is OK, otherwise false</returns>
        public override bool GetConfigure(ref List<UInt32> wConfig)
        {
            if (wConfig.Count < 4) return false;

            GetCOMport();
            wConfig[0] = wRS232COMBaudrate;
            wConfig[1] = wRS232COMDatabits;
            if (tRS232COMStop == System.IO.Ports.StopBits.None)
            {
                wConfig[2] = 0;
                yRS232COMStop = 0;
            }
            else if (tRS232COMStop == System.IO.Ports.StopBits.One)
            {
                wConfig[2] = 0;
                yRS232COMStop = 0;
            }
            else if (tRS232COMStop == System.IO.Ports.StopBits.Two)
            {
                wConfig[2] = 2;
                yRS232COMStop = 2;
            }
            else if (tRS232COMStop == System.IO.Ports.StopBits.OnePointFive)
            {
                wConfig[2] = 1;
                yRS232COMStop = 1;
            }
            else
            {
                wConfig[2] = 0;
                //return false;
            }
            if (pRS232COMParity == System.IO.Ports.Parity.None)
            {
                wConfig[3] = 0;
            }
            else if (pRS232COMParity == System.IO.Ports.Parity.Odd)
            {
                wConfig[3] = 1;
            }
            else if (pRS232COMParity == System.IO.Ports.Parity.Even)
            {
                wConfig[3] = 2;
            }
            else if (pRS232COMParity == System.IO.Ports.Parity.Mark)
            {
                wConfig[3] = 3;
            }
            else if (pRS232COMParity == System.IO.Ports.Parity.Space)
            {
                wConfig[3] = 4;
            }
            else
            {
                wConfig[3] = 0;
                //return false;
            }

            return true;
        }

        public override bool SetO2DelayTime(List<UInt32> wDelay)
        {
            //RS232Serial.ReadTimeout = (int)wDelay[0];
            //RS232Serial.WriteTimeout = (int)wDelay[1];
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

        #region Private Method, COM adapter self method

        /// <summary>
        /// Search input COM port name existing in list 
        /// </summary>
        /// <param name="inStrCOMName">string type, COM port name</param>
        /// <returns>true, if found in list, otherwise false</returns>
        private bool SearchCOMList(string inStrCOMName)
        {
            string[] strCOMArr;
            bool bReturn = false;

            if (inStrCOMName.Length != 0)
            {
                strCOMArr = SerialPort.GetPortNames();
                for (int i = 0; i < strCOMArr.Length; i++)
                {
                    strCOMArr[i] = strCOMArr[i].ToUpper();
                    inStrCOMName = inStrCOMName.ToUpper();
                    if (string.Equals(strCOMArr[i], inStrCOMName))
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
        private unsafe bool EnumerateRS232Master(ref Int16 iDevNum, byte yPortIndex)
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

        /// <summary>
        /// Set configure value into Serial Port class
        /// </summary>
        private void SetCOMport()
        {
            //(M180817)Francis, issueid=1113, try to solve slow download speed
            //RS232Serial.BaudRate = (int)wRS232COMBaudrate;
            //RS232Serial.Parity = pRS232COMParity;
            //RS232Serial.DataBits = wRS232COMDatabits;
            //RS232Serial.StopBits = tRS232COMStop;
            //RS232Serial.ReadBufferSize = CCommunicateManager.MAX_RWBUFFER;// 1024;
            //RS232Serial.WriteBufferSize = CCommunicateManager.MAX_RWBUFFER;// 1024;
            ////RS232Serial.Open();			//it looks like no need to open it?
        }

        /// <summary>
        /// Get configure value from Serial Port class
        /// </summary>
        private void GetCOMport()
        {
            //(M180817)Francis, issueid=1113, try to solve slow download speed
            //wRS232COMBaudrate = (UInt32)RS232Serial.BaudRate;
            //pRS232COMParity = RS232Serial.Parity;
            //wRS232COMDatabits = (UInt16)RS232Serial.DataBits;
            //tRS232COMStop = RS232Serial.StopBits;
            if (ptrUART != System.IntPtr.Zero)
            {
                dcbCommPort.DCBlength = (UInt32)Marshal.SizeOf(dcbCommPort);
                NativeMethods.GetCommState(ptrUART, ref dcbCommPort);
                wRS232COMBaudrate = (UInt32)dcbCommPort.BaudRate;
                wRS232COMDatabits = dcbCommPort.ByteSize;
                pRS232COMParity = (System.IO.Ports.Parity)dcbCommPort.Parity;
                tRS232COMStop = (System.IO.Ports.StopBits)dcbCommPort.StopBits;
            }
        }

        /// <summary>
        /// Call Serial Port write/read function to write data to COM port or read data from COM port
        /// </summary>
        /// <param name="iRetLeng">output, length of bytes that successfully read from COM port </param>
        /// <returns>true: if read/write is OK; otherwise return false</returns>
        private bool O2RS232ControlCommand(out Int32 iRetLeng)
        {
            bool bReturn = false;
            Int32 iRet = 0;
            int iRetry = 50;
            int i = 0;
            int BytesWritten = 0;
            NativeMethods.OVERLAPPED ovlCommPort = new NativeMethods.OVERLAPPED();
            iRetry = 5;
            while ((iRetry > 0) && (m_SendSize > 0))
            {
                try
                {
                    NativeMethods.WriteFile(ptrUART, m_SendBuffer, m_SendSize, ref BytesWritten, ref ovlCommPort);
                    iRetry -= 1;		//just for case
                    break; ;	//nothing happen, exit while loop
                }
                catch (TimeoutException timeoutEx)
                {
                    O2CloseFileCOMport();
                    Thread.Sleep(10);
                    O2CreateFileCOMport();
                    iRetry -= 1;
                }
                catch (Exception e)
                {
                    iRetry = -1;	//other exception exit while loop
                }
            }
            //check if write failed, return false
            if (iRetry == 0)
            {
                ErrorCode = LibErrorCode.IDS_ERR_COM_COM_TIMEOUT;
                iRetLeng = 0;
                return bReturn;
            }
            else if (iRetry == -1)
            {
                iRetLeng = 0;
                ErrorCode = LibErrorCode.IDS_ERR_COM_COM_READ_ZERO;
                return bReturn;
            }
            iRetry = 10;//10;
            /*if (m_ReceiveSize > 0)
            {
                //while (RS232Serial.BytesToRead < m_ReceiveSize)
                //{                    //Thread.Sleep(wUARTReadDelay);
                Thread.Sleep(50);
                //if (iRetry > 0) iRetry--;
                //else break;	//break while loop; avoid dead loop
                //}
            }*/

            iRetLeng = 0;
            if (iRetry >= 0)
            {
                if (m_ReceiveSize > 0)
                {
                    iRetry = 20;
                    i = 0;
                    iRet = 0;
                    try
                    {
                        BytesWritten = 0;
                        NativeMethods.PurgeComm(ptrUART, NativeMethods.PURGE_RXABORT | NativeMethods.PURGE_RXCLEAR); /*清除输入缓冲区*/
                        Thread.Sleep(1);
                        Array.Clear(m_ReceiveBuffer, 0, m_ReceiveBuffer.Length);
                        if (NativeMethods.ReadFile(ptrUART, m_ReceiveBuffer, m_ReceiveSize, ref BytesWritten, ref ovlCommPort))
                        {
                            iRet = 1;
                        }
                    }
                    catch (Exception e)
                    {
                    }
                    if (iRet > 0)
                    {
                        iRetLeng = BytesWritten;
                        ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
                        bReturn = true;
                    }
                    else
                    {
                        iRetLeng = 0;
                        ErrorCode = LibErrorCode.IDS_ERR_COM_COM_TIMEOUT;
                    }
                }
                else
                {
                    ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
                    bReturn = true;
                }
            }
            return bReturn;
        }

        /// <summary>
        /// Read COM port function, check input/output data, copy data into internal buffer, and call O2RS232ControlCommand()
        /// to execute read API
        /// </summary>
        /// <param name="yDataIn">buffer of input data, write to interface device byte by byte sequentially</param>
        /// <param name="yDataOut">buffer of output data</param>
        /// <param name="wDataOutLength">output value indicate the number of output data</param>
        /// <param name="wDataInLength">indicate number to read, should be 1</param>
        /// <returns>true: operation successful; false: operation failed</returns>
        private bool O2ReadCOMport(ref byte[] yDataIn, ref byte[] yDataOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 1)
        {
            bool bReturn = false;
            Int32 iRetLength = 0;

            m_SendSize = wDataInLength;
            if (m_SendSize > 0)
            {
                Buffer.BlockCopy(yDataIn, 0, m_SendBuffer, 0, m_SendSize);
            }
            m_ReceiveSize = wDataOutLength;
            bReturn = O2RS232ControlCommand(out iRetLength);
            if (bReturn)
            {

                //if (iRetLength >= m_ReceiveSize)
                {
                    wDataOutLength = (UInt16)iRetLength;
                    Buffer.BlockCopy(m_ReceiveBuffer, 0, yDataOut, 0, wDataOutLength);
                    ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
                }
                //else
                //{
                //wDataOutLength = 0;
                //ErrorCode = LibErrorCode.IDS_ERR_COM_READ_NOT_ENOUGH;
                //bReturn = false;
                //}
            }
            else
            {
                //ErrorCode = LibErrorCode.;
                //ErrorCode is assigned in O2COMControlCommand()
                wDataOutLength = 0;
            }

            return bReturn;
        }

        /// <summary>
        /// Read COM port function, check input/output data, copy data into internal buffer, and call O2RS232ControlCommand()
        /// to execute read API
        /// </summary>
        /// <param name="yDataIn">buffer of input data, write to interface device byte by byte sequentially</param>
        /// <param name="yDataOut">buffer of output data</param>
        /// <param name="wDataOutLength">output value indicate the number of output data</param>
        /// <param name="wDataInLength">indicate number to read, should be 1</param>
        /// <returns>true: operation successful; false: operation failed</returns>
        private bool O2WriteCOMport(ref byte[] yDataIn, ref byte[] yDataOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 1)
        {
            bool bReturn = false;
            Int32 iRetLength = 0;

            m_SendSize = wDataInLength;
            if (m_SendSize <= yDataIn.GetLength(0))
            {
                Buffer.BlockCopy(yDataIn, 0, m_SendBuffer, 0, m_SendSize);
            }
            else
            {
                //ErrorCode = LibErrorCode.IDS_ERR_COM_WRITE_NOT_ENOUGH;
                return false;
            }

            m_ReceiveSize = wDataOutLength;
            bReturn = O2RS232ControlCommand(out iRetLength);
            //wDataOutLength = (UInt16)iRetLength;
            if (bReturn)
            {

                if (iRetLength >= m_ReceiveSize)
                {
                    wDataOutLength = (UInt16)iRetLength;
                    Buffer.BlockCopy(m_ReceiveBuffer, 0, yDataOut, 0, wDataOutLength);
                    ErrorCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
                }
                else
                {
                    wDataOutLength = 0;
                    ErrorCode = LibErrorCode.IDS_ERR_COM_READ_NOT_ENOUGH;
                    bReturn = false;
                }
            }

            return bReturn;
        }

        #endregion

        #region Private Method, using NativeMethods to communication COM port
        //(M180817)Francis, issueid=1113, try to solve slow download speed
        private bool O2CreateFileCOMport()
        {
            bool bReturn = true;
            bool bSuccess;

            ptrUART = NativeMethods.CreateFile("\\\\.\\"+strPortName,
                                                NativeMethods.GENERIC_READ | NativeMethods.GENERIC_WRITE, 0, 0,
                                                NativeMethods.OPEN_EXISTING, 0, 0);

            if (ptrUART == System.IntPtr.Zero)
            {
                bReturn = false;
            }
            else
            {

                dcbCommPort.DCBlength = (UInt32)Marshal.SizeOf(dcbCommPort) ;
                bReturn = NativeMethods.GetCommState(ptrUART, ref dcbCommPort);
                dcbCommPort.BaudRate = (UInt32)wRS232COMBaudrate;
                dcbCommPort.Parity = (Byte)pRS232COMParity;
                dcbCommPort.ByteSize = (Byte)wRS232COMDatabits;
                dcbCommPort.StopBits = (Byte)yRS232COMStop;// tRS232COMStop;
                //dcbCommPort.fBinary = 1;
                dcbCommPort.flags = 0;
                dcbCommPort.flags |= 1;
                if (pRS232COMParity > 0)
                {
                    //dcb.fParity=1
                    dcbCommPort.flags |= 2;
                }
                else
                {
                    dcbCommPort.flags &= (UInt32)(~0xFFFFFFFD);
                }
                Thread.Sleep(10);
                bReturn = NativeMethods.SetCommState(ptrUART, ref dcbCommPort);
                Thread.Sleep(10);
                bReturn = NativeMethods.GetCommState(ptrUART, ref dcbCommPort);
                //if (dcbCommPort.ByteSize != (byte)wRS232COMDatabits)
                    //return false;

                // SET THE COMM TIMEOUTS.
                NativeMethods.GetCommTimeouts(ptrUART, ref ctoCommPort);
                ctoCommPort.ReadIntervalTimeout = ReadTimeout;
                ctoCommPort.ReadTotalTimeoutConstant = 400;
                ctoCommPort.ReadTotalTimeoutMultiplier = 10;
                ctoCommPort.WriteTotalTimeoutMultiplier = 0;
                ctoCommPort.WriteTotalTimeoutConstant = 0;
                NativeMethods.SetCommTimeouts(ptrUART, ref ctoCommPort);

            }
            return bReturn;
        }

        private bool O2CloseFileCOMport()
        {
            bool bReturn = true;

            if (ptrUART != System.IntPtr.Zero)
            {
                NativeMethods.CloseHandle(ptrUART);
                ptrUART = System.IntPtr.Zero;
            }
            return bReturn;
        }

        #endregion
    }
}
