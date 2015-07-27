using CodePlex.TfsLibrary.RepositoryWebSvc; // ItemType
using SvnBridge.Handlers; // RequestHandlerBase
using SvnBridge.SourceControl; // ItemMetaData, TFSSourceControlProvider
using SvnBridge.Utility; // Helper.Encode()

namespace SvnBridge.Nodes
{
    public class BcFileNode : NodeBase
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

        public override string Href(RequestHandlerBase handler)
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

        protected override string GetProperty_Core(RequestHandlerBase handler, string propertyName)
        {
            return node.GetProperty(handler, propertyName);
        }

        #endregion
    }
}
