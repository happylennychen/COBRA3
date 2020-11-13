using System;
using System.IO;
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
using System.Threading;
using System.Windows.Media.Animation;
using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Research.DynamicDataDisplay;
using Microsoft.Research.DynamicDataDisplay.DataSources;
using Microsoft.Research.DynamicDataDisplay.Charts.Navigation;
using Cobra.Common;

namespace Cobra.SBSPanel
{
    /// <summary>
    /// Interaction logic for WaveUserControl.xaml
    /// </summary>
    public partial class WaveUserControl : UserControl
    {
        public bool bftime = false;
        private int x = 0;
        private double y = 0;
        private double LRsoc = 100;
        private double Lmah2 = 0;

        private float m_CutOff_Voltage = 3500;
        public float cutoff_voltage
        {
            get { return m_CutOff_Voltage; }
            set { m_CutOff_Voltage = value; }
        }

        private float m_Wire_Impedance = 0;
        public float wire_impedance
        {
            get { return m_Wire_Impedance; }
            set { m_Wire_Impedance = value; }
        }

        private MainControl m_Parent;
        public MainControl parent { get; set; }

        private Dictionary<UInt32, double> m_dic = new Dictionary<uint, double>();
        public GasGaugeProject curproject = null;

        private List<string> projtable = new List<string>();
        public GeneralMessage gm = new GeneralMessage("SBS SFL", "", 0);

        private Collection<IPlotterElement> lgc = new Collection<IPlotterElement>();
        private ObservableCollection<SFLModel> wave_parameterlist = new ObservableCollection<SFLModel>();
        private ObservableCollection<RCObject> load_parameterlist = new ObservableCollection<RCObject>();

        private ObservableDataSource<Point> RMsource = new ObservableDataSource<Point>();
        private ObservableDataSource<Point> CVsource = new ObservableDataSource<Point>();
        private ObservableDataSource<Point> CRsource = new ObservableDataSource<Point>();
        private ObservableDataSource<Point> ETsource = new ObservableDataSource<Point>();
        private ObservableDataSource<Point> CARsource = new ObservableDataSource<Point>();

        private ObservableDataSource<Point> RSocPoint1source = new ObservableDataSource<Point>();
        private ObservableDataSource<Point> RSocPoint2source = new ObservableDataSource<Point>();
        private ObservableDataSource<Point> RSocPoint3source = new ObservableDataSource<Point>();
        private ObservableDataSource<Point> RSocPoint12Diffsource = new ObservableDataSource<Point>();
        private ObservableDataSource<Point> RSocPoint13Diffsource = new ObservableDataSource<Point>();

        private ObservableCollection<Point> RSocPoint1List = new ObservableCollection<Point>();
        private ObservableCollection<Point> RSocPoint2List = new ObservableCollection<Point>();
        private ObservableCollection<Point> RSocPoint3List = new ObservableCollection<Point>();
        private ObservableCollection<Point> RSocPoint12DiffList = new ObservableCollection<Point>();
        private ObservableCollection<Point> RSocPoint13DiffList = new ObservableCollection<Point>();

        public WaveUserControl()
        {
            InitializeComponent();
            Inital();
        }

        public void Inital()
        {
            // Set identity mapping of point in collection to point on plot
            Clear();

            RMsource.Collection.Clear();
            RMsource.SetXYMapping(p => p);

            CVsource.Collection.Clear();
            CVsource.SetXYMapping(p => p);

            CRsource.Collection.Clear();
            CRsource.SetXYMapping(p => p);

            ETsource.Collection.Clear();
            ETsource.SetXYMapping(p => p);

            CARsource.Collection.Clear();
            CARsource.SetXYMapping(p => p);

            RSocPoint1source.Collection.Clear();
            RSocPoint1source.SetXYMapping(p => p);

            RSocPoint2source.Collection.Clear();
            RSocPoint2source.SetXYMapping(p => p);

            RSocPoint3source.Collection.Clear();
            RSocPoint3source.SetXYMapping(p => p);

            RSocPoint12Diffsource.Collection.Clear();
            RSocPoint12Diffsource.SetXYMapping(p => p);

            RSocPoint13Diffsource.Collection.Clear();
            RSocPoint13Diffsource.SetXYMapping(p => p);

            // Add all three graphs. Colors are not specified and chosen random
            RMplotter.AddLineGraph(RMsource, 3, "RemainingCapacity");
            RMplotter.Children.Add(new CursorCoordinateGraph());

            Vplotter.AddLineGraph(CVsource, 3, "Cell Voltage");
            Vplotter.Children.Add(new CursorCoordinateGraph());

            Cplotter.AddLineGraph(CRsource, 3, "Current");
            Cplotter.Children.Add(new CursorCoordinateGraph());

            Tempplotter.AddLineGraph(ETsource, 3, "ExtTemperautre");
            Tempplotter.Children.Add(new CursorCoordinateGraph());

            CARplotter.AddLineGraph(CARsource, 3, "CAR");
            CARplotter.Children.Add(new CursorCoordinateGraph());

            Rsocplotter.AddLineGraph(RSocPoint1source, 3, "Device Displayed");
            Rsocplotter.Children.Add(new CursorCoordinateGraph());

            Rsocplotter.AddLineGraph(RSocPoint2source, 3, "PC Calculate");
            Rsocplotter.Children.Add(new CursorCoordinateGraph());

            Rsocplotter.AddLineGraph(RSocPoint3source, 3, "Coulomb Counting");
            Rsocplotter.Children.Add(new CursorCoordinateGraph());

            Difplotter.AddLineGraph(RSocPoint12Diffsource, 3, "Device And PC");
            Difplotter.Children.Add(new CursorCoordinateGraph());

            Difplotter.AddLineGraph(RSocPoint13Diffsource, 3, "Device And Coulomb");
            Difplotter.Children.Add(new CursorCoordinateGraph());
        }

        public void SetDataSource(ObservableCollection<SFLModel> parameterlist)
        {
            wave_parameterlist = parameterlist;
        }

        public void update()
        {
            x++;
            m_dic.Clear();
            CultureInfo culture = CultureInfo.InvariantCulture;
            foreach (SFLModel model in wave_parameterlist)
            {
                y = model.data; //m_random.Next(3000, 5000);
                Point point = new Point(x, y);
                switch (model.guid)
                {
                    case 0x00060f00: //RemainingCapacity
                        {
                            RMsource.AppendAsync(Dispatcher, point);
                            break;
                        }
                    case 0x00063c00: //Cell01Voltage
                        {
                            CVsource.AppendAsync(Dispatcher, point);
                            break;
                        }
                    case 0x00060a00: //Current
                        {
                            CRsource.AppendAsync(Dispatcher, point);
                            break;
                        }
                    case 0x00064a00:  //ExtTemperature
                        {
                            ETsource.AppendAsync(Dispatcher, point);
                            break;
                        }
                    case 0x0006f200: //CAR
                        {
                            CARsource.AppendAsync(Dispatcher, point);
                            break;
                        }
                    case 0x00060d00: //RSOC
                        {
                            //RSocPoint1source.AppendAsync(Dispatcher, point);
                            break;
                        }
                }
                m_dic.Add(model.guid, model.data);
            }
            if (m_dic.Count == 6)
                RsocPointOnMonitor(x);
        }

        public void RsocPointOnMonitor(int x)
        {
            int n = 0;
            bool bconv = true;
            double dcur = 0, dtemp = 0, dvol = 0, ctn = 0;
            double ctn3p5 = 0, ctn1 = 0;
            double drsocfromcvt = 0, drsocdiff = 0, LlRsoc = 100;
            Point point;

            if (!bftime) //第一次初始值
            {
                dcur = TryGetKeyValue(0x00060a00);
                dtemp = TryGetKeyValue(0x00064a00);
                dvol = TryGetKeyValue(0x00063c00) - TryGetKeyValue(0x00060a00) * wire_impedance;

                ctn = curproject.LutRCTable((float)dvol, (float)Math.Abs(TryGetKeyValue(0x00060a00)), (float)TryGetKeyValue(0x00064a00) * 10);
                ctn3p5 = curproject.LutRCTable(cutoff_voltage, (float)Math.Abs(TryGetKeyValue(0x00060a00)), (float)TryGetKeyValue(0x00064a00) * 10);

                LRsoc = TryGetKeyValue(0x00060d00);
                Lmah2 = TryGetKeyValue(0x0006f200);
                ctn1 = ctn - ctn3p5 + TryGetKeyValue(0x0006f200);
                bftime = true;
            }
            else
            {
                bconv = true;
                if ((dcur != TryGetKeyValue(0x00060a00)) || (dtemp != TryGetKeyValue(0x00064a00))) bconv = false;
                if (bconv)  //如果电流温度没有变化
                {
                    if (ctn1 == Lmah2) LRsoc = 0;
                    else LRsoc = LRsoc * (ctn1 - TryGetKeyValue(0x0006f200)) / (ctn1 - Lmah2);
                }
                else //如果电流温度发生变化
                {
                    dvol = TryGetKeyValue(0x00063c00) - TryGetKeyValue(0x00060a00) * wire_impedance;

                    ctn = curproject.LutRCTable((float)dvol, (float)Math.Abs(TryGetKeyValue(0x00060a00)), (float)TryGetKeyValue(0x00064a00) * 10);
                    ctn3p5 = curproject.LutRCTable(cutoff_voltage, (float)Math.Abs(TryGetKeyValue(0x00060a00)), (float)TryGetKeyValue(0x00064a00) * 10);
                    ctn1 = ctn - ctn3p5 + TryGetKeyValue(0x0006f200);

                    if (ctn1 == Lmah2) LRsoc = 0;
                    else LRsoc = LRsoc * (ctn1 - TryGetKeyValue(0x0006f200)) / (ctn1 - Lmah2);
                }
                Lmah2 = TryGetKeyValue(0x0006f200);
                dcur = TryGetKeyValue(0x00060a00);
                dtemp = TryGetKeyValue(0x00064a00);
            }

            if (dvol <= cutoff_voltage)
            {
                if (n == 1)
                {
                    n = 0;
                    LRsoc = LlRsoc - 1;
                }
                else
                {
                    n++;
                    LRsoc = LlRsoc;
                }
            }
            else n = 0;

            if (LRsoc > LlRsoc) LRsoc = LlRsoc;

            drsocfromcvt = LRsoc;
            drsocdiff = TryGetKeyValue(0x00060d00) - drsocfromcvt;


            //建立point集合
            point = new Point(x, TryGetKeyValue(0x00060d00));
            RSocPoint1source.AppendAsync(Dispatcher, point);

            point = new Point(x, drsocfromcvt);
            RSocPoint2source.AppendAsync(Dispatcher, point);

            point = new Point(x, drsocdiff);
            RSocPoint12Diffsource.AppendAsync(Dispatcher, point);
        }

        public void Clear()
        {
            this.x = 0;
            this.y = 0;
            lgc.Clear();
            foreach (var x in RMplotter.Children)
            {
                if (x is LineGraph || x is ElementMarkerPointsGraph)
                    lgc.Add(x);
            }
            foreach (var x in lgc)
                RMplotter.Children.Remove(x);
            lgc.Clear();

            foreach (var x in Vplotter.Children)
            {
                if (x is LineGraph || x is ElementMarkerPointsGraph)
                    lgc.Add(x);
            }
            foreach (var x in lgc)
                Vplotter.Children.Remove(x);
            lgc.Clear();

            foreach (var x in Cplotter.Children)
            {
                if (x is LineGraph || x is ElementMarkerPointsGraph)
                    lgc.Add(x);
            }
            foreach (var x in lgc)
                Cplotter.Children.Remove(x);
            lgc.Clear();

            foreach (var x in Tempplotter.Children)
            {
                if (x is LineGraph || x is ElementMarkerPointsGraph)
                    lgc.Add(x);
            }
            foreach (var x in lgc)
                Tempplotter.Children.Remove(x);
            lgc.Clear();

            foreach (var x in CARplotter.Children)
            {
                if (x is LineGraph || x is ElementMarkerPointsGraph)
                    lgc.Add(x);
            }
            foreach (var x in lgc)
                CARplotter.Children.Remove(x);

            foreach (var x in Rsocplotter.Children)
            {
                if (x is LineGraph || x is ElementMarkerPointsGraph)
                    lgc.Add(x);
            }
            foreach (var x in lgc)
                Rsocplotter.Children.Remove(x);

            foreach (var x in Difplotter.Children)
            {
                if (x is LineGraph || x is ElementMarkerPointsGraph)
                    lgc.Add(x);
            }
            foreach (var x in lgc)
                Difplotter.Children.Remove(x);
        }

        private double TryGetKeyValue(UInt32 key)
        {
            double val = 0;
            if (m_dic.TryGetValue(key, out val))
                return val;
            return val;
        }

        #region Load Log File
        private void LoadBtn_Click(object sender, RoutedEventArgs e)
        {
            string fullpath = "";
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Title = "Load Log File";
            openFileDialog.Filter = "Log files (*.txt)|*.txt||";
            openFileDialog.FileName = "default";
            openFileDialog.FilterIndex = 1;
            openFileDialog.RestoreDirectory = true;
            openFileDialog.DefaultExt = "txt";
            openFileDialog.InitialDirectory = FolderMap.m_currentproj_folder;
            if (openFileDialog.ShowDialog() == true)
            {
                fullpath = openFileDialog.FileName;
                if (!LoadFile(fullpath, true))
                {
                    gm.controls = "Load File button";
                    gm.level = 2;
                    gm.message = LibErrorCode.GetErrorDescription(LibErrorCode.IDS_ERR_SBSSFL_LOAD_FILE);
                    gm.bupdate = true;
                    CallWarningControl(gm);
                    return;
                }
            }
        }

        private void LoadLXBtn_Click(object sender, RoutedEventArgs e)
        {
            string fullpath = "";
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Title = "Load Log File";
            openFileDialog.Filter = "Log files (*.txt)|*.txt||";
            openFileDialog.FileName = "default";
            openFileDialog.FilterIndex = 1;
            openFileDialog.RestoreDirectory = true;
            openFileDialog.DefaultExt = "txt";
            openFileDialog.InitialDirectory = FolderMap.m_currentproj_folder;
            if (openFileDialog.ShowDialog() == true)
            {
                fullpath = openFileDialog.FileName;
                if (!LoadFile(fullpath, false))
                {
                    gm.controls = "Load File button";
                    gm.level = 2;
                    gm.message = LibErrorCode.GetErrorDescription(LibErrorCode.IDS_ERR_SBSSFL_LOAD_FILE);
                    gm.bupdate = true;
                    CallWarningControl(gm);
                    return;
                }
            }
        }

        private bool InitalLoadFile()
        {
            UInt32 ret = 0;

            #region RSOC Plotter初始化
            this.x = 0;
            this.y = 0;
            lgc.Clear();

            foreach (var x in Rsocplotter.Children)
            {
                if (x is LineGraph || x is ElementMarkerPointsGraph)
                    lgc.Add(x);
            }
            foreach (var x in lgc)
                Rsocplotter.Children.Remove(x);

            foreach (var x in Difplotter.Children)
            {
                if (x is LineGraph || x is ElementMarkerPointsGraph)
                    lgc.Add(x);
            }
            foreach (var x in lgc)
                Difplotter.Children.Remove(x);

            RSocPoint1source.Collection.Clear();
            RSocPoint1source.SetXYMapping(p => p);

            RSocPoint2source.Collection.Clear();
            RSocPoint2source.SetXYMapping(p => p);

            RSocPoint3source.Collection.Clear();
            RSocPoint3source.SetXYMapping(p => p);

            RSocPoint12Diffsource.Collection.Clear();
            RSocPoint12Diffsource.SetXYMapping(p => p);

            RSocPoint13Diffsource.Collection.Clear();
            RSocPoint13Diffsource.SetXYMapping(p => p);

            Rsocplotter.AddLineGraph(RSocPoint1source, 3, "Device Displayed");
            Rsocplotter.Children.Add(new CursorCoordinateGraph());

            Rsocplotter.AddLineGraph(RSocPoint2source, 3, "PC Calculate");
            Rsocplotter.Children.Add(new CursorCoordinateGraph());

            Rsocplotter.AddLineGraph(RSocPoint3source, 3, "Coulomb Counting");
            Rsocplotter.Children.Add(new CursorCoordinateGraph());

            Difplotter.AddLineGraph(RSocPoint12Diffsource, 3, "Device And PC");
            Difplotter.Children.Add(new CursorCoordinateGraph());

            Difplotter.AddLineGraph(RSocPoint13Diffsource, 3, "Device And Coulomb");
            Difplotter.Children.Add(new CursorCoordinateGraph());
            #endregion

            #region GG初始化
            projtable.Clear();
            if (parent.viewmode.path_parameterlist.Count == 0) return false;

            foreach (PathModel pathmodel in parent.viewmode.path_parameterlist)
                projtable.Add(pathmodel.path);

            curproject = new GasGaugeProject(projtable);
            return curproject.InitializeProject(ref ret);
            #endregion
        }

        private bool LoadFile(string fullpath, bool bfile)
        {
            initCheckBox();

            #region GG以及Plotter初始化
            if (!InitalLoadFile()) return false;
            #endregion

            RSocPoint1List.Clear();
            RSocPoint2List.Clear();
            RSocPoint3List.Clear();
            RSocPoint12DiffList.Clear();
            RSocPoint13DiffList.Clear();
            load_parameterlist.Clear();
            //Convert as object
            if (bfile)
            {
                if (!Convert2Object(fullpath))
                    return false;
            }
            else
            {
                if (!LXConvert2Object(fullpath))
                    return false;
            }

            //Check Table
            if (!RCPointConvert()) return false;

            StaticUpdate();

            return true;
        }

        private bool Convert2Object(string fullpath)
        {
            UInt16 iblock = 0;
            Double ddata = 0;
            byte bcol = 0;
            UInt16 iparam = 0;

            RCObject element = new RCObject();
            List<String> block = new List<string>();
            Dictionary<int, List<String>> dic = new Dictionary<int, List<String>>();

            string[] signs = { "batt_info.fVolt", "batt_info.fRSOC", "sCtMAH2", "batt_info.fCurr", "batt_info.fCellTemp" };
            string[] temp = System.IO.File.ReadAllLines(fullpath, System.Text.Encoding.GetEncoding("gb2312"));

            //以--获取block
            foreach (string s in temp)
            {
                if (!s.StartsWith("---"))
                {
                    if (!s.StartsWith("AAAA")) continue;  //不包含AAAA的字符串去掉
                    block.Add(s);
                }
                else
                {
                    if (block.Count == 0) continue;    //没有字符串的block去掉
                    dic.Add(iblock, block);
                    iblock++;
                    block = new List<string>();
                }
            }

            foreach (int i in dic.Keys)
            {
                List<String> p = dic[i];
                bcol = 0;
                foreach (string ls in p)
                {
                    string[] pInfo = ls.Split(' ');
                    if (ls.IndexOf(signs[0], 0) != -1)
                    {
                        if (Double.TryParse(pInfo[3], out ddata))
                            element.dvol = ddata;
                        bcol |= 0x01;
                    }
                    else if (ls.IndexOf(signs[1], 0) != -1)
                    {
                        if (Double.TryParse(pInfo[3], out ddata))
                            element.drsoc = ddata;
                        bcol |= 0x02;
                    }
                    else if (ls.IndexOf(signs[2], 0) != -1)
                    {
                        if (Double.TryParse(pInfo[3], out ddata))
                            element.dmah2 = ddata;
                        bcol |= 0x04;
                    }
                    else if (ls.IndexOf(signs[3], 0) != -1)
                    {
                        if (Double.TryParse(pInfo[3], out ddata))
                        {
                            element.dcur = ddata;
                            //if (Math.Abs(element.dcur) < dcurrent) break;
                        }
                        bcol |= 0x08;
                    }
                    else if (ls.IndexOf(signs[4], 0) != -1)
                    {
                        if (Double.TryParse(pInfo[3], out ddata))
                            element.dtemp = ddata;
                        bcol |= 0x10;
                    }
                    else continue;

                    if (bcol == 0x1F)
                    {
                        RCObject param = new RCObject();
                        param.id = iparam;
                        param.dvol = element.dvol - element.dcur * wire_impedance;
                        param.drsoc = element.drsoc;
                        param.dmah2 = element.dmah2;
                        param.dcur = element.dcur;
                        param.dtemp = (element.dtemp * 10);
                        param.drsocfromcvt = 100;

                        load_parameterlist.Add(param);
                        iparam++;
                    }
                }
            }
            if (load_parameterlist.Count == 0) return false;
            return true;
        }

        private bool RCPointConvert()
        {
            bool bftime = false, bconv = true;
            double dcur = 0, dtemp = 0, ctn = 0;
            double ctn3p5 = 0, ctn1 = 0;
            double mah2max = 0, mah2rsoc = 0, mah2rsocdiff = 0, LlRsoc = 100;
            double ddata = 0;

            int x = 0, size = 0, n = 0;
            Point point;

            if (load_parameterlist.Count == 0) return false;
            size = load_parameterlist.Count;
            mah2max = load_parameterlist[size - 1].dmah2;

            foreach (RCObject rc in load_parameterlist)
            {
                if (!bftime) //第一次初始值
                {
                    dcur = rc.dcur;
                    dtemp = rc.dtemp;

                    ctn = curproject.LutRCTable((float)rc.dvol, (float)Math.Abs(rc.dcur), (float)rc.dtemp);
                    ctn3p5 = curproject.LutRCTable(cutoff_voltage, (float)Math.Abs(rc.dcur), (float)rc.dtemp);

                    LRsoc = rc.drsoc;
                    Lmah2 = rc.dmah2;
                    ctn1 = ctn - ctn3p5 + rc.dmah2;
                    bftime = true;
                }
                else
                {
                    bconv = true;
                    if ((dcur != rc.dcur) || (dtemp != rc.dtemp)) bconv = false;
                    if (bconv)  //如果电流温度没有变化
                    {
                        if (ctn1 == Lmah2) LRsoc = 0;
                        else LRsoc = LRsoc * (ctn1 - rc.dmah2) / (ctn1 - Lmah2);
                    }
                    else //如果电流温度发生变化
                    {
                        ctn = curproject.LutRCTable((float)rc.dvol, (float)Math.Abs(rc.dcur), (float)rc.dtemp);
                        ctn3p5 = curproject.LutRCTable(cutoff_voltage, (float)Math.Abs(rc.dcur), (float)rc.dtemp);

                        ctn1 = ctn - ctn3p5 + rc.dmah2;
                        if (ctn1 == Lmah2) LRsoc = 0;
                        else LRsoc = LRsoc * (ctn1 - rc.dmah2) / (ctn1 - Lmah2);
                    }
                    Lmah2 = rc.dmah2;
                    dcur = rc.dcur;
                    dtemp = rc.dtemp;
                }

                if (rc.dvol <= cutoff_voltage)
                {
                    if (n == 1)
                    {
                        n = 0;
                        LRsoc = LlRsoc - 1;
                    }
                    else
                    {
                        n++;
                        LRsoc = LlRsoc;
                    }
                }
                else n = 0;

                if (LRsoc > LlRsoc) LRsoc = LlRsoc;

                rc.drsocfromcvt = LlRsoc = LRsoc;
                rc.drsocdiff = rc.drsoc - rc.drsocfromcvt;

                mah2rsoc = ((mah2max - rc.dmah2) / mah2max) * 100;
                mah2rsocdiff = rc.drsoc - mah2rsoc;

                #region 按Cory要求修订曲线
                for (int i = 1; i < 100; i++)
                {
                    ddata = (double)(mah2rsocdiff / i);
                    if (Math.Abs(ddata) < 3) break;
                }
                rc.drsocdiff = ddata;
                rc.drsocfromcvt = rc.drsoc + ddata;
                #endregion

                //建立point集合

                point = new Point(x, rc.drsoc);
                RSocPoint1List.Add(point);

                point = new Point(x, rc.drsocfromcvt);
                RSocPoint2List.Add(point);

                point = new Point(x, rc.drsocdiff);
                RSocPoint12DiffList.Add(point);

                point = new Point(x, mah2rsoc);
                RSocPoint3List.Add(point);

                point = new Point(x, mah2rsocdiff);
                RSocPoint13DiffList.Add(point);

                x++;
            }
            return true;
        }

        public void StaticUpdate()
        {
            RSocPoint1source.AppendMany(RSocPoint1List);
            RSocPoint2source.AppendMany(RSocPoint2List);
            RSocPoint3source.AppendMany(RSocPoint3List);
            RSocPoint12Diffsource.AppendMany(RSocPoint12DiffList);
            RSocPoint13Diffsource.AppendMany(RSocPoint13DiffList);

            Rsocplotter.FitToView();
            Difplotter.FitToView();
        }

        #region Lian Xiang
        private bool LXConvert2Object(string fullpath)
        {
            Double ddata = 0;
            UInt16 iparam = 0;

            RCObject element = new RCObject();
            string str = String.Empty;
            string[] signs = { "mV", "mA", "oC", "mah", "%" };
            string[] temp = System.IO.File.ReadAllLines(fullpath, System.Text.Encoding.GetEncoding("gb2312"));
            //获取block，后期可添加正则表达式检查
            foreach (string s in temp)
            {
                string[] p = s.Split('\t');
                foreach (string ls in p)
                {
                    ls.Trim();
                    if (ls.IndexOf(signs[0], 0) != -1)
                    {
                        str = ls.Remove(ls.Length - signs[0].Length);
                        if (Double.TryParse(str, out ddata))
                            element.dvol = ddata;
                    }
                    else if (ls.IndexOf(signs[1], 0) != -1)
                    {
                        str = ls.Remove(ls.Length - signs[1].Length);
                        if (Double.TryParse(str, out ddata))
                            element.dcur = ddata;
                    }
                    else if (ls.IndexOf(signs[2], 0) != -1)
                    {
                        str = ls.Remove(ls.Length - signs[2].Length);
                        if (Double.TryParse(str, out ddata))
                            element.dtemp = ddata;
                    }
                    else if (ls.IndexOf(signs[3], 0) != -1)
                    {
                        str = ls.Remove(ls.Length - signs[3].Length);
                        if (Double.TryParse(str, out ddata))
                            element.dmah2 = ddata;
                    }
                    else if (ls.IndexOf(signs[4], 0) != -1)
                    {
                        str = ls.Remove(ls.Length - signs[4].Length);
                        if (Double.TryParse(str, out ddata))
                            element.drsoc = ddata;
                    }
                    else continue;
                }

                RCObject param = new RCObject();
                param.id = iparam;
                param.dvol = element.dvol - element.dcur * wire_impedance;
                param.drsoc = element.drsoc;
                param.dmah2 = element.dmah2;
                param.dcur = element.dcur;
                param.dtemp = (element.dtemp * 10);
                param.drsocfromcvt = 100;

                load_parameterlist.Add(param);
                iparam++;
            }
            if (load_parameterlist.Count == 0) return false;
            return true;
        }
        #endregion
        #endregion

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            string fullpath = String.Empty;
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            saveFileDialog.Title = "Save Log File";
            saveFileDialog.Filter = "files (*.csv)|*.csv||";
            saveFileDialog.FileName = "default";
            saveFileDialog.FilterIndex = 1;
            saveFileDialog.RestoreDirectory = true;
            saveFileDialog.DefaultExt = "xlsx";
            saveFileDialog.InitialDirectory = FolderMap.m_currentproj_folder;
            if (saveFileDialog.ShowDialog() == true)
            {
                fullpath = saveFileDialog.FileName;
                SaveFile(fullpath);
            }
        }

        private bool SaveFile(string filePath)
        {
            Point point;
            bool successFlag = true;

            StringBuilder strValue = new StringBuilder();
            StreamWriter sw = null;
            List<String> props = new List<string>();
            props.Add("RSOC1");
            props.Add("RSOC2");
            props.Add("RSOC3");
            props.Add("RSOC12");
            props.Add("RSOC13");

            try
            {
                sw = new StreamWriter(filePath);
                for (int i = 0; i < props.Count; i++)
                {
                    strValue.Append(props[i]);
                    strValue.Append(",");
                }
                strValue.Remove(strValue.Length - 1, 1);
                sw.WriteLine(strValue);    //write the column name

                for (int i = 0; i < RSocPoint1List.Count; i++)
                {
                    strValue.Remove(0, strValue.Length);
                    if (i < RSocPoint1List.Count)
                    {
                        point = RSocPoint1List[i];
                        strValue.Append(point.Y.ToString());
                        strValue.Append(",");
                    }

                    if (i < RSocPoint2List.Count)
                    {
                        point = RSocPoint2List[i];
                        strValue.Append(point.Y.ToString());
                        strValue.Append(",");
                    }

                    if (i < RSocPoint3List.Count)
                    {
                        point = RSocPoint3List[i];
                        strValue.Append(point.Y.ToString());
                        strValue.Append(",");
                    }

                    if (i < RSocPoint12DiffList.Count)
                    {
                        point = RSocPoint12DiffList[i];
                        strValue.Append(point.Y.ToString());
                        strValue.Append(",");
                    }

                    if (i < RSocPoint13DiffList.Count)
                    {
                        point = RSocPoint13DiffList[i];
                        strValue.Append(point.Y.ToString());
                    }
                    sw.WriteLine(strValue);
                }
            }
            catch (Exception ex)
            {
                successFlag = false;
            }
            finally
            {
                if (sw != null)
                {
                    sw.Dispose();
                }
            }

            return successFlag;
        }

        #region 通用控件消息响应
        public void CallWarningControl(GeneralMessage message)
        {
            WarningPopControl.Dispatcher.Invoke(new Action(() =>
            {
                WarningPopControl.ShowDialog(message);
            }));
        }
        #endregion

        private void RSOC_ck_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            LineGraph ltmp = null;
            switch (cb.Name)
            {
                case "cc_ck":
                case "pc_ck":
                case "dd_ck":
                    foreach (var x in Rsocplotter.Children)
                    {
                        if (x is LineGraph)
                        {
                            ltmp = (LineGraph)x;
                            string str = ltmp.Description.Brief;
                            if (str == cb.Content.ToString())
                                ltmp.Visibility = (cb.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
                        }
                    }
                    break;
                case "dc_ck":
                case "dp_ck":
                    foreach (var x in Difplotter.Children)
                    {
                        if (x is LineGraph)
                        {
                            ltmp = (LineGraph)x;
                            string str = ltmp.Description.Brief;
                            if (str == cb.Content.ToString())
                                ltmp.Visibility = (cb.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
                        }
                    }
                    break;
            }
        }

        private void initCheckBox()
        {
            cc_ck.IsChecked = true;
            dc_ck.IsChecked = true;
            pc_ck.IsChecked = true;
            dp_ck.IsChecked = true;
            dc_ck.IsChecked = true;
        }
    }
}
