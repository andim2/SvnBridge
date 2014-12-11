using System;
using System.Net;
using SvnBridge.Infrastructure;
using SvnBridge.Utility; // Helper.GetUnsafeNetworkCredential()

namespace SvnBridge.SourceControl
{
    public static class CredentialsHelper
    {
        public static NetworkCredential NullCredentials;
        // IMPORTANT SECURITY NOTE!! I believe that whatever we do,
        // we should *never* adopt DefaultNetworkCredentials ("current security context" credentials)
        // since a network server (such as SvnBridge) session
        // quite certainly is running in a security context
        // that's *different*
        // from the security context of the foreign-side client (SVN),
        // i.e. it might have implicit elevated privileges that the client user
        // (which might even be completely unable to supply any valid credentials!)
        // does not have.
        // IOW, make damn sure to do all processing
        // using only those credentials
        // which are/were always fully gathered (requested!!) from the SVN user side
        // rather than "implicit" credentials knowledge
        // of the usually-foreign SvnBridge session environment.
        //public static NetworkCredential DefaultCredentials = Helper.GetUnsafeNetworkCredential();
        public static NetworkCredential DefaultCredentials = NullCredentials;

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
                    credentials = NullCredentials;
            }
            return credentials;
        }

        private static bool IsUriCodePlex(Uri uri)
        {
            string uriHost_Lowercase = uri.Host.ToLowerInvariant();
            return (uriHost_Lowercase.EndsWith("codeplex.com") || uriHost_Lowercase.Contains("tfs.codeplex.com"));
        }
    }
}
