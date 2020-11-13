using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.ComponentModel;
using System.Collections.ObjectModel;
using Cobra.Common;
using Cobra.EM;

namespace Cobra.SBSPanel
{
    public class SFLViewMode
    {
        #region Element分类标签
        public UInt32 SectionElementFlag = 0xFFFF0000;
        public UInt32 OperationElement = 0x00030000;
        public UInt32 BatterySBSElement = 0x00060000;
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

        #region OZ8806
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
        #endregion

        #region SeaElf
        public ParamContainer m_Charger_Parameterlist = new ParamContainer();
        public ParamContainer charger_parameterlist
        {
            get { return m_Charger_Parameterlist; }
            set { m_Charger_Parameterlist = value; }
        }

        public ObservableCollection<SFLModel> sfl_charger_parameterlist = new ObservableCollection<SFLModel>();
        #endregion
        #endregion

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
                if ((param.guid & SectionElementFlag) == OperationElement)
                    ParseOpElement(param);
                else if ((param.guid & SectionElementFlag) == BatterySBSElement)
                    ParseSBSElement(param);
            }
        }

        #region 参数操作
        private void ParseOpElement(Parameter param)
        {
            byte   bdata = 0;
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

                            if (param.subsection == 0) //OZ8806
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
                            else //SeaElf
                            {
                                for (int i = 0; i < 3; i++)
                                {
                                    bdata = (byte)(model.type & (UInt16)(1 << i));
                                    switch (bdata)
                                    {
                                        case 1:
                                            //ggpccs_parameterlist.Add(param);
                                            continue;
                                        case 2:
                                            ggpcsr_parameterlist.Add(param);
                                            continue;
                                        case 4:
                                            sfl_charger_parameterlist.Add(model);
                                            charger_parameterlist.parameterlist.Add(param);
                                            break;
                                    }
                                }
                            }
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
                    default:
                        break;
                }
            }
            param.PropertyChanged += new PropertyChangedEventHandler(Parameter_PropertyChanged);
            sfl_parameterlist.Add(model);
        }

        private void ParseSBSElement(Parameter param)
        {
            byte bdata = 0;
            UInt16 udata = 0;
            SFLModel model = new SFLModel();

            ggpsbsr_parameterlist.Add(param);

            model.parent = param.sfllist[sflname].parent;
            model.guid = param.guid;
            model.data = 0.0;

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
                                model.type = udata;

                            for (int i = 0; i < 4; i++)
                            {
                                bdata = (byte)(model.type & (UInt16)(1 << i));
                                switch (bdata)
                                {
                                    case 0: //不存在于SBS
                                        continue;
                                    case 1: //电池信息组
                                        sfl_batteryInfor_parameterlist.Add(model);
                                        break;
                                    case 2: //瞬时信息组右
                                        sfl_tempLeftInfor_parameterlist.Add(model);
                                        break;
                                    case 4://瞬时信息组左
                                        sfl_tempRightInfor_parameterlist.Add(model);
                                        break;
                                    case 8: //波形组
                                        sfl_wave_parameterlist.Add(model);
                                        break;
                                }
                            }
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
                    default:
                        break;
                }
            }
            sfl_parameterlist.Add(model);
        }

        public void Parameter_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Parameter p = (Parameter)sender;
            SFLModel model = GetParameterByGuid(p.guid);
            if (model == null) return;

            switch (e.PropertyName.ToString())
            {
                case "phydata":
                    {
                        model.data = p.phydata;
                        model.errorcode = p.errorcode;
                        break;
                    }
                default:
                    break;
            }
        }

        public SFLModel GetParameterByGuid(UInt32 guid)
        {
            foreach (SFLModel param in sfl_charger_parameterlist)
            {
                if (param.guid.Equals(guid))
                    return param;
            }
            return null;
        }

        public SFLModel GetBatteryInforParameterByGuid(UInt32 guid)
        {
            foreach (SFLModel param in sfl_batteryInfor_parameterlist)
            {
                if (param.guid.Equals(guid))
                    return param;
            }
            return null;
        }
        #endregion
    }
}
