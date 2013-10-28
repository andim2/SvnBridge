namespace CodePlex.TfsLibrary.ObjectModel
{
    public class WorkspaceInfo
    {
        public string Comment;
        public string Computer;
        public string Name;
        public string Username;

        public WorkspaceInfo(string name,
                             string username,
                             string computer,
                             string comment)
        {
            Name = name;
            Username = username;
            Computer = computer;
            Comment = comment;
        }
    }
}