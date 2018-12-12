using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using Microsoft.Win32;
using System.Linq;
using System.Xml.Linq;
using System.IO;
using System.Reflection;
using System.ComponentModel;
using System.Globalization;
using System.Xml;
using System.Threading;
//using System.Windows.Forms;
using System.Diagnostics;
using O2Micro.Cobra.EM;
using O2Micro.Cobra.Common;
using System.Security.Cryptography;
using O2Micro.Cobra.ControlLibrary;
using System.Collections;

namespace O2Micro.Cobra.ProductionPanel
{
    public class SFLViewModel
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

        private AsyncObservableCollection<SFLParameterModel> m_SFL_ParameterList = new AsyncObservableCollection<SFLParameterModel>();
        public AsyncObservableCollection<SFLParameterModel> sfl_parameterlist
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

        private UInt16 order = 0;

        public SFLViewModel(object pParent, object parent, string name)
        {
            #region 相关初始化
            device_parent = (Device)pParent;
            if (device_parent == null) return;

            control_parent = (MainControl)parent;
            if (control_parent == null) return;

            //sflname = control_parent.sflname;
            sflname = name;
            if (String.IsNullOrEmpty(sflname)) return;
            #endregion

            dm_parameterlist = device_parent.GetParamLists(sflname);
            if (dm_parameterlist == null)   //KALLV don't have a board config section
                return;
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
            Double ddata = 0.0;
            bool bdata = false;
            SFLParameterModel model = new SFLParameterModel();

            model.parent = param.sfllist[sflname].parent;
            model.guid = param.guid;
            model.bedit = true;
            model.berror = false;
            model.itemlist = param.itemlist;
            model.brange = true;
            model.brone = true;
            model.bwone = true;
            model.bsubmenu = true;

            foreach (DictionaryEntry de in param.sfllist[sflname].nodetable)
            {
                switch (de.Key.ToString())
                {
                    case "NickName":
                        model.nickname = de.Value.ToString();
                        break;
                    case "Order":
                        {
                            if (control_parent.border)
                            {
                                if (String.IsNullOrEmpty(de.Value.ToString()))
                                    model.order = 0;
                                else
                                    model.order = Convert.ToUInt16(de.Value.ToString(), 16);
                            }
                            else
                            {
                                model.order = order;
                                order++;
                            }
                            break;
                        }
                    case "EditType":
                        {
                            if (!UInt16.TryParse(de.Value.ToString(), out udata))
                                model.editortype = 0;
                            else
                                model.editortype = udata;
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
                    case "MinValue":
                        {
                            if (!Double.TryParse(de.Value.ToString(), out ddata))
                                model.minvalue = 0.0;
                            else
                                model.minvalue = Convert.ToDouble(de.Value.ToString());
                            break;
                        }
                    case "MaxValue":
                        {
                            if (!Double.TryParse(de.Value.ToString(), out ddata))
                                model.maxvalue = 0.0;
                            else
                            {
                                double d = Convert.ToDouble(de.Value.ToString());
                                model.maxvalue = d;
                            }
                            break;
                        }
                    case "EventMode":
                        {
                            if (!UInt16.TryParse(de.Value.ToString(), out udata))
                                model.eventmode = 0;
                            else
                                model.eventmode = udata;
                            break;
                        }
                    case "Catalog":
                        model.catalog = de.Value.ToString();
                        break;
                    case "DefValue":
                        {
                            if (!Double.TryParse(de.Value.ToString(), out ddata))
                                model.data = 0.0;
                            else
                                model.data = Convert.ToDouble(de.Value.ToString());
                            break;
                        }
                    case "Relations":
                        {
                            AsyncObservableCollection<string> list = (AsyncObservableCollection<string>)de.Value;
                            foreach (string tmp in list)
                            {
                                if (String.IsNullOrEmpty(tmp)) continue;
                                model.relations.Add(Convert.ToUInt32(tmp, 16));
                            }
                            break;
                        }
                    case "BRange":
                        {
                            if (!Boolean.TryParse(de.Value.ToString(), out bdata))
                                model.brange = true;
                            else
                                model.brange = bdata;
                            break;
                        }
                    case "BROne":
                        {
                            if (!Boolean.TryParse(de.Value.ToString(), out bdata))
                                model.brone = true;
                            else
                                model.brone = bdata;
                            break;
                        }
                    case "BWOne":
                        {
                            if (!Boolean.TryParse(de.Value.ToString(), out bdata))
                                model.bwone = true;
                            else
                                model.bwone = bdata;
                            break;
                        }
                    default:
                        break;
                }
            }
            model.bsubmenu = (model.brone | model.bwone);
            if ((model.data > model.maxvalue) || (model.data < model.minvalue))
                model.berror = true;
            else
                model.berror = false;

            sfl_parameterlist.Add(model);
        }
        
        public SFLParameterModel GetParameterByGuid(UInt32 guid)
        {
            foreach (SFLParameterModel param in sfl_parameterlist)
            {
                if (param.guid.Equals(guid))
                    return param;
            }
            return null;
        }


        public SFLParameterModel GetParameterByName(string name)
        {
            foreach (SFLParameterModel param in sfl_parameterlist)
            {
                if (param.nickname.Equals(name))
                    return param;
            }
            return null;
        }

        #endregion

        #region 行为
        public UInt32 WriteDevice()
        {
            foreach (SFLParameterModel model in sfl_parameterlist)
            {
                if (model.berror && (model.errorcode & LibErrorCode.IDS_ERR_SECTION_DEVICECONFSFL) == LibErrorCode.IDS_ERR_SECTION_DEVICECONFSFL)
                    return LibErrorCode.IDS_ERR_SECTION_DEVICECONFSFL_PARAM_INVALID;

                model.IsWriteCalled = true;

                Parameter param = model.parent;
                if (model.brange)
                    param.phydata = model.data;
                else
                    param.sphydata = model.sphydata;

                model.IsWriteCalled = false;
            }
            return LibErrorCode.IDS_ERR_SUCCESSFUL;
        }
        #endregion
    }

    public class SFLParameterModel
    {
        private Parameter m_Parent;
        public Parameter parent
        {
            get { return m_Parent; }
            set { m_Parent = value; }
        }

        private string m_NickName;
        public string nickname
        {
            get { return m_NickName; }
            set { m_NickName = value; }
        }

        private double m_Data;
        public double data
        {
            get { return m_Data; }
            set
            {
                if (m_Data != value)
                {
                    m_Data = value;
                }
            }
        }

        private UInt32 m_Guid;
        public UInt32 guid
        {
            get { return m_Guid; }
            set { m_Guid = value; }
        }

        //参数在SFL参数列表中位置
        private Int32 m_Order;
        public Int32 order
        {
            get { return m_Order; }
            set { m_Order = value; }
        }

        private string m_Catalog;
        public string catalog
        {
            get { return m_Catalog; }
            set { m_Catalog = value; }
        }

        private UInt16 m_EditorType;
        public UInt16 editortype
        {
            get { return m_EditorType; }
            set { m_EditorType = value; }
        }

        private UInt16 m_Format;
        public UInt16 format
        {
            get { return m_Format; }
            set { m_Format = value; }
        }

        private string m_Description;
        public string description
        {
            get { return m_Description; }
            set { m_Description = value; }
        }

        private double m_MinValue;
        public double minvalue
        {
            get { return m_MinValue; }
            set
            {
                if (m_MinValue != value)
                {
                    m_MinValue = value;
                }
            }
        }

        private double m_MaxValue;
        public double maxvalue
        {
            get { return m_MaxValue; }
            set
            {
                if (m_MaxValue != value)
                {
                    m_MaxValue = value;
                }
            }
        }

        private bool m_bEdit;
        public bool bedit
        {
            get { return m_bEdit; }
            set
            {
                m_bEdit = value;
            }
        }

        private bool m_bError;
        public bool berror
        {
            get { return m_bError; }
            set
            {
                m_bError = value;
            }
        }

        private bool m_bRange;
        public bool brange
        {
            get { return m_bRange; }
            set
            {
                m_bRange = value;
            }
        }

        private string m_sPhydata;
        public string sphydata
        {
            get { return m_sPhydata; }
            set
            {
                //if (m_sPhydata != value)
                {
                    m_sPhydata = value;
                }
            }
        }

        private UInt16 m_EventMode;
        public UInt16 eventmode
        {
            get { return m_EventMode; }
            set
            {
                //if (m_EventMode != value)
                {
                    m_EventMode = value;
                }
            }
        }

        private UInt16 m_ListIndex;
        public UInt16 listindex
        {
            get { return m_ListIndex; }
            set
            {
                //if (m_ListIndex != value)
                {
                    m_ListIndex = value;
                }
            }
        }

        private bool m_bCheck;
        public bool bcheck
        {
            get { return m_bCheck; }
            set
            {
                //if (m_bCheck != value)
                {
                    m_bCheck = value;
                }
            }
        }

        private bool m_bROne;
        public bool brone
        {
            get { return m_bROne; }
            set
            {
                //if (m_bCheck != value)
                {
                    m_bROne = value;
                }
            }
        }

        private bool m_bWOne;
        public bool bwone
        {
            get { return m_bWOne; }
            set
            {
                //if (m_bCheck != value)
                {
                    m_bWOne = value;
                }
            }
        }

        private bool m_bSubMenu;
        public bool bsubmenu
        {
            get { return m_bSubMenu; }
            set
            {
                //if (m_bCheck != value)
                {
                    m_bSubMenu = value;
                }
            }
        }

        private UInt32 m_ErrorCode;
        public UInt32 errorcode
        {
            get { return m_ErrorCode; }
            set { m_ErrorCode = value; }
        }

        private UInt32? m_Source;    //Leon添加这个参数，为了解决relations循环问题
        public UInt32? source
        {
            get { return m_Source; }
            set { m_Source = value; }
        }

        private bool m_IsUpdateParamCalled = false;    //Leon添加这个参数，为了解决UpdateParam不调用问题
        public bool IsUpdateParamCalled
        {
            get { return m_IsUpdateParamCalled; }
            set { m_IsUpdateParamCalled = value; }
        }

        private bool m_IsWriteCalled = false;    //Leon添加这个参数，为了解决UpdateParam乱调用问题
        public bool IsWriteCalled
        {
            get { return m_IsWriteCalled; }
            set { m_IsWriteCalled = value; }
        }

        private AsyncObservableCollection<string> m_ItemList = new AsyncObservableCollection<string>();
        public AsyncObservableCollection<string> itemlist
        {
            get { return m_ItemList; }
            set
            {
                m_ItemList = value;
            }
        }

        private AsyncObservableCollection<UInt32> m_Relations = new AsyncObservableCollection<UInt32>();
        public AsyncObservableCollection<UInt32> relations
        {
            get { return m_Relations; }
            set
            {
                m_Relations = value;
            }
        }
    }


    public partial class MainControl
    {
        private SFLViewModel m_cfgviewmodel;
        public SFLViewModel cfgviewmodel
        {
            get { return m_cfgviewmodel; }
            set { m_cfgviewmodel = value; }
        }

        private SFLViewModel m_boardviewmodel;
        public SFLViewModel boardviewmodel
        {
            get { return m_boardviewmodel; }
            set { m_boardviewmodel = value; }
        }

        public bool border = false; //是否采用Order排序模式

    }
}