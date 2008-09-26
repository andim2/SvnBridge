using System.Collections.Generic;
using System.IO;
using System.Text;
using Attach;
using SvnBridge.Interfaces;
using Xunit;
using SvnBridge.Exceptions;
using SvnBridge.Infrastructure;
using SvnBridge.PathParsing;
using SvnBridge.SourceControl;

namespace SvnBridge.Handlers
{
    public class MkColHandlerTests : HandlerTestsBase
    {
        protected MkColHandler handler = new MkColHandler();

        [Fact]
        public void VerifyCorrectOutputForSuccessfulCreate()
        {
            Results r = stubs.Attach(provider.MakeCollection);
            request.Path =
                "http://localhost:8082//!svn/wrk/0eaf3261-5f80-a140-b21d-c1b0316a256a/Spikes/SvnFacade/trunk/New%20Folder%206";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);
            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());

            string expected =
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>201 Created</title>\n" +
                "</head><body>\n" +
                "<h1>Created</h1>\n" +
                "<p>Collection //!svn/wrk/0eaf3261-5f80-a140-b21d-c1b0316a256a/Spikes/SvnFacade/trunk/New Folder 6 has been created.</p>\n" +
                "<hr />\n" +
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8082</address>\n" +
                "</body></html>\n";
            Assert.Equal(expected, result);
            Assert.Equal(201, response.StatusCode);
            Assert.Equal("text/html", response.ContentType);
            Assert.True(
                response.Headers.Contains(
                    new KeyValuePair<string, string>("Location",
                                                     "http://localhost:8082//!svn/wrk/0eaf3261-5f80-a140-b21d-c1b0316a256a/Spikes/SvnFacade/trunk/New Folder 6")));
        }

        [Fact]
        public void VerifyCorrectOutputWhenFolderAlreadyExists()
        {
            stubs.Attach(provider.MakeCollection, new FolderAlreadyExistsException());
            request.Path = "http://localhost:8082//!svn/wrk/de1ec288-d55c-6146-950d-ceaf2ce9403b/newdir";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);
            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());

            string expected =
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>405 Method Not Allowed</title>\n" +
                "</head><body>\n" +
                "<h1>Method Not Allowed</h1>\n" +
                "<p>The requested method MKCOL is not allowed for the URL //!svn/wrk/de1ec288-d55c-6146-950d-ceaf2ce9403b/newdir.</p>\n" +
                "<hr>\n" +
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8082</address>\n" +
                "</body></html>\n";
            Assert.Equal(expected, result);
            Assert.Equal(405, response.StatusCode);
            Assert.Equal("text/html; charset=iso-8859-1", response.ContentType);
            Assert.True(response.Headers.Contains(new KeyValuePair<string, string>("Allow", "TRACE")));
        }

        [Fact]
        public void VerifyHandleCorrectlyInvokesSourceControlProvider()
        {
            Results r = stubs.Attach(provider.MakeCollection);
            request.Path =
                "http://localhost:8081//!svn/wrk/5b34ae67-87de-3741-a590-8bda26893532/Spikes/SvnFacade/trunk/Empty";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            Assert.Equal(1, r.CallCount);
            Assert.Equal("5b34ae67-87de-3741-a590-8bda26893532", r.Parameters[0]);
            Assert.Equal("/Spikes/SvnFacade/trunk/Empty", r.Parameters[1]);
        }

        [Fact]
        public void VerifyPathIsDecodedWhenCallingSourceControlProvider()
        {
            Results r = stubs.Attach(provider.MakeCollection);
            request.Path = "http://localhost:8081//!svn/wrk/0eaf3261-5f80-a140-b21d-c1b0316a256a/Folder%20With%20Spaces";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            Assert.Equal("/Folder With Spaces", r.Parameters[1]);
        }
    }
}
