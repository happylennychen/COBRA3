using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Collections.ObjectModel;
using Cobra.Common;
using Cobra.EM;
using System.IO;

namespace Cobra.Shell
{
    enum editortype
    {
        TextBox_EditType = 0,
        ComboBox_EditType = 1,
        CheckBox_EditType = 2
    }
    /// <summary>
    /// Window1.xaml 的交互逻辑
    /// </summary>
    public partial class BusOptionsWindow : Window
    {
        //父对象保存
        private MainWindow m_parent;
        public MainWindow parent
        {
            get { return m_parent; }
            set { m_parent = value; }
        }

        public BusOptionsWindow(object pParent)
        {
            this.InitializeComponent();

            // 在此点之下插入创建对象所需的代码。
            parent = (MainWindow)pParent;
            //parent.m_EM_Lib.EnumerateInterface();
        }

        private static ObservableCollection<ListCollectionView> m_busoptionslistview = new ObservableCollection<ListCollectionView>();
        public static ObservableCollection<ListCollectionView> busoptionslistview
        {
            get { return m_busoptionslistview; }
            set { busoptionslistview = m_busoptionslistview; }
        }

        private void Load(object sender, System.Windows.RoutedEventArgs e)
        {
            WorkSpace.ItemsSource = Registry.busoptionslist_collectionview;
        }

        private void CancelBtn_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // 在此处添加事件处理程序实现。
            Button o = (Button)sender;
            parent.gm.controls = o.Name;
            parent.gm.message = "Quit Bus Options Window";

            //Registry.RestoreDeviceConnectSetting();
            Hide();
            Close();
        }
        private void SaveAndTestBtn_Click(object sender, RoutedEventArgs e)
        {
            Button o = (Button)sender;
            parent.gm.controls = o.Name;
            parent.gm.message = "Save Bus Adjustion And Quit From Bus Options Window";
            parent.gm.bupdate = true;

            if (!Registry.CheckDeviceConnectSetting())
            {
                parent.gm.message = "Some ports are configured identically, please check!";
                parent.gm.level = 1;
                parent.gm.bupdate = true;
                CallWarningControl(parent.gm);
                return;
            }

            //Registry.SaveDeviceConnectSetting();
            parent.m_EM_Lib.CreateInterface();
            parent.m_EM_Lib.GetDevicesInfor();

            Hide();
            Close();

            if (isSFLExist(COBRA_GLOBAL.Constant.NewBoardConfigName))    //Issue1374 Leon
            {
                if (NeedPromptWarning())
                {
                    MessageBox.Show("Please check board settings first.");
                }
                SwitchToSFL(COBRA_GLOBAL.Constant.NewBoardConfigName);
            }

            string filepath;
            var ret = Registry.GetConfigFilePath(out filepath);
            if (filepath == "" || ret == false)
            {
                return;
            }
            else
            {
                if (File.Exists(filepath))
                {
                    if (isSFLExist(COBRA_GLOBAL.Constant.NewEFUSEConfigName) || isSFLExist(COBRA_GLOBAL.Constant.NewRegisterConfigName))
                    {
                        if (MessageBox.Show("Do you want to load previously saved or loaded settings?", "Warning", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        {
                            if (isSFLExist(COBRA_GLOBAL.Constant.NewEFUSEConfigName))
                            {
                                var sfl = GetSFL(COBRA_GLOBAL.Constant.NewEFUSEConfigName);
                                if (sfl != null)
                                    LoadPreviousSettings(sfl, filepath);
                            }

                            if (isSFLExist(COBRA_GLOBAL.Constant.NewRegisterConfigName))
                            {
                                var sfl = GetSFL(COBRA_GLOBAL.Constant.NewRegisterConfigName);
                                if (sfl != null)
                                    LoadPreviousSettings(sfl, filepath);
                            }
                        }
                    }
                }
                else  //文件已经被删除了
                {
                    Registry.DeleteConfigFilePath();
                }
            }
        }

        private bool NeedPromptWarning()
        {
            bool output = true;
            var setting = SharedAPI.GetProjectSettingFromExtension("SaveAndTestPromptWarning");
            if (setting.ToUpper() == "FALSE")
                output = false;
            return output;
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

        private void textBox_LostFocus(object sender, RoutedEventArgs e)
        {
            double tdb = 0;
            TextBox tb = sender as TextBox;
            ContentPresenter tmp = (ContentPresenter)tb.TemplatedParent;
            Options op = (Options)tmp.Content;

            if (!Double.TryParse(tb.Text, out tdb))
            {
                parent.gm.controls = tb.Name;
                parent.gm.message = "Text value can't be parsed,please check.";
                parent.gm.bupdate = true;
                CallWarningControl(parent.gm);
                op.berror = true;
                op.sphydata = string.Format("{0:F0}", op.data);
                return;
            }

            if ((tdb > op.maxvalue) || (tdb < op.minvalue))
            {
                parent.gm.controls = tb.Name;
                parent.gm.message = "Out of the range,please check.";
                parent.gm.bupdate = true;
                CallWarningControl(parent.gm);
                op.berror = true;
                op.sphydata = string.Format("{0:F0}", op.data);
            }
            else
            {
                op.data = tdb;
                op.berror = false;
            }
            return;
        }

        private void ComboBox_DropDownClosed(object sender, EventArgs e)
        {
            // 在此处添加事件处理程序实现。
            ComboBox o = (ComboBox)sender;
            ContentPresenter tmp = (ContentPresenter)o.TemplatedParent;
            Options op = (Options)tmp.Content;
            if (op != null)
                op.sphydata = op.SelectLocation.Info;
        }

        private void checkBox_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox o = (CheckBox)sender;
            ContentPresenter tmp = (ContentPresenter)o.TemplatedParent;
            Options op = (Options)tmp.Content;
            if (op != null)
                op.sphydata = (op.bcheck == true) ? "1" : "0";
        }

        #region Load Previous Setting
        private bool isSFLExist(string sflname)//Issue1374 Leon
        {
            foreach (var btn in EMExtensionManage.m_EM_DevicesManage.btnPanelList)
            {
                if (btn.btnlabel == sflname)
                {
                    return true;
                }
            }
            return false;
        }

        private int GetSFLIndex(string sflname)//Issue1374 Leon
        {
            foreach (var btn in EMExtensionManage.m_EM_DevicesManage.btnPanelList)
            {
                if (btn.btnlabel == sflname)
                {
                    return btn.id;
                }
            }
            return -1;
        }
        private DeviceConfigurationPanel.MainControl GetSFL(string sflname)
        {
            DeviceConfigurationPanel.MainControl output = null;
            List<WorkPanelItem> tabs = EMExtensionManage.m_EM_DevicesManage.GetWorkPanelTabItemsByBtnLabel(sflname);
            for (int i = 0; i < tabs.Count; i++)
            {
                WorkPanelItem wpi = tabs[i];
                output = (DeviceConfigurationPanel.MainControl)wpi.item;
            }
            return output;
        }
        private void SwitchToSFL(string sflname)
        {
            int index = GetSFLIndex(sflname);
            if (index >= 0)
                parent.FeatureBtnList.SelectedIndex = index;
        }
        private void LoadPreviousSettings(DeviceConfigurationPanel.MainControl sfl, string filepath)
        {
            try
            {
                sfl.Preload(filepath);
            }
            catch (Exception e)
            {
                MessageBox.Show("Load Previews Settings failed.");
                return;
            }
        }
        #endregion

    }
}