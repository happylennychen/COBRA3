using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using O2Micro.Cobra.Common;
using System.IO;
using System.Windows.Controls.Primitives;
using System.Data;
using System.ComponentModel;
using System.Collections.Specialized;


namespace O2Micro.Cobra.AutoMationTest
{
    /// <summary>
    /// Interaction logic for LogViewPanel.xaml
    /// </summary>
    public partial class LogViewPanel : Window
    {

        #region 变量定义

        /*private CobraLog m_testlog;
        public CobraLog testlog
        {
            get { return m_testlog; }
            set { m_testlog = value; }
        }*/

        /*private SummaryInfo m_summaryInfo = new SummaryInfo();
        public SummaryInfo summaryInfo
        {
            get { return m_summaryInfo; }
            set { m_summaryInfo = value; }
        }*/

        /*private LogUIData m_logUIdata = new LogUIData();
        public LogUIData logUIdata
        {
            get { return m_logUIdata; }
            set { m_logUIdata = value; }
        }*/
        #endregion

        public LogViewPanel()
        {
            InitializeComponent();
            //parent = (MainWindow)this.Owner;
            #region Data binding
            logfilelist.ItemsSource = AutomationTestLog.cl.logdatalist; //testlog.logdatalist;
            loguidatagrid.DataContext = AutomationTestLog.logUIdata.logbuf;
            SummeryGrid.DataContext = GlobalData.summaryInfo;
            #endregion

            //DataTable dt = (DataTable)loguidatagrid.DataContext;
            AutomationTestLog.cl.logdatalist.CollectionChanged += new NotifyCollectionChangedEventHandler(logdatalist_CollectionChanged);
            AutomationTestLog.logUIdata.logbuf.TableNewRow += new DataTableNewRowEventHandler(logbuf_TableNewRow);
            //RebuildUISourceFromList();
        }

        private void RebuildUISourceFromList()
        {
            /*#region 根据TestLog.LogHeaders中的数据来初始化DataTable的column以及isDisplay
            List<string> strlist = new List<string>();
            foreach (string logHeader in AutomationTestLog.LogHeaders)
            {
                strlist.Add(logHeader);
            }
            //AutomationTestLog.logUIdata.logbuf.Clear();   //Clear all the history data before the scan start.
            AutomationTestLog.logUIdata.logbuf.Columns.Clear();
            AutomationTestLog.logUIdata.BuildColumn(strlist, true);
            #endregion*/

        }


        private void ViewFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            string str = AutomationTestLog.cl.folder;
            System.Diagnostics.Process.Start("explorer.exe", str);
        }

        private void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            int count = logfilelist.SelectedItems.Count;
            for (int i = count; i > 0; i--)
            {
                LogData lg = (LogData)logfilelist.SelectedItems[i - 1];
                lg.Delete();
            }

        }

        private void LoadBtn_Click(object sender, RoutedEventArgs e)    //先解绑，再跟UI绑定，提升速度
        {
            /*Cursor = Cursors.Wait;
            RebuildUISourceFromList();
            loguidatagrid.DataContext = null;

            List<LogParam> paramlist = new List<LogParam>();
            foreach (Parameter param in dynamicdatalist.parameterlist)
            {
                LogParam logp = new LogParam();
                logp.name = GetHashTableValueByKey("LogName", param.sfllist[sflname].nodetable);
                logp.group = GetHashTableValueByKey("Group", param.sfllist[sflname].nodetable);
                paramlist.Add(logp);
            }
            if (logfilelist.SelectedIndex == -1)
                return;
            string filename = testlog.folder + testlog.logdatalist[logfilelist.SelectedIndex].logname;
            logUIdata.LoadFromFile(filename, paramlist);


            loguidatagrid.DataContext = logUIdata.logbuf;
            Cursor = Cursors.Arrow;*/
        }


        private void logfilelist_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            string headername = e.Column.Header.ToString();

            //Cancel the column you don't want to generate
            if (headername == "parent")
            {
                e.Cancel = true;
            }
            if (headername == "haveheader")
            {
                e.Cancel = true;
            }
            if (headername == "logbuf")
            {
                e.Cancel = true;
            }
        }

        private void logdatalist_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (logfilelist.Items.Count != 0)
                logfilelist.ScrollIntoView(logfilelist.Items[logfilelist.Items.Count - 1]);
        }

        private void logbuf_TableNewRow(object sender, DataTableNewRowEventArgs e)
        {
            if (loguidatagrid.Items.Count != 0)
                loguidatagrid.ScrollIntoView(loguidatagrid.Items[loguidatagrid.Items.Count - 1]);
        }
    }


    public class GlobalData
    {
        static private LogViewPanel m_lvp;// = new LogViewPanel();
        static public LogViewPanel lvp
        {
            get { return m_lvp; }
            set { m_lvp = value; }
        }

        static private SummaryInfo m_summaryInfo = new SummaryInfo();
        static public SummaryInfo summaryInfo
        {
            get { return m_summaryInfo; }
            set { m_summaryInfo = value; }
        }
    }

    public class SummaryInfo : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private static string m_gpec;
        public string GPEC
        {
            get { return m_gpec; }
            set
            {
                m_gpec = value;
                OnPropertyChanged("GPEC");
            }
        }

        private static string m_gcrc;
        public string GCRC
        {
            get { return m_gcrc; }
            set
            {
                m_gcrc = value;
                OnPropertyChanged("GCRC");
            }
        }

        private static string m_gomax;
        public string GoMax
        {
            get { return m_gomax; }
            set
            {
                m_gomax = value;
                OnPropertyChanged("GoMax");
            }
        }

        private static string m_gomin;
        public string GoMin
        {
            get { return m_gomin; }
            set
            {
                m_gomin = value;
                OnPropertyChanged("GoMin");
            }
        }

        private static string m_gtol;
        public string GTol
        {
            get { return m_gtol; }
            set
            {
                m_gtol = value;
                OnPropertyChanged("GTol");
            }
        }

        private static string m_cpec;
        public string CPEC
        {
            get { return m_cpec; }
            set
            {
                m_cpec = value;
                OnPropertyChanged("CPEC");
            }
        }

        private static string m_ccrc;
        public string CCRC
        {
            get { return m_ccrc; }
            set
            {
                m_ccrc = value;
                OnPropertyChanged("CCRC");
            }
        }

        private static string m_chomax;
        public string ChoMax
        {
            get { return m_chomax; }
            set
            {
                m_chomax = value;
                OnPropertyChanged("ChoMax");
            }
        }

        private static string m_chomin;
        public string ChoMin
        {
            get { return m_chomin; }
            set
            {
                m_chomin = value;
                OnPropertyChanged("ChoMin");
            }
        }

        private static string m_cpomax;
        public string CpoMax
        {
            get { return m_cpomax; }
            set
            {
                m_cpomax = value;
                OnPropertyChanged("CpoMax");
            }
        }

        private static string m_cpomin;
        public string CpoMin
        {
            get { return m_cpomin; }
            set
            {
                m_cpomin = value;
                OnPropertyChanged("CpoMin");
            }
        }

        private static string m_ctol;
        public string CTol
        {
            get { return m_ctol; }
            set
            {
                m_ctol = value;
                OnPropertyChanged("CTol");
            }
        }
        private static string m_pecrate;
        public string PECRate
        {
            get { return m_pecrate; }
            set
            {
                m_pecrate = value;
                OnPropertyChanged("PECRate");
            }
        }
        private static string m_crcrate;
        public string CRCRate
        {
            get { return m_crcrate; }
            set
            {
                m_crcrate = value;
                OnPropertyChanged("CRCRate");
            }
        }
        private static string m_homaxrate;
        public string HomaxRate
        {
            get { return m_homaxrate; }
            set
            {
                m_homaxrate = value;
                OnPropertyChanged("HomaxRate");
            }
        }
        private static string m_hominrate;
        public string HominRate
        {
            get { return m_hominrate; }
            set
            {
                m_hominrate = value;
                OnPropertyChanged("HominRate");
            }
        }
        private static string m_totalrate;
        public string TotalRate
        {
            get { return m_totalrate; }
            set
            {
                m_totalrate = value;
                OnPropertyChanged("TotalRate");
            }
        }
    }
}
