using System.Collections.Generic;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace SvnBridge.Protocol
{
    [XmlRoot("update-report", Namespace = WebDav.Namespaces.SVN)]
    public class UpdateReportResponseData
    {
        [XmlElement("open-directory", Namespace = WebDav.Namespaces.SVN)] public List<OpenDirectoryData> OpenDirectories
            = new List<OpenDirectoryData>();

        [XmlAttribute("send-all", DataType = "boolean", Form = XmlSchemaForm.Unqualified)] public bool SendAll = false;

        [XmlElement("target-revision", Namespace = WebDav.Namespaces.SVN)] public TargetRevisionData TargetRevision =
            null;

        public UpdateReportResponseData()
        {
        }
    }
}