using System;
using System.Xml.Serialization;

namespace SvnBridge.Protocol
{
    [Serializable]
    [XmlType("propname", Namespace = WebDav.Namespaces.DAV)]
    public sealed class PropNameData
    {
        public PropNameData()
        {
        }
    }
}