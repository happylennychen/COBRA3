using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Cobra.EM;
using Cobra.Common;
using System.Collections;
using System.ComponentModel;
using System.Windows.Controls.Primitives;
using System.Threading;
using System.IO;

namespace Cobra.SimpleI2CPanel
{
    /// <summary>
    /// Interaction logic for MainControl.xaml
    /// </summary>
    public partial class MainControl : UserControl
    {
        #region variable defination

        private Device m_parent;
        public Device parent
        {
            get { return m_parent; }
            set { m_parent = value; }
        }
        private string m_sflname;
        public string sflname
        {
            get { return m_sflname; }
            set { m_sflname = value; }
        }

        private TASKMessage m_Msg = new TASKMessage();
        public TASKMessage msg
        {
            get { return m_Msg; }
            set { m_Msg = value; }
        }
        private GeneralMessage gm = new GeneralMessage("Init SFL", "", 0);
        private System.Timers.Timer wTimer = new System.Timers.Timer();
        #endregion

        #region Function defination
        public MainControl(object pParent, string name)
        {
            InitializeComponent();

            #region 相关初始化
            parent = (Device)pParent;
            if (parent == null) return;

            sflname = name;
            if (String.IsNullOrEmpty(sflname)) return;

            wTimer.Elapsed += new System.Timers.ElapsedEventHandler(wTimer_Elapsed);
            #endregion
        }

        #region 通用控件消息响应
        public void CallWarningControl(GeneralMessage message)
        {
            WarningPopControl.Dispatcher.Invoke(new Action(() =>
            {
                WarningPopControl.ShowDialog(message);
                runBtn.Content = "Run";
            }));
        }
        #endregion

        #region DM提供的API
        public uint Read(byte address, ref UInt16 value)
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_READ;
            msg.sm.misc[0] = address;
            //msg.bupdate = bControl;            //需要从chip读数据
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            value = msg.sm.misc[1];
            return m_Msg.errorcode;
        }
        public uint Write(byte address, UInt16 value)
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_WRITE;
            msg.sm.misc[0] = address;
            msg.sm.misc[1] = value;
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            return m_Msg.errorcode;
        }
        #endregion

        private void ReadButton_Click(object sender, RoutedEventArgs e)
        {
            byte address = Convert.ToByte(AddressBox.Text, 16);
            UInt16 value = new UInt16();
            Read(address, ref value);
            rValueBox.Text = string.Format("{0:x4}", value);
        }

        private void WriteButton_Click(object sender, RoutedEventArgs e)
        {
            byte address = Convert.ToByte(AddressBox.Text, 16);
            UInt16 value = Convert.ToUInt16(wValueBox.Text, 16);
            Write(address, value);
        }

        private byte crc41_calc(byte[] pdata, int len)
        {
            byte crc = 0;
            byte crcdata;
            int n, j;

            for (n = len - 1; n >= 0; n--)
            {
                crcdata = pdata[n];
                for (j = 0x8; j > 0; j >>= 1)
                {
                    if ((crc & 0x8) != 0)
                    {
                        crc <<= 1;
                        crc ^= 0x3;
                    }
                    else
                        crc <<= 1;
                    if ((crcdata & j) != 0)
                        crc ^= 0x3;
                }
                crc = (byte)(crc & 0xf);
            }
            return crc;
        }

        private byte crc4_calc(byte[] pdata, int len)
        {

            byte crc = 0;
            byte crcdata;
            //byte poly = 0x07;             // poly
            //uint p = (uint)poly + 0x100;
            int n, j;                                      // the length of the data


            for (n = len - 1; n >= 0; n--)
            {
                crcdata = pdata[n];
                for (j = 0x8; j > 0; j >>= 1)
                {
                    if ((crc & 0x8) != 0)
                    {
                        crc <<= 1;
                        crc ^= 0x3;
                    }
                    else
                        crc <<= 1;
                    if ((crcdata & j) != 0)
                        crc ^= 0x3;
                }
                crc = (byte)(crc & 0xf);
            }

            return crc;
        }
        #endregion

        private void runBtn_Click(object sender, RoutedEventArgs e)
        {
            UInt16 uTimer = 0;
            UInt16.TryParse(timerBox.Text, out uTimer);
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            rValueBox.Text = string.Empty;
            wTimer.Interval = uTimer;
            if (runBtn.IsChecked == true)
            {
                runBtn.Content = "Stop";
                wTimer.Start();
            }
            else
            {
                runBtn.Content = "Run";
                wTimer.Stop();
            }
        }

        void wTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            byte address = 0;
            UInt16 rvalue = 0;
            UInt16 wvalue = 0;
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;

            ret = Read(address, ref rvalue);
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                wTimer.Stop();
                gm.level = 2;
                gm.controls = "SimpleI2C!";
                gm.message = LibErrorCode.GetErrorDescription(ret);
                CallWarningControl(gm);
                return;
            }

            Dispatcher.Invoke(new Action(() =>
            {
                rValueBox.Text = string.Format("{0:x4}", rvalue);
                return;
            }));

        }

        private void crc4Btn_Click(object sender, RoutedEventArgs e)
        {
            byte bval = 0;
            byte[] pdata = new byte[8];

            try
            {
                pdata[0] = Convert.ToByte(Reg00.Text.Trim(), 16);
            }
            catch (System.Exception ex)
            {
                gm.level = 2;
                gm.message = "Please input legal 8bit data";
                CallWarningControl(gm);
                return;
            }
            try
            {
                pdata[1] = Convert.ToByte(Reg01.Text.Trim(), 16);
            }
            catch (System.Exception ex)
            {
                gm.level = 2;
                gm.message = "Please input legal 8bit data";
                CallWarningControl(gm);
                return;
            }
            try
            {
                pdata[2] = Convert.ToByte(Reg02.Text.Trim(), 16);
            }
            catch (System.Exception ex)
            {
                gm.level = 2;
                gm.message = "Please input legal 8bit data";
                CallWarningControl(gm);
                return;
            }
            try
            {
                pdata[3] = Convert.ToByte(Reg03.Text.Trim(), 16);
            }
            catch (System.Exception ex)
            {
                gm.level = 2;
                gm.message = "Please input legal 8bit data";
                CallWarningControl(gm);
                return;
            }
            try
            {
                pdata[4] = Convert.ToByte(Reg04.Text.Trim(), 16);
            }
            catch (System.Exception ex)
            {
                gm.level = 2;
                gm.message = "Please input legal 8bit data";
                CallWarningControl(gm);
                return;
            }
            try
            {
                pdata[5] = Convert.ToByte(Reg05.Text.Trim(), 16);
            }
            catch (System.Exception ex)
            {
                gm.level = 2;
                gm.message = "Please input legal 8bit data";
                CallWarningControl(gm);
                return;
            }
            try
            {
                pdata[6] = Convert.ToByte(Reg06.Text.Trim(), 16);
            }
            catch (System.Exception ex)
            {
                gm.level = 2;
                gm.message = "Please input legal 8bit data";
                CallWarningControl(gm);
                return;
            }
            try
            {
                pdata[7] = Convert.ToByte(Reg07.Text.Trim(), 16);
            }
            catch (System.Exception ex)
            {
                gm.level = 2;
                gm.message = "Please input legal 8bit data";
                CallWarningControl(gm);
                return;
            }

            bval = crc4_calc(pdata, 8);
            Result.Text = string.Format("0x{0:x2}", bval);
        }
    }
}
