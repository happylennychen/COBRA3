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

namespace Cobra.Update
{
    public class Registry
    {
        private static XmlElement m_root;
        private static XmlDocument m_register_xmlDoc = new XmlDocument();

        private static ObservableCollection<string> m_skipDeleteFolderPath_List = new ObservableCollection<string>();
        public static ObservableCollection<string> skipDeleteFolderPath_List
        {
            get { return m_skipDeleteFolderPath_List; }
            set { skipDeleteFolderPath_List = m_skipDeleteFolderPath_List; }
        }

        private static ObservableCollection<string> m_skipCopyFolderPath_List = new ObservableCollection<string>();
        public static ObservableCollection<string> skipCopyFolderPath_List
        {
            get { return m_skipCopyFolderPath_List; }
            set { skipCopyFolderPath_List = m_skipCopyFolderPath_List; }
        }

        public static bool LoadRegistryFile()
        {
            if (!File.Exists(FolderMap.m_register_file)) return false;

            m_register_xmlDoc.Load(FolderMap.m_register_file);
            m_root = m_register_xmlDoc.DocumentElement;
            if (m_root == null) return false;

            BuildSkipDeleteFolderList();
            BuildSkipCopyFolderList();
            return true;
        }

        public static bool BuildSkipDeleteFolderList()
        {
            string tmp = null;
            XmlNode node = m_root.SelectSingleNode("//Part[@Name= 'SkipDelete']/Folders");
            if (node == null) return false;

            XmlNodeList snode = node.ChildNodes;
            if (snode.Count != 0)
            {
                foreach (XmlNode ssnode in snode)
                {
                    tmp = ssnode.Attributes["Name"].Value;
                    if (tmp != null)
                        skipDeleteFolderPath_List.Add(Path.Combine(FolderMap.m_parent_folder, tmp));
                }
            }
            return true;
        }

        public static bool BuildSkipCopyFolderList()
        {
            string tmp = null;
            XmlNode node = m_root.SelectSingleNode("//Part[@Name= 'SkipCopy']/Folders");
            if (node == null) return false;

            XmlNodeList snode = node.ChildNodes;
            if (snode.Count != 0)
            {
                foreach (XmlNode ssnode in snode)
                {
                    tmp = ssnode.Attributes["Name"].Value;
                    if (tmp != null)
                        skipCopyFolderPath_List.Add(Path.Combine(System.IO.Path.Combine(FolderMap.m_center_folder, "COBRA"), tmp));
                }
            }
            return true;
        }
    }
}
