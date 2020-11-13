using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Cobra.Common;

namespace Cobra.TableMaker
{
    public class ViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private string m_RawFileName;
        public string RawFileName
        {
            get { return m_RawFileName; }
            set
            {
                m_RawFileName = value;
                OnPropertyChanged("RawFileName");
            }
        }

        private TypeEnum m_SrcFileType = TypeEnum.ErrorType;
        public TypeEnum SrcFileType
        {
            get { return m_SrcFileType; }
            set
            {
                m_SrcFileType = value;
                OnPropertyChanged("SrcFileType");
            }
        }

        private string m_Rawfolder;
        public string Rawfolder
        {
            get { return m_Rawfolder; }
            set { m_Rawfolder = value; }
        }

        private string m_Sourcefolder;
        public string Sourcefolder
        {
            get { return m_Sourcefolder; }
            set { m_Sourcefolder = value; }
        }

        private string m_CFGfolder;
        public string CFGfolder
        {
            get { return m_CFGfolder; }
            set { m_CFGfolder = value; }
        }

        private string m_OutPutfolder;
        public string OutPutfolder
        {
            get { return m_OutPutfolder; }
            set
            {
                m_OutPutfolder = value;
                OnPropertyChanged("OutPutfolder");
            }
        }

        private UInt32 m_OCVlow;
        public UInt32 OCVlow
        {
            get { return m_OCVlow; }
            set { m_OCVlow = value; }
        }

        private UInt32 m_OCVhigh;
        public UInt32 OCVhigh
        {
            get { return m_OCVhigh; }
            set { m_OCVhigh = value; }
        }

        private ObservableCollection<SourceFile> m_sourcelist = new ObservableCollection<SourceFile>();
        public ObservableCollection<SourceFile> sourcelist    //source的集合
        {
            get { return m_sourcelist; }
            set
            {
                m_sourcelist = value;
                //OnPropertyChanged("sourcelist");
            }
        }

        private ObservableCollection<VoltagePoint> m_voltagelist = new ObservableCollection<VoltagePoint>();
        public ObservableCollection<VoltagePoint> voltagelist    //source的集合
        {
            get { return m_voltagelist; }
            set
            {
                m_voltagelist = value;
                //OnPropertyChanged("voltagelist");
            }
        }

        private ObservableCollection<CurrentPoint> m_currentlist = new ObservableCollection<CurrentPoint>();
        public ObservableCollection<CurrentPoint> currentlist    //source的集合
        {
            get { return m_currentlist; }
            set
            {
                m_currentlist = value;
                //OnPropertyChanged("voltagelist");
            }
        }

        private ObservableCollection<HeaderItem> m_header = new ObservableCollection<HeaderItem>();
        public ObservableCollection<HeaderItem> header    //source的集合
        {
            get { return m_header; }
            set
            {
                m_header = value;
                //OnPropertyChanged("voltagelist");
            }
        }

        public ViewModel(string Rawstr, string Sourcestr, string CFGstr, string OutPutstr)  //指定foldername
        {
            Rawfolder = Rawstr;
            Sourcefolder = Sourcestr;
            CFGfolder = CFGstr;
            OutPutfolder = OutPutstr;
        }
    }
    public class SourceFile : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private ViewModel m_parent;
        public ViewModel parent
        {
            get { return m_parent; }
            set { m_parent = value; }
        }

        private string m_filename;
        public string filename
        {
            get { return m_filename; }
            set
            {
                m_filename = value;
                OnPropertyChanged("filename");
            }
        }

        private long m_filesize;
        public long filesize
        {
            get { return m_filesize; }
            set
            {
                m_filesize = value;
                OnPropertyChanged("filesize");
            }
        }

        public SourceFile(string str, ViewModel p = null)       //(M170313)Francis, ViewModel can be null
        {
            filename = str;
            parent = p;
        }

        public void Delete()  //将logdata删除
        {
            if(parent != null)      //(A170313)Francis, in case it is null
                parent.sourcelist.Remove(this);
        }
    }
    public class VoltagePoint : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private string m_Voltage;
        public string Voltage
        {
            get { return m_Voltage; }
            set
            {
                m_Voltage = value;
                OnPropertyChanged("Voltage");
            }
        }

        public VoltagePoint()
        {
            Voltage = "";
        }
    }
    public class CurrentPoint : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private string m_Current;
        public string Current
        {
            get { return m_Current; }
            set
            {
                m_Current = value;
                OnPropertyChanged("Current");
            }
        }

        public CurrentPoint()
        {
            Current = "";
        }
    }
    public class HeaderItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private string m_Item;
        public string Item
        {
            get { return m_Item; }
            set
            {
                m_Item = value;
                OnPropertyChanged("Item");
            }
        }

        private string m_Value;
        public string Value
        {
            get { return m_Value; }
            set
            {
                m_Value = value;
                OnPropertyChanged("Value");
            }
        }

        private ushort m_Type;
        public ushort Type
        {
            get { return m_Type; }
            set
            {
                m_Type = value;
                OnPropertyChanged("Type");
            }
        }
        private ObservableCollection<string> m_ItemList = new ObservableCollection<string>();
        public ObservableCollection<string> itemlist
        {
            get { return m_ItemList; }
            set
            {
                m_ItemList = value;
                OnPropertyChanged("itemlist");
            }
        }

        private UInt16 m_ListIndex;
        public UInt16 listindex
        {
            get { return m_ListIndex; }
            set
            {
                //if (m_ListIndex != value)
                {
                    m_ListIndex = value;
                    OnPropertyChanged("listindex");
                }
            }
        }
    }

    public class OldTable :INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private string m_strOCVTXTFileFullPath = "test OCV";
        public string strOCVTXTFileFullPath
        {
            get { return m_strOCVTXTFileFullPath; }
            set
            { 
                m_strOCVTXTFileFullPath = value;
                myParentControl.txbOCVFileOpen.Text = m_strOCVTXTFileFullPath;
                OnPropertyChanged("strOCVTXTFileFullPath"); 
            }
        }

        private string m_strRCTXTFileFullPath = "test RC";
        public string strRCTXTFileFullPath
        {
            get { return m_strRCTXTFileFullPath; }
            set 
            { 
                m_strRCTXTFileFullPath = value;
                myParentControl.txbRCFileOpen.Text = m_strRCTXTFileFullPath;
                OnPropertyChanged("strRCTXTFileFullPath"); 
            }
        }

        private ObservableCollection<SourceFile> m_OldTableFiles = new ObservableCollection<SourceFile>();
        public ObservableCollection<SourceFile> OldTableFiles
        {
            get { return m_OldTableFiles; }
            set
            {
                m_OldTableFiles = value;
                OnPropertyChanged("OldTableFiles");
            }
        }

        private bool m_bOCVtxtFileValid = false;
        public bool bOCVtxtFileValid
        {
            get { return m_bOCVtxtFileValid; }
            set
            {
                m_bOCVtxtFileValid = value;
                //myParentControl.btnCommitFiles.IsEnabled = (m_bOCVtxtFileValid & m_bRCtxtFileValid & m_bCHFilesValid);
                myParentControl.btnCommitFiles.IsEnabled = (m_bOCVtxtFileValid & m_bRCtxtFileValid) || (m_bCHFilesValid);
            }
        }

        private bool m_bRCtxtFileValid = false;
        public bool bRCtxtFileValid
        {
            get { return m_bRCtxtFileValid; }
            set
            {
                m_bRCtxtFileValid = value;
                //myParentControl.btnCommitFiles.IsEnabled = (m_bOCVtxtFileValid & m_bRCtxtFileValid & m_bCHFilesValid);
                myParentControl.btnCommitFiles.IsEnabled = (m_bOCVtxtFileValid & m_bRCtxtFileValid) || (m_bCHFilesValid);
            }
        }

        private bool m_bCHFilesValid = false;
        public bool bCHFilesValid
        {
            get { return m_bCHFilesValid; }
            set
            {
                m_bCHFilesValid = value;
                //myParentControl.btnCommitFiles.IsEnabled = (m_bOCVtxtFileValid & m_bRCtxtFileValid & m_bCHFilesValid);
                myParentControl.btnCommitFiles.IsEnabled = (m_bOCVtxtFileValid & m_bRCtxtFileValid) || (m_bCHFilesValid);
            }
        }

        private UInt32 m_uErrCode = LibErrorCode.IDS_ERR_SUCCESSFUL;
        public UInt32 uErrCode
        {
            get { return m_uErrCode; }
            set { m_uErrCode = value;  }
        }

        private TableSample myParentSample = null;
        private MainControl myParentControl = null;

        public OldTable(TableSample comeinSample, MainControl comeinControl)
        {
            myParentSample = comeinSample;
            myParentControl = comeinControl;
        }

        public void clearAllFiles()
        {
            strOCVTXTFileFullPath = "";
            strRCTXTFileFullPath = "";
            OldTableFiles.Clear();
            bOCVtxtFileValid = false;
            bRCtxtFileValid = false;
            bCHFilesValid = false;
            myParentControl.btnOCVFileOpen.IsEnabled = true;
            myParentControl.btnRCFileOpen.IsEnabled = true;
            myParentControl.btnCHFileOpen.IsEnabled = true;
        }

        public bool checkFilesValid()
        {
            //myParentControl.btnOCVFileOpen.IsEnabled = !(m_bOCVtxtFileValid & m_bRCtxtFileValid & m_bCHFilesValid);
            //myParentControl.btnRCFileOpen.IsEnabled = !(m_bOCVtxtFileValid & m_bRCtxtFileValid & m_bCHFilesValid);
            //myParentControl.btnCHFileOpen.IsEnabled = !(m_bOCVtxtFileValid & m_bRCtxtFileValid & m_bCHFilesValid);
            //(bOCVtxtFileValid & bRCtxtFileValid & bCHFilesValid);
            bool bReturn = !((m_bOCVtxtFileValid & m_bRCtxtFileValid) || (m_bCHFilesValid));

            myParentControl.btnOCVFileOpen.IsEnabled = bReturn;
            myParentControl.btnRCFileOpen.IsEnabled = bReturn;
            myParentControl.btnCHFileOpen.IsEnabled = bReturn;

            return !bReturn;
        }

        public bool readOCVtxtFileContent()
        {
            //bool bReturn = false;

            bOCVtxtFileValid = myParentSample.readOCVtxtFileContent(m_strOCVTXTFileFullPath, ref m_uErrCode);


            return bOCVtxtFileValid;
        }

        public bool readRCtxtFileContent()
        {
            //bool bReturn = false;

            bRCtxtFileValid = myParentSample.readRCtxtFileContent(m_strRCTXTFileFullPath, ref m_uErrCode);

            return bRCtxtFileValid;
        }

        public bool readCHFilesContent()
        {
            //bool bReturn = false;
            List<string> lstFiles = new List<string>();

            foreach(SourceFile sfil in OldTableFiles)
            {
                lstFiles.Add(sfil.filename);
            }
            bCHFilesValid = myParentSample.readCHFilesContent(lstFiles, ref m_uErrCode);

            return bCHFilesValid;
        }

        public bool convertToNewTables(string strOutFolder, List<string> lstInUsers)
        {
            bool bReturn = false;

            bReturn = myParentSample.makeFalconLYTabe(strOutFolder, lstInUsers, ref m_uErrCode);

            return bReturn;
        }

        public string getLastErrorDescription()
        { return LibErrorCode.GetErrorDescription(m_uErrCode); }
    }
}
