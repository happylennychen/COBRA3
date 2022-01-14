using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Runtime.InteropServices;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Xml;
using System.Security.Cryptography;

namespace Cobra.Common
{
    public class SharedAPI
    {
        public static void ReBuildBusOptions(ref BusOptions busOptions, ref ParamListContainer ParamlistContainer)
        {

        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr _lopen(string lpPathName, int iReadWrite);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        private const int OF_READWRITE = 2;

        private const int OF_SHARE_DENY_NONE = 0x40;

        private static readonly IntPtr HFILE_ERROR = new IntPtr(-1);

        public static UInt32 FileIsOpen(string fileFullName)
        {
            if (!File.Exists(fileFullName))
            {
                return LibErrorCode.IDS_ERR_SECTION_SIMULATION_FILE_LOST;
            }
            IntPtr handle = _lopen(fileFullName, OF_READWRITE | OF_SHARE_DENY_NONE);
            if (handle == HFILE_ERROR)
            {
                return LibErrorCode.IDS_ERR_SECTION_SIMULATION_FILE_OPENED;
            }
            CloseHandle(handle);
            return LibErrorCode.IDS_ERR_SUCCESSFUL;
        }

        /// <summary>
        /// 将字典类型序列化为json字符串
        /// </summary>
        /// <typeparam name="TKey">字典key</typeparam>
        /// <typeparam name="TValue">字典value</typeparam>
        /// <param name="dict">要序列化的字典数据</param>
        /// <returns>json字符串</returns>
        public static string SerializeDictionaryToJsonString<TKey, TValue>(Dictionary<TKey, TValue> dict)
        {
            if (dict.Count == 0)
                return "";

            string jsonStr = JsonConvert.SerializeObject(dict);
            return jsonStr;
        }

        /// <summary>
        /// 将json字符串反序列化为字典类型
        /// </summary>
        /// <typeparam name="TKey">字典key</typeparam>
        /// <typeparam name="TValue">字典value</typeparam>
        /// <param name="jsonStr">json字符串</param>
        /// <returns>字典数据</returns>
        public static Dictionary<TKey, TValue> DeserializeStringToDictionary<TKey, TValue>(string jsonStr)
        {
            if (string.IsNullOrEmpty(jsonStr))
                return new Dictionary<TKey, TValue>();

            Dictionary<TKey, TValue> jsonDict = JsonConvert.DeserializeObject<Dictionary<TKey, TValue>>(jsonStr);

            return jsonDict;

        }

        /// <summary>
        /// 从Extension XML的ProjectSettings节点获取设定值。SFL和DEM有自己的方式，从特定的地方获取XML中的信息，但是SFL和DEM以外的部分，例如Shell，则可通过此API获取信息
        /// </summary>
        public static string GetProjectSettingFromExtension(string setting)
        {
            string output = string.Empty;
            string xmlfilepath = FolderMap.m_extension_work_folder + FolderMap.m_ext_descrip_xml_name + FolderMap.m_extension_work_ext;
            XmlDocument doc = new XmlDocument();
            doc.Load(xmlfilepath);
            XmlNode xn = doc.DocumentElement.SelectSingleNode("descendant::Part[@Name = 'ProjectSettings']");
            if (xn != null)
            {
                var subxn = xn.SelectSingleNode(setting);
                if (subxn != null)
                {
                    output = subxn.InnerText;
                }
            }
            return output;
        }

        /// <summary>
        /// 从Device XML的PRODUCT_FAMILY属性获取设定值。
        /// </summary>
        public static string GetProductFamilyFromExtension()
        {
            string xmlfilepath = FolderMap.m_extension_work_folder + FolderMap.m_dev_descrip_xml_name + FolderMap.m_extension_work_ext;
            XmlDocument doc = new XmlDocument();
            doc.Load(xmlfilepath);
            return doc.DocumentElement.GetAttribute(COBRA_GLOBAL.Constant.PRODUCT_FAMILY_NODE);
        }

        /// <summary>
        /// 从Device XML的chip属性获取设定值。
        /// </summary>
        public static string GetChipNameFromExtension()
        {
            string xmlfilepath = FolderMap.m_extension_work_folder + FolderMap.m_dev_descrip_xml_name + FolderMap.m_extension_work_ext;
            XmlDocument doc = new XmlDocument();
            doc.Load(xmlfilepath);
            return doc.DocumentElement.GetAttribute(COBRA_GLOBAL.Constant.CHIP_NAME_NODE);
        }

        /// <summary>
        /// 向doc xml的entry节点加入子节点，子节点的信息由参数给出
        /// </summary>
        public static XmlElement XmlAddOneNode(XmlDocument doc, XmlElement entry, string nodeName, string nodeInnerText = "", Dictionary<string, string> attributes = null)
        {
            XmlElement xe = doc.CreateElement(nodeName);

            if (attributes != null)
            {
                foreach (var attr in attributes)
                {
                    XmlAttribute xa = doc.CreateAttribute(attr.Key);
                    XmlText value = doc.CreateTextNode(attr.Value);
                    xa.AppendChild(value);
                    xe.SetAttributeNode(xa);
                }
            }
            if (nodeInnerText != string.Empty)
            {
                XmlText content = doc.CreateTextNode(nodeInnerText);
                xe.AppendChild(content);
            }

            entry.AppendChild(xe);
            return xe;
        }

        public static XmlNode FindOneNode(XmlDocument doc, string nodeName, Dictionary<string, string> attributes = null)
        {
            StringBuilder XPath = new StringBuilder();

            XPath.Append("descendant::");
            XPath.Append(nodeName);

            if (attributes != null)
            {
                XPath.Append("[");
                foreach (var attr in attributes)
                {
                    XPath.Append($"@{attr.Key}='{attr.Value}'");
                    if (attr.Key != attributes.Keys.Last())
                        XPath.Append(" and ");
                }
                XPath.Append("]");
            }
            return doc.DocumentElement.SelectSingleNode(XPath.ToString());
        }

        public static XmlNode XmlAddOrUpdateOneNode(XmlDocument doc, XmlElement entry, string nodeName, string nodeInnerText, Dictionary<string, string> attributes = null)
        {
            var xn = FindOneNode(doc, nodeName, attributes);
            if (xn != null)
            {
                xn.InnerText = nodeInnerText;
            }
            else
                xn = XmlAddOneNode(doc, entry, nodeName, nodeInnerText, attributes);
            return xn;
        }

        public static List<byte> LoadBinFileToList(string filepath)
        {
            Encoding ec = Encoding.UTF8;
            List<byte> blist = new List<byte>();
            using (BinaryReader br = new BinaryReader(File.Open(filepath, FileMode.Open), ec))
            {
                try
                {
                    while (true)
                    {
                        blist.Add(br.ReadByte());
                    }
                }
                catch (Exception e)
                {
                    br.Close();
                    if (!(e is EndOfStreamException))
                    {
                        blist.Clear();
                    }
                    return blist;
                }
            }
        }

        public static string GetMD5(string content)
        {
            return GetMD5(Encoding.UTF8.GetBytes(content));
        }

        public static string GetMD5(byte[] content)
        {
            using (MD5 md5Hash = MD5.Create())
            {
                // Convert the input string to a byte array and compute the hash.
                byte[] data = md5Hash.ComputeHash(content);

                // Create a new Stringbuilder to collect the bytes
                // and create a string.
                StringBuilder sBuilder = new StringBuilder();

                // Loop through each byte of the hashed data 
                // and format each one as a hexadecimal string.
                for (int i = 0; i < data.Length; i++)
                {
                    sBuilder.Append(data[i].ToString("x2"));
                }

                // Return the hexadecimal string.
                return sBuilder.ToString();
            }
        }
    }
}
