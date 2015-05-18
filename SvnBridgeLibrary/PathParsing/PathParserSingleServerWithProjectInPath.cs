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
            ValidateServerUri(server);

            this.server = server;
        }

	    public override string GetServerUrl(IHttpRequest request, ICredentials credentials)
		{
            return server;
		}

    protected static void ValidateServerUri(string server)
    {
        // SVNBRIDGE_DOC_REF_EXCEPTIONS: For locations where failure
        // is expected to be exceptional rather than the norm,
        // prefer using a more efficient
        // (due to cleanly failure path separated exception-only handling
        // rather than doing superfluous extra checks in normal code path)
        // action / exception / rethrow combo
        // rather than pre-invoking "uselessly checking" APIs such as
        // .TryGetValue() / Uri.TryCreate() / .ContainsKey().
        try
        {
            Uri ignored = new Uri(server, UriKind.Absolute);
        }
        catch (Exception e)
        {
            throw new InvalidOperationException("The url '" + server + "' is not a valid url", e);
        }
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
