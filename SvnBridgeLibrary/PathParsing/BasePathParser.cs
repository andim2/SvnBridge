using System.Net; // ICredentials
using SvnBridge.Interfaces; // IHttpRequest, IPathParser

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

        /// <summary>
        /// Retrieves activity id from "Destination:" protocol header.
        /// </summary>
        /// <param name="href">The location (grabbed from Destination: header) to be parsed</param>
        /// <returns>WebDAV activity ID</returns>
		public string GetActivityIdFromDestination(string href)
		{
                    // This function is faaar from precise
                    // (perhaps both from its definition and content),
                    // but at least it's a tad bit better now.
                    // Since this SVN URI parsing algo can easily end up wrong,
                    // better list some sample real-life Destination: values:
                    // http://<IP_ADDR>/svn/somehome/!svn/wrk/<activityID_UUID>/eclipse/branches/Staging/java/myhome/user/foo
                    // https://<HOST>/svn/test/!svn/wrk/<activityID_UUID>/test/branches/<name>
                    // http://<HOST>/testing/<user>/$svn/wrk/<activityID_UUID>/somedir/someproj

		    //var parts = href.Split('/');
		    //return href.Contains("/$") ? parts[9] : parts[6];

                    // This is for "/$svn" URIs, right?
                    if (href.Contains("/$"))
                    {
                      var elements = href.Split('/');
                      return elements[9];
                    }
                    else
                    {
                      // Terminus technicus for a "!svn"-only URI is "root collection".
                      // We're interested in those parts which follow a _fixed_ root position.
                      string remainderFromRootCollection = href.Substring(href.IndexOf("/!svn"));
                      var elements = remainderFromRootCollection.Split('/');
                      // Skip the !svn/wrk/ elements ([empty], !svn, wrk),
                      // then the next one is the activity ID:
                      return elements[3];
                    }
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
