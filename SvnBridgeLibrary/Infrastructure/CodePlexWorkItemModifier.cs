using System.Net;
using SvnBridge.CodePlexWebServices;
using SvnBridge.Net;
using SvnBridge.SourceControl;

namespace SvnBridge.Infrastructure
{
    /// <summary>
    /// This can only be used within the CodePlex hosted environment
    /// as it requires a webservice not available externally.
    /// </summary>
    public class CodePlexWorkItemModifier : IWorkItemModifier
    {
        private readonly WorkItemService workItemService;

        public CodePlexWorkItemModifier(WorkItemService workItemService)
        {
            this.workItemService = workItemService;
            this.workItemService.Url = Configuration.CodePlexWorkItemUrl;
        }

        public void Associate(int workItemId, int changeSetId)
        {
            // This is currently not used for CodePlex
        }

        public void SetWorkItemFixed(int workItemId, int changeSetId)
        {
            var projectName = (string) RequestCache.Items["projectName"];
            if (string.IsNullOrEmpty(projectName))
                return;

            var credentials = (NetworkCredential)RequestCache.Items["credentials"];
            if (credentials == null)
                return;

            if (projectName.StartsWith("/"))
                projectName = projectName.Substring(1);

            workItemService.Credentials = CredentialsHelper.DefaultCredentials;
            workItemService.MarkWorkItemAsFixed(projectName, credentials.UserName, workItemId, changeSetId);
        }
    }
}