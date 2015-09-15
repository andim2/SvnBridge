using System;
using System.Net;
using SvnBridge.Interfaces;

namespace SvnBridge.PathParsing
{
	public class PathParserSingleServerWithProjectInPath : BasePathParser
	{
		protected string server;

        protected PathParserSingleServerWithProjectInPath() { }

        public PathParserSingleServerWithProjectInPath(string server)
        {
            Uri ignored;
            if (Uri.TryCreate(server, UriKind.Absolute, out ignored) == false)
                throw new InvalidOperationException("The url '" + server + "' is not a valid url");

            this.server = server;
        }

	    public override string GetServerUrl(IHttpRequest request, ICredentials credentials)
		{
            return server;
		}

		public override string GetLocalPath(IHttpRequest request)
		{
			return request.LocalPath;
		}

        public override string GetLocalPath(IHttpRequest request, string url)
        {
            // If a relative url has been provided, make it an absolute URL so we can still
            // get the same unescaped path from it.
            if (url.StartsWith("/")) {
                url = "http://FakeHost" + url;
            }

            Uri urlAsUri = new Uri(url);
            string path = urlAsUri.GetComponents(UriComponents.Path, UriFormat.SafeUnescaped);
            path = "/" + path;
            if (path.StartsWith(GetApplicationPath(request), StringComparison.InvariantCultureIgnoreCase))
                path = path.Substring(GetApplicationPath(request).Length);

            if (!path.StartsWith("/"))
                path = "/" + path;

            return path;
        }
		public override string GetProjectName(IHttpRequest request)
		{
			return null;
		}

		public override string GetApplicationPath(IHttpRequest request)
		{
			return request.ApplicationPath;
		}

		public override string GetPathFromDestination(string href)
		{
            return href.Substring(href.IndexOf("/!svn/wrk/"));
		}
	}
}
