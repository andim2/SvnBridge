using System.IO;
using System.Text;
using Attach;
using SvnBridge.Interfaces;
using Xunit;
using SvnBridge.Infrastructure;
using SvnBridge.PathParsing;
using SvnBridge.SourceControl;
using System;
using SvnBridge.Utility;

namespace SvnBridge.Handlers
{
    public class GetHandlerTests : HandlerTestsBase
    {
        protected GetHandler handler = new GetHandler();

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

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string expected = "asdf";
            Assert.Equal(expected, Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray()));
            Assert.Equal("text/plain", response.ContentType);
            Assert.Equal(Helper.FormatDateB(Clock.Now), response.GetHeader("Last-Modified"));
            Assert.Equal("\"1234//Foo/Bar.txt\"", response.GetHeader("ETag"));
            Assert.Equal("bytes", response.GetHeader("Accept-Ranges"));
        }

        [Fact]
        public void Handle_CorrectInvokesSourceControlProvide()
        {
            ItemMetaData item = new ItemMetaData();
            item.Name = "Foo/Bar.txt";
            Results getItemsResult = stubs.Attach(provider.GetItems, item);
            Results readFileResult = stubs.AttachReadFile(provider.ReadFile, Encoding.Default.GetBytes("asdf"));
            request.Path = "http://localhost:8082/!svn/bc/1234/Foo/Bar.txt";

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            Assert.Equal(1, getItemsResult.CallCount);
            Assert.Equal(1234, getItemsResult.Parameters[0]);
            Assert.Equal("/Foo/Bar.txt", getItemsResult.Parameters[1]);
            Assert.Equal(1, readFileResult.CallCount);
            Assert.Equal(item, readFileResult.Parameters[0]);
        }
    }
}
