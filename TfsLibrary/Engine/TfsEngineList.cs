using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;

namespace CodePlex.TfsLibrary.ClientEngine
{
    public partial class TfsEngine
    {
        public void List(string tfsUrl,
                         string serverPath,
                         bool recursive,
                         VersionSpec version,
                         SourceItemCallback callback,
                         bool sortAscending,
                         int options)
        {
            Guard.ArgumentNotNullOrEmpty(tfsUrl, "tfsUrl");
            Guard.ArgumentNotNullOrEmpty(serverPath, "serverPath");
            Guard.ArgumentNotNull(version, "version");
            Guard.ArgumentNotNull(callback, "callback");

            foreach (SourceItem sourceItem in QueryItems(tfsUrl, serverPath, recursive ? RecursionType.Full : RecursionType.OneLevel, version, sortAscending, options))
                _Callback(callback, sourceItem);
        }
    }
}