using System;
using System.Xml.Serialization;

namespace SvnBridge.Protocol
{
    [Serializable]
    [XmlType("allprop", Namespace = WebDav.Namespaces.DAV)]
    public class AllPropData
    {
        public AllPropData()
        {
        }
    }
}