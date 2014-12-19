using System.Net;
using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;

namespace CodePlex.TfsLibrary.ClientEngine
{
    public partial class TfsEngine
    {
        protected SourceItem[] QueryItems(string tfsUrl,
                                          string serverPath,
                                          RecursionType recursion,
                                          VersionSpec version)
        {
            ICredentials credentials = GetCredentials(tfsUrl);

            while (true)
            {
                try
                {
                    return sourceControlService.QueryItems(tfsUrl, credentials, serverPath, recursion, version, DeletedState.NonDeleted, ItemType.Any);
                }
                catch (NetworkAccessDeniedException)
                {
                    if (credentialsCallback == null)
                        throw;

                    credentials = GetCredentials(tfsUrl, true);

                    if (credentials == null)
                        throw;
                }
            }
        }
    }
}