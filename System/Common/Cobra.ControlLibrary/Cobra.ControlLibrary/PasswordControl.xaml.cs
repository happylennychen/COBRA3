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
using System.Threading;
using System.Windows.Threading;
using System.Globalization;

namespace Cobra.ControlLibrary
{
	/// <summary>
	/// PasswordControl.xaml 的交互逻辑
	/// </summary>
	public partial class PasswordControl : UserControl
	{
        private UInt16 m_Password;
        public UInt16 password
        {
            get { return m_Password; }
            set { m_Password = value; }
        }

        private bool m_hideRequest = false;
        private bool m_result = false;

        private UIElement m_parent;
        public void SetParent(UIElement parent)
        {
            m_parent = parent;
        }

		public PasswordControl()
		{
            this.InitializeComponent();
            Visibility = Visibility.Hidden;
		}

        public bool ShowDialog()
        {
            Visibility = Visibility.Visible;
            PasswordBox.Password = String.Empty;
            PasswordBox.Focus();

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
            return m_result;
        }

        private void HideDialog()
        {
            m_hideRequest = true;
            Visibility = Visibility.Hidden;
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            m_result = true;

            if (!UInt16.TryParse(PasswordBox.Password, NumberStyles.HexNumber,CultureInfo.InvariantCulture,out m_Password))
                m_Password = 0;
            HideDialog();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            m_result = false;
            HideDialog();
        }

	}
}