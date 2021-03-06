using System.Xml.Schema;
using System.Xml.Serialization;

namespace SvnBridge.Protocol
{
    public sealed class EntryData
    {
        [XmlText()] public string path;

        [XmlAttribute("rev", DataType = "string", Form = XmlSchemaForm.Unqualified)] public string Rev /* = null */;

        /// <summary>
        /// start-empty true indicates that the client
        /// does NOT have a prior revision of this item
        /// (i.e., needs full processing)
        /// </summary>
        [XmlAttribute("start-empty", DataType = "boolean", Form = XmlSchemaForm.Unqualified)] public bool StartEmpty /* = false */;

        public EntryData()
        {
        }

        public override string ToString()
        {
            return path + " #" + Rev;
        }
    }
}
