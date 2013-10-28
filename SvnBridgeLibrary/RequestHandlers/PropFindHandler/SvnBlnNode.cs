using System;
using System.Xml;
using SvnBridge.Handlers;

namespace SvnBridge.Nodes
{
	public class SvnBlnNode : INode
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

		public string Href(RequestHandlerBase handler)
		{
			return handler.GetLocalPath(path);
		}

		public string GetProperty(RequestHandlerBase handler, XmlElement property)
		{
			switch (property.LocalName)
			{
				case "baseline-collection":
					return GetBaselineCollection(handler);
				case "version-name":
					return GetVersionName(property);
				default:
					throw new Exception("Property not found: " + property.LocalName);
			}
		}

		#endregion

		private string GetBaselineCollection(RequestHandlerBase handler)
        {
            return
                "<lp1:baseline-collection><D:href>"+handler.GetLocalPath("/!svn/bc/" + version)+
                "/</D:href></lp1:baseline-collection>";
        }

		private string GetVersionName(XmlElement property)
		{
			return "<lp1:version-name>" + version.ToString() + "</lp1:version-name>";
		}
	}
}