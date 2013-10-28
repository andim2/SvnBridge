using System.Xml.Serialization;

namespace SvnBridge.Protocol
{
    public class CheckedInData
    {
        [XmlElement("href", Namespace = WebDav.Namespaces.DAV, DataType = "string")] public string Href = null;

        public CheckedInData()
        {
        }
    }
}