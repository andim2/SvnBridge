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
        protected GetHandler handler = new GetHandler(false);

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

            string result = Encoding.Default.GetString(((MemoryStream)response.OutputStream).ToArray());

            // Note that this output used to get corrected
            // to contain CRLF rather than formerly LF-only lines,
            // but once the generator then was updated to use a central helper
            // for human-readable purposes the test result broke.
            // However since I don't think that CRLF is correct
            // (all other occasions didn't use it, and there is no comment/commit
            // explicitly mentioning that 160013 perhaps does need CRLF output),
            // I decided that it's likely to be the *test* which is incorrect,
            // thus I reverted it.
            // If that turns out to be wrong, then perhaps our HumanReadable
            // helper ought to get a bool full_crlf param which activates both
            // CRLF and last-line non-LF.
            string expected = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                              "<D:error xmlns:D=\"DAV:\" xmlns:m=\"http://apache.org/dav/xmlns\" xmlns:C=\"svn:\">\n" +
                              "<C:error/>\n" +
                              "<m:human-readable errcode=\"160013\">\n" +
                              "Path does not exist in repository.\n" +
                              "</m:human-readable>\n" +
                              "</D:error>\n";
            Assert.Equal(400, response.StatusCode);
            Assert.Equal("text/xml; charset=\"utf-8\"", response.ContentType);
            Assert.Equal(expected, result);
        }
    }
}
