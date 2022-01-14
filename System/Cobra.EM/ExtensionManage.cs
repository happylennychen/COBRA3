//#define debug
//#define x
//#define extension
//#define y
//#define m
using System;
using System.Text;
using System.IO;
using System.Xml;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Cobra.Common;
using System.Xml.Linq;
using System.Security.Cryptography;

namespace Cobra.EM
{
    public class EMExtensionManage : DependencyObject
    {
        #region variable
        private GeneralMessage m_GM = new GeneralMessage();
        public GeneralMessage gm
        {
            get { return m_GM; }
            set
            {
                m_GM.setvalue((GeneralMessage)value);
            }
        }

        public static XmlDocument m_extDescrip_xmlDoc = new XmlDocument();
        public static EMDevicesManage m_EM_DevicesManage = new EMDevicesManage();
        public static VERSION_CONTROL version_ctl = VERSION_CONTROL.VERSION_CONTROL_02_00_03;
        public static int chipMode = 0;   //ID:784
        #endregion

        #region Shell指令
        public EMExtensionManage()
        {
        }

        public bool Init()
        {
            Registry.LoadRegistryFile();
            m_EM_DevicesManage.gm.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler(gm_PropertyChanged);
            DestroyExtension();
            return BuildExtension();
        }

        void gm_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            gm = (GeneralMessage)sender;
        }

        //根据设备名的存在与否做删除/添加动作
        public void AdjustDevice(bool badjust, string name)
        {
            m_EM_DevicesManage.AdjustDevice(badjust, name);
        }

        //获取设备基本信息
        public void GetDevicesInfor()
        {
            m_EM_DevicesManage.GetDevicesInfor();
        }

        //枚举设备接口
        public bool EnumerateInterface()
        {
            return m_EM_DevicesManage.EnumerateInterface();
        }

        public bool CreateInterface()
        {
            return m_EM_DevicesManage.CreateInterface();
        }

        //检查设备运行状态
        public bool CheckDevicesRun()
        {
            return m_EM_DevicesManage.CheckDevicesRun();
        }

        public bool CheckDeviceRun(string name)
        {
            return m_EM_DevicesManage.CheckDeviceRun(name);
        }
        #endregion

        #region Devices管理
        private bool OpenExtension()
        {
            ClearExtTemp();

            if (!UnZipExtension()) return false;
            if (!LoadExtension())
            {
                return false;
            }

            return true;
        }

        private void ClearExtTemp()
        {
#if !debug
            //Clear temp folder
            DirectoryInfo directory = new DirectoryInfo(FolderMap.m_extension_work_folder);
            if (!directory.Exists)
                directory.Create();
            else
                foreach (FileInfo files in directory.GetFiles(FolderMap.m_extension_common_name))
                    files.Delete();
#endif
        }

        private bool UnZipExtension()
        {
            if (Registry.GetCurExtensionFileName().Length == 0) return false;
            FolderMap.m_curextensionfile_name = Registry.GetCurExtensionFileName();
#if debug
            //string projectname = "KALL8";
            //string projectname = "OZ2610";
            //string projectname = "Woodpecker10";
            string projectname = "Woodpecker8";
            //string projectname = "Pikachu5";
            //string extension = " 3717";
            //string projectname = "KALL17";
            //string projectname = "Wizards";
            //string projectname = "SP8G2";
            //string projectname = "KALL";
            //string extension = " 7703";
            //string projectname = "Azalea14";
#endif
#if x
            string xmlxpath = FolderMap.m_main_folder.Remove(FolderMap.m_main_folder.LastIndexOf("output\\"));
            xmlxpath = Path.Combine(xmlxpath, "System\\DEM\\O2Micro.Cobra." + projectname + "\\xml x\\");
#if extension
            xmlxpath = xmlxpath.Substring(0, xmlxpath.Length-1) + extension + "\\";
#endif
            foreach (string path in Directory.GetFiles(xmlxpath))
            {
                string destPath = Path.Combine(FolderMap.m_extension_work_folder, Path.GetFileName(path));
                File.Copy(path, destPath, true);
            }
#endif
#if y
            string xmlypath = FolderMap.m_main_folder.Remove(FolderMap.m_main_folder.LastIndexOf("output\\"));
            xmlypath = Path.Combine(xmlypath, "System\\DEM\\O2Micro.Cobra." + projectname + "\\xml y\\");
            foreach (string path in Directory.GetFiles(xmlypath))
            {
                string destPath = Path.Combine(FolderMap.m_extension_work_folder, Path.GetFileName(path));
                File.Copy(path, destPath, true);
            }
#endif
#if m
            string xmlmpath = FolderMap.m_main_folder.Remove(FolderMap.m_main_folder.LastIndexOf("output\\"));
            xmlmpath = Path.Combine(xmlmpath, "System\\DEM\\O2Micro.Cobra." + projectname + "\\xml m\\");
            foreach (string path in Directory.GetFiles(xmlmpath))
            {
                string destPath = Path.Combine(FolderMap.m_extension_work_folder, Path.GetFileName(path));
                File.Copy(path, destPath, true);
            }
#endif
#if !debug
            string fullname = Registry.GetCurExtensionFileName() + FolderMap.m_extension_ext;
            string fullpath = FolderMap.m_extension_work_folder;

            DirectoryInfo directory = new DirectoryInfo(FolderMap.m_extensions_folder);
            foreach (FileInfo file in directory.GetFiles(fullname))
            {
                if (!file.Name.Equals(fullname))
                    return false;
                else
                    GZip.Decompress(FolderMap.m_extensions_folder, fullpath, fullname);
            }
            //复制Dll文件到主目录下
            foreach (string path in Directory.GetFiles(FolderMap.m_extension_work_folder, "*.dll"))
            {
                string destPath = Path.Combine(FolderMap.m_dem_library_folder, Path.GetFileName(path));
                File.Copy(path, destPath, true);
            }
#endif
            //创建工程配置数据临时文件夹
            FolderMap.m_currentproj_folder = FolderMap.m_projects_folder + FolderMap.m_curextensionfile_name;
            if (!Directory.Exists(FolderMap.m_currentproj_folder))
                Directory.CreateDirectory(FolderMap.m_currentproj_folder);

            //软件模式下工程配置数据临时文件夹
            FolderMap.m_sm_work_folder = Path.Combine(FolderMap.m_currentproj_folder, "Project\\");
            if (!Directory.Exists(FolderMap.m_sm_work_folder))
                Directory.CreateDirectory(FolderMap.m_sm_work_folder);
            return true;
        }

        private bool LoadExtension()
        {
            string extxmlfullname = FolderMap.m_extension_work_folder + FolderMap.m_ext_descrip_xml_name + FolderMap.m_extension_work_ext;
            if (!File.Exists(extxmlfullname))
            {
                return false;
            }

            m_extDescrip_xmlDoc.Load(extxmlfullname);
            return true;
        }

        private bool BuildExtension()
        {
            if (OpenExtension() != true)
            {
                return false;
            }
            if (!m_EM_DevicesManage.Build()) return false;
            return true;
        }

        private void DestroyExtension()
        {
            ClearExtTemp();
            m_extDescrip_xmlDoc.RemoveAll();
            m_EM_DevicesManage.Destroy();
        }
        #endregion

        #region OCE检查 ID:592 697
        public UInt32 MonitorExtension(string filename)
        {
            try
            {
                ClearMonitorTemp();
                if (GZip.Decompress(FolderMap.m_extensions_folder, FolderMap.m_extension_monitor_folder, string.Format("{0}{1}", filename, FolderMap.m_extension_ext)).Errors)
                    return LibErrorCode.IDS_ERR_SECTION_OCE_UNZIP;
                return LoadExtension(filename);
            }
            catch (System.Exception ex)
            {
                return LibErrorCode.IDS_ERR_SECTION_OCE_UNZIP;
            }
        }

        private void ClearMonitorTemp()
        {
            //Clear temp folder
            DirectoryInfo directory = new DirectoryInfo(FolderMap.m_extension_monitor_folder);
            if (!directory.Exists)
                directory.Create();
            else
                foreach (FileInfo files in directory.GetFiles(FolderMap.m_extension_common_name))
                    files.Delete();
        }

        private UInt32 LoadExtension(string name)
        {
            XmlElement root = null;
            Version oceversion = new Version("0.0.0.0");
            string extxmlfullname = FolderMap.m_extension_monitor_folder + FolderMap.m_ext_descrip_xml_name + FolderMap.m_extension_work_ext;
            if (!File.Exists(extxmlfullname)) return LibErrorCode.IDS_ERR_SECTION_OCE_LOSE_FILE;
            string desxmlfullname = FolderMap.m_extension_monitor_folder + FolderMap.m_dev_descrip_xml_name + FolderMap.m_extension_work_ext;
            if (!File.Exists(desxmlfullname)) return LibErrorCode.IDS_ERR_SECTION_OCE_LOSE_FILE;

            XmlDocument m_extDescrip_xmlDoc = new XmlDocument();
            try
            {
                m_extDescrip_xmlDoc.Load(extxmlfullname);
                root = m_extDescrip_xmlDoc.DocumentElement;
                if (root.GetAttribute("libname") == string.Empty) return LibErrorCode.IDS_ERR_SECTION_OCE_DIS_FILE_ATTRIBUTE;
                if (root.GetAttribute("ProjectCode") == string.Empty) return LibErrorCode.IDS_ERR_SECTION_OCE_DIS_FILE_ATTRIBUTE;
                if (root.GetAttribute("OCEVersion") == string.Empty) return LibErrorCode.IDS_ERR_SECTION_OCE_DIS_FILE_ATTRIBUTE;
                if (root.GetAttribute("libname").ToUpper().Contains("O2MICRO")) return LibErrorCode.IDS_ERR_SECTION_OCE_MISMATCH_20024;
                if (!File.Exists(Path.Combine(FolderMap.m_extension_monitor_folder, string.Format("{0}{1}", root.GetAttribute("libname"), ".dll")))) return LibErrorCode.IDS_ERR_SECTION_OCE_DIS_DEM;
                new VersionInfo(name, root.GetAttribute("ProjectCode"), new Version(root.GetAttribute("OCEVersion")), ASSEMBLY_TYPE.OCE, LibErrorCode.IDS_ERR_SUCCESSFUL);
            }
            catch (System.Exception ex)
            {
                new VersionInfo(name, root.GetAttribute("ProjectCode"), oceversion, ASSEMBLY_TYPE.OCE, LibErrorCode.IDS_ERR_SECTION_CENTER_OCE_VERSION_LOW);
                return LibErrorCode.IDS_ERR_SECTION_OCE_DIS_FILE_ATTRIBUTE;
            }
            return LibErrorCode.IDS_ERR_SUCCESSFUL;
        }
        #endregion

        public void GetExtensionToken()		//Issue1741 Leon
        {
            Dictionary<string, string> TokenContent = new Dictionary<string, string>(); //存放token的数据结构
            Dictionary<string, string> DLLTokenContent = new Dictionary<string, string>(); //存放dll token的数据结构
            Dictionary<string, string> ParamTokenContent = new Dictionary<string, string>(); //存放parameter token的数据结构
            Dictionary<string, string> BoardTokenContent = new Dictionary<string, string>(); //存放board token的数据结构


            XmlDocument m_extDescrip_xmlDoc = new XmlDocument();
            XmlElement root = null;
            string extxmlfullname = FolderMap.m_extension_monitor_folder + FolderMap.m_ext_descrip_xml_name + FolderMap.m_extension_work_ext;
            m_extDescrip_xmlDoc.Load(extxmlfullname);
            root = m_extDescrip_xmlDoc.DocumentElement;
            string dllfilefullpath = Path.Combine(FolderMap.m_extension_monitor_folder, string.Format("{0}{1}", root.GetAttribute("libname"), ".dll"));
            FileInfo fi = new FileInfo(dllfilefullpath);
            string dllfilename = string.Format("{0}{1}", root.GetAttribute("libname"), ".dll");

            string devxmlfullname = FolderMap.m_extension_monitor_folder + FolderMap.m_dev_descrip_xml_name + FolderMap.m_extension_work_ext;
            XDocument doc = XDocument.Load(devxmlfullname);

            var strpair = (from ele in doc.Descendants("Element")
                           where ele.Element("Private") != null && ele.Element("Private").Element("SFL") != null && ele.Element("Private").Element("SFL").Attribute("Name").Value == COBRA_GLOBAL.Constant.NewEFUSEConfigName
                           select new string[] {
                               ele.Element("Private").Element("SFL").Element("NickName").Value,
                               ele.Element("PhyRef").Value + "," + ele.Element("RegRef").Value + "," + ele.Element("SubType").Value + "," + ele.Element("Private").Element("SFL").Element("EditType").Value + ","+ ele.Element("Private").Element("SFL").Element("Format").Value
                           }).ToList();
            strpair.OrderBy(o => o[0]).ToList();
            foreach (var i in strpair)
            {
                TokenContent.Add(i[0], i[1]);
                ParamTokenContent.Add(i[0], i[1]);
            }
            strpair = (from ele in doc.Descendants("Element")
                       where ele.Element("Private") != null && ele.Element("Private").Element("SFL") != null && ele.Element("Private").Element("SFL").Attribute("Name").Value == COBRA_GLOBAL.Constant.NewBoardConfigName
                       select new string[] {
                               ele.Element("Private").Element("SFL").Element("NickName").Value,
                               ele.Element("PhyRef").Value + "," + ele.Element("RegRef").Value + "," + ele.Element("SubType").Value + "," + ele.Element("Private").Element("SFL").Element("EditType").Value + ","+ ele.Element("Private").Element("SFL").Element("Format").Value
                           }).ToList();
            strpair.OrderBy(o => o[0]).ToList();
            foreach (var i in strpair)
            {
                TokenContent.Add(i[0], i[1]);
                BoardTokenContent.Add(i[0], i[1]);
            }

            FileStream fs = new FileStream(dllfilefullpath, FileMode.Open);
            BinaryReader br = new BinaryReader(fs);
            COBRA_GLOBAL.CurrentDLLToken = SharedAPI.GetMD5(br.ReadBytes((int)fs.Length));
            br.Close();
            fs.Close();
            DLLTokenContent.Add(dllfilename, COBRA_GLOBAL.CurrentDLLToken);
            TokenContent.Add(dllfilename, COBRA_GLOBAL.CurrentDLLToken);

            //return str;
            var CurrentOCEToken = GetStringFromDic(TokenContent);

            COBRA_GLOBAL.CurrentOCEToken = SharedAPI.GetMD5(CurrentOCEToken);


            //COBRA_GLOBAL.CurrentDLLToken = GetStringFromDic(DLLTokenContent);

            var CurrentParamToken = GetStringFromDic(ParamTokenContent);

            COBRA_GLOBAL.CurrentParamToken = SharedAPI.GetMD5(CurrentParamToken);

            var CurrentBoardToken = GetStringFromDic(BoardTokenContent);

            COBRA_GLOBAL.CurrentBoardToken = SharedAPI.GetMD5(CurrentBoardToken);
        }

        private string GetStringFromDic(Dictionary<string, string> tokenContent)
        {
            String str = String.Empty;      //以string形式返回Token
            foreach (var k in tokenContent.Keys)
            {
                str += k + ":" + tokenContent[k] + ";";
            }
            return str;
        }
    }
}