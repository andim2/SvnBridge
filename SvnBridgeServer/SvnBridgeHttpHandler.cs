using System;
using System.Web;
using System.Web.Services.Protocols;
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
            else if (!string.IsNullOrEmpty(Configuration.TfsUrl))
            {
                pathParser = new PathParserSingleServerWithProjectInPath(Configuration.TfsUrl);
            }
            else
			{
                pathParser = new PathParserServerAndProjectInPath(Container.Resolve<TfsUrlValidator>());
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
                    if (!(ex is SoapException && ex.Message != null && ex.Message.Contains("Project is not a TFS project")))
                    {
                        var logger = Container.Resolve<DefaultLogger>();
                        logger.ErrorFullDetails(ex, new HttpContextWrapper(context)); 
                    }
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
