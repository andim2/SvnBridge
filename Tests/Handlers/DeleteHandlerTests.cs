using System.IO;
using System.Text;
using Attach;
using SvnBridge.Interfaces;
using Xunit;
using SvnBridge.Infrastructure;
using SvnBridge.SourceControl;
using SvnBridge.Handlers;

namespace UnitTests
{
    public class DeleteHandlerTests : HandlerTestsBase
    {
        protected DeleteHandler handler = new DeleteHandler();

        [Fact]
        public void VerifyHandleCorrectlyInvokesSourceControlProviderForDeleteActivity()
        {
            Results r = stubs.Attach(provider.DeleteActivity);
            request.Path = "http://localhost:8082/!svn/act/5b34ae67-87de-3741-a590-8bda26893532";

            HandlerHandle(
                handler);

            Assert.Equal(1, r.CallCount);
            Assert.Equal("5b34ae67-87de-3741-a590-8bda26893532", r.Parameters[0]);
        }

        [Fact]
        public void VerifyHandleCorrectlyInvokesSourceControlProviderForDeleteFile()
        {
            Results r = stubs.Attach(provider.DeleteItem, true);
            request.Path =
                "http://localhost:8082//!svn/wrk/c512ecbe-7577-ce46-939c-a9e81eb4d98e/Spikes/SvnFacade/trunk/Test4.txt";

            HandlerHandle(
                handler);

            Assert.Equal(1, r.CallCount);
            Assert.Equal("c512ecbe-7577-ce46-939c-a9e81eb4d98e", r.Parameters[0]);
            Assert.Equal("/Spikes/SvnFacade/trunk/Test4.txt", r.Parameters[1]);
        }

        [Fact]
        public void VerifyHandleDecodesPathWhenInvokingSourceControlProviderForDeleteItem()
        {
            Results r = stubs.Attach(provider.DeleteItem, true);
            request.Path =
                "http://localhost:8082//!svn/wrk/125c1a75-a7a6-104d-a661-54689d30dc99/Spikes/SvnFacade/trunk/New%20Folder%206";

            HandlerHandle(
                handler);

            Assert.Equal("/Spikes/SvnFacade/trunk/New Folder 6", r.Parameters[1]);
        }

        [Fact]
        public void VerifyHandleReturnsCorrectResponseWhenDeleteFileNotFound()
        {
            Results r = stubs.Attach(provider.DeleteItem, false);
            request.Path =
                "http://localhost:8082//!svn/wrk/70df3104-9f67-8d4e-add7-6012fe86c03a/Spikes/SvnFacade/trunk/New%20Folder/Test2.txt";

            string result = HandlerHandle(
                handler);

            string expected =
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>404 Not Found</title>\n" +
                "</head><body>\n" +
                "<h1>Not Found</h1>\n" +
                "<p>The requested URL //!svn/wrk/70df3104-9f67-8d4e-add7-6012fe86c03a/Spikes/SvnFacade/trunk/New Folder/Test2.txt was not found on this server.</p>\n" +
                "<hr>\n" +
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8082</address>\n" +
                "</body></html>\n";
            Assert.Equal(expected, result);
            Assert.Equal(404, response.StatusCode);
            Assert.Equal("text/html; charset=iso-8859-1", response.ContentType);
        }
    }
}
