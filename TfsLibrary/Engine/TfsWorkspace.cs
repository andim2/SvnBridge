using System;
using System.Net;

namespace CodePlex.TfsLibrary.ClientEngine
{
    public class TfsWorkspace : IDisposable
    {
        public delegate void DisposeCallback(TfsWorkspace workspace);

        DisposeCallback callback;
        readonly ICredentials credentials;
        readonly string hostName;
        readonly string name;
        readonly string tfsUrl;

        public TfsWorkspace(string name,
                            string tfsUrl,
                            ICredentials credentials,
                            DisposeCallback callback)
        {
            this.name = name;
            this.tfsUrl = tfsUrl;
            this.credentials = credentials;
            this.callback = callback;

            hostName = new Uri(tfsUrl).Host.ToLowerInvariant();
        }

        internal ICredentials Credentials
        {
            get { return credentials; }
        }

        internal string HostName
        {
            get { return hostName; }
        }

        public string Name
        {
            get { return name; }
        }

        internal string TfsUrl
        {
            get { return tfsUrl; }
        }

        public void Dispose()
        {
            if (callback != null)
                callback(this);

            callback = null;
        }
    }
}