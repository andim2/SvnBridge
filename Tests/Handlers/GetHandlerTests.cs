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
using SvnBridge.Handlers;

namespace UnitTests
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
        public void Handle_UnicodeFile_ReturnsCorrectResponse()
        {
            ItemMetaData item = new ItemMetaData();
            item.Name = "Foo/Bar.txt";
            item.ItemRevision = 1234;
            Clock.FrozenCurrentTime = DateTime.Now;
            item.LastModifiedDate = Clock.Now;
            Results getItemsResult = stubs.Attach(provider.GetItems, item);
            byte[] fileData = new byte[] { 110, 160, 70 };
            Results readFileResult = stubs.AttachReadFile(provider.ReadFile, fileData);
            request.Path = "http://localhost:8082/!svn/bc/1234/Foo/Bar.txt";

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            Assert.Equal(fileData, ((MemoryStream)response.OutputStream).ToArray());
        }

        [Fact]
        public void Handle_CorrectInvokesSourceControlProvider()
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

        [Fact]
        public void Handle_ReturnsCorrect404WhenPathContainsMercurialConvertPath()
        {
            request.Path = "http://localhost:8082/!svn/ver/0/.svn";

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string expected = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                              "<D:error xmlns:D=\"DAV:\" xmlns:m=\"http://apache.org/dav/xmlns\" xmlns:C=\"svn:\">\r\n" +
                              "<C:error/>\r\n" +
                              "<m:human-readable errcode=\"160013\">\r\n" +
                              "Path does not exist in repository.\r\n" +
                              "</m:human-readable>\r\n" +
                              "</D:error>";
            Assert.Equal(400, response.StatusCode);
            Assert.Equal("text/xml; charset=\"utf-8\"", response.ContentType);
            Assert.Equal(expected, Encoding.Default.GetString(((MemoryStream)response.OutputStream).ToArray()));
        }
    }
}
