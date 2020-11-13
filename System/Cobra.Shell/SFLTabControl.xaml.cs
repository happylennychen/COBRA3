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
using Cobra.EM;
using Cobra.Common;

namespace Cobra.Shell
{
	/// <summary>
	/// TabControl.xaml 的交互逻辑
	/// </summary>
	public partial class SFLTabControl : UserControl
	{
		public SFLTabControl(int index)
		{
			this.InitializeComponent();
            AddTabs(index);
		}

        public void InsertTab(int num,string name)
        {
            var tab = new TabItem { Header = name };
            tab.Name = Registry.GetBusOptionsByName(name).Name;
            tab.Content = EMExtensionManage.m_EM_DevicesManage.GetWorkPanelTabItemsByPanelID(num).Find(delegate(WorkPanelItem node)
            {
                return node.itemname.Equals(name);
            }
            ).item;

            for (int i = 0; i < tabcontrol.Items.Count; i++)
            {
                TabItem item = (TabItem)tabcontrol.Items[i];
                if (Registry.GetBusOptionsByName(item.Name).DeviceIndex > Registry.GetBusOptionsByName(name).DeviceIndex)
                {
                    tabcontrol.Items.Insert(i, tab);
                    return;
                }
            }
            tabcontrol.Items.Add(tab);
        }

        private void AddTabs(int index)
        {
            for (int i = 0; i < Registry.busoptionslistview.Count; i++)
            {
                var tab = new TabItem { Header = Registry.busoptionslistview[i].Name};
                tab.Name = Registry.busoptionslistview[i].Name;
                tab.Content = EMExtensionManage.m_EM_DevicesManage.GetWorkPanelTabItemsByPanelID(index)[i].item;
                tabcontrol.Items.Add(tab);
            }
		}
	}
}