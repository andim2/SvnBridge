using System;
using System.Net;
using CodePlex.TfsLibrary.ObjectModel;

namespace CodePlex.TfsLibrary.RepositoryWebSvc
{
    public partial class Repository : IRepositoryWebSvc
    {
        public Repository(string repositoryWebServiceUrl,
                          ICredentials credentials)
        {
            Url = repositoryWebServiceUrl;
            Credentials = credentials;
        }

        protected override WebRequest GetWebRequest(Uri uri)
        {
            return TfsUtil.SetupWebRequest(base.GetWebRequest(uri), Credentials);
        }
    }
}