using System.Net;
using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RegistrationWebSvc;

namespace CodePlex.TfsLibrary.RepositoryWebSvc
{
    public class RepositoryWebSvcFactory : IRepositoryWebSvcFactory
    {
        readonly int tfsTimeout;
        readonly IRegistrationWebSvcFactory registrationFactory;

        public RepositoryWebSvcFactory(IRegistrationWebSvcFactory registrationFactory, int tfsTimeout)
        {
            this.registrationFactory = registrationFactory;
            this.tfsTimeout = tfsTimeout;
        }

        public IRepositoryWebSvc Create(string tfsUrl,
                                        ICredentials credentials)
        {
            RegistrationService registration = new RegistrationService(registrationFactory);
            string repositoryUrl = registration.GetServiceInterfaceUrl(tfsUrl, credentials, "VersionControl", "ISCCProvider");
            Repository webSvc = new Repository(repositoryUrl, credentials);
            webSvc.Timeout = this.tfsTimeout;
            webSvc.UserAgent = "TfsLibrary";
            return webSvc;
        }
    }
}