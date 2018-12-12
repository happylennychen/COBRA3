using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.ComponentModel;
using System.Collections.ObjectModel;
using O2Micro.Cobra.Common;
using O2Micro.Cobra.EM;

namespace O2Micro.Cobra.SBS4Panel
{
    public class SFLViewMode
    {
        #region Element分类标签
        public UInt32 SectionElementFlag = 0xFFFF0000;
        public UInt32 TempSettingsElement = 0x00010000;
        public UInt32 OperationElement = 0x00030000;
        public UInt32 BatterySBSElement = 0x00060000;
        public UInt32 MCUElement = 0x00090000;
        #endregion

        //父对象保存
        private MainControl m_control_parent;
        public MainControl control_parent
        {
            get { return m_control_parent; }
            set { m_control_parent = value; }
        }

        private Device m_device_parent;
        public Device device_parent
        {
            get { return m_device_parent; }
            set { m_device_parent = value; }
        }

        private string m_SFLname;
        public string sflname
        {
            get { return m_SFLname; }
            set { m_SFLname = value; }
        }

        private ParamContainer m_DM_ParameterList = new ParamContainer(); //SBS全部参数
        public ParamContainer dm_parameterlist
        {
            get { return m_DM_ParameterList; }
            set { m_DM_ParameterList = value; }
        }

        private ParamContainer m_DM_WO_ParameterList = new ParamContainer(); //SBS全部参数
        public ParamContainer dm_wo_parameterlist
        {
            get { return m_DM_WO_ParameterList; }
            set { m_DM_WO_ParameterList = value; }
        }

        #region 进入GG参数列表
        private AsyncObservableCollection<Parameter> m_GGPccs_ParameterList = new AsyncObservableCollection<Parameter>();
        public AsyncObservableCollection<Parameter> ggpccs_parameterlist
        {
            get { return m_GGPccs_ParameterList; }
            set { m_GGPccs_ParameterList = value; }
        }

        private AsyncObservableCollection<Parameter> m_GGPcsr_ParameterList = new AsyncObservableCollection<Parameter>();
        public AsyncObservableCollection<Parameter> ggpcsr_parameterlist
        {
            get { return m_GGPcsr_ParameterList; }
            set { m_GGPcsr_ParameterList = value; }
        }

        private AsyncObservableCollection<Parameter> m_GGPsbsr_ParameterList = new AsyncObservableCollection<Parameter>();
        public AsyncObservableCollection<Parameter> ggpsbsr_parameterlist
        {
            get { return m_GGPsbsr_ParameterList; }
            set { m_GGPsbsr_ParameterList = value; }
        }
        #endregion

        #region SFL参数列表

        private string m_folder_path = "";
        public string folder_path
        {
            get { return m_folder_path; }
            set { m_folder_path = value; }
        }

        private ObservableCollection<PathModel> m_Path_Parameterlist = new ObservableCollection<PathModel>();
        public ObservableCollection<PathModel> path_parameterlist
        {
            get { return m_Path_Parameterlist; }
            set { m_Path_Parameterlist = value; }
        }

        private ObservableCollection<ParamModel> m_Param_Parameterlist = new ObservableCollection<ParamModel>();
        public ObservableCollection<ParamModel> param_parameterlist
        {
            get { return m_Param_Parameterlist; }
            set { m_Param_Parameterlist = value; }
        }

        private ObservableCollection<SFLModel> m_SFL_ParameterList = new ObservableCollection<SFLModel>();
        public ObservableCollection<SFLModel> sfl_parameterlist
        {
            get { return m_SFL_ParameterList; }
            set { m_SFL_ParameterList = value; }
        }

        public ObservableCollection<SFLModel> sfl_batteryInfor_parameterlist = new ObservableCollection<SFLModel>();
        public AsyncObservableCollection<SFLModel> sfl_tempLeftInfor_parameterlist = new AsyncObservableCollection<SFLModel>();
        public AsyncObservableCollection<SFLModel> sfl_tempRightInfor_parameterlist = new AsyncObservableCollection<SFLModel>();
        public ObservableCollection<SFLModel> sfl_wave_parameterlist = new ObservableCollection<SFLModel>();
        public ObservableCollection<SFLModel> sfl_status_parameterlist = new ObservableCollection<SFLModel>();

        public SFLViewMode(object pParent, object parent)
        {
            #region 相关初始化
            device_parent = (Device)pParent;
            if (device_parent == null) return;

            control_parent = (MainControl)parent;
            if (control_parent == null) return;

            sflname = control_parent.sflname;
            if (String.IsNullOrEmpty(sflname)) return;
            #endregion

            dm_parameterlist = device_parent.GetParamLists(sflname);
            foreach (Parameter param in dm_parameterlist.parameterlist)
            {
                if (param == null) continue;
                if (((param.guid & SectionElementFlag) == OperationElement) || ((param.guid & SectionElementFlag) == MCUElement) ||
                    ((param.guid & SectionElementFlag) == TempSettingsElement))     //(M151002)Francis, add TempSetting flag to parse 
                    ParseGGElement(param);
                else if ((param.guid & SectionElementFlag) == BatterySBSElement)
                    ParseSBSElement(param);
            }
            /*Parameter curr = new Parameter();
            foreach( Parameter p in ggpccs_parameterlist)
            {
                if (p.guid == 0x00031282)
                {
                    curr = p;
                    break;
                }
            }
            int i = ggpccs_parameterlist.IndexOf(curr);
            ggpccs_parameterlist.Move(i, 0);*/
        }

        #region 参数操作
        private void ParseGGElement(Parameter param)
        {
            byte bdata = 0;
            UInt16 udata = 0;
            SFLModel model = new SFLModel();

            model.parent = param.sfllist[sflname].parent;
            model.guid = param.guid;
            model.data = 0.0;
            model.itemlist = param.itemlist;

            foreach (DictionaryEntry de in param.sfllist[sflname].nodetable)
            {
                switch (de.Key.ToString())
                {
                    case "NickName":
                        model.nickname = de.Value.ToString();
                        break;
                    case "Type":
                        {
                            if (!UInt16.TryParse(de.Value.ToString(), out udata))
                                model.type = 0;
                            else
                            {
                                udata = Convert.ToUInt16(de.Value.ToString(), 16);
                                model.type = udata;
                            }
							/*	//there is no subsection = 1 in BlueWhale, also consider that it is better SFL should not take subsection in action, to be more
							 * //flexible if DeviceDescription is modified, just private section in xml to check parameter
                            if (param.subsection == 2)
                            {
                                //(M140515)Francis,
                                param.key = (Double)Convert.ToUInt16(de.Value.ToString(), 16);
                                if ((model.type & (UInt16)(GGType.OZGGPollMask)) != 0)
                                {
                                    ggpccs_parameterlist.Add(param);
                                }
                                if ((model.type & (UInt16)(GGType.OZGGRegMask)) != 0)
                                {
                                    ggpcsr_parameterlist.Add(param);
                                }
                                continue;
                            }
                            else if((param.subsection == 3) || (param.subsection == 1))
                            {
                                for (int i = 0; i < 3; i++)
                                {
                                    bdata = (byte)(model.type & (UInt16)(1 << i));
                                    switch (bdata)
                                    {
                                        case 1:
                                            //ggpccs_parameterlist.Add(param);
                                            //continue;
                                        case 2:
                                            ggpcsr_parameterlist.Add(param);
                                            continue;
                                        case 4:
                                            //sfl_charger_parameterlist.Add(model);
                                            //charger_parameterlist.parameterlist.Add(param);
                                            break;
                                    }
                                }
							}
							* */
                            param.key = (Double)Convert.ToUInt16(de.Value.ToString(), 16);
                            if ((model.type & (UInt16)(GGType.OZGGPollMask)) != 0)
                            {
                                ggpccs_parameterlist.Add(param);
                            }
                            if ((model.type & (UInt16)(GGType.OZGGRegMask)) != 0)
                            {
                                ggpcsr_parameterlist.Add(param);
                            }

                            break;
                        }
                    case "Format":
                        {
                            //(C151002)Francis, use format tag to save SettingFlag for GG using, SettingFlag is defined in Project.cs
                            //There will must be DesignCapacity = 0x0201, ChargeCV = 0x0205, DishcargeEndVoltage = 0x0207, Rbat = 0x209, and Rcon = 0x20a
                            //And ChargeEndCurrent = 0x0206 is optional. 
                            //GG DLL goes to check those setting existing or not. If ChargeEndCurrent is not existed, find it in TASKMessage.sm.misc[0]
                            //Besides ChargeEndCurrent parameter, others must be defined in DeviceDescriptor.xml
							if (!UInt16.TryParse(de.Value.ToString(), System.Globalization.NumberStyles.HexNumber, null, out udata))
								model.format = 0;
							else
							{
								model.format = Convert.ToUInt16(de.Value.ToString(), 16);
							}
                            break;
                        }
                    default:
                        break;
                }
            }
            sfl_parameterlist.Add(model);
        }

        private void ParseSBSElement(Parameter param)
        {
            byte bdata = 0;
            UInt16 udata = 0;
            bool bldata = false;
            SFLModel model = new SFLModel();

            ggpsbsr_parameterlist.Add(param);

            model.parent = param.sfllist[sflname].parent;
            model.guid = param.guid;
            model.data = 0.0;

            foreach (DictionaryEntry de in param.sfllist[sflname].nodetable)
            {
                switch (de.Key.ToString())
                {
                    case "Name":
                        model.nickname = de.Value.ToString();
                        break;
                    case "Description":
                        model.Description = de.Value.ToString();
                        break;
                    case "Type":
                        {
                            if (!UInt16.TryParse(de.Value.ToString(), out udata))
                                model.type = 0;
                            else
                                model.type = udata;

                            for (int i = 0; i < 5; i++)
                            {
                                bdata = (byte)(model.type & (UInt16)(1 << i));
                                switch (bdata)
                                {
                                    case 1: //电池信息组
                                        sfl_batteryInfor_parameterlist.Add(model);
                                        break;
                                    case 2: //瞬时信息组左
                                        sfl_tempLeftInfor_parameterlist.Add(model);
                                        break;
                                    case 4://瞬时信息组右
                                        sfl_tempRightInfor_parameterlist.Add(model);
                                        break;
                                    case 8: //状态位组
                                        sfl_status_parameterlist.Add(model);
                                        break;
                                    case 16: //波形组
                                        sfl_wave_parameterlist.Add(model);
                                        break;
                                }
                            }
                            break;
                        }
                    case "Clickable":
                        {
                            if (!Boolean.TryParse(de.Value.ToString(), out bldata))
                                model.bClickable = false;
                            else
                                model.bClickable = true;
                            break;
                        }
                    case "Order":
                        {
                            if (!UInt16.TryParse(de.Value.ToString(), out udata))
                                model.order = 0;
                            else
                                model.order = udata;
                            break;
                        }
                    case "Format":
                        {
                            if (!UInt16.TryParse(de.Value.ToString(), out udata))
                                model.format = 0;
                            else
                                model.format = udata;
                            break;
                        }
                    case "Mode":
                        {
                            if (!UInt16.TryParse(de.Value.ToString(), out udata))
                                model.mode = 0;
                            else
                                model.mode = udata;
                            break;
                        }
                    default:
                        break;
                }
            } 
            param.PropertyChanged += new PropertyChangedEventHandler(Parameter_PropertyChanged);
            sfl_parameterlist.Add(model);
        }
        #endregion

        #region 行为
        public UInt32 WriteDevice(string guid)
        {
            UInt32 uid = 0;
            dm_wo_parameterlist.parameterlist.Clear();
            if (!UInt32.TryParse(guid, out uid)) return LibErrorCode.IDS_ERR_COM_INVALID_PARAMETER;

            SFLModel param = GetParameterByGuid(uid);
            if (param == null) return LibErrorCode.IDS_ERR_COM_INVALID_PARAMETER;

            switch (param.mode)
            {
                case 0: //button
                    param.parent.phydata = (param.data == 0) ?1:0;
                    break;
                case 1: //radionbutton
                    param.parent.phydata = param.data;
                    break;
            }
            dm_wo_parameterlist.parameterlist.Add(param.parent);
            return LibErrorCode.IDS_ERR_SUCCESSFUL;
        }

        public SFLModel GetParameterByGuid(UInt32 guid)
        {
            foreach (SFLModel param in sfl_parameterlist)
            {
                if (param.guid.Equals(guid))
                    return param;
            }
            return null;
        }

        public SFLModel GetParameterByFormat(UInt16 fm)
        {
            foreach (SFLModel param in sfl_parameterlist)
            {
                if (param.format.Equals(fm))
                    return param;
            }
            return null;
        }

        public void Parameter_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            string tmp = "";
            Parameter p = (Parameter)sender;
            SFLModel model = GetParameterByGuid(p.guid);
            if (model == null) return;
            switch (e.PropertyName.ToString())
            {
                case "phydata":
                    {
                        model.data = p.phydata;
                        model.errorcode = p.errorcode;

                        switch (model.format)
                        {
                            case 0: //Int
                                tmp = String.Format("{0:D}", (Int32)model.data);
                                break;
                            case 1: //float1
                                tmp = String.Format("{0:F1}", model.data);
                                break;
                            case 2: //float2
                                tmp = String.Format("{0:F2}", model.data);
                                break;
                            case 3: //float3
                                tmp = String.Format("{0:F3}", model.data);
                                break;
                            case 4: //float4
                                tmp = String.Format("{0:F4}", model.data);
                                break;
                            case 5: //Hex
                                tmp = String.Format("0x{0:X2}", (byte)model.data);
                                break;
                            case 6: //Word
                                tmp = String.Format("0x{0:X4}", (UInt16)model.data);
                                break;
                            case 7:
                                int hour,min,sec;
                                sec = (int)model.data;
                                hour= sec/3600;     //计算时 3600进制	
                                min=(sec%3600)/60;   //计算分  60进制	
                                sec=(sec%3600)%60;   //计算秒  余下的全为秒数
                                tmp = String.Format("{0:D2}:{1:D2}:{2:D2}", hour,min,sec);
                                break;
                            case 8:
                                tmp = String.Format("{0}%", model.data);
                                break;
                            default:
                                tmp = String.Format("{0}", model.data);
                                break;
                        }
                        model.sdata = tmp;
                        break;
                    }
                default:
                    break;
            }
        }
        #endregion
        #endregion

        //(A150717)Francis, read/write function
        #region Read/Write function
        
        public bool ReadOpRegFromDevice(ref TASKMessage tskMsg, AsyncObservableCollection<Parameter> targetPmList)
        {
            bool bReturn = false;
            ParamContainer pmcnt = new ParamContainer();

            foreach(Parameter pmr in targetPmList)
            {
                pmcnt.parameterlist.Add(pmr);
            }
            tskMsg.gm.sflname = sflname;
            tskMsg.task_parameterlist = pmcnt;
            tskMsg.task = TM.TM_READ;
			device_parent.AccessDevice(ref tskMsg);
			while (tskMsg.bgworker.IsBusy)
				System.Windows.Forms.Application.DoEvents();
			System.Windows.Forms.Application.DoEvents();
            if (tskMsg.errorcode == LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                tskMsg.task = TM.TM_CONVERT_HEXTOPHYSICAL;
                device_parent.AccessDevice(ref tskMsg);
                while (tskMsg.bgworker.IsBusy)
                    System.Windows.Forms.Application.DoEvents();
                System.Windows.Forms.Application.DoEvents();
                if(tskMsg.errorcode == LibErrorCode.IDS_ERR_SUCCESSFUL)
                {
                    bReturn = true;
                }
            }

            return bReturn;
        }

        public bool WriteOpRegtoDevice(ref TASKMessage tskMsg, AsyncObservableCollection<Parameter> targetPmList)
        {
            bool bReturn = false;
            ParamContainer pmcnt = new ParamContainer();

            foreach (Parameter pmr in targetPmList)
            {
                pmcnt.parameterlist.Add(pmr);
            }
            tskMsg.gm.sflname = sflname;
            tskMsg.task_parameterlist = pmcnt;
            tskMsg.task = TM.TM_CONVERT_PHYSICALTOHEX;
            device_parent.AccessDevice(ref tskMsg);
            while (tskMsg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            System.Windows.Forms.Application.DoEvents();
            if (tskMsg.errorcode == LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                tskMsg.task = TM.TM_WRITE;
                device_parent.AccessDevice(ref tskMsg);
                while (tskMsg.bgworker.IsBusy)
                    System.Windows.Forms.Application.DoEvents();
                System.Windows.Forms.Application.DoEvents();
                if (tskMsg.errorcode == LibErrorCode.IDS_ERR_SUCCESSFUL)
                {
                    bReturn = true;
                }
            }

            return bReturn;
        }

        #endregion
        //(E150717)
    }
}