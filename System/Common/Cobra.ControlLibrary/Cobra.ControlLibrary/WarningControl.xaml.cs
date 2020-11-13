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
using System.ComponentModel;
using Cobra.Common;
using System.Threading;
using System.Windows.Threading;

namespace Cobra.ControlLibrary
{
    public class ControlMessage : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private string m_Message;
        public string message
        {
            get { return m_Message; }
            set 
            { 
                m_Message = value;
                OnPropertyChanged("message");
            }
        }

        private int m_Level;
        public int level
        {
            get { return m_Level; }
            set 
            {
                m_Level = value;
                OnPropertyChanged("level");
            }
        }
    }
    /// <summary>
	/// MainControl.xaml 的交互逻辑
	/// </summary>
    public partial class WarningControl : UserControl
	{
        private ControlMessage m_Warningmessage = new ControlMessage();
        public ControlMessage warningmessage
        {
            get { return m_Warningmessage; }
            set { m_Warningmessage = value;}
        }

        private bool m_hideRequest = false;

        private UIElement m_parent;
        public void SetParent(UIElement parent)
        {
            m_parent = parent;
        }

        public WarningControl()
		{
            this.InitializeComponent();
            Visibility = Visibility.Hidden;

            //WarningTextBlock.DataContext = warningmessage;
			LayoutRoot.DataContext = warningmessage;
		}

        public void ShowDialog(GeneralMessage message)
        {
            Visibility = Visibility.Visible;
            m_Warningmessage.message = message.message;
            m_Warningmessage.level = message.level;

            m_hideRequest = false;
            while (!m_hideRequest)
            {
                // HACK: Stop the thread if the application is about to close
                if (this.Dispatcher.HasShutdownStarted ||this.Dispatcher.HasShutdownFinished)
                {
                    break;
                }
                // HACK: Simulate "DoEvents"
                this.Dispatcher.Invoke(DispatcherPriority.Background,new ThreadStart(delegate { }));
                Thread.Sleep(20);
            }
        }

        public void ShowDialog(string message, int level)	//Add a more genaric method. Leon
        {
            Visibility = Visibility.Visible;
            m_Warningmessage.message = message;
            m_Warningmessage.level = level;

            m_hideRequest = false;
            while (!m_hideRequest)
            {
                // HACK: Stop the thread if the application is about to close
                if (this.Dispatcher.HasShutdownStarted || this.Dispatcher.HasShutdownFinished)
                {
                    break;
                }
                // HACK: Simulate "DoEvents"
                this.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate { }));
                Thread.Sleep(20);
            }
        }

        private void HideDialog()
        {
            m_hideRequest = true;
            Visibility = Visibility.Hidden;
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            HideDialog();
        }
	}
}