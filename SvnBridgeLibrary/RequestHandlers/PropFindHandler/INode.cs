using System.Xml; // XmlElement
using SvnBridge.Handlers; // RequestHandlerBase

namespace SvnBridge.Nodes
{
    public interface INode
    {
        string Href(RequestHandlerBase handler);
        string GetProperty(RequestHandlerBase handler, XmlElement property);
    }
}
