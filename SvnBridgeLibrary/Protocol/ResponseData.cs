using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace SvnBridge.Protocol
{
    [Serializable]
    public class ResponseData
    {
        [XmlElement("href", Namespace = WebDav.Namespaces.DAV, DataType = "string")] public string Href = null;

        [XmlElement("propstat", Namespace = WebDav.Namespaces.DAV)] public List<PropStatData> PropStat =
            new List<PropStatData>();

        [XmlElement("status", Namespace = WebDav.Namespaces.DAV, DataType = "string")] public string Status = null;

        public ResponseData()
        {
        }
    }
}