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

namespace SvnBridge.Handlers
{
    public class ReportHandlerUpdateReportTests : HandlerTestsBase
    {
        protected ReportHandler handler = new ReportHandler();

        [Fact]
        public void TestHandleEncodesDeleteFileElements()
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
        public void TestHandleEncodesDeleteFolderElements()
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
        public void TestHandleEncodesUpdateFileElements()
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
        public void VerifyHandleEncodesAddDirectoryCheckedInHrefElements()
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
        public void VerifyHandleEncodesAddDirectoryElements()
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
        public void VerifyHandleEncodesAddFileCheckedInHrefElements()
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
        public void VerifyHandleEncodesAddFileElements()
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
        public void VerifyHandleProducesCorrectOutputForBranchedFile()
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
        public void VerifyHandleProducesCorrectOutputForDeletedFileInSubfolder()
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
        public void VerifyHandleSucceedsWhenTargetRevisionIsNotSpecified()
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
    }
}
