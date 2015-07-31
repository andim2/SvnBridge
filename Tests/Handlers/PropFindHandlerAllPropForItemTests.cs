using System;
using System.IO;
using System.Text;
using System.Web;
using SvnBridge.Interfaces;
using Xunit;
using SvnBridge.Infrastructure;
using SvnBridge.PathParsing;
using SvnBridge.SourceControl;
using SvnBridge.Utility;
using SvnBridge.Handlers;

namespace UnitTests
{
    public class PropFindHandlerAllPropForItemTests : HandlerTestsBase
    {
        public PropFindHandlerAllPropForItemTests()
        {
            handler = new PropFindHandler();

            item = new ItemMetaData();
            item.Name = "Foo/Bar.txt";
            item.ItemRevision = 1234;
            item.Author = "user_foo";
            item.LastModifiedDate = DateTime.Parse("2007-08-14T23:08:22.908519Z");
            stubs.Attach(provider.GetItems, item);
            stubs.AttachReadFile(provider.ReadFile, new byte[4] { 0, 1, 2, 3 });
        }

        private PropFindHandler handler;
        private ItemMetaData item;

        private void ArrangeRequest()
        {
            request.Path = "http://localhost/!svn/bc/1234/Foo/Bar.txt";
            request.Input = "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";
            request.Headers["Depth"] = "0";
        }

        [Fact]
        public void Handle_PathContainsSpecialCharacters_OutputIsProperlyEncoded()
        {
            ArrangeRequest();
            request.Path = "http://localhost/!svn/bc/5775/trunk/H%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60.txt";
            item.ItemRevision = 5774;
            item.Name = "trunk/H !@#$%^&()_-+={[}];',.~`.txt";
            stubs.Attach(provider.GetItems, item);

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream)response.OutputStream).ToArray());
            Assert.True(result.Contains("<D:href>/!svn/bc/5775/trunk/H%20!@%23$%25%5e&amp;()_-+=%7b%5b%7d%5d%3b',.~%60.txt</D:href>"));
            Assert.True(result.Contains("<lp1:getetag>\"5774//trunk/H !@#$%^&amp;()_-+={[}];',.~`.txt\"</lp1:getetag>"));
            Assert.True(result.Contains("<lp1:checked-in><D:href>/!svn/ver/5774/trunk/H%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60.txt</D:href></lp1:checked-in>"));
            Assert.True(result.Contains("<lp2:baseline-relative-path>trunk/H !@#$%^&amp;()_-+={[}];',.~`.txt</lp2:baseline-relative-path>"));
        }

        [Fact]
        public void TestBaselineRelativePath()
        {
            ArrangeRequest();

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(result.Contains("<lp2:baseline-relative-path>Foo/Bar.txt</lp2:baseline-relative-path>"));
        }

        [Fact]
        public void TestCheckedIn()
        {
            ArrangeRequest();

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(
                result.Contains("<lp1:checked-in><D:href>/!svn/ver/1234/Foo/Bar.txt</D:href></lp1:checked-in>"));
        }

        [Fact]
        public void Handle_ReturnsContentLength()
        {
            ArrangeRequest();

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream)response.OutputStream).ToArray());
            Assert.True(result.Contains("<lp1:getcontentlength>4</lp1:getcontentlength>"));
        }

        [Fact]
        public void TestContentType()
        {
            ArrangeRequest();

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(result.Contains("<lp1:getcontenttype>text/plain</lp1:getcontenttype>"));
        }

        [Fact]
        public void TestCreationDate()
        {
            DateTime dt = DateTime.Now;
            item.LastModifiedDate = dt;
            ArrangeRequest();

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(result.Contains("<lp1:creationdate>" + Helper.FormatDate(dt) + "</lp1:creationdate>"));
        }

        [Fact]
        public void TestCreatorDisplayName()
        {
            ArrangeRequest();

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(result.Contains("<lp1:creator-displayname>user_foo</lp1:creator-displayname>"));
        }

        [Fact]
        public void TestDeadDropCount()
        {
            ArrangeRequest();

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(result.Contains("<lp2:deadprop-count>0</lp2:deadprop-count>"));
        }

        [Fact]
        public void TestGetETag()
        {
            ArrangeRequest();

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(result.Contains("<lp1:getetag>\"1234//Foo/Bar.txt\"</lp1:getetag>"));
        }

        [Fact]
        public void TestGetLastModified()
        {
            DateTime dt = DateTime.Now;
            item.LastModifiedDate = dt;
            ArrangeRequest();

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(
                result.Contains("<lp1:getlastmodified>" + dt.ToUniversalTime().ToString("R") + "</lp1:getlastmodified>"));
        }

        [Fact]
        public void TestHref()
        {
            ArrangeRequest();

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(result.Contains("<D:href>/!svn/bc/1234/Foo/Bar.txt</D:href>"));
        }

        //[Fact(Skip="Temporary disable")] // reenabled since it does not fail and I cannot see why it has been disabled...
        public void TestHref_WithUnicode()
        {
            ArrangeRequest();
            request.Path = "http://localhost/!svn/bc/1234/Foo/שלום.txt";

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream)response.OutputStream).ToArray());
            Assert.True(result.Contains("<D:href>" + Helper.UrlEncodeIfNeccesary("/!svn/bc/1234/Foo/שלום.txt") + "</D:href>"));
        }

        [Fact]
        public void TestLockDiscovery()
        {
            ArrangeRequest();

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(result.Contains("<D:lockdiscovery/>"));
        }

        [Fact]
        public void TestMD5Checksum()
        {
            ArrangeRequest();

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(
                result.Contains("<lp2:md5-checksum>" + Helper.GetMd5Checksum(new byte[4] {0, 1, 2, 3}) +
                                "</lp2:md5-checksum>"));
        }

        [Fact]
        public void TestRepositoryUuid()
        {
            ArrangeRequest();

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(
                result.Contains("<lp2:repository-uuid>81a5aebe-f34e-eb42-b435-ac1ecbb335f7</lp2:repository-uuid>"));
        }

        [Fact]
        public void TestResourceType()
        {
            ArrangeRequest();

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(result.Contains("<lp1:resourcetype/>"));
        }

        [Fact]
        public void TestSupportedLock()
        {
            ArrangeRequest();

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(result.Contains(
                              "<D:supportedlock>\n" +
                              "<D:lockentry>\n" +
                              "<D:lockscope><D:exclusive/></D:lockscope>\n" +
                              "<D:locktype><D:write/></D:locktype>\n" +
                              "</D:lockentry>\n" +
                              "</D:supportedlock>\n"));
        }

        [Fact]
        public void TestVersionControlledConfiguration()
        {
            ArrangeRequest();

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(
                result.Contains(
                    "<lp1:version-controlled-configuration><D:href>/!svn/vcc/default</D:href></lp1:version-controlled-configuration>"));
        }

        [Fact]
        public void TestVersionName()
        {
            ArrangeRequest();

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(result.Contains("<lp1:version-name>1234</lp1:version-name>"));
        }
    }
}
