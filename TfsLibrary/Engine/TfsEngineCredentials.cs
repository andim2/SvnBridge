using System.Collections.Generic;
using System.Net;

namespace CodePlex.TfsLibrary.ClientEngine
{
    public partial class TfsEngine
    {
        readonly IDictionary<string, ICredentials> credentialCache = new Dictionary<string, ICredentials>();

        protected ICredentials GetCredentials(string tfsUrl)
        {
            return GetCredentials(tfsUrl, false);
        }

        protected ICredentials GetCredentials(string tfsUrl,
                                              bool force)
        {
            if (force || !credentialCache.ContainsKey(tfsUrl))
            {
                ICredentials creds = null;

                if (credentialsCallback != null)
                    creds = credentialsCallback(null, tfsUrl);

                credentialCache[tfsUrl] = creds;
            }

            return credentialCache[tfsUrl];
        }
    }
}