using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Data;

namespace Cobra.EM
{
    public class BusOptionListCollectionView : ListCollectionView
    {
        public BusOptionListCollectionView(IList list) : base(list) { }

        private int m_Order;
        public int order
        {
            get { return m_Order; }
            set { m_Order = value; }
        }
    }
}
