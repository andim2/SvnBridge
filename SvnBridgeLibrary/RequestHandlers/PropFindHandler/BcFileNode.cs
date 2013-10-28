using System.Xml;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using SvnBridge.Handlers;
using SvnBridge.Interfaces;
using SvnBridge.SourceControl;
using SvnBridge.Utility;

namespace SvnBridge.Nodes
{
    public class BcFileNode : INode
    {
        private readonly ItemMetaData item;
        private readonly FileNode node;
        private readonly int requestVersion;

        public BcFileNode(int requestVersion,
                          ItemMetaData item,
                          TFSSourceControlProvider sourceControlProvider)
        {
            this.requestVersion = requestVersion;
            this.item = item;
            node = new FileNode(item, sourceControlProvider);
        }

        #region INode Members

        public string Href(RequestHandlerBase handler)
        {
            string path = item.Name;

            if (!path.StartsWith("/"))
            {
                path = "/" + path;
            }

            string href = "/!svn/bc/" + requestVersion + path;

            if (item.ItemType == ItemType.Folder && ((href.Length == 0) || (href[href.Length - 1] != '/')))
            {
                href += "/";
            }

        	return handler.GetLocalPath(Helper.Encode(href));
        }

        public string GetProperty(RequestHandlerBase handler, XmlElement property)
        {
            return node.GetProperty(handler, property);
        }

        #endregion
    }
}