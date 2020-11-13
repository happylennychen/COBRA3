using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Cobra.Update
{
    public class FolderMap
    {
        public static FileStream m_RecordFile;
        public static StreamWriter m_stream_writer;

        public static string m_parent_folder = "";
        public static string m_main_folder = "";
        public static string m_root_folder = "";
        public static string m_center_folder = "";
        public static string m_logs_folder = "";
        public static string m_register_file = "";

        public static bool InitFolders(string tmp = null)
        {
            try
            {
                m_parent_folder = tmp;
                m_root_folder = AppDomain.CurrentDomain.BaseDirectory;//Environment.CurrentDirectory.ToString();
                if (!Directory.Exists(m_root_folder))
                    Directory.CreateDirectory(m_root_folder);

                m_main_folder = m_root_folder.Remove(m_root_folder.LastIndexOf("Upgrade\\"));
                if (!Directory.Exists(m_main_folder))
                    Directory.CreateDirectory(m_main_folder);

                m_center_folder = Path.Combine(m_main_folder, "CobraCenter\\");
                if (!Directory.Exists(m_center_folder))
                    Directory.CreateDirectory(m_center_folder);

                m_logs_folder = Path.Combine(m_root_folder, "Logs");
                if (!Directory.Exists(m_logs_folder))
                    Directory.CreateDirectory(m_logs_folder);

                m_register_file = Path.Combine(m_root_folder, "Settings\\setting.xml");
                if (!File.Exists(m_register_file)) return false;

                string path = Path.Combine(FolderMap.m_logs_folder ,"Record" + DateTime.Now.GetDateTimeFormats('s')[0].ToString().Replace(@":", @"-") + ".log");
                m_RecordFile = new FileStream(path, FileMode.OpenOrCreate);
                m_stream_writer = new StreamWriter(m_RecordFile);

                WriteFile(FolderMap.m_parent_folder);
                WriteFile(AppDomain.CurrentDomain.BaseDirectory);
                WriteFile(m_center_folder);
                return true;
            }
            catch (System.Exception ex)
            {
                return false;
            }
        }

        public static void WriteFile(string info)
        {
            info += ": "+"Upgrade" + DateTime.Now.ToString() + ":" + DateTime.Now.Millisecond.ToString() + "\r\n";
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
            }
            return bReturn;
        }
    }
}
