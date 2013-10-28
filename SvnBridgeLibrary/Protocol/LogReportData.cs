using System.Xml.Serialization;

namespace SvnBridge.Protocol
{
    [XmlRoot("log-report", Namespace = WebDav.Namespaces.SVN)]
    public class LogReportData
    {
        [XmlElement("discover-changed-paths", Namespace = WebDav.Namespaces.SVN, DataType = "string")] public string
            DiscoverChangedPaths = null;

        [XmlElement("end-revision", Namespace = WebDav.Namespaces.SVN, DataType = "string")] public string EndRevision =
            null;

        [XmlElement("limit", Namespace = WebDav.Namespaces.SVN, DataType = "string")] public string Limit = null;

        [XmlElement("path", Namespace = WebDav.Namespaces.SVN, DataType = "string")] public string Path = null;

        [XmlElement("start-revision", Namespace = WebDav.Namespaces.SVN, DataType = "string")] public string
            StartRevision = null;

        public LogReportData()
        {
        }
    }
}