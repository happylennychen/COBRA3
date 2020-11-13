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
using System.Windows.Shapes;
using Cobra.Common;

namespace Cobra.ControlLibrary
{
    /// <summary>
    /// Interaction logic for SelectWindow.xaml
    /// </summary>
    public partial class SelectWindow : Window
    {
        private ControlMessage m_Warningmessage = new ControlMessage();
        public ControlMessage warningmessage
        {
            get { return m_Warningmessage; }
            set { m_Warningmessage = value; }
        }

        public bool m_result = false;
        public SelectWindow()
        {
            InitializeComponent();
            LayoutRoot.DataContext = warningmessage;
        }

        public void ShowDialog(GeneralMessage message)
        {
            Visibility = Visibility.Visible;
            m_Warningmessage.message = message.message;
            ShowDialog();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            m_result = false;
            Close();
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            m_result = true;
            Close();
        }
    }
}
