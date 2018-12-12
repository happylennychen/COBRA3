using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows;

namespace O2Micro.Cobra.Common
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
        public static string m_EULA_file = "";
        public static string m_ReadMe_file = "";

        //Upgrade Folder
        public static string m_center_folder = "";
        public static string m_upgrade_folder = "";
        public static string m_upgrade_file = "O2Micro.Cobra.Update";
        public static string m_upgrade_ext = ".exe";
        public static string m_sm_work_folder = string.Empty;

        public static bool InitFolders()
        {
            try
            {
                m_customer_folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (!Directory.Exists(m_customer_folder))
                    Directory.CreateDirectory(m_customer_folder);

                m_root_folder = AppDomain.CurrentDomain.BaseDirectory;//Environment.CurrentDirectory.ToString();
                if (!Directory.Exists(m_root_folder))
                    Directory.CreateDirectory(m_root_folder);

                m_main_folder = m_root_folder.Remove(m_root_folder.LastIndexOf("COBRA\\"));
                if (!Directory.Exists(m_main_folder))
                    Directory.CreateDirectory(m_main_folder);

                m_upgrade_folder = Path.Combine(m_main_folder, "Upgrade\\");
                if (!Directory.Exists(m_upgrade_folder))
                    Directory.CreateDirectory(m_upgrade_folder);

                m_center_folder = Path.Combine(m_main_folder, "CobraCenter\\");
                if (!Directory.Exists(m_center_folder))
                    Directory.CreateDirectory(m_center_folder);

                m_extensions_folder = Path.Combine(m_root_folder, "Extensions\\");
                if (!Directory.Exists(m_extensions_folder))
                    Directory.CreateDirectory(m_extensions_folder);

                m_extension_work_folder = Path.Combine(m_root_folder, "ExtensionRuntime\\");
                if (!Directory.Exists(m_extension_work_folder))
                    Directory.CreateDirectory(m_extension_work_folder);

                m_extension_monitor_folder = Path.Combine(m_root_folder, "ExtensionMonitor\\");
                if (!Directory.Exists(m_extension_monitor_folder))
                    Directory.CreateDirectory(m_extension_monitor_folder);

                m_standard_feature_library_folder = Path.Combine(m_root_folder, "SFL\\");
                if (!Directory.Exists(m_standard_feature_library_folder))
                    Directory.CreateDirectory(m_standard_feature_library_folder);

                m_dem_library_folder = Path.Combine(m_root_folder, "Libs\\");
                if (!Directory.Exists(m_dem_library_folder))
                    Directory.CreateDirectory(m_dem_library_folder);

                m_projects_folder = Path.Combine(m_customer_folder, "COBRA Documents\\");
                if (!Directory.Exists(m_projects_folder))
                    Directory.CreateDirectory(m_projects_folder);

                m_logs_folder = Path.Combine(m_root_folder, "Logs\\");
                if (!Directory.Exists(m_logs_folder))
                    Directory.CreateDirectory(m_logs_folder);

                string path = FolderMap.m_logs_folder + "Record" + DateTime.Now.GetDateTimeFormats('s')[0].ToString().Replace(@":", @"-") + ".log";
                m_RecordFile = new FileStream(path, FileMode.OpenOrCreate);
                m_stream_writer = new StreamWriter(m_RecordFile);

                m_register_file = Path.Combine(m_root_folder, "Settings\\setting.xml");
                if (!File.Exists(m_register_file)) return false;

                m_EULA_file = Path.Combine(m_root_folder, "O2Micro EULA.rtf");
                if (!File.Exists(m_EULA_file)) return false;

                //m_ReadMe_file = Path.Combine(m_root_folder, "Readme.rtf");
                //if (!File.Exists(m_ReadMe_file)) return false;
                return true;
            }
            catch (System.Exception ex)
            {
                return false;
            }
        }

        public static void WriteFile(string info)
        {
            info += ": " + DateTime.Now.ToString() + ":" + DateTime.Now.Millisecond.ToString() + "\r\n";
            m_stream_writer.Write(info);
            m_stream_writer.Flush();
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
    }
}
