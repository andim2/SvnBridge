namespace CodePlex.TfsLibrary.ClientEngine
{
    public interface IIgnoreList
    {
        string IgnoreFilename { get; set; }

        bool IsIgnored(string path);
    }
}