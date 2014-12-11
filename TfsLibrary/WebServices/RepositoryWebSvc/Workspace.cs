namespace CodePlex.TfsLibrary.RepositoryWebSvc
{
    public partial class Workspace
    {
        public Workspace(string workspaceName,
                         string computerName,
                         string ownerName,
                         string comment)
        {
            name = workspaceName;
            computer = computerName;
            owner = ownerName;
            Comment = comment;
        }

        public void AddWorkingFolder(WorkingFolder folder)
        {
            WorkingFolder[] oldFolders = Folders ?? new WorkingFolder[0];
            Folders = new WorkingFolder[oldFolders.Length + 1];
            oldFolders.CopyTo(Folders, 0);
            Folders[oldFolders.Length] = folder;
        }
    }
}