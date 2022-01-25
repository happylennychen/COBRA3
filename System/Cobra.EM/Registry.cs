using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Text;
using System.ComponentModel;
using System.Windows.Data;
using System.Collections.ObjectModel;
using Cobra.Common;

namespace Cobra.EM
{
    public class Registry
    {
        private static XmlElement m_root;
        private static XmlDocument m_register_xmlDoc = new XmlDocument();

        public static int devicenum = 0;
        public static BUS_TYPE m_BusType = BUS_TYPE.BUS_TYPE_I2C;
        private static AsyncObservableCollection<BusOptions> m_busoptionslist = new AsyncObservableCollection<BusOptions>();
        public static AsyncObservableCollection<BusOptions> busoptionslist
        {
            get { return m_busoptionslist; }
            set { busoptionslist = m_busoptionslist; }
        }

        private static AsyncObservableCollection<BusOptions> m_busoptionslistview = new AsyncObservableCollection<BusOptions>();
        public static AsyncObservableCollection<BusOptions> busoptionslistview
        {
            get { return m_busoptionslistview; }
            set { busoptionslistview = m_busoptionslistview; }
        }


        private static List<BusOptionListCollectionView> m_busoptionslist_collectionview = new List<BusOptionListCollectionView>();
        public static List<BusOptionListCollectionView> busoptionslist_collectionview
        {
            get { return m_busoptionslist_collectionview; }
            set { busoptionslist_collectionview = m_busoptionslist_collectionview; }
        }

        public static bool LoadRegistryFile()
        {
            if (!File.Exists(FolderMap.m_register_file))
            {
                System.Windows.MessageBox.Show("Failed to load registry file!");	//Leon add this warnning.
                return false;
            }

            m_register_xmlDoc.Load(FolderMap.m_register_file);
            m_root = m_register_xmlDoc.DocumentElement;
            if (m_root == null) return false;

            return true;
        }

        public static string GetCurExtensionFileName()
        {
            XmlNode node = m_root.SelectSingleNode("//Part[@Name= 'PreProject']/PreExtension");
            if (node == null) return null;

            return node.Attributes["Name"].Value;
        }

        public static bool SaveCurExtensionFileName(string name)
        {
            XmlNode node = m_root.SelectSingleNode("//Part[@Name= 'PreProject']/PreExtension");
            if (node == null) return false;
            else node.Attributes["Name"].Value = name;

            m_register_xmlDoc.Save(FolderMap.m_register_file);
            return true;
        }

        public static void GetCurExtensionBusType()
        {
            string type = null;

            XmlNode node = m_root.SelectSingleNode("//Part[@Name= 'PreProject']/PreExtensionBusType");
            if (node == null)
            {
                m_BusType = BUS_TYPE.BUS_TYPE_I2C;
                return;
            }

            type = node.Attributes["BusType"].Value;
            switch (type)
            {
                case "I2C":
                    {
                        m_BusType = BUS_TYPE.BUS_TYPE_I2C;
                        break;
                    }
                case "I2C2":
                    {
                        m_BusType = BUS_TYPE.BUS_TYPE_I2C2;
                        break;
                    }
                case "SPI":
                    {
                        m_BusType = BUS_TYPE.BUS_TYPE_SPI;
                        break;
                    }
                //(A141020)Francis
                case "SVID":
                    {
                        m_BusType = BUS_TYPE.BUS_TYPE_SVID;
                        break;
                    }
                //(E141020)
                case "RS232":
                    {
                        m_BusType = BUS_TYPE.BUS_TYPE_RS232;
                        break;
                    }
                default:
                    {
                        m_BusType = BUS_TYPE.BUS_TYPE_I2C;
                        break;
                    }
            }
        }

        public static bool SaveCurExtensionBusType(string stype)
        {
            XmlNode node = m_root.SelectSingleNode("//Part[@Name= 'PreProject']/PreExtensionBusType");
            if (node == null) return false;
            else node.Attributes["BusType"].Value = stype;

            m_register_xmlDoc.Save(FolderMap.m_register_file);
            return true;
        }

        public static bool RestoreDeviceConnectSetting()
        {
            BusOptions busoptions = null;
            for (int i = 0; i < m_busoptionslistview.Count; i++)
            {
                busoptions = m_busoptionslistview[i];
                if (busoptions == null) return false;
            }
            return true;
        }

        public static bool CheckDeviceConnectSetting()
        {
            BusOptions busoptions = null;
            BusOptions tmpoptions = null;
            for (int i = 0; i < m_busoptionslistview.Count; i++)
            {
                busoptions = m_busoptionslistview[i];
                if (busoptions == null) continue;

                for (int j = 0; j < m_busoptionslistview.Count; j++)
                {
                    if (i == j) continue;
                    tmpoptions = m_busoptionslistview[j];
                    if (tmpoptions == null) continue;
                    /*
                    if((String.Compare(tmpoptions.SelConnectPort.Info,busoptions.SelConnectPort.Info) == 0)&&(busoptions.SelConnectPort.Index != 0))
                        return false;
                    else
                        continue;*/
                    //ByGuoZhu
                }
            }
            return true;
        }

        public static bool GetDeviceConnectSetting()
        {
            devicenum = 0;
            m_busoptionslist.Clear();
            m_busoptionslistview.Clear();
            m_busoptionslist_collectionview.Clear();

            XmlNode busnode = null;
            XmlNode node = m_root.SelectSingleNode("//Part[@Name= 'Device Connection Settings']");
            if (node == null) return false;

            GetCurExtensionBusType();
            busnode = node.SelectSingleNode("//DeviceList");
            foreach (XmlNode devicenode in busnode.ChildNodes)
            {
                if (devicenode == null) break;

                BusOptions busobject = new BusOptions();
                busobject.BusType = m_BusType;
                busobject.DeviceIndex = devicenum;
                busobject.DeviceIsCheck = Convert.ToBoolean(devicenode.Attributes["IsChecked"].Value);
                busobject.Name = devicenode.Attributes["Name"].Value;
                busobject.DeviceName = busobject.Name;

                m_busoptionslist.Add(busobject);
                if (busobject.DeviceIsCheck)
                    m_busoptionslistview.Add(busobject);
                devicenum++;
            }
            return true;
        }

        public static bool SaveDeviceConnectSetting()
        {
            string devicename = null;
            XmlNode busnode = null;

            XmlNode node = m_root.SelectSingleNode("//Part[@Name= 'Device Connection Settings']");
            if (node == null) return false;

            GetCurExtensionBusType();
            switch (m_BusType)
            {
                case BUS_TYPE.BUS_TYPE_I2C:
                    {
                        busnode = node.SelectSingleNode("//Bus[@Type= 'I2C']");
                        break;
                    }
                case BUS_TYPE.BUS_TYPE_I2C2:
                    {
                        busnode = node.SelectSingleNode("//Bus[@Type= 'I2C2']");
                        break;
                    }
                case BUS_TYPE.BUS_TYPE_SPI:
                    {
                        busnode = node.SelectSingleNode("//Bus[@Type= 'SPI']");
                        break;
                    }
                //(A141021)Francis
                case BUS_TYPE.BUS_TYPE_SVID:
                    {
                        busnode = node.SelectSingleNode("//Bus[@Type= 'SVID']");
                        break;
                    }
                //(E141021)
                case BUS_TYPE.BUS_TYPE_RS232:
                    {
                        busnode = node.SelectSingleNode("//Bus[@Type= 'RS232']");
                        break;
                    }
                default:
                    {
                        busnode = node.SelectSingleNode("//Bus[@Type= 'I2C']");
                        break;
                    }
            }

            foreach (XmlNode devicenode in busnode.ChildNodes)
            {
                if (devicenode == null) break;

                devicename = devicenode.Attributes["Name"].Value;
                //Parse XML Document
            }
            m_register_xmlDoc.Save(FolderMap.m_register_file);
            return true;
        }

        public static BusOptions GetBusOptionsByName(string name)
        {
            foreach (BusOptions device in m_busoptionslist)
            {
                if (device.Name.Equals(name))
                    return device;
            }
            return null;
        }

        public static bool CheckBusOptionsByNameInListView(string name)
        {
            foreach (BusOptions device in m_busoptionslistview)
            {
                if (device.Name.Equals(name))
                    return true;
            }
            return false;
        }

        public static BusOptions GetBusOptionsByindexInListView(int index)
        {
            foreach (BusOptions device in m_busoptionslistview)
            {
                if (device.DeviceIndex.Equals(index))
                    return device;
            }
            return null;
        }

        #region new xml node in setting.xml <Part Name="PreProduction">
        //(A130801)Francis, support new xml node in setting.xml, for <Part Name="PreProduction">
        public static bool GetCurPreProductionNode(string strNode, ref string strName)
        {
            string strTarget = "//Part[@Name= 'PreProduction']/";

            strTarget += strNode;
            XmlNode node = m_root.SelectSingleNode(strTarget);

            if (node == null)
                return false;
            else
            {
                strName = node.Attributes["Name"].Value;
                return true;
            }
        }

        public static bool SaveCurPreProductionNode(string strNode, string strName)
        {
            string strTarget = "//Part[@Name= 'PreProduction']/";

            strTarget += strNode;
            XmlNode node = m_root.SelectSingleNode(strTarget);

            if (node == null)
                return false;
            else
            {
                node.Attributes["Name"].Value = strName;
                m_register_xmlDoc.Save(FolderMap.m_register_file);
                return true;
            }
        }
        //(E130801)
        #endregion

        #region CFG file path        
        public static bool SaveConfigFilePath(string fullpath)
        {
            bool output = false;
            try
            {
                Dictionary<string, string> dic = new Dictionary<string, string>();
                dic.Add("Name", COBRA_GLOBAL.Constant.CONFIG_FILE_PATH_NODE);
                var partNode = SharedAPI.FindOneNode(m_register_xmlDoc, "Part", dic);
                if (partNode == null)
                {
                    partNode = SharedAPI.XmlAddOneNode(m_register_xmlDoc, m_root, "Part", "", dic);
                    SharedAPI.XmlAddOneNode(m_register_xmlDoc, (XmlElement)partNode, COBRA_GLOBAL.CurrentOCEName, fullpath);
                }
                else
                {
                    SharedAPI.XmlAddOrUpdateOneNode(m_register_xmlDoc, (XmlElement)partNode, COBRA_GLOBAL.CurrentOCEName, fullpath);
                }
                m_register_xmlDoc.Save(FolderMap.m_register_file);
                output = true;
            }
            catch (Exception e)
            {
                System.Windows.MessageBox.Show(e.Message);
                output = false;
            }
            return output;
        }
        public static bool GetConfigFilePath(out string fullpath)
        {
            bool output = false;
            try
            {
                var node = SharedAPI.FindOneNode(m_register_xmlDoc, COBRA_GLOBAL.CurrentOCEName);
                if (node != null)
                {
                    fullpath = node.InnerText;
                    output = true;
                }
                else
                {
                    output = false;
                    fullpath = string.Empty;
                }
            }
            catch (Exception e)
            {
                System.Windows.MessageBox.Show(e.Message);
                output = false;
                fullpath = string.Empty;
            }
            return output;

        }
        public static bool DeleteConfigFilePath()
        {
            bool output = false;
            try
            {
                Dictionary<string, string> dic = new Dictionary<string, string>();
                dic.Add("Name", COBRA_GLOBAL.Constant.CONFIG_FILE_PATH_NODE);
                var oceNode = SharedAPI.FindOneNode(m_register_xmlDoc, COBRA_GLOBAL.CurrentOCEName);
                if (oceNode == null)
                {
                }
                else
                {
                    oceNode.ParentNode.RemoveChild(oceNode);
                }
                m_register_xmlDoc.Save(FolderMap.m_register_file);
                output = true;
            }
            catch (Exception e)
            {
                System.Windows.MessageBox.Show(e.Message);
                output = false;
            }
            return output;
        }
        #endregion
    }
}
