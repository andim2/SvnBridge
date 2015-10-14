using System;
using System.IO;
using System.Text;
using SvnBridge.Interfaces;
using Xunit;
using SvnBridge.Infrastructure;
using SvnBridge.SourceControl;
using SvnBridge.Utility;
using SvnBridge.Handlers;

namespace UnitTests
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
        public void Handle_FolderWithSpecialCharacters_ProperlyEncodesResults()
        {
            item.Name = "trunk/A !@#$%^&()_-+={[}];',.~`";
            item.ItemRevision = 5787;
            item.Author = "jwanagel";

            request.Path = "http://localhost:8080/!svn/bc/5787/trunk/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60";
            request.Input = "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";
            request.Headers["Depth"] = "0";

            string result = HandlerHandle(
                handler);

            Assert.True(result.Contains("<D:href>/!svn/bc/5787/trunk/A%20!@%23$%25%5e&amp;()_-+=%7b%5b%7d%5d%3b',.~%60/</D:href>\n"));
            Assert.True(result.Contains("<lp1:getetag>\"5787//trunk/A !@#$%^&amp;()_-+={[}];',.~`\"</lp1:getetag>"));
            Assert.True(result.Contains("<lp1:checked-in><D:href>/!svn/ver/5787/trunk/A%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60</D:href></lp1:checked-in>"));
            Assert.True(result.Contains("<lp2:baseline-relative-path>trunk/A !@#$%^&amp;()_-+={[}];',.~`</lp2:baseline-relative-path>"));
        }

        [Fact]
        public void Handle_AllPropCustomProperty()
        {
            request.Path = "http://localhost/!svn/bc/1234/Foo";
            request.Input = "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";
            request.Headers["Depth"] = "0";

            string result = HandlerHandle(
                handler);

            Assert.True(result.Contains("<C:bugtraq:message>Work Item: %BUGID%</C:bugtraq:message>"));
        }

        [Fact]
        public void Handle_AllPropSvnProperty()
        {
            request.Path = "http://localhost/!svn/bc/1234/Foo";
            request.Input = "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";
            request.Headers["Depth"] = "0";

            string result = HandlerHandle(
                handler);

            Assert.True(result.Contains("<S:ignore>*.log\n</S:ignore>"));
        }

        [Fact]
        public void Handle_AllPropBaselineRelativePath()
        {
            request.Path = "http://localhost/!svn/bc/1234/Foo";
            request.Input = "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";
            request.Headers["Depth"] = "0";

            string result = HandlerHandle(
                handler);

            Assert.True(result.Contains("<lp2:baseline-relative-path>Foo</lp2:baseline-relative-path>"));
        }

        [Fact]
        public void Handle_AllPropCheckedIn()
        {
            request.Path = "http://localhost/!svn/bc/1234/Foo";
            request.Input = "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";
            request.Headers["Depth"] = "0";

            string result = HandlerHandle(
                handler);

            Assert.True(result.Contains("<lp1:checked-in><D:href>/!svn/ver/1234/Foo</D:href></lp1:checked-in>"));
        }

        [Fact]
        public void Handle_AllPropContentType()
        {
            request.Path = "http://localhost/!svn/bc/1234/Foo";
            request.Input = "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";
            request.Headers["Depth"] = "0";

            string result = HandlerHandle(
                handler);
            Assert.True(result.Contains("<lp1:getcontenttype>text/html; charset=UTF-8</lp1:getcontenttype>"));
        }

        [Fact]
        public void Handle_AllPropCreationDate()
        {
            DateTime dt = DateTime.Now;
            item.LastModifiedDate = dt;
            request.Path = "http://localhost/!svn/bc/1234/Foo";
            request.Input = "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";
            request.Headers["Depth"] = "0";

            string result = HandlerHandle(
                handler);
            Assert.True(
                result.Contains("<lp1:creationdate>" + Helper.FormatDate(dt) + "</lp1:creationdate>"));
        }

        [Fact]
        public void Handle_AllPropCreatorDisplayName()
        {
            request.Path = "http://localhost/!svn/bc/1234/Foo";
            request.Input = "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";
            request.Headers["Depth"] = "0";

            string result = HandlerHandle(
                handler);
            Assert.True(result.Contains("<lp1:creator-displayname>user_foo</lp1:creator-displayname>"));
        }

        [Fact]
        public void Handle_AllPropDeadDropCount()
        {
            request.Path = "http://localhost/!svn/bc/1234/Foo";
            request.Input = "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";
            request.Headers["Depth"] = "0";

            string result = HandlerHandle(
                handler);
            Assert.True(result.Contains("<lp2:deadprop-count>2</lp2:deadprop-count>"));
        }

        [Fact]
        public void Handle_AllPropGetETag()
        {
            request.Path = "http://localhost/!svn/bc/1234/Foo";
            request.Input = "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";
            request.Headers["Depth"] = "0";

            string result = HandlerHandle(
                handler);
            Assert.True(result.Contains("<lp1:getetag>\"1234//Foo\"</lp1:getetag>"));
        }

        [Fact]
        public void Handle_AllPropGetLastModified()
        {
            DateTime dt = DateTime.Now;
            item.LastModifiedDate = dt;
            request.Path = "http://localhost/!svn/bc/1234/Foo";
            request.Input = "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";
            request.Headers["Depth"] = "0";

            string result = HandlerHandle(
                handler);
            Assert.True(
                result.Contains("<lp1:getlastmodified>" + dt.ToUniversalTime().ToString("R") + "</lp1:getlastmodified>"));
        }

        [Fact]
        public void Handle_AllPropRepositoryUuid()
        {
            request.Path = "http://localhost/!svn/bc/1234/Foo";
            request.Input = "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";
            request.Headers["Depth"] = "0";

            string result = HandlerHandle(
                handler);
            Assert.True(
                result.Contains("<lp2:repository-uuid>81a5aebe-f34e-eb42-b435-ac1ecbb335f7</lp2:repository-uuid>"));
        }

        [Fact]
        public void Handle_AllPropResourceType()
        {
            request.Path = "http://localhost/!svn/bc/1234/Foo";
            request.Input = "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";
            request.Headers["Depth"] = "0";

            string result = HandlerHandle(
                handler);
            Assert.True(result.Contains("<lp1:resourcetype><D:collection/></lp1:resourcetype>"));
        }

        [Fact]
        public void Handle_AllPropVersionControlledConfiguration()
        {
            request.Path = "http://localhost/!svn/bc/1234/Foo";
            request.Input = "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";
            request.Headers["Depth"] = "0";

            string result = HandlerHandle(
                handler);
            Assert.True(
                result.Contains(
                    "<lp1:version-controlled-configuration><D:href>/!svn/vcc/default</D:href></lp1:version-controlled-configuration>"));
        }

        [Fact]
        public void Handle_AllPropVersionName()
        {
            request.Path = "http://localhost/!svn/bc/1234/Foo";
            request.Input = "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";
            request.Headers["Depth"] = "0";

            string result = HandlerHandle(
                handler);
            Assert.True(result.Contains("<lp1:version-name>1234</lp1:version-name>"));
        }

        [Fact]
        public void Handle_AllPropOnFolderWithSpaces_CorrectlyEncodesHrefElement()
        {
            request.Path = "http://localhost:8080/!svn/bc/5784/Quick%20Starts";
            request.Input = "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";
            request.Headers["Depth"] = "0";

            string result = HandlerHandle(
                handler);
            Assert.True(result.Contains("<D:href>/!svn/bc/5784/Quick%20Starts/</D:href>"));
        }
    }
}
