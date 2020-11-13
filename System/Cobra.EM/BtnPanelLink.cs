using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows;
using System.Xml;
using System.ComponentModel;
using System.ComponentModel.Composition.Hosting;
using Cobra.Common;

namespace Cobra.EM
{
    [Serializable]
    public class WorkPanelItem
    {
        private string m_Item_Name;
        public string itemname
        {
            get { return m_Item_Name; }
            set { m_Item_Name = value; }
        }

        private UIElement m_Item;
        public UIElement item
        {
            get { return m_Item; }
            set { m_Item = value; }
        }
    }

    public class BtnPanelItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private bool m_Btn_Lamp;
        public bool btnlamp
        {
            get { return m_Btn_Lamp; }
            set 
            { 
                m_Btn_Lamp = value;
                OnPropertyChanged("btnlamp");
            }
        }

        private int m_Id;
        public int id
        {
            get { return m_Id; }
            set { m_Id = value; }
        }
    }

    public class BtnPanelLink : DependencyObject
    {
        private string m_Btn_Name;
        public string btnname
        {
            get { return m_Btn_Name; }
            set { m_Btn_Name = value; }
        }

        private string m_Btn_Label;
        public string btnlabel
        {
            get { return m_Btn_Label; }
            set { m_Btn_Label = value; }
        }

        private string m_Panel_Name;
        public string panelname
        {
            get { return m_Panel_Name; }
            set { m_Panel_Name = value; }
        }

        private int m_Id;
        public int id
        {
            get { return m_Id; }
            set { m_Id = value; }
        }

        private List<WorkPanelItem> m_WorkPanel_TabItems = new List<WorkPanelItem>();
        public List<WorkPanelItem> workpaneltabitems
        {
            get { return m_WorkPanel_TabItems; }
            set { value = m_WorkPanel_TabItems; }
        }

        private AsyncObservableCollection<BtnPanelItem> m_BtnPanel_LampItems = new AsyncObservableCollection<BtnPanelItem>();
        public AsyncObservableCollection<BtnPanelItem> btnpanellampitems
        {
            get { return m_BtnPanel_LampItems; }
            set { value = m_BtnPanel_LampItems; }
        }

        private XmlNodeList m_nodelist;
        public XmlNodeList nodelist
        {
            get { return m_nodelist; }
            set { m_nodelist = value; }
        }

        public void RemoveBtnPanelLampItemByID(int id)
        {
            for (int i = 0; i < btnpanellampitems.Count; i++)
            {
                BtnPanelItem item = btnpanellampitems[i];
                if (item == null) continue;
                if(item.id == id)
                    btnpanellampitems.Remove(item);
            }
        }

        public void AddBtnPanelLampItemByID(int id)
        {
            BtnPanelItem item = new BtnPanelItem();
            item.id = id;
            item.btnlamp = false;

            m_BtnPanel_LampItems.Add(item);
            m_BtnPanel_LampItems.Sort(x => x.id);
        }

        public BtnPanelItem GetBtnPanelLampItemByID(int id)
        {
            foreach (BtnPanelItem item in btnpanellampitems)
            {
                if (item.id == id)
                    return item;
            }
            return null;
        }
    }
}
