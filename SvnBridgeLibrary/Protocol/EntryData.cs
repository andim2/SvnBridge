using System.Xml.Schema;
using System.Xml.Serialization;

namespace SvnBridge.Protocol
{
    public class EntryData
    {
        [XmlText()] public string path;

        [XmlAttribute("rev", DataType = "string", Form = XmlSchemaForm.Unqualified)] public string Rev = null;

        [XmlAttribute("start-empty", DataType = "boolean", Form = XmlSchemaForm.Unqualified)] public bool StartEmpty =
            false;

        public EntryData()
        {
        }

        public override string ToString()
        {
            return path + " #" + Rev;
        }
    }
}