using System;
using System.Net;
using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using SvnBridge.Interfaces;
using System.Collections.Generic;
using SvnBridge.Infrastructure;
using SvnBridge.CodePlexWebServices;

namespace SvnBridge.SourceControl
{
    public class ProjectInformationRepository
    {
		private readonly MetaDataRepositoryFactory metaDataRepositoryFactory;
        private readonly string serverUrl;
        private static Dictionary<string, ProjectLocationInformation> projectLocations = new Dictionary<string, ProjectLocationInformation>();

        public ProjectInformationRepository(MetaDataRepositoryFactory metaDataRepositoryFactory, string serverUrl)
        {
			this.metaDataRepositoryFactory = metaDataRepositoryFactory;
            this.serverUrl = serverUrl;
        }

        public virtual ProjectLocationInformation GetProjectLocation(string projectName)
        {
            return projectLocations[projectName.ToLower()];
        }

        public virtual ProjectLocationInformation GetProjectLocation(ICredentials credentials, string projectName)
        {
            projectName = projectName.ToLower();
            if (!projectLocations.ContainsKey(projectName))
            {
                string[] servers = serverUrl.Split(',');
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
                    throw new InvalidOperationException("Could not find project '" + projectName + "' in: " + serverUrl);
            }
            return projectLocations[projectName];
        }
    }
}