using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.ComponentModel;
using Cobra.Common;
using Cobra.EM;

namespace Cobra.TrimPanel
{
    public class SFLViewMode
    {
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

        private AsyncObservableCollection<SFLModel> m_SFL_ParameterList = new AsyncObservableCollection<SFLModel>();
        public AsyncObservableCollection<SFLModel> sfl_parameterlist
        {
            get { return m_SFL_ParameterList; }
            set { m_SFL_ParameterList = value; }
        }

        private ParamContainer m_DM_ParameterList = new ParamContainer();
        public ParamContainer dm_parameterlist
        {
            get { return m_DM_ParameterList; }
            set { m_DM_ParameterList = value; }
        }

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
                InitSFLParameter(param);
            }
        }

        #region 参数操作
        private void InitSFLParameter(Parameter param)
        {
            UInt16 udata = 0;
            UInt32 wdata = 0;
            Double ddata = 0.0;
            SFLModel model = new SFLModel();

            model.parent = param.sfllist[sflname].parent;
            model.guid = param.guid;

            foreach (DictionaryEntry de in param.sfllist[sflname].nodetable)
            {
                switch (de.Key.ToString())
                {
                    case "NickName":
                        model.nickname = de.Value.ToString();
                        break;
                    case "Order":
                        {
                            if (String.IsNullOrEmpty(de.Value.ToString()))
                                model.order = 0;
                            else
                                model.order = Convert.ToUInt16(de.Value.ToString(), 10);
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
                    case "Description":
                        model.description = de.Value.ToString();
                        break;
                    case "DefValue":
                        {
                            if (!Double.TryParse(de.Value.ToString(), out ddata))
                                model.data = 0.0;
                            else
                                model.data = Convert.ToDouble(de.Value.ToString());
                            break;
                        }
                    case "Slope":
                        {
                            wdata = Convert.ToUInt32(de.Value.ToString(), 16);
                            model.slope_relation = GetParameterByGuid(wdata);
                            break;
                        }
                    case "Offset":
                        {
                            wdata = Convert.ToUInt32(de.Value.ToString(), 16);
                            model.offset_relation = GetParameterByGuid(wdata);
                            break;
                        }
                    case "SubType":
                        {
                            if (!UInt16.TryParse(de.Value.ToString(), out udata))
                                model.subType = 0;
                            else
                                model.subType = udata;
                            break;
                        }
                    case "RetryTime":
                        {
                            if (!UInt16.TryParse(de.Value.ToString(), out udata))
                                model.retry_time = 1;
                            else
                                model.retry_time = udata;
                            break;
                        }
                    default:
                        break;
                }
            }
            if (model.subType == 0)
                sfl_parameterlist.Add(model);
        }

        public Parameter GetParameterByGuid(UInt32 guid)
        {
            return dm_parameterlist.GetParameterByGuid(guid);
        }

        public SFLModel GetParameterByName(string name)
        {
            foreach (SFLModel param in sfl_parameterlist)
            {
                if (param.nickname.Equals(name))
                    return param;
            }
            return null;
        }
        #endregion
    }
}
