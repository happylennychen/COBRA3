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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections.ObjectModel;

namespace Cobra.SBSPanel
{
    /// <summary>
    /// Interaction logic for ChargerControl.xaml
    /// </summary>
    public partial class ChargerControl : UserControl
    {
        private ObservableCollection<SFLModel> charger_parameterlist = new ObservableCollection<SFLModel>();
        public ChargerControl()
        {
            InitializeComponent();
        }

        public void SetDataSource(ObservableCollection<SFLModel> parameterlist)
        {
            charger_parameterlist = parameterlist;
        }

        public void update(bool bSeaElf)
        {
            //ChargerstatusUpdate(bSeaElf);
            ThermalUpdate(bSeaElf);
        }

        private void ChargerstatusUpdate(bool bSeaElf)
        {
            byte bdata = 0;
            if (!bSeaElf)
            {
                foreach (SFLModel model in charger_parameterlist)
                {
                    switch (model.guid)
                    {
                        case 0x0003c100:
                            {
                                if (model.data != 0)
                                    bdata |= 0x01;
                                else
                                    bdata &= 0xFE;
                                break;
                            }
                        case 0x0003c101:
                            {
                                if (model.data != 0)
                                    bdata |= 0x02;
                                else
                                    bdata &= 0xFD;
                                break;
                            }
                        case 0x0003c102:
                            {
                                if (model.data != 0)
                                    bdata |= 0x04;
                                else
                                    bdata &= 0xFB;
                                break;
                            }
                        case 0x0003c103:
                            {
                                if (model.data != 0)
                                    bdata |= 0x08;
                                else
                                    bdata &= 0xF7;
                                break;
                            }
                        case 0x0003c104:
                            {
                                if (model.data != 0)
                                    bdata |= 0x10;
                                else
                                    bdata &= 0xEF;
                                break;
                            }
                    }
                }
            }
            chargerstatus.Update(bdata);
        }

        private void ThermalUpdate(bool bSeaElf)
        {
            byte bdata = 0;
            int  index = 0;
            bool bcc = false;
            double ddata = 0;
            string[] dlist = new string[5];
            if (!bSeaElf)
            {
                foreach (SFLModel model in charger_parameterlist)
                {
                    switch (model.guid)
                    {
                        case 0x0003c200:
                            {
                                if (model.data != 0)
                                    bdata |= 0x01;
                                else
                                    bdata &= 0xFE;
                                break;
                            }
                        case 0x0003c201:
                            {
                                if (model.data != 0)
                                    bdata |= 0x02;
                                else
                                    bdata &= 0xFD;
                                break;
                            }
                        case 0x0003c202:
                            {
                                if (model.data != 0)
                                    bdata |= 0x04;
                                else
                                    bdata &= 0xFB;
                                break;
                            }
                        case 0x0003c203:
                            {
                                if (model.data != 0)
                                    bdata |= 0x08;
                                else
                                    bdata &= 0xF7;
                                break;
                            }
                        case 0x0003c204:
                            {
                                if (model.data != 0)
                                    bdata |= 0x10;
                                else
                                    bdata &= 0xEF;
                                break;
                            }
                        case 0x00039000: //CC
                            {
                                if (model.data < 0) index = 0;
                                else index = (int)model.data;
                                if ((index > model.itemlist.Count) || (!Double.TryParse(model.itemlist[index], out ddata)))
                                {
                                    dlist[0] = "CC/2";
                                    dlist[1] = "CC";
                                    bcc = false;
                                }
                                else
                                {
                                    dlist[0] = String.Format("{0:F1}", ddata / 2);
                                    dlist[1] = model.itemlist[index];
                                    bcc = true;
                                }
                                break;
                            }
                        case 0x00038000: //CV
                            {
                                if (model.data < 0) index = 0;
                                else index = (int)model.data;
                                if ((index > model.itemlist.Count) || (!Double.TryParse(model.itemlist[index], out ddata)))
                                    dlist[2] = "CV";
                                else
                                    dlist[2] = model.itemlist[index];
                                break;
                            }
                        case 0x00038100: //CV_T34
                            {
                                if (model.data < 0) index = 0;
                                else index = (int)model.data;
                                if ((index > model.itemlist.Count) || (!Double.TryParse(model.itemlist[index], out ddata)))
                                    dlist[3] = "CV_T34";
                                else
                                    dlist[3] = model.itemlist[index];
                                break;
                            }
                        case 0x00038200: //CV_T45
                            {
                                if (model.data < 0) index = 0;
                                else index = (int)model.data;
                                if ((index > model.itemlist.Count) || (!Double.TryParse(model.itemlist[index], out ddata)))
                                    dlist[4] = "CV_T45";
                                else
                                    dlist[4] = model.itemlist[index];
                                break;
                            }
                    }
                }
            }
            else
            {
                bcc = false;
                bdata = 0;
                dlist[0] = "CC/2";
                dlist[1] = "CC";
                dlist[2] = "CV";
                dlist[3] = "CV_T34"; 
                dlist[4] = "CV_T45";
            }
            thermal.Update(bcc,bdata,dlist);
        }
    }
}
