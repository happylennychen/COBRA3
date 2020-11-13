using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Threading;
using System.Collections.ObjectModel;
using Cobra.Center;

namespace Cobra.Update
{
    /// <summary>
    /// Interaction logic for ServerConnectWindow.xaml
    /// </summary>
    public partial class ServerConnectWindow : Window
    {
        private event Action Connect;
        private BackgroundWorker backgroundWorker = null;
        private MainWindow m_control_parent;
        public MainWindow control_parent
        {
            get { return m_control_parent; }
            set { m_control_parent = value; }
        }

        public ServerConnectWindow(object parent)
        {
            InitializeComponent();
            control_parent = (MainWindow)parent;
            if (control_parent == null) return;
            InitBWork();
        }

        public void InitBWork()
        {
            //Upgrade worker
            backgroundWorker = new BackgroundWorker(); // 实例化后台对象

            backgroundWorker.WorkerReportsProgress = true; // 设置可以通告进度
            backgroundWorker.WorkerSupportsCancellation = true; // 设置可以取消

            backgroundWorker.ProgressChanged += ProgressChanged;
            backgroundWorker.DoWork += new DoWorkEventHandler(DoWork);
            backgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(CompletedWork);

            prgbarConnect.Visibility = Visibility.Collapsed;
            txbProgress.Visibility = Visibility.Collapsed;
            
            Connect +=control_parent.Connect;
        }

        private void SaveAndTestBtn_Click(object sender, RoutedEventArgs e)
        {
            SaveAndTestBtn.IsEnabled = false;
            CancelBtn.IsEnabled = false;
            prgbarConnect.Visibility = Visibility.Visible;
            txbProgress.Visibility = Visibility.Visible;
            backgroundWorker.RunWorkerAsync();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            Close();
        }

        private void DoWork(object sender, DoWorkEventArgs e)
        {
            string erMessage = string.Empty;
            backgroundWorker.ReportProgress(0);
            this.Dispatcher.Invoke((Action)(() =>
            {
                txbProgress.Text = "Initializing...";
            }));
            Thread.Sleep(500);
            backgroundWorker.ReportProgress(30);
            this.Dispatcher.Invoke((Action)(() =>
            {
                txbProgress.Text = "Saving configuration";
            }));
            control_parent.viewmode.syncSettingList();
            Thread.Sleep(100);
            backgroundWorker.ReportProgress(60);
            this.Dispatcher.Invoke((Action)(() =>
            {
                txbProgress.Text = "Trying to Connect server";
            }));
            if (control_parent.viewmode.connectCobraCenter(ref erMessage))
            {
                this.Dispatcher.Invoke((Action)(() =>
                {
                    txbProgress.Visibility = Visibility.Visible;
                    txbProgress.Text = "Successfully connected to server";
                    Connect();
                    Hide();
                    Close();
                }));
            }
            else
            {
                this.Dispatcher.Invoke((Action)(() =>
                {
                    txbProgress.Visibility = Visibility.Visible;
                    txbProgress.Text = erMessage;
                }));
            }
            backgroundWorker.ReportProgress(100);
        }

        private void ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            prgbarConnect.Value = e.ProgressPercentage;
        }

        private void CompletedWork(object sender, RunWorkerCompletedEventArgs e)
        {
            CancelBtn.IsEnabled = true;
            SaveAndTestBtn.IsEnabled = true;
            prgbarConnect.Visibility = Visibility.Collapsed;
        }

    }
}
