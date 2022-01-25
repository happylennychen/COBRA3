using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Threading;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Windows.Threading;
using System.Windows.Data;
using System.Collections;

namespace Cobra.Common
{
    public class AsyncObservableCollection<T> : ObservableCollection<T>
    {
        public override event NotifyCollectionChangedEventHandler CollectionChanged;
        private static object _syncLock = new object();

        public AsyncObservableCollection()
        {
            enableCollectionSynchronization(this, _syncLock);
        }

        public void Sort<TKey>(Func<T, TKey> keySelector, ListSortDirection direction = ListSortDirection.Ascending)
        {
            switch (direction)
            {
                case ListSortDirection.Ascending:
                    {
                        ApplySort(Items.OrderBy(keySelector));
                        break;
                    }
                case ListSortDirection.Descending:
                    {
                        ApplySort(Items.OrderByDescending(keySelector));
                        break;
                    }
            }
        }

        public void Sort<TKey>(Func<T, TKey> keySelector, IComparer<TKey> comparer)
        {
            ApplySort(Items.OrderBy(keySelector, comparer));
        }

        private void ApplySort(IEnumerable<T> sortedItems)
        {
            var sortedItemsList = sortedItems.ToList();

            foreach (var item in sortedItemsList)
            {
                Move(IndexOf(item), sortedItemsList.IndexOf(item));
            }
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {            
            try
            {
                using (BlockReentrancy())
                {
                    var eh = CollectionChanged;
                    if (eh == null) return;

                    foreach (NotifyCollectionChangedEventHandler nh in CollectionChanged.GetInvocationList())
                    {
                        DispatcherObject dispObj = nh.Target as DispatcherObject;
                        if (dispObj != null)
                        {
                            Dispatcher dispatcher = dispObj.Dispatcher;
                            if (dispatcher != null && !dispatcher.CheckAccess())
                            {/*
                                dispatcher.BeginInvoke(
                                    (Action)(() => nh.Invoke(this,
                                        new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset))),
                                    DispatcherPriority.DataBind);*/
                                dispatcher.Invoke(DispatcherPriority.DataBind, (Action)(() => OnCollectionChanged(e)));
                                continue;
                            }
                        }
                        nh.Invoke(this, e);
                    }
                }
            }
            catch
            {

            }
            /*
            try
            {
                using (BlockReentrancy())
                {
                    var eh = CollectionChanged;
                    if (eh == null) return;

                    var dispatcher = (from NotifyCollectionChangedEventHandler nh in eh.GetInvocationList()
                                      let dpo = nh.Target as DispatcherObject
                                      where dpo != null
                                      select dpo.Dispatcher).FirstOrDefault();

                    if (dispatcher != null && dispatcher.CheckAccess() == false)
                    {
                        dispatcher.Invoke(DispatcherPriority.DataBind, (Action)(() => OnCollectionChanged(e)));
                    }
                    else
                    {
                        foreach (NotifyCollectionChangedEventHandler nh in eh.GetInvocationList())
                            nh.Invoke(this, e);
                    }
                }
            }
            catch
            {

            }*/
        }

        private static void enableCollectionSynchronization(IEnumerable collection, object lockObject)
        {
            var method = typeof(BindingOperations).GetMethod("EnableCollectionSynchronization",
                                    new Type[] { typeof(IEnumerable), typeof(object) });
            if (method != null)
            {
                // It's .NET 4.5
                method.Invoke(null, new object[] { collection, lockObject });
            }
        }
    }

}
