using System.Xml.Serialization;

namespace SvnBridge.Protocol
{
    [XmlRoot("checkout", Namespace = WebDav.Namespaces.DAV)]
    public class CheckoutData
    {
        [XmlElement("activity-set", Namespace = WebDav.Namespaces.DAV)] public ActivitySetData ActivitySet = null;

        public CheckoutData()
        {
        }
    }
}