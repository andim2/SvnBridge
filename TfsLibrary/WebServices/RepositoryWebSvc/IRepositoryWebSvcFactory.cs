using System.Net;

namespace CodePlex.TfsLibrary.RepositoryWebSvc
{
    public interface IRepositoryWebSvcFactory
    {
        IRepositoryWebSvc Create(string tfsUrl,
                                 ICredentials credentials);
    }
}