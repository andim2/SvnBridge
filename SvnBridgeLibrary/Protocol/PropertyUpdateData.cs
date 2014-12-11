using System.Xml.Serialization;

namespace SvnBridge.Protocol
{
    [XmlRoot("propertyupdate", Namespace = WebDav.Namespaces.DAV)]
    public class PropertyUpdateData
    {
        [XmlElement("set", Namespace = WebDav.Namespaces.DAV)] 
		public SetData Set = new SetData();

		[XmlElement("remove", Namespace = WebDav.Namespaces.DAV)]
		public SetData Remove = new SetData();

        public PropertyUpdateData()
        {
        }
    }
}