using System.Text;
using SvnBridge.Interfaces;
using SvnBridge.Utility;
using SvnBridge.SourceControl;

namespace SvnBridge.Handlers
{
    public class CopyHandler : RequestHandlerBase
    {
        protected override void Handle(
            IHttpContext context,
            TFSSourceControlProvider sourceControlProvider)
        {
            IHttpRequest request = context.Request;
            IHttpResponse response = context.Response;

            SetResponseSettings(response, "text/html", Encoding.UTF8, 201);

            string destinationHeader = request.Headers["Destination"];
            string activityId = PathParser.GetActivityIdFromDestination(destinationHeader);

            string requestPath = GetPath(request);

            int itemVersion = TFSSourceControlProvider.LATEST_VERSION;
            if (requestPath.StartsWith("/!svn/bc/"))
            {
                string[] parts = requestPath.Split('/');
                if (parts.Length >= 3)
                    int.TryParse(parts[3], out itemVersion);
            }

            string serverPath = GetServerSidePath(requestPath);

            string destinationHeaderDecoded = Helper.DecodeC(destinationHeader);
            string destination = PathParser.GetPathFromDestination(destinationHeaderDecoded);
            string targetPath = destination.Substring(destination.IndexOf('/', 12));
            sourceControlProvider.CopyItem(activityId, itemVersion, serverPath, targetPath);

            response.AppendHeader("Location", destinationHeaderDecoded);

            string responseContent = GetResourceCreatedResponse(
                WebDAVResourceType.Copy,
                destination,
                request);

            WriteToResponse(response, responseContent);
        }
    }
}
