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
using Microsoft.Research.DynamicDataDisplay.ViewportRestrictions;
using O2Micro.Cobra.Common;

namespace O2Micro.Cobra.SBS4Panel
{
    /// <summary>
    /// Interaction logic for WaveUserControl.xaml
    /// </summary>
    public partial class WaveUserControl : UserControl
    {
        #region 参数定义
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
        #endregion

        #region 数据集合定义
        private Dictionary<UInt32, ObservableDataSource<Point>> m_dic = new Dictionary<uint, ObservableDataSource<Point>>();
        public GasGaugeProject curproject = null;

        private List<string> projtable = new List<string>();
        public GeneralMessage gm = new GeneralMessage("SBS SFL", "", 0);

        private Collection<IPlotterElement> lgc = new Collection<IPlotterElement>();
        private ObservableCollection<SFLModel> wave_parameterlist = new ObservableCollection<SFLModel>();
        #endregion

        #region 数据类型定义
        private const UInt32 WAVE_RMCP  = 0x00066900;
        private const UInt32 WAVE_VOL   = 0x00066B00;
        private const UInt32 WAVE_CUR   = 0x00066D00;
        private const UInt32 WAVE_EXTP  = 0x00066F00;
        private const UInt32 WAVE_CAR   = 0x00067100;
        private const UInt32 WAVE_RSOC  = 0x00067200;
        #endregion

        #region 测试数据定义
        private TabItem Wave_tab = null;
        private ChartPlotter ChartPlot = null;
        private ObservableDataSource<Point> DataSource = null;
        private ObservableCollection<ChartPlotter> ChartPlotList = new ObservableCollection<ChartPlotter>();
        #endregion

        public WaveUserControl()
        {
            InitializeComponent();
        }

        public void Inital()
        {
            // Set identity mapping of point in collection to point on plot
            Clear();

            m_dic.Clear();
            ChartPlotList.Clear();
            tabctrl.Items.Clear();
            foreach (SFLModel model in wave_parameterlist)
            {
                DataSource = new ObservableDataSource<Point>();
                DataSource.Collection.Clear();
                DataSource.SetXYMapping(p => p);
                m_dic.Add(model.guid, DataSource);

                ChartPlot = new ChartPlotter();
                //ChartPlot.HorizontalAxis = new Microsoft.Research.DynamicDataDisplay.Charts.Axes.HorizontalIntegerAxis();
                Microsoft.Research.DynamicDataDisplay.VerticalAxisTitle va = new Microsoft.Research.DynamicDataDisplay.VerticalAxisTitle();
                va.Content = model.Description;
                va.FontSize = 14;
                va.FontFamily = new FontFamily("Arial");
                va.FontWeight = FontWeights.Bold;
                ChartPlot.Children.Add(va);

                Microsoft.Research.DynamicDataDisplay.HorizontalAxisTitle ha = new Microsoft.Research.DynamicDataDisplay.HorizontalAxisTitle();
                ha.Content = "Time(second)";
                ha.FontSize = 14;
                ha.FontFamily = new FontFamily("Arial");
                ha.FontWeight = FontWeights.Bold;
                ChartPlot.Children.Add(ha);

                ChartPlot.AddLineGraph(DataSource, 3, model.nickname);
                ChartPlot.Children.Add(new CursorCoordinateGraph());
                ChartPlotList.Add(ChartPlot);

                ChartPlot.Legend.Visibility = Visibility.Collapsed;
                if (model.nickname == "Volt")
                {
                    VerticalRestriction br = new VerticalRestriction(2000, 5000);
                    ChartPlot.Viewport.Restrictions.Add(br);
                    ChartPlot.VerticalAxisNavigation.Remove();
                }
                if (model.nickname == "RSOC")
                {
                    VerticalRestriction br = new VerticalRestriction(-10, 110);
                    ChartPlot.Viewport.Restrictions.Add(br);
                    ChartPlot.VerticalAxisNavigation.Remove();
                }
                if (model.nickname == "Temp")
                {
                    VerticalRestriction br = new VerticalRestriction(-50, 100);
                    ChartPlot.Viewport.Restrictions.Add(br);
                    ChartPlot.VerticalAxisNavigation.Remove();
                }

                Wave_tab = new TabItem();
                Wave_tab.Header = model.nickname;
                Wave_tab.ToolTip = model.Description;
                UserControl uc = new UserControl();
                uc.Content = ChartPlot;
                Wave_tab.Content = uc;
              
                tabctrl.Items.Add(Wave_tab);
            }
        }

        public void SetDataSource(ObservableCollection<SFLModel> parameterlist)
        {
            wave_parameterlist = parameterlist;
            Inital();
        }

        public void update()
        {
            x+=4;
            //m_dic.Clear();
            CultureInfo culture = CultureInfo.InvariantCulture;
            ObservableDataSource<Point> points = null;
            foreach (SFLModel model in wave_parameterlist)
            {
                y = model.data; //m_random.Next(3000, 5000);
                Point point = new Point(x, y);
                points = TryGetKeyValue(model.guid);
                points.AppendAsync(Dispatcher, point);
            }
        }

        public void Clear()
        {
            this.x = 0;
            this.y = 0;
            lgc.Clear();

            foreach (var chartplot in ChartPlotList)
            {
                foreach (var x in chartplot.Children)
                {
                    if (x is LineGraph || x is ElementMarkerPointsGraph)
                        lgc.Add(x);
                }
                foreach (var x in lgc)
                    chartplot.Children.Remove(x);
                lgc.Clear();
            }
        }

        private ObservableDataSource<Point> TryGetKeyValue(UInt32 key)
        {
            ObservableDataSource<Point> val = null;
            if (m_dic.TryGetValue(key, out val))
                return val;
            return val;
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

    }
}
