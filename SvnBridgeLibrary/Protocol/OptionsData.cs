using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace SvnBridge.Protocol
{
    [XmlRoot("options", Namespace = WebDav.Namespaces.DAV)]
    public class OptionsData
    {
        [XmlElement("activity-collection-set", Namespace = WebDav.Namespaces.DAV, DataType = "string")]
        public string ActivityCollectionSet = null;
    }
}
