using System.Text;
using SvnBridge.Interfaces;
using SvnBridge.Net;
using SvnBridge.Utility;
using SvnBridge.SourceControl;

namespace SvnBridge.Handlers
{
    public class DeleteHandler : RequestHandlerBase
    {
        protected override void Handle(IHttpContext context,
                                       TFSSourceControlProvider sourceControlProvider)
        {
            IHttpRequest request = context.Request;
            IHttpResponse response = context.Response;

            string path = GetPath(request);

            bool fileDeleted = Delete(sourceControlProvider, path);

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
                    "<p>The requested URL /" + Helper.Decode(path) + " was not found on this server.</p>\n" +
                    "<hr>\n" +
                    "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at " + request.Url.Host + " Port " +
                    request.Url.Port + "</address>\n" +
                    "</body></html>\n";

                WriteToResponse(response, responseContent);
            }
        }

        private bool Delete(TFSSourceControlProvider sourceControlProvider,
                            string path)
        {
            if (path.StartsWith("/!svn/act/"))
            {
                string activityId = path.Substring(10);
                sourceControlProvider.DeleteActivity(activityId);
            }
            else if (path.StartsWith("/!svn/wrk/"))
            {
                string activityId = path.Substring(10, path.IndexOf('/', 10) - 10);
                string filePath = path.Substring(path.IndexOf('/', 10));
                return sourceControlProvider.DeleteItem(activityId, Helper.Decode(filePath));
            }
            return true;
        }
    }
}
