using System;
using System.Xml.Serialization;

namespace SvnBridge.Protocol
{
    [Serializable]
    [XmlRoot("get-locks-report", Namespace = WebDav.Namespaces.SVN)]
    public class GetLocksReportData
    {
    }
}