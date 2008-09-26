using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Attach;
using SvnBridge.Interfaces;
using Xunit;
using SvnBridge.Infrastructure;
using SvnBridge.PathParsing;
using SvnBridge.SourceControl;

namespace SvnBridge.Handlers
{
    public class PutHandlerTests : HandlerTestsBase
    {
        protected PutHandler handler = new PutHandler();

        [Fact]
        public void TestCorrectOutput()
        {
            Results r = stubs.Attach(provider.WriteFile, true);
            request.Path =
                "http://localhost:8082//!svn/wrk/be3dd5c3-e77f-f246-a1e8-640012b047a2/Spikes/SvnFacade/trunk/New%20Folder%207/Empty%20File%202.txt";
            request.Input = "SVN\0";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);
            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());

            string expected =
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>201 Created</title>\n" +
                "</head><body>\n" +
                "<h1>Created</h1>\n" +
                "<p>Resource //!svn/wrk/be3dd5c3-e77f-f246-a1e8-640012b047a2/Spikes/SvnFacade/trunk/New Folder 7/Empty File 2.txt has been created.</p>\n" +
                "<hr />\n" +
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8082</address>\n" +
                "</body></html>\n";
            Assert.Equal(expected, result);
            Assert.Equal(201, response.StatusCode);
            Assert.Equal("text/html", response.ContentType);
            Assert.True(
                response.Headers.Contains(
                    new KeyValuePair<string, string>("Location",
                                                     "http://localhost:8082//!svn/wrk/be3dd5c3-e77f-f246-a1e8-640012b047a2/Spikes/SvnFacade/trunk/New Folder 7/Empty File 2.txt")));
        }

        [Fact]
        public void TestPathIsDecodedWhenInvokingSourceControlProviderForFolderPath()
        {
            Results r = stubs.Attach(provider.WriteFile, false);
            request.Path =
                "http://localhost:8082//!svn/wrk/be3dd5c3-e77f-f246-a1e8-640012b047a2/Spikes/SvnFacade/trunk/New%20Folder%207/Empty%20File%202.txt";
            request.Input = "SVN\0";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            Assert.Equal("/Spikes/SvnFacade/trunk/New Folder 7/Empty File 2.txt", r.Parameters[1]);
        }

        [Fact]
        public void TestResourceIsProperlyEncoded()
        {
            Results r = stubs.Attach(provider.WriteFile, true);
            request.Path =
                "http://localhost:8082//!svn/wrk/b50ca3a0-05d8-5b4d-8b51-11fce9cbc603/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/B%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/C%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60..txt";
            request.Input = "SVN\0";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);
            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());

            Assert.True(
                result.Contains(
                    "Resource //!svn/wrk/b50ca3a0-05d8-5b4d-8b51-11fce9cbc603/A !@#$%^&amp;()_-+={[}];',.~`/B !@#$%^&amp;()_-+={[}];',.~`/C !@#$%^&amp;()_-+={[}];',.~`..txt has been created."));
        }

        [Fact]
        public void TestThrowsExceptionIfBaseFileDoesNotMatchChecksum()
        {
            stubs.Attach(provider.GetItemInActivity, new ItemMetaData());
            stubs.AttachReadFile(provider.ReadFile, new byte[] { });
            request.Path = "http://localhost:8082//!svn/wrk/61652fe8-44cd-8d43-810f-c95deccc6db3/Test.txt";
            request.Input = "SVN\0\0\u0004\u0008\u0001\u0008\u0088bbbb111a";
            request.Headers["X-SVN-Base-Fulltext-MD5"] = "65ba841e01d6db7733e90a5b7f9e6f80";

            Exception result = Record.Exception(delegate { handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null); });

            Assert.IsType(typeof(Exception), result);
            Assert.Equal("Checksum mismatch with base file", result.Message);
        }

        [Fact]
        public void TestThrowsExceptionIfBaseFileDoesNotMatchChecksumWhenUpdateToEmptyFile()
        {
            stubs.Attach(provider.GetItemInActivity, new ItemMetaData());
            stubs.AttachReadFile(provider.ReadFile, new byte[] { });
            request.Path = "http://localhost:8082//!svn/wrk/61652fe8-44cd-8d43-810f-c95deccc6db3/Test.txt";
            request.Input = "SVN\0";
            request.Headers["X-SVN-Base-Fulltext-MD5"] = "65ba841e01d6db7733e90a5b7f9e6f80";

			Exception result = Record.Exception(delegate { handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null); });

            Assert.IsType(typeof(Exception), result);
            Assert.Equal("Checksum mismatch with base file", result.Message);
        }
    }
}
