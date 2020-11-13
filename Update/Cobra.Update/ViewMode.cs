using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Data;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using Cobra.Common;
using Cobra.Center;

namespace Cobra.Update
{
    public class ViewMode : CenterAPI
    {
        #region internal members definition
        internal ObservableCollection<Model> connectSetList = null;
        internal List<DownloadCetre> myATUDownloadList = null;
        internal ObservableCollection<FileModel> myFileList = null;
        #endregion

        public ViewMode()
        {
            initConnectSetList();
            initializeAutoUpdateList();
        }

        ~ViewMode()
        {

        }
        
        public void initConnectSetList()
        {
            if (connectSetList == null)
            {
                connectSetList = new ObservableCollection<Model>();
                Model settingIPAddr = new Model("Ip Address:", strServerIPAddr);
                connectSetList.Add(settingIPAddr);
                Model settingPort = new Model("Port:", strServerPort);
                connectSetList.Add(settingPort);
                Model settingEmail = new Model("Login email:", strUserenmailValue);
                connectSetList.Add(settingEmail);
                Model settingPassword = new Model("Login password:", strPasswordValue, true);
                connectSetList.Add(settingPassword);
            }
            else
            {
                connectSetList[0].strSettingValue = strServerIPAddr;
                connectSetList[1].strSettingValue = strServerPort;
                connectSetList[2].strSettingValue = strUserenmailValue;
                connectSetList[3].strSettingValue = strPasswordValue;
            }
        }

        public void syncSettingList()
        {
            strServerIPAddr = connectSetList[0].strSettingValue;
            strServerPort = connectSetList[1].strSettingValue;
            strUserenmailValue = connectSetList[2].strSettingValue;
            strPasswordValue = connectSetList[3].strSettingValue;
        }

        #region Release View operation
        public void initializeAutoUpdateList()
        {
            if(myATUDownloadList == null)
            {
                myATUDownloadList = new List<DownloadCetre>();
            }
            else
            {
                myATUDownloadList.Clear();
            }

            if(myFileList == null)
            {
                myFileList = new ObservableCollection<FileModel>();
            }
            else
            {
                myFileList.Clear();
            }
        }

        //Add module name and version into AutoUpdate list
        //public void addModuleNameVersionToList(string strModuleName, int iMajor, int iMiddle, int iMinor, string strFilename = null)
        public void addModuleNameVersionToList(VersionInfo vi)
        {
            FileModel fileObj = new FileModel(vi);
            myFileList.Add(fileObj);
        }

        public bool checkServerWithList(bool bRSearch = false)
        {
            bool bRet = true;

            myATUDownloadList.Clear();
            foreach(FileModel fileOne in myFileList)
            {
                if (fileOne.versionInfo.ErrorCode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                {
                    fileOne.strFileDescription = LibErrorCode.GetErrorDescription(fileOne.versionInfo.ErrorCode);
                    continue;
                }
                if (!bRSearch)
                {
                    fileOne.m_downloadCenter.dwnfilemajorver = fileOne.iOldMajorVer;
                    fileOne.m_downloadCenter.dwnfilemiddlever = fileOne.iOldMiddleVer;
                    fileOne.m_downloadCenter.dwnfileminorver = fileOne.iOldMinorVer;
                }
                myATUDownloadList.Add(fileOne.m_downloadCenter);
            }
            bRet = postNewUpdateList(ref myATUDownloadList);
            if(bRet)
            {
                for(int i=0; i<myATUDownloadList.Count; i++)
                {
                    myFileList[i].m_downloadCenter = myATUDownloadList[i];
                }
            }
            else
            {

            }

            return bRet;
        }
        #endregion
    }
}
