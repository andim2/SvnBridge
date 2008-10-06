using System;
using System.Web;
using SvnBridge.Net;
using SvnBridge;
using SvnBridge.Infrastructure;
using SvnBridge.SourceControl;
using SvnBridge.Interfaces;
using SvnBridge.PathParsing;
using SvnBridge.Infrastructure.Statistics;

namespace SvnBridgeServer
{
	public class SvnBridgeHttpHandler : IHttpHandler
	{
		private readonly HttpContextDispatcher dispatcher;

        static SvnBridgeHttpHandler()
        {
            BootStrapper.Start();
        }

		public SvnBridgeHttpHandler()
		{
            IPathParser pathParser;
		    
            if (Configuration.UseCodePlexServers)
            {
                pathParser = new PathParserProjectInDomainCodePlex();
            }
            else if (Configuration.DomainIncludesProjectName)
            {
                pathParser = new PathParserProjectInDomain(Configuration.TfsUrl, Container.Resolve<TFSSourceControlService>());
            }
			else
			{
                pathParser = new PathParserSingleServerWithProjectInPath(Configuration.TfsUrl);
			}
            dispatcher = new HttpContextDispatcher(pathParser, Container.Resolve<ActionTrackingViaPerfCounter>());
        }

		#region IHttpHandler Members

		public bool IsReusable
		{
			get { return false; }
		}

		public void ProcessRequest(HttpContext context)
		{
			try
			{
                try
                {
                    dispatcher.Dispatch(new HttpContextWrapper(context));
                }
                catch (Exception ex)
                {
                    DefaultLogger logger = Container.Resolve<DefaultLogger>();
                    logger.ErrorFullDetails(ex, new HttpContextWrapper(context));
                    throw;
                }
			}
			finally
			{
				context.Response.OutputStream.Dispose();
			}
		}

		#endregion
	}
}
