using System.Net;

namespace CodePlex.TfsLibrary.ObjectModel
{
    public interface IRegistrationService
    {
        string GetServiceInterfaceUrl(string tfsUrl,
                                      ICredentials credentials,
                                      string serviceType,
                                      string interfaceName);
    }
}