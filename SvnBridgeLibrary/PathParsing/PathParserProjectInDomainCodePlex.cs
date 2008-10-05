using System;
using System.Collections.Generic;
using System.Text;
using SvnBridge.Interfaces;
using System.Net;
using SvnBridge.CodePlexWebServices;
using SvnBridge.SourceControl;

namespace SvnBridge.PathParsing
{
    public class PathParserProjectInDomainCodePlex : PathParserSingleServerWithProjectInPath
    {
        private static Dictionary<string, ProjectLocationInformation> projectLocations = new Dictionary<string, ProjectLocationInformation>();

        public override string GetServerUrl(IHttpRequest request, ICredentials credentials)
        {
            return GetProjectLocation(request).ServerUrl;
        }

        public override string GetProjectName(IHttpRequest request)
        {
            return GetProjectLocation(request).RemoteProjectName;
        }

        private ProjectLocationInformation GetProjectLocation(IHttpRequest request)
        {
            string projectName = request.Headers["Host"].Split('.')[0];
            projectName = projectName.ToLower();
            if (!projectLocations.ContainsKey(projectName))
            {
                ProjectInfoService service = new ProjectInfoService();
                ProjectTfsInfo info = service.GetTfsInfoForProject(projectName);
                string tfsServerUrl = info.TfsServerUrl.Substring(0, info.TfsServerUrl.Length - 5);
                string tfsProjectName = info.ProjectPrefix.Substring(2, info.ProjectPrefix.Length - 3);
                projectLocations[projectName] = new ProjectLocationInformation(tfsProjectName, tfsServerUrl);
            }
            return projectLocations[projectName];
        }
    }
}
