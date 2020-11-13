using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace O2Micro.Cobra.DeviceConfigurationPanel
{
    //SINGLETON模式
    class ConfigData : INotifyPropertyChanged
    {
        public static ConfigData Instance = new ConfigData();

        private ConfigData() { }

        #region INotifyPropertyChanged 成员
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        void NotifyPropertyChange(string proper)
        {
            if (PropertyChanged == null)
                return;
            PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(proper));
        }
        #endregion

        private bool m_EraseBtn_IsEnable;
        public bool erasebtn_isenable
        {
            get { return m_EraseBtn_IsEnable; }
            set { m_EraseBtn_IsEnable = value; }
        }
    }
}
