using System.Collections.Specialized;
using System.IO;
using System.Text; // Encoding
using Xunit;
using SvnBridge.SourceControl;
using Tests;
using SvnBridge.Net;
using Attach;
using System;
using SvnBridge;
using SvnBridge.Handlers; // RequestHandlerBase
using SvnBridge.Infrastructure;
using SvnBridge.PathParsing; // PathParserSingleServerWithProjectInPath

namespace UnitTests
{
    public abstract class HandlerTestsBase : IDisposable
    {
        protected StubHttpContext context;
        protected StubHttpRequest request;
        protected StubHttpResponse response;
        protected TFSSourceControlProvider provider;
        protected MyMocks stubs = new MyMocks();
        protected string tfsUrl;

        public HandlerTestsBase()
        {
            BootStrapper.Start();
            provider = stubs.CreateTFSSourceControlProviderStub();
            Container.Register(typeof(TFSSourceControlProvider), provider);
            context = new StubHttpContext();
            request = new StubHttpRequest();
            request.Headers = new NameValueCollection();
            context.Request = request;
            response = new StubHttpResponse();
            response.OutputStream = new MemoryStream(Constants.BufferSize);
            context.Response = response;
            tfsUrl = "http://tfsserver";
            RequestCache.Init();
        }

        public virtual void Dispose()
        {
            Clock.FrozenCurrentTime = null;
            Container.Reset();
        }

        protected string HandlerHandle(RequestHandlerBase handler, string serverUrl)
        {
            string result;

            using (var output = new StreamWriter(response.OutputStream))
            {
                handler.Handle(context, new PathParserSingleServerWithProjectInPath(serverUrl), null, output);
            }

            result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());

            return result;
        }

        protected string HandlerHandle(RequestHandlerBase handler)
        {
            return HandlerHandle(handler, tfsUrl);
        }

        /// <summary>
        /// Q&amp;D helper (probably shouldn't be in here ultimately).
        /// Creates a string containing an svn diff which exists to apply a single-char change to a file,
        /// for the purpose of guaranteeing a write to an SCM item.
        /// </summary>
        /// <returns>string which contains the svn diff</returns>
        protected static string GetSvnDiffStringForSingleCharFileWrite()
        {
            char[] svnHeader = new char[] { 'S', 'V', 'N', '\0' };
            char[] svnDiffWindow = new char[] { '\0', '\0', '\u0001', '\u0001', '\u0001', '\u0081', 'X' };
            char[] svnDiff = new char[svnHeader.Length + svnDiffWindow.Length];
            svnHeader.CopyTo(svnDiff, 0);
            svnDiffWindow.CopyTo(svnDiff, svnHeader.Length);
            //string svnDiffString = Encoding.Unicode.GetString(svnDiff);
            string svnDiffString = new string(svnDiff);
            return svnDiffString;
        }
    }
}
