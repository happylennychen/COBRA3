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
using System.Windows.Shapes;
using System.ComponentModel;
using System.Collections.ObjectModel;
using O2Micro.Cobra.Common;
using O2Micro.Cobra.EM;
using System.Xml;

namespace O2Micro.Cobra.Shell
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
        public string BoardConfigsflname; 
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
        private O2Micro.Cobra.DeviceConfigurationPanel.MainControl BoardConfigSFL;
        private bool hasBoardConfigSFL()
        {
            foreach (var btn in EMExtensionManage.m_EM_DevicesManage.btnPanelList)
            {
                if (btn.btnlabel == "BoardConfig" || btn.btnlabel == "Board Config")
                {
                    BoardConfigsflname = btn.btnlabel;
                    return true;
                }
            }
            return false;
        }

        private int GetBoardConfigIndex()
        {
            foreach (var btn in EMExtensionManage.m_EM_DevicesManage.btnPanelList)
            {
                if (btn.btnlabel == "BoardConfig" || btn.btnlabel == "Board Config")
                {
                    return btn.id;
                }
            }
            return -1;
        }
        private void SwitchToBoardConfig()
        {
            int index = GetBoardConfigIndex();
            if (index > 0)
                parent.FeatureBtnList.SelectedIndex = index;
        }
        private string GetPreviousSettingsFilePath()
        {
            string settingfilepath = System.IO.Path.Combine(FolderMap.m_currentproj_folder, "settings.xml");
            XmlDocument doc = new XmlDocument();
            doc.Load(settingfilepath);
            XmlElement root = doc.DocumentElement;
            if (root.FirstChild != null && root.FirstChild.Name == "BoardConfigFileName")
                return root.FirstChild.InnerText;
            return "";
        }
        private void LoadPreviousSettings()
        {
            try
            {
                string filepath = GetPreviousSettingsFilePath();
                if (filepath == "")
                {
                    BoardConfigSFL.LoadBoardConfigFromInternalMemory();
                    return;
                }
                BoardConfigSFL.BoardConfigAutoLoadFile(filepath);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message + "The default values will be used in Board Settings.");
                //set Settings.xml content to ""
                //Load default
                BoardConfigSFL.LoadBoardConfigFromInternalMemory();
                BoardConfigSFL.SaveBoardConfigFilePath("");
                return;
            }
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

            if (hasBoardConfigSFL())    //Issue1374 Leon
            {
                MessageBox.Show("Please check board settings first.");
                SwitchToBoardConfig();

                List<WorkPanelItem> tabs = EMExtensionManage.m_EM_DevicesManage.GetWorkPanelTabItemsByBtnName(BoardConfigsflname);
                for (int i = 0; i < tabs.Count; i++)
                {
                    WorkPanelItem wpi = tabs[i];
                    BoardConfigSFL = (O2Micro.Cobra.DeviceConfigurationPanel.MainControl)wpi.item;
                }

                LoadPreviousSettings();
            }
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
                op.sphydata = (op.bcheck == true)?"1":"0";
        }

	}
}