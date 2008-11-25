using System.Collections.Generic;
using System.Net;
using System.Threading;
using SvnBridge.CodePlexWebServices;
using SvnBridge.Interfaces;
using SvnBridge.SourceControl;

namespace SvnBridge.PathParsing
{
    public class PathParserProjectInDomainCodePlex : PathParserSingleServerWithProjectInPath
    {
        private static Dictionary<string, ProjectLocationInformation> projectLocations = new Dictionary<string, ProjectLocationInformation>();
        private static ReaderWriterLockSlim projectLocationsLock = new ReaderWriterLockSlim();

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

            projectLocationsLock.EnterUpgradeableReadLock();
            try
            {
                if (!projectLocations.ContainsKey(projectName))
                {
                    projectLocationsLock.EnterWriteLock();
                    try
                    {
                        var service = new ProjectInfoService();
                        ProjectTfsInfo info = service.GetTfsInfoForProject(projectName);
                        string tfsServerUrl = info.TfsServerUrl.Substring(0, info.TfsServerUrl.Length - 5);
                        string tfsProjectName = info.ProjectPrefix.Substring(2, info.ProjectPrefix.Length - 3);
                        projectLocations[projectName] = new ProjectLocationInformation(tfsProjectName, tfsServerUrl);
                    }
                    finally
                    {
                        projectLocationsLock.ExitWriteLock();
                    }
                }

                return projectLocations[projectName];
            }
            finally
            {
                projectLocationsLock.ExitUpgradeableReadLock();
            }
        }
    }
}