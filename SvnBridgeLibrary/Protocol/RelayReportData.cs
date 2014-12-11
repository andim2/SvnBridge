using System.Xml.Serialization;

namespace SvnBridge.Protocol
{
    [XmlRoot("replay-report", Namespace = WebDav.Namespaces.SVN)]
    public class ReplayReportData
    {
        [XmlElement("revision", Namespace = WebDav.Namespaces.SVN)]
        public int Revision;

        [XmlElement("low-water-mark", Namespace = WebDav.Namespaces.SVN)]
        public int LowWaterMark;

        [XmlElement("send-deltas", Namespace = WebDav.Namespaces.SVN)]
        public int SendDelta;
    }
}
