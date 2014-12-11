using System.Net;

namespace CodePlex.TfsLibrary.Utility
{
    public interface ICredentialsCache
    {
        NetworkCredential this[string url] { get; set; }

        void Clear();
    }
}