using System.Collections.Generic;

namespace SvnBridge.Net
{
	public class ProxyInformation
	{
		private bool useProxy;
		private int port;
		private string username;
		private string password;
		private bool useDefaultCredentails;
		private string url;
        private string tfsProxyUrl;

		public bool UseProxy
		{
			get { return useProxy; }
			set { useProxy = value; }
		}

		public int Port
		{
			get { return port; }
			set { port = value; }
		}

		public string Username
		{
			get { return username; }
			set { username = value; }
		}

		public string Password
		{
			get { return password; }
			set { password = value; }
		}

		public bool UseDefaultCredentails
		{
			get { return useDefaultCredentails; }
			set { useDefaultCredentails = value; }
		}

        public string TfsProxyUrl
        {
            get { return tfsProxyUrl; }
            set { tfsProxyUrl = value; }
        }

		public string Url
		{
			get { return url; }
			set { url = value; }
		}

		public static bool operator ==(ProxyInformation x, ProxyInformation y)
		{
			return Equals(x, y);
		}

		public static bool operator !=(ProxyInformation x, ProxyInformation y)
		{
			return !(x == y);
		}

		public bool Equals(ProxyInformation other)
		{
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;

			return Equals(other.useProxy, useProxy) &&
				other.port == port &&
				Equals(other.username, username) &&
				Equals(other.password, password) &&
				Equals(other.useDefaultCredentails, useDefaultCredentails) &&
                Equals(other.url, url) &&
                Equals(other.tfsProxyUrl, tfsProxyUrl);
		}

		public override bool Equals(object other)
		{
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;
			return Equals(other as ProxyInformation);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				int result = useProxy.GetHashCode();
				result = (result * 397) ^ port;
				result = (result * 397) ^ (username != null ? username.GetHashCode() : 0);
				result = (result * 397) ^ (password != null ? password.GetHashCode() : 0);
				result = (result * 397) ^ useDefaultCredentails.GetHashCode();
				result = (result * 397) ^ (url != null ? url.GetHashCode() : 0);
                result = (result * 397) ^ (tfsProxyUrl != null ? tfsProxyUrl.GetHashCode() : 0);
                return result;
			}
		}
	}
}