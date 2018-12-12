using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.ComponentModel;
using System.Collections.ObjectModel;
using O2Micro.Cobra.Common;
using O2Micro.Cobra.EM;

namespace O2Micro.Cobra.SBS2Panel
{
    public class SFLViewMode
    {
        #region Element分类标签
        public UInt32 SectionElementFlag = 0xFFFF0000;
        public UInt32 OperationElement = 0x00030000;
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

        #region SFL参数列表
        private ObservableCollection<SFLModel> m_SFL_ParameterList = new ObservableCollection<SFLModel>();
        public ObservableCollection<SFLModel> sfl_parameterlist
        {
            get { return m_SFL_ParameterList; }
            set { m_SFL_ParameterList = value; }
        }

        public ObservableCollection<SFLModel> sfl_batteryInfor_parameterlist = new ObservableCollection<SFLModel>();
        public AsyncObservableCollection<SFLModel> sfl_tempLeftInfor_parameterlist = new AsyncObservableCollection<SFLModel>();
        public AsyncObservableCollection<SFLModel> sfl_tempMidInfor_parameterlist = new AsyncObservableCollection<SFLModel>();
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
                ParseSBSElement(param);
            }
        }

        #region 参数操作
        private void ParseSBSElement(Parameter param)
        {
            byte bdata = 0;
            UInt16 udata = 0;
            bool bldata = false;
            SFLModel model = new SFLModel();
            
            model.parent = param.sfllist[sflname].parent;
            model.itemlist = param.itemlist;
            model.guid = param.guid;
            model.data = 0.0;

            foreach (DictionaryEntry de in param.sfllist[sflname].nodetable)
            {
                switch (de.Key.ToString())
                {
                    case "Name":
                        model.nickname = de.Value.ToString();
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
                                    case 1: //瞬时信息组左
                                        sfl_tempLeftInfor_parameterlist.Add(model);
                                        break;
                                    case 2: //瞬时信息组中
                                        sfl_tempMidInfor_parameterlist.Add(model);
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
            if (p.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL) return;

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
    }
}