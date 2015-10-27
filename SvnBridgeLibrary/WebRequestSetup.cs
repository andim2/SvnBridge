using System.Net; // WebRequest

namespace SvnBridge
{
	public static class WebRequestSetup
	{
		/// <summary>
		/// VERY IMPORTANT callback hook from CodePlexClient library:
		/// it enables us to tweak various attributes of the WebRequest
		/// that's used for TFS web service communication, as needed.
		/// </summary>
		/// <remarks>
		/// Obviously, try to keep its implementation reasonably fast,
		/// since it is an absolutely extreme hotpath
		/// (will need to be processed on *every* single one
		/// of the many, many web requests within this app).
		/// </remarks>
		public static void OnWebRequest(WebRequest request)
		{
      // Note that an "as"-cast ("isinst" opcode)
      // is faster than "(casted)"-cast ("castclass" opcode), see
      // http://www.codeproject.com/Articles/8052/Type-casting-impact-over-execution-performance-in
			HttpWebRequest httpWebRequest = request as HttpWebRequest;
			if (httpWebRequest != null)
			{
                httpWebRequest.UnsafeAuthenticatedConnectionSharing = false;
			}
		}
	}
}
