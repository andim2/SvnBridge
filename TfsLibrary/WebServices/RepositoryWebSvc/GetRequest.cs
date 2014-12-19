namespace CodePlex.TfsLibrary.RepositoryWebSvc
{
    public partial class GetRequest
    {
        public GetRequest() {}

        public GetRequest(string serverPath,
                          RecursionType recursion,
                          VersionSpec versionSpec)
        {
            ItemSpec = new ItemSpec();

            ItemSpec.item = serverPath;
            ItemSpec.recurse = recursion;

            VersionSpec = versionSpec;
        }
    }
}