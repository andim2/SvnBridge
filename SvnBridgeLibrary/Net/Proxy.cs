using System.Net;
using SvnBridge.Utility;

namespace SvnBridge.Net
{
    public static class Proxy
    {
    	public static ProxyInformation DefaultProxy = new ProxyInformation();

        public static void Set(ProxyInformation proxyInformation)
        {
        	DefaultProxy = proxyInformation;
			if (proxyInformation.UseProxy)
			{
				WebRequest.DefaultWebProxy = Helper.CreateProxy(proxyInformation);
			}
        }
    }
}