using System.Collections.Generic;
using System.Xml.Serialization;

namespace SvnBridge.Protocol
{
    [XmlRoot("updated-set", Namespace = WebDav.Namespaces.DAV)]
    public class UpdatedSetData
    {
        [XmlElement("response", Namespace = WebDav.Namespaces.DAV)] public List<ResponseData> Responses =
            new List<ResponseData>();

        public UpdatedSetData()
        {
        }
    }
}