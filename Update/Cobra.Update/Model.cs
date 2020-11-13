using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Windows;
using System.Net;
using System.IO;
using System.Windows.Data;
using System.Globalization;
using Cobra.Common;
using Cobra.Center;

namespace Cobra.Update
{
    public class Model : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private string m_strSettingName;
        public string strSettingName
        {
            get { return m_strSettingName; }
            set { m_strSettingName = value; OnPropertyChanged("strSettingName"); }
        }

        private string m_strSettingValue;
        public string strSettingValue
        {
            get { return m_strSettingValue; }
            set { if (m_strSettingValue == value) return; m_strSettingValue = value; OnPropertyChanged("strSettingValue"); }
        }

        private bool m_bEncrypted;
        public bool bEncrypted
        {
            get { return m_bEncrypted; }
            set { m_bEncrypted = value; OnPropertyChanged("bEncrypted"); }
        }

        public Model(string strIniName, string strIniValue, bool bInEncrypt = false)
        {
            m_strSettingName = strIniName;
            m_strSettingValue = strIniValue;
            m_bEncrypted = bInEncrypt;
        }
    }

    public class FileModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        public DownloadCetre m_downloadCenter = null;

        public string strFileName
        {
            get
            {
                if (m_downloadCenter != null)
                {
                    return m_downloadCenter.dwnfilename;
                }
                else
                {
                    return string.Empty;
                }
            }
            set
            {
                if (m_downloadCenter != null)
                {
                    m_downloadCenter.dwnfilename = value;
                    OnPropertyChanged("strFileName");
                }
            }
        }

        public string strFullName
        {
            get
            {
                try
                {
                    return m_downloadCenter.dwnfilelink.url.Substring(m_downloadCenter.dwnfilelink.url.LastIndexOf("/") + 1);
                }
                catch (System.Exception ex)
                {
                    return string.Empty;                	
                }
            }
        }

        private string m_strNewFileVersion;
        public string strNewFileVersion
        {
            get
            {
                try
                {
                    if (m_downloadCenter.dwnfilelink.url == null)
                    {
                        m_strNewFileVersion = "---";
                    }
                    else
                    {
                        m_strNewFileVersion = string.Format("V{0}.{1}.{2}", iMajorVer, iMiddleVer, iMinnorVer);
                    }
                }
                catch (System.Exception ex)
                {
                    m_strNewFileVersion = "---";
                }
                return m_strNewFileVersion;
            }
            //set { m_strFileVersion = value; OnPropertyChanged("strFileVersion"); }
        }

        public string strFileType
        {
            get
            {
                if (m_downloadCenter != null)
                {
                    return m_downloadCenter.dwnfiletype;
                }
                else
                {
                    return string.Empty;
                }
            }
            set
            {
                if (m_downloadCenter != null)
                {
                    m_downloadCenter.dwnfiletype = value;
                    OnPropertyChanged("strFileType");
                }
            }
        }

        public string strFileDescription
        {
            get
            {
                if (m_downloadCenter != null)
                {
                    return m_downloadCenter.dwnfiledescription;
                }
                else
                {
                    return string.Empty;
                }
            }
            set
            {
                if (m_downloadCenter != null)
                {
                    m_downloadCenter.dwnfiledescription = value;
                    OnPropertyChanged("strFileDescription");
                }
            }
        }

        public string strReleaseCenterID
        {
            get
            {
                if (m_downloadCenter != null)
                {
                    return m_downloadCenter.dwnreleasecenter_id;
                }
                else
                {
                    return string.Empty;
                }
            }
            set
            {
                if (m_downloadCenter != null)
                {
                    m_downloadCenter.dwnreleasecenter_id = value;
                    OnPropertyChanged("strReleaseCenterID");
                }
            }
        }

        private bool m_bShowFiles;
        public bool bShowFiles
        {
            get { return m_bShowFiles; }
            set { m_bShowFiles = value; OnPropertyChanged("bShowFiles"); }
        }

        public bool bDownloadable //是否能下载
        {
            get
            {
                try
                {
                    if (m_downloadCenter.dwnfilelink.url == null)
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
                catch (System.Exception ex)
                {
                    return false;
                }
            }
        }

        private bool m_bDownload = false; //是否已下载
        public bool bdownload
        {
            get { return m_bDownload; }
            set { m_bDownload = value; OnPropertyChanged("bdownload"); }
        }

        private bool m_bCancel = true; //是否已下载
        public bool bcancel
        {
            get { return m_bCancel; }
            set { m_bCancel = value; OnPropertyChanged("bcancel"); }
        }

        private Visibility m_vsiDescription;
        public Visibility vsiDescription
        {
            get { return m_vsiDescription; }
            set { m_vsiDescription = value; OnPropertyChanged("vsiDescription"); }
        }

        private Visibility m_vsiDownload;
        public Visibility vsiDownload
        {
            get
            {
                //if (m_downloadCenter.dwnfilelink.url == null)
                //{
                //return Visibility.Hidden;
                //}
                //else
                {
                    return m_vsiDownload;
                }
            }
            set { m_vsiDownload = value; OnPropertyChanged("vsiDownload"); }
        }

        private Visibility m_vsiProgressbar;
        public Visibility vsiProgressbar
        {
            get { return m_vsiProgressbar; }
            set { m_vsiProgressbar = value; OnPropertyChanged("vsiProgressbar"); }
        }

        private int m_iProgressValue;
        public int iProgressValue
        {
            get { return m_iProgressValue; }
            set { m_iProgressValue = value; OnPropertyChanged("iProgressValue"); }
        }

        private string m_strProgressText;
        public string strProgressText
        {
            get { return m_strProgressText; }
            set { m_strProgressText = value; OnPropertyChanged("strProgressText"); }
        }

        public int iMajorVer
        {
            get
            {
                if (m_downloadCenter != null)
                {
                    return m_downloadCenter.dwnfilemajorver;
                }
                else
                {
                    return -1;
                }
            }
            set
            {
                if (m_downloadCenter != null)
                {
                    m_downloadCenter.dwnfilemajorver = value;
                    OnPropertyChanged("iMajorVer");
                }
            }
        }

        public int iMiddleVer
        {
            get
            {
                if (m_downloadCenter != null)
                {
                    return m_downloadCenter.dwnfilemiddlever;
                }
                else
                {
                    return -1;
                }
            }
            set
            {
                m_downloadCenter.dwnfilemiddlever = value;
                OnPropertyChanged("iMiddleVer");
            }
        }

        public int iMinnorVer
        {
            get
            {
                if (m_downloadCenter != null)
                {
                    return m_downloadCenter.dwnfileminorver;
                }
                else
                {
                    return -1;
                }
            }
            set
            {
                if (m_downloadCenter != null)
                {
                    m_downloadCenter.dwnfileminorver = value;
                    OnPropertyChanged("iMinnorVer");
                }
            }
        }

        private int m_iOldMajorVer;
        public int iOldMajorVer
        {
            get { return m_iOldMajorVer; }
            set { m_iOldMajorVer = value; OnPropertyChanged("iOldMajorVer"); OnPropertyChanged("strOldFileVersion"); }
        }

        private int m_iOldMiddleVer;
        public int iOldMiddleVer
        {
            get { return m_iOldMiddleVer; }
            set { m_iOldMiddleVer = value; OnPropertyChanged("iOldMiddleVer"); OnPropertyChanged("strOldFileVersion"); }
        }

        private int m_iOldMinorVer;
        public int iOldMinorVer
        {
            get { return m_iOldMinorVer; }
            set { m_iOldMinorVer = value; OnPropertyChanged("iOldMinorVer"); OnPropertyChanged("strOldFileVersion"); }
        }

        private string m_strOldFileVersion;
        public string strOldFileVersion
        {
            get { m_strOldFileVersion = string.Format("V{0}.{1}.{2}", m_iOldMajorVer, m_iOldMiddleVer, m_iOldMinorVer); return m_strOldFileVersion; }
        }

        public string strProjectCode
        {
            get
            {
                if (m_downloadCenter != null)
                {
                    return m_downloadCenter.dwnprojectcode;
                }
                else
                {
                    return string.Empty;
                }
            }
            set
            {
                if (m_downloadCenter != null)
                {
                    m_downloadCenter.dwnprojectcode = value;
                    OnPropertyChanged("strProjectCode");
                }
            }
        }

        private VersionInfo m_version_info;
        public VersionInfo versionInfo
        {
            get { return m_version_info; }
            set { m_version_info = value; }
        }

        private O2WebClient m_webClient = new O2WebClient();
        public O2WebClient webClient
        {
            get { return m_webClient; }
            set { m_webClient = value; }
        }

        public FileModel()
        {

        }

        public FileModel(VersionInfo vi)
        {
            m_downloadCenter = new DownloadCetre(vi.Assembly_ProjectCode, vi.Assembly_ver.Major, vi.Assembly_ver.Minor, vi.Assembly_ver.Build, vi.Assembly_name);
            m_iOldMajorVer = vi.Assembly_ver.Major;
            m_iOldMiddleVer = vi.Assembly_ver.Minor;
            m_iOldMinorVer = vi.Assembly_ver.Build;
            m_vsiDescription = Visibility.Visible;
            m_vsiDownload = Visibility.Visible;
            m_vsiProgressbar = Visibility.Hidden;
            versionInfo = vi;

            webClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(webClient_DownloadProgressChanged);
            webClient.DownloadFileCompleted += new System.ComponentModel.AsyncCompletedEventHandler(webClient_DownloadFileCompleted);
        }

        void webClient_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            iProgressValue = e.ProgressPercentage;
            strProgressText = string.Format("{0} KB's / {1} KB's", (e.BytesReceived / 1024d).ToString("0.00"), (e.TotalBytesToReceive / 1024d).ToString("0.00"));
        }

        void webClient_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            vsiProgressbar = Visibility.Hidden;
            vsiDescription = Visibility.Visible;
            if (e.Error != null)
            {
                strFileDescription = e.Error.Message;
                bdownload = false;
            }
            else
            {
                strFileDescription = "Download successfully!";
                bdownload = true;
            }
            bcancel = true;
        }
    }

    public class ReleaseCenterModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private string m_strReleaseID;
        public string strReleaeID
        {
            get { return m_strReleaseID; }
            set { m_strReleaseID = value; }
        }

        private string m_strReleaseSummary;
        public string strReleaseSummary
        {
            get { return m_strReleaseSummary; }
            set { m_strReleaseSummary = value; OnPropertyChanged("strReleaseSummry"); }
        }

        private string m_strReleaseCategory;
        public string strReleaseCategory
        {
            get { return m_strReleaseCategory; }
            set { m_strReleaseCategory = value; OnPropertyChanged("strReleaseCategory"); }
        }

        //private string m_strReleaseFilesNumber;
        public string strReleaseFilesNumber
        {
            get { return string.Format("{0}", m_iReleaseFilesNubmer);/*m_strReleaseFilesNumber;*/ }
            set
            {
                //m_strReleaseFilesNumber = value; 
                if (!Int32.TryParse(value, out m_iReleaseFilesNubmer))
                {
                    m_iReleaseFilesNubmer = 0;
                }
                OnPropertyChanged("strReleaseFilesNumber");
            }
        }

        private string m_strReleaseDate;
        public string strReleaseDate
        {
            get { return m_strReleaseDate; }
            set { m_strReleaseDate = value; OnPropertyChanged("strReleaseDate"); }
        }

        private bool m_bShowMyFiles;
        public bool bShowMyFiles
        {
            get { return m_bShowMyFiles; }
            set { m_bShowMyFiles = value; }//OnPropertyChanged("bShowMyFiles"); }
        }

        private Int32 m_iReleaseFilesNubmer;

        public ReleaseCenterModel(ReleaseCenter rlcRecord)
        {
            m_strReleaseSummary = rlcRecord.releasesummary;
            m_strReleaseCategory = string.Format("{0}", rlcRecord.projectcontrol_id);
            m_strReleaseDate = rlcRecord.created_at;
            m_bShowMyFiles = false;
        }

        public ReleaseCenterModel(DataRow rowIn, DataTable dtCatProjectIn, DataTable dtProjectIn)
        {
            m_strReleaseID = string.Format("{0}", rowIn["id"]);
            m_strReleaseSummary = string.Format("{0}", rowIn["releasesummary"]);
            DataRow drResultPrj = dtProjectIn.Rows.Find(rowIn["projectcontrol_id"]);
            DataRow drResultCat = dtCatProjectIn.Rows.Find(drResultPrj["categoryproject_id"]);
            m_strReleaseCategory = string.Format("{0}", drResultCat["categoryprojectstring"]);
            m_strReleaseDate = string.Format("{0}", rowIn["created_at"]);
            m_bShowMyFiles = false;
            m_iReleaseFilesNubmer = 0;
        }

        public void connectReleaseFile(ObservableCollection<FileModel> lstFilesIn)
        {
            foreach (FileModel crelfileOne in lstFilesIn)
            {
                if (string.Equals(m_strReleaseID, crelfileOne.strReleaseCenterID))
                {
                    crelfileOne.bShowFiles = m_bShowMyFiles;
                    m_iReleaseFilesNubmer += 1;
                }
            }

            return;
        }
    }

    internal class ReleaseViewMode
    {
        private ObservableCollection<ReleaseCenterModel> m_CenterReleaseList = new ObservableCollection<ReleaseCenterModel>();
        public ObservableCollection<ReleaseCenterModel> CenterReleaseList
        {
            get { return m_CenterReleaseList; }
            set { m_CenterReleaseList = value; }
        }

        private ObservableCollection<FileModel> m_CenterReleaseFileList = new ObservableCollection<FileModel>();
        public ObservableCollection<FileModel> CenterReleaseFileList
        {
            get { return m_CenterReleaseFileList; }
            set { m_CenterReleaseFileList = value; }
        }

        public ReleaseViewMode()
        {
            m_CenterReleaseList.Clear();
        }

        public bool sumFileNumbersfromRelaseFile()
        {
            bool bRet = true;

            return bRet;
        }

    }
}
