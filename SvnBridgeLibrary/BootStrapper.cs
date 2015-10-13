using System.Net; // ServicePointManager
using CodePlex.TfsLibrary.ObjectModel; // IRegistrationService, IWebTransferService
using SvnBridge.Infrastructure; // Container, *WorkItemModifier, Configuration
using CodePlex.TfsLibrary.RegistrationWebSvc;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using CodePlex.TfsLibrary.Utility; // IFileSystem

namespace SvnBridge
{
    public static class BootStrapper
    {
        public static void Start()
        {
            ServicePointManager.DefaultConnectionLimit = 5000;
            ServicePointManagerConfigureTcpKeepAlive();
            TfsUtil.OnSetupWebRequest = WebRequestSetup.OnWebRequest;

            Container.Register(typeof(IRegistrationService), typeof(RegistrationService));
            Container.Register(typeof(IWebTransferService), typeof(WebTransferService));
            Container.Register(typeof(IRegistrationWebSvcFactory), typeof(RegistrationWebSvcFactory));
            Container.Register(typeof(IRepositoryWebSvcFactory), typeof(RepositoryWebSvcFactory));
            Container.Register(typeof(IFileSystem), typeof(FileSystem));
            Container.Register(typeof (IWorkItemModifier),
                               string.IsNullOrEmpty(Configuration.CodePlexWorkItemUrl)
                                   ? typeof (TfsWorkItemModifier)
                                   : typeof (CodePlexWorkItemModifier));
        }

        private static void ServicePointManagerConfigureTcpKeepAlive()
        {
            // Nice hint by
            // http://blogs.msdn.com/b/granth/archive/2013/02/13/tfs-load-balancers-idle-timeout-settings-and-tcp-keep-alives.aspx
            // Enable TCP Keep-Alives. Send the first Keep-Alive after 50 seconds, then if no response is received in 1 second, send another keep-alive.
            // This should be able to avoid connection loss
            // which gets accompanied by error messages such as
            // "The underlying connection was closed: A connection that was expected to be kept alive was closed by the server"
            // But execute this setup only
            // in case enabling this feature actually is requested;
            // this would be even more important
            // if it was instead done
            // in the performance-critical OnWebRequest hook hotpath.
            var keepAliveTimeSeconds = 50;
            var keepAliveIntervalSeconds = 1;
            bool enable = (0 != keepAliveTimeSeconds);
            if (enable)
            {
                // If this code happened to be in hotpath:
                // perhaps we could then pre-calculate the millisecond multiply somewhere...
                //httpWebRequest.ServicePoint
                ServicePointManager
                  .SetTcpKeepAlive(true, keepAliveTimeSeconds * 1000, keepAliveIntervalSeconds * 1000);
            }
        }
    }
}
