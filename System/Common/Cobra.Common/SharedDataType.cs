using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using System.Collections;
using System.ComponentModel;
using System.Reflection;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;
using System.Windows;

namespace Cobra.Common
{
    #region 公共信息
    public class GeneralMessage : EventArgs, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        public GeneralMessage()
        {
            controls = null;
            message = null;
            level = 0;
        }

        public GeneralMessage(string scontrol, string smessage, int ilevel = 0)
        {
            controls = scontrol;
            message = smessage;
            level = ilevel;
        }

        //external property
        public string time
        {
            get { return (DateTime.Now.ToString()); }
        }

        //SFL名称
        private string m_SFLName;
        public string sflname
        {
            get { return m_SFLName; }
            set { m_SFLName = value; }
        }

        //Control Name
        private string m_Controls;
        public string controls
        {
            get { return m_Controls; }
            set { m_Controls = value; }
        }

        //定义警告信息
        private string m_Message;
        public string message
        {
            get { return m_Message; }
            set { m_Message = value; }
        }

        //设备索引
        private int m_DeviceIndex;
        public int deviceindex
        {
            get { return m_DeviceIndex; }
            set { m_DeviceIndex = value; }
        }

        //消息等级0：正常操作    白 message
        //       1： 参数UI操作  绿 Error
        //       2： 参数设备操作  红 Warning
        private int m_Level;
        public int level
        {
            get { return m_Level; }
            set { m_Level = value; }
        }

        private bool m_bUpdate;
        public bool bupdate
        {
            get { return m_bUpdate; }
            set
            {
                m_bUpdate = value;
                OnPropertyChanged("bupdate");
            }
        }

        public void setvalue(GeneralMessage gm)
        {
            sflname = gm.sflname;
            controls = gm.controls;
            message = gm.message;
            deviceindex = gm.deviceindex;
            level = gm.level;

            bupdate = gm.bupdate;
        }
    }

    public class ControlMessage
    {
        public ControlMessage()
        {
            bshow = false;
            percent = 0;
            message = String.Empty;
        }

        //控件展现
        private bool m_bShow;
        public bool bshow
        {
            get { return m_bShow; }
            set { m_bShow = value; }
        }

        //百分比
        private int m_Percent;
        public int percent
        {
            get { return m_Percent; }
            set { m_Percent = value; }
        }

        //信息
        private string m_Message;
        public string message
        {
            get { return m_Message; }
            set { m_Message = value; }
        }

        //密码
        private UInt16 m_Password;
        public UInt16 password
        {
            get { return m_Password; }
            set { m_Password = value; }
        }

        //结果
        private bool m_bCancel;
        public bool bcancel
        {
            get { return m_bCancel; }
            set { m_bCancel = value; }
        }
    }

    public class SystemMessage : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private TSMBbuffer m_TsmbBuffer = new TSMBbuffer(); //ID:632
        public TSMBbuffer tsmbBuffer
        {
            get { return m_TsmbBuffer; }
            set { m_TsmbBuffer = value; }
        }

        public SystemMessage()
        {
            for (int i = 0; i < gpios.Length; i++)
            {
                gpios[i] = false;
                misc[i] = 0;
                parts[i] = false;
            }
        }

        //GPIO
        private bool[] m_GPIOs = new bool[10];
        public bool[] gpios
        {
            get { return m_GPIOs; }
            set { m_GPIOs = value; }
        }

        //Others
        private UInt16[] m_Misc = new UInt16[10];
        public UInt16[] misc
        {
            get { return m_Misc; }
            set { m_Misc = value; }
        }

        private bool[] m_Parts = new bool[10];
        public bool[] parts
        {
            get { return m_Parts; }
            set { m_Parts = value; }
        }

        private Dictionary<uint, bool> m_dic = new Dictionary<uint, bool>();
        public Dictionary<uint, bool> dic
        {
            get { return m_dic; }
            set { m_dic = value; }
        }

        private string m_hexdata = String.Empty;	//Issue1265 Leon
        public string efusehexdata
        {
            set { m_hexdata = value; }
            get { return m_hexdata; }
        }

        private List<byte> m_bindata = new List<byte>();	//Issue1336 Leon
        public List<byte> efusebindata
        {
            set { m_bindata = value; }
            get { return m_bindata; }
        }
    }

    public class TASKMessage : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        public TASKMessage()
        {
            owner = null;
            sub_task = 0;
            task = TM.TM_DEFAULT;
            errorcode = LibErrorCode.IDS_ERR_SUCCESSFUL;
            controlreq = COMMON_CONTROL.COMMON_CONTROL_DEFAULT;
        }

        //控件名
        private Object m_Owner;
        public Object owner
        {
            get { return m_Owner; }
            set { m_Owner = value; }
        }

        public string m_funName;
        public string funName
        {
            get { return m_funName; }
            set { m_funName = value; }
        }

        //操作任务信息
        private TM m_Task;
        public TM task
        {
            get { return m_Task; }
            set { m_Task = value; }
        }

        //读或写
        private bool m_bRW;
        public bool brw
        {
            get { return m_bRW; }
            set { m_bRW = value; }
        }

        //提供给DEM，告诉是局部访问还是全局访问
        //0：全局访问，所有参数参与运算
        //X：局部访问，局部参数参与运算
        private UInt16 m_sub_Task;
        public UInt16 sub_task
        {
            get { return m_sub_Task; }
            set { m_sub_Task = value; }

        }

        private const UInt16 m_SUBTASK_JSON_MASK = 0xffff;
        public UInt16 SUBTASK_JSON_MASK
        {
            get { return m_SUBTASK_JSON_MASK; }
        }

        private string m_sub_Task_Json;
        public string sub_task_json
        {
            get { return m_sub_Task_Json; }
            set { m_sub_Task_Json = value; }
        }

        private byte[] m_FlashData;
        public byte[] flashData
        {
            get { return m_FlashData; }
            set { m_FlashData = value; }
        }

        private List<MemoryControl> m_BufferList;
        public List<MemoryControl> bufferList
        {
            get { return m_BufferList; }
            set { m_BufferList = value; }
        }

        //任务进度
        private int m_Percent;
        public int percent
        {
            get { return m_Percent; }
            set { m_Percent = value; }
        }

        //错误信息码
        private UInt32 m_Error;
        public UInt32 errorcode
        {
            get { return m_Error; }
            set { m_Error = value; }
        }

        //消息控件请求
        private COMMON_CONTROL m_Control_Req;
        public COMMON_CONTROL controlreq
        {
            get { return m_Control_Req; }
            set
            {
                m_Control_Req = value;
                OnPropertyChanged("controlreq");
            }
        }

        //控件展现信息
        private ControlMessage m_Control_Msg = new ControlMessage();
        public ControlMessage controlmsg
        {
            get { return m_Control_Msg; }
            set { m_Control_Msg = value; }
        }

        private GeneralMessage m_GM = new GeneralMessage();
        public GeneralMessage gm
        {
            get { return m_GM; }
            set { m_GM = value; }
        }

        private SystemMessage m_SM = new SystemMessage();
        public SystemMessage sm
        {
            get { return m_SM; }
            set { m_SM = value; }
        }

        private BackgroundWorker m_Bgworker = new BackgroundWorker();
        public BackgroundWorker bgworker
        {
            get { return m_Bgworker; }
            set { m_Bgworker = value; }
        }

        private ParamContainer m_task_parameterlist = new ParamContainer();
        public ParamContainer task_parameterlist
        {
            get { return m_task_parameterlist; }
            set { m_task_parameterlist = value; }
        }
    }

    public class LibInfor : MarshalByRefObject
    {
        public static ObservableCollection<VersionInfo> m_assembly_list = new ObservableCollection<VersionInfo>();
        public ObservableCollection<VersionInfo> GetAssemblyList()
        {
            return m_assembly_list;
        }

        public VersionInfo GetAssemblyByType(ASSEMBLY_TYPE type)
        {
            foreach (VersionInfo ver in m_assembly_list)
            {
                if (ver.Assembly_Type != type) continue;
                return ver;
            }
            return null;
        }

        public Process GetCurrentProcess()
        {
            return Process.GetCurrentProcess();
        }

        public static bool m_bUpgrade = false;
        public void upgradeIsRun(bool brun)
        {
            m_bUpgrade = brun;
        }

        public static void Init()
        {
            m_assembly_list.Clear();
        }

        public static void Add(VersionInfo info)
        {
            if ((m_assembly_list.Count == 0) | (info.ErrorCode != LibErrorCode.IDS_ERR_SUCCESSFUL))
            {
                m_assembly_list.Add(info);
                return;
            }
            foreach (VersionInfo ver in m_assembly_list)
            {
                if (string.Compare(ver.Assembly_ProjectCode, info.Assembly_ProjectCode) == 0)
                {
                    ver.Assembly_ver = info.Assembly_ver;
                    ver.Assembly_name = info.Assembly_name;
                    ver.Assembly_Path = info.Assembly_Path;
                    ver.Assembly_Type = info.Assembly_Type;
                    return;
                }
            }
            m_assembly_list.Add(info);
        }

        public static void AssemblyRegister(Assembly asm, ASSEMBLY_TYPE type)
        {
            AssemblyDescriptionAttribute asmdis = (AssemblyDescriptionAttribute)Attribute.GetCustomAttribute(asm, typeof(AssemblyDescriptionAttribute));
            new VersionInfo(asm.GetName().Name, asmdis.Description, asm.GetName().Version, type);
        }
    }
    #endregion

    #region 枚举类型定义
    /// <summary>
    /// Definition type of Hardware Interface, we're having O2USBtoI2C, AardvarkI2C(not supported), O2USBtoSPI(not supported), and AardvarkSPI(not supported)
    /// </summary>
    public enum BUS_TYPE
    {
        BUS_TYPE_I2C = 0,
        BUS_TYPE_I2C2,
        BUS_TYPE_SPI,
        BUS_TYPE_SVID,
        BUS_TYPE_RS232,
    }

    public enum TM
    {
        TM_DEFAULT = 0,
        TM_READ,
        TM_WRITE,
        TM_COMMAND,
        TM_BITOPERATION,
        TM_BLOCK_ERASE,
        TM_BLOCK_MAP,
        TM_CONVERT_PHYSICALTOHEX,
        TM_CONVERT_HEXTOPHYSICAL,
        TM_SPEICAL_GETSYSTEMINFOR,
        TM_SPEICAL_GETDEVICEINFOR,
        TM_SPEICAL_GETREGISTEINFOR,
        TM_SPEICAL_READDEVICE,
        TM_SPEICAL_WRITEDEVIE,
        TM_SPEICAL_VERIFICATION
    };

    public enum SUB_TM  //ID:632
    {
        SUB_TM_READ = 0x8000,
        SUB_TM_WRITE
    };

    public enum COMMON_CONTROL
    {
        COMMON_CONTROL_DEFAULT = 0,
        COMMON_CONTROL_WARNING,
        COMMON_CONTROL_PASSWORD,
        COMMON_CONTROL_WAITTING,
        COMMON_CONTROL_SELECT,
    }

    public enum BUS_CONFIG
    {
        CONFIG_I2C = 0x00,
        CONFIG_SPI = 0x01,
        CONFIG_SVID = 0x02,
        CONFIG_RS232 = 0x03,
    }

    public enum UI_TYPE
    {
        TextBox_Type = 0,
        ComboBox_Type,
        CheckBox_Type
    }

    public enum VERSION_CONTROL
    {
        VERSION_CONTROL_01_00_00,
        VERSION_CONTROL_02_00_00,
        VERSION_CONTROL_02_00_03
    }

    public enum ASSEMBLY_TYPE
    {
        SHELL,
        EM,
        DM,
        SFL,
        DEM,
        OCE
    }
    #endregion

    #region 总线属性
    public class ComboboxRoad
    {
        public int ID { set; get; }
        public UInt16 Code { set; get; }
        public string Info { set; get; }
    }

    public class Options : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

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
                    OnPropertyChanged("data");
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

        private double m_MinValue;
        public double minvalue
        {
            get { return m_MinValue; }
            set { m_MinValue = value; }
        }

        private double m_MaxValue;
        public double maxvalue
        {
            get { return m_MaxValue; }
            set { m_MaxValue = value; }
        }

        private bool m_bEdit;
        public bool bedit
        {
            get { return m_bEdit; }
            set
            {
                m_bEdit = value;
                OnPropertyChanged("bedit");
            }
        }

        private bool m_bError;
        public bool berror
        {
            get { return m_bError; }
            set
            {
                m_bError = value;
                OnPropertyChanged("berror");
            }
        }

        private bool m_bRange;
        public bool brange
        {
            get { return m_bRange; }
            set
            {
                m_bRange = value;
                OnPropertyChanged("brange");
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
                    OnPropertyChanged("sphydata");
                }
            }
        }

        private string m_sDeviceName;
        public string sdevicename
        {
            get { return m_sDeviceName; }
            set
            {
                m_sDeviceName = value;
                OnPropertyChanged("sdevicename");
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
                    OnPropertyChanged("bcheck");
                }
            }
        }

        #region Combobox对象
        private ComboboxRoad m_selectLocation = new ComboboxRoad();
        public ComboboxRoad SelectLocation
        {
            get { return m_selectLocation; }
            set
            {
                m_selectLocation = value;
                if (this.PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("SelectLocation"));
            }
        }

        private ObservableCollection<ComboboxRoad> m_locationSource = new ObservableCollection<ComboboxRoad>();
        public ObservableCollection<ComboboxRoad> LocationSource
        {
            get { return m_locationSource; }
            set
            {
                m_locationSource = value;
                if (this.PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("LocationSource"));
            }
        }
        #endregion
    }

    public class BusOptions : INotifyPropertyChanged
    {
        #region GUID definition, used in Communication Layer to identify which Options
        public const UInt32 BusOptionsElement = 0x00100000;
        public const UInt32 I2CBusOptionsElement = 0x00100000;
        public const UInt32 ConnectPort_GUID = I2CBusOptionsElement + 0x0000;
        public const UInt32 I2CFrequency_GUID = I2CBusOptionsElement + 0x0001;
        public const UInt32 I2CAddress_GUID = I2CBusOptionsElement + 0x0002;
        public const UInt32 I2CPECMODE_GUID = I2CBusOptionsElement + 0x0003;
        public const UInt32 I2C2Address_GUID = I2CBusOptionsElement + 0x0004;
        public const UInt32 I2C2PECMODE_GUID = I2CBusOptionsElement + 0x0005;

        public const UInt32 SPIBusOptionsElement = 0x00100020;
        public const UInt32 SPIConnectPort_GUID = I2CBusOptionsElement + 0x0000;
        public const UInt32 SPIBaudRate_GUID = SPIBusOptionsElement + 0x0001;
        public const UInt32 SPISSPolarity_GUID = SPIBusOptionsElement + 0x0002;
        public const UInt32 SPIBitOrder_GUID = SPIBusOptionsElement + 0x0003;
        public const UInt32 SPIPolarity_GUID = SPIBusOptionsElement + 0x0004;
        public const UInt32 SPIPhase_GUID = SPIBusOptionsElement + 0x0005;
        public const UInt32 SPIWire_GUID = SPIBusOptionsElement + 0x0006;

        public const UInt32 RS232BusOptionsElement = 0x00100040;
        public const UInt32 RS232ConnectPort_GUID = RS232BusOptionsElement + 0x0000;
        public const UInt32 RS232BaudRate_GUID = RS232BusOptionsElement + 0x0001;
        public const UInt32 RS232DataBits_GUID = RS232BusOptionsElement + 0x0002;
        public const UInt32 RS232Stopbit_GUID = RS232BusOptionsElement + 0x0003;
        public const UInt32 RS232Parity_GUID = RS232BusOptionsElement + 0x0004;

        public const UInt32 SVIDBusOptionsElement = 0x00100060;
        public const UInt32 SVIDI2CFrequency_GUID = SVIDBusOptionsElement + 0x01;
        public const UInt32 SVIDI2CAddress_GUID = SVIDBusOptionsElement + 0x02;
        public const UInt32 SVIDBaudRate_GUID = SVIDBusOptionsElement + 0x0003;
        public const UInt32 SVIDDataBits_GUID = SVIDBusOptionsElement + 0x0004;
        public const UInt32 SVIDStopbit_GUID = SVIDBusOptionsElement + 0x0005;
        public const UInt32 SVIDParity_GUID = SVIDBusOptionsElement + 0x0006;
        #endregion

        private DBManage m_DB_Manager = null;
        public DBManage db_Manager
        {
            get { return m_DB_Manager; }
            set { m_DB_Manager = value; }
        }

        private ObservableCollection<Options> m_OptionsList = new ObservableCollection<Options>();
        public ObservableCollection<Options> optionsList
        {
            get { return m_OptionsList; }
            set { m_OptionsList = value; }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private bool m_deviceischeck;
        public bool DeviceIsCheck
        {
            get { return m_deviceischeck; }
            set
            {
                m_deviceischeck = value;
                OnPropertyChanged("DeviceIsCheck");
            }
        }

        private BUS_TYPE m_bustype;
        public BUS_TYPE BusType
        {
            get { return m_bustype; }
            set
            {
                m_bustype = value;
                OnPropertyChanged("BusType");
            }
        }

        public int DeviceIndex { get; set; }

        private string m_devicename;
        public string DeviceName
        {
            get { return String.Format("{0} Connection Setting", m_devicename); }
            set
            {
                m_devicename = value;
                OnPropertyChanged("DeviceName");
            }
        }

        public Options GetOptionsByGuid(UInt32 guid)
        {
            foreach (Options op in optionsList)
            {
                if (op.guid.Equals(guid))
                    return op;
            }
            return null;
        }

        public string Name { get; set; }

        public BusOptions()
        {
            DeviceIsCheck = true;
            BusType = 0;
            DeviceIndex = 0;
            DeviceName = null;
            Name = null;
            m_DB_Manager = new DBManage(this);
        }
    }
    #endregion

    #region 芯片信息
    public class DeviceInfor : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private List<UInt32> m_PreType;
        public List<UInt32> pretype
        {
            get { return m_PreType; }
            set { m_PreType = value; }
        }

        private int m_Mode;   //ID:784
        public int mode
        {
            get { return m_Mode; }
            set
            {
                m_Mode = value;
                OnPropertyChanged("mode");
            }
        }

        private int m_index;
        public int index
        {
            get { return m_index; }
            set
            {
                m_index = value;
                OnPropertyChanged("index");
            }
        }

        private int m_status;
        public int status
        {
            get { return m_status; }
            set
            {
                m_status = value;
                OnPropertyChanged("status");
            }
        }

        private int m_type;
        public int type
        {
            get { return m_type; }
            set
            {
                m_type = value;
                OnPropertyChanged("type");
            }
        }

        private string m_oce_type;
        public string oce_type
        {
            get { return m_oce_type; }
            set
            {
                m_oce_type = value;
                OnPropertyChanged("oce_type");
            }
        }

        private int m_hw_version;
        public int hwversion
        {
            get { return m_hw_version; }
            set
            {
                m_hw_version = value;
                OnPropertyChanged("hwversion");
            }
        }

        private int m_hw_sub_version;
        public int hwsubversion
        {
            get { return m_hw_sub_version; }
            set
            {
                m_hw_sub_version = value;
                OnPropertyChanged("hwsubversion");
            }
        }

        private string m_shw_version;
        public string shwversion
        {
            get { return m_shw_version; }
            set
            {
                m_shw_version = value;
                OnPropertyChanged("shwversion");
            }
        }

        private string m_ate_version;  //ID:784
        public string ateversion
        {
            get { return m_ate_version; }
            set
            {
                m_ate_version = value;
                OnPropertyChanged("ateversion");
            }
        }

        private string m_fw_version;  //ID:784
        public string fwversion
        {
            get { return m_fw_version; }
            set
            {
                m_fw_version = value;
                OnPropertyChanged("fwversion");
            }
        }
    }
    #endregion

    #region 数据集合操作
    public class ParamListContainer
    {
        private AsyncObservableCollection<ParamContainer> m_DeviceParameterList_Container = new AsyncObservableCollection<ParamContainer>();
        public AsyncObservableCollection<ParamContainer> deviceparameterlistcontainer
        {
            get { return m_DeviceParameterList_Container; }
            set { m_DeviceParameterList_Container = value; }
        }

        public ParamContainer GetParameterListByName(string name)
        {
            foreach (ParamContainer paramlist in deviceparameterlistcontainer)
            {
                if (paramlist.listname.Equals(name))
                    return paramlist;
            }
            return null;
        }

        public ParamContainer GetParameterListByGuid(UInt32 guid)
        {
            foreach (ParamContainer paramlist in deviceparameterlistcontainer)
            {
                if (paramlist.guid.Equals(guid))
                    return paramlist;
            }
            return null;
        }

        public void AddParameterList(ParamContainer list)
        {
            deviceparameterlistcontainer.Add(list);
        }

        public int Destroy()
        {
            if (deviceparameterlistcontainer.Count() > 0)
                deviceparameterlistcontainer.Clear();

            return 0;

        }
    }

    public class ParamContainer
    {
        private string m_list_name;
        public string listname
        {
            get { return m_list_name; }
            set { m_list_name = value; }
        }

        private UInt32 m_Guid;
        public UInt32 guid
        {
            get { return m_Guid; }
            set { m_Guid = value; }
        }

        public ParamContainer()
        {
        }

        //行信息定义
        private AsyncObservableCollection<Parameter> m_parameter_list = new AsyncObservableCollection<Parameter>();
        public AsyncObservableCollection<Parameter> parameterlist
        {
            get { return m_parameter_list; }
            set { m_parameter_list = value; }
        }

        public Parameter GetParameterByGuid(UInt32 guid)
        {
            foreach (Parameter param in parameterlist)
            {
                if (param.guid.Equals(guid))
                    return param;
            }
            return null;
        }
    }
    #endregion

    #region 数据结构
    #region 硬件模式寄存器结构定义
    public class COBRA_HWMode_Reg
    {
        public UInt16 val;
        public UInt32 wval;
        public UInt32 err;
        public UInt32 cid;
    }
    #endregion

    #region 软件模式数据存储结构定义
    public class MemoryControl
    {
        public UInt32 startAddress;
        public UInt32 endAddress;
        public UInt32 totalSize;
        public byte[] buffer;

        public MemoryControl()
        {
        }
        public void Update()
        {
            buffer = new byte[totalSize];
        }
    }
    public class TSMBbuffer
    {
        public ushort length;
        public byte[] bdata; //byte0:type(op,efuse) byte1:reg addr byte2~31:data

        public TSMBbuffer()
        {
            length = 2;
            bdata = new byte[32];
        }
    };
    #endregion

    public class Reg
    {
        private UInt16 m_Address;
        public UInt16 address
        {
            get { return m_Address; }
            set { m_Address = value; }
        }
        private UInt32 m_U32Address;
        public UInt32 u32Address
        {
            get { return m_U32Address; }
            set { m_U32Address = value; }
        }

        private UInt16 m_StartBit;
        public UInt16 startbit
        {
            get { return m_StartBit; }
            set { m_StartBit = value; }
        }

        private UInt16 m_BitsNumber;
        public UInt16 bitsnumber
        {
            get { return m_BitsNumber; }
            set { m_BitsNumber = value; }
        }
    };

    public class SFL : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private Parameter m_Parent;
        public Parameter parent
        {
            get { return m_Parent; }
            set { m_Parent = value; }
        }

        private string m_SFLName;
        public string sflname
        {
            get { return m_SFLName; }
            set { m_SFLName = value; }
        }

        private Hashtable m_node_table = new Hashtable();
        public Hashtable nodetable
        {
            get { return m_node_table; }
            set { m_node_table = value; }
        }
    }

    public class Parameter : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        //参数唯一标识符
        private UInt32 m_Guid;
        public UInt32 guid
        {
            get { return m_Guid; }
            set { m_Guid = value; }
        }

        //参数在Section参数列表中位置
        private Int32 m_SectionPos;
        public Int32 sectionpos
        {
            get { return m_SectionPos; }
            set { m_SectionPos = value; }
        }

        private Double m_Key;
        public Double key
        {
            get { return m_Key; }
            set { m_Key = value; }
        }

        private UInt16 m_SubSection;
        public UInt16 subsection
        {
            get { return m_SubSection; }
            set { m_SubSection = value; }
        }

        private UInt16 m_SubType;
        public UInt16 subtype
        {
            get { return m_SubType; }
            set { m_SubType = value; }
        }

        private double m_PhyRef;
        public double phyref
        {
            get { return m_PhyRef; }
            set { m_PhyRef = value; }
        }

        private double m_RegRef;
        public double regref
        {
            get { return m_RegRef; }
            set { m_RegRef = value; }
        }

        private double m_Offset;
        public double offset
        {
            get { return m_Offset; }
            set { m_Offset = value; }
        }

        /*(D151224)Francis, looks no used anymore
        private double m_MinValue;
        public double minvalue
        {
            get { return m_MinValue; }
            set { m_MinValue = value; }
        }

        private double m_MaxValue;
        public double maxvalue
        {
            get { return m_MaxValue; }
            set { m_MaxValue = value; }
        }
         */

        //(A151224)Francis, add for HexMin, HexMax, PhyMin, and PhyMax
        private Int64 m_dbHexMin;		//Support DWord in KALL17 project
        public Int64 dbHexMin
        {
            get { return m_dbHexMin; }
            set { m_dbHexMin = value; }
        }

        private Int64 m_dbHexMax;		//Support DWord in KALL17 project
        public Int64 dbHexMax
        {
            get { return m_dbHexMax; }
            set { m_dbHexMax = value; }
        }

        private double m_dbPhyMin;
        public double dbPhyMin
        {
            get { return m_dbPhyMin; }
            set
            {
                {
                    m_dbPhyMin = value;
                    OnPropertyChanged("dbPhyMin");
                }
            }
        }

        private double m_dbPhyMax;
        public double dbPhyMax
        {
            get { return m_dbPhyMax; }
            set
            {

                {
                    m_dbPhyMax = value;
                    OnPropertyChanged("dbPhyMax");
                }
            }
        }


        private Dictionary<string, Reg> m_RegList = new Dictionary<string, Reg>();
        public Dictionary<string, Reg> reglist
        {
            get { return m_RegList; }
            set { m_RegList = value; }
        }

        private double m_PhyData;
        public double phydata
        {
            get { return m_PhyData; }
            set
            {
                //if (m_PhyData != value)
                {
                    m_PhyData = value;
                    OnPropertyChanged("phydata");
                }
            }
        }

        private UInt16 m_HexData;
        public UInt16 hexdata
        {
            get { return m_HexData; }
            set { m_HexData = value; }
        }

        private UInt32 m_U32HexData;
        public UInt32 u32hexdata
        {
            get { return m_U32HexData; }
            set
            {
                m_U32HexData = value;
                OnPropertyChanged("u32hexdata");
            }
        }

        private string m_sPhyData;
        public string sphydata
        {
            get { return m_sPhyData; }
            set
            {
                m_sPhyData = value;
                OnPropertyChanged("sphydata");
            }
        }

        //Add 20170301
        private bool m_bShow = true; //用于设定此参数是否需要在UI上隐藏之功能
        public bool bShow
        {
            get { return m_bShow; }
            set
            {
                m_bShow = value;
                OnPropertyChanged("bShow");
            }
        }

        //Add 20220126
        private string m_sMessage = string.Empty; //用于传输参数详细信息
        public string sMessage
        {
            get { return m_sMessage; }
            set
            {
                m_sMessage = value;
                OnPropertyChanged("sMessage");
            }
        }

        private TSMBbuffer m_TsmbBuffer = new TSMBbuffer();
        public TSMBbuffer tsmbBuffer
        {
            get { return m_TsmbBuffer; }
            set { m_TsmbBuffer = value; }
        }

        private Dictionary<string, SFL> m_SFLList = new Dictionary<string, SFL>();
        public Dictionary<string, SFL> sfllist
        {
            get { return m_SFLList; }
            set { m_SFLList = value; }
        }

        private AsyncObservableCollection<string> m_ItemList = new AsyncObservableCollection<string>();
        public AsyncObservableCollection<string> itemlist
        {
            get { return m_ItemList; }
            set
            {
                m_ItemList = value;
                OnPropertyChanged("itemlist");
            }
        }

        private UInt32 m_ErrorCode;
        public UInt32 errorcode
        {
            get { return m_ErrorCode; }
            set
            {
                m_ErrorCode = value;
                OnPropertyChanged("errorcode");
            }
        }

        /// <summary>
        /// 通过参数名检查节点是否存在
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public string GetXElementValueByName(XElement node, string name)
        {
            if (node.Element(name) == null) return null;
            else if (String.IsNullOrEmpty(node.Element(name).Value)) return null;
            else return node.Element(name).Value;
        }

        /// <summary>
        /// 通过参数属性名检查节点是否存在
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public string GetXElementValueByAttribute(XElement node, string name)
        {
            if (node.Attribute(name) == null) return null;
            else if (String.IsNullOrEmpty(node.Attribute(name).Value)) return null;
            else return node.Attribute(name).Value;
        }
    }

    public class VersionInfo : MarshalByRefObject
    {
        public VersionInfo(string name, string des, Version ver, ASSEMBLY_TYPE type, UInt32 errcode = LibErrorCode.IDS_ERR_SUCCESSFUL)
        {
            m_assembly_name = name;
            m_assembly_projectcode = des;
            m_assembly_ver = ver;
            m_assembly_Type = type;
            m_ErrorCode = errcode;
            switch (type)
            {
                case ASSEMBLY_TYPE.OCE:
                    m_assembly_path = FolderMap.m_extensions_folder;
                    break;
                case ASSEMBLY_TYPE.SFL:
                    m_assembly_path = FolderMap.m_standard_feature_library_folder;
                    break;
                default:
                    m_assembly_path = FolderMap.m_root_folder;
                    break;
            }
            Application.Current.Dispatcher.BeginInvoke((Action)delegate
            {
                LibInfor.Add(this);
            });
        }

        Version m_assembly_ver;
        public Version Assembly_ver
        {
            get { return m_assembly_ver; }
            set { m_assembly_ver = value; }
        }

        string m_assembly_name;
        public string Assembly_name
        {
            get { return m_assembly_name; }
            set { m_assembly_name = value; }
        }

        ASSEMBLY_TYPE m_assembly_Type;
        public ASSEMBLY_TYPE Assembly_Type
        {
            get { return m_assembly_Type; }
            set { m_assembly_Type = value; }
        }

        string m_assembly_projectcode;
        public string Assembly_ProjectCode
        {
            get { return m_assembly_projectcode; }
            set { m_assembly_projectcode = value; }
        }

        string m_assembly_path;
        public string Assembly_Path
        {
            get { return m_assembly_path; }
            set { m_assembly_path = value; }
        }

        UInt32 m_ErrorCode;
        public UInt32 ErrorCode
        {
            get { return m_ErrorCode; }
            set { m_ErrorCode = value; }
        }
    }
    #endregion
}
