using System;
using System.Net;
using SvnBridge.Net;

namespace SvnBridge.Interfaces
{
	public interface IPathParser
	{
		/// <remarks>
		/// For GetServerUrl() implementation requirements,
		/// see its unit tests.
		/// </remarks>
        string GetServerUrl(IHttpRequest request, ICredentials credentials);
		string GetLocalPath(IHttpRequest request);
		string GetLocalPath(IHttpRequest request, string url);
		string GetProjectName(IHttpRequest request);
		string GetApplicationPath(IHttpRequest request);
		string GetActivityId(string href);
		string GetActivityIdFromDestination(string href);
		string ToApplicationPath(IHttpRequest request, string href);
		string GetPathFromDestination(string href);
	}
}
