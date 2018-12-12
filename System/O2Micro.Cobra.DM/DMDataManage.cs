using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Reflection;
using System.ComponentModel;
using System.IO;
using O2Micro.Cobra.Common;

namespace O2Micro.Cobra.DM
{
    public class DMDataManage
    {
        #region 属性定义
        /// <summary>
        /// 特殊设备库名及所在应用域
        /// </summary>
        public static string libname;
        private Assembly m_device_assembly;
        public Assembly device_assembly
        {
            get { return m_device_assembly; }
            set { m_device_assembly = value; }
        }

        /// <summary>
        /// 硬件模式下设备内存
        /// </summary>
        private ParamListContainer m_SectionParamList_Container = new ParamListContainer();
        private ParamListContainer m_SFLsParamList_Container = new ParamListContainer();

        /// <summary>
        /// 设备对象
        /// </summary>
        private IDEMLib m_dem_lib;
        public IDEMLib dem_lib
        {
            get { return m_dem_lib; }
            set { m_dem_lib = value; }
        }

        public static List<string> m_SFLNames_list = new List<string>();
        private BusOptions m_busoption = null;
        #endregion

        /// <summary>
        /// 数据源初始化
        /// </summary>
        public bool Init(ref BusOptions busoptions)
        {
            m_busoption = busoptions;
            //构建SFLs参数列表容器
            SFLsParamListContainerInit();

            //初始化HWModeDataManage数据结构
            if (!LoadDeviceDescriptionXML()) return false;

            //实例化方法
            InitDEM(ref busoptions);
            return true;
        }

        public bool EnumerateInterface()
        {
            return m_dem_lib.EnumerateInterface();
        }

        public bool CreateInterface()
        {
            return m_dem_lib.CreateInterface();
        }

        public void UpdataDEMParameterList(Parameter p)
        { 
            m_dem_lib.UpdataDEMParameterList(p);
        }
        
        /// <summary>
        /// 加载Device Strucutre XML
        /// 实例化参数并建立链表
        /// </summary>
        /// <returns></returns>
        private bool LoadDeviceDescriptionXML()
        {
            ParamContainer list = null;
            try
            {
                XElement rootNode = XElement.Load(FolderMap.m_extension_work_folder + FolderMap.m_dev_descrip_xml_name + FolderMap.m_extension_work_ext);
                IEnumerable<XElement> targetNodes = from target in rootNode.Elements("Section") select target;
                foreach (XElement node in targetNodes)
                {
                    list = new DMParameterList(this, node);
                               
                    m_SectionParamList_Container.AddParameterList(list);
                }
                BuildSFLsParamListContainer();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return false;						//add by leon, fix a logic issue
            }
            return true;
        }

        /// <summary>
        /// 建立SFLs参数列表
        /// 按参数属性加入各自SFLs
        /// </summary>
        private void BuildSFLsParamListContainer()
        {
            foreach (ParamContainer list in m_SectionParamList_Container.deviceparameterlistcontainer)
            {
                foreach (Parameter p in list.parameterlist)
                {
                    ICollection<string> key= p.sfllist.Keys;
                    foreach (string sfl in key)
                    {
                        foreach (string name in m_SFLNames_list)
                        {
                            if (sfl.Equals(name))
                                m_SFLsParamList_Container.GetParameterListByName(name).parameterlist.Add(p);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 初始化SFLs参数列表
        /// 参数列表值均为空
        /// </summary>
        private void SFLsParamListContainerInit()
        {
            ParamContainer list = null;

            foreach (string name in m_SFLNames_list)
            {
                list = m_SFLsParamList_Container.GetParameterListByName(name);
                if (list == null)
                {
                    list = new ParamContainer();
                    list.listname = name;
                    m_SFLsParamList_Container.AddParameterList(list);
                }
            }
        }


        /// <summary>
        /// 实例化
        /// </summary>
        /// <returns></returns>
        private bool InitDEM(ref BusOptions busoptions)
        {
            if (!File.Exists(libname)) return false;
            byte[] raw = File.ReadAllBytes(libname);
            device_assembly = Assembly.Load(raw);

            Type tType = null;
            object o = null;
            //获得程序集里的所有类
            Type[] types = device_assembly.GetTypes();
            foreach (Type type in types)
            {
                switch (type.Name)
                {
                    case "DEMDeviceManage":
                        {
                            tType = type;
                            o = Activator.CreateInstance(tType, new object[] { });
                            m_dem_lib = o as IDEMLib;
                            break;
                        }
                }
            }

            m_dem_lib.Init(ref busoptions, ref m_SectionParamList_Container, ref m_SFLsParamList_Container);
            return true;
        }

        /// <summary>
        /// 通过索引获取参数
        /// </summary>
        /// <param name="sType"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public Parameter GetObject(string sType, UInt32 key)
        {
            return m_SectionParamList_Container.GetParameterListByName(sType).GetParameterByGuid(key);
        }

        /// <summary>
        /// 通过类型获取参数列表
        /// </summary>
        /// <param name="sType"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public AsyncObservableCollection<Parameter> GetObjects(string sType)
        {
            return m_SectionParamList_Container.GetParameterListByName(sType).parameterlist;
        }

        /// <summary>
        /// 通过类型获取参数列表
        /// </summary>
        /// <param name="sType"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public ParamContainer GetParamLists(string sType)
        {
            return m_SFLsParamList_Container.GetParameterListByName(sType);
        }

        public void Destory()
        {
            //m_local_loader.Unload();
            try
            {
                (dem_lib as IDEMLib2).DestroyInterface();
            }
            catch (System.Exception ex)
            {
            	
            }
            m_SFLsParamList_Container.deviceparameterlistcontainer.Clear();
            m_SectionParamList_Container.deviceparameterlistcontainer.Clear();

            device_assembly = null;
            dem_lib = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        #region 设备操作
        public UInt32 GetDeviceInfor(ref DeviceInfor deviceinfor)
        {
            AutomationElement el = null;
            try
            {
                el = m_busoption.GetATMElementbyGuid(AutomationElement.GUIDATMTestStart);
                if ((el != null) && (el.dbValue != 0)) //AutoMationTest Mode
                    return LibErrorCode.IDS_ERR_SUCCESSFUL;
            }
            catch (System.Exception ex)
            {
                return LibErrorCode.IDS_ERR_SUCCESSFUL;
            }
           return m_dem_lib.GetDeviceInfor(ref deviceinfor);
        }

        public UInt32 Erase(ref TASKMessage bgworker)
        {
            return m_dem_lib.Erase(ref bgworker);
        }

        public UInt32 BlockMap(ref TASKMessage bgworker)
        {
            return m_dem_lib.BlockMap(ref bgworker);
        }

        public UInt32 Command(ref TASKMessage msg)
        {
            return m_dem_lib.Command(ref msg);
        }

        public UInt32 Read(ref TASKMessage bgworker)
        {
            return m_dem_lib.Read(ref bgworker);
        }

        public UInt32 Write(ref TASKMessage bgworker)
        {
            return m_dem_lib.Write(ref bgworker);
        }

        public UInt32 ConvertHexToPhysical(ref TASKMessage m_Msg)
        {
            return m_dem_lib.ConvertHexToPhysical(ref m_Msg);
        }

        public UInt32 ConvertPhysicalToHex(ref TASKMessage m_Msg)
        {
            return m_dem_lib.ConvertPhysicalToHex(ref m_Msg);
        }

        public UInt32 GetSystemInfor(ref TASKMessage m_Msg)
        {
            return m_dem_lib.GetSystemInfor(ref m_Msg);
        }

        public UInt32 GetRegisteInfor(ref TASKMessage m_Msg)
        {
            return m_dem_lib.GetRegisteInfor(ref m_Msg);
        }

        public UInt32 BitOperation(ref TASKMessage m_Msg)
        {
            return m_dem_lib.BitOperation(ref m_Msg);
        }
        #endregion
    }
}
