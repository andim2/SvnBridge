using System.Xml.Serialization;

namespace SvnBridge.Protocol
{
    public class TxDeltaData
    {
        [XmlText(DataType = "string")] public string Data = null;

        public TxDeltaData()
        {
        }
    }
}