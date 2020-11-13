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
using System.Reflection;

namespace Cobra.InitPanel
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
        AsyncObservableCollection<Parameter> paramlist = new AsyncObservableCollection<Parameter>();
        System.Windows.Threading.DispatcherTimer t = new System.Windows.Threading.DispatcherTimer();


        Parameter LDO_Trim1 = new Parameter();
        Parameter LDO_Trim0 = new Parameter();
        Parameter LDO_Trim = new Parameter();
        Parameter Sleep_Mode = new Parameter();
        Parameter BCOffset = new Parameter();

        Parameter VICL = new Parameter();
        //ushort LDO_Trim1, LDO_Trim0, LDO_Trim, Sleep_Mode, BCOffset;

        Parameter ate_lock_primary_1_0 = new Parameter(), osc_trim_1_0 = new Parameter(), bg_v_trim_5_0 = new Parameter(), bg_tc_trim_2_0 = new Parameter(), int_tmp_trim_2_0 = new Parameter(), adcbuf_az_enable = new Parameter(), adc_start_sel = new Parameter(),
            ls5_trim_2_0 = new Parameter(), ls4_trim_2_0 = new Parameter(), ls3_trim_2_0 = new Parameter(), ls2_trim_2_0 = new Parameter(), ls1_trim_2_0 = new Parameter(), packv_trim_2_0 = new Parameter(), packc_trim_2_0 = new Parameter(),
            doc_trim_2_0 = new Parameter(), thm3k_trim_4_0 = new Parameter(), thm60k_trim_4_0 = new Parameter(), trim_reserved_bit_3_0 = new Parameter(), crc_sum_3_0 = new Parameter();
        ParamContainer paramc = new ParamContainer();
        #endregion

        #region Function defination
        public string GetHashTableValueByKey(string str, Hashtable htable)
        {
            foreach (DictionaryEntry de in htable)
            {
                if (de.Key.ToString().Equals(str))
                    return de.Value.ToString();
            }
            return "NoSuchKey";
        }
        public MainControl(object pParent, string name)
        {
            InitializeComponent();

            #region 相关初始化
            parent = (Device)pParent;
            if (parent == null) return;

            sflname = name;
            if (String.IsNullOrEmpty(sflname)) return;
            #endregion

            paramlist = parent.GetParamLists(sflname).parameterlist;
            foreach (Parameter p in paramlist)
            {
                if (GetHashTableValueByKey("Index", p.sfllist[sflname].nodetable) == "0")
                    LDO_Trim1 = p;
                else if (GetHashTableValueByKey("Index", p.sfllist[sflname].nodetable) == "1")
                    LDO_Trim0 = p;
                else if (GetHashTableValueByKey("Index", p.sfllist[sflname].nodetable) == "2")
                    LDO_Trim = p;
                else if (GetHashTableValueByKey("Index", p.sfllist[sflname].nodetable) == "3")
                    Sleep_Mode = p;
                else if (GetHashTableValueByKey("Index", p.sfllist[sflname].nodetable) == "4")
                    BCOffset = p;
                else if (GetHashTableValueByKey("Index", p.sfllist[sflname].nodetable) == "5")
                    VICL = p;

                ///////////////////////////////////////////////////////////////////////////

                if (GetHashTableValueByKey("Description", p.sfllist[sflname].nodetable) == "ate_lock_primary_1_0")
                {
                    ate_lock_primary_1_0 = p;
                    paramc.parameterlist.Add(p);
                }
                else if (GetHashTableValueByKey("Description", p.sfllist[sflname].nodetable) == "osc_trim_1_0")
                {
                    osc_trim_1_0 = p;
                    paramc.parameterlist.Add(p);
                }
                else if (GetHashTableValueByKey("Description", p.sfllist[sflname].nodetable) == "bg_v_trim_5_0")
                {
                    bg_v_trim_5_0 = p;
                    paramc.parameterlist.Add(p);
                }
                else if (GetHashTableValueByKey("Description", p.sfllist[sflname].nodetable) == "bg_tc_trim_2_0")
                {
                    bg_tc_trim_2_0 = p;
                    paramc.parameterlist.Add(p);
                }
                else if (GetHashTableValueByKey("Description", p.sfllist[sflname].nodetable) == "int_tmp_trim_2_0")
                {
                    int_tmp_trim_2_0 = p;
                    paramc.parameterlist.Add(p);
                }
                else if (GetHashTableValueByKey("Description", p.sfllist[sflname].nodetable) == "adcbuf_az_enable")
                {
                    adcbuf_az_enable = p;
                    paramc.parameterlist.Add(p);
                }
                else if (GetHashTableValueByKey("Description", p.sfllist[sflname].nodetable) == "adc_start_sel")
                {
                    adc_start_sel = p;
                    paramc.parameterlist.Add(p);
                }
                else if (GetHashTableValueByKey("Description", p.sfllist[sflname].nodetable) == "ls5_trim_2_0")
                {
                    ls5_trim_2_0 = p;
                    paramc.parameterlist.Add(p);
                }
                else if (GetHashTableValueByKey("Description", p.sfllist[sflname].nodetable) == "ls4_trim_2_0")
                {
                    ls4_trim_2_0 = p;
                    paramc.parameterlist.Add(p);
                }
                else if (GetHashTableValueByKey("Description", p.sfllist[sflname].nodetable) == "ls3_trim_2_0")
                {
                    ls3_trim_2_0 = p;
                    paramc.parameterlist.Add(p);
                }
                else if (GetHashTableValueByKey("Description", p.sfllist[sflname].nodetable) == "ls2_trim_2_0")
                {
                    ls2_trim_2_0 = p;
                    paramc.parameterlist.Add(p);
                }
                else if (GetHashTableValueByKey("Description", p.sfllist[sflname].nodetable) == "ls1_trim_2_0")
                {
                    ls1_trim_2_0 = p;
                    paramc.parameterlist.Add(p);
                }
                else if (GetHashTableValueByKey("Description", p.sfllist[sflname].nodetable) == "packv_trim_2_0")
                {
                    packv_trim_2_0 = p;
                    paramc.parameterlist.Add(p);
                }
                else if (GetHashTableValueByKey("Description", p.sfllist[sflname].nodetable) == "packc_trim_2_0")
                {
                    packc_trim_2_0 = p;
                    paramc.parameterlist.Add(p);
                }
                else if (GetHashTableValueByKey("Description", p.sfllist[sflname].nodetable) == "doc_trim_2_0")
                {
                    doc_trim_2_0 = p;
                    paramc.parameterlist.Add(p);
                }
                else if (GetHashTableValueByKey("Description", p.sfllist[sflname].nodetable) == "thm3k_trim_4_0")
                {
                    thm3k_trim_4_0 = p;
                    paramc.parameterlist.Add(p);
                }
                else if (GetHashTableValueByKey("Description", p.sfllist[sflname].nodetable) == "thm60k_trim_4_0")
                {
                    thm60k_trim_4_0 = p;
                    paramc.parameterlist.Add(p);
                }
                else if (GetHashTableValueByKey("Description", p.sfllist[sflname].nodetable) == "trim_reserved_bit_3_0")
                {
                    trim_reserved_bit_3_0 = p;
                    paramc.parameterlist.Add(p);
                }
                else if (GetHashTableValueByKey("Description", p.sfllist[sflname].nodetable) == "crc_sum_3_0")
                {
                    crc_sum_3_0 = p;
                    paramc.parameterlist.Add(p);
                }
                    
            }
            LibInfor.AssemblyRegister(Assembly.GetExecutingAssembly(), ASSEMBLY_TYPE.SFL);
        }

        #region 通用控件消息响应
        public void CallWarningControl(GeneralMessage message)
        {
            WarningPopControl.Dispatcher.Invoke(new Action(() =>
            {
                WarningPopControl.ShowDialog(message);
            }));
        }
        #endregion

        #region DM提供的API
        public uint Mapping(ParamContainer pc)
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_BLOCK_MAP;
            msg.task_parameterlist = pc;
            //msg.bupdate = bControl;            //需要从chip读数据
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            return m_Msg.errorcode;
        }
        public uint Read(ParamContainer pc)
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_READ;
            msg.task_parameterlist = pc;
            //msg.bupdate = bControl;            //需要从chip读数据
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            return m_Msg.errorcode;
        }
        public uint Write(ParamContainer pc)
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_WRITE;
            msg.task_parameterlist = pc;
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            return m_Msg.errorcode;
        }
        public uint ConvertHexToPhysical(ParamContainer pc)
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_CONVERT_HEXTOPHYSICAL;
            msg.task_parameterlist = pc;
            //msg.bupdate = true;         //不用从chip读，只从img读
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            return m_Msg.errorcode;
        }
        public uint ConvertPhysicalToHex(ParamContainer pc)
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_CONVERT_PHYSICALTOHEX;
            msg.task_parameterlist = pc;
            //msg.bupdate = true;         //不用从chip读，只从img读
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            return m_Msg.errorcode;
        }
        public uint GetSysInfo()
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_SPEICAL_GETSYSTEMINFOR;
            //msg.bupdate = bControl;            //需要从chip读数据
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            return m_Msg.errorcode;
        }
        public uint GetDevInfo()
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_SPEICAL_GETDEVICEINFOR;
            //msg.bupdate = bControl;            //需要从chip读数据
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            return m_Msg.errorcode;
        }
        public uint GetRegInfo()
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_SPEICAL_GETREGISTEINFOR;
            //msg.bupdate = bControl;            //需要从chip读数据
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            return m_Msg.errorcode;
        }
        public uint ClearBit(ParamContainer pc)
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_BITOPERATION;
            msg.task_parameterlist = pc;
            //msg.bupdate = bControl;            //需要从chip读数据
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            return m_Msg.errorcode;
        }
        #endregion

        private void ClearBCOffset_Click(object sender, RoutedEventArgs e)
        {
            uint errorcode = 0;

            ParamContainer pc = new ParamContainer();
            BCOffset.phydata = 0;
            pc.parameterlist.Add(BCOffset);
            errorcode = ConvertPhysicalToHex(pc);
            if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {

                gm.message = LibErrorCode.GetErrorDescription(errorcode);
                CallWarningControl(gm);
                return;
            }
            errorcode = Write(pc);
            if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {

                gm.message = LibErrorCode.GetErrorDescription(errorcode);
                CallWarningControl(gm);
                return;
            }
            MessageBox.Show("Done!");
        }

        #endregion

        private void Init_Click(object sender, RoutedEventArgs e)
        {
            uint errorcode = 0;

            ParamContainer pc = new ParamContainer();
            pc.parameterlist.Add(LDO_Trim0);
            pc.parameterlist.Add(LDO_Trim1);
            errorcode = Read(pc); 
            if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {

                gm.message = LibErrorCode.GetErrorDescription(errorcode);
                CallWarningControl(gm);
                return;
            }
            LDO_Trim.phydata = LDO_Trim0.hexdata + LDO_Trim1.hexdata * 2;
            pc.parameterlist.Clear();
            pc.parameterlist.Add(LDO_Trim);
            errorcode = ConvertPhysicalToHex(pc);
            if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {

                gm.message = LibErrorCode.GetErrorDescription(errorcode);
                CallWarningControl(gm);
                return;
            }
            errorcode = Write(pc);
            if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {

                gm.message = LibErrorCode.GetErrorDescription(errorcode);
                CallWarningControl(gm);
                return;
            }

            //-------------------------------------------------------------------
            errorcode = Read(pc);
            if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {

                gm.message = LibErrorCode.GetErrorDescription(errorcode);
                CallWarningControl(gm);
                return;
            }

            errorcode = ConvertHexToPhysical(pc); ;
            if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {

                gm.message = LibErrorCode.GetErrorDescription(errorcode);
                CallWarningControl(gm);
                return;
            }

            if (LDO_Trim.phydata != LDO_Trim0.hexdata + LDO_Trim1.hexdata * 2)
            {
                MessageBox.Show("Failed! Please try again!");
                return;
            }
            //-------------------------------------------------------------------
            pc.parameterlist.Clear();
            pc.parameterlist.Add(Sleep_Mode);
            //ClearBit(pc);
            Sleep_Mode.phydata = 0;
            errorcode = ConvertPhysicalToHex(pc);
            if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {

                gm.message = LibErrorCode.GetErrorDescription(errorcode);
                CallWarningControl(gm);
                return;
            }
            errorcode = Write(pc);
            if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {

                gm.message = LibErrorCode.GetErrorDescription(errorcode);
                CallWarningControl(gm);
                return;
            }
            MessageBox.Show("Done!");
        }

        private void LogicLow_Click(object sender, RoutedEventArgs e)
        {
            OnLogicLow();
        }

        private void LogicHigh_Click(object sender, RoutedEventArgs e)
        {
            OnLogicHigh();
        }

        private void OnLogicLow()
        {
            WriteVICL(0);
        }

        private void OnLogicHigh()
        {
            WriteVICL(5);
        }

        private byte ReadVICL()
        {
            uint errorcode = 0;

            ParamContainer pc = new ParamContainer();
            //VICL.phydata = data;
            pc.parameterlist.Add(VICL);
            errorcode = Read(pc);
            if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {

                gm.message = LibErrorCode.GetErrorDescription(errorcode);
                CallWarningControl(gm);
                return 0;
            }
            errorcode = ConvertHexToPhysical(pc);
            if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {

                gm.message = LibErrorCode.GetErrorDescription(errorcode);
                CallWarningControl(gm);
                return 0;
            }
            return (byte)VICL.phydata;
        }

        private void WriteVICL(byte data)
        {
            uint errorcode = 0;

            ParamContainer pc = new ParamContainer();
            VICL.phydata = data;
            pc.parameterlist.Add(VICL);
            errorcode = ConvertPhysicalToHex(pc);
            if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {

                gm.message = LibErrorCode.GetErrorDescription(errorcode);
                CallWarningControl(gm);
                return;
            }
            errorcode = Write(pc);
            if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {

                gm.message = LibErrorCode.GetErrorDescription(errorcode);
                CallWarningControl(gm);
                return;
            }
        }

        private void Rest(Int32 mS, bool choice)
        {
            if (choice)
            {
                t.Interval = TimeSpan.FromMilliseconds(mS - 5);
                t.Tick += new EventHandler(t_Elapsed);
                t.Start();
                while (t.IsEnabled) ;
            }
            else
            {
                Thread.Sleep(mS - Convert.ToInt32(TimerOffset.Text));
            }
        }
        void t_Elapsed(object sender, EventArgs e)
        {
            t.Stop();
        }

        private void Decrease_Click(object sender, RoutedEventArgs e)
        {
            byte temp = 0;
            bool bControl = false;

            temp = ReadVICL();

            OnLogicLow();
            Rest(150, bControl);

            OnLogicHigh();
            Rest(300, bControl);
            OnLogicLow();
            Rest(100, bControl);
            OnLogicHigh();
            Rest(300, bControl);
            OnLogicLow();
            Rest(100, bControl);
            OnLogicHigh();
            Rest(300, bControl);
            OnLogicLow();
            Rest(100, bControl);
            OnLogicHigh();
            Rest(100, bControl);
            OnLogicLow();
            Rest(100, bControl);
            OnLogicHigh();
            Rest(100, bControl);
            OnLogicLow();
            Rest(100, bControl);
            OnLogicHigh();
            Rest(500, bControl);

            OnLogicLow();
            Rest(150, bControl);

            WriteVICL(temp);
        }

        private void Increase_Click(object sender, RoutedEventArgs e)
        {
            byte temp = 0;
            bool bControl = false;

            temp = ReadVICL();

            OnLogicLow();
            Rest(150, bControl);

            OnLogicHigh();
            Rest(100, bControl);
            OnLogicLow();
            Rest(100, bControl);
            OnLogicHigh();
            Rest(100, bControl);
            OnLogicLow();
            Rest(100, bControl);
            OnLogicHigh();
            Rest(300, bControl);
            OnLogicLow();
            Rest(100, bControl);
            OnLogicHigh();
            Rest(300, bControl);
            OnLogicLow();
            Rest(100, bControl);
            OnLogicHigh();
            Rest(300, bControl);
            OnLogicLow();
            Rest(100, bControl);
            OnLogicHigh();
            Rest(500, bControl);

            OnLogicLow();
            Rest(150, bControl);

            WriteVICL(temp);
        }

        private void CRC_Click(object sender, RoutedEventArgs e)
        {
            uint errorcode = 0;
            byte[] otp_4bit_data = new byte[16];
            byte[] tmp = new byte[8];
            byte crc_sum_calc, crc_err;

            errorcode = Read(paramc);
            if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {

                gm.message = LibErrorCode.GetErrorDescription(errorcode);
                CallWarningControl(gm);
                return;
            }

            errorcode = ConvertHexToPhysical(paramc);
            if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {

                gm.message = LibErrorCode.GetErrorDescription(errorcode);
                CallWarningControl(gm);
                return;
            }


            foreach (Parameter p in paramc.parameterlist)
            {
                FolderMap.WriteFile(GetHashTableValueByKey("Description", p.sfllist[sflname].nodetable) + "\t\t is \t\t0x" + Convert.ToByte(p.phydata).ToString("X2") + "\t\t");
            }
            tmp[7] = (byte)((Convert.ToByte(osc_trim_1_0.phydata) << 6) | Convert.ToByte(bg_v_trim_5_0.phydata));
            tmp[6] = (byte)((Convert.ToByte(bg_tc_trim_2_0.phydata) << 5) | (Convert.ToByte(int_tmp_trim_2_0.phydata) << 2) | (Convert.ToByte(adcbuf_az_enable.phydata) << 1) | Convert.ToByte(adc_start_sel.phydata));
            tmp[5] = (byte)((Convert.ToByte(ls5_trim_2_0.phydata) << 5) | (Convert.ToByte(ls4_trim_2_0.phydata) << 2) | (Convert.ToByte(ls3_trim_2_0.phydata) >> 1));
            tmp[4] = (byte)((Convert.ToByte(ls3_trim_2_0.phydata) << 7) | (Convert.ToByte(ls2_trim_2_0.phydata) << 4) | (Convert.ToByte(ls1_trim_2_0.phydata) << 1) | (Convert.ToByte(packv_trim_2_0.phydata) >> 2));
            tmp[3] = (byte)((Convert.ToByte(packv_trim_2_0.phydata) << 6) | (Convert.ToByte(packc_trim_2_0.phydata) << 3) | Convert.ToByte(doc_trim_2_0.phydata));
            tmp[2] = (byte)((Convert.ToByte(thm3k_trim_4_0.phydata) << 3) | (Convert.ToByte(thm60k_trim_4_0.phydata) >> 2));
            tmp[1] = (byte)((Convert.ToByte(thm60k_trim_4_0.phydata) << 6) | (Convert.ToByte(trim_reserved_bit_3_0.phydata) << 2));
            tmp[0] = (byte)((Convert.ToByte(ate_lock_primary_1_0.phydata) << 4) | Convert.ToByte(crc_sum_3_0.phydata));
            //otp_4bit_data[15] = (byte)((Convert.ToByte(osc_trim_1_0.phydata) << 2) | (Convert.ToByte(bg_v_trim_5_0.phydata) >> 4));
            //otp_4bit_data[14] = (byte)(Convert.ToByte(bg_v_trim_5_0.phydata) & 0x0f);
            //otp_4bit_data[13] = (byte)((Convert.ToByte(bg_tc_trim_2_0.phydata) << 1) | (Convert.ToByte(int_tmp_trim_2_0.phydata) >> 2));
            //otp_4bit_data[12] = (byte)(((Convert.ToByte(int_tmp_trim_2_0.phydata) & 0x03) << 2) | (Convert.ToByte(adcbuf_az_enable.phydata) << 1) | Convert.ToByte(adc_start_sel.phydata));

            for (byte i = 0; i < 8; i++)
            {
                otp_4bit_data[2*i] = (byte)(tmp[i] & 0x0f);
                //FolderMap.WriteFile("otp" + (2*i).ToString() + "\t\t is \t\t0x" + otp_4bit_data[i].ToString("X2") + "\t\t");

                otp_4bit_data[2*i + 1] = (byte)((tmp[i] & 0xf0) >> 4);
                //FolderMap.WriteFile("otp" + (2*i+1).ToString() + "\t\t is \t\t0x" + otp_4bit_data[i + 1].ToString("X2") + "\t\t");
            }

            for (byte i = 0; i < 16; i++)
            {
                FolderMap.WriteFile("otp" + i.ToString() + "\t\t is \t\t0x" + otp_4bit_data[i].ToString("X2") + "\t\t");
            }
                crc_sum_calc = calc_crc_sum(otp_4bit_data, 15, 1);
            FolderMap.WriteFile("crc_sum_calc \t\t is \t\t0x"+crc_sum_calc.ToString("X2") + "\t\t");
            //fprintf(stderr, "Y-flash crc_sum: 0x%x\n", crc_sum_calc);
            string msg = "Utility crc_sum: 0x" + crc_sum_calc.ToString("X2") + "\n";
            // crc_sum check
            //otp_4bit_data[0]=crc_sum_calc;
            //crc_err = calc_crc_sum(otp_4bit_data, 15, 0);
            //FolderMap.WriteFile("crc_err \t\t is \t\t0x" + crc_err.ToString("X2") + "\t\t");
            //fprintf(stderr, "Y-flash crc_error: 0x%x\n", crc_err);//    ??crc_sum_calc==otp_4bit_data[0];??
            msg += "Y-flash crc_sum: 0x" + ((byte)(crc_sum_3_0.phydata)).ToString("X2") + "\n";
            if (crc_sum_calc == (byte)crc_sum_3_0.phydata)
                msg += "CRC is OK!";
            else
                msg += "CRC is wrong!";

            MessageBox.Show(msg);

        }

        byte calc_crc_sum(byte[] pdata, int first, int last)
        {

            byte crc = 0;
            byte crcdata;
            uint d = 0;
            byte poly = 0x07;             // poly
            uint p = (uint)poly + 0x100;
            int n, j;                                      // the length of the data

            if (first < last) 
                for (n = first; n <= last; n++)
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
            else 
                for (n = first; n >= last; n--)
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

    }
}
