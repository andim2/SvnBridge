using System.Text;
using SvnBridge.Interfaces;
using SvnBridge.Utility;
using SvnBridge.SourceControl;

namespace SvnBridge.Handlers
{
    public class DeleteHandler : RequestHandlerBase
    {
        protected override void Handle(
            IHttpContext context,
            TFSSourceControlProvider sourceControlProvider)
        {
            IHttpRequest request = context.Request;
            IHttpResponse response = context.Response;

            string requestPath = GetPath(request);

            bool isSuccessfullyDeleted = Delete(sourceControlProvider, requestPath);

            if (isSuccessfullyDeleted)
            {
                SetResponseSettings(response, "text/plain", Encoding.UTF8, 204);
            }
            else
            {
                SetResponseSettings(response, "text/html; charset=iso-8859-1", Encoding.UTF8, 404);

                string responseContent =
                    "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                    "<html><head>\n" +
                    "<title>404 Not Found</title>\n" +
                    "</head><body>\n" +
                    "<h1>Not Found</h1>\n" +
                    // FIXME: I really don't think that a *de*code of a *raw* (non-split) requestPath
                    // is even remotely correct...
                    "<p>The requested URL /" + Helper.Decode(requestPath) + " was not found on this server.</p>\n" +
                    "<hr>\n" +
                    "<address>" + GetServerIdentificationString_HostPort(request.Url.Host, request.Url.Port.ToString()) + "</address>\n" +
                    "</body></html>\n";

                WriteToResponse(response, responseContent);
            }
        }

        private static bool Delete(TFSSourceControlProvider sourceControlProvider,
                                   string requestPath)
        {
            // May do one of:
            // - DELETEing a resource (file)
            // - explicitly DELETEing (finalizing) entire transaction activity (MKACTIVITY)
            // Transaction deletion judged to probably be more frequent than resource DELETEs.
            if (requestPath.StartsWith("/!svn/act/"))
            {
                string activityId = requestPath.Substring(10);
                sourceControlProvider.DeleteActivity(activityId);
            }
            else if (requestPath.StartsWith("/!svn/wrk/"))
            {
                const int startIndex = 10;
                string activityId = requestPath.Substring(startIndex, requestPath.IndexOf('/', startIndex) - startIndex);
                string itemPathUndecoded = requestPath.Substring(requestPath.IndexOf('/', startIndex));
                string itemPath = Helper.Decode(itemPathUndecoded);
                return sourceControlProvider.DeleteItem(activityId, itemPath);
            }
            else
            {
                ReportUnsupportedSVNRequestPath(requestPath);
            }
            return true;
        }
    }
}
