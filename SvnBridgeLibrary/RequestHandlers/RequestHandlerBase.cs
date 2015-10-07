using System;
using System.IO; // Path.Combine(), Stream, StreamWriter, TextWriter
using System.Net; // ICredentials
using System.Text; // Encoding
using SvnBridge.Interfaces;
using SvnBridge.SourceControl;
using SvnBridge.Utility; // Helper.Encode*()
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

        public void SetSourceControlProvider(TFSSourceControlProvider sourceControlProvider)
	    {
	        this.sourceControlProvider = sourceControlProvider;
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

            // BIG FAT CENTRAL WARNING:
            // all request handlers which are expected
            // to need to send huge amounts of data
            // (in the range of hundreds of MB)
            // definitely ought to enable chunked transfers,
            // otherwise the *single* output block
            // (required due to indicating a *fixed* Content-Length before-hand!)
            // will explode into a huge MemoryStream object
            // (== potentially not allocatable due to linear-alloc requirement).
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

        /// <summary>
        /// Since almost all handlers were enabling SendChunked
        /// and this is *dangerous* to not enable
        /// (huge single-block memory allocations),
        /// decide to enable it as often as possible,
        /// at least where chunked transfer support is a *required* feature
        /// (&gt;= HTTP 1.1).
        /// </summary>
        protected void ConfigureResponse_SendChunked(
            )
        {
            httpContext.Response.SendChunked = true;
        }

        protected static void WriteHumanReadableError(TextWriter output, int svn_error_code, string error_string)
        {
            string responseContent =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:error xmlns:D=\"DAV:\" xmlns:m=\"http://apache.org/dav/xmlns\" xmlns:C=\"svn:\">\n" +
                "<C:error/>\n" +
                "<m:human-readable errcode=\"" + svn_error_code.ToString() + "\">\n" +
                error_string + "\n" +
                "</m:human-readable>\n" +
                "</D:error>\n";
            output.Write(responseContent);
            // Perhaps in addition we should actively do an output.Close() directly within this method,
            // since it's possible that the SVN protocol implies that such an error is always
            // the very last communication part that gets sent, thus the Close() should be made hard behaviour?
        }

        protected void WriteFileNotFoundResponse(IHttpRequest request, IHttpResponse response)
        {
            string requestPath = GetPath(request);
            response.StatusCode = (int)HttpStatusCode.NotFound;
            response.ContentType = "text/html; charset=iso-8859-1";

            string responseContent =
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>404 Not Found</title>\n" +
                "</head><body>\n" +
                "<h1>Not Found</h1>\n" +
                "<p>The requested URL " + Helper.EncodeB(requestPath) + " was not found on this server.</p>\n" +
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

        public static void WriteLog(string logMessage)
        {
            string pathLogFile = Path.Combine(LogBasePath, "requestlog.txt");
            using (StreamWriter w = File.AppendText(pathLogFile))
            {
                w.WriteLine("{0}", logMessage);
                w.WriteLine("-------------------------------");
            }
        }
    }

    /// <summary>
    /// Contains generic helpers for WebDAV stream generation etc.
    /// </summary>
    public class WebDAVGeneratorHelpers
    {
        /// <summary>
        /// This should be returning an entity tag ("entity-tag") of a resource
        /// (a _quoted_ string: "...content..." - or W/"...content..." to indicate a weak tag).
        /// Probably a hash (simple hashing, or MD5, SHA1, possibly using System.Security.Cryptography ComputeHash())
        /// of the resource's specific unique data such as inode / mtime / ...
        /// See "thoughts on ETags and mod_dav" http://marc.info/?l=apache-httpd-dev&m=119213950421845&w=3
        /// and "Weak Etags in Apache are useless and violate RFC 2616, 13.3.3" https://issues.apache.org/bugzilla/show_bug.cgi?id=42987
        ///
        /// SEMI-STUB!
        /// OK, for now return item.Md5Hash, since that should be more or less what's expected here.
        /// And we better should mark it weak ("W/")?
        /// Hmm, Md5Hash may (sometimes?) be null. Are we supposed to invoke ReadFileAsync() or some such
        /// on the item in such a case, to get Md5Hash member populated?
        /// </summary>
        public static string GetETag_revision_item(
            string xml_namespace,
            int itemRevision,
            string itemLocation)
        {
            //if (item.Md5Hash == null)
            //{
            //    return "<D:getetag/>";
            //}
            //else
            //{
            //    return "<D:getetag>W/\"" + item.Md5Hash + "\"</D:getetag>";
            //}
            // NOPE, we'll do the same thing that PropFindHandler.cs does (FIXME duplicated code!):
            return "<lp1:getetag>W/\"" + itemRevision + "//" + Helper.EncodeB(itemLocation) + "\"</lp1:getetag>";
        }
	}

    /// <summary>
    /// Contains generic helpers for SVN-specific stream generation etc.
    /// </summary>
    public class SVNGeneratorHelpers
    {
        public static string GetSvnVerFromRevisionLocation(int revision, string itemLocation, bool isItemLocationRelativePath)
        {
            // *We* are about to assemble a '/'-separated path *here*,
            // thus it's *here* that *we* are supposed to be escaping (encoding)
            // any payload (non-protocol) content
            // which might contain e.g. slashes as well.
            string itemLocationEncoded = Helper.Encode(itemLocation, true);
            string svnVerPath = "/!svn/ver/" + revision + (isItemLocationRelativePath ? "/" : "") + itemLocationEncoded;
            return svnVerPath;
        }
    }
}
