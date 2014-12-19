using System.Net;
using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RegistrationWebSvc;

namespace CodePlex.TfsLibrary.RepositoryWebSvc
{
    public class RepositoryWebSvcFactory : IRepositoryWebSvcFactory
    {
        readonly IRegistrationWebSvcFactory registrationFactory;

        public RepositoryWebSvcFactory(IRegistrationWebSvcFactory registrationFactory)
        {
            this.registrationFactory = registrationFactory;
        }

        public IRepositoryWebSvc Create(string tfsUrl,
                                        ICredentials credentials)
        {
            RegistrationService registration = new RegistrationService(registrationFactory);
            string repositoryUrl = registration.GetServiceInterfaceUrl(tfsUrl, credentials, "VersionControl", "ISCCProvider");
            Repository webSvc = new Repository(repositoryUrl, credentials);
            webSvc.Timeout = 15 * 60 * 1000;
            webSvc.UserAgent = "CodePlexClient";
            return webSvc;
        }
    }
}