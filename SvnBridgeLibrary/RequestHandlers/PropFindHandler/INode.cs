using System.Xml;
using SvnBridge.Handlers;

namespace SvnBridge.Nodes
{
    public interface INode
    {
        string Href(RequestHandlerBase handler);
        string GetProperty(RequestHandlerBase handler, XmlElement property);
    }
}