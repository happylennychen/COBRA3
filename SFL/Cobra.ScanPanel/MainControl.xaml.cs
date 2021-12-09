#define SupportCurve
//#define SupportThreadMonitor
//#define FakeData
using System;
using System.Collections.Generic;
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
using System.Linq;
using System.Xml.Linq;
using System.Xml;
using System.IO;
using System.Data;
using System.ComponentModel;
using Cobra.EM;
using Cobra.Common;
using System.Windows.Controls.Primitives;
using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection;
//using System.Windows.Threading;
using System.Threading;
using System.Timers;
using Microsoft.Research.DynamicDataDisplay;
using Microsoft.Research.DynamicDataDisplay.PointMarkers;
using Microsoft.Research.DynamicDataDisplay.DataSources;
using Microsoft.Research.DynamicDataDisplay.Charts.Navigation;
using Microsoft.Research.DynamicDataDisplay.Navigation;
using Microsoft.Research.DynamicDataDisplay.Charts;
using Microsoft.Research.DynamicDataDisplay.Common;
using Microsoft.Research.DynamicDataDisplay.ViewportRestrictions;
namespace Cobra.ScanPanel
{
    /// <summary>
    /// MainControl.xaml 的交互逻辑
    /// </summary>
    public partial class MainControl
    {
        #region 变量定义
        bool isReentrant_Run = false;   //控制Run button的重入
        //父对象保存
        private Device m_parent;
        public Device parent
        {
            get { return m_parent; }
            set { m_parent = value; }
        }
        private string m_sflname;
        public string sflname
        {
            get { return m_sflname; }
            set { m_sflname = value; }
        }

        private TASKMessage m_Msg = new TASKMessage();
        public TASKMessage msg
        {
            get { return m_Msg; }
            set { m_Msg = value; }
        }


        private double m_ECOT = new double();
        public double ECOT
        {
            get { return m_ECOT; }
            set { m_ECOT = value; }
        }
        private double m_EDOT = new double();
        public double EDOT
        {
            get { return m_EDOT; }
            set { m_EDOT = value; }
        }
        private GeneralMessage gm = new GeneralMessage("SCAN SFL", "", 0);
        //private System.Windows.Threading.DispatcherTimer t = new System.Windows.Threading.DispatcherTimer();
        private System.Timers.Timer t = new System.Timers.Timer();
        private Thread IOThread;
        private Thread UIThread;
        private Thread DBThread;
        //private Thread LogThread;

        private object DB_Lock = new object();
        private object IO_Lock = new object();
        private object UI_Lock = new object();

        ParamContainer staticdatalist = new ParamContainer();
        ParamContainer dynamicdatalist = new ParamContainer();
        AsyncObservableCollection<Parameter> scanlist = new AsyncObservableCollection<Parameter>();
#if SupportCobraLog
        private CobraLog m_scanlog;
        public CobraLog scanlog
        {
            get { return m_scanlog; }
            set { m_scanlog = value; }
        }
#else

        private AsyncObservableCollection<LogData> m_logdatalist = new AsyncObservableCollection<LogData>();
        public AsyncObservableCollection<LogData> logdatalist    //LogData的集合
        {
            get { return m_logdatalist; }
            set
            {
                m_logdatalist = value;
                //OnPropertyChanged("logdatalist");
            }
        }
#endif
        private ScanLogUIData m_logUIdata = new ScanLogUIData();
        public ScanLogUIData logUIdata
        {
            get { return m_logUIdata; }
            set { m_logUIdata = value; }
        }

        ObservableCollection<LineGraph> lAm = new ObservableCollection<LineGraph>();

        private Collection<string> m_scanoption = new Collection<string>();
        public Collection<string> scanratelist
        {
            set { m_scanoption = value; }
            get { return m_scanoption; }
        }

        public Dictionary<string, ushort> m_subtask = new Dictionary<string, ushort>();
        public Dictionary<string, ushort> subtasklist
        {
            set { m_subtask = value; }
            get { return m_subtask; }
        }

        private AsyncObservableCollection<setModel> m_setmodel_list = new AsyncObservableCollection<setModel>();
        public AsyncObservableCollection<setModel> options
        {
            get { return m_setmodel_list; }
            set { m_setmodel_list = value; }
        }

        public string optionsJson = string.Empty;
        public Dictionary<string, string> optionsDictionary = new Dictionary<string, string>();

        private bool gpio_update = false;
        private Point orig_point;

#if SupportThreadMonitor
        private ThreadMonitorWindow tm = new ThreadMonitorWindow();
#endif
        int session_id = -1;
        ulong session_row_number = 0;
        #endregion

        #region 函数定义

        #region Internal Function

        private string GetHashTableValueByKey(string str, Hashtable htable)
        {
            /*foreach (DictionaryEntry de in htable)
            {
                if (de.Key.ToString().Equals(str))
                    return de.Value.ToString();
            }
            return "NoSuchKey";*/
            /*if (htable.ContainsKey(str))  //之所以不能这样用，是因为这个htable在创建的时候，Key的类型为XName而非string
                return htable[str].ToString();
            else
                return "NoSuchKey";*/
            if (htable.ContainsKey(str))
                return htable[str].ToString();
            else
                return "NoSuchKey";
        }

        private void RebuildUISourceFromList()
        {
            #region 根据dynamicdatalist中的数据来初始化DataTable的column以及isDisplay
            List<LogParam> paramlist = new List<LogParam>();
            foreach (Parameter param in dynamicdatalist.parameterlist)
            {
                LogParam logp = new LogParam();
                logp.name = GetHashTableValueByKey("LogName", param.sfllist[sflname].nodetable);
                logp.group = GetHashTableValueByKey("Group", param.sfllist[sflname].nodetable);
                paramlist.Add(logp);
            }
            logUIdata.logbuf.Clear();   //Clear all the history data before the scan start.
            logUIdata.logbuf.Columns.Clear();
            //logUIdata.logbuf.Reset(); //千万不能加这一句
            logUIdata.BuildColumn(paramlist, true);
            //logUIdata.isDisplay.Clear();
            logUIdata.BuildIsDisplay(paramlist);
            #endregion

        }
        private void RebuildLineGraphFromList()
        {
            ClearCurve();
            BuildCurve();
        }

        private void ClearCurve()
        {
#if SupportCurve
            #region 清除原来的曲线
            ChildrenCollection cc = new ChildrenCollection();
            foreach (object obj in Vplotter.Children)
            {
                if (obj.GetType().ToString() == "Microsoft.Research.DynamicDataDisplay.LineGraph")
                    cc.Add((IPlotterElement)obj);
                if (obj.GetType().ToString() == "Microsoft.Research.DynamicDataDisplay.ElementMarkerPointsGraph")
                    cc.Add((IPlotterElement)obj);
            }
            foreach (IPlotterElement ipe in cc)
            {
                Vplotter.Children.Remove(ipe);
            }
            cc.Clear();
            foreach (object obj in Tplotter.Children)
            {
                if (obj.GetType().ToString() == "Microsoft.Research.DynamicDataDisplay.LineGraph")
                    cc.Add((IPlotterElement)obj);
                if (obj.GetType().ToString() == "Microsoft.Research.DynamicDataDisplay.ElementMarkerPointsGraph")
                    cc.Add((IPlotterElement)obj);
            }
            foreach (IPlotterElement ipe in cc)
            {
                Tplotter.Children.Remove(ipe);
            }
            cc.Clear();
            foreach (object obj in Cplotter.Children)
            {
                if (obj.GetType().ToString() == "Microsoft.Research.DynamicDataDisplay.LineGraph")
                    cc.Add((IPlotterElement)obj);
                if (obj.GetType().ToString() == "Microsoft.Research.DynamicDataDisplay.ElementMarkerPointsGraph")
                    cc.Add((IPlotterElement)obj);
            }
            foreach (IPlotterElement ipe in cc)
            {
                Cplotter.Children.Remove(ipe);
            }
            cc.Clear();
            #endregion
#endif
        }

        private void BuildCurve()   //创建曲线之前，logbuf必须已经创建好，且带有Group信息
        {
#if SupportCurve
            #region 创建新曲线

            List<string> strlist = new List<string>();
            /*foreach (Parameter param in dynamicdatalist.parameterlist)
            {
                string str = GetHashTableValueByKey("LogName", param.sfllist[sflname].nodetable);
                strlist.Add(str);
            }*/

            DataTable table = logUIdata.logbuf;

            foreach (DataColumn dc in table.Columns)
            {
                string str = dc.ColumnName;
                if (str != "Time")
                    strlist.Add(str);
            }

            for (int index = 0; index < strlist.Count; index++)
            {
                string group = table.Columns[index].Caption;
                if (group != "0" && group != "1" && group != "2")
                    continue;
                TableDataSource data = new TableDataSource(table);
                // X is time in seconds
                data.SetXMapping(row => ((DateTime)row["Time"] - (DateTime)table.Rows[0]["Time"]).TotalSeconds);
                //data.SetXMapping(row => ((DateTime)row["Time"] - StartTime).TotalSeconds);
                // Y is value of "Sine" column
                //data.SetYMapping(row => Convert.ToDouble(row[strlist[index]]));
                #region 绑Y轴
                string str = strlist[index];
                data.SetYMapping(row => Convert.ToDouble(row[str]));
                #endregion

                //CompositeDataSource compositeDataSource = new CompositeDataSource(data);
                SolidColorBrush br = new SolidColorBrush(Color.FromRgb((byte)(index * 15), (byte)(index * 15), 0));
                CircleElementPointMarker mk = new CircleElementPointMarker
                {
                    Size = 5,
                    Brush = br,
                    Fill = br
                };
                if (group == "0")
                {
                    Vtab.Visibility = Visibility.Visible;
                    Vplotter.Visibility = Visibility.Visible;
                    /*lAm.Add(Vplotter.AddLineGraph(data,
                        new Pen(br, 2),
                        mk,
                        new PenDescription(strlist[index])));//*/
                    lAm.Add(Vplotter.AddLineGraph(data, 2, strlist[index]));
                }
                else if (group == "1")
                {
                    Ttab.Visibility = Visibility.Visible;
                    Tplotter.Visibility = Visibility.Visible;
                    /*lAm.Add(Tplotter.AddLineGraph(data,
                        new Pen(br, 2),
                        mk,
                        new PenDescription(strlist[index])));*/
                    lAm.Add(Tplotter.AddLineGraph(data, 2, strlist[index]));
                }
                else if (group == "2")
                {
                    Ctab.Visibility = Visibility.Visible;
                    Cplotter.Visibility = Visibility.Visible;
                    /*lAm.Add(Cplotter.AddLineGraph(data,
                        new Pen(br, 2),
                        mk,
                        new PenDescription(strlist[index])));*/
                    lAm.Add(Cplotter.AddLineGraph(data, 2, strlist[index]));
                }
            }
            //Vplotter.Viewport.Visible = new Rect(Vplotter.Viewport.Visible.X, 0, Vplotter.Viewport.Visible.Width, 6000);
            //Vplotter.FitToView();
            //Tplotter.FitToView();
            //Cplotter.FitToView();
            #endregion 
#endif
        }
        private void UpdateVoltageDisplay(int CellNum)
        {
            if (CellNum == 0)    //兼容没有CellNum的芯片
            {
                foreach (CellVoltage cv in vPnl.vViewModel.voltageList)
                {
                    cv.pUsability = false;
                }
            }
            else
            {
                GetSysInfo();
                int i = 0;
                if (msg.sm.dic.Count != 0)
                {
                    foreach (var item in msg.sm.dic)
                    {
                        vPnl.vViewModel.voltageList[(int)(item.Key)].pUsability = !item.Value;
                        i++;
                    }
                }
                else    //兼容不需要乱序的芯片
                {
                    foreach (CellVoltage cv in vPnl.vViewModel.voltageList)
                    {
                        if (cv.pIndex < CellNum)
                        {
                            cv.pUsability = false;
                        }
                        else
                            cv.pUsability = true;
                    }
                }
            }
        }

        /// <summary>
        /// 根据staticdatalist中parameter的phydata，来更新ViewModel中的值，从而改变UI
        /// </summary>
        public void UpdateMonitorStaticUI()
        {
            foreach (Parameter param in staticdatalist.parameterlist)
            {
                //if (param.phydata != -999999)
                {
                    if (GetHashTableValueByKey("Group", param.sfllist[sflname].nodetable) == "0")
                    {
                        if (GetHashTableValueByKey("SubGroup", param.sfllist[sflname].nodetable) == "2")
                        {
                            vPnl.vViewModel.pUVTH = param.phydata;
                            foreach (CellVoltage cv in vPnl.vViewModel.voltageList)
                            {
                                cv.pMinValue = param.phydata;
                            }
                        }
                        else if (GetHashTableValueByKey("SubGroup", param.sfllist[sflname].nodetable) == "1")
                        {
                            vPnl.vViewModel.pOVTH = param.phydata;
                            foreach (CellVoltage cv in vPnl.vViewModel.voltageList)
                            {
                                cv.pMaxValue = param.phydata;
                            }
                        }
                        else if (GetHashTableValueByKey("SubGroup", param.sfllist[sflname].nodetable) == "3")
                        {
                            int CellNum;
                            /*if (param.phydata == -999999)
                            {
                                MessageBox.Show("CellNum is invalid!");
                                CellNum = 6;
                            }
                            else*/
                            CellNum = Convert.ToInt16(param.itemlist[(int)param.phydata]);    //用UpdateVoltageDisplay()取代
                            /*foreach (CellVoltage cv in vPnl.vViewModel.voltageList)   
                            {
                                if (cv.pIndex < CellNum)
                                {
                                    cv.pUsability = false;
                                }
                                else
                                    cv.pUsability = true;
                            }*/

                            UpdateVoltageDisplay(CellNum);

                            /*从dynamicdatalist中删除所有voltage cell，然后根据CellNum来重新添加*/      //为了OZ77系列最高CELL特殊性注释掉
                            /*AsyncObservableCollection<Parameter> plist = new AsyncObservableCollection<Parameter>();
                            plist = parent.GetParamLists(sflname).parameterlist;
                            foreach (Parameter p in plist)
                            {
                                if (GetHashTableValueByKey("Group", p.sfllist[sflname].nodetable) == "0")
                                {
                                    if (GetHashTableValueByKey("SubGroup", p.sfllist[sflname].nodetable) == "0")
                                    {
                                        dynamicdatalist.parameterlist.Remove(p);
                                        if (Convert.ToInt32(GetHashTableValueByKey("Order", p.sfllist[sflname].nodetable)) < CellNum)
                                            dynamicdatalist.parameterlist.Add(p);
                                    }
                                }
                            }*/
                        }
                    }
                    else if (GetHashTableValueByKey("Group", param.sfllist[sflname].nodetable) == "1")
                    {
                        if (GetHashTableValueByKey("SubGroup", param.sfllist[sflname].nodetable) == "2")
                        {
                            tPnl.tViewModel.pIOTTH = param.phydata;
                            foreach (CellTemperature ct in tPnl.tViewModel.itemperatureList)
                            {
                                ct.pMaxValue = param.phydata;
                            }
                        }
                        else if (GetHashTableValueByKey("SubGroup", param.sfllist[sflname].nodetable) == "3")
                        {
                            tPnl.tViewModel.pIUTTH = param.phydata;
                            foreach (CellTemperature ct in tPnl.tViewModel.itemperatureList)
                            {
                                ct.pMinValue = param.phydata;
                            }
                        }
                        else if (GetHashTableValueByKey("SubGroup", param.sfllist[sflname].nodetable) == "4")   //一开始用ECOT作为EOT值
                        {
                            ECOT = param.phydata;
                            tPnl.tViewModel.pEOTTH = param.phydata;
                            foreach (CellTemperature ct in tPnl.tViewModel.etemperatureList)
                            {
                                ct.pMaxValue = param.phydata;
                            }
                        }
                        else if (GetHashTableValueByKey("SubGroup", param.sfllist[sflname].nodetable) == "5")
                        {
                            tPnl.tViewModel.pEUTTH = param.phydata;
                            foreach (CellTemperature ct in tPnl.tViewModel.etemperatureList)
                            {
                                ct.pMinValue = param.phydata;
                            }
                        }
                        else if (GetHashTableValueByKey("SubGroup", param.sfllist[sflname].nodetable) == "6")
                        {
                            EDOT = param.phydata;
                            tPnl.tViewModel.pEOTTH = param.phydata;
                        }
                    }
                    else if (GetHashTableValueByKey("Group", param.sfllist[sflname].nodetable) == "2")
                    {
                        /*if (GetHashTableValueByKey("SubGroup", param.sfllist[sflname].nodetable) == "1")
                        {
                            cPnl.cViewModel.pCOCTH = 1000 * Convert.ToDouble(param.itemlist[(int)param.phydata]);
                            cPnl.cViewModel.pMaxValue = cPnl.cViewModel.pCOCTH;
                        }
                        else if (GetHashTableValueByKey("SubGroup", param.sfllist[sflname].nodetable) == "2")
                        {
                            cPnl.cViewModel.pDOCTH = -1000 * param.phydata;
                            cPnl.cViewModel.pMinValue = cPnl.cViewModel.pDOCTH;
                        }*/
                    }
                    else if (GetHashTableValueByKey("Group", param.sfllist[sflname].nodetable) == "7") //特殊组
                    {
                        if (GetHashTableValueByKey("SubGroup", param.sfllist[sflname].nodetable) == "0")    //SubGroup 包含与T\C都相关的参数，或者说是GPIO组
                        {
                        }
                        else if (GetHashTableValueByKey("SubGroup", param.sfllist[sflname].nodetable) == "2")   //包含与C相关的参数
                        { }
                    }
                }
            }
            if (gpio_update)    //need to update UI according to gpio
            {
                //轮询Tgroup，根据pIndexGPIO来设置pUsability
                foreach (CellTemperature ct in tPnl.tViewModel.etemperatureList)
                {
                    if (ct.pIndexGPIO != 9999)  //如果这个参数带有GPIO控制位
                        ct.pUsability = !msg.sm.gpios[ct.pIndexGPIO];    //则根据API来控制其显示
                }
                //更新dynamicdatalist
                AsyncObservableCollection<Parameter> plist = new AsyncObservableCollection<Parameter>();
                plist = parent.GetParamLists(sflname).parameterlist;
                foreach (Parameter p in plist)
                {
                    if (GetHashTableValueByKey("Group", p.sfllist[sflname].nodetable) == "1")
                        if (GetHashTableValueByKey("SubGroup", p.sfllist[sflname].nodetable) == "1")
                        {
                            dynamicdatalist.parameterlist.Remove(p);
                            if (GetHashTableValueByKey("GPIO", p.sfllist[sflname].nodetable) == "NoSuchKey")//没有GPIO节点
                                dynamicdatalist.parameterlist.Add(p);
                            else
                                if (msg.sm.gpios[Convert.ToInt32(GetHashTableValueByKey("GPIO", p.sfllist[sflname].nodetable))] == true)
                                dynamicdatalist.parameterlist.Add(p);
                        }
                }
                //根据pIndexGPIO来设置pUsability
                /*if (cPnl.cViewModel.pIndexGPIO != 9999)  //如果这个参数带有GPIO控制位
                    cPnl.cViewModel.pUsability = !msg.sm.gpios[cPnl.cViewModel.pIndexGPIO];*/    //则根据API来控制其显示
                //更新dynamicdatalist
                foreach (Parameter p in plist)
                {
                    if (GetHashTableValueByKey("Group", p.sfllist[sflname].nodetable) == "2")
                        if (GetHashTableValueByKey("SubGroup", p.sfllist[sflname].nodetable) == "0")
                        {
                            dynamicdatalist.parameterlist.Remove(p);
                            if (GetHashTableValueByKey("GPIO", p.sfllist[sflname].nodetable) == "NoSuchKey")//没有GPIO节点
                                dynamicdatalist.parameterlist.Add(p);
                            else
                                if (msg.sm.gpios[Convert.ToInt32(GetHashTableValueByKey("GPIO", p.sfllist[sflname].nodetable))] == true)
                                dynamicdatalist.parameterlist.Add(p);
                        }
                }
            }
        }   //更新UI的同时，也更新了原始数据dynamicdatalist


        private delegate void UpdateMonitorDynamicUIDelegate();

        /// <summary>
        /// 根据dynamicdatalist中parameter的phydata，来更新ViewModel中的值，从而改变UI
        /// </summary>
        public void UpdateMonitorDynamicUI()
        {
            foreach (Parameter param in dynamicdatalist.parameterlist)
            {
                //if (param.phydata != -999999)
                {
                    if (GetHashTableValueByKey("Group", param.sfllist[sflname].nodetable) == "0")
                    {
                        if (GetHashTableValueByKey("SubGroup", param.sfllist[sflname].nodetable) == "0")
                        {
                            int i = Convert.ToInt32(GetHashTableValueByKey("Order", param.sfllist[sflname].nodetable)); //cell id == i+1
                            if (msg.sm.dic.Count != 0)
                            {
                                uint pos = msg.sm.dic.Keys.ElementAt(i);    //the position we want to display cell n
                                vPnl.vViewModel.voltageList[(int)pos].pValue = param.phydata;
                            }
                            else
                                vPnl.vViewModel.voltageList[i].pValue = param.phydata;
                        }
                        else if (GetHashTableValueByKey("SubGroup", param.sfllist[sflname].nodetable) == "5")
                        {
                            int i = Convert.ToInt32(GetHashTableValueByKey("Order", param.sfllist[sflname].nodetable));
                            vPnl.vViewModel.voltageList[i].pBleeding = Convert.ToBoolean(param.phydata);
                        }
                    }
                    else if (GetHashTableValueByKey("Group", param.sfllist[sflname].nodetable) == "1")
                    {
                        if (GetHashTableValueByKey("SubGroup", param.sfllist[sflname].nodetable) == "0")
                        {
                            int i = Convert.ToInt32(GetHashTableValueByKey("Order", param.sfllist[sflname].nodetable), 16);
                            tPnl.tViewModel.itemperatureList[i].pValue = param.phydata;
                        }
                        else if (GetHashTableValueByKey("SubGroup", param.sfllist[sflname].nodetable) == "1")   //更新EOT
                        {
                            /*if (EDOT != -9999 && cPnl.cViewModel.pCharge != null && cPnl.cViewModel.pDischarge != null)   //如果EDOT存在
                            {
                                if ((bool)cPnl.cViewModel.pDischarge) //如果在放电
                                {
                                    //if (tPnl.tViewModel.pEOTTH != EDOT)
                                    {
                                        tPnl.tViewModel.pEOTTH = EDOT;
                                        foreach (CellTemperature ct in tPnl.tViewModel.etemperatureList)
                                        {
                                            ct.pMaxValue = EDOT;
                                        }
                                    }
                                }
                                else //if ((bool)cPnl.cViewModel.pCharge) //如果在充电
                                {
                                    //if (tPnl.tViewModel.pEOTTH != ECOT)
                                    {
                                        tPnl.tViewModel.pEOTTH = ECOT;
                                        foreach (CellTemperature ct in tPnl.tViewModel.etemperatureList)
                                        {
                                            ct.pMaxValue = ECOT;
                                        }
                                    }
                                }
                            }*/
                            int i = Convert.ToInt32(GetHashTableValueByKey("Order", param.sfllist[sflname].nodetable), 16);
                            tPnl.tViewModel.etemperatureList[i].pValue = param.phydata;
                        }
                    }
                    else if (GetHashTableValueByKey("Group", param.sfllist[sflname].nodetable) == "2")
                    {
                        if (GetHashTableValueByKey("SubGroup", param.sfllist[sflname].nodetable) == "0")
                        {
                            int i;
                            if (!param.sfllist[sflname].nodetable.ContainsKey("Order"))
                                i = 0;
                            else
                                i = Convert.ToInt32(GetHashTableValueByKey("Order", param.sfllist[sflname].nodetable), 16);
                            cPnl.cViewModel[i].pValue = param.phydata;
                        }
                        /*else if (GetHashTableValueByKey("SubGroup", param.sfllist[sflname].nodetable) == "3")
                        {
                            cPnl.cViewModel.pCharge = Convert.ToBoolean(param.phydata);
                        }
                        else if (GetHashTableValueByKey("SubGroup", param.sfllist[sflname].nodetable) == "4")
                        {
                            cPnl.cViewModel.pDischarge = Convert.ToBoolean(param.phydata);
                        }*/
                    }
                    else if (GetHashTableValueByKey("Group", param.sfllist[sflname].nodetable) == "3")
                    {
                        if (GetHashTableValueByKey("SubGroup", param.sfllist[sflname].nodetable) == "0")
                        {
                            int i = Convert.ToInt32(GetHashTableValueByKey("Order", param.sfllist[sflname].nodetable), 16);
                            fdPnl.pfdViewModel.FetDisableList[i].pValue = Convert.ToBoolean(param.phydata);
                        }
                        else if (GetHashTableValueByKey("SubGroup", param.sfllist[sflname].nodetable) == "1")
                        {
                            int i = Convert.ToInt32(GetHashTableValueByKey("Order", param.sfllist[sflname].nodetable), 16);
                            fdPnl.pfdViewModel.FetDisableList[i].pTimer = Convert.ToInt32(param.phydata);
                        }
                    }
                    else if (GetHashTableValueByKey("Group", param.sfllist[sflname].nodetable) == "4")
                    {
                        int i = Convert.ToInt32(GetHashTableValueByKey("Order", param.sfllist[sflname].nodetable), 16);
                        sePnl.pseViewModel.SafetyEventList[i].pValue = Convert.ToBoolean(param.phydata);
                    }
                    else if (GetHashTableValueByKey("Group", param.sfllist[sflname].nodetable) == "6")
                    {
                        int i = Convert.ToInt32(GetHashTableValueByKey("Order", param.sfllist[sflname].nodetable), 16);
                        mcPnl.pmcViewModel.MiscList[i].pValue = Convert.ToDouble(param.phydata);
                    }
                }
            }
        }
        private delegate void UpdateLogUIDelegate();
        private void UpdateLogUI()
        {
            DataRow row = logUIdata.logbuf.NewRow();
            foreach (Parameter param in dynamicdatalist.parameterlist)
            {
                string str = GetHashTableValueByKey("LogName", param.sfllist[sflname].nodetable);

                decimal num = new decimal((double)param.phydata);

                row[str] = Decimal.Round(num, 1).ToString();
            }
            row["Time"] = DateTime.Now;
            if (logUIdata.logbuf.Rows.Count >= 1000)
                logUIdata.logbuf.Rows.RemoveAt(0);
            logUIdata.logbuf.Rows.Add(row);
            loguidatagrid.ScrollIntoView(loguidatagrid.Items[loguidatagrid.Items.Count - 1]); //scroll to the last item
            //*/
        }

        private delegate void UpdateUIDelegate();
        private void UpdateUI()
        {
            UpdateLogUI();
            UpdateMonitorDynamicUI();
        }
        private void BuildRestriction()
        {
#if false
            Vplotter.Viewport.AutoFitToView = true;
            OscilloscopeRestriction restr = new OscilloscopeRestriction(Vplotter.Viewport.Visible.Top, Vplotter.Viewport.Visible.Bottom, Vplotter.Viewport.Visible.Width, this);
            //restr.Width = 20;
            //restr.Top = 0;
            //restr.Bottom = 5000;
            Vplotter.Viewport.Restrictions.Add(restr);

            Vplotter.Viewport.FitToView();
#endif
        }

        private void RemoveRestriction()
        {
#if false
            Rect oldVisible = Vplotter.Viewport.Visible;
            RestrictionCollection rc = new RestrictionCollection();
            foreach (object obj in Vplotter.Viewport.Restrictions)
            {
                string str = obj.GetType().ToString();
                if (obj.GetType().ToString() == "Cobra.MonitorPanel.OscilloscopeRestriction")
                    rc.Add((IViewportRestriction)obj);
            }
            foreach (IViewportRestriction ivr in rc)
            {
                Vplotter.Viewport.Restrictions.Remove(ivr);
            }
            Vplotter.Viewport.Visible = oldVisible;
            //Vplotter.Viewport.FitToView();*/
#endif
        }

        private void DeleteColumn(ColumnDefinitionCollection cdc, string name)
        {
            ColumnDefinition CD = null;
            foreach (ColumnDefinition cd in cdc)
            {
                if (cd.Name == name)
                {
                    CD = cd;
                    break;
                }
            }
            if (CD != null)
                cdc.Remove(CD);
        }

        private void DeleteColumn(ColumnDefinitionCollection cdc, int pos)
        {
            ColumnDefinition CD = null;
            foreach (ColumnDefinition cd in cdc)
            {
                if ((int)cd.GetValue(Grid.ColumnProperty) == pos)
                {
                    CD = cd;
                    break;
                }
            }
            if (CD != null)
                cdc.Remove(CD);
        }
        #endregion

        public MainControl(object pParent, string name)
        {
            this.InitializeComponent();

            #region 相关初始化
            parent = (Device)pParent;
            if (parent == null) return;

            sflname = name;
            if (String.IsNullOrEmpty(sflname)) return;

            //gm.PropertyChanged += new PropertyChangedEventHandler(gm_PropertyChanged);
            //msg.PropertyChanged += new PropertyChangedEventHandler(msg_PropertyChanged);
            //msg.gm.PropertyChanged += new PropertyChangedEventHandler(gm_PropertyChanged);


            WarningPopControl.SetParent(MyGrid);
            #endregion

            #region log初始化
#if SupportCobraLog
            //Get folder name
            string logfolder = System.IO.Path.Combine(FolderMap.m_currentproj_folder, "ScanLog\\");
            if (!Directory.Exists(logfolder))
                Directory.CreateDirectory(logfolder);
            scanlog = new CobraLog(logfolder, 10);
            //将目录中已有的可识别的logdata加入scanlog.logdatalist中
            scanlog.SyncLogData();
#else
            UpdateLogDataList();
#endif
            #endregion

            #region 初始化interval和SubTask
            string str_option = String.Empty;
            XmlNodeList nodelist = parent.GetUINodeList(sflname);
            foreach (XmlNode node in nodelist)
            {
                str_option = node.Name;
                switch (str_option)
                {
                    case "ScanRate":
                        {
                            foreach (XmlNode sub in node)
                            {
                                str_option = sub.InnerText;
                                scanratelist.Add(str_option);
                            }
                            ScanInterval.SelectedIndex = 0;
                            break;
                        }
                    case "SubTask":
                        {
                            foreach (XmlNode sub in node)
                            {
                                subtasklist.Add(sub.Name, Convert.ToUInt16(sub.InnerText));
                            }
                            SubTask.SelectedIndex = 0;
                            break;
                        }
                    case "Section":
                        {
                            if (!node.HasChildNodes) break;
                            foreach (XmlNode subnode in node.ChildNodes)
                            {
                                setModel smodel = new setModel(subnode);
                                options.Add(smodel);
                                optionsDictionary.Add(smodel.nickname, smodel.m_Item_dic[smodel.itemlist[(UInt16)smodel.phydata]]);
                            }
                            break;
                        }
                }
            }

            if (scanratelist.Count == 0)
                ScanInterval.Visibility = Visibility.Collapsed;
            if (subtasklist.Count == 0)
                SubTask.Visibility = Visibility.Collapsed;
            if (optionsDictionary.Count == 0)
                ConfigBtn.Visibility = Visibility.Collapsed;
            else
                optionsJson = SharedAPI.SerializeDictionaryToJsonString(optionsDictionary);
            #endregion
            ECOT = -9999;
            EDOT = -9999;

            #region Data Binding
            vPnl.parent = this;
            tPnl.parent = this;
            cPnl.parent = this;
            sePnl.parent = this;
            mcPnl.parent = this;
#if SupportCobraLog
            loglist.ItemsSource = scanlog.logdatalist;
#else
            loglist.ItemsSource = logdatalist;
#endif
            loguidatagrid.DataContext = logUIdata.logbuf;
            //DataSelector.ItemsSource = logUIdata.logbuf.Columns;
            DataSelector.ItemsSource = logUIdata.isDisplay;  //只有跟带有INotifyCollectionChanged接口的ObservableCollection型集合绑定，才能在集合item数目发生变化时更新UI
            ScanInterval.ItemsSource = scanratelist;
            SubTask.ItemsSource = subtasklist.Keys;
            #endregion

            #region initialize static and dynamic data list and monitor UI, based on parameterlist, which based on XML file
            scanlist = parent.GetParamLists(sflname).parameterlist;

            AsyncObservableCollection<Parameter> voltagelist = new AsyncObservableCollection<Parameter>();
            int CellNum = 0;
            string grp, subgrp;
            try
            {
                foreach (Parameter param in scanlist)  //第一遍轮询，从XML对应的parameter中提取私有属性，给viewmodel赋初值，并将parameter归入读取容器，以便Run时使用
                {
                    grp = GetHashTableValueByKey("Group", param.sfllist[sflname].nodetable);
                    switch (grp)
                    {
                        #region VoltageGroup init
                        case "0":
                            subgrp = GetHashTableValueByKey("SubGroup", param.sfllist[sflname].nodetable);
                            switch (subgrp)
                            {
                                case "0":
                                    CellVoltage cv = new CellVoltage(vPnl.vViewModel);
                                    cv.pIndex = Convert.ToInt32(GetHashTableValueByKey("Order", param.sfllist[sflname].nodetable));
                                    cv.pTip = GetHashTableValueByKey("Description", param.sfllist[sflname].nodetable);
                                    //cv.pUsability = Convert.ToBoolean(GetHashTableValueByKey("Usability", param.sfllist[sflname].nodetable));
                                    cv.pValue = Convert.ToDouble(GetHashTableValueByKey("DefValue", param.sfllist[sflname].nodetable));
                                    cv.pMaxValue = Convert.ToDouble(GetHashTableValueByKey("MaxValue", param.sfllist[sflname].nodetable));
                                    cv.pMinValue = Convert.ToDouble(GetHashTableValueByKey("MinValue", param.sfllist[sflname].nodetable));
                                    vPnl.vViewModel.voltageList.Add(cv);
                                    voltagelist.Add(param);
                                    break;
                                case "1":
                                    vPnl.vViewModel.pOVTH = Convert.ToDouble(GetHashTableValueByKey("DefValue", param.sfllist[sflname].nodetable));
                                    staticdatalist.parameterlist.Add(param);
                                    break;
                                case "2":
                                    vPnl.vViewModel.pUVTH = Convert.ToDouble(GetHashTableValueByKey("DefValue", param.sfllist[sflname].nodetable));
                                    staticdatalist.parameterlist.Add(param);
                                    break;
                                case "3":
                                    CellNum = Convert.ToInt32(GetHashTableValueByKey("DefValue", param.sfllist[sflname].nodetable));
                                    staticdatalist.parameterlist.Add(param);
                                    break;
                                    //case "5"://Bleeding在第二次轮询加入，因为第一次cell voltage list可能顺序还不对
                                    //break;
                            }
                            break;
                        #endregion
                        #region TemperatrueGroup init
                        case "1":
                            subgrp = GetHashTableValueByKey("SubGroup", param.sfllist[sflname].nodetable);
                            CellTemperature ct = new CellTemperature(tPnl.tViewModel);
                            switch (subgrp)
                            {
                                case "0":   //内部温度
                                    ct.pIndex = Convert.ToInt32(GetHashTableValueByKey("Order", param.sfllist[sflname].nodetable), 16);
                                    ct.pTip = GetHashTableValueByKey("Description", param.sfllist[sflname].nodetable);
                                    ct.pUsability = Convert.ToBoolean(GetHashTableValueByKey("Usability", param.sfllist[sflname].nodetable));
                                    ct.pValue = Convert.ToDouble(GetHashTableValueByKey("DefValue", param.sfllist[sflname].nodetable));
                                    ct.pMaxValue = Convert.ToDouble(GetHashTableValueByKey("MaxValue", param.sfllist[sflname].nodetable));
                                    ct.pMinValue = Convert.ToDouble(GetHashTableValueByKey("MinValue", param.sfllist[sflname].nodetable));
                                    ct.pLabel = GetHashTableValueByKey("NickName", param.sfllist[sflname].nodetable);
                                    tPnl.tViewModel.itemperatureList.Add(ct);
                                    dynamicdatalist.parameterlist.Add(param);
                                    break;
                                case "1":   //外部温度
                                            //CellTemperature ct = new CellTemperature(tPnl.tViewModel);
                                    ct.pIndex = Convert.ToInt32(GetHashTableValueByKey("Order", param.sfllist[sflname].nodetable), 16);
                                    ct.pTip = GetHashTableValueByKey("Description", param.sfllist[sflname].nodetable);
                                    ct.pUsability = Convert.ToBoolean(GetHashTableValueByKey("Usability", param.sfllist[sflname].nodetable));
                                    ct.pValue = Convert.ToDouble(GetHashTableValueByKey("DefValue", param.sfllist[sflname].nodetable));
                                    ct.pMaxValue = Convert.ToDouble(GetHashTableValueByKey("MaxValue", param.sfllist[sflname].nodetable));
                                    ct.pMinValue = Convert.ToDouble(GetHashTableValueByKey("MinValue", param.sfllist[sflname].nodetable));
                                    ct.pLabel = GetHashTableValueByKey("NickName", param.sfllist[sflname].nodetable);
                                    //string str = GetHashTableValueByKey("GPIO", param.sfllist[sflname].nodetable);
                                    if (GetHashTableValueByKey("GPIO", param.sfllist[sflname].nodetable) == "NoSuchKey")    //如果没有这个节点
                                        ct.pIndexGPIO = 9999;
                                    else
                                    {
                                        ct.pIndexGPIO = Convert.ToInt32(GetHashTableValueByKey("GPIO", param.sfllist[sflname].nodetable));
                                        gpio_update = true; // need to update UI according to gpio
                                    }
                                    tPnl.tViewModel.etemperatureList.Add(ct);
                                    dynamicdatalist.parameterlist.Add(param);
                                    break;
                                case "2":
                                    tPnl.tViewModel.pIOTTH = Convert.ToDouble(GetHashTableValueByKey("DefValue", param.sfllist[sflname].nodetable));
                                    staticdatalist.parameterlist.Add(param);
                                    break;
                                case "3":
                                    tPnl.tViewModel.pIUTTH = Convert.ToDouble(GetHashTableValueByKey("DefValue", param.sfllist[sflname].nodetable));
                                    staticdatalist.parameterlist.Add(param);
                                    break;
                                case "4":
                                    ECOT = Convert.ToDouble(GetHashTableValueByKey("DefValue", param.sfllist[sflname].nodetable)); //将ECOT值保存到本地
                                    tPnl.tViewModel.pEOTTH = ECOT;
                                    staticdatalist.parameterlist.Add(param);
                                    break;
                                case "5":
                                    tPnl.tViewModel.pEUTTH = Convert.ToDouble(GetHashTableValueByKey("DefValue", param.sfllist[sflname].nodetable));
                                    staticdatalist.parameterlist.Add(param);
                                    break;
                                case "6":        //如果有EDOT
                                    EDOT = Convert.ToDouble(GetHashTableValueByKey("DefValue", param.sfllist[sflname].nodetable)); //将EDOT值保存到本地
                                    staticdatalist.parameterlist.Add(param);
                                    break;
                            }
                            break;
                        #endregion*/
                        #region CurrentGroup init
                        case "2":
                            subgrp = GetHashTableValueByKey("SubGroup", param.sfllist[sflname].nodetable);
                            switch (subgrp)
                            {
                                case "0":
                                    CellCurrent cc = new CellCurrent();
                                    cc.pValue = Convert.ToDouble(GetHashTableValueByKey("DefValue", param.sfllist[sflname].nodetable));
                                    cc.pMaxValue = Convert.ToDouble(GetHashTableValueByKey("MaxValue", param.sfllist[sflname].nodetable));
                                    cc.pMinValue = Convert.ToDouble(GetHashTableValueByKey("MinValue", param.sfllist[sflname].nodetable));
                                    if (!param.sfllist[sflname].nodetable.ContainsKey("Order"))
                                        cc.pIndex = 0;
                                    else
                                        cc.pIndex = Convert.ToInt32(GetHashTableValueByKey("Order", param.sfllist[sflname].nodetable));
                                    cc.pLabel = GetHashTableValueByKey("NickName", param.sfllist[sflname].nodetable);
                                    cc.pUsability = Convert.ToBoolean(GetHashTableValueByKey("Usability", param.sfllist[sflname].nodetable));  //有电流值，则设为可以显示
                                    if (GetHashTableValueByKey("GPIO", param.sfllist[sflname].nodetable) == "NoSuchKey")    //如果没有这个节点
                                        cc.pIndexGPIO = 9999;
                                    else
                                    {
                                        cc.pIndexGPIO = Convert.ToInt32(GetHashTableValueByKey("GPIO", param.sfllist[sflname].nodetable));
                                        gpio_update = true;
                                    }
                                    cPnl.cViewModel.Add(cc);
                                    dynamicdatalist.parameterlist.Add(param);
                                    break;
                                    /*case "1":
                                        cPnl.cViewModel.pCOCTH = Convert.ToDouble(GetHashTableValueByKey("DefValue", param.sfllist[sflname].nodetable));
                                        staticdatalist.parameterlist.Add(param);
                                        break;
                                    case "2":
                                        cPnl.cViewModel.pDOCTH = Convert.ToDouble(GetHashTableValueByKey("DefValue", param.sfllist[sflname].nodetable));
                                        staticdatalist.parameterlist.Add(param);
                                        break;
                                    case "3":
                                        cPnl.cViewModel.pCharge = Convert.ToBoolean(GetHashTableValueByKey("DefValue", param.sfllist[sflname].nodetable));
                                        dynamicdatalist.parameterlist.Add(param);
                                        break;
                                    case "4":
                                        cPnl.cViewModel.pDischarge = Convert.ToBoolean(GetHashTableValueByKey("DefValue", param.sfllist[sflname].nodetable));
                                        dynamicdatalist.parameterlist.Add(param);
                                        break;*/
                            }
                            break;
                        #endregion//*/
                        #region FDGroup init
                        case "3":           //第一次轮询找出所有的FD（但不管timer）
                            subgrp = GetHashTableValueByKey("SubGroup", param.sfllist[sflname].nodetable);
                            switch (subgrp)
                            {
                                case "0":
                                    FetDisable fd = new FetDisable(fdPnl.pfdViewModel);
                                    fd.pIndex = Convert.ToInt32(GetHashTableValueByKey("Order", param.sfllist[sflname].nodetable), 16);
                                    fd.pTip = GetHashTableValueByKey("Description", param.sfllist[sflname].nodetable);
                                    fd.pLabel = GetHashTableValueByKey("NickName", param.sfllist[sflname].nodetable);
                                    //cv.pUsability = Convert.ToBoolean(GetHashTableValueByKey("Usability", param.sfllist[sflname].nodetable));
                                    fd.pValue = (Convert.ToInt16(GetHashTableValueByKey("DefValue", param.sfllist[sflname].nodetable)) != 0) ? true : false;
                                    fd.pTimer = null;
                                    fdPnl.pfdViewModel.FetDisableList.Add(fd);
                                    //fdlist.Add(param);
                                    dynamicdatalist.parameterlist.Add(param);
                                    break;
                            }
                            break;
                        #endregion
                        #region SEGroup init
                        case "4":
                            SafetyEvent se = new SafetyEvent(sePnl.pseViewModel);
                            se.pIndex = Convert.ToInt32(GetHashTableValueByKey("Order", param.sfllist[sflname].nodetable), 16);
                            se.pTip = GetHashTableValueByKey("Description", param.sfllist[sflname].nodetable);
                            se.pLabel = GetHashTableValueByKey("NickName", param.sfllist[sflname].nodetable);
                            //cv.pUsability = Convert.ToBoolean(GetHashTableValueByKey("Usability", param.sfllist[sflname].nodetable));
                            se.pValue = (Convert.ToInt16(GetHashTableValueByKey("DefValue", param.sfllist[sflname].nodetable)) != 0) ? true : false;
                            if (param.sfllist[sflname].nodetable.Contains("BClearable"))
                                se.pClearable = Convert.ToBoolean(GetHashTableValueByKey("BClearable", param.sfllist[sflname].nodetable));
                            else
                                se.pClearable = true;
                            se.pParam = param;
                            sePnl.pseViewModel.SafetyEventList.Add(se);
                            //fdlist.Add(param);
                            dynamicdatalist.parameterlist.Add(param);
                            break;
                        #endregion
                        #region MISCGroup init
                        case "6":
                            Misc mc = new Misc(mcPnl.pmcViewModel);
                            mc.pIndex = Convert.ToInt32(GetHashTableValueByKey("Order", param.sfllist[sflname].nodetable), 16);
                            mc.pTip = GetHashTableValueByKey("Description", param.sfllist[sflname].nodetable);
                            mc.pLabel = GetHashTableValueByKey("NickName", param.sfllist[sflname].nodetable);
                            mc.pValue = Convert.ToDouble(GetHashTableValueByKey("DefValue", param.sfllist[sflname].nodetable));
                            mc.pParam = param;
                            mcPnl.pmcViewModel.MiscList.Add(mc);
                            dynamicdatalist.parameterlist.Add(param);
                            break;
                        #endregion
                        #region Special Group
                        case "7":
                            /*subgrp = GetHashTableValueByKey("SubGroup", param.sfllist[sflname].nodetable);
                            switch (subgrp)
                            {
                                case "0":
                                    break;
                                case "1":
                                    break;
                                case "2":
                                    break;
                                case "3":
                                    break;
                                case "4":
                                    break;
                            }*/
                            staticdatalist.parameterlist.Add(param);
                            break;
                            #endregion
                    }

                    //param.PropertyChanged += new PropertyChangedEventHandler(param_PropertyChanged);  //用RefreshUI替代
                }
            }
            catch (System.Exception e)
            {
                MessageBox.Show(e.Message);
            }

            UpdateVoltageDisplay(CellNum);

            foreach (Parameter param in voltagelist)
            {
                if (CellNum == 0)   //对于那些没有Cell Number寄存器的芯片
                {
                    dynamicdatalist.parameterlist.Add(param);
                }
                else   //对于那些有Cell Number寄存器的芯片
                {
                    if (Convert.ToInt32(GetHashTableValueByKey("Order", param.sfllist[sflname].nodetable)) < CellNum)
                        dynamicdatalist.parameterlist.Add(param);
                }
            }


            vPnl.vViewModel.voltageList.Sort(x => x.pIndex);
            tPnl.tViewModel.itemperatureList.Sort(x => x.pIndex);
            cPnl.cViewModel.Sort(x => x.pIndex);
            tPnl.tViewModel.etemperatureList.Sort(x => x.pIndex);
            fdPnl.pfdViewModel.FetDisableList.Sort(x => x.pIndex);
            sePnl.pseViewModel.SafetyEventList.Sort(x => x.pIndex);
            mcPnl.pmcViewModel.MiscList.Sort(x => x.pIndex);

            foreach (Parameter param in scanlist)      //第二次轮询
            {
                if (GetHashTableValueByKey("Group", param.sfllist[sflname].nodetable) == "3")//找出所有的timer
                {
                    if (GetHashTableValueByKey("SubGroup", param.sfllist[sflname].nodetable) == "1")
                    {
                        fdPnl.pfdViewModel.FetDisableList[Convert.ToInt32(GetHashTableValueByKey("Order", param.sfllist[sflname].nodetable), 16)].pTimer = Convert.ToInt32(GetHashTableValueByKey("DefValue", param.sfllist[sflname].nodetable));
                        fdPnl.pfdViewModel.FetDisableList[Convert.ToInt32(GetHashTableValueByKey("Order", param.sfllist[sflname].nodetable), 16)].pTimerTip = GetHashTableValueByKey("Description", param.sfllist[sflname].nodetable);
                        //cv.pUsability = Convert.ToBoolean(GetHashTableValueByKey("Usability", param.sfllist[sflname].nodetable));
                        //fd.pValue = Convert.ToBoolean(GetHashTableValueByKey("DefValue", param.sfllist[sflname].nodetable));

                        //fdlist.Add(param);
                        dynamicdatalist.parameterlist.Add(param);
                    }
                }
                if (GetHashTableValueByKey("Group", param.sfllist[sflname].nodetable) == "0")//Bleeding
                {
                    if (GetHashTableValueByKey("SubGroup", param.sfllist[sflname].nodetable) == "5")    //如果没有这个参数，那么pBleeding会维持空值
                    {
                        int i = Convert.ToInt32(GetHashTableValueByKey("Order", param.sfllist[sflname].nodetable));
                        vPnl.vViewModel.voltageList[i].pBleeding = Convert.ToBoolean(GetHashTableValueByKey("DefValue", param.sfllist[sflname].nodetable));
                        dynamicdatalist.parameterlist.Add(param);
                    }
                }
            }

            #region Monitor UI re-initialize
            #region 根据TH值，重新赋值MaxValue和MinValue属性，如果TH值为null，则MaxValue和MinValue保持不变（XML中定义的值）
            if (vPnl.vViewModel.pOVTH != null && vPnl.vViewModel.pUVTH != null)
                foreach (CellVoltage cv in vPnl.vViewModel.voltageList)
                {
                    cv.pMaxValue = (double)vPnl.vViewModel.pOVTH;
                    cv.pMinValue = (double)vPnl.vViewModel.pUVTH;
                }
            if (tPnl.tViewModel.pIOTTH != null && tPnl.tViewModel.pIUTTH != null)
                foreach (CellTemperature ct in tPnl.tViewModel.itemperatureList)
                {
                    ct.pMaxValue = (double)tPnl.tViewModel.pIOTTH;
                    ct.pMinValue = (double)tPnl.tViewModel.pIUTTH;
                }
            if (tPnl.tViewModel.pEOTTH != null && tPnl.tViewModel.pEUTTH != null)
                foreach (CellTemperature ct in tPnl.tViewModel.etemperatureList)
                {
                    ct.pMaxValue = (double)tPnl.tViewModel.pEOTTH;
                    ct.pMinValue = (double)tPnl.tViewModel.pEUTTH;
                }
            /*if (cPnl.cViewModel.pCOCTH != null && cPnl.cViewModel.pDOCTH != null)
            {
                cPnl.cViewModel.pMaxValue = (double)cPnl.cViewModel.pCOCTH;
                cPnl.cViewModel.pMinValue = (double)cPnl.cViewModel.pDOCTH;
            }*/
            #endregion

            #region 判断group是否留空
            bool vPnl_empty = false;
            bool tPnl_empty = false;
            bool tPnl_it_empty = false;
            bool tPnl_et_empty = false;
            bool cPnl_empty = false;
            bool fdPnl_empty = false;
            bool sePnl_empty = false;
            bool mcPnl_empty = false;
            if (vPnl.vViewModel.voltageList.Count == 0)
            {
                vPnl.Visibility = Visibility.Collapsed;
                //MyGrid.RowDefinitions.Remove(MyGrid.RowDefinitions[0]);
                //CellVoltage fakecv = new CellVoltage(vPnl.vViewModel);  //放入假值，以免converters出错
                //vPnl.vViewModel.voltageList.Add(fakecv);
                vPnl_empty = true;
            }
            else
            {
                if (vPnl.vViewModel.pOVTH == null)
                {
                    vPnl.OVTH.Visibility = System.Windows.Visibility.Collapsed;
                    vPnl.L2.Visibility = System.Windows.Visibility.Collapsed;
                }
                if (vPnl.vViewModel.pUVTH == null)
                {
                    vPnl.UVTH.Visibility = System.Windows.Visibility.Collapsed;
                    vPnl.L1.Visibility = System.Windows.Visibility.Collapsed;
                }
            }
            if (tPnl.tViewModel.itemperatureList.Count == 0 && tPnl.tViewModel.etemperatureList.Count == 0)
            {
                tPnl.Visibility = Visibility.Collapsed;
                tPnl_empty = true;
                tPnl_et_empty = true;
                tPnl_it_empty = true;
                //TCGrid.ColumnDefinitions.Remove(TCGrid.ColumnDefinitions[0]);
            }
            else
            {
                if (tPnl.tViewModel.pIOTTH == null)
                {
                    tPnl.IOTTH.Visibility = System.Windows.Visibility.Collapsed;
                    tPnl.itL2.Visibility = System.Windows.Visibility.Collapsed;
                }
                if (tPnl.tViewModel.pIUTTH == null)
                {
                    tPnl.IUTTH.Visibility = System.Windows.Visibility.Collapsed;
                    tPnl.itL1.Visibility = System.Windows.Visibility.Collapsed;
                }
                if (tPnl.tViewModel.pEOTTH == null)
                {
                    tPnl.EOTTH.Visibility = System.Windows.Visibility.Collapsed;
                    tPnl.etL2.Visibility = System.Windows.Visibility.Collapsed;
                }
                if (tPnl.tViewModel.pEUTTH == null)
                {
                    tPnl.EUTTH.Visibility = System.Windows.Visibility.Collapsed;
                    tPnl.etL1.Visibility = System.Windows.Visibility.Collapsed;
                }
            }

            if (tPnl.tViewModel.itemperatureList.Count == 0)
            {
                tPnl.igrid.Visibility = System.Windows.Visibility.Collapsed;
                tPnl_it_empty = true;

                //CellTemperature fakect = new CellTemperature(tPnl.tViewModel);//放入假值，以免converters出错
                //tPnl.tViewModel.itemperatureList.Add(fakect);
            }
            if (tPnl.tViewModel.etemperatureList.Count == 0)
            {
                tPnl.egrid.Visibility = System.Windows.Visibility.Collapsed;
                tPnl_et_empty = true;

                //CellTemperature fakect = new CellTemperature(tPnl.tViewModel);//放入假值，以免converters出错
                //tPnl.tViewModel.etemperatureList.Add(fakect);
            }

            if (cPnl.cViewModel.Count == 0)                     //如果为true，表示XML中没有这一项，那么将其隐藏
            {
                cPnl.Visibility = Visibility.Collapsed;
                cPnl_empty = true;
            }
            else
            {
                /*if (cPnl.cViewModel.pCOCTH == null)
                {
                    //cPnl.COCTH.Visibility = System.Windows.Visibility.Collapsed;
                    //cPnl.L2.Visibility = System.Windows.Visibility.Collapsed;
                    DoubleCollection dc = new DoubleCollection();
                    dc.Add(2);
                    dc.Add(2);
                    cPnl.L2a.StrokeDashArray = dc;
                }
                if (cPnl.cViewModel.pDOCTH == null)
                {
                    //cPnl.DOCTH.Visibility = System.Windows.Visibility.Collapsed;
                    //cPnl.L1.Visibility = System.Windows.Visibility.Collapsed;
                    DoubleCollection dc = new DoubleCollection();
                    dc.Add(2);
                    dc.Add(2);
                    cPnl.L2a.StrokeDashArray = dc;
                }*/
            }






            if (fdPnl.pfdViewModel.FetDisableList.Count == 0)
            {
                fdPnl.Visibility = Visibility.Collapsed;
                fdPnl_empty = true;
            }
            if (sePnl.pseViewModel.SafetyEventList.Count == 0)
            {
                sePnl.Visibility = Visibility.Collapsed;
                sePnl_empty = true;
            }
            if (mcPnl.pmcViewModel.MiscList.Count == 0)
            {
                mcPnl.Visibility = Visibility.Collapsed;
                mcPnl_empty = true;
            }


            if (cPnl_empty == true)
                TCGrid.ColumnDefinitions.Remove(TCGrid.ColumnDefinitions[1]);

            if (tPnl_empty == true)
                TCGrid.ColumnDefinitions.Remove(TCGrid.ColumnDefinitions[0]);
            else
            {
                if (tPnl_et_empty == true)
                    tPnl.maingrid.ColumnDefinitions.Remove(tPnl.maingrid.ColumnDefinitions[1]);
                if (tPnl_it_empty == true)
                    tPnl.maingrid.ColumnDefinitions.Remove(tPnl.maingrid.ColumnDefinitions[0]);
            }


            /*if (wePnl_empty == true)
                FSWGrid.ColumnDefinitions.Remove(FSWGrid.ColumnDefinitions[2]);
            if (fdPnl_empty == true)
                FSWGrid.ColumnDefinitions.RemoveAt(1);
            if (sePnl_empty == true)
                FSWGrid.ColumnDefinitions.Remove(FSWGrid.ColumnDefinitions[0]);*/

            /*if (mcPnl_empty == true)
                FSWGrid.ColumnDefinitions.Remove(FSWGrid.ColumnDefinitions[3]); 
            if (wePnl_empty == true)
                FSWGrid.ColumnDefinitions.RemoveAt(1);
            if (fdPnl_empty == true)
                FSWGrid.ColumnDefinitions.RemoveAt(2);
            if (sePnl_empty == true)
                FSWGrid.ColumnDefinitions.Remove(FSWGrid.ColumnDefinitions[0]);*/

            int MissingGroup = 0;


            if (sePnl_empty == true)
                MissingGroup++;
            if (fdPnl_empty == true)
                MissingGroup++;
            if (mcPnl_empty == true)
                MissingGroup++;

            for (; MissingGroup > 0; MissingGroup--)
            {
                DeleteColumn(FSWGrid.ColumnDefinitions, MissingGroup - 1);
            }

            if (sePnl_empty == false)
            {
                sePnl.SetValue(Grid.ColumnProperty, 0);
                if (fdPnl_empty == false)
                {
                    fdPnl.SetValue(Grid.ColumnProperty, 1);
                    if (mcPnl_empty == false)
                    {
                        mcPnl.SetValue(Grid.ColumnProperty, 2);
                    }
                }
                else
                {
                    if (mcPnl_empty == false)
                    {
                        mcPnl.SetValue(Grid.ColumnProperty, 1);
                    }
                }
            }
            else
            {
                if (fdPnl_empty == false)
                {
                    fdPnl.SetValue(Grid.ColumnProperty, 0);
                    if (mcPnl_empty == false)
                    {
                        mcPnl.SetValue(Grid.ColumnProperty, 1);
                    }
                }
                else
                {
                    if (mcPnl_empty == false)
                    {
                        mcPnl.SetValue(Grid.ColumnProperty, 0);
                    }
                }
            }
            /*
            if (sePnl_empty == true)
                DeleteColumn(FSWGrid.ColumnDefinitions, "FlagGroup");
            if (fdPnl_empty == true)
                DeleteColumn(FSWGrid.ColumnDefinitions, "StatusGroup");
            if (mcPnl_empty == true)
                DeleteColumn(FSWGrid.ColumnDefinitions, "MiscGroup");
            */
            if (vPnl_empty)
            {
                MyGrid.RowDefinitions.Remove(MyGrid.RowDefinitions[0]);
                if (cPnl_empty == true && tPnl_empty == true)
                {
                    MyGrid.RowDefinitions.Remove(MyGrid.RowDefinitions[0]);
                    Grid.SetRow(MyGrid.Children[2], 0);
                }
                else
                {
                    Grid.SetRow(MyGrid.Children[1], 0);
                    Grid.SetRow(MyGrid.Children[2], 1);
                }
            }
            else
            {
                if (cPnl_empty == true && tPnl_empty == true)
                {
                    MyGrid.RowDefinitions.Remove(MyGrid.RowDefinitions[1]);
                    Grid.SetRow(MyGrid.Children[2], 1);
                }
            }
            //MyGrid.Children;
            #endregion

            #region 重新命名Group
            foreach (Parameter param in scanlist)      //
            {
                String str;
                if ((str = GetHashTableValueByKey("GroupName", param.sfllist[sflname].nodetable)) == "NoSuchKey")//找出所有的timer
                    continue;
                if (GetHashTableValueByKey("Group", param.sfllist[sflname].nodetable) == "0")
                {
                    vPnl.vGroup.Header = str;
                }
                if (GetHashTableValueByKey("Group", param.sfllist[sflname].nodetable) == "1")
                {
                    tPnl.tGroup.Header = str;
                }
                if (GetHashTableValueByKey("Group", param.sfllist[sflname].nodetable) == "2")
                {
                    cPnl.cGroup.Header = str;
                }
                if (GetHashTableValueByKey("Group", param.sfllist[sflname].nodetable) == "3")
                {
                    fdPnl.fdGroup.Header = str;
                }
                if (GetHashTableValueByKey("Group", param.sfllist[sflname].nodetable) == "4")
                {
                    sePnl.seGroup.Header = str;
                }
                if (GetHashTableValueByKey("Group", param.sfllist[sflname].nodetable) == "6")
                {
                    mcPnl.mcGroup.Header = str;
                }
            }
            #endregion
            #endregion
            #endregion

            #region event handler initialize
            //t.Tick += new EventHandler(t_Elapsed);
            t.Elapsed += new ElapsedEventHandler(t_Elapsed);
            #endregion

            #region LogViewer UI initialize
            //RebuildUISourceFromList();
            RebuildLineGraphFromList(); //这里必须有，不然在没有Run的情况下点Load会看不到曲线

            //Vplotter.Viewport.Visible = new Rect(0, 0, 120, 5000);
            /*#region Show Log UI Columns
            {
                DataRow row = logUIdata.logbuf.NewRow();
                foreach (Parameter param in dynamicdatalist.parameterlist)
                {
                    string str = GetHashTableValueByKey("LogName", param.sfllist[sflname].nodetable);
                    row[str] = "";
                }
                row["Time"] = DateTime.Now;
                logUIdata.logbuf.Rows.Add(row);
            }
            #endregion*/
            #endregion

            LibInfor.AssemblyRegister(Assembly.GetExecutingAssembly(), ASSEMBLY_TYPE.SFL);
        }



        #region DM提供的API
        public uint Command(ParamContainer pc, ushort subtask)
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_COMMAND;
            msg.sub_task = subtask;
            msg.task_parameterlist = pc;
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            return m_Msg.errorcode;
        }
        public uint CommandEX(ParamContainer pc)
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_COMMAND;
            msg.sub_task = msg.SUBTASK_JSON_MASK;
            msg.sub_task_json = optionsJson;
            msg.task_parameterlist = pc;
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            return m_Msg.errorcode;
        }
        public uint Read(ParamContainer pc)
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_READ;
            msg.task_parameterlist = pc;
            //msg.bupdate = false;            //需要从chip读数据
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            /*if (m_Msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                msg.bgworker.Dispose();
                msg.bgworker.CancelAsync();
            }*/
            return m_Msg.errorcode;
        }
        public uint Write(ParamContainer pc)
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_WRITE;
            msg.task_parameterlist = pc;
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            return m_Msg.errorcode;
        }
        public uint ConvertHexToPhysical(ParamContainer pc)
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_CONVERT_HEXTOPHYSICAL;
            msg.task_parameterlist = pc;
            //msg.bupdate = true;         //不用从chip读，只从img读
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            return m_Msg.errorcode;
        }
        public uint GetSysInfo()
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_SPEICAL_GETSYSTEMINFOR;
            //msg.bupdate = false;            //需要从chip读数据
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            return m_Msg.errorcode;
        }
        public uint GetDevInfo()
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_SPEICAL_GETDEVICEINFOR;
            //msg.bupdate = false;            //需要从chip读数据
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            return m_Msg.errorcode;
        }
        public uint ClearBit(ParamContainer pc)
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_BITOPERATION;
            msg.task_parameterlist = pc;
            //msg.bupdate = false;            //需要从chip读数据
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            return m_Msg.errorcode;
        }
        #endregion

        #region event handler

        public void CallWarningControl(uint errorcode)
        {
            gm.message = LibErrorCode.GetErrorDescription(errorcode);

            if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                WarningPopControl.Dispatcher.Invoke(new Action(() =>
                {
                    WarningPopControl.ShowDialog(gm);
                }));
            }
        }

        public void CallWarningControl(GeneralMessage gm)
        {
            WarningPopControl.Dispatcher.Invoke(new Action(() =>
            {
                WarningPopControl.ShowDialog(gm);
            }));
        }

        private void EnterStartState()
        {
            gm.controls = "Run button";
            gm.message = "Read Device";
            gm.bupdate = false;  //??

            runBtn.Content = "Stop";
            //runBtn.IsChecked = false;
            ScanInterval.IsEnabled = false;
            SubTask.IsEnabled = false;
            loglist.IsEnabled = false;
            BuildRestriction();
            //XNavi.IsEnabled = false;
            //YNavi.IsEnabled = false;
            //MNavi.IsEnabled = false;
            //MNavi.OnPlotterDetaching(Vplotter);
            t.Start();

            ThreadMonitor.Start();
#if FakeData
            for (int i = 0; i < 50000; i++)
            {
                DataRow row = logUIdata.logbuf.NewRow();
                foreach (Parameter param in dynamicdatalist.parameterlist)
                {
                    string str = GetHashTableValueByKey("LogName", param.sfllist[sflname].nodetable);

                    decimal num = new decimal((double)param.phydata);

                    row[str] = Decimal.Round(num, 1).ToString();
                }
                row["Time"] = DateTime.Now;
                logUIdata.logbuf.Rows.Add(row);
            }
#endif
        }

        private void ResetContext()
        {
            parent.bBusy = false;
            runBtn.Content = "Run";
            runBtn.IsChecked = false;
            ScanInterval.IsEnabled = true;
            SubTask.IsEnabled = true;
            loglist.IsEnabled = true;
            isReentrant_Run = false;
            UpdateDataColor(false);
        }


        private delegate void EnterStopStateDelegate(UInt32 errorcode, int session_id, ulong session_row_number);
        private void EnterStopState(UInt32 errorcode, int session_id, ulong session_row_number)
        {
            t.Stop(); 
            UpdateLogDataList(session_id, session_row_number);

            gm.controls = "Stop button";
            gm.message = LibErrorCode.GetErrorDescription(errorcode);
            gm.bupdate = true;

            ResetContext();
            RemoveRestriction();

            ThreadMonitor.Reset();
#if SupportThreadMonitor
            tm.Stop();
#endif
            CallWarningControl(errorcode);
        }


        public void PreRead(ref uint errorcode)
        {
            //uint errorcode = (uint)obj;
            #region 预读数据
            errorcode = GetSysInfo();                                           //读GPIO信息 
            if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                return;
            }
            errorcode = GetDevInfo();                                           //读设备信息
            if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                return;
            }
            Thread.Sleep(1000);
            errorcode = Read(staticdatalist);                                   //读一次静态数据
            if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                return;
            }
            errorcode = Read(dynamicdatalist);                                  //读一次动态数据
            if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                return;
            }
            errorcode = ConvertHexToPhysical(staticdatalist);                   //转换一次静态数据
            if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                return;
            }
            errorcode = ConvertHexToPhysical(dynamicdatalist);                  //转换一次动态数据
            if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                return;
            }
            #endregion
        }

        private void UpdateLogDataList(int session_id = 0, ulong session_row_number = 0)
        {
            List<List<String>> records = new List<List<string>>();
            parent.db_Manager.GetSessionsInfor(sflname, ref records);
            logdatalist.Clear();
            foreach (var record in records)
            {
                LogData ld = new LogData();
                ld.Timestamp = record[0];
                try
                {
                    ld.RecordNumber = Convert.ToInt64(record[1]);
                }
                catch
                {
                    ld.RecordNumber = -9999;
                }
                ld.DeviceNum = record[2];//Issue1428 Leon
                logdatalist.Add(ld);
            }
        }

        private void runBtn_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton btn = sender as ToggleButton;
            uint errorcode = 0;
            if (isReentrant_Run == false)   //此次点击并没有重入
            {
                isReentrant_Run = true;
                if ((bool)btn.IsChecked)    //点了Run
                {
                    if (parent.bBusy)       //Scan功能是否被其他SFL占用
                    {
                        errorcode = LibErrorCode.IDS_ERR_EM_THREAD_BKWORKER_BUSY;
                        ResetContext();
                        CallWarningControl(errorcode);
                        return;
                    }
                    else
                        parent.bBusy = true;

                    if (msg.bgworker.IsBusy == true) //bus是否正忙
                    {
                        errorcode = LibErrorCode.IDS_ERR_EM_THREAD_BKWORKER_BUSY;
                        ResetContext();
                        CallWarningControl(errorcode);
                        return;
                    }
                    //一切正常，可以开始scan
                    UpdateDataColor(true);
                    PreRead(ref errorcode);
                    if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    {
                        CallWarningControl(errorcode);
                        ResetContext();
                        return;
                    }
                    UpdateMonitorStaticUI();
                    UpdateMonitorDynamicUI();
                    session_row_number = 0;
                    parent.db_Manager.NewSession(sflname, ref session_id, DateTime.Now.ToString());
                    RebuildUISourceFromList();
                    loguidatagrid.DataContext = null;
                    loguidatagrid.DataContext = logUIdata.logbuf;
                    RebuildLineGraphFromList(); //不要在这里建，而在读到第一组数据后，因为要记录起始时间点

                    #region Scan rate
                    string interval = "";
                    if (scanratelist.Count != 0)
                    {
                        interval = ScanInterval.SelectedItem.ToString();
                    }
                    else
                    {
                        if (optionsDictionary.Count != 0)
                        {
                            interval = optionsDictionary["Scan Rate"];
                        }
                    }
                    if (interval.EndsWith("mS"))
                    {
                        interval = interval.Remove(interval.Length - 2);
                        t.Interval = Convert.ToDouble(interval);
                    }
                    else if (interval.EndsWith("S"))
                    {
                        interval = interval.Remove(interval.Length - 1);
                        t.Interval = Convert.ToDouble(interval) * 1000;
                    }
                    #endregion
                    //t.Interval = 1000;
                    EnterStartState();

#if SupportThreadMonitor
                    if (tm.Owner == null)
                    {
                        FrameworkElement p = (FrameworkElement)this.Parent;
                        FrameworkElement pp = (FrameworkElement)p.Parent;
                        FrameworkElement ppp = (FrameworkElement)pp.Parent;
                        FrameworkElement pppp = (FrameworkElement)ppp.Parent;
                        FrameworkElement ppppp = (FrameworkElement)pppp.Parent;
                        FrameworkElement pppppp = (FrameworkElement)ppppp.Parent;
                        FrameworkElement ppppppp = (FrameworkElement)pppppp.Parent;
                        FrameworkElement pppppppp = (FrameworkElement)ppppppp.Parent;
                        FrameworkElement p9 = (FrameworkElement)pppppppp.Parent;
                        tm.Owner = (Window)p9;
                    }
                        tm.Show();
                        tm.Start();
#endif
                }
                else    //点了stop
                {
                    EnterStopState(errorcode, session_id, session_row_number);
                    //ResetContext();
                    //UpdateLogDataList(session_id, session_row_number);
                }

                isReentrant_Run = false;
            }
            else  //重入了，需要将IsChecked属性还原
            {
                runBtn.IsChecked = !runBtn.IsChecked;
            }
        }

        private void UpdateDataColor(bool b)
        {
            foreach (var v in vPnl.vViewModel.voltageList)
            {
                v.IsRunning = b;
            }
            foreach (var c in cPnl.cViewModel)
            {
                c.IsRunning = b;
            }
            foreach (var it in tPnl.tViewModel.itemperatureList)
            {
                it.IsRunning = b;
            }
            foreach (var et in tPnl.tViewModel.etemperatureList)
            {
                et.IsRunning = b;
            }
            foreach (var m in mcPnl.pmcViewModel.MiscList)
            {
                m.IsRunning = b;
            }
        }

        private delegate ushort GetSubTaskDelegate();
        private ushort GetSubTask()
        {
            return subtasklist[(SubTask.SelectedItem).ToString()];
        }

        private void IO_Callback(object TimerCounter)
        {
            lock (IO_Lock)
            {
                uint errorcode = LibErrorCode.IDS_ERR_SUCCESSFUL;
                if (msg.bgworker.IsBusy == true) //bus是否正忙
                {
                    errorcode = LibErrorCode.IDS_ERR_EM_THREAD_BKWORKER_BUSY;
                    //if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    //EnterStopState(errorcode);
                }
                else
                {
                    if (subtasklist.Count != 0)     //当前oce支持subtask特性
                    {
                        //ushort st = subtask[(SubTask.SelectedItem).ToString()];
                        ushort st = (ushort)this.Dispatcher.Invoke(new GetSubTaskDelegate(GetSubTask));
                        errorcode = Command(dynamicdatalist, st);
                        //if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        //EnterStopState(errorcode);
                    }
                    else if (optionsDictionary.Count != 0)     //当前oce支持options
                    {
                        errorcode = CommandEX(dynamicdatalist);
                    }
                    else
                    {
                        errorcode = Read(dynamicdatalist);
                        //if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        //EnterStopState(errorcode);

                    }

                    if (errorcode == LibErrorCode.IDS_ERR_SUCCESSFUL)
                    {
                        errorcode = ConvertHexToPhysical(dynamicdatalist);
                        //if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        //EnterStopState(errorcode);
                    }
                    //if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    //{
                    //    this.Dispatcher.Invoke(new EnterStopStateDelegate(EnterStopState), errorcode, session_id, session_row_number);
                    //}
                    else
                    {
                        MarkError(dynamicdatalist.parameterlist);
                    }
                }
            }
        }

        private void MarkError(AsyncObservableCollection<Parameter> parameterlist)
        {
            foreach (var param in parameterlist)
            {
                param.phydata = -999999;
            }
        }

        private void UI_Callback(object TimerCounter)
        {
            lock (UI_Lock)
            {
                this.Dispatcher.Invoke(new UpdateUIDelegate(UpdateUI)); //如果直接在TimerCallback中调用，就没有办法观察UI Thread
            }
        }

        private void DB_Callback(object TimerCounter)
        {
            lock (DB_Lock)
            {
                Dictionary<string, string> records = SnapShot.TimerCallbackPool[(long)TimerCounter].LogRow;   //取出快照
                try
                {
                    parent.db_Manager.BeginNewRow(session_id, records);
                    session_row_number += 1;
                    parent.db_Manager.UpdateSessionSize(session_id, session_row_number);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(ex.Message + ex.InnerException.Message);
                }
                SnapShot.TimerCallbackPool[(long)TimerCounter].DBAccessed = true;
                if (SnapShot.TimerCallbackPool[(long)TimerCounter].UIAccessed)  //如果这一帧数据已经被使用完了，就将其移除出快照池
                    SnapShot.TimerCallbackPool.Remove((long)TimerCounter);
            }
        }

        private static long TimerCounter = 0;
        private static int EnteringTimerCallback = 0;

        void t_Elapsed(object sender, EventArgs e)
        {
            if (0 == Interlocked.Exchange(ref EnteringTimerCallback, 1))    //0 indicates that the method was not in use, then set to 1 to indicates it is in use now. 
            {
                #region IO work
                if (ThreadMonitor.IOThreadList.Count >= 10)
                    ThreadMonitor.IOAbandonCounter++;
                else
                {
                    IOThread = new Thread(IO_Callback);
                    IOThread.Name = "Timer " + TimerCounter.ToString() + "IO Thread";
                    //IOThread.IsBackground = true;
                    IOThread.Start(TimerCounter);
                    ThreadMonitor.IO_Add(IOThread);
                }
                #endregion
                //ToDo:数据快照
                #region Snapshot
                DateTime now = DateTime.Now;
                Thread t = Thread.CurrentThread;
                t.Name = "Timer Thread " + TimerCounter.ToString();

                string colname;
                Dictionary<string, string> records = new Dictionary<string, string>();
                foreach (Parameter param in dynamicdatalist.parameterlist)
                {
                    colname = GetHashTableValueByKey("LogName", param.sfllist[sflname].nodetable);
                    records[colname] = param.phydata.ToString();
                }
                records.Add("Time", now.ToString("yyyy/MM/dd HH:mm:ss-fff"));
                SnapShot.TimerCallbackPool.Add(TimerCounter, new DataFrame(dynamicdatalist, records));
                #endregion

                #region DB log
                if (ThreadMonitor.DBThreadList.Count >= 100)
                    ThreadMonitor.DBAbandonCounter++;
                else
                {
                    DBThread = new Thread(DB_Callback);
                    DBThread.Name = "Timer " + TimerCounter.ToString() + "DB Thread";
                    //DBThread.IsBackground = true;
                    DBThread.Start(TimerCounter);
                    ThreadMonitor.DB_Add(DBThread);
                }
                #endregion

                #region UI log
                if (ThreadMonitor.UIThreadList.Count >= 1)
                    ThreadMonitor.UIAbandonCounter++;
                else
                {
                    UIThread = new Thread(UI_Callback);
                    UIThread.Name = "Timer " + TimerCounter.ToString() + "UI Thread";
                    //UIThread.IsBackground = true;
                    UIThread.Start(TimerCounter);
                    ThreadMonitor.UI_Add(UIThread);
                }
                #endregion

                TimerCounter++;
                Interlocked.Exchange(ref EnteringTimerCallback, 0); //Set to 0 indicates that the method is not in use
            }
            else
                ThreadMonitor.TimerSkipCounter++;
        }

        private void switchGraph_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton btn = sender as ToggleButton;
            if ((bool)btn.IsChecked)
            {
                loguidatagrid.Visibility = Visibility.Collapsed;
                d3border.Visibility = Visibility.Visible;
                //TimeSelector.Visibility = Visibility.Visible;
                btn.Content = "List";
            }
            else
            {
                loguidatagrid.Visibility = Visibility.Visible;
                d3border.Visibility = Visibility.Collapsed;
                //TimeSelector.Visibility = Visibility.Collapsed;
                btn.Content = "Curve";
                //loglist.SelectedItems.Count;
                //DataGrid

                bool b = false;
                foreach (KV kv in logUIdata.isDisplay)
                {
                    if (kv.pValue)
                    {
                        b = true;
                        break;
                    }
                }
                if (b)
                    loguidatagrid.Visibility = System.Windows.Visibility.Visible;
                else
                    loguidatagrid.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            foreach (DataGridColumn col in loguidatagrid.Columns)
                if (col.Header.ToString() == cb.Content.ToString())
                    col.Visibility = Visibility.Visible;
            foreach (LineGraph LAM in lAm)
            {
                string str = LAM.Description.Brief;
                if (str == cb.Content.ToString())
                {
                    LAM.Visibility = Visibility.Visible;
                    //LAM.MarkerGraph.Visibility = Visibility.Visible;
                }
            }
            if (!(bool)switchGraph.IsChecked)
                loguidatagrid.Visibility = System.Windows.Visibility.Visible;
            Vplotter.FitToView();
            Tplotter.FitToView();
            Cplotter.FitToView();
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            foreach (DataGridColumn col in loguidatagrid.Columns)
                if (col.Header.ToString() == cb.Content.ToString())
                    col.Visibility = Visibility.Collapsed;
            foreach (LineGraph LAM in lAm)
            {
                string str = LAM.Description.Brief;
                if (str == cb.Content.ToString())
                {
                    LAM.Visibility = Visibility.Collapsed;
                    //LAM.MarkerGraph.Visibility = Visibility.Collapsed;
                }
            }
            if (!(bool)switchGraph.IsChecked)
            {
                bool b = false;
                foreach (KV kv in logUIdata.isDisplay)
                {
                    if (kv.pValue)
                    {
                        b = true;
                        break;
                    }
                }
                if (b)
                    loguidatagrid.Visibility = System.Windows.Visibility.Visible;
                else
                    loguidatagrid.Visibility = System.Windows.Visibility.Hidden;
            }

            Vplotter.FitToView();
            Tplotter.FitToView();
            Cplotter.FitToView();
        }

        private void AllNone_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton tb = sender as ToggleButton;
            foreach (KV kv in logUIdata.isDisplay)
            {
                kv.pValue = (bool)tb.IsChecked;
            }
            if (!(bool)switchGraph.IsChecked)
            {
                if ((bool)tb.IsChecked)
                    loguidatagrid.Visibility = System.Windows.Visibility.Visible;
                else
                    loguidatagrid.Visibility = System.Windows.Visibility.Hidden;
            }
        }
        private void ViewFolderBtn_Click(object sender, RoutedEventArgs e)
        {
        }

        private void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            LogData ld = (LogData)loglist.SelectedItem;
            try
            {
                parent.db_Manager.DeleteOneSession(sflname, ld.Timestamp);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message + ex.InnerException.Message);
            }
            logdatalist.Remove(ld);
        }

        private void ExportBtn_Click(object sender, RoutedEventArgs e)
        {
            string fullpath = "";
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            LogData ld = (LogData)loglist.SelectedItem;
            //records.Add("Time", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss-fff"));
            string tmp = ld.Timestamp;
            char[] skip = { ' ', '/', ':' };
            foreach (var s in skip)
            {
                tmp = tmp.Replace(s, '_');
            }
            tmp = ld.DeviceNum + "_Scan_" + tmp;//Issue1428 Leon
            //tmp.Remove(tmp.Length - 4);
            saveFileDialog.FileName = tmp;
            saveFileDialog.FilterIndex = 1;
            saveFileDialog.RestoreDirectory = true;
            saveFileDialog.Title = "Export DB data to csv file";
            saveFileDialog.Filter = "CSV file (*.csv)|*.csv||";
            saveFileDialog.DefaultExt = "csv";
            saveFileDialog.InitialDirectory = FolderMap.m_logs_folder;
            if (saveFileDialog.ShowDialog() == true)
            {
                DataTable dt = new DataTable();
                //DBManager2.GetLog(sflname, ld.Timestamp, ref dt);
                try
                {
                    parent.db_Manager.GetOneSession(sflname, ld.Timestamp, ref dt);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(ex.Message + ex.InnerException.Message);
                }
                fullpath = saveFileDialog.FileName;
                ExportDB(fullpath, dt);
            }
        }

        public bool ExportDB(string fullpath, DataTable dt) //Save buffer content to hard disk as temperary file, then clear buffer
        {
            FileStream file = new FileStream(fullpath, FileMode.Create);
            StreamWriter sw = new StreamWriter(file);
            int length;
            string str = "";
            foreach (DataColumn col in dt.Columns)
            {
                str += col.ColumnName + ",";
            }
            length = str.Length;
            str = str.Remove(length - 1);
            sw.WriteLine(str);

            foreach (DataRow row in dt.Rows)
            {
                str = "";
                foreach (DataColumn col in dt.Columns)
                {
                    str += row[col.ColumnName] + ",";
                }
                length = str.Length;
                str = str.Remove(length - 1);
                sw.WriteLine(str);
            }
            sw.Close();
            file.Close();
            //dt.Clear();
            //FileInfo fi = new FileInfo(parent.folder + logname);
            return true;
        }

        private void LoadBtn_Click(object sender, RoutedEventArgs e)    //先解绑，再跟UI绑定，提升速度
        {
#if SupportCobraLog
            Cursor = Cursors.Wait;
            RebuildUISourceFromList();
            loguidatagrid.DataContext = null;
            ClearCurve();

            List<LogParam> paramlist = new List<LogParam>();
            foreach (Parameter param in dynamicdatalist.parameterlist)
            {
                LogParam logp = new LogParam();
                logp.name = GetHashTableValueByKey("LogName", param.sfllist[sflname].nodetable);
                logp.group = GetHashTableValueByKey("Group", param.sfllist[sflname].nodetable);
                paramlist.Add(logp);
            }
            if (loglist.SelectedIndex == -1)
                return;
            string filename = scanlog.folder + scanlog.logdatalist[loglist.SelectedIndex].logname;
            logUIdata.LoadFromFile(filename, paramlist);


            loguidatagrid.DataContext = logUIdata.logbuf;
            BuildCurve(); 
            Cursor = Cursors.Arrow;
#else
#endif
        }

        void gm_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            parent.gm = (GeneralMessage)sender;
        }

        void msg_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            TASKMessage msg = sender as TASKMessage;
            switch (e.PropertyName)
            {
                case "controlreq":
                    switch (msg.controlreq)
                    {
                        case COMMON_CONTROL.COMMON_CONTROL_WARNING:
                            {
                                CallWarningControl(msg.gm);
                                /*t.Stop();
                                runBtn.IsChecked = false;
                                runBtn.Content = "Run";*/

                                break;
                            }
                    }
                    break;
            }
        }

        private void loglist_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
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

        private void FloatPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            orig_point = Mouse.GetPosition(canvas);
            FloatPanel.Style = null;
        }

        private void canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (FloatPanel.Style == null)
                if (Mouse.LeftButton == MouseButtonState.Pressed)
                {
                    Point point = Mouse.GetPosition(canvas);
                    Vector offset = orig_point - point;
                    //if (offset.Length != 0)
                    {
                        double bottom = Canvas.GetBottom(FloatPanel);
                        double right = Canvas.GetRight(FloatPanel);
                        Canvas.SetBottom(FloatPanel, bottom + offset.Y);
                        Canvas.SetRight(FloatPanel, right + offset.X);
                        orig_point = point;
                    }
                }
        }

        private void canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (FloatPanel.Style == null)
            {
                Style autohidding = (Style)UserControl.Resources["AutoHidding"];
                FloatPanel.Style = autohidding;
            }
        }

        private void canvas_MouseLeave(object sender, MouseEventArgs e)
        {
            if (FloatPanel.Style == null)
            {
                Style autohidding = (Style)UserControl.Resources["AutoHidding"];
                FloatPanel.Style = autohidding;
                Canvas.SetBottom(FloatPanel, 35);
                Canvas.SetRight(FloatPanel, 20);
            }
        }

        private void ConfigBtn_Click(object sender, RoutedEventArgs e)
        {
            ConfigWindow configwindow = new ConfigWindow(this);
            configwindow.ShowDialog();
        }

        #endregion



        #endregion

    }
    public class DataFrame
    {
        public ParamContainer RowData;
        public Dictionary<string, string> LogRow;
        public bool DBAccessed;
        public bool UIAccessed;
        public DataFrame(ParamContainer pc, Dictionary<string, string> record)
        {
            RowData = pc;
            LogRow = record;
            DBAccessed = false;
            UIAccessed = true;
        }
    }
    public static class SnapShot
    {
        public static Dictionary<long, DataFrame> TimerCallbackPool = new Dictionary<long, DataFrame>();
    }
    public class LogData : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private string m_DeviceNum;//Issue1428 Leon
        public string DeviceNum
        {
            get { return m_DeviceNum; }
            set
            {
                m_DeviceNum = value;
                OnPropertyChanged("DeviceNum");
            }
        }

        private string m_Timestamp;
        public string Timestamp
        {
            get { return m_Timestamp; }
            set
            {
                m_Timestamp = value;
                OnPropertyChanged("Timestamp");
            }
        }

        private long m_RecordNumber;
        public long RecordNumber
        {
            get { return m_RecordNumber; }
            set
            {
                m_RecordNumber = value;
                OnPropertyChanged("RecordNumber");
            }
        }
    }
}