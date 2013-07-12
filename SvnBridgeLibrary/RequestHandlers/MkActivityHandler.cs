using System.Text;
using SvnBridge.Interfaces;
using SvnBridge.SourceControl;
using SvnBridge.Utility; // Helper.Decode()

namespace SvnBridge.Handlers
{
    public class MkActivityHandler : RequestHandlerBase
    {
        protected override void Handle(
            IHttpContext context,
            TFSSourceControlProvider sourceControlProvider)
        {
            IHttpRequest request = context.Request;
            IHttpResponse response = context.Response;

            string requestPath = GetPath(request);
            string activityPath = Helper.Decode(requestPath);
            string activityId = requestPath.Substring(10);
            sourceControlProvider.MakeActivity(activityId);

            SetResponseSettings(response, "text/html", Encoding.UTF8, 201);
            response.AppendHeader("Cache-Control", "no-cache");
            response.AppendHeader("Location", "http://" + request.Headers["Host"] + activityPath);
            SetResponseHeader_X_Pad_avoid_browser_bug(
                response);

            string responseContent = GetResourceCreatedResponse(
                WebDAVResourceType.Activity,
                activityPath,
                request);

            WriteToResponse(response, responseContent);
        }
    }
}
