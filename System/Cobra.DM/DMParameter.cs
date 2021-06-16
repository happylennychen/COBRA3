using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using System.Collections;
using System.Globalization;
using Cobra.Common;

namespace Cobra.DM
{
    public class DMParameter : Parameter
    {
        /// <summary>
        /// 参数初始化
        /// </summary>
        /// <param name="node"></param>
        public DMParameter(XElement node)
        {
            string tmp = string.Empty;
            UInt16 u16data = 0;
            UInt32 u32data = 0;

            if (GetXElementValueByAttribute(node, "Guid") != null)
                guid = Convert.ToUInt32(GetXElementValueByAttribute(node, "Guid"), 16);

            if (GetXElementValueByName(node, "Key") != null)
                key = Convert.ToDouble(GetXElementValueByName(node, "Key"));

            if (GetXElementValueByName(node, "SubType") != null)
                subtype = Convert.ToUInt16(GetXElementValueByName(node, "SubType"));

            if (GetXElementValueByName(node, "SubSection") != null)
                subsection = Convert.ToUInt16(GetXElementValueByName(node, "SubSection"));

            if (GetXElementValueByName(node, "PhysicalData") != null)
                phydata = Convert.ToDouble(GetXElementValueByName(node, "PhysicalData"));

            if (GetXElementValueByName(node, "PhyRef") != null)
                phyref = Convert.ToDouble(GetXElementValueByName(node, "PhyRef"));

            if (GetXElementValueByName(node, "RegRef") != null)
                regref = Convert.ToDouble(GetXElementValueByName(node, "RegRef"));

            //Added by Leon, for KALL 10/14/17 projects
            if (GetXElementValueByName(node, "Offset") != null)
                offset = Convert.ToDouble(GetXElementValueByName(node, "Offset"));
            //Added by Leon

            /*(D151224)Francis, looks no used anymore
            if (GetXElementValueByName(node, "MaxValue") != null)
                maxvalue = Convert.ToDouble(GetXElementValueByName(node, "MaxValue"));

            if (GetXElementValueByName(node, "MinValue") != null)
                minvalue = Convert.ToDouble(GetXElementValueByName(node, "MinValue"));
             * */
            //(A151224)Francis, add for HexMin, HexMax, PhyMin, and PhyMax
            if (GetXElementValueByName(node, "HexMin") != null)
                dbHexMin = Convert.ToInt64(GetXElementValueByName(node, "HexMin"), 16);	//Support DWord, leon

            if (GetXElementValueByName(node, "HexMax") != null)
                dbHexMax = Convert.ToInt64(GetXElementValueByName(node, "HexMax"), 16);	//Support DWord, leon

            if (GetXElementValueByName(node, "PhyMin") != null)
                dbPhyMin = Convert.ToDouble(GetXElementValueByName(node, "PhyMin"));

            if (GetXElementValueByName(node, "PhyMax") != null)
                dbPhyMax = Convert.ToDouble(GetXElementValueByName(node, "PhyMax"));
            //(E151224)

            //Add bshow
            if (GetXElementValueByName(node, "bShow") != null)
                bShow = Convert.ToBoolean(GetXElementValueByName(node, "bShow"));

            XElement itemnodes = node.Element("ItemList");
            if (itemnodes != null)
            {
                IEnumerable<XElement> items = from Target in itemnodes.Elements() select Target;
                foreach (XElement item in items)
                    itemlist.Add(item.Value);
            }

            XElement Nodes = node.Element("LocationList");
            if (Nodes != null)
            {
                IEnumerable<XElement> snode = from Target in Nodes.Elements("Location") where Target.HasElements select Target;
                foreach (XElement ssnode in snode)
                {
                    tmp = GetXElementValueByAttribute(ssnode, "Position");
                    Reg register = new Reg();

                    if (GetXElementValueByName(ssnode, "Address") != null)
                    {
                        if(UInt16.TryParse(GetXElementValueByName(ssnode, "Address"),NumberStyles.HexNumber, null,out u16data))  
                            register.address = u16data;
                        else if(UInt32.TryParse(GetXElementValueByName(ssnode, "Address"), NumberStyles.HexNumber, null, out u32data))
                            register.u32Address = u32data;
                    }

                    if (GetXElementValueByName(ssnode, "StartBit") != null)
                        register.startbit = Convert.ToUInt16(GetXElementValueByName(ssnode, "StartBit"),10);

                    if (GetXElementValueByName(ssnode, "BitsNumber") != null)
                        register.bitsnumber = Convert.ToUInt16(GetXElementValueByName(ssnode, "BitsNumber"),10);

                    reglist.Add(tmp, register);
                }                
            }

            errorcode = LibErrorCode.IDS_ERR_SUCCESSFUL;

            XElement sflnodes = node.Element("Private");
            if (sflnodes != null)
            {
                IEnumerable<XElement> sflitems = from Target in sflnodes.Elements() select Target;
                foreach (XElement sflitem in sflitems)
                {
                    if (!sflitem.HasElements) continue;

                    SFL sfl = new SFL();
                    sfl.parent = this;

                    if (GetXElementValueByAttribute(sflitem, "Name") != null)
                        sfl.sflname = GetXElementValueByAttribute(sflitem, "Name");

                    IEnumerable<XElement> sflitemnodes = sflitem.Elements();
                    foreach (XElement sflitemnode in sflitemnodes)
                    {
                        if (!sflitemnode.HasElements)
                            sfl.nodetable.Add(sflitemnode.Name.LocalName, sflitemnode.Value);
                        else
                        {
                            IEnumerable<XElement> sflitemsnodes = sflitemnode.Elements();	//Use string instead of XName so that we can access it simply by Contains method. Leon
                            AsyncObservableCollection<string> list = new AsyncObservableCollection<string>();
                            foreach (XElement sflitemsnode in sflitemsnodes)
                                list.Add(sflitemsnode.Value);

                            sfl.nodetable.Add(sflitemnode.Name.LocalName, list);
                        }
                    }

                    sfllist.Add(sfl.sflname,sfl);
                }
            }
        }
    }
}
