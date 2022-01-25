using System;
using System.Text;
using System.Xml;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Windows.Controls;
using System.AddIn.Hosting;
using System.Windows;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel;
using System.Xaml;
using Cobra.Common;
using Cobra.DM;

namespace Cobra.EM
{
    public class EMDevicesManage : INotifyPropertyChanged
    {
        #region Extension信息
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private List<UInt32> m_Idevicetype = new List<UInt32>();
        public List<UInt32> idevicetype
        {
            get { return m_Idevicetype; }
            set { m_Idevicetype = value; }
        }

        private string m_bustype;
        public string bustype
        {
            get { return "Communication: " + m_bustype; }
            set { 
                    m_bustype = value; 
                    OnPropertyChanged("bustype"); 
                }
        }

        private string m_devicetype;
        public string devicetype
        {
            get { return "Device Type: " + m_devicetype; }
            set { 
                    m_devicetype = value;
                    OnPropertyChanged("devicetype"); 
                }
        }

        private string m_libname;
        public string libname
        {
            get { return m_libname + ".dll"; }
            set { m_libname = value; }
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
        #endregion

        #region BtnPanel关系管理
        private List<BtnPanelLink> m_btnPanelList = new List<BtnPanelLink>();
        public List<BtnPanelLink> btnPanelList
        {
            get { return m_btnPanelList; }
            set { value = m_btnPanelList; }
        }

        private bool CreateBtnPanelLinkList()
        {
            int         iId = 0;
            string      tmp = string.Empty;
            string[]    types = new string[]{};
            UInt32 utype = 0;
            XmlNodeList nodeList;

            idevicetype.Clear();
            
            XmlElement root = EMExtensionManage.m_extDescrip_xmlDoc.DocumentElement;
            if (root == null) return false;
            tmp = root.GetAttribute("AppVersion");
            //if (string.Compare(tmp, "2.00.00") != 0)
            if (tmp == string.Empty)
            {
                EMExtensionManage.version_ctl = VERSION_CONTROL.VERSION_CONTROL_01_00_00;
                return false;
            }
            else
            {
                EMExtensionManage.version_ctl = tmp.CompareTo("2.00.00") > 0 ? VERSION_CONTROL.VERSION_CONTROL_02_00_03 : VERSION_CONTROL.VERSION_CONTROL_02_00_00;
            }

            bustype = root.GetAttribute("bustype");
            Registry.SaveCurExtensionBusType(m_bustype);
            Registry.GetDeviceConnectSetting();

            try
            {
                EMExtensionManage.chipMode = int.Parse(root.GetAttribute("chipMode"));
            }
            catch (System.Exception ex)
            {
                EMExtensionManage.chipMode = 0;
            }
            devicetype = root.GetAttribute("chip");
            types = root.GetAttribute("chiptype").Split('|');
            foreach (string type in types)
            {
                if (!UInt32.TryParse(type, System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out utype))
                    return false;
                else
                    idevicetype.Add(utype);
            }

            libname = root.GetAttribute("libname");
            DMDataManage.libname = System.IO.Path.Combine(FolderMap.m_dem_library_folder,libname);

            nodeList = root.SelectSingleNode("descendant::Part[@Name = 'MainBtnList']").ChildNodes;
            foreach (XmlNode xn in nodeList)
            {
                XmlElement xe = (XmlElement)xn;
                BtnPanelLink link = new BtnPanelLink();

                link.btnname = xe.GetAttribute("Name");
                link.btnlabel = xe.GetAttribute("Label");
                link.panelname = xe.GetAttribute("PanelName");
                link.nodelist = xn.ChildNodes;
                link.id = iId;

                for (int i = 0; i < Registry.busoptionslistview.Count; i++)
                {
                    BtnPanelItem item = new BtnPanelItem();
                    item.btnlamp = false;
                    item.id = Registry.busoptionslistview[i].DeviceIndex + 1;

                    link.btnpanellampitems.Add(item);
                }
                switch (EMExtensionManage.version_ctl)
                {
                    case VERSION_CONTROL.VERSION_CONTROL_02_00_00:
                DMDataManage.m_SFLNames_list.Add(link.btnname);
                        break;
                    case VERSION_CONTROL.VERSION_CONTROL_02_00_03:
                DMDataManage.m_SFLNames_list.Add(link.btnlabel);
                        break;
                    default:
                        DMDataManage.m_SFLNames_list.Add(link.btnlabel);
                        break;
                }

                m_btnPanelList.Add(link);
                iId++;

                //Leon Issue 710
                string external = xe.GetAttribute("External");
                if (external != String.Empty)
                {
                    string[] externalsfls = external.Split(new char[] { '|' });
                    foreach(string sfl in externalsfls)
                    {
                        if (!DMDataManage.m_SFLNames_list.Contains(sfl))
                            DMDataManage.m_SFLNames_list.Add(sfl);
                    }
                }
                //Leon Issue 710
            }


            return true;
        }

        private void ClearBtnPanelLinkList()
        {
            m_btnPanelList.Clear();
        }

        public List<WorkPanelItem> GetWorkPanelTabItemsByBtnName(string btnname)
        {
            BtnPanelLink link = m_btnPanelList.Find(delegate(BtnPanelLink node)
            {
                return node.btnname.Equals(btnname);
            }
            );
            if (link != null) return link.workpaneltabitems;
            else return null;
        }

        public List<WorkPanelItem> GetWorkPanelTabItemsByBtnLabel(string btnlabel)  //Issue1374 Leon
        {
            BtnPanelLink link = m_btnPanelList.Find(delegate(BtnPanelLink node)
            {
                return node.btnlabel.Equals(btnlabel);
            }
            );
            if (link != null) return link.workpaneltabitems;
            else return null;
        }

        public List<WorkPanelItem> GetWorkPanelTabItemsByPanelName(string panelname)
        {
            BtnPanelLink link = m_btnPanelList.Find(delegate(BtnPanelLink node)
            {
                return node.btnname.Equals(panelname);
            }
            );
            if (link != null) return link.workpaneltabitems;
            else return null;
        }

        public List<WorkPanelItem> GetWorkPanelTabItemsByPanelID(int id)
        {
            BtnPanelLink link = m_btnPanelList.Find(delegate(BtnPanelLink node)
            {
                return node.id.Equals(id);
            }
             );
            if (link != null) return link.workpaneltabitems;
            else return null;
        }

        public string GetPanelNameByID(int id)
        {
            BtnPanelLink link = m_btnPanelList.Find(delegate(BtnPanelLink node)
            {
                return node.id.Equals(id);
            }
             );
            if (link != null) return link.panelname;
            else return null;
        }

        #endregion

        #region Devices信息管理
        private List<Device> m_device_list = new List<Device>();
        public List<Device> devicelist
        {
            get { return m_device_list; }
            set { value = m_device_list; }
        }

        private AsyncObservableCollection<DeviceInfor> m_deviceinfor_list = new AsyncObservableCollection<DeviceInfor>();
        public AsyncObservableCollection<DeviceInfor> deviceinforlist
        {
            get { return m_deviceinfor_list; }
            set { m_deviceinfor_list = value; }
        }

        private bool CreateDeviceList()
        {
            for (int i = 0; i < Registry.busoptionslistview.Count; i++)
            {
                Device device = new Device(Registry.busoptionslistview[i].Name);
                device.device_infor.pretype = idevicetype;
                device.device_infor.oce_type = m_devicetype;
                device.gm.PropertyChanged += new PropertyChangedEventHandler(gm_PropertyChanged);
                device.msg.PropertyChanged += new PropertyChangedEventHandler(msg_PropertyChanged);
                m_device_list.Add(device);
                m_deviceinfor_list.Add(device.device_infor);
            }
            return true;
        }

        void msg_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            return;
            //throw new NotImplementedException();
        }

        void gm_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            gm = (GeneralMessage)sender;
            if (gm.level == 2)
            {
                BtnPanelLink btnpanellink = btnPanelList.Find(delegate(BtnPanelLink o)
                {
                    return o.btnlabel.Equals(gm.sflname);
                }
                );
                if (btnpanellink == null) return;

                BtnPanelItem btnpanelitem = btnpanellink.GetBtnPanelLampItemByID(gm.deviceindex);
                if (btnpanelitem == null) return;

                btnpanelitem.btnlamp = !btnpanelitem.btnlamp;
            }
        }

        private void ClearDeviceList()
        {
            for (int i = 0; i < Registry.busoptionslistview.Count; i++)
            {
                Device device = m_device_list.Find(delegate(Device o)
                {
                    return o.index.Equals(i);
                }
                );
                if (device != null)
                    device.Destory();
            }
            m_deviceinfor_list.Clear();
            m_device_list.Clear();
            //只能在功能切换时清除
            DMDataManage.m_SFLNames_list.Clear();
        }

        public void AdjustDevice(bool badjust, string name)
        {
            if (badjust)//添加设备
            {
                Device device = new Device(name);
                device.device_infor.pretype = idevicetype;
                device.gm.PropertyChanged += new PropertyChangedEventHandler(gm_PropertyChanged);
                m_deviceinfor_list.Add(device.device_infor);
                m_deviceinfor_list.Sort(x => x.index);
                m_device_list.Add(device);
                m_device_list.Sort();

                for (int j = 0; j < btnPanelList.Count; j++)
                {
                    BtnPanelLink btnpanellink = btnPanelList[j];
                    if (btnpanellink == null) continue;

                    btnpanellink.AddBtnPanelLampItemByID(device.index + 1);
                }
            }
            else//删除设备
            {
                for (int i = 0; i < m_device_list.Count; i++)
                {
                    if (m_device_list[i].name.Equals(name))
                    {
                        for (int j = 0; j < btnPanelList.Count; j++)
                        {
                            BtnPanelLink btnpanellink = btnPanelList[j];
                            if (btnpanellink == null) continue;

                            btnpanellink.RemoveBtnPanelLampItemByID(m_device_list[i].index+1);
                        }

                        m_deviceinfor_list.Remove(m_deviceinfor_list[i]);
                        m_device_list[i].Destory();
                        m_device_list.Remove(m_device_list[i]);
                        break;
                    }
                }
            }
            return;
        }

        public void GetDevicesInfor()
        {
            foreach (Device device in devicelist)
            {
                if (device == null) continue;
                device.GetDeviceInfor();
            }
        }

        public Device GetDeviceByName(string name)
        {
            Device device = m_device_list.Find(delegate(Device item)
            {
                return item.name.Equals(name);
            }
            );
            if (device != null) return device;
            else return null;
        }

        public bool EnumerateInterface()
        {
            foreach (Device device in devicelist)
            {
                if (device == null) continue;
                device.EnumerateInterface();
            }
            return true;
        }

        public bool CreateInterface()
        {
            foreach (Device device in devicelist)
            {
                if (device == null) continue;
                device.CreateInterface();
            }

            return true;
        }

        //检查设备运行状态
        public bool CheckDevicesRun()
        {
            foreach (Device device in devicelist)
            {
                if (device.bBusy)
                    return true;
            }
            
            return false;
        }

        public bool CheckDeviceRun(string name)
        {
            Device device = GetDeviceByName(name);
            if (device == null) return false;

            if (device.bBusy)
                    return true;
            
            return false;
        }
        #endregion

        #region 整体管理
        public bool Build()
        {
            if (!CreateBtnPanelLinkList()) return false;
            return CreateDeviceList();
        }

        public void Destroy()
        {
            ClearBtnPanelLinkList();
            ClearDeviceList();
        }
        #endregion
    }
}
