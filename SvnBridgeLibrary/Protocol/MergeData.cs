using System.Xml.Serialization;

namespace SvnBridge.Protocol
{
    [XmlRoot("merge", Namespace = WebDav.Namespaces.DAV)]
    public class MergeData
    {
        [XmlElement("no-auto-merge", Namespace = WebDav.Namespaces.DAV)] public string NoAutoMerge = null;

        [XmlElement("no-checkout", Namespace = WebDav.Namespaces.DAV)] public string NoCheckout = null;

        [XmlElement("prop", Namespace = WebDav.Namespaces.DAV)] public PropData Prop = null;

        [XmlElement("source", Namespace = WebDav.Namespaces.DAV)] public SourceData Source = null;

        public MergeData()
        {
        }
    }
}