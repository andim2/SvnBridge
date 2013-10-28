using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;

namespace SvnBridge.Protocol
{
    [Serializable]
    public class PropData
    {
        [XmlAnyElement()] 
		public List<XmlElement> Properties = new List<XmlElement>();

        public PropData()
        {
        }

        public XmlElement FindProperty(string ns,
                                       string name)
        {
            return Properties.Find(delegate(XmlElement e) { return e.NamespaceURI == ns && e.LocalName == name; });
        }
    }
}