using System.Xml.Serialization;

namespace SvnBridge.Protocol
{
    [XmlRoot("options-response", Namespace = WebDav.Namespaces.DAV)]
    public class OptionsResponseData
    {
        [XmlElement("activity-collection-set", Namespace = WebDav.Namespaces.DAV)] public ActivityCollectionSetData
            ActivityCollectionSet = null;

        public OptionsResponseData()
        {
        }
    }
}