using System.IO;
using System.Net;
using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;

namespace CodePlex.TfsLibrary.ClientEngine
{
    public partial class TfsEngine
    {
        public void Checkout(string tfsUrl,
                             string serverPath,
                             string directory,
                             bool recursive,
                             VersionSpec version,
                             SourceItemCallback callback,
                             bool sortAscending,
                             int options)
        {
            Guard.ArgumentNotNullOrEmpty(tfsUrl, "tfsUrl");
            Guard.ArgumentNotNullOrEmpty(serverPath, "serverPath");
            Guard.ArgumentNotNullOrEmpty(directory, "directory");

            try
            {
                fileSystem.EnsurePath(directory);
            }
            catch (DirectoryNotFoundException)
            {
                _Callback(callback, directory, SourceItemResult.E_PathNotFound);
                return;
            }

            if (tfsState.IsFolderTracked(directory))
                _Callback(callback, directory, SourceItemResult.E_AlreadyUnderSourceControl);
            else
            {
                if (version == null)
                    version = VersionSpec.Latest;

                foreach (SourceItem serverItem in QueryItems(tfsUrl, serverPath, recursive ? RecursionType.Full : RecursionType.OneLevel, version, sortAscending, options))
                {
                    string localItemPath = TfsUtil.ServerPathToLocalPath(serverPath, directory, serverItem.RemoteName);

                    if (serverItem.ItemType == ItemType.File)
                        Checkout_File(serverItem, localItemPath, tfsUrl, callback);
                    else
                        Checkout_Folder(tfsUrl, serverItem, localItemPath, callback);
                }
            }
        }

        void Checkout_File(SourceItem serverItem,
                           string filename,
                           string tfsUrl,
                           SourceItemCallback callback)
        {
            if (fileSystem.FileExists(filename))
            {
                SourceItem callbackResult = SourceItem.FromLocalFile(serverItem.ItemId, SourceItemStatus.Unversioned, SourceItemStatus.Unversioned,
                                                                     filename, null, Constants.NullChangesetId, Constants.NullChangesetId, null);
                callbackResult.RemoteChangesetId = serverItem.RemoteChangesetId;
                callbackResult.RemoteItemStatus = serverItem.RemoteItemStatus;
                callbackResult.RemoteName = serverItem.RemoteName;

                _Callback(callback, callbackResult, SourceItemResult.E_WontClobberLocalItem);
            }
            else
            {
                try
                {
                    webTransferService.Download(serverItem.DownloadUrl, GetCredentials(tfsUrl), filename);
                    tfsState.TrackFile(filename, serverItem.ItemId, serverItem.RemoteChangesetId, SourceItemStatus.Unmodified);

                    _Callback(callback, tfsState.GetSourceItem(filename));
                }
                catch (WebException)
                {
                    _Callback(callback, filename, SourceItemResult.E_AccessDenied);
                }
            }
        }

        void Checkout_Folder(string tfsUrl,
                             SourceItem serverItem,
                             string directory,
                             SourceItemCallback callback)
        {
            fileSystem.EnsurePath(directory);

            tfsState.TrackFolder(tfsUrl, serverItem.RemoteName, directory, serverItem.ItemId, serverItem.RemoteChangesetId, SourceItemStatus.Unmodified);

            _Callback(callback, tfsState.GetSourceItem(directory));
        }
    }
}