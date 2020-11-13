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
using Cobra.Common;

namespace Cobra.Shell
{
	/// <summary>
	/// AboutWindow.xaml 的交互逻辑
	/// </summary>
	public partial class AboutWindow : Window
	{
		public AboutWindow()
		{
			this.InitializeComponent();
			
			// 在此点之下插入创建对象所需的代码。
		}
		
		//父对象保存
        private MainWindow m_parent;
        public MainWindow parent
        {
            get { return m_parent; }
            set { m_parent = value; }
        }

		public AboutWindow(object pParent)
		{
			this.InitializeComponent();

			// 在此点之下插入创建对象所需的代码。
            parent = (MainWindow)pParent;
            VersionList.ItemsSource = LibInfor.m_assembly_list;
		}
		
		private void CancelBtn_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			// 在此处添加事件处理程序实现。
            Button o = (Button)sender;
            parent.gm.controls = o.Name;
            parent.gm.message = "Quit About Window";

			Hide();
            Close();
		}

        private void TextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            EULAWindow eula = new EULAWindow();
            eula.Show();
        }
	}
}