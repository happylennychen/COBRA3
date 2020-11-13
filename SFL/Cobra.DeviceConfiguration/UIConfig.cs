using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows;

namespace Cobra.DeviceConfigurationPanel
{
    public class UIConfig
    {
        private ObservableCollection<btnControl> m_btn_Controls = new ObservableCollection<btnControl>();
        public ObservableCollection<btnControl> btn_controls
        {
            get { return m_btn_Controls; }
            set { m_btn_Controls = value; }
        }

        public btnControl GetBtnControlByName(string name)
        {
            foreach (btnControl bctrl in btn_controls)
            {
                if (bctrl.btn_name.Equals(name))
                    return bctrl;
            }
            return null;
        }
    }

    public class btnControl : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private string m_btn_Name;
        public string btn_name
        {
            get { return m_btn_Name; }
            set { m_btn_Name = value; }
        }

        private string m_btn_Content;
        public string btn_content
        {
            get { return m_btn_Content; }
            set
            {
                m_btn_Content = value;
                OnPropertyChanged("btn_content");
            }
        }

        private bool m_bEnable;
        public bool benable
        {
            get { return m_bEnable; }
            set { m_bEnable = value; }
        }

        private Visibility m_visi;
        public Visibility visi
        {
            get { return m_visi; }
            set { m_visi = value; }
        }

        public System.Windows.Controls.ContextMenu btn_cm = new System.Windows.Controls.ContextMenu();

        private ObservableCollection<subMenu> m_btn_Menu_Control = new ObservableCollection<subMenu>();
        public ObservableCollection<subMenu> btn_menu_control
        {
            get { return m_btn_Menu_Control; }
            set { m_btn_Menu_Control = value; }
        }
    }

    public class subMenu : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private string m_Header;
        public string header
        {
            get { return m_Header; }
            set { m_Header = value; }
        }

        private UInt16 m_SubTask;
        public UInt16 subTask
        {
            get { return m_SubTask; }
            set { m_SubTask = value; }
        }
    }
}
