using System; // Exception
using SvnBridge.Handlers; // RequestHandlerBase

namespace SvnBridge.Nodes
{
    public interface INode
    {
        string Href(RequestHandlerBase handler);
        string GetProperty(RequestHandlerBase handler, string propertyName);
    }

    public abstract class NodeBase : INode
    {
        public abstract string Href(RequestHandlerBase handler);

        public string GetProperty(RequestHandlerBase handler, string propertyName)
        {
            string prop = GetProperty_Core(handler, propertyName);

            bool haveProperty = (null != prop);
            if (!haveProperty)
            {
                ReportErrorPropertyNotFound(propertyName);
            }

            return prop;
        }

        protected abstract string GetProperty_Core(RequestHandlerBase handler, string propertyName);

        private static void ReportErrorPropertyNotFound(string propertyName)
        {
            throw new Exception("Property not found: " + propertyName);
        }
    }
}
