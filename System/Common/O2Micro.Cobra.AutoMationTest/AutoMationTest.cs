using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Collections.ObjectModel;
using O2Micro.Cobra.Common;
using System.Collections;

namespace O2Micro.Cobra.AutoMationTest
{
    public class AutoMationTest
    {
        private static ParamContainer m_ParamListContainer = null;
        private static Dictionary<UInt32, COBRA_HWMode_Reg[]> m_HwMode_RegList = null;
        private static Dictionary<UInt32, ObservableCollection<Parameter>> m_param_List = new Dictionary<UInt32, ObservableCollection<Parameter>>();
        private static BusOptions busOpSetting = null;
        public static bool bErrGenerate { get; set; }
        public static UInt16 u16Sensor { get; set; }
        public static bool bErrOutMax { get; set; }
        public static bool bErrOutMin { get; set; }
        public static bool bErrPEC { get; set; }
        public static bool bPECEnable { get; set; }
        public static bool bErrCRC { get; set; }
        public static bool bMakeFakeCRC { get; set; }
        public static bool bIsCRCRegister { get; set; }
        private static TASKMessage m_taskMsg = null;
        public static string strATMErrDescrip = string.Empty;

        private static UInt32 ErrOutMaxCounter = new ushort();
        public static UInt32 wErrOutMaxCounter
        {
            get { return ErrOutMaxCounter; }
            set { ErrOutMaxCounter = value; GlobalData.summaryInfo.GoMax = wErrOutMaxCounter.ToString(); }
        }

        private static UInt32 ErrOutMinCounter = new ushort();
        public static UInt32 wErrOutMinCounter
        {
            get { return ErrOutMinCounter; }
            set { ErrOutMinCounter = value; GlobalData.summaryInfo.GoMin = wErrOutMinCounter.ToString(); }
        }
        private static UInt32 ErrPECCounter = new ushort();
        public static UInt32 wErrPECCounter
        {
            get { return ErrPECCounter; }
            set { ErrPECCounter = value; GlobalData.summaryInfo.GPEC = wErrPECCounter.ToString(); }
        }
        private static UInt32 ErrCRCCounter = new ushort();
        public static UInt32 wErrCRCCounter
        {
            get { return ErrCRCCounter; }
            set { ErrCRCCounter = value; GlobalData.summaryInfo.GCRC = wErrCRCCounter.ToString(); }
        }

        public static UInt32 wErrSummary
        {
            get { GlobalData.summaryInfo.GTol = (wErrOutMaxCounter + wErrOutMinCounter + wErrPECCounter + wErrCRCCounter).ToString(); return (UInt16)(wErrOutMaxCounter + wErrOutMinCounter + wErrPECCounter + wErrCRCCounter); }
        }
        public static UInt32 wTotalRun { get; set; }

/// ////////////////////////////////////////////////////////////////////////////////////////

        private static UInt32 m_HOMaxCtr = new ushort();
        public static UInt32 HOMaxCtr
        {
            get { return m_HOMaxCtr; }
            set { m_HOMaxCtr = value; GlobalData.summaryInfo.ChoMax = HOMaxCtr.ToString(); }
        }
        private static UInt32 m_HOMinCtr = new ushort();
        public static UInt32 HOMinCtr
        {
            get { return m_HOMinCtr; }
            set { m_HOMinCtr = value; GlobalData.summaryInfo.ChoMin = HOMinCtr.ToString(); }
        }
        private static UInt32 m_POMaxCtr = new ushort();
        public static UInt32 POMaxCtr
        {
            get { return m_POMaxCtr; }
            set { m_POMaxCtr = value; GlobalData.summaryInfo.CpoMax = POMaxCtr.ToString(); }
        }
        private static UInt32 m_POMinCtr = new ushort();
        public static UInt32 POMinCtr
        {
            get { return m_POMinCtr; }
            set { m_POMinCtr = value; GlobalData.summaryInfo.CpoMin = POMinCtr.ToString(); }
        }

        private static UInt32 m_PECCtr = new ushort();
        public static UInt32 PECCtr
        {
            get { return m_PECCtr; }
            set { m_PECCtr = value; GlobalData.summaryInfo.CPEC = PECCtr.ToString(); }
        }

        private static UInt32 m_CRCCtr = new ushort();
        public static UInt32 CRCCtr
        {
            get { return m_CRCCtr; }
            set { m_CRCCtr = value; GlobalData.summaryInfo.CCRC = CRCCtr.ToString(); }
        }

        public static UInt32 ErrSummary
        {
            get
            {
                return (UInt32)(HOMaxCtr + HOMinCtr + POMaxCtr + POMinCtr + PECCtr + CRCCtr);
            }
        }
        public static Reg regCRCInfor { get; set; }
        public static Random rndRandVal = new Random();

        private string GetHashTableValueByKey(string str, Hashtable htable)
        {
            foreach (DictionaryEntry de in htable)
            {
                if (de.Key.ToString().Equals(str))
                    return de.Value.ToString();
            }
            return "NoSuchKey";
        }

        public static void init(Dictionary<UInt32, COBRA_HWMode_Reg[]> reglist)
        {
            m_HwMode_RegList = reglist;
            #region Create AMT Table    //added by leon

            if (DBManager.supportdb == true)
            {
                //string colname;
                string[] strColHeader = { "CADDR", "VALUE", "ERRORTYPE", "CID", "GUID", "NICKNAME", "ERRORCODE", "DESCRIPTION", "API", "REGADDR", "PVALUE", "HVALUE" };
                Dictionary<string, DBManager.DataType> columns = new Dictionary<string, DBManager.DataType>();
                foreach (string colname in strColHeader)
                {
                    columns.Add(colname, DBManager.DataType.TEXT);
                }
                //columns.Add("Time", DBManager.DataType.TEXT);
                int ret = DBManager.CreateTableN("AMT", columns);
                if (ret != 0)
                    System.Windows.MessageBox.Show("Create AMT Table Failed!");
            }
            #endregion
        }

        #region AutoMationTest线程
        public static void CompletedWork(TASKMessage taskMsg)
        {
            if (taskMsg.gm.sflname == null) return;
            if (taskMsg.gm.sflname.Equals("DeviceConfig") || taskMsg.gm.sflname.Equals("EfuseConfig") || taskMsg.gm.sflname.Equals("EFUSE Config"))
            {
                m_taskMsg = taskMsg;
                UpdateParamByRegMap();
            }
        }

        public static void ParseParamByRegType(ParamContainer paramContainer)
        {
            if (m_HwMode_RegList == null) return;
            if (m_HwMode_RegList.Count == 0) return;
            if (paramContainer == null) return;

            m_param_List.Clear();
            m_ParamListContainer = paramContainer;

            foreach (UInt32 key in m_HwMode_RegList.Keys)
            {
                ObservableCollection<Parameter> paramList = new ObservableCollection<Parameter>();
                foreach (Parameter p in m_ParamListContainer.parameterlist)
                {
                    if (p == null) continue;
                    if ((p.guid & ElementDefine.ElementMask) == key) paramList.Add(p);
                }
                m_param_List.Add(key, paramList);
            }
        }

        enum ErrTpy
        {
            SUCCESS,
            HOMax,
            HOMin,
            POMax,
            POMin
        }
        private static void UpdateParamByRegMap()
        {
            Reg reg = null;
            COBRA_HWMode_Reg hwreg = null;
            byte baddress = 0;
            List<byte> YFLASHReglist = new List<byte>();
            Parameter param = null;
            ObservableCollection<Parameter> paramList = null;
            COBRA_HWMode_Reg[] hwReglist;
            //COBRA_HWMode_Reg[] hwYFReglist;

            if (m_HwMode_RegList == null) return;
            if (m_HwMode_RegList.Count == 0) return;
            if (m_ParamListContainer == null) return;
            if (m_param_List.Count == 0) return;
            switch (m_taskMsg.task)
            {
                case TM.TM_READ:
                case TM.TM_WRITE:
                    {
                        foreach (UInt32 key in m_HwMode_RegList.Keys)
                        {
                            paramList = m_param_List[key];
                            if (paramList.Count == 0) continue;
                            hwReglist = m_HwMode_RegList[key];
                            if (hwReglist.Length == 0) continue;

                            for (int i = 0; i < paramList.Count; i++)
                            {
                                param = paramList[i];

                                param.errorcode = LibErrorCode.IDS_ERR_SUCCESSFUL;

                                foreach (KeyValuePair<string, Reg> dic in param.reglist)
                                {
                                    reg = dic.Value;
                                    baddress = (byte)reg.address;
                                    YFLASHReglist.Add(baddress);
                                }
                                if (param.reglist.ContainsKey("High"))
                                    param.errorcode |= hwReglist[param.reglist["High"].address].err;

                                if (param.reglist.ContainsKey("Low"))
                                    param.errorcode |= hwReglist[param.reglist["Low"].address].err;
                            }

                            YFLASHReglist = YFLASHReglist.Distinct().ToList();
                            //counter 考虑addr重复的问题
                            UInt32 original = CRCCtr;
                            //foreach (COBRA_HWMode_Reg treg in hwReglist)
                            for (int i = 0; i < YFLASHReglist.Count; i++)
                            {
                                hwreg = hwReglist[YFLASHReglist[i]];
                                switch (hwreg.err)
                                {
                                    case LibErrorCode.IDS_ERR_BUS_DATA_PEC_ERROR:
                                        PECCtr++;
                                        break;
                                    case LibErrorCode.IDS_ERR_DEM_ATE_CRC_ERROR:
                                        if (original == CRCCtr)
                                            CRCCtr++;
                                        break;
                                }
                            }
                        }

                        break;
                    }
                case TM.TM_CONVERT_PHYSICALTOHEX:
                case TM.TM_CONVERT_HEXTOPHYSICAL:
                    {

                        List<List<ErrTpy>> errlist = new List<List<ErrTpy>>();
                        foreach (UInt32 key in m_HwMode_RegList.Keys)
                        {
                            paramList = m_param_List[key];
                            if (paramList.Count == 0) continue;
                            hwReglist = m_HwMode_RegList[key];
                            if (hwReglist.Length == 0) continue;


                            foreach (COBRA_HWMode_Reg treg1 in hwReglist)
                            {
                                //ErrTpy b = ErrTpy.SUCCESS;
                                List<ErrTpy> b = new List<ErrTpy>();
                                errlist.Add(b);
                            }

                            for (int i = 0; i < paramList.Count; i++)
                            {
                                param = paramList[i];
                                param.errorcode = LibErrorCode.IDS_ERR_SUCCESSFUL;
                                try
                                {
                                    if (param.reglist.ContainsKey("High"))
                                    {
                                        if (hwReglist[param.reglist["High"].address].err != LibErrorCode.IDS_ERR_SUCCESSFUL)
                                            continue;    //如果RW过程中已经出错了，就不再判断phy的错误了
                                    }
                                    if (param.reglist.ContainsKey("Low"))
                                    {
                                        if (hwReglist[param.reglist["Low"].address].err != LibErrorCode.IDS_ERR_SUCCESSFUL)
                                            continue;    //如果RW过程中已经出错了，就不再判断phy的错误了
                                    }
                                }
                                catch (System.Exception ex)
                                {
                                    continue;
                                }

                                List<ErrTpy> b = new List<ErrTpy>();
                                if (param.hexdata < param.dbHexMin) //改为if...else if结构，如果已经HEX越界，就不去判断PHY越界了
                                {
                                    param.errorcode = LibErrorCode.IDS_ERR_PARAM_HEX_DATA_OVERMINRANGE;
                                    //errlist[param.reglist["Low"].address] = ErrTpy.OMin;
                                    ErrTpy et = new ErrTpy();
                                    et = ErrTpy.HOMin;
                                    b.Add(et);
                                }

                                else if (param.hexdata > param.dbHexMax)
                                {
                                    param.errorcode = LibErrorCode.IDS_ERR_PARAM_HEX_DATA_OVERMAXRANGE;
                                    //errlist[param.reglist["Low"].address] = ErrTpy.OMax;
                                    ErrTpy et = new ErrTpy();
                                    et = ErrTpy.HOMax;
                                    b.Add(et);
                                }

                                else if (param.phydata < param.dbPhyMin)
                                {
                                    param.errorcode = LibErrorCode.IDS_ERR_PARAM_PHY_DATA_OVERMINRANGE;
                                    //errlist[param.reglist["Low"].address] = ErrTpy.OMin;
                                    ErrTpy et = new ErrTpy();
                                    et = ErrTpy.POMin;
                                    b.Add(et);
                                }

                                else if (param.phydata > param.dbPhyMax)
                                {
                                    param.errorcode = LibErrorCode.IDS_ERR_PARAM_PHY_DATA_OVERMAXRANGE;
                                    //errlist[param.reglist["Low"].address] = ErrTpy.OMax;
                                    ErrTpy et = new ErrTpy();
                                    et = ErrTpy.POMax;
                                    b.Add(et);
                                }
                                if(b.Count != 0)
                                    errlist[param.reglist["Low"].address] = b;
                            }
                            foreach (List<ErrTpy> b in errlist)
                            {
                                /*if (et == ErrTpy.OMax)
                                    OMaxCtr++;
                                else if (et == ErrTpy.OMin)
                                    OMinCtr++;*/
                                foreach (ErrTpy et in b)
                                {
                                    if (et == ErrTpy.HOMax)
                                        HOMaxCtr++;
                                    else if (et == ErrTpy.HOMin)
                                        HOMinCtr++;
                                    else if (et == ErrTpy.POMax)
                                        POMaxCtr++;
                                    else if (et == ErrTpy.POMin)
                                        POMinCtr++;
                                }
                            }
                        }
                        break;
                    }
                default:
                    break;
            }
            GlobalData.summaryInfo.CTol = (HOMaxCtr + HOMinCtr + POMaxCtr + POMinCtr + PECCtr + CRCCtr).ToString();
            //GlobalData.summaryInfo.Rate = (((float)AutoMationTest.ErrSummary / (float)AutoMationTest.wErrSummary) * 100).ToString("F2") + "%";
            string str;
            float f;

            f = ((float)AutoMationTest.HOMaxCtr / (float)AutoMationTest.ErrOutMaxCounter);
            if (float.IsNaN(f))
                str = "NA";
            else
                str = (f * 100).ToString("F2") + "%";
            GlobalData.summaryInfo.HomaxRate = str;

            f = ((float)AutoMationTest.HOMinCtr / (float)AutoMationTest.ErrOutMinCounter);
            if (float.IsNaN(f))
                str = "NA";
            else
                str = (f * 100).ToString("F2") + "%";
            GlobalData.summaryInfo.HominRate = str;

            f = (
                    (
                        (float)AutoMationTest.POMinCtr + 
                        (float)AutoMationTest.POMaxCtr + 
                        (float)AutoMationTest.HOMaxCtr + 
                        (float)AutoMationTest.HOMinCtr + 
                        (float)AutoMationTest.PECCtr + 
                        (float)AutoMationTest.CRCCtr
                    ) 
                        / 
                    (
                        (float)AutoMationTest.ErrOutMinCounter + 
                        (float)AutoMationTest.ErrOutMaxCounter +
                        (float)AutoMationTest.ErrCRCCounter +
                        (float)AutoMationTest.ErrPECCounter
                    )
                );
            if (float.IsNaN(f))
                str = "NA";
            else
                str = (f * 100).ToString("F2") + "%";
            GlobalData.summaryInfo.TotalRate = str;

            f = ((float)AutoMationTest.PECCtr / (float)AutoMationTest.ErrPECCounter);
            if (float.IsNaN(f))
                str = "NA";
            else
                str = (f * 100).ToString("F2") + "%";
            GlobalData.summaryInfo.PECRate = str;

            f = ((float)AutoMationTest.CRCCtr / (float)AutoMationTest.ErrCRCCounter);
            if (float.IsNaN(f))
                str = "NA";
            else
                str = (f * 100).ToString("F2") + "%";
            GlobalData.summaryInfo.CRCRate = str;
            saveToDB();
        }

        private static void saveToDB()
        {
            //Parameter param = null;
            if (m_taskMsg.task == TM.TM_READ || m_taskMsg.task == TM.TM_WRITE || m_taskMsg.task == TM.TM_CONVERT_HEXTOPHYSICAL || m_taskMsg.task == TM.TM_CONVERT_PHYSICALTOHEX)
            {
                ObservableCollection<Parameter> paramList = null;
                COBRA_HWMode_Reg[] hwReglist;

                if (m_HwMode_RegList == null) return;
                if (m_HwMode_RegList.Count == 0) return;
                if (m_ParamListContainer == null) return;
                if (m_param_List.Count == 0) return;

                foreach (UInt32 key in m_HwMode_RegList.Keys)
                {
                    paramList = m_param_List[key];
                    if (paramList.Count == 0) continue;
                    hwReglist = m_HwMode_RegList[key];
                    if (hwReglist.Length == 0) continue;

                    foreach (Parameter param in paramList)
                    {
                        foreach (DictionaryEntry de in param.sfllist[m_taskMsg.gm.sflname].nodetable)
                        {
                            switch (de.Key.ToString())
                            {
                                case "NickName":
                                    AutomationTestLog.newrow[AutomationTestLog.LogHeaders[(int)AutomationTestLogHeader.NICKNAME]] = de.Value.ToString();
                                    break;
                                //case "Description":
                                    //AutomationTestLog.newrow[AutomationTestLog.LogHeaders[(int)AutomationTestLogHeader.DESCRIPTION]] = de.Value.ToString();
                                    //break;
                                default:
                                    break;
                            }
                        }
                        AutomationTestLog.newrow[AutomationTestLog.LogHeaders[(int)AutomationTestLogHeader.CADDR]] = "0x" + param.reglist["Low"].address.ToString("X2");
                        AutomationTestLog.newrow[AutomationTestLog.LogHeaders[(int)AutomationTestLogHeader.VALUE]] = "0x" + hwReglist[param.reglist["Low"].address].val.ToString("X4");
                        AutomationTestLog.newrow[AutomationTestLog.LogHeaders[(int)AutomationTestLogHeader.ERRORTYPE]] = "0x" + hwReglist[param.reglist["Low"].address].err.ToString("X8");
                        AutomationTestLog.newrow[AutomationTestLog.LogHeaders[(int)AutomationTestLogHeader.CID]] = "0x" + hwReglist[param.reglist["Low"].address].cid.ToString("X8");
                        AutomationTestLog.newrow[AutomationTestLog.LogHeaders[(int)AutomationTestLogHeader.GUID]] = "0x" + param.guid.ToString("X8");
                        AutomationTestLog.newrow[AutomationTestLog.LogHeaders[(int)AutomationTestLogHeader.ERRORCODE]] = "0x" + param.errorcode.ToString("X8");
                        AutomationTestLog.newrow[AutomationTestLog.LogHeaders[(int)AutomationTestLogHeader.DESCRIPTION]] = LibErrorCode.GetErrorDescription(param.errorcode).Replace(",", " ");
                        AutomationTestLog.newrow[AutomationTestLog.LogHeaders[(int)AutomationTestLogHeader.API]] = m_taskMsg.task.ToString();

                        AutomationTestLog.newrow[AutomationTestLog.LogHeaders[(int)AutomationTestLogHeader.REGADDR]] = "0x" + param.reglist["Low"].address.ToString("X2");
                        AutomationTestLog.newrow[AutomationTestLog.LogHeaders[(int)AutomationTestLogHeader.PVALUE]] = (m_taskMsg.task == TM.TM_READ || m_taskMsg.task == TM.TM_WRITE) ? "NA" : param.phydata.ToString("F2");
                        AutomationTestLog.newrow[AutomationTestLog.LogHeaders[(int)AutomationTestLogHeader.HVALUE]] = "0x" + param.hexdata.ToString("X4");
                        AutomationTestLog.AddOneRow();
                    }

                    #region Database NowRow
                    if (DBManager.supportdb == true)
                    {
                        List<Dictionary<string, string>> records = new List<Dictionary<string, string>>();
                        foreach (Parameter param in paramList)
                        {
                            Dictionary<string, string> record = new Dictionary<string, string>();
                            foreach (DictionaryEntry de in param.sfllist[m_taskMsg.gm.sflname].nodetable)
                            {
                                switch (de.Key.ToString())
                                {
                                    case "NickName":
                                        record["NickName"] = de.Value.ToString();
                                        break;
                                    //case "Description":
                                    //record["Description"] = de.Value.ToString();
                                    //break;
                                    default:
                                        break;
                                }
                            }
                            record["CADDR"] = "0x" + param.reglist["Low"].address.ToString("X2");
                            record["VALUE"] = "0x" + hwReglist[param.reglist["Low"].address].val.ToString("X4");
                            record["ERRORTYPE"] = "0x" + hwReglist[param.reglist["Low"].address].err.ToString("X8");
                            record["CID"] = "0x" + hwReglist[param.reglist["Low"].address].cid.ToString("X8");
                            record["GUID"] = "0x" + param.guid.ToString("X8");
                            record["ERRORCODE"] = "0x" + param.errorcode.ToString("X8");
                            record["DESCRIPTION"] = LibErrorCode.GetErrorDescription(param.errorcode).Replace(",", " ");
                            record["API"] = m_taskMsg.task.ToString();

                            record["REGADDR"] = "0x" + param.reglist["Low"].address.ToString("X2");
                            record["PVALUE"] = (m_taskMsg.task == TM.TM_READ || m_taskMsg.task == TM.TM_WRITE) ? "NA" : param.phydata.ToString("F2");
                            record["HVALUE"] = (m_taskMsg.task == TM.TM_READ || m_taskMsg.task == TM.TM_WRITE) ? "NA" : param.hexdata.ToString("X4");
                            records.Add(record);
                        }
                        int ret = DBManager.NewRows("AMT", records);
                        if (ret != 0)
                            System.Windows.MessageBox.Show("AMT New Rows Failed!");
                    }
                    #endregion
                }
            }
        }
        #endregion

        #region Fake data preparation and inspection

        public static bool SetupBusOption(BusOptions busIn)
        {

            //}

            //public static bool GetATMSetting()
            //{
            bool bReturn = true;
            AutomationElement ATMElment = null;
            Options optTmp = null;

            busOpSetting = busIn;

            //get Error Generating setting
            ATMElment = busOpSetting.GetATMElementbyGuid(AutomationElement.GUIDATMTestRandomError);
            if (ATMElment != null)
            {
                if (ATMElment.dbValue != 0)
                {
                    bErrGenerate = true;
                }
                else
                {
                    bErrGenerate = false;
                }
                bReturn &= true;
            }
            else
            {
                bErrGenerate = true;    //default set as true
            }

            //get TestSensitive setting
            ATMElment = busOpSetting.GetATMElementbyGuid(AutomationElement.GUIDATMTestSensitive);
            if (ATMElment != null)
            {
                u16Sensor = (UInt16)ATMElment.dbValue;
                bReturn &= true;
            }

            //get OutofMax setting
            ATMElment = busOpSetting.GetATMElementbyGuid(AutomationElement.GUIDATMTestOutofMaxError);
            if (ATMElment != null)
            {
                if (ATMElment.dbValue != 0)
                {
                    bErrOutMax = true;
                }
                else
                {
                    bErrOutMax = false;
                }
                bReturn &= true;
            }

            //get OutofMin setting
            ATMElment = busOpSetting.GetATMElementbyGuid(AutomationElement.GUIDATMTestOutofMinError);
            if (ATMElment != null)
            {
                if (ATMElment.dbValue != 0)
                {
                    bErrOutMin = true;
                }
                else
                {
                    bErrOutMin = false;
                }
                bReturn &= true;
            }

            //get PEC setting
            ATMElment = busOpSetting.GetATMElementbyGuid(AutomationElement.GUIDATMTestPECError);
            if (ATMElment != null)
            {
                if (ATMElment.dbValue != 0)
                {
                    bErrPEC = true;
                }
                else
                {
                    bErrPEC = false;
                }
                bReturn &= true;
            }

            //get CRC setting
            ATMElment = busOpSetting.GetATMElementbyGuid(AutomationElement.GUIDATMTestCRCError);
            if (ATMElment != null)
            {
                if (ATMElment.dbValue != 0)
                {
                    bErrCRC = true;
                }
                else
                {
                    bErrCRC = false;
                }
                bReturn &= true;
            }

            if (busOpSetting.BusType == BUS_TYPE.BUS_TYPE_I2C)
            {
                optTmp = busOpSetting.GetOptionsByGuid(BusOptions.I2CPECMODE_GUID);
            }
            else if (busOpSetting.BusType == BUS_TYPE.BUS_TYPE_I2C2)
            {
                optTmp = busOpSetting.GetOptionsByGuid(BusOptions.I2C2PECMODE_GUID);
            }
            if (optTmp != null)
            {
                bool bPEC = false;
                bool.TryParse(optTmp.sphydata, out bPEC);
                if (!bPEC)
                    bErrCRC = false;
                bPECEnable = bPEC;
            }
            else
            {
                bPECEnable = false;
            }

            return bReturn;
        }

        public static UInt32 MakeNAssignATMUID(byte yRegIndex, byte yRW)
        {
            ObservableCollection<Parameter> pmcltTmp = new ObservableCollection<Parameter>();
            COBRA_HWMode_Reg[] hwregOut = null;

            //if (FindParameterCollectionByRegIndex(yRegIndex, ref pmcltTmp, ref hwregOut))
            FindParameterCollectionByRegIndex(yRegIndex, ref pmcltTmp, ref hwregOut);       //pretend everything fine
            return MakeNAssignATMUID(yRegIndex, yRW, ref hwregOut);
            //else
            //return UInt32.MaxValue;
        }

        private static UInt32 MakeNAssignATMUID(byte yRegIndex, byte yRW, ref COBRA_HWMode_Reg[] hwregOut)
        {
            //Random rndTmp = new Random();
            UInt32 dwTmp = 0;// = (UInt16)rndTmp.Next(0, 65536);
            //DateTime dtimeCurr = DateTime.Now;

            //dwTmp <<= 28;
            //dwTmp += (UInt32)(dtimeCurr.Millisecond);
            //dwTmp += (UInt32)(dtimeCurr.Second)<<10;
            //dwTmp += (UInt32)dtimeCurr.Minute << 16;
            //dwTmp += (UInt32)dtimeCurr.Hour << 22;
            //dwTmp += (UInt32)dtimeCurr.Day << 17;
            //dwTmp += (UInt32)dtimeCurr.Month << 22;
            //dwTmp += (UInt32)yRW << 26;
            //dwTmp += (UInt32)yRegIndex << 28;
            //dwTmp <<= 16;
            if (CommunicationLog.uCounting < UInt32.MaxValue)
            {
                CommunicationLog.uCounting += 1;
            }
            else
            {
                CommunicationLog.uCounting = 0;
            }
            dwTmp += CommunicationLog.uCounting;
            //(A170119)Francis, saving log to database
            if (DBManager.supportdb == true)
            {
                if(CommunicationDBLog.uCounting < UInt32.MaxValue)
                {
                    CommunicationDBLog.uCounting += 1;
                }
                else
                {
                    CommunicationDBLog.uCounting = 0;
                }
                dwTmp -= CommunicationLog.uCounting;        //TBD: to be compatible with CobraLog, if removing CobraLog and CommunicationLog, this line can be removed also.
                dwTmp += CommunicationDBLog.uCounting;
            }
            //(E170119)

            if (hwregOut != null)    //if null, find internal base on RegisterIndex
            {
                if (yRegIndex < hwregOut.Length)
                {
                    hwregOut[yRegIndex].cid = dwTmp;
                }
                else
                {
                    //dwTmp = UInt32.MaxValue;      //pretend everything fine
                }
            }
            else
            {
                //dwTmp = UInt32.MaxValue;  //pretend everything fine
            }

            return dwTmp;
        }

        public static void AnalyzeATMUID(UInt32 uIDIn, ref string strOut, ref byte yRWOut)
        {
            //int iMillieS, iSecond, iMinute, iHour;//, iDay, iMonth;
            //DateTime dtimeMake;

            //strOut = string.Empty;
            //yRWOut = 0; //default write command

            //iMillieS = (int)(uIDIn & 0x000003FF);
            //iSecond = (int)((uIDIn >> 10) & 0x0000003F);
            //iMinute = (int)((uIDIn >> 16) & 0x0000003F);
            //iHour = (int)((uIDIn >> 22) & 0x0000001F);
            //iDay = (int)((uIDIn >> 17) & 0x0000001F);
            //iMonth = (int)((uIDIn >> 22) & 0x0000000F);
            //yRWOut = (byte)((uIDIn >> 26) & 0x00000001);
            //dtimeMake = new DateTime(DateTime.Now.Year, iMonth, iDay, iHour, iMinute, iSecond, DateTime.Now.Millisecond);
            //dtimeMake = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, iHour, iMinute, iSecond, iMillieS);
            //strOut = dtimeMake.ToString("HH:mm:ss:fff");
            strOut = DateTime.Now.ToString("HH:mm:ss:ff");
            //strOut = strOut.Substring(5);   //subtract year from string
        }

        private static void MakeErrorOrNot(ref List<bool> listbOut, bool bForceFalse = false)
        {
            int iData = 0;
            bool bIsMinErr = bErrOutMin;    //copy UI setting value, need to generate Min,Max, and PEC
            bool bIsMaxErr = bErrOutMax;
            bool bNeedsPECErr = bErrPEC;
            bool bNeedCRCErr = bErrCRC;        //default is true, not all chip have CRC. So if chip has CRC, it will work; if not, no effects
            //Random rndTmp = new Random();
            int iSense = u16Sensor / 10;

            listbOut.Add(bIsMinErr);
            listbOut.Add(bIsMaxErr);
            listbOut.Add(bNeedsPECErr);     //let PEC error is the last, make it has double percentage to generate error
            //listbOut.Add(bNeedCRCErr);

            if (bForceFalse)
            {
                for (int i = 0; i < listbOut.Count; i++)
                {
                    listbOut[i] = false;
                }
                return;
            }
            if (bErrGenerate)
            {
                //iData = rndTmp.Next(0, 10000);  //0~9999 range
                //if (iData < (u16Sensor * 110))
                if ((wTotalRun != 0) && (wErrSummary != 0))
                {
                    float ftmp = (float)wErrSummary / (float)wTotalRun;
                    if (Math.Abs((ftmp * 100) - u16Sensor) > (u16Sensor * 0.3))
                    {
                        iSense *= 2;
                    }
                    else
                    {
                        iSense = u16Sensor;
                    }
                }
                iData = rndRandVal.Next(0, 10);
                if (iData < iSense)
                {
                    iData = iData % (listbOut.Count);
                    AutoMationTest.strATMErrDescrip = "ATM: generate data successfully.";
                    for (int i = 0; i < listbOut.Count; i++)
                    {
                        if ((iData == i) && (listbOut[i] == true))
                        {
                            listbOut[i] = true;
                            if (i == 0)
                            {
                                AutoMationTest.strATMErrDescrip = string.Format("ATM: generate Out of Minimum data");
                            }
                            else if (i == 1)
                            {
                                AutoMationTest.strATMErrDescrip = string.Format("ATM: generate Out of Max value");
                            }
                            else if (i == 2)
                            {
                                AutoMationTest.strATMErrDescrip = string.Format("ATM: generate PEC error data");
                            }
                            else if (i == 3)
                            {
                                bMakeFakeCRC = true;
                                //assign error string after make sure CRC error value is generated
                                //AutoMationTest.strATMErrDescrip = string.Format("ATM: generate CRC error data");
                            }
                        }
                        else
                        {
                            listbOut[i] = false;
                            //AutoMationTest.strATMErrDescrip = "ATM: generate data successfully.";
                        }
                    }
                    if (iData >= listbOut.Count)
                    {
                        listbOut[listbOut.Count - 1] = true;
                        //AutoMationTest.strATMErrDescrip = "ATM: generate PEC error data";
                    }
                }   //if(iData < u16Sensor)
                else
                {
                    for (int i = 0; i < listbOut.Count; i++)
                    {
                        listbOut[i] = false;
                    }
                    AutoMationTest.strATMErrDescrip = "ATM: generate data successfully.";
                }   //if(iData < u16Sensor)
            }   //if (bErrGenerate)

        }

        private static bool FindParameterCollectionByRegIndex(byte yRegIndex, ref ObservableCollection<Parameter> pmcltOut, ref COBRA_HWMode_Reg[] hwregOut)
        {
            bool bReturn = false; ;
            ObservableCollection<Parameter> pmcollectTmp = null;

            if ((pmcltOut != null))
            {
                if ((m_HwMode_RegList != null) && (m_HwMode_RegList.Count != 0) && (m_ParamListContainer != null) && (m_param_List.Count != 0))
                {
                    foreach (UInt32 key in m_HwMode_RegList.Keys)
                    {
                        pmcollectTmp = m_param_List[key];
                        if (pmcollectTmp.Count == 0) continue;

                        foreach (Parameter pmrTmp in pmcollectTmp)
                        {
                            if (pmrTmp == null) continue;
                            foreach (KeyValuePair<string, Reg> inreg in pmrTmp.reglist)
                            {
                                if (inreg.Value.address == yRegIndex)
                                {
                                    pmcltOut.Add(pmrTmp);
                                    hwregOut = m_HwMode_RegList[key];
                                    bReturn = true;
                                }
                            }
                        }
                    }
                }
            }

            return bReturn;
        }

        private static bool MakeDatabyParameter(byte yRegIndex, ObservableCollection<Parameter> pmcltIn, COBRA_HWMode_Reg[] hwregIn, ref ushort wDataOut, List<bool> bErrlist, bool bIsWord = false)
        {
            bool bReturn = false;
            ushort wFakeTemp = 0;
            Random rndTmp = new Random();
            bool bIsMinErr = bErrlist[0];
            bool bIsMaxErr = bErrlist[1];
            Reg Regtmplow = null;//new KeyValuePair<string,Reg>();
            UInt16 uPhyUpper = ushort.MaxValue;

            if ((pmcltIn.Count > 0) && (hwregIn != null))
            {
                wDataOut = 0;
                foreach (Parameter pmrTmp in pmcltIn)
                {
                    if (pmrTmp == null) continue;
                    foreach (KeyValuePair<string, Reg> inreg in pmrTmp.reglist)
                    {
                        if (inreg.Value.address == yRegIndex)
                        {
                            int wMinTmp = Convert.ToUInt16(pmrTmp.dbHexMin);
                            int wMaxTmp = Convert.ToUInt16(pmrTmp.dbHexMax);
                            //Do we need to check High/Low? TBD
                            if (inreg.Key.Equals("Low"))
                            {
                                //if((inreg.Value.bitsnumber+inreg.Value.startbit) > 8)
                                //{
                                //bWordOut = false;   //cannot use bitsnumber+startbit to indicate            //indicate it is word value
                                //}
                                //break;
                                UInt16 utmp = 1;
                                utmp <<= inreg.Value.bitsnumber;
                                utmp -= 1;
                                uPhyUpper = utmp;
                                if (wMaxTmp > utmp)
                                {
                                    wMaxTmp = utmp;
                                }
                            }
                            else if (inreg.Key.Equals("High"))
                            {
                                //bWordOut = false;
                                //break;
                                Regtmplow = pmrTmp.reglist["Low"];
                                if (Regtmplow == null)
                                {
                                    Regtmplow = new Reg();
                                    Regtmplow.address = 0;
                                    Regtmplow.bitsnumber = 0;
                                    Regtmplow.startbit = 0;
                                }
                                UInt16 utmp = 1;
                                utmp <<= Regtmplow.bitsnumber;
                                utmp -= 1;
                                if (wMinTmp > utmp)
                                    wMinTmp -= utmp;
                                if (wMaxTmp > utmp)
                                    wMaxTmp -= utmp;
                            }
                            wFakeTemp = 0;
                            for (int i = 0; i < inreg.Value.bitsnumber; i++)
                            {
                                wFakeTemp += (ushort)(1 << i);
                            }
                            if ((wMinTmp == ushort.MinValue) && (bIsMinErr))    //minval in xml is reached hex minimum
                            {
                                bIsMinErr = false;
                                if (AutoMationTest.strATMErrDescrip.IndexOf("@ ") == -1)
                                    AutoMationTest.strATMErrDescrip = "ATM: generate data successfully.";
                            }
                            if ((wMaxTmp == wFakeTemp) && (bIsMaxErr))           //maxval in xml is reached hex maximum
                            {
                                bIsMaxErr = false;
                                if (AutoMationTest.strATMErrDescrip.IndexOf("@ ") == -1)
                                    AutoMationTest.strATMErrDescrip = "ATM: generate data successfully.";
                            }
                            //for (int i = 0; i < inreg.Value.startbit; i++)
                            //{
                            //wMinTmp *= 2;
                            //wMaxTmp *= 2;
                            //}
                            wFakeTemp = (ushort)(rndTmp.Next(wMinTmp, wMaxTmp + 1));
                            if ((bIsMinErr) == true)
                            {
                                //wFakeTemp -= 1;
                                wFakeTemp = (ushort)(rndTmp.Next(ushort.MinValue, wMinTmp));
                                if (AutoMationTest.strATMErrDescrip.IndexOf("@ ") == -1)
                                    wErrOutMinCounter += 1;
                                AutoMationTest.strATMErrDescrip += string.Format("@ GUID=0x{0:X8} || ", pmrTmp.guid);
                            }
                            if ((bIsMaxErr) == true)
                            {
                                //wFakeTemp += 1;
                                wFakeTemp = (ushort)(rndTmp.Next(wMaxTmp + 1, (int)(uPhyUpper) + 1));
                                if (AutoMationTest.strATMErrDescrip.IndexOf("@ ") == -1)
                                    wErrOutMaxCounter += 1;
                                AutoMationTest.strATMErrDescrip += string.Format("@ GUID=0x{0:X8} || ", pmrTmp.guid);
                            }
                            wDataOut |= (ushort)(wFakeTemp << inreg.Value.startbit);
                            bReturn = true;
                            break;  //break foreach (KeyValuePair<string, Reg> inreg in pmrTmp.reglist)
                        }
                    }
                }
                //if(bIsMinErr)
                //wErrOutMinCounter += 1;
                //if(bIsMaxErr)
                //wErrOutMaxCounter += 1;
            }

            return bReturn;
        }

        private static bool GenerateI2CData(ref UInt32 uidOut, byte[] yDataToDev, ref byte[] yDataFromDev, ref UInt32 ErrOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 1)
        {
            bool bReturn = false;
            bool bIsWord = false;
            byte yTmpData = 0;
            byte yPECData = 0;
            ushort wTmpData = 0;
            List<bool> blistError = new List<bool>();
            int iCount = 0;
            ObservableCollection<Parameter> pmrcollectData = new ObservableCollection<Parameter>();
            COBRA_HWMode_Reg[] hwregReg = null;

            //more than 3 bytes data, considering it's block read
            if (((wDataInLength == 2) && (bPECEnable == false)) ||
                (wDataInLength == 3) && (bPECEnable == true))
            {
                bIsWord = true;
            }
            //pretend everything works fine
            //ErrOut = LibErrorCode.IDS_ERR_ATM_LOST_PARAMETER;
            ErrOut = LibErrorCode.IDS_ERR_SUCCESSFUL;
            bReturn = true;
            AutoMationTest.strATMErrDescrip = "ATM: generate data successfully.";
            for (iCount = 0; iCount < wDataInLength; iCount++)
            {
                yDataFromDev[iCount] = 0;
                if ((bPECEnable) && ((wDataInLength - iCount) == 1))      //have last one byte for I2C PEC value
                {
                    yPECData = CalculateReadPEC(yDataToDev[0], yDataToDev[1], yDataFromDev, iCount);
                    yDataFromDev[wDataInLength - 1] = yPECData;
                }
            }
            wDataOutLength = (ushort)iCount;
            //end pretending
            LibErrorCode.uVal01 = yDataToDev[1];
            uidOut = MakeNAssignATMUID(yDataToDev[1], 1);
            if (bIsCRCRegister)
            {
                if (bErrCRC)
                {
                    int itmp = rndRandVal.Next(0, 10);
                    int iSense = u16Sensor / 10;
                    if (itmp < iSense)
                    {
                        bMakeFakeCRC = true;
                    }
                    else
                    {
                        bMakeFakeCRC = false;
                    }
                }
                else
                {
                    bMakeFakeCRC = false;
                }
                bIsCRCRegister = false; //Add by Leon 20160223
                byte wValue = (byte)GetCRCReg(yDataToDev[1]);
                byte yTmp = 1;
                byte yReserved = 0x00;
                yTmp <<= regCRCInfor.bitsnumber;
                yTmp -= 1;
                yTmp <<= regCRCInfor.startbit;
                yReserved = (byte)(wValue & (~yTmp));
                wValue = (byte)(wValue & yTmp);
                if (bMakeFakeCRC)
                {
                    wValue = (byte)(~wValue);
                    AutoMationTest.strATMErrDescrip = string.Format("ATM: generate CRC error data");
                    wErrCRCCounter += 1;
                    wValue = (byte)(wValue & yTmp);
                }
                if (FindParameterCollectionByRegIndex(yDataToDev[1], ref pmrcollectData, ref hwregReg))
                {
                    if ((pmrcollectData.Count > 0) && (hwregReg != null))
                    {
                        MakeErrorOrNot(ref blistError, true);
                        //uidOut = MakeNAssignATMUID(yDataToDev[1], 1, ref hwregReg);
                        MakeDatabyParameter(yDataToDev[1], pmrcollectData, hwregReg, ref wTmpData, blistError, bIsWord);
                    }
                }
                yReserved = (byte)(wTmpData & (~yTmp));
                wValue += yReserved;
				//(A160624)Francis, as Leon feedback
				if (!bIsWord)
				{
					yDataFromDev[0] = wValue;
					//yDataFromDev[wDataInLength - 1] = wValue;
					yPECData = CalculateReadPEC(yDataToDev[0], yDataToDev[1], yDataFromDev, 1);
				}
				else
				{
					yDataFromDev[1] = wValue;
					yDataFromDev[0] = (byte)((wTmpData & 0xFF00) >> 8);
					yPECData = CalculateReadPEC(yDataToDev[0], yDataToDev[1], yDataFromDev, 2);
				}
				//(E160224)
                /* TBD: should we generate PEC error at same time? temporary no, only CRC error
                if (blistError[2])
                {
                    yPECData = (byte)(~yPECData);
                    AutoMationTest.strATMErrDescrip = string.Format("ATM: generate PEC error @ RegIndex = 0x{0:X2}", yDataToDev[1]);
                    wErrPECCounter += 1;
                }
                */
                yDataFromDev[wDataInLength - 1] = yPECData;
            }
            else
            {
                if (FindParameterCollectionByRegIndex(yDataToDev[1], ref pmrcollectData, ref hwregReg))
                {
                    if ((pmrcollectData.Count > 0) && (hwregReg != null))
                    {
                        MakeErrorOrNot(ref blistError);
                        //uidOut = MakeNAssignATMUID(yDataToDev[1], 1, ref hwregReg);
                        for (iCount = 0; iCount < wDataInLength; )
                        {
                            MakeDatabyParameter(yDataToDev[1], pmrcollectData, hwregReg, ref wTmpData, blistError, bIsWord);
                            if (bIsWord)
                            {
                                //MakeWordData(ref wTmpData, pmrcollectData, ilistMinVal, ilistMinVal);
                                yDataFromDev[iCount] = (byte)((wTmpData & 0xFF00) >> 8);
                                iCount += 1;
                                yDataFromDev[iCount] = (byte)(wTmpData & 0x00FF);
                                iCount += 1;
                            }
                            else
                            {
                                //MakeByteData(ref yTmpData, wMinVal, wMaxVal);
                                //wTmpData = yTmpData;
                                yDataFromDev[iCount] = (byte)(wTmpData & 0x00FF);
                                iCount += 1;
                            }
                            if ((bPECEnable) && ((wDataInLength - iCount) == 1))      //have last one byte for I2C PEC value
                            {
                                yPECData = CalculateReadPEC(yDataToDev[0], yDataToDev[1], yDataFromDev, iCount);
                                if (blistError[2])
                                {
                                    Random rndTmp = new Random();
                                    byte yTmp = (byte)rndTmp.Next(0, 256);
                                    while (yPECData == yTmp)
                                    {
                                        yTmp = (byte)rndTmp.Next(0, 256);
                                    }
                                    //yPECData = (byte)(~yPECData);
                                    AutoMationTest.strATMErrDescrip = string.Format("ATM: generate PEC error @ RegIndex = 0x{0:X2}", yDataToDev[1]);
                                    wErrPECCounter += 1;
                                    yPECData = yTmp;
                                }
                                yDataFromDev[wDataInLength - 1] = yPECData;
                                iCount += 1;
                            }

                        }
                        wDataOutLength = (ushort)iCount;
                        if (uidOut == UInt32.MaxValue)
                        {
                            //ErrOut = LibErrorCode.IDS_ERR_ATM_LOST_PARAMETER        //TBD error code
                        }
                        else
                        {
                            bReturn = true;
                            ErrOut = LibErrorCode.IDS_ERR_SUCCESSFUL;
                        }
                    }
                    else
                    {
                        //ErrOut = LibErrorCode.IDS_ERR_ATM_LOST_PARAMETER
                    }   //if ((pmrcollectData.Count > 0) && (hwregReg != null))
                }
                else
                {
                    //ErrOut = LibErrorCode.IDS_ERR_ATM_LOST_PARAMETER
                }   //if (FindParameterCollectionByRegIndex(yDataToDev[1], ref pmrcollectData, ref hwregReg))
            }   //if (bIsCRCRegister)

            if (bReturn)
                wTotalRun += 1;

            return bReturn;
        }

        private static bool GenerateSPIData(ref UInt32 uidOut, byte[] yDataToDev, ref byte[] yDataFromDev, ref UInt32 ErrOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 1)
        {
            bool bReturn = false;

            uidOut = UInt32.MaxValue;
            wDataOutLength = 0;

            return bReturn;
        }

        private static bool GenerateSVIDData(ref UInt32 uidOut, byte[] yDataToDev, ref byte[] yDataFromDev, ref UInt32 ErrOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 1)
        {
            bool bReturn = false;

            uidOut = UInt32.MaxValue;
            wDataOutLength = 0;

            return bReturn;
        }

        private static bool GenerateRS232Data(ref UInt32 uidOut, byte[] yDataToDev, ref byte[] yDataFromDev, ref UInt32 ErrOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 1)
        {
            bool bReturn = false;

            uidOut = UInt32.MaxValue;
            wDataOutLength = 0;

            return bReturn;
        }

        public static bool GetDatabyRegIndex(ref UInt32 uidOut, byte[] yDataToDev, ref byte[] yDataFromDev, ref UInt32 ErrOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 1)
        {
            bool bReturn = false;

            if ((busOpSetting.BusType == BUS_TYPE.BUS_TYPE_I2C) || (busOpSetting.BusType == BUS_TYPE.BUS_TYPE_I2C2))
            {
                bReturn = GenerateI2CData(ref uidOut, yDataToDev, ref yDataFromDev, ref ErrOut, ref wDataOutLength, wDataInLength);
            }
            else if (busOpSetting.BusType == BUS_TYPE.BUS_TYPE_SPI)
            {
                bReturn = GenerateSPIData(ref uidOut, yDataToDev, ref yDataFromDev, ref ErrOut, ref wDataOutLength, wDataInLength);
            }
            else if (busOpSetting.BusType == BUS_TYPE.BUS_TYPE_SVID)
            {
                bReturn = GenerateSVIDData(ref uidOut, yDataToDev, ref yDataFromDev, ref ErrOut, ref wDataOutLength, wDataInLength);
            }
            else if (busOpSetting.BusType == BUS_TYPE.BUS_TYPE_RS232)
            {
                bReturn = GenerateRS232Data(ref uidOut, yDataToDev, ref yDataFromDev, ref ErrOut, ref wDataOutLength, wDataInLength);
            }

            return bReturn;
        }

        public static bool WriteDatatoReg(ref UInt32 uidOut, byte[] yDataToDev, ref byte[] yDataFromDev, ref UInt32 ErrOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 1)
        {
            uidOut = MakeNAssignATMUID(yDataToDev[1], 0);
            AutoMationTest.strATMErrDescrip = string.Empty;
            return true;
        }

        public static bool CheckDataAccurate(byte[] yDataToDev, ref byte[] yDataFromDev, ref UInt32 ErrOut, ref UInt16 wDataOutLength, UInt16 wDataInLength = 1)
        {
            bool bReturn = true;

            return bReturn;
        }

        public static byte CalculatePECbyFormula(byte[] yDatArray, int iLength, byte yFormula = 7)
        {
            byte yCRCResult = 0;
            byte yCRCData;
            int i, j;

            for (i = 0; i < iLength; i++)
            {
                yCRCData = yDatArray[i];
                for (j = 0x80; j != 0; j >>= 1)
                {
                    if ((yCRCResult & 0x80) != 0)
                    {
                        yCRCResult <<= 1;
                        yCRCResult ^= 0x07;
                    }
                    else
                        yCRCResult <<= 1;

                    if ((yCRCData & j) != 0)
                        yCRCResult ^= 0x07;
                }
            }
            return yCRCResult;
        }

        public static byte CalculateReadPEC(byte ySlaveAddr, byte yRegIndex, byte[] yDataArr, int iLength)
        {
            byte[] pdata = new byte[iLength + 3];

            pdata[0] = ySlaveAddr;
            pdata[1] = yRegIndex;
            pdata[2] = (byte)(ySlaveAddr | 0x01);
            for (int i = 0; i < iLength; i++)
            {
                pdata[3 + i] = yDataArr[i];
            }

            return CalculatePECbyFormula(pdata, (iLength + 3));
        }

        public static byte CalculateWritePEC(byte ySlaveAddr, byte yRegIndex, byte[] yDataArr, int iLength)
        {
            byte[] pdata = new byte[iLength + 2];

            pdata[0] = ySlaveAddr;
            pdata[1] = yRegIndex;
            for (int i = 0; i < iLength; i++)
            {
                pdata[2 + i] = yDataArr[2 + i];
            }

            return CalculatePECbyFormula(yDataArr, iLength + 2);
        }

        public static void ResetAllErrorCounter(UInt16 wInCount = 0)
        {
            wErrOutMaxCounter = wInCount;
            wErrOutMinCounter = wInCount;
            wErrPECCounter = wInCount;
            wErrCRCCounter = wInCount;
            wTotalRun = wInCount;
            bMakeFakeCRC = false;
            bIsCRCRegister = false;

            HOMaxCtr = 0;
            HOMinCtr = 0;
            POMaxCtr = 0;
            POMinCtr = 0;
            PECCtr = 0;
            CRCCtr = 0;

            uint i = wErrSummary;
            regCRCInfor = new Reg();
            //regCRCInfor.address = 0x0C;     //initial value, will be assigned by DEM
            //regCRCInfor.startbit = 0x04;    //initial value, will be assigned by DEM
            //regCRCInfor.bitsnumber = 0x04;  //initial value, will be assigned by DEM
        }

        public static byte GetCRCReg(byte addr)
        {
            COBRA_HWMode_Reg[] hwReglist;

            foreach (UInt32 key in m_HwMode_RegList.Keys)
            {
                hwReglist = m_HwMode_RegList[key];
                if (hwReglist.Length == 0) continue;
                return (byte)hwReglist[addr].val;
            }
            return 0;
        }

        #endregion

    }
}
