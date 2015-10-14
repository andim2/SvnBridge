using System.IO;
using System.Text;
using Attach;
using SvnBridge.Interfaces;
using Xunit;
using SvnBridge.Infrastructure;
using SvnBridge.SourceControl;
using System;
using SvnBridge.Utility;
using SvnBridge.Handlers;

namespace UnitTests
{
    public class HeadHandlerTests : HandlerTestsBase
    {
        /* Instantiate a GetHandler object in HEAD mode. SVN clients >= 1.8.2 use it for checking whether a file
         * exists remotely. This is more efficient as the server only has to send up the HTTP headers and not
         * the whole file contents */
        protected GetHandler handler = new GetHandler(true);

        [Fact]
        public void Handle_ReturnsCorrectResponse()
        {
            ItemMetaData item = new ItemMetaData();
            item.Name = "Foo/Bar.txt";
            item.ItemRevision = 1234;
            Clock.FrozenCurrentTime = DateTime.Now;
            item.LastModifiedDate = Clock.Now;
            Results getItemsResult = stubs.Attach(provider.GetItems, item);
            Results readFileResult = stubs.AttachReadFile(provider.ReadFile, Encoding.Default.GetBytes("asdf"));
            request.Path = "http://localhost:8082/!svn/bc/1234/Foo/Bar.txt";

            string result = HandlerHandle(
                handler);

            // We only expect an HTTP header back from the server, response should be empty
            string expected = string.Empty;
            Assert.Equal(expected, result);
            Assert.Equal("text/plain", response.ContentType);
            Assert.Equal(Helper.FormatDateB(Clock.Now), response.GetHeader("Last-Modified"));
            Assert.Equal("\"1234//Foo/Bar.txt\"", response.GetHeader("ETag"));
            Assert.Equal("bytes", response.GetHeader("Accept-Ranges"));
        }

        [Fact]
        public void Handle_ReturnsCorrectResponseFileDoesNotExist()
        {
            ItemMetaData item = null;
            Results getItemsResult = stubs.Attach(provider.GetItems, item);
            Results readFileResult = stubs.AttachReadFile(provider.ReadFile, Encoding.Default.GetBytes("asdf"));
            request.Path = "http://localhost:8082/!svn/bc/1234/Foo/NotFound.txt";

            HandlerHandle(
                handler);

            // In this case, we get a 404 Not Found code
            Assert.Equal(404, response.StatusCode);
        }

        [Fact]
        public void Handle_CorrectInvokesSourceControlProvider()
        {
            ItemMetaData item = new ItemMetaData();
            item.Name = "Foo/Bar.txt";
            Results getItemsResult = stubs.Attach(provider.GetItems, item);
            Results readFileResult = stubs.AttachReadFile(provider.ReadFile, Encoding.Default.GetBytes("asdf"));
            request.Path = "http://localhost:8082/!svn/bc/1234/Foo/Bar.txt";

            HandlerHandle(
                handler);

            Assert.Equal(1, getItemsResult.CallCount);
            Assert.Equal(1234, getItemsResult.Parameters[0]);
            Assert.Equal("/Foo/Bar.txt", getItemsResult.Parameters[1]);

            /* The HTTP spec doesn't enforce SvnBridge to send a content-length value back to the client for HEAD requests,
             * nor it needs to send the file's contents. The outcome is that SvnBridge doesn't have the need to call the TFS server */
            Assert.Equal(0, readFileResult.CallCount);
        }
    }
}
