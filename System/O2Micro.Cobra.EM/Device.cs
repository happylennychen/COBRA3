using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Xml.Linq;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using System.Windows;
using System.Threading;
using System.Xml;
using System.Windows.Data;
using O2Micro.Cobra.AutoMationTest;
using O2Micro.Cobra.DM;
using O2Micro.Cobra.Common;

namespace O2Micro.Cobra.EM
{
    [Serializable]
    public class Device : IComparable, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private bool m_bSimulation = false;
        public bool bOnce
        {
            get { return m_bSimulation; }
            set { m_bSimulation = value; }
        }

        private uint m_iATMTestRepeatTimes = 1;
        public uint iATMTestRepeatTimes
        {
            get
            {
                try
                {
                    return (uint)m_busoptions.GetATMElementbyGuid(AutomationElement.GUIDATMTestRepeatTimes).dbValue;
                }
                catch (System.Exception ex)
                {
                    return 1;
                }
            }
            set { m_iATMTestRepeatTimes = value; }
        }

        public bool bATMTestStart
        {
            get
            {
                try
                {
                    return m_busoptions.GetATMElementbyGuid(AutomationElement.GUIDATMTestStart).dbValue > 0 ? true : false;
                }
                catch (System.Exception ex)
                {
                    return false;
                }
            }
        }

        #region IComparable 成员
        public int CompareTo(object obj)
        {
            if (obj is Device)
            {
                Device device = obj as Device;
                return this.index.CompareTo(device.index);
            }
            throw new NotImplementedException("obj is not a Student!");
        }
        #endregion

        #region MEF接口定义
        [Import(typeof(IServices))]
        public IServices SFLUserControl { get; set; }
        #endregion

        #region 属性定义
        private bool m_bBusy;
        public bool bBusy
        {
            set
            {
                if (m_bBusy != value)
                {
                    m_bBusy = value;
                    OnPropertyChanged("bBusy");
                }
            }
            get { return m_bBusy; }
        }

        private bool m_bDestroyed;
        public bool bDestroyed
        {
            set
            {
                m_bDestroyed = value;
                OnPropertyChanged("bDestroyed");                
            }
            get { return m_bDestroyed; }
        }

        private bool m_bControl = false;
        public bool bControl
        {
            get { return m_bControl; }
            set { m_bControl = value; }
        }

        //Francis
        private bool m_bSBSReady;
        public bool bSBSReady
        {
            set { m_bSBSReady = value; }
            get { return m_bSBSReady; }
        }

        private int m_Index;
        public int index
        {
            set { m_Index = value; }
            get { return m_Index; }
        }

        private string m_Name;
        public string name
        {
            set { m_Name = value; }
            get { return m_Name; }
        }

        private GeneralMessage m_GM = new GeneralMessage();
        public GeneralMessage gm
        {
            get { return m_GM; }
            set
            {
                m_GM.setvalue((GeneralMessage)value);
            }
        }

        private TASKMessage m_Msg = new TASKMessage();
        public TASKMessage msg
        {
            get { return m_Msg; }
            set { m_Msg = value; }
        }

        private BusOptions m_busoptions = null;
        #endregion

        #region 部件定义
        private DeviceInfor m_Device_Infor = new DeviceInfor();
        public DeviceInfor device_infor
        {
            get { return m_Device_Infor; }
            set { m_Device_Infor = value; }
        }

        private BackgroundWorker m_Bgworker = new BackgroundWorker();
        public BackgroundWorker bgworker
        {
            get { return m_Bgworker; }
            set { m_Bgworker = value; }
        }

        private DMDataManage m_device_dm = new DMDataManage();
        public DMDataManage device_dm
        {
            get { return m_device_dm; }
            set { m_device_dm = value; }
        }

        private List<WorkPanelItem> m_device_panellist = new List<WorkPanelItem>();
        #endregion

        #region 设备基础定义
        public Device(string name)
        {
            m_Name = name;
            m_bBusy = false;
            m_bDestroyed = false;
            m_bSBSReady = false;		//Francis
            m_Index = Registry.GetBusOptionsByName(name).DeviceIndex;

            PropertyChanged += new PropertyChangedEventHandler(Device_PropertyChanged);

            #region 构建设备信息
            m_Device_Infor.index = index + 1;
            m_Device_Infor.mode = EMExtensionManage.chipMode; //0:DFE 1:FW   //ID:784
            m_Device_Infor.status = -1;
            m_Device_Infor.type = -1;
            m_Device_Infor.hwversion = -1;
            m_Device_Infor.hwsubversion = -1;
            m_Device_Infor.shwversion = String.Empty;
            m_Device_Infor.ateversion = String.Empty; //ID:784
            m_Device_Infor.fwversion = String.Empty;  //ID:784

            bgworker.WorkerReportsProgress = true;
            bgworker.WorkerSupportsCancellation = true;
            bgworker.ProgressChanged += bgworker_ProgressChanged;
            bgworker.RunWorkerCompleted += bgworker_RunWorkerCompleted;
            bgworker.DoWork += bgworker_DoWork;
            #endregion

            #region 构建DDM数据结构
            m_busoptions = Registry.GetBusOptionsByindexInListView(index);
            m_busoptions.optionsList.Clear();
            m_device_dm.Init(ref m_busoptions);

            BusOptionListCollectionView GroupedCustomers = new BusOptionListCollectionView(m_busoptions.optionsList);
            GroupedCustomers.GroupDescriptions.Add(new PropertyGroupDescription("catalog"));
            GroupedCustomers.order = m_Index;
            if (m_Index > Registry.busoptionslist_collectionview.Count)
                Registry.busoptionslist_collectionview.Add(GroupedCustomers);
            else
                Registry.busoptionslist_collectionview.Insert(m_Index,GroupedCustomers);
            #endregion

            #region 构建SFL
            List<BtnPanelLink> BtnPanelList = EMExtensionManage.m_EM_DevicesManage.btnPanelList;
            for (int i = 0; i < BtnPanelList.Count; i++)
            {
                //实例化SFL
                var catalog = new DirectoryCatalog(FolderMap.m_standard_feature_library_folder + BtnPanelList[i].btnname);
                var container = new CompositionContainer(catalog);
                container.ComposeParts(this);

                WorkPanelItem panelitem = new WorkPanelItem();
                switch(EMExtensionManage.version_ctl)
                {
                    case VERSION_CONTROL.VERSION_CONTROL_02_00_00:
                        panelitem.item = SFLUserControl.Insert(this, BtnPanelList[i].btnname);
                        break;
                    case VERSION_CONTROL.VERSION_CONTROL_02_00_03:
                        panelitem.item = SFLUserControl.Insert(this, BtnPanelList[i].btnlabel);
                        break;
                    default:
                        panelitem.item = SFLUserControl.Insert(this, BtnPanelList[i].btnlabel);
                        break;
                }
                panelitem.itemname = name;


                if (BtnPanelList[i].workpaneltabitems.Count < index)
                    BtnPanelList[i].workpaneltabitems.Add(panelitem);
                else
                    BtnPanelList[i].workpaneltabitems.Insert(index, panelitem);


                if (m_device_panellist.Count < index)
                    m_device_panellist.Add(panelitem);
                else
                    m_device_panellist.Insert(index, panelitem);
            }
            #endregion
        }

        void Device_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "bBusy":
                    if (!bControl)
                        bOnce = false;
                    break;
            }
            return;
        }

        public void Destory()
        {
            m_device_dm.Destory();

            #region 销毁SFL
            bDestroyed = true;
            #endregion

            for (int i = 0; i < m_device_panellist.Count; i++)
            {
                for (int k = 0; k < EMExtensionManage.m_EM_DevicesManage.btnPanelList.Count; k++)
                {
                    BtnPanelLink btnpanel = EMExtensionManage.m_EM_DevicesManage.btnPanelList[k];
                    for (int m = 0; m < btnpanel.workpaneltabitems.Count; m++)
                    {
                        WorkPanelItem panel = btnpanel.workpaneltabitems[m];
                        if (Object.ReferenceEquals(m_device_panellist[i], panel) == true)
                        {
                            EMExtensionManage.m_EM_DevicesManage.btnPanelList[k].workpaneltabitems.Remove(panel);
                        }
                    }
                }
            }
            foreach (BusOptionListCollectionView lv in Registry.busoptionslist_collectionview)
            {
                if (lv.order == m_Index)
                {
                    Registry.busoptionslist_collectionview.Remove(lv);
                    break;
                }
            }
        }
        #endregion

        #region 设备数据集合
        public AsyncObservableCollection<Parameter> GetObjects(string sType)
        {
            return m_device_dm.GetObjects(sType);
        }

        public ParamContainer GetParamLists(string sType)
        {
            return m_device_dm.GetParamLists(sType);
        }

        public XmlNodeList GetUINodeList(string sType)
        {
            var panellink = from x in EMExtensionManage.m_EM_DevicesManage.btnPanelList where (x.btnlabel == sType || x.btnname == sType) select x;
            foreach (BtnPanelLink link in panellink)
            {
                if (link != null)
                    return link.nodelist;
            }
            return null;
        }
        #endregion

        #region 设备工作线程定义
        private void bgworker_DoWork(object sender, DoWorkEventArgs e)
        {
            msg.gm.controls = name + ": " + "Device Thread";
            msg.gm.deviceindex = index + 1;
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            switch (msg.task)
            {
                case TM.TM_READ:
                    {
                        msg.gm.level = 2;
                        msg.gm.message = "Read Device";
                        gm = msg.gm;
                        msg.bgworker.ReportProgress(msg.percent, msg.gm.message);
                        ret = Read(ref m_Msg);
                        break;
                    }
                case TM.TM_WRITE:
                    {
                        msg.gm.level = 2;
                        msg.gm.message = "Write Device";
                        gm = msg.gm;
                        msg.bgworker.ReportProgress(msg.percent, msg.gm.message);
                        ret = Write(ref m_Msg);
                        break;
                    }
                case TM.TM_BITOPERATION:
                    {
                        msg.gm.level = 2;
                        msg.gm.message = "Bit Operation";
                        gm = msg.gm;
                        msg.bgworker.ReportProgress(msg.percent, msg.gm.message);
                        ret = BitOperation(ref m_Msg);
                        break;
                    }
                case TM.TM_BLOCK_ERASE:
                    {
                        msg.gm.level = 2;
                        msg.gm.message = "Erase Device";
                        gm = msg.gm;
                        msg.bgworker.ReportProgress(msg.percent, msg.gm.message);
                        ret = EraseEEPROM(ref m_Msg);
                        break;
                    }
                case TM.TM_BLOCK_MAP:
                    {
                        msg.gm.level = 2;
                        msg.gm.message = "Map Device Register";
                        gm = msg.gm;
                        msg.bgworker.ReportProgress(msg.percent, msg.gm.message);
                        ret = BlockMap(ref m_Msg);
                        break;
                    }
                case TM.TM_COMMAND:
                    {
                        msg.gm.level = 2;
                        msg.gm.message = "Command Operation";
                        gm = msg.gm;
                        msg.bgworker.ReportProgress(msg.percent, msg.gm.message);
                        ret = Command(ref m_Msg);
                        break;
                    }
                case TM.TM_CONVERT_PHYSICALTOHEX:
                    {
                        msg.gm.level = 1;
                        msg.gm.message = "Convert Physical To Hex";
                        gm = msg.gm;
                        msg.bgworker.ReportProgress(msg.percent, msg.gm.message);
                        ret = ConvertPhysicalToHex(ref m_Msg);
                        break;
                    }
                case TM.TM_CONVERT_HEXTOPHYSICAL:
                    {
                        msg.gm.level = 1;
                        msg.gm.message = "Convert Hex To Physical ";
                        gm = msg.gm;
                        msg.bgworker.ReportProgress(msg.percent, msg.gm.message);
                        ret = ConvertHexToPhysical(ref m_Msg);
                        break;
                    }
                case TM.TM_SPEICAL_GETSYSTEMINFOR:
                    {
                        msg.gm.level = 2;
                        msg.gm.message = "Get System Information";
                        gm = msg.gm;
                        msg.bgworker.ReportProgress(msg.percent, msg.gm.message);
                        ret = GetSystemInfor(ref m_Msg);
                        break;
                    }
                case TM.TM_SPEICAL_GETDEVICEINFOR:
                    {
                        msg.gm.level = 2;
                        msg.gm.message = "Get Device Information";
                        gm = msg.gm;
                        msg.bgworker.ReportProgress(msg.percent, msg.gm.message);
                        ret = GetDeviceInfor();
                        break;
                    }
                case TM.TM_SPEICAL_GETREGISTEINFOR:
                    {
                        msg.gm.level = 2;
                        msg.gm.message = "Get Registe Information";
                        gm = msg.gm;
                        msg.bgworker.ReportProgress(msg.percent, msg.gm.message);
                        ret = GetRegisteInfor(ref m_Msg);
                        break;
                    }
                default:
                    break;
            }
            msg.errorcode = ret;
            msg.gm.message = LibErrorCode.GetErrorDescription(ret);
            gm = msg.gm;
        }

        private void bgworker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            try
            {
                if (bATMTestStart)
                    msg.errorcode = LibErrorCode.IDS_ERR_SUCCESSFUL;
            }
            catch (Exception e1)
            {

            }
            AutoMationTest.AutoMationTest.CompletedWork(msg);
            msg.controlmsg.bshow = false;
            msg.controlreq = COMMON_CONTROL.COMMON_CONTROL_WAITTING;
        }

        private void bgworker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            msg.controlmsg.bshow = true;
            msg.controlmsg.percent = e.ProgressPercentage;
            msg.controlmsg.message = e.UserState.ToString();
            msg.controlreq = COMMON_CONTROL.COMMON_CONTROL_WAITTING;
        }
        #endregion

        #region 设备功能操作
        public UInt32 GetDeviceInfor()
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            ret = m_device_dm.GetDeviceInfor(ref m_Device_Infor);
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                m_Device_Infor.index = index + 1;
                m_Device_Infor.status = -1;
                m_Device_Infor.type = -1;
                m_Device_Infor.hwversion = -1;
                m_Device_Infor.hwsubversion = -1;
                m_Device_Infor.shwversion = String.Empty;
                m_Device_Infor.ateversion = String.Empty;  //ID：784
                m_Device_Infor.fwversion = String.Empty;   //ID：784
            }
            return ret;
        }

        public bool EnumerateInterface()
        {
            return m_device_dm.EnumerateInterface();
        }

        public bool CreateInterface()
        {
            bool ret = m_device_dm.CreateInterface();

            //Leon add for bug 16036
            if (DBManager.supportdb == true)
            {
                if (ret == true)
                    SharedAPI.SaveBusOptionsToDB(m_busoptions);
            }
            //Leon add for bug 16036

            return ret;
        }

        public void UpdataDEMParameterList(Parameter p)
        {
            m_device_dm.UpdataDEMParameterList(p);
        }

        public UInt32 AccessDevice(ref TASKMessage msg)
        {
            if (!bOnce)
            {
                bOnce = true;
                try
                {
                    if (bATMTestStart) //has Start Node
                    {
                        AutoMationTest.AutoMationTest.ParseParamByRegType(msg.task_parameterlist);
                        try
                        {
                            #region create log for AMT and Com
                            if (msg.gm.sflname.Equals("DeviceConfig") || msg.gm.sflname.Equals("EfuseConfig") || msg.gm.sflname.Equals("EFUSE Config"))  //only for Config SFL
                            {
                                #region use CobraLog
                                string str = "";
                                if (msg.gm.controls != null)
                                    str = String.Concat(msg.gm.controls.Split(' '));
                                else
                                    str = "Unknown";
                                str = msg.gm.sflname + "_" + str + "_";

                                    str += DateTime.Now.Year.ToString("D4");
                                    str += DateTime.Now.Month.ToString("D2");
                                    str += DateTime.Now.Day.ToString("D2");
                                    str += DateTime.Now.Hour.ToString("D2");
                                    str += DateTime.Now.Minute.ToString("D2");
                                    str += DateTime.Now.Second.ToString("D2");
                                    str += DateTime.Now.Millisecond.ToString("D2");
                                    str += "Log" + ".csv.tmp";
                                    AutomationTestLog.CreateLogFile(str);//*/
                                    if (!CommunicationLog.CreateComLogFile(str))
                                        return LibErrorCode.IDS_ERR_SECTION_CANNOT_CREATE_FOLDER_COM;
                                    AutoMationTest.AutoMationTest.ResetAllErrorCounter();
                                    #endregion
                                    #region use DB

                                    string timestamp = DateTime.Now.ToString();
                                    int log_id = -1;
                                    int ret = DBManager.NewLog("AMT", "AMT Log", timestamp, ref log_id);
                                    if (ret != 0)
                                        MessageBox.Show("New AMT log Failed!");

                                #endregion
                            }
                            #endregion
                            ((ISFLLib)msg.owner).Simulation();
                            msg.errorcode = LibErrorCode.IDS_ERR_SECTION_SIMULATION_START;
                            return LibErrorCode.IDS_ERR_SUCCESSFUL;
                        }
                        catch
                        {
                        }

                    }
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message);
                }
            }
            //Normal mode starts here
            this.msg = msg;
            msg.bgworker = bgworker;
            if (bgworker.IsBusy) return LibErrorCode.IDS_ERR_EM_THREAD_BKWORKER_BUSY;
            bgworker.RunWorkerAsync();

            return LibErrorCode.IDS_ERR_SUCCESSFUL;
        }

        private UInt32 EraseEEPROM(ref TASKMessage msg)
        {
            return m_device_dm.Erase(ref msg);
        }

        private UInt32 BlockMap(ref TASKMessage msg)
        {
            return m_device_dm.BlockMap(ref msg);
        }

        private UInt32 Command(ref TASKMessage msg)
        {
            return m_device_dm.Command(ref msg);
        }

        private UInt32 Read(ref TASKMessage msg)
        {
            return m_device_dm.Read(ref msg);
        }

        private UInt32 Write(ref TASKMessage msg)
        {
            return m_device_dm.Write(ref msg);
        }

        private UInt32 ConvertHexToPhysical(ref TASKMessage m_Msg)
        {
            return m_device_dm.ConvertHexToPhysical(ref m_Msg);
        }

        private UInt32 ConvertPhysicalToHex(ref TASKMessage m_Msg)
        {
            return m_device_dm.ConvertPhysicalToHex(ref m_Msg);
        }

        private UInt32 GetSystemInfor(ref TASKMessage m_Msg)
        {
            return m_device_dm.GetSystemInfor(ref m_Msg);
        }

        private UInt32 GetRegisteInfor(ref TASKMessage m_Msg)
        {
            return m_device_dm.GetRegisteInfor(ref m_Msg);
        }

        public UInt32 BitOperation(ref TASKMessage m_Msg)
        {
            return m_device_dm.BitOperation(ref m_Msg);
        }
        #endregion
    }
}
