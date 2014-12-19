namespace CodePlex.TfsLibrary.RepositoryWebSvc
{
    public partial class WorkingFolder
    {
        public WorkingFolder(string serverPath,
                             string localPath,
                             WorkingFolderType folderType)
        {
            item = serverPath;
            local = localPath;
            type = folderType;
        }
    }
}