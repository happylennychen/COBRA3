using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Diagnostics;

namespace Cobra.Common
{
    public class FolderMap
    {
        public static FileStream m_RecordFile;
        public static StreamWriter m_stream_writer;

        public static string m_extensions_folder = "";
        public static string m_extension_work_folder = "";
        public static string m_extension_monitor_folder = "";
        public static string m_projects_folder = "";
        public static string m_currentproj_folder = "";
        public static string m_curextensionfile_name = "";
        public static string m_logs_folder = "";
        public static string m_extension_ext = ".oce";
        public static string m_extension_work_ext = ".xml";
        public static string m_trim_template_ext = ".xlsx";
        public static string m_extension_common_name = "*";
        public static string m_register_file = "";
        public static string m_ext_descrip_xml_name = "ExtensionDescriptor";
        public static string m_dev_descrip_xml_name = "DeviceDescriptor";
        public static string m_trim_template_name = "TrimTemplate";
        public static string m_standard_feature_library_folder = "";
        public static string m_dem_library_folder = "";
        public static string m_main_folder = "";
        public static string m_root_folder = "";
        public static string m_customer_folder = "";
        public static string m_ReadMe_file = "";
        public static object locker = new object();

        //Upgrade Folder
        public static string m_center_folder = "";
        public static string m_upgrade_folder = "";
        public static string m_upgrade_file = "Cobra.Update";
        public static string m_upgrade_ext = ".exe";
        public static string m_sm_work_folder = string.Empty;

        public static UInt32 InitFolders()
        {
            try
            {
                m_customer_folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (!Directory.Exists(m_customer_folder))
                    Directory.CreateDirectory(m_customer_folder);

                m_root_folder = AppDomain.CurrentDomain.BaseDirectory;//Environment.CurrentDirectory.ToString();
                if (!Directory.Exists(m_root_folder))
                    return LibErrorCode.IDS_ERR_SECTION_FOLDERS_LOST;
                else
                {
                    if (!IsReadable(new DirectoryInfo(m_root_folder)) | !IsWriteable(new DirectoryInfo(m_root_folder)))
                        return LibErrorCode.IDS_ERR_SECTION_CANNOT_ACCESS_COBRA_FOLDER;
                }

                m_main_folder = m_root_folder.Remove(m_root_folder.LastIndexOf("COBRA\\"));
                if (!Directory.Exists(m_main_folder))
                    return LibErrorCode.IDS_ERR_SECTION_FOLDERS_LOST;
                else
                {
                    if (!IsReadable(new DirectoryInfo(m_main_folder)) | !IsWriteable(new DirectoryInfo(m_main_folder)))
                        return LibErrorCode.IDS_ERR_SECTION_CANNOT_ACCESS_COBRA_FOLDER;
                }

                m_upgrade_folder = Path.Combine(m_main_folder, "Upgrade\\");
                if (!Directory.Exists(m_upgrade_folder))
                    return LibErrorCode.IDS_ERR_SECTION_FOLDERS_LOST;
                else
                {
                    if (!IsReadable(new DirectoryInfo(m_upgrade_folder)) | !IsWriteable(new DirectoryInfo(m_upgrade_folder)))
                        return LibErrorCode.IDS_ERR_SECTION_CANNOT_ACCESS_COBRA_FOLDER;
                }

                m_center_folder = Path.Combine(m_main_folder, "CobraCenter\\");
                if (!Directory.Exists(m_center_folder))
                    return LibErrorCode.IDS_ERR_SECTION_FOLDERS_LOST;
                else
                {
                    if (!IsReadable(new DirectoryInfo(m_center_folder)) | !IsWriteable(new DirectoryInfo(m_center_folder)))
                        return LibErrorCode.IDS_ERR_SECTION_CANNOT_ACCESS_COBRA_FOLDER;
                }

                m_extensions_folder = Path.Combine(m_root_folder, "Extensions\\");
                if (!Directory.Exists(m_extensions_folder))
                    return LibErrorCode.IDS_ERR_SECTION_FOLDERS_LOST;
                else
                {
                    if (!IsReadable(new DirectoryInfo(m_extensions_folder)) | !IsWriteable(new DirectoryInfo(m_extensions_folder)))
                        return LibErrorCode.IDS_ERR_SECTION_CANNOT_ACCESS_COBRA_FOLDER;
                }

                m_extension_work_folder = Path.Combine(m_root_folder, "ExtensionRuntime\\");
                if (!Directory.Exists(m_extension_work_folder))
                    Directory.CreateDirectory(m_extension_work_folder);
                else
                {
                    if (!IsReadable(new DirectoryInfo(m_extension_work_folder)) | !IsWriteable(new DirectoryInfo(m_extension_work_folder)))
                        return LibErrorCode.IDS_ERR_SECTION_CANNOT_ACCESS_ExtRT_FOLDER;
                }

                m_extension_monitor_folder = Path.Combine(m_root_folder, "ExtensionMonitor\\");
                if (!Directory.Exists(m_extension_monitor_folder))
                    Directory.CreateDirectory(m_extension_monitor_folder);
                else
                {
                    if (!IsReadable(new DirectoryInfo(m_extension_monitor_folder)) | !IsWriteable(new DirectoryInfo(m_extension_monitor_folder)))
                        return LibErrorCode.IDS_ERR_SECTION_CANNOT_ACCESS_ExtMT_FOLDER;
                }

                m_standard_feature_library_folder = Path.Combine(m_root_folder, "SFL\\");
                if (!Directory.Exists(m_standard_feature_library_folder))
                    return LibErrorCode.IDS_ERR_SECTION_FOLDERS_LOST;
                else
                {
                    if (!IsReadable(new DirectoryInfo(m_standard_feature_library_folder)))
                        return LibErrorCode.IDS_ERR_SECTION_CANNOT_ACCESS_COBRA_FOLDER;
                }

                m_dem_library_folder = Path.Combine(m_root_folder, "Libs\\");
                if (!Directory.Exists(m_dem_library_folder))
                    Directory.CreateDirectory(m_dem_library_folder);
                else
                {
                    if (!IsReadable(new DirectoryInfo(m_dem_library_folder)))
                        return LibErrorCode.IDS_ERR_SECTION_CANNOT_ACCESS_COBRA_FOLDER;
                }

                //m_projects_folder = Path.Combine(m_customer_folder, "COBRA Documents\\");
                m_projects_folder = Path.Combine(m_root_folder, "COBRA Documents\\");
                if (!Directory.Exists(m_projects_folder))
                    Directory.CreateDirectory(m_projects_folder);
                else
                {
                    if (!IsReadable(new DirectoryInfo(m_projects_folder)) | !IsWriteable(new DirectoryInfo(m_projects_folder)))
                        return LibErrorCode.IDS_ERR_SECTION_CANNOT_ACCESS_COBRA_DOC;
                }

                m_logs_folder = Path.Combine(m_root_folder, "Logs\\");
                if (!Directory.Exists(m_logs_folder))
                    Directory.CreateDirectory(m_logs_folder);
                else
                {
                    if (!IsReadable(new DirectoryInfo(m_logs_folder)) | !IsWriteable(new DirectoryInfo(m_logs_folder)))
                        return LibErrorCode.IDS_ERR_SECTION_CANNOT_ACCESS_LOG;
                }

                string path = FolderMap.m_logs_folder + "Record" + DateTime.Now.GetDateTimeFormats('s')[0].ToString().Replace(@":", @"-") + ".log";
                m_RecordFile = new FileStream(path, FileMode.OpenOrCreate);
                m_stream_writer = new StreamWriter(m_RecordFile);

                m_register_file = Path.Combine(m_root_folder, "Settings\\setting.xml");
                if (!File.Exists(m_register_file)) return LibErrorCode.IDS_ERR_SECTION_LOST_SET_FILES; 
                return LibErrorCode.IDS_ERR_SUCCESSFUL;
            }
            catch (System.Exception ex)
            {
                return LibErrorCode.IDS_ERR_SECTION_CANNOT_CREATE_FOLDER_COM;
            }
        }

        public static void WriteFile(string info)
        {
            lock (locker)
            {
                info += ": " + DateTime.Now.ToString() + ":" + DateTime.Now.Millisecond.ToString() + "\r\n";
                m_stream_writer.Write(info);
                m_stream_writer.Flush();
            }
        }

        public static bool CreateFolder(string strInFolder)
        {
            bool bReturn = true;
            try
            {
                if (!Directory.Exists(strInFolder))
                    Directory.CreateDirectory(strInFolder);
            }
            catch (Exception ex)
            {
                bReturn = false;
                MessageBox.Show(ex.Message);
            }
            return bReturn;
        }

        public static bool HasOperationPermission(string folder)
        {
            var currentUserIdentity = Path.Combine(Environment.UserDomainName, Environment.UserName);
            DirectorySecurity fileAcl = Directory.GetAccessControl(folder);
            var userAccessRules = fileAcl.GetAccessRules(true, true, typeof(System.Security.Principal.NTAccount)).OfType<FileSystemAccessRule>().Where(i => i.IdentityReference.Value == currentUserIdentity).ToList();
            return userAccessRules.Any(i => i.AccessControlType == AccessControlType.Deny);
        }

        public static bool IsReadable(DirectoryInfo di)
        {
            AuthorizationRuleCollection rules;
            WindowsIdentity identity;
            try
            {
                rules = di.GetAccessControl().GetAccessRules(true, true, typeof(SecurityIdentifier));
                identity = WindowsIdentity.GetCurrent();
            }
            catch (UnauthorizedAccessException uae)
            {
                Debug.WriteLine(uae.ToString());
                return false;
            }

            bool isAllow = false;
            string userSID = identity.User.Value;

            foreach (FileSystemAccessRule rule in rules)
            {
                if (rule.IdentityReference.ToString() == userSID || identity.Groups.Contains(rule.IdentityReference))
                {
                    if ((rule.FileSystemRights.HasFlag(FileSystemRights.Read) ||
                        rule.FileSystemRights.HasFlag(FileSystemRights.ReadAttributes) ||
                        rule.FileSystemRights.HasFlag(FileSystemRights.ReadData)) && rule.AccessControlType == AccessControlType.Deny)
                        return false;
                    else if ((rule.FileSystemRights.HasFlag(FileSystemRights.Read) &&
                        rule.FileSystemRights.HasFlag(FileSystemRights.ReadAttributes) &&
                        rule.FileSystemRights.HasFlag(FileSystemRights.ReadData)) && rule.AccessControlType == AccessControlType.Allow)
                        isAllow = true;

                }
            }
            return isAllow;

        }

        public static bool IsWriteable(DirectoryInfo me)
        {
            AuthorizationRuleCollection rules;
            WindowsIdentity identity;
            try
            {
                rules = me.GetAccessControl().GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier));
                identity = WindowsIdentity.GetCurrent();
            }
            catch (UnauthorizedAccessException uae)
            {
                Debug.WriteLine(uae.ToString());
                return false;
            }

            bool isAllow = false;
            string userSID = identity.User.Value;

            foreach (FileSystemAccessRule rule in rules)
            {
                if (rule.IdentityReference.ToString() == userSID || identity.Groups.Contains(rule.IdentityReference))
                {
                    if ((rule.FileSystemRights.HasFlag(FileSystemRights.Write) ||
                        rule.FileSystemRights.HasFlag(FileSystemRights.WriteAttributes) ||
                        rule.FileSystemRights.HasFlag(FileSystemRights.WriteData) ||
                        rule.FileSystemRights.HasFlag(FileSystemRights.CreateDirectories) ||
                        rule.FileSystemRights.HasFlag(FileSystemRights.CreateFiles)) && rule.AccessControlType == AccessControlType.Deny)
                        return false;
                    else if ((rule.FileSystemRights.HasFlag(FileSystemRights.Write) &&
                        rule.FileSystemRights.HasFlag(FileSystemRights.WriteAttributes) &&
                        rule.FileSystemRights.HasFlag(FileSystemRights.WriteData) &&
                        rule.FileSystemRights.HasFlag(FileSystemRights.CreateDirectories) &&
                        rule.FileSystemRights.HasFlag(FileSystemRights.CreateFiles)) && rule.AccessControlType == AccessControlType.Allow)
                        isAllow = true;

                }
            }
            return isAllow;
        }
    }
}
