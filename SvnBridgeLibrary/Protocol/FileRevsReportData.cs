using System.Xml.Serialization;

namespace SvnBridge.Protocol
{
    [XmlRoot("file-revs-report", Namespace = WebDav.Namespaces.SVN)]
    public class FileRevsReportData
    {
        [XmlElement("start-revision", Namespace = WebDav.Namespaces.SVN)]
        public int StartRevision;

        [XmlElement("end-revision", Namespace = WebDav.Namespaces.SVN)]
        public int EndRevision;

        [XmlElement("path", Namespace = WebDav.Namespaces.SVN)] 
        public string Path;
    }
}