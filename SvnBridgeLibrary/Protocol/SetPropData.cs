using System.Xml.Schema;
using System.Xml.Serialization;

namespace SvnBridge.Protocol
{
    public class SetPropData
    {
        [XmlAttribute("name", DataType = "string", Form = XmlSchemaForm.Unqualified)] public string Name = null;

        [XmlText(DataType = "string")] public string Value = null;

        public SetPropData()
        {
        }

        public SetPropData(string name,
                           string value)
        {
            Name = name;
            Value = value;
        }
    }
}