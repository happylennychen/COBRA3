using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;

namespace Cobra.Update
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {

        protected override void OnStartup(StartupEventArgs e)
        {
            LoadResource();
            base.OnStartup(e);
        }

        private void LoadResource()
        {
            ResourceDictionary langRd = null;
            try
            {
#if O2MICRO
                langRd = Application.LoadComponent(new Uri("/Cobra.Images;component/Images/O2images.xaml", UriKind.Relative)) as ResourceDictionary;
#else
                langRd = Application.LoadComponent(new Uri("/Cobra.Images;component/Images/BGMimages.xaml", UriKind.Relative)) as ResourceDictionary;
#endif
            }
            catch (Exception e)
            {
            }
            if (langRd != null)
                this.Resources.MergedDictionaries.Add(langRd);
        }
    }
}
