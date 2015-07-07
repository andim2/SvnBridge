using System;
using System.Net;
using SvnBridge.Infrastructure;
using SvnBridge.Utility; // Helper.GetUnsafeNetworkCredential()

namespace SvnBridge.SourceControl
{
    public static class CredentialsHelper
    {
        public static NetworkCredential DefaultCredentials = Helper.GetUnsafeNetworkCredential();
        public static NetworkCredential NullCredentials;

        public static ICredentials GetCredentialsForServer(string tfsUrl, ICredentials credentials)
        {
            if (credentials == null)
            {
                var uri = new Uri(tfsUrl);
                bool want_codeplex_credential = IsUriCodePlex(uri);
                if (want_codeplex_credential)
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
                            // Cast required - avoid .NET4 NetworkCredential ctor signature ambiguity error:
                            //     'System.Net.NetworkCredential.NetworkCredential(string, System.Security.SecureString)'
                            // vs. 'System.Net.NetworkCredential.NetworkCredential(string, string)'
                            { uri, "Basic", new NetworkCredential("anonymous", (string)null) }
                        };
                        credentials = cache;
                    }
                }
                else
                    credentials = DefaultCredentials;
            }
            return credentials;
        }

        private static bool IsUriCodePlex(Uri uri)
        {
            return (uri.Host.ToLowerInvariant().EndsWith("codeplex.com") || uri.Host.ToLowerInvariant().Contains("tfs.codeplex.com"));
        }
    }
}
