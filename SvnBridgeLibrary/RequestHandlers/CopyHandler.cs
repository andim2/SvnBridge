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

            string activityId = PathParser.GetActivityIdFromDestination(request.Headers["Destination"]);

            string requestPath = GetPath(request);
            string serverPath = GetServerSidePath(requestPath);

            string destination = PathParser.GetPathFromDestination(Helper.DecodeC(request.Headers["Destination"]));
            string targetPath = destination.Substring(destination.IndexOf('/', 12));
            sourceControlProvider.CopyItem(activityId, serverPath, targetPath);

            response.AppendHeader("Location", Helper.DecodeC(request.Headers["Destination"]));

            string responseContent = GetResourceCreatedResponse(
                WebDAVResourceType.Copy,
                destination,
                request);

            WriteToResponse(response, responseContent);
        }
    }
}
