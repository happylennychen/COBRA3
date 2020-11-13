#define Timer
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
//using System.Windows.Threading;
using System.Threading;
using System.ComponentModel;
using Cobra.Common;
//using System.Timers;

namespace Cobra.ScanPanel
{

    public static class ThreadMonitor
    {
        //private static System.Windows.Threading.DispatcherTimer t = new System.Windows.Threading.DispatcherTimer();
#if Timer
        private static System.Timers.Timer t = new System.Timers.Timer();
#else
        private static Thread t = new Thread(Callback);
#endif

        private static object PTC_Lock = new object();
        private static int mPoolThreadsCounter = 0;
        public static int PoolThreadsCounter
        {
            set
            {
                lock (PTC_Lock)
                {
                    mPoolThreadsCounter = value;
                }
            }
            get
            {
                lock (PTC_Lock)
                {
                    return mPoolThreadsCounter;
                }

            }
        }


        private static object SKIP_Lock = new object();
        private static long mTimerSkipCounter = 0;
        public static long TimerSkipCounter
        {
            set
            {
                lock (SKIP_Lock)
                {
                    mTimerSkipCounter = value;
                }
            }
            get
            {
                lock (SKIP_Lock)
                {
                    return mTimerSkipCounter;
                }

            }
        }

        #region IO Thread
        private static List<Thread> mIOThreadList = new List<Thread>(); //存放需要监控的线程
        public static object IOThreadListLock = new object();
        public static List<Thread> IOThreadList
        {
            set
            {
                lock (IOThreadListLock)
                {
                    mIOThreadList = value;
                }
            }
            get
            {
                lock (IOThreadListLock)
                {
                    return mIOThreadList;
                }

            }
        }

        private static object IOTC_Lock = new object();
        private static int mIOThreadsCounter = 0;
        public static int IOThreadsCounter
        {
            set
            {
                lock (IOTC_Lock)
                {
                    mIOThreadsCounter = value;
                }
            }
            get
            {
                lock (IOTC_Lock)
                {
                    return mIOThreadsCounter;
                }

            }
        }


        public static void IO_Add(Thread t)
        {
            lock (IOThreadListLock)
            {
                IOThreadList.Add(t);
            }
        }
        public static void IO_Remove(Thread t)
        {
            lock (IOThreadListLock)
            {
                IOThreadList.Remove(t);
            }
        }

        private static object IOAbandonCounter_Lock = new object();
        private static int mIOAbandonCounter = 0;
        public static int IOAbandonCounter
        {
            set
            {
                lock (IOAbandonCounter_Lock)
                {
                    mIOAbandonCounter = value;
                }
            }
            get
            {
                lock (IOAbandonCounter_Lock)
                {
                    return mIOAbandonCounter;
                }

            }
        }
        #endregion

        #region DB Thread
        private static List<Thread> mDBThreadList = new List<Thread>(); //存放需要监控的线程
        public static object DBThreadListLock = new object();
        public static List<Thread> DBThreadList
        {
            set
            {
                lock (DBThreadListLock)
                {
                    mDBThreadList = value;
                }
            }
            get
            {
                lock (DBThreadListLock)
                {
                    return mDBThreadList;
                }

            }
        }

        private static object DBTC_Lock = new object();
        private static int mDBThreadsCounter = 0;
        public static int DBThreadsCounter
        {
            set
            {
                lock (DBTC_Lock)
                {
                    mDBThreadsCounter = value;
                }
            }
            get
            {
                lock (DBTC_Lock)
                {
                    return mDBThreadsCounter;
                }

            }
        }


        public static void DB_Add(Thread t)
        {
            lock (DBThreadListLock)
            {
                DBThreadList.Add(t);
            }
        }
        public static void DB_Remove(Thread t)
        {
            lock (DBThreadListLock)
            {
                DBThreadList.Remove(t);
            }
        }

        private static object DBAbandonCounter_Lock = new object();
        private static int mDBAbandonCounter = 0;
        public static int DBAbandonCounter
        {
            set
            {
                lock (DBAbandonCounter_Lock)
                {
                    mDBAbandonCounter = value;
                }
            }
            get
            {
                lock (DBAbandonCounter_Lock)
                {
                    return mDBAbandonCounter;
                }

            }
        }
        #endregion


        #region UI Thread
        private static List<Thread> mUIThreadList = new List<Thread>(); //存放需要监控的线程
        public static object UIThreadListLock = new object();
        public static List<Thread> UIThreadList
        {
            set
            {
                lock (UIThreadListLock)
                {
                    mUIThreadList = value;
                }
            }
            get
            {
                lock (UIThreadListLock)
                {
                    return mUIThreadList;
                }

            }
        }

        private static object UITC_Lock = new object();
        private static int mUIThreadsCounter = 0;
        public static int UIThreadsCounter
        {
            set
            {
                lock (UITC_Lock)
                {
                    mUIThreadsCounter = value;
                }
            }
            get
            {
                lock (UITC_Lock)
                {
                    return mUIThreadsCounter;
                }

            }
        }


        public static void UI_Add(Thread t)
        {
            lock (UIThreadListLock)
            {
                UIThreadList.Add(t);
            }
        }
        public static void UI_Remove(Thread t)
        {
            lock (UIThreadListLock)
            {
                UIThreadList.Remove(t);
            }
        }

        private static object UIAbandonCounter_Lock = new object();
        private static int mUIAbandonCounter = 0;
        public static int UIAbandonCounter
        {
            set
            {
                lock (UIAbandonCounter_Lock)
                {
                    mUIAbandonCounter = value;
                }
            }
            get
            {
                lock (UIAbandonCounter_Lock)
                {
                    return mUIAbandonCounter;
                }

            }
        }
        #endregion

#if Timer
        public static void Start()                                  //开始监控
        {
            t.Interval = 50;
            t.Elapsed += new System.Timers.ElapsedEventHandler(t_Elapsed);
            t.Start();
        }
        private static int EnteringTimerCallback = 0;
        private static void t_Elapsed(object sender, EventArgs e)
        {
            if (0 == Interlocked.Exchange(ref EnteringTimerCallback, 1))    //0 indicates that the method was not in use, then set to 1 to indicates it is in use now. 
            {
                RemoveAbortedThread();
                Interlocked.Exchange(ref EnteringTimerCallback, 0); //Set to 0 indicates that the method is not in use
            }
        }
#else
        static void Callback()
        {
            while (true)
            {
                RemoveAbortedThread();
                Thread.Sleep(50);
            }
        }

        public static void Start()                                  //开始监控
        {
            //t.Interval = 50;
            //t.Elapsed += new System.Timers.ElapsedEventHandler(t_Elapsed);
            t.Start();
        }

        public static void Stop()                                  //停止监控
        {
            t.Abort();
        }
#endif
        

        private static void RemoveAbortedThread()                       //移除死掉的线程
        {
            lock (IOThreadListLock)
            {
                for (int i = 0; i < IOThreadList.Count; i++)
                {
                    Thread t = IOThreadList[i];
                    if (t.IsAlive == false)
                        IOThreadList.Remove(t);
                }
            }
            lock (DBThreadListLock)
            {
                for (int i = 0; i < DBThreadList.Count; i++)
                {
                    Thread t = DBThreadList[i];
                    if (t.IsAlive == false)
                        DBThreadList.Remove(t);
                }
            }
            lock (UIThreadListLock)
            {
                for (int i = 0; i < UIThreadList.Count; i++)
                {
                    Thread t = UIThreadList[i];
                    if (t.IsAlive == false)
                        UIThreadList.Remove(t);
                }
            }
        }
        public static void Reset()
        {
            t.Stop();
            PoolThreadsCounter = 0;
            TimerSkipCounter = 0;
            IOAbandonCounter = 0;
            DBAbandonCounter = 0;
            UIAbandonCounter = 0;

            lock (IOThreadListLock)
            {
                for (int i = 0; i < IOThreadList.Count; i++)
                {
                    Thread thread = IOThreadList[i];
                    thread.Abort();
                    //if (thread.IsAlive == false)
                    IOThreadList.Remove(thread);
                }
            }
            lock (DBThreadListLock)
            {
                for (int i = 0; i < DBThreadList.Count; i++)
                {
                    Thread thread = DBThreadList[i];
                    thread.Abort();
                    //if (thread.IsAlive == false)
                    DBThreadList.Remove(thread);
                }
            }
            lock (UIThreadListLock)
            {
                for (int i = 0; i < UIThreadList.Count; i++)
                {
                    Thread thread = UIThreadList[i];
                    thread.Abort();
                    //if (thread.IsAlive == false)
                    UIThreadList.Remove(thread);
                }
            }
        }

    }

    /// <summary>
    /// Interaction logic for ThreadMonitor.xaml
    /// </summary>
    public partial class ThreadMonitorWindow : Window
    {
        AsyncObservableCollection<ThreadInfo> DBThreadsInfo = new AsyncObservableCollection<ThreadInfo>();
        AsyncObservableCollection<ThreadInfo> IOThreadsInfo = new AsyncObservableCollection<ThreadInfo>();
        AsyncObservableCollection<ThreadInfo> UIThreadsInfo = new AsyncObservableCollection<ThreadInfo>();
#if Timer
        private static System.Timers.Timer t = new System.Timers.Timer();
#else
        private Thread t;
#endif
        public ThreadMonitorWindow()
        {
            InitializeComponent();

            IOThreadsGrid.DataContext = IOThreadsInfo;
            DBThreadsGrid.DataContext = DBThreadsInfo;
            UIThreadsGrid.DataContext = UIThreadsInfo;

#if Timer
            t.Interval = 1000;
            t.Elapsed += new System.Timers.ElapsedEventHandler(t_Elapsed);
#else
            t = new Thread(Callback);
#endif
            //t.Start();
        }

        private delegate void UpdateUIDelegate();
        private void UpdateUI()
        {
            IOThreadsInfo.Clear();
            lock (ThreadMonitor.IOThreadListLock)
            {
                foreach (Thread t in ThreadMonitor.IOThreadList)
                {
                    ThreadInfo ti = new ThreadInfo();
                    ti.Name = t.Name;
                    ti.ID = t.ManagedThreadId;
                    ti.State = t.ThreadState;
                    IOThreadsInfo.Add(ti);
                }
            }
            IOAliveThreadCount.Content = ThreadMonitor.IOThreadList.Count;
            IOAbandonedThreadCount.Content = ThreadMonitor.IOAbandonCounter.ToString();

            DBThreadsInfo.Clear();
            lock (ThreadMonitor.DBThreadListLock)
            {
                foreach (Thread t in ThreadMonitor.DBThreadList)
                {
                    ThreadInfo ti = new ThreadInfo();
                    ti.Name = t.Name;
                    ti.ID = t.ManagedThreadId;
                    ti.State = t.ThreadState;
                    DBThreadsInfo.Add(ti);
                }
            }
            DBAliveThreadCount.Content = ThreadMonitor.DBThreadList.Count;
            DBAbandonedThreadCount.Content = ThreadMonitor.DBAbandonCounter.ToString();

            UIThreadsInfo.Clear();
            lock (ThreadMonitor.UIThreadListLock)
            {
                foreach (Thread t in ThreadMonitor.UIThreadList)
                {
                    ThreadInfo ti = new ThreadInfo();
                    ti.Name = t.Name;
                    ti.ID = t.ManagedThreadId;
                    ti.State = t.ThreadState;
                    UIThreadsInfo.Add(ti);
                }
            }
            UIAliveThreadCount.Content = ThreadMonitor.UIThreadList.Count;
            UIAbandonedThreadCount.Content = ThreadMonitor.UIAbandonCounter.ToString();

            //PoolThreadsNumber.Content = ThreadMonitor.PoolThreadsCounter.ToString();
            int a, b, c = 0;
            ThreadPool.GetAvailableThreads(out a, out c);
            ThreadPool.GetMaxThreads(out b, out c);
            PoolDepth.Content = b.ToString();
            WorkingThreadNumber.Content = (b - a).ToString();
            TimerSkipedNumber.Content = ThreadMonitor.TimerSkipCounter;
        }
#if Timer
        void t_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            this.Dispatcher.Invoke(new UpdateUIDelegate(UpdateUI));
        }
#else
        private void Callback()
        {
            while (true)
            {
                Thread.Sleep(1000);
                this.Dispatcher.Invoke(new UpdateUIDelegate(UpdateUI));
            }
        }
#endif
        public void Start()
        {
            DBThreadsInfo.Clear();
            IOThreadsInfo.Clear();
            UIThreadsInfo.Clear();
            t.Start();
        }
        public void Stop()
        {
            t.Stop();
        }
    }

    public class ThreadInfo : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private int mID;
        public int ID
        {
            get { return mID; }
            set
            {
                mID = value;
                OnPropertyChanged("ID");
            }
        }

        private string mName;
        public string Name
        {
            get { return mName; }
            set
            {
                mName = value;
                OnPropertyChanged("Name");
            }
        }

        private ThreadState mState;
        public ThreadState State
        {
            get { return mState; }
            set
            {
                mState = value;
                OnPropertyChanged("State");
            }
        }
    }
}
