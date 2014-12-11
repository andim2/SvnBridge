using System;
using System.Collections.Generic;
using System.Xml.Schema;
using System.Xml.Serialization;
using SvnBridge.Interfaces;
using SvnBridge.Net;

namespace SvnBridge.Protocol
{
    [XmlRoot("update-report", Namespace = WebDav.Namespaces.SVN)]
    public class UpdateReportData
    {
        [XmlElement("entry", Namespace = WebDav.Namespaces.SVN)]
        public List<EntryData> Entries = null;

        [XmlElement("missing", Namespace = WebDav.Namespaces.SVN)]
        public List<string> Missing = null;

        [XmlAttribute("send-all", DataType = "boolean", Form = XmlSchemaForm.Unqualified)]
        public bool SendAll = false;

        [XmlElement("src-path", Namespace = WebDav.Namespaces.SVN, DataType = "string")]
        public string SrcPath = null;

        [XmlElement("target-revision", Namespace = WebDav.Namespaces.SVN, DataType = "string")]
        public string TargetRevision = null;

        [XmlElement("update-target", Namespace = WebDav.Namespaces.SVN, DataType = "string")]
        public string UpdateTarget = null;

        public bool IsCheckOut
        {
            get { return Entries[0].StartEmpty && Entries.Count == 1; }
        }
    }
}