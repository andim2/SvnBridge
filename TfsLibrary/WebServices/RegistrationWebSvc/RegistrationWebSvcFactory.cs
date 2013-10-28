using System.Net;

namespace CodePlex.TfsLibrary.RegistrationWebSvc
{
    public class RegistrationWebSvcFactory : IRegistrationWebSvcFactory
    {
        readonly int tfsTimeout;

        public RegistrationWebSvcFactory(int tfsTimeout)
        {
            this.tfsTimeout = tfsTimeout;
        }

        public IRegistrationWebSvc Create(string tfsUrl,
                                          ICredentials credentials)
        {
            Registration webSvc = new Registration(tfsUrl, credentials);
            webSvc.Timeout = this.tfsTimeout;
            webSvc.UserAgent = "TfsLibrary";
            return webSvc;
        }
    }
}