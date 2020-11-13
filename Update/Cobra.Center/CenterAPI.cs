using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Data;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Cobra.Common;

namespace Cobra.Center
{
    public class CenterAPI
    {
        #region private members defintion
        private WebRequest wreqCenter;
        private WebResponse wresponseCenter;
        private Stream strmCenter;
        private StreamReader sreadReader;
        private StreamWriter swriteWriter;
        #endregion

        #region public members definition
        //settings variable
        //public string strCenterDocLocation = FolderMap.m_center_folder;
        public string strCenterFilename = "cobracenterfile.zip";
        public string strServerIPAddr = "https://cobra.o2micro.com";// "10.22.1.88";
        public string strServerPort = "443";//"80";
        public string strServerURLValue = "cobracenteroauth2/oauth/token";
        public string strGrantName = "grant_type=";
        public string strGrantValue = "password";
        public string strGrantRefresh = "refresh_token";
        public string strClientidName = "client_id=";
        public string strClientidValue = "a9ea7639affb7be85c582f5ae68cdabfc5c8f82f40b1927ea7157189147c48f0";//"0035706a8921d6faec2e763b8dbe90a188efcb489eff86d7f05aa9dcfaaa2e1a";
        public string strClientsecretName = "client_secret=";
        public string strClientsecretValue = "51436d1ab67a1b29ce2c374189049b666cc0f8642fa689572ec56f6e4c077263";//"ec88c67ce01f6435a2ded72cea130329137200d2dfb243391a9e96af46534e5e";
        public string strUseremailName = "email=";
        public string strUserenmailValue = "user.cobra@o2micro.com";
        public string strPasswordName = "password=";
        public string strPasswordValue = "886225459095";
        public string strRefreshName = "refresh_token";
        public string strRefreshValue = "";
        public string strRedirectURIName = "redirect_uri";
        public string strRedirectURIValue = "https://www.getpostman.com/oauth2/callback";
        public string strTokenValue = "";

        public List<ReleaseCenter> listreleaseCobra;
        public List<ReleaseFile> listfileCobra;
        public List<HwPlatform> listhwPlatForm;
        public List<CategoryProject> listcategoryProject;
        public List<OsPlatform> listosPlatform;
        public List<CategoryFile> listcategoryFile;
        public DataTable dtProjectControl;
        public DataTable dtReleaseCenter;
        public DataTable dtReleaseFile;
        public DataTable dtHwPlatform;
        public DataTable dtCategoryProject;
        public DataTable dtOsPlatform;
        public DataTable dtCategoryFile;
        public UserAccount myAccountInfo;
        public UInt32 uErrCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
        #endregion

        #region constructor/destructor
        public CenterAPI()
        {
        }

        ~CenterAPI()
        {
        }
        #endregion
        
        #region private methods definition
        private bool openZipfile_getOAuth()
        {
            bool bRet = true;
            return bRet;
        }

        private bool saveZipfile()
        {
            bool bRet = true;

            return bRet;
        }

        private string makeURLString(bool bRefresh = false)
        {
            string strHttpLink;

            if (strServerPort.Length != 0)
                strHttpLink = strServerIPAddr + ":" + strServerPort + "/" + strServerURLValue;//"http://" + strServerIPAddr + ":" + strServerPort + "/" + strServerURLValue;
            else
                strHttpLink = strServerIPAddr + "/" + strServerURLValue;//"http://" + strServerIPAddr + "/" + strServerURLValue;
            strHttpLink += "?" + strClientidName + strClientidValue;
            strHttpLink += "&" + strClientsecretName + strClientsecretValue;
            if (!bRefresh)
            {
                strHttpLink += "&" + strGrantName + strGrantValue;
                strHttpLink += "&" + strUseremailName + strUserenmailValue;
                strHttpLink += "&" + strPasswordName + strPasswordValue;
            }
            else
            {
                strHttpLink += "&" + strGrantName + strGrantRefresh;
                strHttpLink += "&" + strRedirectURIName + strRedirectURIValue;
                strHttpLink += "&" + strRefreshName + strRefreshValue;
            }
            return strHttpLink;
        }

        private bool getAuthorizedToken(ref string erMessage,bool bRefresh = false)
        {
            bool bRet = true;
            string strHttpLink;
            string strContent = "";

            strHttpLink = makeURLString(bRefresh);
            wreqCenter = WebRequest.Create(strHttpLink);
            wreqCenter.Method = "POST";
            wreqCenter.Timeout = 5000;
            try
            {
                strmCenter = wreqCenter.GetRequestStream();
                wresponseCenter = wreqCenter.GetResponse();
                strmCenter = wresponseCenter.GetResponseStream();
                sreadReader = new StreamReader(strmCenter);
                strContent = sreadReader.ReadToEnd();
                JObject jo = JObject.Parse(strContent);

                strTokenValue = (string)jo["access_token"];
                strRefreshValue = (string)jo["refresh_token"];
            }
            catch (WebException e)
            {
                bRet = false;
                erMessage = e.Message;
                using (WebResponse response = e.Response)
                {
                    HttpWebResponse httpResponse = (HttpWebResponse)response;
                    if (response == null)
                        uErrCode = LibErrorCode.IDS_ERR_SECTION_CENTER_IP_NACK;
                    else
                    {
                        string strHttpErr = e.ToString();
                        {
                            if (strHttpErr.IndexOf("401") != -1)
                                uErrCode = LibErrorCode.IDS_ERR_SECTION_CENTER_PASSWORD;
                        }
                    }
                }
            }

            return bRet;
        }

        private bool sendAPIGetRequst(string strRequst, out string strResponse, string strJSON = null)
        {
            bool bRet = true;
            string strURLAPIList;
            string strContent = "";
            if (strServerPort.Length != 0)
                strURLAPIList = strServerIPAddr + ":" + strServerPort + "/" + strRequst;//"http://" + strServerIPAddr + ":" + strServerPort + "/" + strRequst;
            else
                strURLAPIList = strServerIPAddr + "/" + strRequst;//"http://" + strServerIPAddr + "/" + strRequst;
            wreqCenter = WebRequest.Create(strURLAPIList);//+strContent);
            wreqCenter.ContentType = "application/json; charset=utf-8";
            wreqCenter.Method = "GET";
            wreqCenter.Headers.Set("Authorization", "Bearer " + strTokenValue);
            try
            {
                if (strJSON != null)
                {
                    swriteWriter = new StreamWriter(wreqCenter.GetRequestStream());
                    swriteWriter.Write(strJSON);
                    swriteWriter.Flush();
                    swriteWriter.Close();
                }
                wresponseCenter = wreqCenter.GetResponse();
                strmCenter = wresponseCenter.GetResponseStream();
                sreadReader = new StreamReader(strmCenter);
                strContent = sreadReader.ReadToEnd();
            }
            catch (Exception e)
            {
                bRet = false;
            }
            strResponse = strContent;

            return bRet;
        }

        private bool sendAPIPostRequst(string strRequst, out string strResponse, string strJSON = null)
        {
            bool bRet = true;
            string strURLAPIList;
            string strContent = "";
            if (strServerPort.Length != 0)
                strURLAPIList = strServerIPAddr + ":" + strServerPort + "/" + strRequst;//"http://" + strServerIPAddr + ":" + strServerPort + "/" + strRequst;
            else
                strURLAPIList = strServerIPAddr + "/" + strRequst; //"http://" + strServerIPAddr + "/" + strRequst;
            wreqCenter = WebRequest.Create(strURLAPIList);//+strContent);
            wreqCenter.ContentType = "application/json; charset=utf-8";
            wreqCenter.ContentLength = Encoding.UTF8.GetBytes(strJSON).Length;
            wreqCenter.Method = "POST";
            wreqCenter.Headers.Set("Authorization", "Bearer " + strTokenValue);
            try
            {
                if (strJSON != null)
                {
                    swriteWriter = new StreamWriter(wreqCenter.GetRequestStream());
                    swriteWriter.Write(strJSON);
                    swriteWriter.Flush();
                    swriteWriter.Close();
                }
                wresponseCenter = wreqCenter.GetResponse();
                strmCenter = wresponseCenter.GetResponseStream();
                sreadReader = new StreamReader(strmCenter);
                strContent = sreadReader.ReadToEnd();
            }
            catch (Exception e)
            {
                bRet = false;
            }
            strResponse = strContent;

            return bRet;
        }

        private DataTable JsonStringToDataTable(string jsonString)
        {
            DataTable dt = new DataTable();
            string[] jsonStringArray = Regex.Split(jsonString.Replace("[", "").Replace("]", ""), "},{");
            List<string> ColumnsName = new List<string>();
            foreach (string jSA in jsonStringArray)
            {
                string[] jsonStringData = Regex.Split(jSA.Replace("{", "").Replace("}", ""), ",");
                foreach (string ColumnsNameData in jsonStringData)
                {
                    try
                    {
                        int idx = ColumnsNameData.IndexOf(":");
                        string ColumnsNameString = ColumnsNameData.Substring(0, idx - 1).Replace("\"", "");
                        if (!ColumnsName.Contains(ColumnsNameString))
                        {
                            ColumnsName.Add(ColumnsNameString);
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(string.Format("Error Parsing Column Name : {0}", ColumnsNameData));
                    }
                }
                break;
            }
            foreach (string AddColumnName in ColumnsName)
            {
                dt.Columns.Add(AddColumnName);
            }
            foreach (string jSA in jsonStringArray)
            {
                string[] RowData = Regex.Split(jSA.Replace("{", "").Replace("}", ""), ",");
                DataRow nr = dt.NewRow();
                foreach (string rowData in RowData)
                {
                    try
                    {
                        int idx = rowData.IndexOf(":");
                        string RowColumns = rowData.Substring(0, idx - 1).Replace("\"", "");
                        string RowDataString = rowData.Substring(idx + 1).Replace("\"", "");
                        nr[RowColumns] = RowDataString;
                    }
                    catch (Exception ex)
                    {
                        continue;
                    }
                }
                dt.Rows.Add(nr);
            }
            return dt;
        }
        #endregion

        #region public methods definition
        public bool setHttpAddressNPort(string strIPAddress, string strInPort)
        {
            bool bRet = true;
            if (strIPAddress.IndexOf("https://") != -1)
            {
                strIPAddress = strIPAddress.Substring(8, strIPAddress.Length - 8);
            }
            else
            {
                strServerIPAddr = strIPAddress;
            }
            strServerPort = strInPort;
            return bRet;
        }

        public void getHttpAddressNPort(out string strIPAddress)
        {
            strIPAddress = strServerIPAddr;
        }

        public bool setUseremailNPasswordforConnect(string struseremail, string struserpassword)
        {
            bool bRet = true;

            strUserenmailValue = struseremail;
            strPasswordValue = struserpassword;
            return bRet;
        }

        public void getUseremailNPasswordforConnect(out string struseremail, out string struserpassword)
        {
            struseremail = strUserenmailValue;
            struserpassword = strPasswordValue;
        }

        public bool connectCobraCenter(ref string erMessage)
        {
            bool bRet = true;

            bRet &= openZipfile_getOAuth();
            if (bRet)
            {
                bRet = getAuthorizedToken(ref erMessage);
            }
            return bRet;
        }

        public bool getUserprofile()
        {
            bool bRet = true;
            string strResponse;

            bRet = sendAPIGetRequst("api/v1/getuserprofile", out strResponse);
            if (bRet)
            {
                try
                {
                    myAccountInfo = JsonConvert.DeserializeObject<UserAccount>(strResponse);
                }
                catch (Exception e)
                {
                    bRet = false;
                }
            }

            return bRet;
        }

        public bool getCobraReleaseInfo()
        {
            bool bRet = true;
            string strAPICommand;
            string strResponse;

            strAPICommand = "api/v1/getallcobrarelease";
            bRet = sendAPIGetRequst(strAPICommand, out strResponse);
            if (bRet)
            {
                try
                {
                    listreleaseCobra = JsonConvert.DeserializeObject<List<ReleaseCenter>>(strResponse);
                    dtReleaseCenter = JsonStringToDataTable(strResponse);
                    dtReleaseCenter.PrimaryKey = new DataColumn[] { dtReleaseCenter.Columns["id"] };
                }
                catch (Exception e)
                {
                    bRet = false;
                }
                if (bRet)
                {
                    foreach (ReleaseCenter rlcenter in listreleaseCobra)
                    {
                        strAPICommand = string.Format("api/v1/getfilelistbyreleaseid?releaseid={0}", rlcenter.id);
                        bRet = sendAPIGetRequst(strAPICommand, out strResponse);
                        try
                        {
                            listfileCobra = JsonConvert.DeserializeObject<List<ReleaseFile>>(strResponse);
                            dtReleaseFile = JsonStringToDataTable(strResponse);
                            dtReleaseFile.PrimaryKey = new DataColumn[] { dtReleaseFile.Columns["id"] };
                        }
                        catch (Exception e)
                        {
                            bRet = false;
                        }
                        if (!bRet) break;   //break foreach loop
                    }
                }
            }

            return bRet;
        }

        public bool getHWPlatformInfo()
        {
            bool bRet = true;
            string strAPICommand;
            string strResponse;

            strAPICommand = "api/v1/gethwplatforms";
            bRet = sendAPIGetRequst(strAPICommand, out strResponse);
            if (bRet)
            {
                try
                {
                    listhwPlatForm = JsonConvert.DeserializeObject<List<HwPlatform>>(strResponse);
                    dtHwPlatform = JsonStringToDataTable(strResponse);
                    dtHwPlatform.PrimaryKey = new DataColumn[] { dtHwPlatform.Columns["id"] };
                }
                catch (Exception e)
                {
                    bRet = false;
                }
            }

            return bRet;
        }

        public bool getCategoryProjectInfo()
        {
            bool bRet = true;
            string strAPICommand;
            string strResponse;

            strAPICommand = "api/v1/getcategoryprojects";
            bRet = sendAPIGetRequst(strAPICommand, out strResponse);
            if (bRet)
            {
                try
                {
                    listcategoryProject = JsonConvert.DeserializeObject<List<CategoryProject>>(strResponse);
                    dtCategoryProject = JsonStringToDataTable(strResponse);
                    dtCategoryProject.PrimaryKey = new DataColumn[] { dtCategoryProject.Columns["id"] };
                }
                catch (Exception e)
                {
                    bRet = false;
                }
            }

            return bRet;
        }

        public bool getOSPlatformInfo()
        {
            bool bRet = true;
            string strAPICommand;
            string strResponse;

            strAPICommand = "api/v1/getallosplatforms";
            bRet = sendAPIGetRequst(strAPICommand, out strResponse);
            if (bRet)
            {
                try
                {
                    listosPlatform = JsonConvert.DeserializeObject<List<OsPlatform>>(strResponse);
                    dtOsPlatform = JsonStringToDataTable(strResponse);
                    dtOsPlatform.PrimaryKey = new DataColumn[] { dtOsPlatform.Columns["id"] };
                }
                catch (Exception e)
                {
                    bRet = false;
                }
            }

            return bRet;
        }

        public bool getCategoryFileInfo()
        {
            bool bRet = true;
            string strAPICommand;
            string strResponse;

            strAPICommand = "api/v1/getcategoryfiles";
            bRet = sendAPIGetRequst(strAPICommand, out strResponse);
            if (bRet)
            {
                try
                {
                    listcategoryFile = JsonConvert.DeserializeObject<List<CategoryFile>>(strResponse);
                    dtCategoryFile = JsonStringToDataTable(strResponse);
                    dtCategoryFile.PrimaryKey = new DataColumn[] { dtCategoryFile.Columns["id"] };
                }
                catch (Exception e)
                {
                    bRet = false;
                }
            }

            return bRet;
        }

        public bool getProjectsAll()
        {
            bool bRet = true;
            string strAPICommand;
            string strResponse;

            strAPICommand = "api/v1/getallprojects";
            bRet = sendAPIGetRequst(strAPICommand, out strResponse);
            if (bRet)
            {
                try
                {
                    dtProjectControl = JsonStringToDataTable(strResponse);
                    dtProjectControl.PrimaryKey = new DataColumn[] { dtProjectControl.Columns["id"] };
                }
                catch (Exception e)
                {
                    bRet = false;
                }
            }

            return bRet;
        }

        public bool getReleasesAll()
        {
            bool bRet = true;
            string strAPICommand;
            string strResponse;

            strAPICommand = "api/v1/getallreleases";
            bRet = sendAPIGetRequst(strAPICommand, out strResponse);
            if (bRet)
            {
                try
                {
                    listreleaseCobra = JsonConvert.DeserializeObject<List<ReleaseCenter>>(strResponse);
                    dtReleaseCenter = JsonStringToDataTable(strResponse);
                    dtReleaseCenter.PrimaryKey = new DataColumn[] { dtReleaseCenter.Columns["id"] };
                }
                catch (Exception e)
                {
                    bRet = false;
                }
            }

            return bRet;
        }

        public bool getReleasefileAll()
        {
            bool bRet = true;
            string strAPICommand;
            string strResponse;

            strAPICommand = "api/v1/getallreleasefiles";
            bRet = sendAPIGetRequst(strAPICommand, out strResponse);
            if (bRet)
            {
                try
                {
                    listfileCobra = JsonConvert.DeserializeObject<List<ReleaseFile>>(strResponse);
                    dtReleaseFile = JsonStringToDataTable(strResponse);
                    dtReleaseFile.PrimaryKey = new DataColumn[] { dtReleaseFile.Columns["id"] };
                }
                catch (Exception e)
                {
                    bRet = false;
                }
            }

            return bRet;
        }

        public bool getPasswordfFomServerByEmail(string streMail, out string strPasswordOut)
        {
            bool bRet = true;
            string strAPICommand;

            strAPICommand = "api/v1/getuserpasswordbyemail";
            bRet = sendAPIGetRequst(strAPICommand, out strPasswordOut);

            return bRet;
        }

        public bool postNewUpdateList(ref List<DownloadCetre> objUpdateList)
        {
            bool bRet = true;
            string strAPICommand;
            string strResponse;
            List<CenterNewUpdateOject> objUpdateTemp = new List<CenterNewUpdateOject>();

            //convert DownloadCetre to mini-version CenterNewUpdateObject, to avoid some empty column cannot be parsed by server
            foreach (DownloadCetre dwnOne in objUpdateList)
            {
                CenterNewUpdateOject objUpOne = new CenterNewUpdateOject(dwnOne.dwnprojectcode, dwnOne.dwnfilemajorver, dwnOne.dwnfilemiddlever, dwnOne.dwnfileminorver, dwnOne.dwnfilename);
                objUpdateTemp.Add(objUpOne);
            }

            string strjson = JsonConvert.SerializeObject(objUpdateTemp);
            strAPICommand = "api/v1/postnewupdatelist";
            bRet = sendAPIPostRequst(strAPICommand, out strResponse, strjson);
            if (bRet)
            {
                try
                {
                    List<DownloadCetre> objUpdatenew = JsonConvert.DeserializeObject<List<DownloadCetre>>(strResponse);
                    for (int i = 0; i < objUpdateList.Count; i++)
                    {
                        objUpdateList[i] = objUpdatenew[i];
                    }
                }
                catch (Exception e)
                {
                    bRet = false;
                }
            }

            return bRet;
        }

        public bool downloadFileFromUri(string strUri, string strLocalFilePath)
        {
            bool bRet = true;
            WebClient myClient = new WebClient();

            myClient.DownloadFile(strUri, strLocalFilePath);

            return bRet;
        }

        public bool setPasswordToServerByEmail(string streMail, string strPassword)
        {
            bool bRet = true;
            string strAPICommand;

            //TBD
            strAPICommand = "api/v1/putuserpasswordbyemail";

            return bRet;
        }

        public bool createNewUser(UserAccount userNew)
        {
            bool bRet = true;

            return bRet;
        }

        public bool doGETRequest(string strRqstCommand, string strRqstParams, out string strRspnReturn)
        {
            bool bRet = true;
            string strAPICommand;

            if (strRqstCommand[0] == '/')
            {
                strAPICommand = "api/v1" + strRqstCommand;
            }
            else
            {
                strAPICommand = "api/v1/" + strRqstCommand;
            }
            if ((strRqstCommand[strRqstCommand.Length - 1] != '?') || (strRqstParams[0] != '?'))
            {
                strAPICommand = strAPICommand + "?" + strRqstParams;
            }
            else
            {
                strAPICommand = strRqstCommand + strRqstParams;
            }
            bRet = sendAPIGetRequst(strAPICommand, out strRspnReturn);

            return bRet;
        }
        #endregion
    }
}