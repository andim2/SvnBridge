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
		/// IMPORTANT PERFORMANCE NOTE:
		/// for those properties which are global rather than per-request,
		/// it probably is very preferable
		/// to have them configured *once* (on app startup, and globally)
		/// at ServicePointManager,
		/// rather than setting them in a per-request fine-grained manner
		/// in this massive hotpath.
		/// Examples:
		/// - ServicePoint[Manager].SetTcpKeepAlive()
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
                // [This .PreAuthenticate optimization flag is relevant only
                // for non-persistent-connection case i.e. non-HTTP-Keep-Alive]
                //httpWebRequest.PreAuthenticate = true;

                // Note that TfsLibrary already sets
                // .UseNagleAlgorithm = false;
                // , otherwise we likely ought to do it here, too,
                // since quite likely we're predominantly sending
                // *small* request packets
                // (where it's better to have Nagle disabled).

                // Not sure yet whether
                // we can (strength of our support infrastructure?)
                // and should (remote side always compatible?)
                // enable this.
                //httpWebRequest.Pipelined = true; // said to only be used if .KeepAlive true, too.
			}
		}
	}
}
