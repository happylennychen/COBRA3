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
using Cobra.EM;
using Cobra.Common;
using System.Collections;
using System.ComponentModel;
using System.Windows.Controls.Primitives;
using System.Threading;
using System.IO;
using System.Xml;

namespace Cobra.DBGPanel
{
    /// <summary>
    /// Interaction logic for MainControl.xaml
    /// </summary>
    public partial class MainControl : UserControl
    {
        #region variable defination

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
        private GeneralMessage gm = new GeneralMessage("DBG SFL", "", 0);
        AsyncObservableCollection<Parameter> paramlist = new AsyncObservableCollection<Parameter>();
        System.Windows.Threading.DispatcherTimer t = new System.Windows.Threading.DispatcherTimer();


        public AsyncObservableCollection<SubTask> subtask = new AsyncObservableCollection<SubTask>();


        #endregion

        #region Function defination
        public string GetHashTableValueByKey(string str, Hashtable htable)
        {
            foreach (DictionaryEntry de in htable)
            {
                if (de.Key.ToString().Equals(str))
                    return de.Value.ToString();
            }
            return "NoSuchKey";
        }
        public MainControl(object pParent, string name)
        {
            InitializeComponent();

            #region 相关初始化
            parent = (Device)pParent;
            if (parent == null) return;

            sflname = name;
            if (String.IsNullOrEmpty(sflname)) return;
            #endregion

            #region 初始化SubTask
            string str_option = String.Empty;
            XmlNodeList nodelist = parent.GetUINodeList(sflname);
            foreach (XmlNode node in nodelist)
            {
                str_option = node.Name;
                switch (str_option)
                {
                    case "SubTask":
                        {
                            foreach (XmlNode sub in node)
                            {
                                SubTask st = new SubTask();
                                st.pID = Convert.ToUInt16(sub.InnerText);
                                st.pName = sub.Name;
                                subtask.Add(st);
                            }
                            break;
                        }
                }
            }
            //if (subtask.Count == 0)
                //SubTask.Visibility = Visibility.Collapsed;
            #endregion
            ButtonList.DataContext = subtask;
            paramlist = parent.GetParamLists(sflname).parameterlist;
        }

        #region 通用控件消息响应
        public void CallWarningControl(GeneralMessage message)
        {
            WarningPopControl.Dispatcher.Invoke(new Action(() =>
            {
                WarningPopControl.ShowDialog(message);
            }));
        }        
        public void CallWarningControl(uint errorcode)	//Issue685 leon
        {
            gm.message = LibErrorCode.GetErrorDescription(errorcode);
            gm.level = 2;
            if (errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                WarningPopControl.Dispatcher.Invoke(new Action(() =>
                {
                    WarningPopControl.ShowDialog(gm);
                }));
            }
        }
        #endregion

        #region DM提供的API
        public uint Mapping(ParamContainer pc)
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_BLOCK_MAP;
            msg.task_parameterlist = pc;
            //msg.bupdate = bControl;            //需要从chip读数据
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
            //msg.bupdate = bControl;            //需要从chip读数据
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
            //msg.bupdate = bControl;            //需要从chip读数据
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
            //msg.bupdate = bControl;            //需要从chip读数据
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            return m_Msg.errorcode;
        }
        public uint GetRegInfo()
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_SPEICAL_GETREGISTEINFOR;
            //msg.bupdate = bControl;            //需要从chip读数据
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
            //msg.bupdate = bControl;            //需要从chip读数据
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            return m_Msg.errorcode;
        }
        public uint Command(ushort arg)
        {
            msg.owner = this;
            msg.gm.sflname = sflname;
            msg.task = TM.TM_COMMAND;
            msg.sub_task = arg;
            //msg.sm.misc[0] = arg;
            //msg.bupdate = bControl;            //需要从chip读数据
            uint ret = parent.AccessDevice(ref m_Msg);
            while (msg.bgworker.IsBusy)
                System.Windows.Forms.Application.DoEvents();
            return m_Msg.errorcode;
        }
        #endregion

        #endregion

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            Button btn = sender as Button;
            ushort ID = (ushort)btn.Tag;
            ret = Command(ID);
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                CallWarningControl(ret);	//Issue685 leon
            }
        }


    }


    public class SubTask
    {
        private ushort ID;
        public ushort pID
        {
            get { return ID; }
            set { ID = value; }
        }
        private string Name;
        public string pName
        {
            get { return Name; }
            set { Name = value; }
        }

    }
}
