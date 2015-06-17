using System;
using System.Net;
using IntegrationTests.Properties;
using SvnBridge.SourceControl;

namespace IntegrationTests
{
    /// <summary>
    /// This class is needed so we will authenticate as a non admin user, which is required 
    /// because of the process template used in CodePlex
    /// </summary>
    public class AuthenticateAsLowPrivilegeUser : IDisposable
    {
        private readonly NetworkCredential oldCredentials;

        public AuthenticateAsLowPrivilegeUser() : this(Settings.Default.Username, Settings.Default.Password, Settings.Default.Domain)
        {
        }

        public AuthenticateAsLowPrivilegeUser(string user, string password, string domain)
        {
            oldCredentials = CredentialsHelper.DefaultCredentials;
            if (string.IsNullOrEmpty(user.Trim()))
                return;

            NetworkCredential newCredentials = new NetworkCredential(user, password, domain);
            CredentialsHelper.NullCredentials = newCredentials;
            CredentialsHelper.DefaultCredentials = newCredentials;
        }

        public void Dispose()
        {
            CredentialsHelper.DefaultCredentials = oldCredentials;
            CredentialsHelper.NullCredentials = null;
        }
    }
}
