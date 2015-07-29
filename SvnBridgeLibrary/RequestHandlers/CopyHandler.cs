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
            string serverPath = GetServerSidePath(requestPath);

            string destinationHeaderDecoded = Helper.DecodeC(destinationHeader);
            string destination = PathParser.GetPathFromDestination(destinationHeaderDecoded);
            string targetPath = destination.Substring(destination.IndexOf('/', 12));
            sourceControlProvider.CopyItem(activityId, serverPath, targetPath);

            response.AppendHeader("Location", destinationHeaderDecoded);

            string responseContent = GetResourceCreatedResponse(
                WebDAVResourceType.Copy,
                destination,
                request);

            WriteToResponse(response, responseContent);
        }
    }
}
