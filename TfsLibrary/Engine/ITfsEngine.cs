using System.Net;
using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;

namespace CodePlex.TfsLibrary.ClientEngine
{
    public delegate ICredentials CredentialsCallback(ICredentials oldCredentials,
                                                     string tfsUrl);

    public interface ITfsEngine
    {
        bool AttemptAutoMerge { get; set; }

        CredentialsCallback CredentialsCallback { get; set; }

        string IgnoreFile { get; set; }

        void Add(string localPath,
                 bool recursive,
                 SourceItemCallback callback);

        void Checkout(string tfsUrl,
                      string serverPath,
                      string directory,
                      bool recursive,
                      VersionSpec version,
                      SourceItemCallback callback,
                      bool sortAscending,
                      int options);

        int Commit(string directory,
                   string message,
                   SourceItemCallback callback);

        void Delete(string localPath,
                    bool force,
                    SourceItemCallback callback);

        void Diff(string localPath,
                  bool recursive,
                  DiffCallback callback);

        TfsFolderInfo GetFolderInfo(string directory);

        SourceItem GetSourceItem(string localPath);

        SourceItem[] GetSourceItems(string directory);

        bool IsFileTracked(string filename);

        bool IsFolderTracked(string directory);

        void List(string tfsUrl,
                  string serverPath,
                  bool recursive,
                  VersionSpec version,
                  SourceItemCallback callback,
                  bool sortAscending,
                  int options);

        void Log(string localPath,
                 VersionSpec versionFrom,
                 VersionSpec versionTo,
                 int maxCount,
                 LogCallback callback);

        void Resolve(string localPath,
                     bool recursive,
                     SourceItemCallback callback);

        void Revert(string localPath,
                    bool recursive,
                    SourceItemCallback callback);

        void Status(string localPath,
                    VersionSpec version,
                    bool recursive,
                    bool includeServer,
                    SourceItemCallback callback);

        void Syncup(string directory,
                    AddSourceItemCallback addItemCallback,
                    SyncupCallback syncupCallback);

        void Update(string localPath,
                    bool recursive,
                    VersionSpec version,
                    UpdateCallback callback);
    }
}