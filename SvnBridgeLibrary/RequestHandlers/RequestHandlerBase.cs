using System;
using System.IO; // Path.Combine(), Stream, StreamWriter, TextWriter
using System.Net; // ICredentials
using System.Text; // Encoding
using SvnBridge.Interfaces;
using SvnBridge.SourceControl;
using SvnBridge.Utility; // Helper.EncodeB()
using SvnBridge.Infrastructure;

namespace SvnBridge.Handlers
{
    /// <summary>
    /// Have separate classes for both HTTP-generic parts and derived WebDAV-/SVN-specific parts.
    /// </summary>
    public abstract class RequestHandlerHttpBase
    {
        public virtual void Cancel()
        {
        }

		protected static void SetResponseSettings(IHttpResponse response, string contentType, Encoding contentEncoding, int status)
		{
			response.ContentType = contentType;
			response.ContentEncoding = contentEncoding;
			response.StatusCode = status;
		}

        /// <remarks>
        /// Related info:
        /// http://stackoverflow.com/questions/8711584/x-pad-avoid-browser-bug-header-added-by-apache
        /// </remarks>
        protected static void SetResponseHeader_X_Pad_avoid_browser_bug(
            IHttpResponse response)
        {
            response.AppendHeader("X-Pad", "avoid browser bug");
        }

        protected static StreamWriter CreateStreamWriter(Stream outputStream)
        {
            return Helper.ConstructStreamWriterUTF8(outputStream);
        }

		protected static void WriteToResponse(IHttpResponse response, string content)
		{
			using (StreamWriter writer = CreateStreamWriter(response.OutputStream))
			{
				writer.Write(content);
			}
		}
    }

    public abstract class RequestHandlerBase : RequestHandlerHttpBase
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

        public virtual void Handle(
            IHttpContext context,
            IPathParser pathParser,
            NetworkCredential credentials)
		{
            this.credentials = credentials;
            Initialize(context, pathParser);
            sourceControlProvider = Container.Resolve<TFSSourceControlProvider>();

            Handle(
                context,
                sourceControlProvider);
		}

		public void Initialize(IHttpContext context, IPathParser parser)
		{
			this.httpContext = context;
			this.pathParser = parser;
		}

        protected abstract void Handle(
            IHttpContext context,
            TFSSourceControlProvider sourceControlProvider);

    protected static string GetServerSidePath(string path)
    {
        // FIXME: quite likely instead of doing fugly open-coded crap
        // this should be made
        // to use some (possibly new?) functionality
        // of the PathParser member which we already have...
        if (path.IndexOf('/', 9) > -1)
        {
            return path.Substring(path.IndexOf('/', 9));
        }
        else
            return "/";
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

        protected static Recursion ConvertDepthHeaderToRecursion(string depth)
        {
            Recursion recursion = Recursion.None;

            if (depth.Equals("0"))
            {
                recursion = Recursion.None;
            }
            else
            if (depth.Equals("1"))
            {
                recursion = Recursion.OneLevel;
            }
            else
            if (depth.Equals("infinity"))
            {
                recursion = Recursion.Full;
            }
            else
            {
                throw new InvalidOperationException(String.Format("Depth not supported: {0}", depth));
            }

            return recursion;
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

        public static string LogBasePath
        {
            get
            {
                return "F:\\svnbridge\\Logs";
            }
        }

        public void WriteLog(string logMessage)
        {
            string pathLogFile = Path.Combine(LogBasePath, "requestlog.txt");
            using (StreamWriter w = File.AppendText(pathLogFile))
            {
                w.WriteLine("{0}", logMessage);
                w.WriteLine("-------------------------------");
            }
        }
	}
}
