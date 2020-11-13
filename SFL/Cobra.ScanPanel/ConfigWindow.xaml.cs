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
using System.Xml;
using Cobra.Common;
using System.ComponentModel;

namespace Cobra.ScanPanel
{
    /// <summary>
    /// Interaction logic for ConfigWindow.xaml
    /// </summary>
    public partial class ConfigWindow : Window
    {
        private Dictionary<string, string> subTask_Dic = new Dictionary<string, string>();
        //父对象保存
        private MainControl m_parent;
        public MainControl parent
        {
            get { return m_parent; }
            set { m_parent = value; }
        }
        public ConfigWindow()
        {
            InitializeComponent();
        }

        public ConfigWindow(object pParent)
        {
            this.InitializeComponent();

            // 在此点之下插入创建对象所需的代码。
            parent = (MainControl)pParent;
            mDataGrid.ItemsSource = parent.options;
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                subTask_Dic.Clear();
                foreach (setModel mod in parent.options)
                {
                    if (mod == null) continue;
                    subTask_Dic.Add(mod.nickname, mod.m_Item_dic[mod.itemlist[(UInt16)mod.phydata]]);
                }
                parent.optionsJson = SharedAPI.SerializeDictionaryToJsonString(subTask_Dic);
                parent.optionsDictionary = subTask_Dic;
                Hide();
                Close();
            }
            catch (System.Exception ex)
            {
                Hide();
                Close();
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            Close();
        }
    }

    public class setModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private string m_Nickname;
        public string nickname
        {
            get { return m_Nickname; }
            set
            {
                m_Nickname = value;
            }
        }

        private string m_Catalog;
        public string catalog
        {
            get { return m_Catalog; }
            set { m_Catalog = value; }
        }

        private UInt16 m_Editortype;
        public UInt16 editortype
        {
            get { return m_Editortype; }
            set { m_Editortype = value; }
        }

        private double m_PhyData;
        public double phydata
        {
            get { return m_PhyData; }
            set
            {
                //if (m_PhyData != value)
                {
                    m_PhyData = value;
                    OnPropertyChanged("phydata");
                }
            }
        }

        private AsyncObservableCollection<string> m_ItemList = new AsyncObservableCollection<string>();
        public AsyncObservableCollection<string> itemlist
        {
            get { return m_ItemList; }
            set
            {
                m_ItemList = value;
                OnPropertyChanged("itemlist");
            }
        }

        public Dictionary<string, string> m_Item_dic = new Dictionary<string, string>();
        public Dictionary<string, string> item_dic
        {
            get { return m_Item_dic; }
            set
            {
                m_Item_dic = value;
                OnPropertyChanged("item_dic");
            }
        }

        /// <summary>
        /// 参数初始化
        /// </summary>
        /// <param name="node"></param>
        public setModel(XmlNode node)
        {
            string tmp = string.Empty;
            m_Nickname = node.Attributes["Name"].Value;
            foreach (XmlNode snode in node.ChildNodes)
            {
                switch (snode.Name)
                {
                    case "DefValue":
                        phydata = Convert.ToDouble(snode.InnerText.Trim());
                        break;
                    case "EditorType":
                        editortype = Convert.ToUInt16(snode.InnerText.Trim());
                        break;
                    case "Catalog":
                        catalog = snode.InnerText.Trim();
                        break;
                    case "ItemList":
                        foreach (XmlNode ssnode in snode.ChildNodes)
                        {
                            m_Item_dic.Add(ssnode.InnerText.Trim(), ssnode.Attributes["Value"].Value.Trim());
                            itemlist.Add(ssnode.InnerText.Trim());
                        }
                        break;
                }
            }
        }
    }

    enum editortype
    {
        TextBox_EditType = 0,
        ComboBox_EditType = 1,
        CheckBox_EditType = 2
    }

    public class SetDataDataTypeTemplateSelector : DataTemplateSelector
    {
        public DataTemplate TextBoxTemplate { get; set; }
        public DataTemplate ComboBoxTemplate { get; set; }
        public DataTemplate CheckBoxTemplate { get; set; }
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            UInt16 controlType = (UInt16)editortype.TextBox_EditType;
            if (item != null)
            {
                setModel param = item as setModel;
                controlType = param.editortype;

                switch (controlType)
                {
                    case (UInt16)editortype.TextBox_EditType:
                        return TextBoxTemplate;
                    case (UInt16)editortype.ComboBox_EditType:
                        return ComboBoxTemplate;
                    case (UInt16)editortype.CheckBox_EditType:
                        return CheckBoxTemplate;
                    default:
                        return TextBoxTemplate;
                }
            }
            return TextBoxTemplate;
        }
    }
}
