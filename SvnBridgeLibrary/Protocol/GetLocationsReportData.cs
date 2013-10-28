using System.Xml.Serialization;
using System.Collections.Generic;

namespace SvnBridge.Protocol
{
    [XmlRoot("get-locations", Namespace = WebDav.Namespaces.SVN)]
    public class GetLocationsReportData
    {
        [XmlElement("location-revision", Namespace = WebDav.Namespaces.SVN)]
        public List<string> LocationRevision = null;

        [XmlElement("path", Namespace = WebDav.Namespaces.SVN, DataType = "string")] 
        public string Path = null;

        [XmlElement("peg-revision", Namespace = WebDav.Namespaces.SVN, DataType = "string")] 
        public string PegRevision = null;
    }
}
