using System;
using System.IO;
using System.Text;
using Attach;
using SvnBridge.Interfaces;
using SvnBridge.Utility;
using Xunit;
using SvnBridge.Infrastructure;
using SvnBridge.PathParsing;
using SvnBridge.SourceControl;
using Tests;
using SvnBridge.Handlers;

namespace UnitTests
{
    public class ReportHandlerUpdateReportTests : HandlerTestsBase
    {
        protected ReportHandler handler = new ReportHandler();

        [Fact]
        public void Handle_EncodesDeleteFileElements()
        {
            FolderMetaData metadata = new FolderMetaData();
            metadata.Name = "";
            metadata.ItemRevision = 5734;
            metadata.Author = "jwanagel";
            metadata.LastModifiedDate = DateTime.Parse("2008-01-20T08:55:13.330897Z");
            DeleteMetaData file1 = new DeleteMetaData();
            file1.Name = "F !@#$%^&()_-+={[}];',.~`.txt";
            metadata.Items.Add(file1);
            stubs.Attach(provider.GetChangedItems, metadata);
            request.Path = "http://localhost:8084/!svn/vcc/default";
            request.Input =
                "<S:update-report send-all=\"true\" xmlns:S=\"svn:\"><S:src-path>http://localhost:8084</S:src-path><S:target-revision>5734</S:target-revision><S:entry rev=\"5733\" ></S:entry></S:update-report>";

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);
            string output = Encoding.Default.GetString(((MemoryStream)response.OutputStream).ToArray());

            Assert.True(output.Contains("<S:delete-entry name=\"F !@#$%^&amp;()_-+={[}];',.~`.txt\"/>"));
        }

        [Fact]
        public void Handle_EncodesDeleteFolderElements()
        {
            FolderMetaData metadata = new FolderMetaData();
            metadata.Name = "";
            metadata.ItemRevision = 5734;
            metadata.Author = "jwanagel";
            metadata.LastModifiedDate = DateTime.Parse("2008-01-20T08:55:13.330897Z");
            DeleteFolderMetaData folder1 = new DeleteFolderMetaData();
            folder1.Name = "B !@#$%^&()_-+={[}];',.~`";
            metadata.Items.Add(folder1);
            stubs.Attach(provider.GetChangedItems, metadata);
            request.Path = "http://localhost:8084/!svn/vcc/default";
            request.Input =
                "<S:update-report send-all=\"true\" xmlns:S=\"svn:\"><S:src-path>http://localhost:8084</S:src-path><S:target-revision>5734</S:target-revision><S:entry rev=\"5733\" ></S:entry></S:update-report>";

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);
            string output = Encoding.Default.GetString(((MemoryStream)response.OutputStream).ToArray());

            Assert.True(output.Contains("<S:delete-entry name=\"B !@#$%^&amp;()_-+={[}];',.~`\"/>"));
        }

        [Fact]
        public void Handle_EncodesUpdateFileElements()
        {
            FolderMetaData metadata = new FolderMetaData();
            metadata.Name = "";
            metadata.ItemRevision = 5734;
            metadata.Author = "jwanagel";
            metadata.LastModifiedDate = DateTime.Parse("2008-01-20T08:55:13.330897Z");
            ItemMetaData file1 = new ItemMetaData();
            file1.Name = "G !@#$%^&()_-+={[}];',.~`.txt";
            file1.ItemRevision = 5734;
            file1.LastModifiedDate = DateTime.Parse("2008-01-20T08:55:13.330897Z");
            file1.Author = "jwanagel";
            metadata.Items.Add(file1);
            stubs.Attach(provider.GetChangedItems, metadata);
            stubs.Attach(provider.ItemExists, true);
            byte[] fileData = Encoding.UTF8.GetBytes("1234abcd");
            stubs.Attach(provider.ReadFileAsync, fileData);
            request.Path = "http://localhost:8084/!svn/vcc/default";
            request.Input =
                "<S:update-report send-all=\"true\" xmlns:S=\"svn:\"><S:src-path>http://localhost:8084</S:src-path><S:target-revision>5734</S:target-revision><S:entry rev=\"5733\" ></S:entry></S:update-report>";

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);
            string output = Encoding.Default.GetString(((MemoryStream)response.OutputStream).ToArray());

            Assert.True(output.Contains("<S:open-file name=\"G !@#$%^&amp;()_-+={[}];',.~`.txt\" rev=\"5733\">"));
        }

        [Fact]
        public void Handle_EncodesAddDirectoryCheckedInHrefElements()
        {
            FolderMetaData metadata = new FolderMetaData();
            metadata.Name = "Test";
            metadata.ItemRevision = 5722;
            metadata.Author = "bradwils";
            metadata.LastModifiedDate = DateTime.Parse("2007-12-15T00:56:55.541665Z");
            FolderMetaData folder = new FolderMetaData();
            folder.Name = "Test/B !@#$%^&()_-+={[}];',.~`";
            folder.ItemRevision = 5722;
            folder.Author = "bradwils";
            folder.LastModifiedDate = DateTime.Parse("2007-12-15T00:56:55.541665Z");
            metadata.Items.Add(folder);
            stubs.Attach(provider.GetItems, metadata);
            byte[] fileData = Encoding.UTF8.GetBytes("test");
            stubs.Attach(provider.ReadFileAsync, fileData);
            request.Path = "http://localhost:8084/!svn/vcc/default";
            request.Input =
                "<S:update-report send-all=\"true\" xmlns:S=\"svn:\"><S:src-path>http://localhost:8084/Test</S:src-path><S:target-revision>5722</S:target-revision><S:entry rev=\"5722\"  start-empty=\"true\"></S:entry></S:update-report>";

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);
            string output = Encoding.Default.GetString(((MemoryStream)response.OutputStream).ToArray());

            Assert.True(
                output.Contains(
                    "<D:checked-in><D:href>/!svn/ver/5722/Test/B%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60</D:href></D:checked-in>"));
        }

        [Fact]
        public void Handle_EncodesAddDirectoryElements()
        {
            FolderMetaData metadata = new FolderMetaData();
            metadata.Name = "Test";
            metadata.ItemRevision = 5722;
            metadata.Author = "bradwils";
            metadata.LastModifiedDate = DateTime.Parse("2007-12-15T00:56:55.541665Z");
            FolderMetaData folder = new FolderMetaData();
            folder.Name = "Test/B !@#$%^&()_-+={[}];',.~`";
            folder.ItemRevision = 5722;
            folder.Author = "bradwils";
            folder.LastModifiedDate = DateTime.Parse("2007-12-15T00:56:55.541665Z");
            metadata.Items.Add(folder);
            stubs.Attach(provider.GetItems, metadata);
            byte[] fileData = Encoding.UTF8.GetBytes("test");
            stubs.Attach(provider.ReadFileAsync, fileData);
            request.Path = "http://localhost:8084/!svn/vcc/default";
            request.Input =
                "<S:update-report send-all=\"true\" xmlns:S=\"svn:\"><S:src-path>http://localhost:8084/Test</S:src-path><S:target-revision>5722</S:target-revision><S:entry rev=\"5722\"  start-empty=\"true\"></S:entry></S:update-report>";

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);
            string output = Encoding.Default.GetString(((MemoryStream)response.OutputStream).ToArray());

            Assert.True(
                output.Contains(
                    "<S:add-directory name=\"B !@#$%^&amp;()_-+={[}];',.~`\" bc-url=\"/!svn/bc/5722/Test/B%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60\">"));
        }

        [Fact]
        public void Handle_EncodesAddFileCheckedInHrefElements()
        {
            FolderMetaData metadata = new FolderMetaData();
            metadata.Name = "Test";
            metadata.ItemRevision = 5722;
            metadata.Author = "bradwils";
            metadata.LastModifiedDate = DateTime.Parse("2007-12-15T00:56:55.541665Z");
            ItemMetaData item = new ItemMetaData();
            item.Name = "Test/C !@#$%^&()_-+={[}];',.~`..txt";
            item.ItemRevision = 5722;
            item.Author = "bradwils";
            item.LastModifiedDate = DateTime.Parse("2007-12-15T00:56:55.541665Z");
            metadata.Items.Add(item);
            stubs.Attach(provider.GetItems, metadata);
            byte[] fileData = Encoding.UTF8.GetBytes("test");
            stubs.Attach(provider.ReadFileAsync, fileData);
            request.Path = "http://localhost:8084/!svn/vcc/default";
            request.Input =
                "<S:update-report send-all=\"true\" xmlns:S=\"svn:\"><S:src-path>http://localhost:8084/Test</S:src-path><S:target-revision>5722</S:target-revision><S:entry rev=\"5722\"  start-empty=\"true\"></S:entry></S:update-report>";

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);
            string output = Encoding.Default.GetString(((MemoryStream)response.OutputStream).ToArray());

            Assert.True(
                output.Contains(
                    "<D:checked-in><D:href>/!svn/ver/5722/Test/C%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60..txt</D:href></D:checked-in>"));
        }

        [Fact]
        public void Handle_EncodesAddFileElements()
        {
            FolderMetaData metadata = new FolderMetaData();
            metadata.Name = "Test";
            metadata.ItemRevision = 5722;
            metadata.Author = "bradwils";
            metadata.LastModifiedDate = DateTime.Parse("2007-12-15T00:56:55.541665Z");
            ItemMetaData item = new ItemMetaData();
            item.Name = "Test/C !@#$%^&()_-+={[}];',.~`..txt";
            item.ItemRevision = 5722;
            item.Author = "bradwils";
            item.LastModifiedDate = DateTime.Parse("2007-12-15T00:56:55.541665Z");
            metadata.Items.Add(item);
            stubs.Attach(provider.GetItems, metadata);
            byte[] fileData = Encoding.UTF8.GetBytes("test");
            stubs.Attach(provider.ReadFileAsync, fileData);
            request.Path = "http://localhost:8084/!svn/vcc/default";
            request.Input =
                "<S:update-report send-all=\"true\" xmlns:S=\"svn:\"><S:src-path>http://localhost:8084/Test</S:src-path><S:target-revision>5722</S:target-revision><S:entry rev=\"5722\"  start-empty=\"true\"></S:entry></S:update-report>";

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);
            string output = Encoding.Default.GetString(((MemoryStream)response.OutputStream).ToArray());

            Assert.True(output.Contains("<S:add-file name=\"C !@#$%^&amp;()_-+={[}];',.~`..txt\">"));
        }

        [Fact]
        public void Handle_ProducesCorrectOutputForBranchedFile()
        {
            FolderMetaData folder = new FolderMetaData();
            folder.Name = "";
            folder.Author = "jwanagel";
            folder.ItemRevision = 5700;
            folder.LastModifiedDate = DateTime.Parse("2007-09-05T18:37:14.239559Z");
            folder.Items.Add(new ItemMetaData());
            folder.Items[0].Name = "asfd2.txt";
            folder.Items[0].Author = "jwanagel";
            folder.Items[0].ItemRevision = 5700;
            folder.Items[0].LastModifiedDate = DateTime.Parse("2007-09-05T18:37:14.239559Z");
            Results r = stubs.Attach(provider.GetChangedItems, folder);
            stubs.Attach(provider.ItemExists, false);
            byte[] fileData = Encoding.UTF8.GetBytes("test");
            stubs.Attach(provider.ReadFileAsync, fileData);
            request.Path = "http://localhost:8082/!svn/vcc/default";
            request.Input =
                "<S:update-report send-all=\"true\" xmlns:S=\"svn:\"><S:src-path>http://localhost:8082</S:src-path><S:target-revision>5700</S:target-revision><S:entry rev=\"5699\" ></S:entry></S:update-report>";

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string expected =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<S:update-report xmlns:S=\"svn:\" xmlns:V=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:D=\"DAV:\" send-all=\"true\">\n" +
                "<S:target-revision rev=\"5700\"/>\n" +
                "<S:open-directory rev=\"5699\">\n" +
                "<D:checked-in><D:href>/!svn/ver/5700/</D:href></D:checked-in>\n" +
                "<S:set-prop name=\"svn:entry:committed-rev\">5700</S:set-prop>\n" +
                "<S:set-prop name=\"svn:entry:committed-date\">2007-09-05T18:37:14.239559Z</S:set-prop>\n" +
                "<S:set-prop name=\"svn:entry:last-author\">jwanagel</S:set-prop>\n" +
                "<S:set-prop name=\"svn:entry:uuid\">81a5aebe-f34e-eb42-b435-ac1ecbb335f7</S:set-prop>\n" +
                "<S:add-file name=\"asfd2.txt\">\n" +
                "<D:checked-in><D:href>/!svn/ver/5700/asfd2.txt</D:href></D:checked-in>\n" +
                "<S:set-prop name=\"svn:entry:committed-rev\">5700</S:set-prop>\n" +
                "<S:set-prop name=\"svn:entry:committed-date\">2007-09-05T18:37:14.239559Z</S:set-prop>\n" +
                "<S:set-prop name=\"svn:entry:last-author\">jwanagel</S:set-prop>\n" +
                "<S:set-prop name=\"svn:entry:uuid\">81a5aebe-f34e-eb42-b435-ac1ecbb335f7</S:set-prop>\n" +
                //"<S:txdelta>U1ZOAQAABAIFAYQEdGVzdA==\n" +
                "<S:txdelta>U1ZOAAAABAEEhHRlc3Q=\n" +
                "</S:txdelta><S:prop><V:md5-checksum>098f6bcd4621d373cade4e832627b4f6</V:md5-checksum></S:prop>\n" +
                "</S:add-file>\n" +
                "<S:prop></S:prop>\n" +
                "</S:open-directory>\n" +
                "</S:update-report>\n";
            Assert.Equal(expected, Encoding.Default.GetString(((MemoryStream)response.OutputStream).ToArray()));
            Assert.Equal("text/xml; charset=\"utf-8\"", response.ContentType);
            Assert.Equal(Encoding.UTF8, response.ContentEncoding);
            Assert.Equal(200, response.StatusCode);
            Assert.True(response.SendChunked);
            Assert.Equal("/", r.Parameters[0]);
            Assert.Equal(5699, r.Parameters[1]);
            Assert.Equal(5700, r.Parameters[2]);
        }

        [Fact]
        public void Handle_ProducesCorrectOutputForDeletedFileInSubfolder()
        {
            FolderMetaData folder = new FolderMetaData();
            folder.Name = "";
            folder.Author = "jwanagel";
            folder.ItemRevision = 5698;
            folder.LastModifiedDate = DateTime.Parse("2007-08-21T00:41:27.680005Z");
            folder.Items.Add(new FolderMetaData());
            folder.Items[0].Name = "Test9";
            folder.Items[0].Author = "jwanagel";
            folder.Items[0].ItemRevision = 5698;
            folder.Items[0].LastModifiedDate = DateTime.Parse("2007-08-21T00:41:27.680005Z");
            ((FolderMetaData)folder.Items[0]).Items.Add(new DeleteMetaData());
            ((FolderMetaData)folder.Items[0]).Items[0].Name = "Test.txt";
            Results r = stubs.Attach(provider.GetChangedItems, folder);
            stubs.Attach(provider.ItemExists, true);
            request.Path = "http://localhost:8082/!svn/vcc/default";
            request.Input =
                "<S:update-report send-all=\"true\" xmlns:S=\"svn:\"><S:src-path>http://localhost:8082</S:src-path><S:target-revision>5698</S:target-revision><S:entry rev=\"5697\" ></S:entry></S:update-report>";

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string expected =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<S:update-report xmlns:S=\"svn:\" xmlns:V=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:D=\"DAV:\" send-all=\"true\">\n" +
                "<S:target-revision rev=\"5698\"/>\n" +
                "<S:open-directory rev=\"5697\">\n" +
                "<D:checked-in><D:href>/!svn/ver/5698/</D:href></D:checked-in>\n" +
                "<S:set-prop name=\"svn:entry:committed-rev\">5698</S:set-prop>\n" +
                "<S:set-prop name=\"svn:entry:committed-date\">2007-08-21T00:41:27.680005Z</S:set-prop>\n" +
                "<S:set-prop name=\"svn:entry:last-author\">jwanagel</S:set-prop>\n" +
                "<S:set-prop name=\"svn:entry:uuid\">81a5aebe-f34e-eb42-b435-ac1ecbb335f7</S:set-prop>\n" +
                "<S:open-directory name=\"Test9\" rev=\"5697\">\n" +
                "<D:checked-in><D:href>/!svn/ver/5698/Test9</D:href></D:checked-in>\n" +
                "<S:set-prop name=\"svn:entry:committed-rev\">5698</S:set-prop>\n" +
                "<S:set-prop name=\"svn:entry:committed-date\">2007-08-21T00:41:27.680005Z</S:set-prop>\n" +
                "<S:set-prop name=\"svn:entry:last-author\">jwanagel</S:set-prop>\n" +
                "<S:set-prop name=\"svn:entry:uuid\">81a5aebe-f34e-eb42-b435-ac1ecbb335f7</S:set-prop>\n" +
                "<S:delete-entry name=\"Test.txt\"/>\n" +
                "<S:prop></S:prop>\n" +
                "</S:open-directory>\n" +
                "<S:prop></S:prop>\n" +
                "</S:open-directory>\n" +
                "</S:update-report>\n";
            Assert.Equal(expected, Encoding.Default.GetString(((MemoryStream)response.OutputStream).ToArray()));
            Assert.Equal("text/xml; charset=\"utf-8\"", response.ContentType);
            Assert.Equal(Encoding.UTF8, response.ContentEncoding);
            Assert.Equal(200, response.StatusCode);
            Assert.True(response.SendChunked);
            Assert.Equal("/", r.Parameters[0]);
            Assert.Equal(5697, r.Parameters[1]);
            Assert.Equal(5698, r.Parameters[2]);
        }

        [Fact]
        public void Handle_SucceedsWhenTargetRevisionIsNotSpecified()
        {
            FolderMetaData folder = new FolderMetaData();
            folder.Name = "";
            folder.Author = "jwanagel";
            folder.ItemRevision = 5713;
            folder.LastModifiedDate = DateTime.Parse("2007-09-17T02:38:24.225369Z");
            stubs.Attach(provider.GetChangedItems, folder);
            stubs.Attach(provider.GetLatestVersion, 5713);
            request.Path = "http://localhost:8085/!svn/vcc/default";
            request.Input =
                "<S:update-report send-all=\"true\" xmlns:S=\"svn:\"><S:src-path>http://localhost:8085</S:src-path><S:entry rev=\"5713\" ></S:entry></S:update-report>";

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string expected =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<S:update-report xmlns:S=\"svn:\" xmlns:V=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:D=\"DAV:\" send-all=\"true\">\n" +
                "<S:target-revision rev=\"5713\"/>\n" +
                "<S:open-directory rev=\"5713\">\n" +
                "<D:checked-in><D:href>/!svn/ver/5713/</D:href></D:checked-in>\n" +
                "<S:set-prop name=\"svn:entry:committed-rev\">5713</S:set-prop>\n" +
                "<S:set-prop name=\"svn:entry:committed-date\">2007-09-17T02:38:24.225369Z</S:set-prop>\n" +
                "<S:set-prop name=\"svn:entry:last-author\">jwanagel</S:set-prop>\n" +
                "<S:set-prop name=\"svn:entry:uuid\">81a5aebe-f34e-eb42-b435-ac1ecbb335f7</S:set-prop>\n" +
                "<S:prop></S:prop>\n" +
                "</S:open-directory>\n" +
                "</S:update-report>\n";
            Assert.Equal(expected, Encoding.Default.GetString(((MemoryStream)response.OutputStream).ToArray()));
        }

        [Fact]
        public void Handle_ClientStateForFileIsDifferentAndFileWasModified_OpenFileElementRevisionMatchesClientState()
        {
            FolderMetaData metadata = new FolderMetaData();
            metadata.Name = "svn";
            metadata.ItemRevision = 5782;
            metadata.Author = "jwanagel";
            metadata.LastModifiedDate = DateTime.Parse("2008-10-16T00:39:59.089062Z");
            ItemMetaData item = new ItemMetaData();
            item.Id = 1234;
            item.Name = "svn/Commerce.MVC.sln";
            item.ItemRevision = 5782;
            item.Author = "jwanagel";
            item.LastModifiedDate = DateTime.Parse("2008-10-16T00:39:59.089062Z");
            metadata.Items.Add(item);
            stubs.Attach(provider.GetChangedItems, metadata);
            byte[] fileData = Encoding.UTF8.GetBytes("3\r\n4\r\n5\r\n6");
            stubs.Attach(provider.ReadFileAsync, fileData);
            stubs.Attach((MyMocks.GetLatestVersion)provider.GetLatestVersion, Return.Value(5782));
            stubs.Attach((MyMocks.ItemExists)provider.ItemExists, Return.DelegateResult(
                delegate(object[] parameters)
                {
                    string value = parameters[0].ToString();
                    if (value == "svn/Commerce.MVC.sln" || value == "1234")
                        return true;
                    return false;
                }
            ));
            request.Path = "http://localhost:8080/!svn/vcc/default";
            request.Input =
                "<S:update-report send-all=\"true\" xmlns:S=\"svn:\"><S:src-path>http://localhost:8080/svn</S:src-path><S:entry rev=\"5780\" ></S:entry><S:entry rev=\"5781\" >Commerce.MVC.sln</S:entry></S:update-report>";

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);
            string output = Encoding.Default.GetString(((MemoryStream)response.OutputStream).ToArray());

            Assert.True(output.Contains("<S:open-file name=\"Commerce.MVC.sln\" rev=\"5781\">"));
        }

        [Fact]
        public void Handle_CheckoutAtRootWithCodePlexPathParser_Succeeds()
        {
            FolderMetaData metadata = new FolderMetaData();
            metadata.Name = "svn";
            metadata.ItemRevision = 5782;
            metadata.Author = "jwanagel";
            metadata.LastModifiedDate = DateTime.Parse("2008-10-16T00:39:59.089062Z");
            ItemMetaData item = new ItemMetaData();
            item.Id = 1234;
            item.Name = "svn/Commerce.MVC.sln";
            item.ItemRevision = 5782;
            item.Author = "jwanagel";
            item.LastModifiedDate = DateTime.Parse("2008-10-16T00:39:59.089062Z");
            metadata.Items.Add(item);
            stubs.Attach(provider.GetItems, metadata);
            byte[] fileData = Encoding.UTF8.GetBytes("3\r\n4\r\n5\r\n6");
            stubs.Attach(provider.ReadFileAsync, fileData);

            request.ApplicationPath = "/svn";
            request.Path = "http://svnbridge.redmond.corp.microsoft.com/svn/!svn/vcc/default";
            request.Input =
                "<update-report xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" send-all=\"true\" xmlns=\"svn:\">" +
                "  <entry rev=\"23664\" start-empty=\"true\" />" +
                "  <src-path>http://svnbridge.redmond.corp.microsoft.com/svn</src-path>" +
                "  <target-revision>23664</target-revision>" +
                "</update-report>";

            Exception result = Record.Exception(delegate { handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null); });

            Assert.Null(result);
        }

        [Fact]
        public void Handle_UpdateWithFolderMissingInClientState()
        {
            FolderMetaData metadata = new FolderMetaData();
            metadata.Name = "";
            metadata.ItemRevision = 5734;
            metadata.Author = "jwanagel";
            metadata.LastModifiedDate = DateTime.Parse("2008-01-20T08:55:13.330897Z");
            FolderMetaData folder = new FolderMetaData("Test/foo");
            metadata.Items.Add(folder);
            stubs.Attach(provider.GetChangedItems, metadata);
            stubs.Attach(provider.ItemExists, true);
            request.Path = "http://127.0.0.1:25169/!svn/vcc/default";
            request.Input =
                "<S:update-report send-all=\"true\" xmlns:S=\"svn:\"><S:src-path>http://127.0.0.1:25169/Test</S:src-path><S:target-revision>5733</S:target-revision><S:entry rev=\"5733\" ></S:entry><S:missing>foo</S:missing></S:update-report>";

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);
            string output = Encoding.Default.GetString(((MemoryStream)response.OutputStream).ToArray());

            Assert.True(output.Contains("<S:add-directory name=\"foo\""));
        }

        [Fact]
        public void Handle_UpdateForInvalidFile()
        {
            FolderMetaData metadata = new FolderMetaData();
            stubs.Attach(provider.GetItems, Return.Value(null));
            request.Path = "localhost:8080/!svn/vcc/default";
            request.Input =
                "<update-report xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" send-all=\"true\" xmlns=\"svn:\"><entry rev=\"5795\" start-empty=\"true\" /><src-path>http://localhost:8080/svn/robots.txt</src-path><target-revision>5795</target-revision></update-report>";

            handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);
            string output = Encoding.Default.GetString(((MemoryStream)response.OutputStream).ToArray());

            Assert.Equal(500, response.StatusCode);
            Assert.True(output.Contains("<m:human-readable errcode=\"160005\">\nTarget path does not exist\n</m:human-readable>\n"));
        }
    }
}
