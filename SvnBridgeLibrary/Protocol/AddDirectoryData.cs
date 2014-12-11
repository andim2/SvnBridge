using System.Collections.Generic;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace SvnBridge.Protocol
{
    public class AddDirectoryData
    {
        [XmlElement("add-directory", Namespace = WebDav.Namespaces.SVN)] public List<AddDirectoryData> AddDirectory =
            new List<AddDirectoryData>();

        [XmlElement("add-file", Namespace = WebDav.Namespaces.SVN)] public List<AddFileData> AddFile =
            new List<AddFileData>();

        [XmlAttribute("bc-url", DataType = "string", Form = XmlSchemaForm.Unqualified)] public string BcUrl = null;

        [XmlElement("checked-in", Namespace = WebDav.Namespaces.DAV)] public CheckedInData CheckedIn = null;

        [XmlAttribute("name", DataType = "string", Form = XmlSchemaForm.Unqualified)] public string Name = null;

        [XmlElement("set-prop", Namespace = WebDav.Namespaces.SVN)] public List<SetPropData> SetProp =
            new List<SetPropData>();

        public AddDirectoryData()
        {
        }
    }
}