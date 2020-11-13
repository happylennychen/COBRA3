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
using System.Collections;
using System.ComponentModel;
using System.Windows.Controls.Primitives;
using System.Threading;
using System.IO;
using System.Xml;
using Cobra.EM;
using Cobra.Common;
using Cobra.ControlLibrary;

namespace Cobra.TrimPanel
{
    /// <summary>
    /// Interaction logic for MainControl.xaml
    /// </summary>
    public partial class MainControl
    {
        #region variable defination
        //父对象保存
        private Device m_parent;
        public Device parent
        {
            get { return m_parent; }
            set { m_parent = value; }
        }

        private string m_SFLname;
        public string sflname
        {
            get { return m_SFLname; }
            set { m_SFLname = value; }
        }

        private TASKMessage m_Msg = new TASKMessage();
        public TASKMessage msg
        {
            get { return m_Msg; }
            set { m_Msg = value; }
        }

        private SFLViewMode m_viewmode;
        public SFLViewMode viewmode
        {
            get { return m_viewmode; }
            set { m_viewmode = value; }
        }

        private GeneralMessage gm = new GeneralMessage("Trim SFL", "", 0);
        string fullpath = "";
        private UIConfig m_UI_Config = new UIConfig();
        public UIConfig ui_config
        {
            get { return m_UI_Config; }
            set { m_UI_Config = value; }
        }
        #endregion

        public MainControl(object pParent, string name)
        {
            InitializeComponent();

            #region 相关初始化
            parent = (Device)pParent;
            if (parent == null) return;

            sflname = name;
            if (String.IsNullOrEmpty(sflname)) return;

            InitalUI();
            viewmode = new SFLViewMode(pParent, this);
            //viewmode.sfl_parameterlist.OrderBy(mod => mod.order);
            WarningPopControl.SetParent(LayoutRoot);
            #endregion
        }

        public void Simulation()
        {
        }

        public void InitalUI()
        {
            bool bdata = false;
            string name = String.Empty;
            XmlNodeList nodelist = parent.GetUINodeList(sflname);
            if (nodelist == null) return;

            foreach (XmlNode node in nodelist)
            {
                if (node.Attributes["Name"] == null) continue;
                name = node.Attributes["Name"].Value.ToString();
                switch (name)
                {
                    case "layout":
                        {
                            foreach (XmlNode sub in node)
                            {
                                if (sub.Attributes["Name"] == null) continue;
                                if (sub.Attributes["IsEnable"] == null) continue;
                                if (sub.Attributes["SubTask"] == null) continue;
                                btnControl btCtrl = new btnControl();
                                btCtrl.btn_name = sub.Attributes["Name"].Value.ToString();
                                if (Boolean.TryParse(sub.Attributes["IsEnable"].Value.ToString(), out bdata))
                                    btCtrl.benable = bdata;
                                else
                                    btCtrl.benable = true;

                                btCtrl.subTask = Convert.ToUInt16(sub.Attributes["SubTask"].Value.ToString(), 16);

                                System.Windows.Controls.Button btn = WorkPanel.FindName(btCtrl.btn_name) as System.Windows.Controls.Button;
                                if (btn != null) btn.DataContext = btCtrl;

                                ui_config.btn_controls.Add(btCtrl);
                            }
                            break;
                        }
                }
            }
        }

        private string GetHashTableValueByKey(string str, Hashtable htable)
        {
            foreach (DictionaryEntry de in htable)
            {
                if (de.Key.ToString().Equals(str))
                    return de.Value.ToString();
            }
            return "NoSuchKey";
        }

        private void TrimBtn_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.Button btn = sender as System.Windows.Controls.Button;
            btnControl btn_ctrl = (btnControl)btn.DataContext;
            if (btn_ctrl == null) return;

            string str = ",";
            try
            {
                FileStream file = new FileStream(fullpath, FileMode.Create);
                StreamWriter sw = new StreamWriter(file);

                TrimBtn.IsEnabled = false;
                for (int i = 0; i < 16; i++)
                    str += String.Format("TrimCode = {0},", i);
                sw.WriteLine(str);

                if (parent.bBusy)
                {
                    gm.level = 1;
                    gm.controls = "Trim button";
                    gm.message = LibErrorCode.GetErrorDescription(LibErrorCode.IDS_ERR_EM_THREAD_BKWORKER_BUSY);
                    gm.bupdate = true;
                    CallWarningControl(gm);
                    sw.Close();
                    file.Close();
                    TrimBtn.IsEnabled = true;
                    return;
                }
                else
                    parent.bBusy = true;
                msg.task = TM.TM_SPEICAL_GETDEVICEINFOR;
                parent.AccessDevice(ref m_Msg);
                while (msg.bgworker.IsBusy)
                    System.Windows.Forms.Application.DoEvents();
                if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                {
                    gm.level = 2;
                    gm.message = LibErrorCode.GetErrorDescription(msg.errorcode);
                    CallWarningControl(gm);
                    parent.bBusy = false;
                    sw.Close();
                    file.Close();
                    TrimBtn.IsEnabled = true;
                    return;
                }
                msg.task = TM.TM_SPEICAL_GETREGISTEINFOR;
                parent.AccessDevice(ref m_Msg);
                while (msg.bgworker.IsBusy)
                    System.Windows.Forms.Application.DoEvents();
                if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                {
                    gm.level = 2;
                    gm.message = LibErrorCode.GetErrorDescription(msg.errorcode);
                    CallWarningControl(gm);
                    parent.bBusy = false;
                    sw.Close();
                    file.Close();
                    TrimBtn.IsEnabled = true;
                    return;
                }
                msg.task = TM.TM_COMMAND;
                msg.gm.sflname = sflname;
                msg.sub_task = btn_ctrl.subTask;
                msg.task_parameterlist = viewmode.dm_parameterlist;
                parent.AccessDevice(ref m_Msg);
                while (msg.bgworker.IsBusy)
                    System.Windows.Forms.Application.DoEvents();
                if (msg.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                {
                    gm.level = 2;
                    gm.message = LibErrorCode.GetErrorDescription(msg.errorcode);
                    CallWarningControl(gm);
                    parent.bBusy = false;
                    sw.Close();
                    file.Close();
                    TrimBtn.IsEnabled = true;
                    return;
                }

                parent.bBusy = false;
                Cursor = Cursors.Wait;
                for (ushort i = 0; i < viewmode.sfl_parameterlist.Count; i++)
                {
                    string name = GetHashTableValueByKey("Name", viewmode.sfl_parameterlist[i].parent.sfllist[sflname].nodetable);
                    if (name == "NoSuchKey")
                        name = "channel " + (i + 1).ToString();
                    str = name + ",";
                    str += viewmode.sfl_parameterlist[i].parent.sphydata;
                    sw.WriteLine(str);
                }
                str = "";
                Cursor = Cursors.Arrow;

                sw.Close();
                file.Close();
                TrimBtn.IsEnabled = true;
            }
            catch (SystemException exc)
            {
                MessageBox.Show(exc.Message);
                return;
            }
        }

        #region DM提供的API
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

        public uint ConvertPhysicalToHex(ParamContainer pc)
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_CONVERT_PHYSICALTOHEX;
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

        #region 运算需要的函数
        private double Read(SFLModel param)
        {
            double sum = 0;
            List<double> values = new List<double>();
            ParamContainer pc = new ParamContainer();
            pc.parameterlist.Add(param.parent);

            for (int i = 0; i < param.retry_time; i++)
            {
                Read(pc);
                ConvertHexToPhysical(pc);

                values.Add(param.parent.phydata);
                sum += param.parent.phydata;

                Thread.Sleep(100);
            }
            sum /= param.retry_time;
            int minIndex = 0;
            double err = 999;
            for (int i = 0; i < param.retry_time; i++)
            {
                if (err > Math.Abs(values[i] - sum))
                {
                    err = Math.Abs(values[i] - sum);
                    minIndex = i;
                }
            }
            pc.parameterlist.Clear();
            return values[minIndex];

        }

        private void Write(Parameter param, ushort value)
        {
            ParamContainer pc = new ParamContainer();
            pc.parameterlist.Add(param);
            param.phydata = value;
            ConvertPhysicalToHex(pc);
            Write(pc);
            pc.parameterlist.Clear();
        }
        #endregion

        #region 通用控件消息响应
        public void CallWarningControl(GeneralMessage message)
        {
            WarningPopControl.Dispatcher.Invoke(new Action(() =>
            {
                WarningPopControl.ShowDialog(message);
            }));
        }
        #endregion

        private void Save_Button_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            saveFileDialog.Title = "Save ADC File";
            saveFileDialog.Filter = "Trim files (*.csv)|*.csv||";
            saveFileDialog.FileName = "default";
            saveFileDialog.FilterIndex = 1;
            saveFileDialog.RestoreDirectory = true;
            saveFileDialog.DefaultExt = "csv";
            saveFileDialog.InitialDirectory = FolderMap.m_currentproj_folder;
            if (saveFileDialog.ShowDialog() == true)
            {
                if (parent == null) return;
                else
                {
                    fullpath = saveFileDialog.FileName;
                    FileName.Content = fullpath;
                }
            }

        }
    }
}
