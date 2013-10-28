using System;
using System.Net;
using SvnBridge.Infrastructure;

namespace SvnBridge.SourceControl
{
    public static class CredentialsHelper
    {
        public static NetworkCredential DefaultCredentials = CredentialCache.DefaultNetworkCredentials;
        public static NetworkCredential NullCredentials;

        public static ICredentials GetCredentialsForServer(string tfsUrl, ICredentials credentials)
        {
            if (credentials == null)
            {
                var uri = new Uri(tfsUrl);
                if (uri.Host.ToLowerInvariant().EndsWith("codeplex.com") || uri.Host.ToLowerInvariant().Contains("tfs.codeplex.com"))
                {
                    if (!string.IsNullOrEmpty(Configuration.CodePlexAnonUserName))
                    {
                        credentials = new NetworkCredential(Configuration.CodePlexAnonUserName,
                                                            Configuration.CodePlexAnonUserPassword,
                                                            Configuration.CodePlexAnonUserDomain);
                    }
                    else
                    {
                        var cache = new CredentialCache
                        {
                            { uri, "Basic", new NetworkCredential("anonymous", null) }
                        };
                        credentials = cache;
                    }
                }
                else
                    credentials = DefaultCredentials;
            }
            return credentials;
        }
    }
}
