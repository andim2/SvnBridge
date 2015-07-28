using System.Xml.Serialization;

namespace SvnBridge.Protocol
{
    public sealed class TxDeltaData
    {
        [XmlText(DataType = "string")] public string Data /* = null */;

        public TxDeltaData()
        {
        }
    }
}
