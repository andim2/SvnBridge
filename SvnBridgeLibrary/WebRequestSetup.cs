using System;
using System.Net;

namespace SvnBridge
{
	public static class WebRequestSetup
	{
		public static void OnWebRequest(WebRequest request)
		{
			HttpWebRequest httpWebRequest = request as HttpWebRequest;
			if (httpWebRequest!=null)
			{
                httpWebRequest.UnsafeAuthenticatedConnectionSharing = false;
			}
		}
	}
}