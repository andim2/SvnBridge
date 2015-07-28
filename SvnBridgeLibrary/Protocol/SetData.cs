using System.Xml.Serialization;

namespace SvnBridge.Protocol
{
    [XmlRoot("set", Namespace = WebDav.Namespaces.DAV)]
    public sealed class SetData
    {
        [XmlElement("prop", Namespace = WebDav.Namespaces.DAV)] public PropData Prop = new PropData();

        public SetData()
        {
        }
    }
}