using SvnBridge.Interfaces;
using SvnBridge.Net;
using SvnBridge.SourceControl;
using System.Net;
using System;

namespace SvnBridge.PathParsing
{
	public class PathParserProjectInDomain : PathParserSingleServerWithProjectInPath
	{
        private ProjectInformationRepository projectInformationRepository;

	    public PathParserProjectInDomain(string servers, ProjectInformationRepository projectInformationRepository)
	    {
            foreach (string singleServerUrl in servers.Split(','))
            {
                Uri ignored;
                if (Uri.TryCreate(singleServerUrl, UriKind.Absolute, out ignored) == false)
                    throw new InvalidOperationException("The url '" + servers + "' is not a valid url");
            }
            this.server = servers;
            this.projectInformationRepository = projectInformationRepository;
        }

        public override string GetServerUrl(IHttpRequest request, ICredentials credentials)
        {
            string projectName = request.Headers["Host"].Split('.')[0];
            return projectInformationRepository.GetProjectLocation(credentials, projectName).ServerUrl;
        }

        public override string GetProjectName(IHttpRequest request)
		{
            string projectName = request.Headers["Host"].Split('.')[0];
            return projectInformationRepository.GetProjectLocation(projectName).RemoteProjectName;
		}
	}
}