using SvnBridge.Interfaces;
using SvnBridge.Net;
using SvnBridge.SourceControl;
using System.Net;
using System;
using SvnBridge.Infrastructure;
using System.Collections.Generic;
using CodePlex.TfsLibrary.ObjectModel;

namespace SvnBridge.PathParsing
{
	public class PathParserProjectInDomain : PathParserSingleServerWithProjectInPath
	{
        private static Dictionary<string, ProjectLocationInformation> projectLocations = new Dictionary<string, ProjectLocationInformation>();

        private readonly MetaDataRepositoryFactory metaDataRepositoryFactory;

        public PathParserProjectInDomain(string servers, MetaDataRepositoryFactory metaDataRepositoryFactory)
	    {
            foreach (string singleServerUrl in servers.Split(','))
            {
                Uri ignored;
                if (Uri.TryCreate(singleServerUrl, UriKind.Absolute, out ignored) == false)
                    throw new InvalidOperationException("The url '" + servers + "' is not a valid url");
            }
            this.server = servers;
            this.metaDataRepositoryFactory = metaDataRepositoryFactory;
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
                    int revision = metaDataRepositoryFactory.GetLatestRevision(server, credentialsForServer);
                    SourceItem[] items = metaDataRepositoryFactory
                            .Create(credentialsForServer, server, Constants.ServerRootPath + projectName)
                                .QueryItems(revision, "", Recursion.None);

                    if (items != null && items.Length > 0)
                    {
                        string remoteProjectName = items[0].RemoteName.Substring(Constants.ServerRootPath.Length);
                        projectLocations[projectName] = new ProjectLocationInformation(remoteProjectName, server);
                    }
                }
                if (!projectLocations.ContainsKey(projectName))
                    throw new InvalidOperationException("Could not find project '" + projectName + "' in: " + this.server);
            }
            return projectLocations[projectName];
        }
    }
}