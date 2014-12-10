using System.Net;
using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;

namespace CodePlex.TfsLibrary.ClientEngine
{
    public partial class TfsEngine
    {
        static void _Callback(LogCallback callback,
                              string localPath,
                              SourceItemResult result)
        {
            if (callback != null)
                callback(new LogItem(localPath, null, null), result);
        }

        static void _Callback(LogCallback callback,
                              LogItem logItem,
                              SourceItemResult result)
        {
            if (callback != null)
                callback(logItem, result);
        }

        public void Log(string localPath,
                        VersionSpec versionFrom,
                        VersionSpec versionTo,
                        int maxCount,
                        LogCallback callback)
        {
            Guard.ArgumentNotNull(callback, "callback");
            Guard.ArgumentNotNullOrEmpty(localPath, "localPath");
            Guard.ArgumentNotNull(versionFrom, "versionFrom");
            Guard.ArgumentNotNull(versionTo, "versionTo");

            if (fileSystem.DirectoryExists(localPath) || tfsState.IsFolderTracked(localPath))
                Log_Folder(localPath, versionFrom, versionTo, maxCount, callback, false);
            else if (fileSystem.FileExists(localPath) || tfsState.IsFileTracked(localPath))
                Log_File(localPath, versionFrom, versionTo, maxCount, callback, false);
            else
                _Callback(callback, localPath, SourceItemResult.E_PathNotFound);
        }

        void Log_File(string filename,
                      VersionSpec versionFrom,
                      VersionSpec versionTo,
                      int maxCount,
                      LogCallback callback,
                      bool sortAscending)
        {
            if (!IsParentDirectoryTracked(filename))
                _Callback(callback, filename, SourceItemResult.E_NotInAWorkingFolder);
            else if (!tfsState.IsFileTracked(filename))
                _Callback(callback, filename, SourceItemResult.E_NotUnderSourceControl);
            else
            {
                string directory = fileSystem.GetDirectoryName(filename);
                TfsFolderInfo info = tfsState.GetFolderInfo(directory);
                LogItem logItem = null;

                ICredentials credentials = GetCredentials(info.TfsUrl);

                while (logItem == null)
                {
                    try
                    {
                        logItem = sourceControlService.QueryLog(info.TfsUrl, credentials,
                                                                TfsUtil.LocalPathToServerPath(info.ServerPath,
                                                                                              directory,
                                                                                              filename,
                                                                                              ItemType.Folder),
                                                                versionFrom,
                                                                versionTo,
                                                                RecursionType.None,
                                                                maxCount,
                                                                sortAscending);
                    }
                    catch (NetworkAccessDeniedException)
                    {
                        if (credentialsCallback == null)
                            throw;

                        credentials = GetCredentials(info.TfsUrl, true);

                        if (credentials == null)
                            throw;
                    }
                }

                _Callback(callback, logItem, SourceItemResult.S_Ok);
            }
        }

        void Log_Folder(string directory,
                        VersionSpec versionFrom,
                        VersionSpec versionTo,
                        int maxCount,
                        LogCallback callback,
                        bool sortAscending)
        {
            if (!tfsState.IsFolderTracked(directory))
                _Callback(callback, directory, SourceItemResult.E_NotUnderSourceControl);
            else
            {
                TfsFolderInfo info = tfsState.GetFolderInfo(directory);
                LogItem logItem = null;

                ICredentials credentials = GetCredentials(info.TfsUrl);

                while (logItem == null)
                {
                    try
                    {
                        logItem = sourceControlService.QueryLog(info.TfsUrl, credentials,
                                                                info.ServerPath,
                                                                versionFrom,
                                                                versionTo,
                                                                RecursionType.Full,
                                                                maxCount,
                                                                sortAscending);
                    }
                    catch (NetworkAccessDeniedException)
                    {
                        if (credentialCache == null)
                            throw;

                        credentials = GetCredentials(info.TfsUrl, true);

                        if (credentials == null)
                            throw;
                    }
                }

                _Callback(callback, logItem, SourceItemResult.S_Ok);
            }
        }

        // Callback helpers
    }
}