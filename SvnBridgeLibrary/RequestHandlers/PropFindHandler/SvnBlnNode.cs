using SvnBridge.Handlers; // RequestHandlerBase

namespace SvnBridge.Nodes
{
	public sealed class SvnBlnNode : NodeBase
	{
		private string path;
		private int version;

		public SvnBlnNode(string path,
						  int version)
		{
			this.path = path;
			this.version = version;
		}

		#region INode Members

		public override string Href(RequestHandlerBase handler)
		{
			return handler.GetLocalPath(path);
		}

		protected override string GetProperty_Core(RequestHandlerBase handler, string propertyName)
		{
			switch (propertyName)
			{
				case "baseline-collection":
					return GetBaselineCollection(handler);
				case "version-name":
					return GetVersionName();
				default:
					return null;
			}
		}

		#endregion

		private string GetBaselineCollection(RequestHandlerBase handler)
        {
            return
                "<lp1:baseline-collection><D:href>"+handler.GetLocalPath("/!svn/bc/" + version)+
                "/</D:href></lp1:baseline-collection>";
        }

		private string GetVersionName()
		{
			return "<lp1:version-name>" + version.ToString() + "</lp1:version-name>";
		}
	}
}
