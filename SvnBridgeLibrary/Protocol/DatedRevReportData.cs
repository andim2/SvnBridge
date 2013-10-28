using System;
using System.Globalization;
using System.Xml.Serialization;

namespace SvnBridge.Protocol
{
    [XmlRoot("dated-rev-report", Namespace = WebDav.Namespaces.SVN)]
    public class DatedRevReportData
    {
        [XmlElement("creationdate", Namespace = WebDav.Namespaces.DAV, DataType = "string")] 
        public string creationdate;

        public DateTime CreationDate
        {
            get
            {
                 return DateTime.Parse(creationdate);
            }
        }
    }
}
