using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections.ObjectModel;

namespace O2Micro.Cobra.SBS2Panel
{
    /// <summary>
    /// Interaction logic for ChargerControl.xaml
    /// </summary>
    public partial class StatusControl : UserControl
    {
        private MainControl m_parent;
        public MainControl parent
        {
            get { return m_parent; }
            set { m_parent = value; }
        }

        public StatusControl()
        {
            InitializeComponent();
        }

        public void SetDataSource(object pParent,ObservableCollection<SFLModel> parameterlist)
        {
            parent = (MainControl)pParent;
            SFLModel model = null;
            for (int i = 0; i < parameterlist.Count; i++)
            {
                model = parameterlist[i];
                if (model == null) continue;

                switch (model.mode)
                {
                    case 0:
                        {
                            StatusTempControl control = new StatusTempControl();
                            control.DataContext = model;
                            if (model.bClickable)
                                control.btn.Click += new RoutedEventHandler(parent.WriteOneBtn_Click);
                            
                            flagGrid.Children.Add(control);
                            break;
                        }
                    case 1:
                        {
                            GroupBox gb = new GroupBox();
                            gb.Header = model.nickname;
                            Thickness thick = new Thickness(20,2,20,2); 
                            gb.Margin = thick;
                            checkGrid.Children.Add(gb);
                            UniformGrid gg = new UniformGrid();
                            gg.Columns = 4;
                            gb.Content = gg;
                            for (int n = 0; n < model.parent.itemlist.Count; n++)
                            {
                                RadioButton cb = new RadioButton();
                                cb.Uid = model.guid.ToString();
                                cb.SetBinding(RadioButton.IsCheckedProperty, new Binding("data")   
                                {   
                                    Source = model,   
                                    Mode = BindingMode.TwoWay,
                                    Converter = new RadioBoolToIntConverter(),
                                    ConverterParameter = n,
                                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                                });  

                                cb.Click += new RoutedEventHandler(parent.WriteOneBtn_Click);
                                cb.GroupName = model.nickname;
                                cb.Content = model.parent.itemlist[n];
                                gg.Children.Add(cb);
                            }
                            break;
                        }
                    case 2:
                        ComboxTempControl cb_control = new ComboxTempControl();
                        cb_control.DataContext = model;
                        cbGrid.Children.Add(cb_control);
                        break;
                }
            }
        }

        public void update()
        {
        }
    }
}
