using System;
using System.Net;
using SvnBridge.Interfaces;
using SvnBridge.Infrastructure;

namespace SvnBridge.PathParsing
{
	public class PathParserServerAndProjectInPath : BasePathParser
	{
		private readonly TfsUrlValidator urlValidator;

		public PathParserServerAndProjectInPath(TfsUrlValidator urlValidator)
		{
			this.urlValidator = urlValidator;
		}

		public override string GetServerUrl(IHttpRequest request, ICredentials credentials)
		{
			string url = GetUrlFromRequest(request.Url);

            if (urlValidator.IsValidTfsServerUrl("https://" + url + ":8443/tfs"))
                return "https://" + url + ":8443/tfs";
            if (urlValidator.IsValidTfsServerUrl("http://" + url + ":8080/tfs"))
                return "http://" + url + ":8080/tfs";
            if (urlValidator.IsValidTfsServerUrl("https://" + url))
				return "https://" + url;
            if (urlValidator.IsValidTfsServerUrl("http://" + url))
                return "http://" + url;
            if (urlValidator.IsValidTfsServerUrl("http://" + url + ":8080"))
                return "http://" + url + ":8080";
            if (urlValidator.IsValidTfsServerUrl("https://" + url + ":8443"))
                return "https://" + url + ":8443";
            return "http://" + url;
		}

		private string GetUrlFromRequest(Uri requestUrl)
		{
			string path = requestUrl.GetComponents(UriComponents.Path, UriFormat.SafeUnescaped);
            
            if (string.IsNullOrEmpty(path))
            {
                throw new InvalidOperationException("Could not find server url in the url (" +
                    requestUrl.AbsoluteUri + "). Not valid when using the RequestBasePathParser");
            }

		    int serverDelim = path.IndexOf("/$");
            if (serverDelim == -1)
			    serverDelim = path.IndexOf('/');
            
            return serverDelim == -1 ? path : path.Substring(0, serverDelim);
		}

		public override string GetLocalPath(IHttpRequest request)
		{
			return GetLocalPath(request.Url.AbsoluteUri);
		}

		public override string GetLocalPath(IHttpRequest request, string url)
		{
			return GetLocalPath(url);
		}

		private string GetLocalPath(string url)
		{
			Uri urlAsUri = new Uri(url);
			string path = urlAsUri.GetComponents(UriComponents.Path, UriFormat.SafeUnescaped);
			string urlFromRequest = GetUrlFromRequest(urlAsUri);
			string localPath = path.Substring(urlFromRequest.Length);
            if (localPath.StartsWith("/$"))
                localPath = localPath.Length > 2 ? localPath.Substring(2) : string.Empty;
			if (!localPath.StartsWith("/"))
				localPath = "/" + localPath;

			return localPath;
		}

		public override string GetProjectName(IHttpRequest request)
		{
			return null;
		}

		public override string GetApplicationPath(IHttpRequest request)
		{
			string url = GetUrlFromRequest(request.Url);
			string path = url + request.ApplicationPath;
			if (path.StartsWith("/") == false)
				path = '/' + path;
			if (path.EndsWith("/") == false)
				path = path + '/';
            if (path.Contains("/tfs"))
                path += "$/";
			return path;
		}

		public override string GetPathFromDestination(string href)
		{
			return GetLocalPath(href);
		}
	}
}