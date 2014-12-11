using System;
using System.Xml.Serialization;

namespace SvnBridge.Protocol
{
    [Serializable]
    [XmlRoot("propfind", Namespace = WebDav.Namespaces.DAV)]
    public class PropFindData
    {
        [XmlElement("allprop", Namespace = WebDav.Namespaces.DAV)] public AllPropData AllProp = null;

        [XmlElement("prop", Namespace = WebDav.Namespaces.DAV)] public PropData Prop = null;

        [XmlElement("propname", Namespace = WebDav.Namespaces.DAV)] public PropNameData PropName = null;

        public PropFindData()
        {
        }
    }
}