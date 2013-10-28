using System.Collections.Generic;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace SvnBridge.Protocol
{
    public class AddFileData
    {
        [XmlElement("checked-in", Namespace = WebDav.Namespaces.DAV)] public CheckedInData CheckedIn = null;

        [XmlAttribute("name", DataType = "string", Form = XmlSchemaForm.Unqualified)] public string Name = null;

        [XmlElement("prop", Namespace = WebDav.Namespaces.SVNDAV)] public List<PropData> Prop = new List<PropData>();

        [XmlElement("set-prop", Namespace = WebDav.Namespaces.SVN)] public List<SetPropData> SetProp =
            new List<SetPropData>();

        [XmlElement("txdelta", Namespace = WebDav.Namespaces.SVN)] public TxDeltaData TxDelta = null;

        public AddFileData()
        {
        }
    }
}