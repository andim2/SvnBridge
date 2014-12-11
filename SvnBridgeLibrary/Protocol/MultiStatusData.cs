using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace SvnBridge.Protocol
{
    [Serializable]
    [XmlRoot("multistatus", Namespace = WebDav.Namespaces.DAV)]
    public class MultiStatusData
    {
        [XmlElement("response", Namespace = WebDav.Namespaces.DAV)] public List<ResponseData> Responses =
            new List<ResponseData>();

        public MultiStatusData()
        {
        }
    }
}