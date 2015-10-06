using System;
using System.IO;
using System.Text;
using Attach;
using SvnBridge.Interfaces;
using Xunit;
using SvnBridge.Infrastructure;
using SvnBridge.Nodes;
using SvnBridge.PathParsing;
using SvnBridge.SourceControl;
using SvnBridge.Utility;
using SvnBridge.Handlers;
using Tests;
using SvnBridge.Net;

namespace UnitTests
{
    public class PropFindHandlerTests : HandlerTestsBase
    {
        protected PropFindHandler handler;

    	public PropFindHandlerTests()
    	{
    		handler = new PropFindHandler();
            handler.Initialize(context, new PathParserSingleServerWithProjectInPath(tfsUrl));
    	}

        [Fact]
        public void TestBcFileNodeHrefForFolder()
        {
            ItemMetaData item = new ItemMetaData();
            item.Name = "Foo/Bar.txt";
            stubs.Attach(provider.GetItems, item);

            BcFileNode node = new BcFileNode(1234, item, provider);

            string expected = "/!svn/bc/1234/Foo/Bar.txt";
            Assert.Equal(expected, node.Href(handler));
        }

        [Fact]
        public void TestBcMd5Checksum()
        {
            stubs.Attach(provider.ItemExists, true);
            ItemMetaData item = new ItemMetaData();
            item.Name = "Foo/Bar.txt";
            stubs.Attach(provider.GetItems, item);
            stubs.AttachReadFile(provider.ReadFile, new byte[4] {0, 1, 2, 3});

            request.Path = "http://localhost:8082/!svn/bc/1234/Foo/Bar.txt";
            request.Input =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:propfind xmlns:D='DAV:'><D:prop><D:md5-checksum/></D:prop></D:propfind>";
            request.Headers["Depth"] = "0";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(
                result.Contains("<lp2:md5-checksum>" + Helper.GetMd5Checksum(new byte[4] {0, 1, 2, 3}) +
                                "</lp2:md5-checksum>"));
        }

        [Fact]
        public void TestCorrectlyInvokesProviderWithBcPathAndDepthOne()
        {
            stubs.Attach(provider.ItemExists, true);
            stubs.Attach(provider.IsDirectory, true);
            FolderMetaData folder = new FolderMetaData();
            folder.Name = "Foo";
            Results results = stubs.Attach(provider.GetItems, folder);
            request.Path = "http://localhost:8082/!svn/bc/1234/Foo";
            request.Input =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><resourcetype xmlns=\"DAV:\"/></prop></propfind>";
            request.Headers["Depth"] = "1";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            Assert.Equal(1, results.CallCount);
            Assert.Equal(1234, results.Parameters[0]);
            Assert.Equal("/Foo", results.Parameters[1]);
            Assert.Equal(Recursion.OneLevel, results.Parameters[2]);
        }

        [Fact]
        public void TestInvalidDepthThrowsEx()
        {
            stubs.Attach(provider.ItemExists, true);
            request.Path = "http://localhost:8082/Folder%20With%20Spaces";
            request.Input =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><baseline-relative-path xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/></prop></propfind>";
            request.Headers["Depth"] = "2";

			Exception result = Record.Exception(delegate { handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null); });

            Assert.IsType(typeof(InvalidOperationException), result);
        }

        [Fact]
        public void TestPropFindLockDiscovery()
        {
            stubs.Attach(provider.ItemExists, true);
            ItemMetaData item = new ItemMetaData();
            item.Name = "Foo";
            stubs.Attach(provider.GetItems, item);

            request.Path = "http://localhost:8082/!svn/bc/1234/Foo";
            request.Input =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:propfind xmlns:D='DAV:'><D:prop><D:lockdiscovery/></D:prop></D:propfind>";
            request.Headers["Depth"] = "0";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(result.Contains("<D:lockdiscovery/>"));
        }

        [Fact]
        public void TestPropFindWithDepthOneIncludesFolderAndChildren()
        {
            stubs.Attach(provider.ItemExists, true);
            stubs.Attach(provider.IsDirectory, Return.MultipleValues(true, false));
            FolderMetaData folder = new FolderMetaData();
            folder.Name = "Foo";
            stubs.Attach(provider.GetItems, folder);
            ItemMetaData item = new ItemMetaData();
            item.Name = "Foo/Bar.txt";
            folder.Items.Add(item);
            request.Path = "http://localhost:8082/!svn/bc/1234/Foo";
            request.Input =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><resourcetype xmlns=\"DAV:\"/></prop></propfind>";
            request.Headers["Depth"] = "1";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(result.Contains("<D:href>/!svn/bc/1234/Foo/</D:href>"));
            Assert.True(result.Contains("<D:href>/!svn/bc/1234/Foo/Bar.txt</D:href>"));
        }

        [Fact]
        public void VerifyBaselineRelativePathPropertyForFolderReturnsDecoded()
        {
            stubs.Attach(provider.ItemExists, true);
            FolderMetaData item = new FolderMetaData();
            item.Name = "Folder With Spaces";
            stubs.Attach(provider.GetItems, item);
            request.Path = "http://localhost:8082/Folder%20With%20Spaces";
            request.Input =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><baseline-relative-path xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/></prop></propfind>";
            request.Headers["Depth"] = "0";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);
            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());

            string expected = "<lp2:baseline-relative-path>Folder With Spaces</lp2:baseline-relative-path>";
            Assert.True(result.Contains(expected));
        }

        [Fact]
        public void VerifyCreationDate()
        {
            stubs.Attach(provider.ItemExists, true);
            ItemMetaData item = new ItemMetaData();
            item.Name = "Foo";
            item.ItemRevision = 1234;
            item.Author = "user_foo";
            DateTime dt = DateTime.Now.ToUniversalTime();
            item.LastModifiedDate = dt;
            stubs.Attach(provider.GetItems, item);
            request.Path = "http://localhost:8082/!svn/bc/1234/Foo";
            request.Input =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><creationdate xmlns=\"DAV:\"/></prop></propfind>";
            request.Headers["Depth"] = "0";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(
                result.Contains("<lp1:creationdate>" + Helper.FormatDate(dt.ToUniversalTime()) + "</lp1:creationdate>"));
        }

        [Fact]
        public void VerifyCreatorDisplayName()
        {
            stubs.Attach(provider.ItemExists, true);
            FolderMetaData item = new FolderMetaData();
            item.Name = "Foo";
            item.ItemRevision = 1234;
            item.Author = "user_foo";
            item.LastModifiedDate = DateTime.Now.ToUniversalTime();
            stubs.Attach(provider.GetItems, item);
            request.Path = "http://localhost:8082/!svn/bc/1234/Foo";
            request.Input =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><creator-displayname xmlns=\"DAV:\"/></prop></propfind>";
            request.Headers["Depth"] = "0";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(result.Contains("<lp1:creator-displayname>user_foo</lp1:creator-displayname>"));
        }

        [Fact]
        public void VerifyDeadPropCountReturnsZero()
        {
            stubs.Attach(provider.ItemExists, true);
            ItemMetaData item = new ItemMetaData();
            item.Name = "Foo";
            stubs.Attach(provider.GetItems, item);
            request.Path = "http://localhost:8082/!svn/bc/1234/Foo";
            request.Input =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><deadprop-count xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/></prop></propfind>";
            request.Headers["Depth"] = "0";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(result.Contains("<lp2:deadprop-count>0</lp2:deadprop-count>"));
        }

        [Fact]
        public void VerifyGetContentLengthForFolder()
        {
            stubs.Attach(provider.ItemExists, true);
            FolderMetaData item = new FolderMetaData();
            item.Name = "Foo";
            item.ItemRevision = 1234;
            item.Author = "user_foo";
            DateTime dt = DateTime.Now.ToUniversalTime();
            item.LastModifiedDate = dt;
            stubs.Attach(provider.GetItems, item);
            byte[] fileData = new byte[]{1,2,3,4,5};
            stubs.Attach(provider.ReadFileAsync, fileData);
            request.Path = "http://localhost:8082/!svn/bc/1234/Foo";
            request.Input =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><getcontentlength xmlns=\"DAV:\"/></prop></propfind>";
            request.Headers["Depth"] = "0";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(result.Contains("<g0:getcontentlength/>"));
        }

        [Fact]
        public void VerifyPathIsDecodedWhenInvokingSourceControlProviderForFolderPath()
        {
            Results r1 = stubs.Attach(provider.ItemExists, true);
            FolderMetaData item = new FolderMetaData();
            item.Name = "New%20Folder%207";
            Results r2 = stubs.Attach(provider.GetItems, item);
            request.Path = "http://localhost:8082/Spikes/SvnFacade/trunk/New%20Folder%207";
            request.Input =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><version-controlled-configuration xmlns=\"DAV:\"/></prop></propfind>";
            request.Headers["Depth"] = "0";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            Assert.Equal("/Spikes/SvnFacade/trunk/New Folder 7", r1.Parameters[0]);
            Assert.Equal("/Spikes/SvnFacade/trunk/New Folder 7", r2.Parameters[1]);
        }

        [Fact]
        public void VerifyPathIsDecodedWhenInvokingSourceControlProviderForSvnBcFolderPath()
        {
            Results r = stubs.Attach(provider.ItemExists, true);
            stubs.Attach(provider.IsDirectory, true);
            ItemMetaData item = new ItemMetaData();
            item.Name = "Test Project";
            stubs.Attach(provider.GetItems, item);
            request.Path = "http://localhost:8082/!svn/bc/3444/Test%20Project";
            request.Input =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><version-controlled-configuration xmlns=\"DAV:\"/><resourcetype xmlns=\"DAV:\"/><baseline-relative-path xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/><repository-uuid xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/></prop></propfind>";
            request.Headers["Depth"] = "0";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            Assert.Equal("/Test Project", r.Parameters[0]);
        }

        [Fact]
        public void VerifyVersionName()
        {
            stubs.Attach(provider.ItemExists, true);
            FolderMetaData item = new FolderMetaData();
            item.Name = "Foo";
            item.ItemRevision = 1234;
            item.Author = "user_foo";
            DateTime dt = DateTime.Now.ToUniversalTime();
            item.LastModifiedDate = dt;
            stubs.Attach(provider.GetItems, item);
            request.Path = "http://localhost:8082/!svn/bc/1234/Foo";
            request.Input =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><version-name xmlns=\"DAV:\"/></prop></propfind>";
            request.Headers["Depth"] = "0";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(result.Contains("<lp1:version-name>1234</lp1:version-name>"));
        }

        [Fact]
        public void Handle_SvnWrkPropFind_CorrectlyInvokesSourceControlProvider()
        {
            Results r1 = stubs.Attach((MyMocks.GetItemInActivity)provider.GetItemInActivity, null);
            request.Path = "http://localhost:8080//!svn/wrk/7a6c73b2-7e7c-2141-817d-9bd653873445/trunk/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60.txt";
            request.Input = "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><version-controlled-configuration xmlns=\"DAV:\"/><resourcetype xmlns=\"DAV:\"/><baseline-relative-path xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/><repository-uuid xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/></prop></propfind>";
            request.Headers["Depth"] = "0";

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            Assert.Equal("7a6c73b2-7e7c-2141-817d-9bd653873445", r1.Parameters[0]);
            Assert.Equal("trunk/A !@#$%^&()_-+={[}];',.~`.txt", r1.Parameters[1]);
        }


        [Fact]
        public void Handle_ErrorOccurs_RequestBodyIsSetInRequestCache()
        {
            stubs.Attach((MyMocks.ItemExists)provider.ItemExists, new Exception("Test"));
            request.Path = "http://localhost:8080/Quick%20Starts";
            request.Input = "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><version-controlled-configuration xmlns=\"DAV:\"/><resourcetype xmlns=\"DAV:\"/><baseline-relative-path xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/><repository-uuid xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/></prop></propfind>";

            Record.Exception(delegate { handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null); });

            Assert.NotNull(RequestCache.Items["RequestBody"]);
        }
    }
}
