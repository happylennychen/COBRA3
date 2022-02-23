using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.ComponentModel;
using Cobra.Common;
using Cobra.EM;

namespace Cobra.DeviceConfigurationPanel
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

        private ParamContainer m_DM_One_ParameterList = new ParamContainer();
        public ParamContainer dm_part_parameterlist
        {
            get { return m_DM_One_ParameterList; }
            set { m_DM_One_ParameterList = value; }
        }

        private UInt16 order = 0;

        #region 支持C/H文件
        List<Dictionary<string,string>> fgs= new List<Dictionary<string,string>>();
        #endregion

        public SFLViewModel(object pParent, object parent, string Sflname)	//Leon: updated to support Board Config
        {
            #region 相关初始化
            device_parent = (Device)pParent;
            if (device_parent == null) return;

            control_parent = (MainControl)parent;
            if (control_parent == null) return;

            sflname = Sflname;
            if (String.IsNullOrEmpty(sflname)) return;
            #endregion

            dm_parameterlist = device_parent.GetParamLists(sflname);
            foreach (Parameter param in dm_parameterlist.parameterlist)
            {
                if (param == null) continue;
                sfl_parameterlist.Add(InitSFLParameter(param, sflname));
            }

            foreach (SFLModel mode in sfl_parameterlist)
            {
                if (mode == null) continue;               
                try
                {
                    phyTostr(mode);
                }
                catch (Exception e)
                {
                }
            }
        }

        #region 参数操作        
        public MainControl GetBoardConfigSFL()  //Issue1593 Leon
        {
            foreach (var i in device_parent.device_panellist)
            {
                MainControl o = i.item as MainControl;
                if(o != null)
                    if (o.sflname == COBRA_GLOBAL.Constant.NewBoardConfigName)
                        return o;
            }
            return null;
        }
        public SFLModel InitSFLParameter(Parameter param, string sflname)	//Leon: updated to support Board Config
        {
            Double errorvalue = -9999;
            UInt16 udata = 0;
            Double ddata = 0.0;
            bool bdata = false;
            SFLModel model = new SFLModel();

            model.parent = param.sfllist[sflname].parent;
            model.VeiwModelParent = this;
            model.guid = param.guid;
            model.bedit = true;
            model.berror = false;
            model.itemlist = param.itemlist;
            model.brange = true;
            model.brone = true;
            model.bwone = true;
            model.bsubmenu = true;
            model.sphydata = string.Empty;

            foreach (DictionaryEntry de in param.sfllist[sflname].nodetable)
            {
                switch (de.Key.ToString())
                {
                    case "bEdit":
                        if (!Boolean.TryParse(de.Value.ToString(), out bdata))
                            model.bedit = true;
                        else
                            model.bedit = bdata;
                        break;
                    case "NickName":
                        model.nickname = de.Value.ToString();
                        break;
                    case "Name":
                        model.name = de.Value.ToString();
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
                    case "sMessage":
                        model.sMessage = de.Value.ToString();
                        break;
                    case "MinValue":
                        {
                            if (!Double.TryParse(de.Value.ToString(), out ddata))
                                model.minvalue = 0.0;
                            else
                            {
                                double xmlmin = Convert.ToDouble(de.Value.ToString());
                                if (xmlmin != errorvalue)
                                    model.minvalue = Convert.ToDouble(de.Value.ToString());
                                else
                                {
                                    model.minvalue = control_parent.GetMinValue(param);
                                    MainControl BoardConfigSFL = GetBoardConfigSFL();
                                    if(BoardConfigSFL != null)
                                        BoardConfigSFL.BoardConfigChanged += new EventHandler(model.SFLModel_BoardConfigChanged);
                                }
                            }
                            break;
                        }
                    case "MaxValue":
                        {
                            if (!Double.TryParse(de.Value.ToString(), out ddata))
                                model.maxvalue = 0.0;
                            else
                            {
                                double xmlmax = Convert.ToDouble(de.Value.ToString());
                                if (xmlmax != errorvalue)
                                    model.maxvalue = Convert.ToDouble(de.Value.ToString());
                                else
                                {
                                    model.maxvalue = control_parent.GetMaxValue(param);
                                    MainControl BoardConfigSFL = GetBoardConfigSFL();
                                    if (BoardConfigSFL != null)
                                        BoardConfigSFL.BoardConfigChanged += new EventHandler(model.SFLModel_BoardConfigChanged);
                                }
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
                    case "DefStrValue":
                        {                            
                            model.sphydata = de.Value.ToString().Trim();
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
            if (sflname == COBRA_GLOBAL.Constant.OldBoardConfigName || sflname == COBRA_GLOBAL.Constant.NewBoardConfigName)//support them both in COBRA2.00.15, so all old and new OCEs will work fine.//Issue 1426 Leon
                model.bsubmenu = false;
            else
                model.bsubmenu = (model.brone | model.bwone);
            if ((model.data > model.maxvalue) || (model.data < model.minvalue))
                model.berror = true;
            else
                model.berror = false;

            param.PropertyChanged += new PropertyChangedEventHandler(Parameter_PropertyChanged);
            model.PropertyChanged += new PropertyChangedEventHandler(SFL_Parameter_PropertyChanged);
            return model;
            //sfl_parameterlist.Add(model);
        }

        internal void phyTostr(SFLModel p)
        {
            string tmp = "";
            if (p == null) return;

            p.PropertyChanged -= SFL_Parameter_PropertyChanged;
            if ((p.data > p.maxvalue) || (p.data < p.minvalue))
            {
                p.berror = true;
                p.errorcode = LibErrorCode.IDS_ERR_SECTION_DEVICECONFSFL_PARAM_INVALID;
            }
            else
                p.berror = false;
            
            if(p.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                p.berror = true;

            switch (p.editortype)
            {
                case 0:
                    {
                        switch (p.format)
                        {
                            case 0: //Int
                                tmp = String.Format("{0:D}", (Int32)p.data);
                                break;
                            case 1: //float1
                                tmp = String.Format("{0:F1}", p.data);
                                break;
                            case 2: //float2
                                tmp = String.Format("{0:F2}", p.data);
                                break;
                            case 3: //float3
                                tmp = String.Format("{0:F3}", p.data);
                                break;
                            case 4: //float4
                                tmp = String.Format("{0:F4}", p.data);
                                break;
                            case 5: //Hex
                                tmp = String.Format("0x{0:X2}", (byte)p.data);
                                break;
                            case 6: //Word
                                tmp = String.Format("0x{0:X4}", (UInt16)p.data);
                                break;
                            case 7: //DWord
                                tmp = String.Format("0x{0:X8}", (UInt32)p.data);
                                break;
                            case 8: //Date
                                tmp = SharedFormula.UInt32ToData((UInt16)p.data);
                                break;
                            case 9: //String
                                tmp = p.sphydata;
                                break;
                            default:
                                tmp = String.Format("{0}", p.data);
                                break;
                        }
                        p.sphydata = tmp;
                        break;
                    }
                case 1:
                    {
                        /*switch (p.format)
                        {
                            case 0:
                                p.listindex = (UInt16)p.data;
                                break;
                            default:
                                break;
                        }*/
                        p.listindex = (UInt16)p.data;   //Issue893 Leon
                        break;
                    }
                case 2:
                    {
                        if (p.data > 0.0)
                            p.bcheck = true;
                        else
                            p.bcheck = false;
                        break;
                    }
                default:
                    break;
            }
            p.PropertyChanged += SFL_Parameter_PropertyChanged;
        }

        internal void FWHexTostr(SFLModel p)
        {
            int len = 0;
            string tmp = "";
            StringBuilder stB = new StringBuilder();
            Parameter parent = null;
            if (p == null) return;

            parent = p.parent;
            p.PropertyChanged -= SFL_Parameter_PropertyChanged;
            switch (p.editortype)
            {
                case 0:
                    {
                        switch (p.format)
                        {
                            case 0: //Int
                                tmp = String.Format("{0:D}", (Int32)SharedFormula.MAKEDWORD(SharedFormula.MAKEWORD(parent.tsmbBuffer.bdata[0], parent.tsmbBuffer.bdata[1]), SharedFormula.MAKEWORD(parent.tsmbBuffer.bdata[2], parent.tsmbBuffer.bdata[3])));
                                break;
                            case 1: //float1
                                tmp = String.Format("{0:F1}", (Int32)SharedFormula.MAKEDWORD(SharedFormula.MAKEWORD(parent.tsmbBuffer.bdata[0], parent.tsmbBuffer.bdata[1]), SharedFormula.MAKEWORD(parent.tsmbBuffer.bdata[2], parent.tsmbBuffer.bdata[3])));
                                break;
                            case 2: //float2
                                tmp = String.Format("{0:F2}", (Int32)SharedFormula.MAKEDWORD(SharedFormula.MAKEWORD(parent.tsmbBuffer.bdata[0], parent.tsmbBuffer.bdata[1]), SharedFormula.MAKEWORD(parent.tsmbBuffer.bdata[2], parent.tsmbBuffer.bdata[3])));
                                break;
                            case 3: //float3
                                tmp = String.Format("{0:F3}", (Int32)SharedFormula.MAKEDWORD(SharedFormula.MAKEWORD(parent.tsmbBuffer.bdata[0], parent.tsmbBuffer.bdata[1]), SharedFormula.MAKEWORD(parent.tsmbBuffer.bdata[2], parent.tsmbBuffer.bdata[3])));
                                break;
                            case 4: //float4
                                tmp = String.Format("{0:F4}", (Int32)SharedFormula.MAKEDWORD(SharedFormula.MAKEWORD(parent.tsmbBuffer.bdata[0], parent.tsmbBuffer.bdata[1]), SharedFormula.MAKEWORD(parent.tsmbBuffer.bdata[2], parent.tsmbBuffer.bdata[3])));
                                break;
                            case 5: //Hex
                                tmp = String.Format("0x{0:X2}", SharedFormula.MAKEDWORD(SharedFormula.MAKEWORD(parent.tsmbBuffer.bdata[0], parent.tsmbBuffer.bdata[1]), SharedFormula.MAKEWORD(parent.tsmbBuffer.bdata[2], parent.tsmbBuffer.bdata[3])));
                                break;
                            case 6: //Word
                                tmp = String.Format("0x{0:X4}", SharedFormula.MAKEDWORD(SharedFormula.MAKEWORD(parent.tsmbBuffer.bdata[0], parent.tsmbBuffer.bdata[1]), SharedFormula.MAKEWORD(parent.tsmbBuffer.bdata[2], parent.tsmbBuffer.bdata[3])));
                                break;
                            case 7: //DWord
                                tmp = String.Format("0x{0:X8}", SharedFormula.MAKEDWORD(SharedFormula.MAKEWORD(parent.tsmbBuffer.bdata[0], parent.tsmbBuffer.bdata[1]), SharedFormula.MAKEWORD(parent.tsmbBuffer.bdata[2], parent.tsmbBuffer.bdata[3])));
                                break;
                            case 8: //Date
                                tmp = String.Format("0x{0:X8}", SharedFormula.MAKEDWORD(SharedFormula.MAKEWORD(parent.tsmbBuffer.bdata[0], parent.tsmbBuffer.bdata[1]), SharedFormula.MAKEWORD(parent.tsmbBuffer.bdata[2], parent.tsmbBuffer.bdata[3])));
                                break;
                            case 9: //String
                                stB.Clear();
                                stB.Append("{");
                                len = parent.tsmbBuffer.bdata[0];
                                stB.Append(String.Format("0x{0:X2}",len));
                                Char[] c = Encoding.ASCII.GetChars(parent.tsmbBuffer.bdata, 1, len);
                                for (int i = 0; i < c.Length; i++ )
                                {
                                    stB.Append(",");
                                    stB.Append("'");
                                    stB.Append(c[i].ToString());
                                    stB.Append("'");
                                }
                                stB.Append("}");
                                tmp = stB.ToString();
                                break;
                            default:
                                tmp = String.Format("{0}", p.data);
                                break;
                        }
                        break;
                    }
                case 1:
                    {
                        tmp = String.Format("{0:D}", SharedFormula.MAKEDWORD(SharedFormula.MAKEWORD(parent.tsmbBuffer.bdata[0], parent.tsmbBuffer.bdata[1]), SharedFormula.MAKEWORD(parent.tsmbBuffer.bdata[2], parent.tsmbBuffer.bdata[3])));
                        break;
                    }
                case 2:
                    {
                        tmp = String.Format("{0:D}", SharedFormula.MAKEDWORD(SharedFormula.MAKEWORD(parent.tsmbBuffer.bdata[0], parent.tsmbBuffer.bdata[1]), SharedFormula.MAKEWORD(parent.tsmbBuffer.bdata[2], parent.tsmbBuffer.bdata[3])));
                        break;
                    }
                default:
                    break;
            }
            p.sphydata = tmp;
            p.PropertyChanged += SFL_Parameter_PropertyChanged;
        }

        internal void strTophy(ref SFLModel p)
        {
            double ddata = 0.0;
            p.berror = false;
            if (p == null)
            {
                p.errorcode = LibErrorCode.IDS_ERR_PARAM_INVALID_HANDLER;
                return;
            }

            switch (p.editortype)
            {
                #region 定义编辑类型
                case 0:
                    {
                        switch (p.format)
                        {
                            case 0: //Int     
                            case 1: //float1
                            case 2: //float2
                            case 3: //float3
                            case 4: //float4
                                {
                                    if (!Double.TryParse(p.sphydata, out ddata))
                                        p.errorcode = LibErrorCode.IDS_ERR_PARAM_DATA_ILLEGAL;
                                    else
                                        p.errorcode = LibErrorCode.IDS_ERR_SUCCESSFUL;
                                    break;
                                }
                            case 5: //Hex
                            case 6: //Word
                                {
                                    try
                                    {
                                        ddata = (Double)Convert.ToInt32(p.sphydata, 16);
                                    }
                                    catch (Exception e)
                                    {
                                        p.errorcode = LibErrorCode.IDS_ERR_PARAM_DATA_ILLEGAL;
                                        break;
                                    }
                                    p.errorcode = LibErrorCode.IDS_ERR_SUCCESSFUL;
                                    break;
                                }
                            case 7: //DWord
                                {
                                    try
                                    {
                                        ddata = (Double)Convert.ToUInt32(p.sphydata, 16);
                                    }
                                    catch (Exception e)
                                    {
                                        p.errorcode = LibErrorCode.IDS_ERR_PARAM_DATA_ILLEGAL;
                                        break;
                                    }
                                    p.errorcode = LibErrorCode.IDS_ERR_SUCCESSFUL;
                                    break;
                                }
                            case 8: //Date
                                {
                                    try
                                    {
                                        ddata = SharedFormula.DateToUInt32(p.sphydata);
                                    }
                                    catch (Exception e)
                                    {
                                        p.errorcode = LibErrorCode.IDS_ERR_PARAM_DATA_ILLEGAL;
                                        break;
                                    }
                                    p.errorcode = LibErrorCode.IDS_ERR_SUCCESSFUL;
                                    break;
                                }
                            case 9: //String
                                break;
                            default:
                                break;
                        }
                        //转换string为double类型
                        //if ((ddata > p.maxvalue) || (ddata < p.minvalue))
                            //p.errorcode = LibErrorCode.IDS_ERR_PARAM_DATA_OVERRANGE;
                    }
                    break;
                #endregion
                #region 定义复选类型
                case 1:
                    {
                        switch (p.format)
                        {
                            case 0:
                                {
                                    ddata = (double)p.listindex;
                                    p.errorcode = LibErrorCode.IDS_ERR_SUCCESSFUL;
                                    break;
                                }
                            case 1:
                                {
                                    if (!Double.TryParse(p.itemlist[p.listindex], out ddata))
                                        p.errorcode = LibErrorCode.IDS_ERR_PARAM_DATA_ILLEGAL;
                                    else
                                        p.errorcode = LibErrorCode.IDS_ERR_SUCCESSFUL;
                                    break;
                                }
                        }
                    }
                    break;
                #endregion
                #region 定义点击类型
                case 2:
                    {
                        if (p.bcheck)
                            ddata = 1.0;
                        else
                            ddata = 0.0;
                        p.errorcode = LibErrorCode.IDS_ERR_SUCCESSFUL;
                    }
                    break;
                #endregion
                default:
                    break;
            }
            if(p.errorcode == LibErrorCode.IDS_ERR_SUCCESSFUL)
                p.data = ddata;
        }

        /// <summary>
        /// 更新参数相关属性
        /// </summary>
        /// <param name="param"></param>
        private void UpdateParam(ref SFLModel param)
        {
            SFLModel model = null;
            if (param == null) return;
            bool preventloop = false;

            //转换UI值到默认值
            /*
            strTophy(ref param);
            if (param.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                //消息记录
                control_parent.gm.controls = param.nickname;
                control_parent.gm.message = LibErrorCode.GetErrorDescription(param.errorcode);
                //呼叫警告框
                control_parent.CallWarningControl(control_parent.gm);
                //恢复原始值
                phyTostr(param);
                return;
            }*/

            //针对编辑状态更新
            if (param.relations.Count == 0) return;
            switch (param.editortype)
            {
                case 0:
                    {
                        //针对电阻类型的更新
                        param.parent.PropertyChanged -= Parameter_PropertyChanged;
                        param.parent.phydata = param.data;
                        param.parent.PropertyChanged += Parameter_PropertyChanged;

                        foreach (uint guid in param.relations)
                        {
                            if (guid != param.source)                       //目标参数不可以是源参数，不然就是无效循环
                            {
                                SFLModel target = GetParameterByGuid(guid);     //获取目标参数
                                if (target.relations.Contains(param.guid))       //如果目标参数的关联列表中有源参数
                                {
                                    //target.relations.Remove(param.guid);        //暂时移除源参数
                                    target.source = param.guid;                   //记录源参数到source变量中
                                    preventloop = true;                         //标记进行了防循环动作

                                }
                                device_parent.UpdataDEMParameterList(dm_parameterlist.GetParameterByGuid(guid));

                                if (preventloop)                                //如果进行了防循环动作
                                {
                                    //target.relations.Add(param.guid);        //移除源参数
                                    target.source = null;                       //取消source中记录的源参数
                                    preventloop = false;                         //解除标记
                                }
                            }
                        }
                        break;
                    }
                case 1:
                    {
                        switch (param.eventmode)
                        {
                            case 0:
                                {
                                    foreach (UInt32 guid in param.relations)
                                    {
                                        model = GetParameterByGuid(guid);
                                        if (model == null) continue;

                                        //if (param.data > 0.0)
                                        if (param.data >= 0)  //In case param.data == -9999 which means it is illigle
                                        {
                                            if (param.itemlist[(int)param.data].ToUpper() == "ENABLE")
                                                model.bedit = true;
                                            else if (param.itemlist[(int)param.data].ToUpper() == "DISABLE")
                                                model.bedit = false;
                                        }
                                    }
                                    param.parent.PropertyChanged -= Parameter_PropertyChanged;
                                    param.parent.phydata = param.data;
                                    param.parent.PropertyChanged += Parameter_PropertyChanged;

                                    foreach (uint guid in param.relations)
                                    {
                                        if (guid != param.source)                       //目标参数不可以是源参数，不然就是无效循环
                                        {
                                            SFLModel target = GetParameterByGuid(guid);     //获取目标参数
                                            if (target.relations.Contains(param.guid))       //如果目标参数的关联列表中有源参数
                                            {
                                                //target.relations.Remove(param.guid);        //暂时移除源参数
                                                target.source = param.guid;                   //记录源参数到source变量中
                                                preventloop = true;                         //标记进行了防循环动作

                                            }

                                            device_parent.UpdataDEMParameterList(dm_parameterlist.GetParameterByGuid(guid));

                                            if (preventloop)                                //如果进行了防循环动作
                                            {
                                                //target.relations.Add(param.guid);        //移除源参数
                                                target.source = null;                       //取消source中记录的源参数
                                                preventloop = false;                         //解除标记
                                            }
                                        }
                                    }

                                    break;
                                }
                            case 1:
                                {
                                    UInt16 total = 0;
                                    int mindex = (int)(param.maxvalue - 1);
                                    UInt16.TryParse(param.itemlist[mindex], out total);

                                    for (int i = 0; i < (param.listindex + total - param.itemlist.Count + 1); i++)
                                    {
                                        model = GetParameterByGuid(param.relations[i]);
                                        if (model == null) continue;
                                        model.bedit = true;
                                    }
                                    for (int i = (int)(param.listindex + total - param.itemlist.Count + 1); i < total; i++)
                                    {
                                        model = GetParameterByGuid(param.relations[i]);
                                        if (model == null) continue;
                                        model.bedit = false;
                                    }
                                    break;
                                }
                            default:
                                break;
                        }
                        break;
                    }
                case 2:
                    {
                        foreach (UInt32 guid in param.relations)
                        {
                            model = GetParameterByGuid(guid);
                            if (model == null) continue;

                            if (param.data > 0.0)
                                model.bedit = true;
                            else
                                model.bedit = false;
                        }
                        break;
                    }
            }

            //记录消息
            control_parent.gm.controls = param.nickname;
            control_parent.gm.message = "Exit the edit!";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void SFL_Parameter_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            SFLModel p = (SFLModel)sender;
            switch (e.PropertyName.ToString())
            {
                case "sphydata":
                case "bcheck":
                case "listindex":
                     {
                        if (p.brange)
                        {
                            strTophy(ref p);
                            if (p.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                            {
                                //消息记录
                                control_parent.gm.controls = p.nickname;
                                control_parent.gm.message = LibErrorCode.GetErrorDescription(p.errorcode);
                                //呼叫警告框
                                control_parent.CallWarningControl(control_parent.gm);
                                //恢复原始值
                                phyTostr(p);
                                return;
                            }
                            UpdateOneModel(p);	//Leon Issue 1941: UI更新就更新DM Parameter
                            //UpdateParam(ref p);
                        }
                        break;
                    }
                case "data":
                    {
                        phyTostr(p);
                        UpdateParam(ref p);
                        p.IsUpdateParamCalled = true;
                        UpdateOneModel(p);	//Leon Issue 1941: UI更新就更新DM Parameter
                        break;
                    }
                default:
                    break;
            }
        }

        public void Parameter_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Parameter p = (Parameter)sender;
            SFLModel model = GetParameterByGuid(p.guid);
            if (model == null) return;

            model.IsUpdateParamCalled = false;
            switch (e.PropertyName.ToString())
            {
                case "phydata":
                    {
                        if ((p.phydata > model.maxvalue) || (p.phydata < model.minvalue) || (p.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL))
                            model.berror = true;
                        else
                            model.berror = false;
                            
                        model.data = p.phydata;
                        model.errorcode = p.errorcode;

                        if (model.IsUpdateParamCalled == false && model.IsWriteCalled == false)
                            UpdateParam(ref model);
                        model.IsUpdateParamCalled = false;
                        break;
                    }
                case "itemlist":
                    {
                        if (p.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                            model.berror = true;
                        else
                            model.berror = false;

                        model.itemlist = p.itemlist;        //Issue1438 Leon
                        model.listindex = model.listindex; //触发Combobox选择
                        model.errorcode = p.errorcode;
                        break;
                    }
                case "sphydata":
                    {
                        if (p.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                            model.berror = true;
                        else
                            model.berror = false;
                        
                        model.sphydata = p.sphydata;
                        model.errorcode = p.errorcode;
                        break;
                    }
                case "dbPhyMin":
                    {
                        if (p.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                            model.berror = true;
                        else
                            model.berror = false;

                        model.minvalue = p.dbPhyMin;
                        model.errorcode = p.errorcode;
                        break;
                    }
                case "dbPhyMax":
                    {
                        if (p.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                            model.berror = true;
                        else
                            model.berror = false;

                        model.maxvalue = p.dbPhyMax;
                        model.errorcode = p.errorcode;
                        break;
                    }
                case "sMessage":
                    {
                        model.sMessage = p.sMessage;
                        break;
                    }
                default:
                    break;
            }
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

        #region 行为
        public UInt32 UpdateAllModels()	//原WriteDevice, Leon updated
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            foreach (SFLModel vm in sfl_parameterlist)
            {
                ret = UpdateOneModel(vm);
                if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    return ret;
            }
            return ret;
        }

        private UInt32 UpdateOneModel(SFLModel vm)
        {
            if (vm.berror && (vm.errorcode & LibErrorCode.IDS_ERR_SECTION_DEVICECONFSFL) == LibErrorCode.IDS_ERR_SECTION_DEVICECONFSFL)
                return LibErrorCode.IDS_ERR_SECTION_DEVICECONFSFL_PARAM_INVALID;

            vm.IsWriteCalled = true;

            Parameter param = vm.parent;
            if (vm.brange)
                param.phydata = vm.data;
            else
                param.sphydata = vm.sphydata;

            vm.IsWriteCalled = false;

            return LibErrorCode.IDS_ERR_SUCCESSFUL;
        }

        public UInt32 BuildPartParameterList(string guid)
        {
            UInt32 uid = 0;
            dm_part_parameterlist.parameterlist.Clear();
            if (!UInt32.TryParse(guid, out uid)) return LibErrorCode.IDS_ERR_COM_INVALID_PARAMETER;

            SFLModel param = GetParameterByGuid(uid);
            if (param == null) return LibErrorCode.IDS_ERR_COM_INVALID_PARAMETER;

            foreach (UInt32 tuid in param.relations)
            {
                SFLModel model = GetParameterByGuid(tuid);
                if (param == null) continue;

                dm_part_parameterlist.parameterlist.Add(model.parent);
            }
            dm_part_parameterlist.parameterlist.Add(param.parent);
            return LibErrorCode.IDS_ERR_SUCCESSFUL;
        }
        #endregion
    }
}
