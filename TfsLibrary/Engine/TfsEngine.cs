using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using CodePlex.TfsLibrary.Utility;

namespace CodePlex.TfsLibrary.ClientEngine
{
    public partial class TfsEngine : ITfsEngine
    {
        CredentialsCallback credentialsCallback;
        readonly IFileSystem fileSystem;
        readonly IIgnoreList ignoreList;
        readonly ISourceControlService sourceControlService;
        readonly TfsState tfsState;
        readonly IWebTransferService webTransferService;
        readonly TfsWorkspaceManager workspaceManager;
        readonly List<WorkspaceMetadata> workspaces = new List<WorkspaceMetadata>();

        public TfsEngine(ISourceControlService sourceControlService,
                         IWebTransferService webTransferService,
                         IFileSystem fileSystem,
                         IIgnoreList ignoreList,
                         IAppConfig appConfig)
        {
            this.sourceControlService = sourceControlService;
            this.webTransferService = webTransferService;
            this.fileSystem = fileSystem;
            this.ignoreList = ignoreList;

            if (appConfig != null)
            {
                AttemptAutoMerge = appConfig.AttemptAutoMerge;
                IgnoreFile = appConfig.IgnoreFile;
            }

            workspaceManager = new TfsWorkspaceManager(sourceControlService, fileSystem);
            tfsState = new TfsState(fileSystem);
        }

        public CredentialsCallback CredentialsCallback
        {
            get { return credentialsCallback; }
            set { credentialsCallback = value; }
        }

        public string IgnoreFile
        {
            get { return ignoreList.IgnoreFilename; }
            set { ignoreList.IgnoreFilename = value; }
        }

        internal TfsState TfsState
        {
            get { return tfsState; }
        }

        static void _Callback(SourceItemCallback callback,
                              SourceItem item)
        {
            _Callback(callback, item, SourceItemResult.S_Ok);
        }

        static void _Callback(SourceItemCallback callback,
                              SourceItem item,
                              SourceItemResult result)
        {
            if (callback != null)
                callback(item, result);
        }

        static void _Callback(SourceItemCallback callback,
                              string localPath,
                              SourceItemResult result)
        {
            if (callback != null)
                callback(SourceItem.FromLocalPath(localPath), result);
        }

        void CleanUpConflictArtifacts(SourceItem item)
        {
            fileSystem.DeleteFile(string.Format("{0}.r{1}", item.LocalName, item.LocalChangesetId));
            fileSystem.DeleteFile(string.Format("{0}.r{1}", item.LocalName, item.LocalConflictChangesetId));
            fileSystem.DeleteFile(string.Format("{0}.mine", item.LocalName));
        }

        public TfsFolderInfo GetFolderInfo(string directory)
        {
            return tfsState.GetFolderInfo(directory);
        }

        public SourceItem GetSourceItem(string localPath)
        {
            Guard.ArgumentNotNullOrEmpty(localPath, "localPath");

            return tfsState.GetSourceItem(localPath);
        }

        public SourceItem[] GetSourceItems(string directory)
        {
            return tfsState.GetSourceItems(directory);
        }

        public bool IsFileTracked(string filename)
        {
            return tfsState.IsFileTracked(filename);
        }

        public bool IsFolderTracked(string directory)
        {
            return tfsState.IsFolderTracked(directory);
        }

        bool IsIgnored(string localPath,
                       ItemType itemType)
        {
            if (itemType == ItemType.File)
            {
                if (localPath.EndsWith(".mine") || ignoreList.IsIgnored(localPath) || Regex.IsMatch(localPath, @"\.r\d+$"))
                    return true;
                return false;
            }
            return ignoreList.IsIgnored(localPath);
        }

        static bool IsMetadataFolder(string localPath)
        {
            return localPath.EndsWith(@"\" + TfsState.METADATA_FOLDER);
        }

        bool IsParentDirectoryTracked(string localPath)
        {
            return tfsState.IsFolderTracked(fileSystem.GetDirectoryName(localPath));
        }

        public virtual void ValidateDirectoryStructure(string directory)
        {
            if (!fileSystem.DirectoryExists(directory))
                throw new DirectoryNotFoundException("Directory not found: " + directory);
            if (!tfsState.IsFolderTracked(directory))
                throw new TfsStateException(TfsStateError.NotAWorkingFolder, directory);

            List<InvalidTfsDirectoryStructureException.Error> errors = new List<InvalidTfsDirectoryStructureException.Error>();
            ValidateDirectoryStructure_Helper(directory, tfsState.GetFolderInfo(directory), errors);

            if (errors.Count > 0)
                throw new InvalidTfsDirectoryStructureException(errors);
        }

        void ValidateDirectoryStructure_Helper(string directory,
                                               TfsFolderInfo tfsInfo,
                                               List<InvalidTfsDirectoryStructureException.Error> errors)
        {
            foreach (string subDirectory in fileSystem.GetDirectories(directory))
            {
                if (tfsState.IsFolderTracked(subDirectory))
                {
                    string shortName = fileSystem.GetFileName(subDirectory);
                    string expectedServerPath = TfsUtil.CombineProjectPath(tfsInfo.ServerPath, shortName);
                    TfsFolderInfo subTfsInfo = tfsState.GetFolderInfo(subDirectory);

                    if (string.Compare(tfsInfo.TfsUrl, subTfsInfo.TfsUrl, true) != 0 || string.Compare(expectedServerPath, subTfsInfo.ServerPath, true) != 0)
                        errors.Add(new InvalidTfsDirectoryStructureException.Error(subDirectory, tfsInfo.TfsUrl, expectedServerPath,
                                                                                   subTfsInfo.TfsUrl, subTfsInfo.ServerPath));
                    else
                        ValidateDirectoryStructure_Helper(subDirectory, subTfsInfo, errors);
                }
            }
        }

        public class WorkspaceMetadata
        {
            public ICredentials Credentials;
            public string ServerPath;
            public string TfsUrl;
            public string WorkspaceName;

            public WorkspaceMetadata(string tfsUrl,
                                     ICredentials credentials,
                                     string serverPath,
                                     string workspaceName)
            {
                TfsUrl = tfsUrl;
                Credentials = credentials;
                ServerPath = serverPath;
                WorkspaceName = workspaceName;
            }
        }
    }
}