using System.Xml.Serialization;

namespace SvnBridge.Protocol
{
    [XmlRoot("merge-response", Namespace = WebDav.Namespaces.DAV)]
    public class MergeResponseData
    {
        [XmlElement("updated-set", Namespace = WebDav.Namespaces.DAV)] public UpdatedSetData UpdatedSet =
            new UpdatedSetData();

        public MergeResponseData()
        {
        }
    }
}