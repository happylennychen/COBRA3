using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Xml;
using System.Xml.Linq;
using System.Reflection;
using Cobra.Common;

namespace Cobra.DM
{
    public class DMParameterList : ParamContainer
    {
        //父对象保存
        private DMDataManage m_parent;
        public DMDataManage parent
        {
            get { return m_parent; }
            set { m_parent = value; }
        }

        private Int16 pos = 0;

        /// <summary>
        /// ParameterList构造函数
        /// </summary>
        /// <param name="node"></param>
        public DMParameterList(object pParent, XElement node)
        {
            parent = (DMDataManage)pParent;

            listname = node.Attribute("Name").Value;
            if (node.Attribute("Guid") != null)
                guid = Convert.ToUInt32(node.Attribute("Guid").Value, 16);

            IEnumerable<XElement> myTargetNodes = from myTarget in node.Elements("Element") where myTarget.HasElements select myTarget;
            foreach (XElement snode in myTargetNodes)
            {
                DMParameter param = new DMParameter(snode);
                param.sectionpos = pos;
                AddParameter(param);
                pos++;
            }
        }

        public void AddParameter(DMParameter p)
        {
            parameterlist.Add(p);
        }

    }
}
