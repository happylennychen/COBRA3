using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cobra.Common;

namespace Cobra.Center
{
    public class UserAccount
    {
        public int id;
        public string email;
        public string role;
        public string created_at;
        public string updated_at;
        public string firstname;
        public string lastname;
        public string company;
        public string cellphone;
    }

    public class HwPlatform
    {
        public int id;
        public string hwnickname;
        public string hwcode;
        public string hwdescription;
        public string hwmajorversion;
        public string hwminorversion;
        public string created_at;
    }

    public class OsPlatform
    {
        public int id;
        public int categoryproject;
        public string osdescribe;
        public int osorder;
        public string created_at;
    }

    public class CategoryProject
    {
        public int id;
        public string categoryprojectstring;
        public int categoryprojectorder;
        public string created_at;
    }

    public class CategoryFile
    {
        public int id;
        public string categoryfilestring;
        public int categoryfileorder;
        public string created_at;
    }

    public class ReleaseCenter
    {
        public int id;
        public int projectcontrol_id;
        public string releasesummary;
        public string releasedescription;
        public int reporter_id;
        public int assigner_id;
        //public int releasehwplatform_id;
        public int releasestatus_id;
        public string created_at;
    }

    public class DownloadFileObject
    {
        public string url;
    }

    public class ReleaseFile
    {
        public int id;
        public int releasecenter_id;
        public string relfilename;
        public string relfiledescription;
        public int relfilemajorver;
        public int relfilemiddlever;
        public int relfileminorver;
        public string relfilevalid;     //bool
        public int reporter_id;
        public int projectcontrol_id;
        public int categoryfile_id;
        public string created_at;
        public DownloadFileObject reluploadfile;
    }

    //this one is used for server that return data to client after POST method finished
    public class DownloadCetre
    {
        public int id;
        public string dwnprojectcode;
        public string dwnfilename;
        public int dwnfilemajorver;
        public int dwnfilemiddlever;
        public int dwnfileminorver;             //upper are filled up by client
        public string dwnprojectcontrol_id;     //below are filled up by server     //make it as string type, to prevent null return
        public string dwnreleasecenter_id;      //make it as string type, to prevent null return
        public int dwnuser_id;                  //will must have value, so no need to make string type
        public string dwnreleasefile_id;        //make it as string type, to prevent null return
        public string dwnfiletype;
        public string dwnfiledescription;
        public string dwndownloadfinished;      //bool
        public string dwnfinishedtime;
        public string created_on;
        public DownloadFileObject dwnfilelink;
        public DownloadCetre(string ProjectCodeIn, int MajorVerIn, int MiddleVerIn, int MinnorVerIn, string FileNameIn)
        {
            dwnprojectcode = ProjectCodeIn;
            dwnfilename = FileNameIn;
            dwnfilemajorver = MajorVerIn;
            dwnfilemiddlever = MiddleVerIn;
            dwnfileminorver = MinnorVerIn;
        }
    }

    //beacus server cannot convert json string to timestamp to save in DB, so that, seperate to 2 database
    //this one is used for client that preparing data and call POST method
    public class CenterNewUpdateOject
    {
        public string dwnprojectcode;
        public string dwnfilename;
        public int dwnfilemajorver;
        public int dwnfilemiddlever;
        public int dwnfileminorver;             //upper are filled up by client

        public CenterNewUpdateOject(string strProjectcodeIn,
                            int strMajorversionIn,
                            int strMiddleversionIn,
                            int strMinorversionIn,
                            string strFilenameIn = null)
        {
            dwnprojectcode = strProjectcodeIn;
            dwnfilename = strFilenameIn;
            dwnfilemajorver = strMajorversionIn;
            dwnfilemiddlever = strMiddleversionIn;
            dwnfileminorver = strMinorversionIn;
        }
    }
}
