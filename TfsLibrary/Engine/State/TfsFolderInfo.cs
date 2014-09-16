namespace CodePlex.TfsLibrary.ClientEngine
{
    public class TfsFolderInfo
    {
        public string ServerPath;
        public string TfsUrl;

        public TfsFolderInfo() {}

        public TfsFolderInfo(string tfsUrl,
                             string serverPath)
        {
            TfsUrl = tfsUrl;
            ServerPath = serverPath;
        }
    }
}