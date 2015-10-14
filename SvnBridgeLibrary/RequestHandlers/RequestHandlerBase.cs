using System;
using System.IO;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Threading;
using SvnBridge.Interfaces;
using SvnBridge.Net;
using SvnBridge.SourceControl;
using SvnBridge.Utility;
using SvnBridge.Infrastructure;
using System.Collections;
using SvnBridge.Cache;

namespace SvnBridge.Handlers
{
	public abstract class RequestHandlerBase
	{
		private IPathParser pathParser;
		private IHttpContext httpContext;
	    private ICredentials credentials;

        public void SetSourceControlProvider(TFSSourceControlProvider value)
	    {
	        sourceControlProvider = value;
	    }

        TFSSourceControlProvider sourceControlProvider;

		public IPathParser PathParser
		{
			get { return pathParser; }
		}

	    public ICredentials Credentials
	    {
	        get { return credentials; }
	    }

	    public virtual void Handle(IHttpContext context, IPathParser pathParser, NetworkCredential credentials)
		{
            this.credentials = credentials;
            Initialize(context, pathParser);
            sourceControlProvider = Container.Resolve<TFSSourceControlProvider>();

            Handle(context, sourceControlProvider);
		}

		public void Initialize(IHttpContext context, IPathParser parser)
		{
			this.httpContext = context;
			this.pathParser = parser;
		}

		public virtual void Cancel()
		{
		}

        protected abstract void Handle(IHttpContext context, TFSSourceControlProvider sourceControlProvider);

		protected static void SetResponseSettings(IHttpResponse response, string contentType, Encoding contentEncoding, int status)
		{
			response.ContentType = contentType;
			response.ContentEncoding = contentEncoding;
			response.StatusCode = status;
		}

		protected static void WriteToResponse(IHttpResponse response, string content)
		{
			using (StreamWriter writer = new StreamWriter(response.OutputStream))
			{
				writer.Write(content);
			}
		}

		protected string GetPath(IHttpRequest request)
		{
			return pathParser.GetLocalPath(request);
		}

		public string VccPath
		{
			get { return GetLocalPath(Constants.SvnVccPath); }
		}

		public string GetLocalPath(string href)
		{
            string result;
            string applicationPath = PathParser.GetApplicationPath(httpContext.Request);

			if (href.StartsWith("/") == false && applicationPath.EndsWith("/") == false)
			    result =  applicationPath + "/" + href;
			if (href.StartsWith("/") && applicationPath.EndsWith("/"))
			    result = applicationPath + href.Substring(1);
		    else
                result = applicationPath + href;
		    return result;
		}

		public string GetLocalPathFromUrl(string path)
		{
			return PathParser.GetLocalPath(httpContext.Request, path);
		}

        protected void WriteFileNotFoundResponse(IHttpRequest request, IHttpResponse response)
        {
            response.StatusCode = (int)HttpStatusCode.NotFound;
            response.ContentType = "text/html; charset=iso-8859-1";

            string responseContent =
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>404 Not Found</title>\n" +
                "</head><body>\n" +
                "<h1>Not Found</h1>\n" +
                "<p>The requested URL " + Helper.EncodeB(GetPath(request)) + " was not found on this server.</p>\n" +
                "<hr>\n" +
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at " + request.Url.Host + " Port " + request.Url.Port + "</address>\n" +
                "</body></html>\n";

            WriteToResponse(response, responseContent);
        }
	}
}