using System.Collections.Generic;
using System.IO;
using System.Text;
using Attach;
using SvnBridge.Interfaces;
using Xunit;
using SvnBridge.Infrastructure;
using SvnBridge.PathParsing;
using SvnBridge.SourceControl;
using SvnBridge.Handlers;

namespace UnitTests
{
    public class CopyHandlerTests : HandlerTestsBase
    {
        protected CopyHandler handler = new CopyHandler();

        [Fact]
        public void TestDestinationInResponseMessageIsDecodedAndEncoded()
        {
            Results r = stubs.Attach(provider.CopyItem);
            request.Path = "http://localhost:8082/!svn/bc/5730/B%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60";
            request.Headers["Destination"] =
                "http://localhost:8084//!svn/wrk/15407bc3-2250-aa4c-aa51-4e65b2c824c3/BB%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);
            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());

            Assert.True(
                result.Contains(
                    "<p>Destination //!svn/wrk/15407bc3-2250-aa4c-aa51-4e65b2c824c3/BB !@#$%^&amp;()_-+={[}];',.~` has been created.</p>"));
        }

        [Fact]
        public void TestRequestDestinationWithCollection()
        {
            Results r = stubs.Attach(provider.CopyItem);
            request.Path = "http://localhost:8082/!svn/bc/5730/B%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60";
            request.Headers["Destination"] =
                "http://localhost:8084/tfserver/tfs/collection/$/!svn/wrk/15407bc3-2250-aa4c-aa51-4e65b2c824c3/BB%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60";

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            Assert.Equal("15407bc3-2250-aa4c-aa51-4e65b2c824c3", r.Parameters[0]);
        }

        [Fact]
        public void TestHandleProducesCorrectOutput()
        {
            Results r = stubs.Attach(provider.CopyItem);
            request.Path = "http://localhost:8082/!svn/bc/5522/File.txt";
            request.Headers["Destination"] =
                "http://localhost:8082//!svn/wrk/cdfcf93f-8649-5e44-a8ec-b3f40e10e907/FileRenamed.txt";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string expected =
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>201 Created</title>\n" +
                "</head><body>\n" +
                "<h1>Created</h1>\n" +
                "<p>Destination //!svn/wrk/cdfcf93f-8649-5e44-a8ec-b3f40e10e907/FileRenamed.txt has been created.</p>\n" +
                "<hr />\n" +
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8082</address>\n" +
                "</body></html>\n";
            Assert.Equal(expected, Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray()));
            Assert.Equal("text/html", response.ContentType);
            Assert.True(
                response.Headers.Contains(
                    new KeyValuePair<string, string>("Location",
                                                     "http://localhost:8082//!svn/wrk/cdfcf93f-8649-5e44-a8ec-b3f40e10e907/FileRenamed.txt")));
            Assert.Equal(1, r.CallCount);
            Assert.Equal("cdfcf93f-8649-5e44-a8ec-b3f40e10e907", r.Parameters[0]);
            Assert.Equal("/File.txt", r.Parameters[2]);
            Assert.Equal("/FileRenamed.txt", r.Parameters[3]);
        }

        [Fact]
        public void TestLocationResponseHeaderIsDecoded()
        {
            Results r = stubs.Attach(provider.CopyItem);
            request.Path = "http://localhost:8082/!svn/bc/5730/B%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60";
            request.Headers["Destination"] =
                "http://localhost:8084//!svn/wrk/15407bc3-2250-aa4c-aa51-4e65b2c824c3/BB%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            Assert.True(
                response.Headers.Contains(
                    new KeyValuePair<string, string>("Location",
                                                     "http://localhost:8084//!svn/wrk/15407bc3-2250-aa4c-aa51-4e65b2c824c3/BB !@#$%^&()_-+={[}];',.~`")));
        }

        [Fact]
        public void TestSourceControlProviderCalledCorrectlyWithSpecialCharactersInPath()
        {
            Results r = stubs.Attach(provider.CopyItem);
            request.Path = "http://localhost:8082/!svn/bc/5730/B%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60";
            request.Headers["Destination"] =
                "http://localhost:8084//!svn/wrk/15407bc3-2250-aa4c-aa51-4e65b2c824c3/BB%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            Assert.Equal(1, r.CallCount);
            Assert.Equal("15407bc3-2250-aa4c-aa51-4e65b2c824c3", r.Parameters[0]);
            Assert.Equal("/B !@#$%^&()_-+={[}];',.~`", r.Parameters[2]);
            Assert.Equal("/BB !@#$%^&()_-+={[}];',.~`", r.Parameters[3]);
        }
    }
}
