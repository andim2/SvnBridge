namespace CodePlex.TfsLibrary.RepositoryWebSvc
{
    public partial class Failure
    {
        public Failure(string localPath,
                       string message)
            : this()
        {
            local = localPath;
            Message = message;
        }
    }
}