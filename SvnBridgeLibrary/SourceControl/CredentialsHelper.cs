using System;
using System.Net;

namespace SvnBridge.SourceControl
{
    public static class CredentialsHelper
    {
        public static NetworkCredential DefaultCredentials = CredentialCache.DefaultNetworkCredentials;
        public static NetworkCredential NullCredentials = null;

        public static ICredentials GetCredentialsForServer(string tfsUrl, ICredentials credentials)
        {
            if (credentials == null)
            {
                Uri uri = new Uri(tfsUrl);
                if (uri.Host.ToLowerInvariant().EndsWith("codeplex.com"))
                {
                    CredentialCache cache = new CredentialCache();
                    cache.Add(uri, "Basic", new NetworkCredential("anonymous", null));
                    credentials = cache;
                }
                else
                {
                    credentials = DefaultCredentials;
                }
            }
            return credentials;
        }
    }
}
