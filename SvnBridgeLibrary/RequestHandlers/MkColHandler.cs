using System.IO; // StreamWriter
using System.Text;
using System.Text.RegularExpressions;
using SvnBridge.Exceptions;
using SvnBridge.Interfaces;
using SvnBridge.Utility;
using SvnBridge.SourceControl;

namespace SvnBridge.Handlers
{
    public class MkColHandler : RequestHandlerBase
    {
        protected override void Handle(
            IHttpContext context,
            TFSSourceControlProvider sourceControlProvider,
            StreamWriter output)
        {
            IHttpRequest request = context.Request;
            IHttpResponse response = context.Response;

            string requestPath = GetPath(request);
            string itemPath = Helper.Decode(requestPath);

            try
            {
                MakeCollection(requestPath, sourceControlProvider);

                SendCreatedResponse(request, response, itemPath, request.Url.Host, request.Url.Port.ToString(), output);
            }
            catch (FolderAlreadyExistsException)
            {
                SendFailureResponse(response, itemPath, request.Url.Host, request.Url.Port.ToString(), output);
            }
        }

        private static void MakeCollection(string requestPath, TFSSourceControlProvider sourceControlProvider)
        {
            if (!requestPath.StartsWith("//"))
            {
                requestPath = "/" + requestPath;
            }

            Match match = Regex.Match(requestPath, @"//!svn/wrk/([a-zA-Z0-9\-]+)/?");
            string folderPathElementUndecoded = requestPath.Substring(match.Groups[0].Value.Length - 1);
            string folderPathElement = Helper.Decode(folderPathElementUndecoded);
            string activityId = match.Groups[1].Value;
            sourceControlProvider.MakeCollection(activityId, folderPathElement);
        }

        private static void SendCreatedResponse(IHttpRequest request, IHttpResponse response, string itemPath, string server, string port, StreamWriter output)
        {
            SetResponseSettings(response, "text/html", Encoding.UTF8, 201);

            response.AppendHeader("Location", "http://" + request.Headers["Host"] + "/" + itemPath);

            string responseContent = GetResourceCreatedResponse(
                WebDAVResourceType.Collection,
                itemPath,
                server,
                port);

            output.Write(responseContent);
        }

        private static void SendFailureResponse(IHttpResponse response, string itemPath, string server, string port, StreamWriter output)
        {
            SetResponseSettings(response, "text/html; charset=iso-8859-1", Encoding.UTF8, 405);

            response.AppendHeader("Allow", "TRACE");

            string responseContent =
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>405 Method Not Allowed</title>\n" +
                "</head><body>\n" +
                "<h1>Method Not Allowed</h1>\n" +
                "<p>The requested method MKCOL is not allowed for the URL /" + itemPath + ".</p>\n" +
                "<hr>\n" +
                "<address>" + GetServerIdentificationString_HostPort(server, port) + "</address>\n" +
                "</body></html>\n";

            output.Write(responseContent);
        }
    }
}
