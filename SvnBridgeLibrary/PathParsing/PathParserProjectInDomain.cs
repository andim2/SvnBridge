using SvnBridge.Interfaces;
using SvnBridge.SourceControl;
using System.Net;
using System;
using System.Collections.Generic;
using CodePlex.TfsLibrary.ObjectModel; // SourceItem
using CodePlex.TfsLibrary.RepositoryWebSvc; // DeletedState, ItemType, RecursionType, VersionSpec

namespace SvnBridge.PathParsing
{
    public class PathParserProjectInDomain : PathParserSingleServerWithProjectInPath
    {
        private static Dictionary<string, ProjectLocationInformation> projectLocations = new Dictionary<string, ProjectLocationInformation>();

        private readonly TFSSourceControlService sourceControlService;

        public static void ResetCache()
        {
            projectLocations = new Dictionary<string, ProjectLocationInformation>();
        }

        public PathParserProjectInDomain(string servers, TFSSourceControlService sourceControlService)
        {
            foreach (string singleServerUrl in servers.Split(','))
            {
                ValidateServerUri(singleServerUrl);
            }
            this.server = servers;
            this.sourceControlService = sourceControlService;
        }

        public override string GetServerUrl(IHttpRequest request, ICredentials credentials)
        {
            string projectName = request.Headers["Host"].Split('.')[0];
            return GetProjectLocation(credentials, projectName).ServerUrl;
        }

        public override string GetProjectName(IHttpRequest request)
        {
            string projectName = request.Headers["Host"].Split('.')[0];
            return projectLocations[projectName.ToLower()].RemoteProjectName;
        }

        private ProjectLocationInformation GetProjectLocation(ICredentials credentials, string projectName)
        {
            projectName = projectName.ToLower();
            if (!projectLocations.ContainsKey(projectName))
            {
                string[] servers = this.server.Split(',');
                foreach (string server in servers)
                {
                    ICredentials credentialsForServer = CredentialsHelper.GetCredentialsForServer(server, credentials);
                    SourceItem[] items = sourceControlService.QueryItems(server, credentialsForServer, Constants.ServerRootPath + projectName, RecursionType.None, VersionSpec.Latest, DeletedState.NonDeleted, ItemType.Folder, false, 0);

                    bool haveFoundItem = (items != null && items.Length > 0);
                    bool isProjectFoundOnThisServer = (haveFoundItem);
                    if (isProjectFoundOnThisServer)
                    {
                        string remoteProjectName = items[0].RemoteName.Substring(Constants.ServerRootPath.Length);
                        projectLocations[projectName] = new ProjectLocationInformation(remoteProjectName, server);
                        // Hmm... to break; or not to break;?
                    }
                }
            }
            // SVNBRIDGE_DOC_REF_EXCEPTIONS (we can expect to find an entry...).
            try
            {
                return projectLocations[projectName];
            }
            catch
            {
                throw new InvalidOperationException("Could not find project '" + projectName + "' in: " + this.server);
            }
        }
    }
}
