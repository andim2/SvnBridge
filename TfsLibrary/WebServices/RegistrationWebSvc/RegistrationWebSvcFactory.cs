using System.Net;

namespace CodePlex.TfsLibrary.RegistrationWebSvc
{
    public class RegistrationWebSvcFactory : IRegistrationWebSvcFactory
    {
        public IRegistrationWebSvc Create(string tfsUrl,
                                          ICredentials credentials)
        {
            Registration webSvc = new Registration(tfsUrl, credentials);
            webSvc.Timeout = 15 * 60 * 1000;
            webSvc.UserAgent = "CodePlexClient";
            return webSvc;
        }
    }
}