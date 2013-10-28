using System;
using System.Net;
using SvnBridge.Interfaces;
using SvnBridge.Net;

namespace SvnBridge.PathParsing
{
	public abstract class BasePathParser : IPathParser
	{
        public abstract string GetServerUrl(IHttpRequest request, ICredentials credentials);
		public abstract string GetLocalPath(IHttpRequest request);
		public abstract string GetLocalPath(IHttpRequest request, string url);
		public abstract string GetProjectName(IHttpRequest request);
		public abstract string GetApplicationPath(IHttpRequest request);
		public abstract string GetPathFromDestination(string href);

		public string GetActivityId(string href)
		{
			int activityIdStart = href.LastIndexOf('/') + 1;
			return href.Substring(activityIdStart);
		}

		public string GetActivityIdFromDestination(string href)
		{
		    var parts = href.Split('/');
		    return href.Contains("/$") ? parts[9] : parts[6];
		}

	    public string ToApplicationPath(IHttpRequest request, string href)
		{
			string applicationPath = GetApplicationPath(request);
			if (applicationPath.EndsWith("/"))
				return applicationPath.Substring(0, applicationPath.Length - 1) + href;
			return applicationPath + href;
		}
	}
}