using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using System.Diagnostics;
using System.Threading;

namespace Cobra.Shell
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            
            //当前运行WPF程序的进程实例
            Process process = Process.GetCurrentProcess();
            //遍历WPF程序的同名进程组
            foreach (Process p in Process.GetProcessesByName(process.ProcessName))
            {
                //不是同一进程并且本进程启动时间最晚,则关闭较早进程
                if (p.Id != process.Id && (p.StartTime - process.StartTime).TotalMilliseconds <= 0)
                {/*
                    MessageBox.Show("COBRA is running!", "COBRA", MessageBoxButton.OK);
                    process.Kill();//这个地方用kill 而不用Shutdown();的原因是,Shutdown关闭程序在进程管理器里进程的释放有延迟不是马上关闭进程的
                    return;*/
                }
            }
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
            catch(Exception e)
            {
            }
            if (langRd != null)
                this.Resources.MergedDictionaries.Add(langRd);
        }
    }
}
