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

            bool fileDeleted = Delete(sourceControlProvider, requestPath);

            if (fileDeleted)
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
            if (requestPath.StartsWith("/!svn/act/"))
            {
                string activityId = requestPath.Substring(10);
                sourceControlProvider.DeleteActivity(activityId);
            }
            else if (requestPath.StartsWith("/!svn/wrk/"))
            {
                const int startIndex = 10;
                string activityId = requestPath.Substring(startIndex, requestPath.IndexOf('/', startIndex) - startIndex);
                string filePath = requestPath.Substring(requestPath.IndexOf('/', startIndex));
                return sourceControlProvider.DeleteItem(activityId, Helper.Decode(filePath));
            }
            else
            {
                ReportUnsupportedSVNRequestPath(requestPath);
            }
            return true;
        }
    }
}
