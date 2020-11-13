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

namespace Cobra.ControlLibrary
{
    /// <summary>
    /// Interaction logic for WaitControl.xaml
    /// </summary>
    public partial class WaitControl : UserControl
    {
        private UIElement m_parent;
        public void SetParent(UIElement parent)
        {
            m_parent = parent;
        }

        public WaitControl()
        {
            InitializeComponent();
            Visibility = Visibility.Hidden;
        }

        public void ShowAdorner()
        {
            Visibility = Visibility.Visible;
        }

        private void HideAdorner()
        {
            Visibility = Visibility.Hidden;
        }

        public bool IsBusy
        {
            get { return (bool)GetValue(IsBusyProperty); }
            set { this.SetValue(IsBusyProperty, value); }
        }

        public static readonly DependencyProperty IsBusyProperty = DependencyProperty.Register("IsBusy111", typeof(Boolean), typeof(WaitControl), new UIPropertyMetadata(false, OnIsBusyPropertyChangedCallback));

        public static void OnIsBusyPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var self = d as WaitControl;
            bool showDecorator = (bool)e.NewValue;

            if (showDecorator)
            {
                self.ShowAdorner();
            }
            else
            {
                self.HideAdorner();
            }
        }

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(WaitControl), new UIPropertyMetadata("", OnTextPropertyChangedCallback));

        public static void OnTextPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var self = d as WaitControl;
            var msg = e.NewValue.ToString();
            self.waitlabel.Content = msg;
        }

        public string Percent
        {
            get { return (string)GetValue(PercentProperty); }
            set { SetValue(PercentProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Percent.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty PercentProperty = DependencyProperty.Register("Percent", typeof(string), typeof(WaitControl), new UIPropertyMetadata("", OnPercentPropertyChangedCallback));

        public static void OnPercentPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var self = d as WaitControl;
            var msg = e.NewValue.ToString();
            self.animationcontrol.Content = msg;
        }
    }
}
