using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.Utility;

namespace CodePlex.TfsLibrary.ClientEngine
{
    /// <summary>
    /// This class manages workspace creation and cleanup.
    /// 
    /// When each workspace is created, a file is placed on the filesystem with the name
    /// "tfshostname - guid", where guid is the name of the workspace. The manager keeps
    /// track of open workspaces by handing out TfsWorkspace objects, which must be disposed
    /// by the clients who use them. When they are disposed, the manager will delete the
    /// workspace.
    /// 
    /// The contents of the file also contain the guid. The file is left open while the
    /// workspace is in use. When the workspace is closed, the file contents are erased
    /// and the file is deleted.
    /// 
    /// When CreateWorkspace is called, it looks for files for the same tfshostname, and
    /// if it can open them and they contain contents, then it cleans up the workspace
    /// (because this means the workspace was left over from a previous run). Since the
    /// manager keeps the file open while it's running for workspaces it creates, it
    /// prevents other managers (perhaps in other processes) from deleting its in-use
    /// workspaces.
    /// 
    /// The file is written immediately rather than after failure, in order to compensate
    /// for the possibility that the process could crash or be terminated and the manager
    /// would not be notified.
    /// 
    /// The files are stored in the user's personal file space. The reason deletion is
    /// delayed until CrateWorkspace() calls is because we need the credentials for the
    /// TFS server, and storing them on the file system is problematic.
    /// </summary>
    public class TfsWorkspaceManager
    {
        protected const string WorkspaceComment = "Temporary workspace for edit-merge-commit";

        readonly ISourceControlService sourceControlService;
        readonly IFileSystem fileSystem;
        readonly Dictionary<TfsWorkspace, Stream> workspaceStreams = new Dictionary<TfsWorkspace, Stream>();

        public TfsWorkspaceManager(ISourceControlService sourceControlService,
                                   IFileSystem fileSystem)
        {
            this.sourceControlService = sourceControlService;
            this.fileSystem = fileSystem;
        }

        void CleanUpWorkspaces(string tfsUrl,
                               ICredentials credentials)
        {
            string hostName = new Uri(tfsUrl).Host;

            foreach (string filename in fileSystem.GetFiles(fileSystem.UserDataPath, hostName + " - *"))
            {
                try
                {
                    using (Stream stream = fileSystem.OpenFile(filename, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                        if (stream.ReadByte() == 1)
                        {
                            string workspaceName = filename.Substring(filename.IndexOf(" - ") + 3);
                            sourceControlService.DeleteWorkspace(tfsUrl, credentials, workspaceName);
                            stream.Position = 0;
                            stream.WriteByte(0);
                        }

                    fileSystem.DeleteFile(filename);
                }
                catch (Exception) {}
            }
        }

        public TfsWorkspace CreateWorkspace(string tfsUrl,
                                            ICredentials credentials)
        {
            CleanUpWorkspaces(tfsUrl, credentials);

            string workspaceName = Guid.NewGuid().ToString("N");

            TfsWorkspace workspace = new TfsWorkspace(workspaceName, tfsUrl, credentials, OnWorkspaceDisposed);
            string workspaceFilename = GetWorkspaceFilename(workspace);
            Stream stream = fileSystem.OpenFile(workspaceFilename, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            stream.WriteByte(1);
            stream.Flush();

            sourceControlService.CreateWorkspace(tfsUrl, credentials, workspaceName, WorkspaceComment);
            workspaceStreams[workspace] = stream;
            return workspace;
        }

        string GetWorkspaceFilename(TfsWorkspace workspace)
        {
            return fileSystem.CombinePath(fileSystem.UserDataPath, string.Format(@"{0} - {1}", workspace.HostName, workspace.Name));
        }

        void OnWorkspaceDisposed(TfsWorkspace workspace)
        {
            Stream stream = workspaceStreams[workspace];

            try
            {
                sourceControlService.DeleteWorkspace(workspace.TfsUrl, workspace.Credentials, workspace.Name);

                stream.Position = 0;
                stream.WriteByte(0);
                stream.Flush();
                stream.Dispose();
                stream = null;

                string workspaceFilename = GetWorkspaceFilename(workspace);
                fileSystem.DeleteFile(workspaceFilename);
            }
            catch (Exception) {}
            finally
            {
                if (stream != null)
                    stream.Dispose();

                workspaceStreams.Remove(workspace);
            }
        }
    }
}