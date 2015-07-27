using SvnBridge.Handlers; // RequestHandlerBase
using SvnBridge.SourceControl; // TFSSourceControlProvider

namespace SvnBridge.Nodes
{
    // Node: <server>/!svn/vcc/default
    public class SvnVccDefaultNode : NodeBase
    {
        private string label;
        private string path;
        private TFSSourceControlProvider sourceControlProvider;

        public SvnVccDefaultNode(TFSSourceControlProvider sourceControlProvider,
                                 string path,
                                 string label)
        {
            this.sourceControlProvider = sourceControlProvider;
            this.path = path;
            this.label = label;
        }

        #region INode Members

        public override string Href(RequestHandlerBase handler)
        {
            if (label == null)
            {
                return handler.GetLocalPath(path);
            }
            else
            {
                return handler.GetLocalPath("/!svn/bln/" + label);
            }
        }

        protected override string GetProperty_Core(RequestHandlerBase handler, string propertyName)
        {
            switch (propertyName)
            {
                case "checked-in":
                    return GetCheckedIn(handler);
                case "baseline-collection":
                    return GetBaselineCollection(handler);
                case "version-name":
                    return GetVersionName();
                case "auto-version":
                    return "";
                default:
                    return null;
            }
        }

        #endregion

        private string GetCheckedIn(RequestHandlerBase handler)
        {
            int maxVersion = sourceControlProvider.GetLatestVersion();
            return "<lp1:checked-in><D:href>" + handler.GetLocalPath( "/!svn/bln/" + maxVersion) + "</D:href></lp1:checked-in>";
        }

        private string GetBaselineCollection(RequestHandlerBase handler)
        {
            return "<lp1:baseline-collection><D:href>" + handler.GetLocalPath("/!svn/bc/" + label) + "/</D:href></lp1:baseline-collection>";
        }

        private string GetVersionName()
        {
            return "<lp1:version-name>" + label + "</lp1:version-name>";
        }
    }
}
