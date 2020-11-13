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
using System.Threading;
using System.Windows.Threading;
using Cobra.Common;

namespace Cobra.ControlLibrary
{
    /// <summary>
    /// Interaction logic for SelectControl.xaml
    /// </summary>
    public partial class SelectControl : UserControl
    {
        private ControlMessage m_Warningmessage = new ControlMessage();
        public ControlMessage warningmessage
        {
            get { return m_Warningmessage; }
            set { m_Warningmessage = value; }
        }

        private bool m_hideRequest = false;
        private bool m_result = false;

        private UIElement m_parent;
        public void SetParent(UIElement parent)
        {
            m_parent = parent;
        }

        public SelectControl()
		{
            this.InitializeComponent();
            Visibility = Visibility.Hidden;
            LayoutRoot.DataContext = warningmessage;
		}

        public bool ShowDialog(GeneralMessage message)
        {
            Visibility = Visibility.Visible;
            m_Warningmessage.message = message.message;

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

            return m_result;
        }

        private void HideDialog()
        {
            m_hideRequest = true;
            Visibility = Visibility.Hidden;
        }
	
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            m_result = false;
            HideDialog();
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        { 
            m_result = true;
            HideDialog();
        }
    }
}
