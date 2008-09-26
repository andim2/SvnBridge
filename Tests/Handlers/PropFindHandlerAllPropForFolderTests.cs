using System;
using System.IO;
using System.Text;
using SvnBridge.Interfaces;
using Xunit;
using SvnBridge.Infrastructure;
using SvnBridge.PathParsing;
using SvnBridge.SourceControl;
using SvnBridge.Utility;

namespace SvnBridge.Handlers
{
    public class PropFindHandlerAllPropForFolderTests : HandlerTestsBase
    {
        public PropFindHandlerAllPropForFolderTests()
        {
            handler = new PropFindHandler();

            item = new FolderMetaData();
            item.Name = "Foo";
            item.ItemRevision = 1234;
            item.Author = "user_foo";
            item.Properties.Add("bugtraq:message", "Work Item: %BUGID%");
            item.Properties.Add("svn:ignore", "*.log\n");
            item.LastModifiedDate = DateTime.Now.ToUniversalTime();
            stubs.Attach(provider.GetItems, item);
        }

        private PropFindHandler handler;
        private FolderMetaData item;

        [Fact]
        public void TestAllPropCustomProperty()
        {
            request.Path = "http://localhost/!svn/bc/1234/Foo";
            request.Input = "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";
            request.Headers["Depth"] = "0";

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream)response.OutputStream).ToArray());
            Assert.True(result.Contains("<C:bugtraq:message>Work Item: %BUGID%</C:bugtraq:message>"));
        }

        [Fact]
        public void TestAllPropSvnProperty()
        {
            request.Path = "http://localhost/!svn/bc/1234/Foo";
            request.Input = "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";
            request.Headers["Depth"] = "0";

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream)response.OutputStream).ToArray());
            Assert.True(result.Contains("<S:ignore>*.log\n</S:ignore>"));
        }

        [Fact]
        public void TestAllPropBaselineRelativePath()
        {
            request.Path = "http://localhost/!svn/bc/1234/Foo";
            request.Input = "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";
            request.Headers["Depth"] = "0";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(result.Contains("<lp2:baseline-relative-path>Foo</lp2:baseline-relative-path>"));
        }

        [Fact]
        public void TestAllPropCheckedIn()
        {
            request.Path = "http://localhost/!svn/bc/1234/Foo";
            request.Input = "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";
            request.Headers["Depth"] = "0";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(result.Contains("<lp1:checked-in><D:href>/!svn/ver/1234/Foo</D:href></lp1:checked-in>"));
        }

        [Fact]
        public void TestAllPropContentType()
        {
            request.Path = "http://localhost/!svn/bc/1234/Foo";
            request.Input = "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";
            request.Headers["Depth"] = "0";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(result.Contains("<lp1:getcontenttype>text/html; charset=UTF-8</lp1:getcontenttype>"));
        }

        [Fact]
        public void TestAllPropCreationDate()
        {
            DateTime dt = DateTime.Now;
            item.LastModifiedDate = dt;
            request.Path = "http://localhost/!svn/bc/1234/Foo";
            request.Input = "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";
            request.Headers["Depth"] = "0";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(
                result.Contains("<lp1:creationdate>" + Helper.FormatDate(dt) + "</lp1:creationdate>"));
        }

        [Fact]
        public void TestAllPropCreatorDisplayName()
        {
            request.Path = "http://localhost/!svn/bc/1234/Foo";
            request.Input = "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";
            request.Headers["Depth"] = "0";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(result.Contains("<lp1:creator-displayname>user_foo</lp1:creator-displayname>"));
        }

        [Fact]
        public void TestAllPropDeadDropCount()
        {
            request.Path = "http://localhost/!svn/bc/1234/Foo";
            request.Input = "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";
            request.Headers["Depth"] = "0";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(result.Contains("<lp2:deadprop-count>2</lp2:deadprop-count>"));
        }

        [Fact]
        public void TestAllPropGetETag()
        {
            request.Path = "http://localhost/!svn/bc/1234/Foo";
            request.Input = "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";
            request.Headers["Depth"] = "0";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(result.Contains("<lp1:getetag>W/\"1234//Foo\"</lp1:getetag>"));
        }

        [Fact]
        public void TestAllPropGetLastModified()
        {
            DateTime dt = DateTime.Now;
            item.LastModifiedDate = dt;
            request.Path = "http://localhost/!svn/bc/1234/Foo";
            request.Input = "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";
            request.Headers["Depth"] = "0";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(
                result.Contains("<lp1:getlastmodified>" + dt.ToUniversalTime().ToString("R") + "</lp1:getlastmodified>"));
        }

        [Fact]
        public void TestAllPropRepositoryUuid()
        {
            request.Path = "http://localhost/!svn/bc/1234/Foo";
            request.Input = "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";
            request.Headers["Depth"] = "0";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(
                result.Contains("<lp2:repository-uuid>81a5aebe-f34e-eb42-b435-ac1ecbb335f7</lp2:repository-uuid>"));
        }

        [Fact]
        public void TestAllPropResourceType()
        {
            request.Path = "http://localhost/!svn/bc/1234/Foo";
            request.Input = "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";
            request.Headers["Depth"] = "0";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(result.Contains("<lp1:resourcetype><D:collection/></lp1:resourcetype>"));
        }

        [Fact]
        public void TestAllPropVersionControlledConfiguration()
        {
            request.Path = "http://localhost/!svn/bc/1234/Foo";
            request.Input = "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";
            request.Headers["Depth"] = "0";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(
                result.Contains(
                    "<lp1:version-controlled-configuration><D:href>/!svn/vcc/default</D:href></lp1:version-controlled-configuration>"));
        }

        [Fact]
        public void TestAllPropVersionName()
        {
            request.Path = "http://localhost/!svn/bc/1234/Foo";
            request.Input = "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";
            request.Headers["Depth"] = "0";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(result.Contains("<lp1:version-name>1234</lp1:version-name>"));
        }
    }
}
