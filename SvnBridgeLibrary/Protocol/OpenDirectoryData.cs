using System.Collections.Generic;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace SvnBridge.Protocol
{
    public class OpenDirectoryData
    {
        [XmlElement("add-directory", Namespace = WebDav.Namespaces.SVN)] public List<AddDirectoryData> AddDirectory =
            new List<AddDirectoryData>();

        [XmlElement("add-file", Namespace = WebDav.Namespaces.SVN)] public List<AddFileData> AddFile =
            new List<AddFileData>();

        [XmlElement("checked-in", Namespace = WebDav.Namespaces.DAV)] public CheckedInData CheckedIn = null;

        [XmlAttribute("rev", DataType = "string", Form = XmlSchemaForm.Unqualified)] public string Rev = null;

        [XmlElement("set-prop", Namespace = WebDav.Namespaces.SVN)] public List<SetPropData> SetProp =
            new List<SetPropData>();

        public OpenDirectoryData()
        {
        }
    }
}