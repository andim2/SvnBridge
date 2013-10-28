using System.Xml.Schema;
using System.Xml.Serialization;

namespace SvnBridge.Protocol
{
    public class TargetRevisionData
    {
        [XmlAttribute("rev", DataType = "string", Form = XmlSchemaForm.Unqualified)] public string Rev = null;

        public TargetRevisionData()
        {
        }
    }
}