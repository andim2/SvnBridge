using System.Net;

namespace CodePlex.TfsLibrary.RegistrationWebSvc
{
    public interface IRegistrationWebSvcFactory
    {
        IRegistrationWebSvc Create(string tfsUrl,
                                   ICredentials credentials);
    }
}