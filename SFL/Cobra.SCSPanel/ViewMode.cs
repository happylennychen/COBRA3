using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.ComponentModel;
using System.Collections.ObjectModel;
using Cobra.Common;
using Cobra.EM;

namespace Cobra.SCSPanel
{
    public class ViewMode
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

        #region SFL参数包括整体参数，log参数，scan参数列表
        private AsyncObservableCollection<Model> m_SFL_ParameterList = new AsyncObservableCollection<Model>();
        public AsyncObservableCollection<Model> sfl_parameterlist
        {
            get { return m_SFL_ParameterList; }
            set { m_SFL_ParameterList = value; }
        }

        private ParamContainer m_Scan_ParameterList = new ParamContainer();
        public ParamContainer scan_parameterlist
        {
            get { return m_Scan_ParameterList; }
            set { m_Scan_ParameterList = value; }
        }
        #endregion

        private ParamContainer m_DM_ParameterList = new ParamContainer();
        public ParamContainer dm_parameterlist
        {
            get { return m_DM_ParameterList; }
            set { m_DM_ParameterList = value; }
        }

        public ViewMode(object pParent, object parent)
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
            Model model = new Model();
            model.parent = param.sfllist[sflname].parent;
            model.guid = param.guid;
            model.sphydata = string.Empty;
            model.data = model.parent.phydata;
            model.nickname = GetHashTableValueByKey("NickName", param.sfllist[sflname].nodetable);

            foreach (DictionaryEntry de in param.sfllist[sflname].nodetable)
            {
                switch (de.Key.ToString())
                {
                    case "LogName":
                        model.nickname = de.Value.ToString();
                        break;
                    case "Order":
                        model.index = Convert.ToUInt16(de.Value.ToString(), 16);
                        break;
                    default:
                        break;
                }
            }
            sfl_parameterlist.Add(model);
        }

        public Model GetParameterByGuid(UInt32 guid)
        {
            foreach (Model param in sfl_parameterlist)
            {
                if (param.guid.Equals(guid))
                    return param;
            }
            return null;
        }

        private string GetHashTableValueByKey(string str, Hashtable htable)
        {
            if (htable.ContainsKey(str))
                return htable[str].ToString();
            else
                return "NoSuchKey";
        }
        #endregion
    }
}
