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
			string tfsUrl = Configuration.TfsUrl;
            IPathParser pathParser;
		    if (Configuration.UrlIncludesProjectName)
			{
                ProjectInformationRepository projectInformationRepository = new ProjectInformationRepository(Container.Resolve<MetaDataRepositoryFactory>(), tfsUrl, Configuration.UseCodePlexServers);
                pathParser = new PathParserProjectInDomain(tfsUrl, projectInformationRepository);
			}
			else
			{
                pathParser = new PathParserSingleServerWithProjectInPath(tfsUrl);
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
