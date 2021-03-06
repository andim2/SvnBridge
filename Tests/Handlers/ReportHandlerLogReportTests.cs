using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Attach;
using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using SvnBridge.Interfaces;
using Xunit;
using SvnBridge.Infrastructure;
using SvnBridge.SourceControl;
using Tests;
using SvnBridge.Handlers;

namespace UnitTests
{
    public class ReportHandlerLogReportTests : HandlerTestsBase
    {
        protected ReportHandler handler = new ReportHandler();

        [Fact]
        public void VerifyHandleEncodesFilenamesWithSpecialCharacters()
        {
            List<SourceItemHistory> histories = new List<SourceItemHistory>();
            SourceItemHistory history1 =
                new SourceItemHistory(5532, "jwanagel", DateTime.Parse("2007-07-25T00:13:14.466022Z"), "1234");
            history1.Changes.Add(TestHelper.MakeChange(ChangeType.Add, "newFolder4"));
            history1.Changes.Add(TestHelper.MakeChange(ChangeType.Add, "newFolder4/A!@#$%^&()~`_-+={[}];',.txt"));
            history1.Changes.Add(TestHelper.MakeChange(ChangeType.Edit, "newFolder4/B!@#$%^&()~`_-+={[}];',.txt"));
            history1.Changes.Add(TestHelper.MakeChange(ChangeType.Delete, "newFolder4/C!@#$%^&()~`_-+={[}];',.txt"));
            history1.Changes.Add(
                TestHelper.MakeChange(ChangeType.Rename,
                                      "newFolder4/E!@#$%^&()~`_-+={[}];',.txt",
                                      "newFolder4/D!@#$%^&()~`_-+={[}];',.txt",
                                      5531));
            history1.Changes.Add(
                TestHelper.MakeChange(ChangeType.Branch,
                                      "newFolder4/G!@#$%^&()~`_-+={[}];',.txt",
                                      "newFolder4/F!@#$%^&()~`_-+={[}];',.txt",
                                      5531));
            histories.Add(history1);
            Results r = stubs.Attach(provider.GetLog, Return.Value(new LogItem(@"C:\", "newFolder4", histories.ToArray())));
            request.Path = "http://localhost:8082/!svn/bc/5532/newFolder4";
            request.Input =
                "<S:log-report xmlns:S=\"svn:\"><S:start-revision>5532</S:start-revision><S:end-revision>1</S:end-revision><S:limit>100</S:limit><S:discover-changed-paths/><S:path></S:path></S:log-report>";

            string result = HandlerHandle(
                handler);

            string expected =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<S:log-report xmlns:S=\"svn:\" xmlns:D=\"DAV:\">\n" +
                "<S:log-item>\n" +
                "<D:version-name>5532</D:version-name>\n" +
                "<D:creator-displayname>jwanagel</D:creator-displayname>\n" +
                "<S:date>2007-07-25T00:13:14.466022Z</S:date>\n" +
                "<D:comment>1234</D:comment>\n" +
                "<S:added-path node-kind=\"dir\">/newFolder4</S:added-path>\n" +
                "<S:added-path node-kind=\"dir\">/newFolder4/A!@#$%^&amp;()~`_-+={[}];',.txt</S:added-path>\n" +
                "<S:modified-path node-kind=\"dir\">/newFolder4/B!@#$%^&amp;()~`_-+={[}];',.txt</S:modified-path>\n" +
                "<S:deleted-path node-kind=\"dir\">/newFolder4/C!@#$%^&amp;()~`_-+={[}];',.txt</S:deleted-path>\n" +
                "<S:added-path copyfrom-path=\"/newFolder4/D!@#$%^&amp;()~`_-+={[}];',.txt\" copyfrom-rev=\"5531\" node-kind=\"dir\">/newFolder4/E!@#$%^&amp;()~`_-+={[}];',.txt</S:added-path>\n" +
                "<S:deleted-path node-kind=\"dir\">/newFolder4/D!@#$%^&amp;()~`_-+={[}];',.txt</S:deleted-path>\n" +
                "<S:added-path copyfrom-path=\"/newFolder4/F!@#$%^&amp;()~`_-+={[}];',.txt\" copyfrom-rev=\"5531\" node-kind=\"dir\">/newFolder4/G!@#$%^&amp;()~`_-+={[}];',.txt</S:added-path>\n" +
                "</S:log-item>\n" +
                "</S:log-report>\n";
            Assert.Equal(expected, result);
        }

        [Fact]
        public void VerifyHandleForGetAtLogRoot()
        {
            List<SourceItemHistory> histories = new List<SourceItemHistory>();
            SourceItemHistory history1 =
                new SourceItemHistory(5696, "jwanagel", DateTime.Parse("2007-08-20T03:23:41.054140Z"), "1234");
            history1.Changes.Add(TestHelper.MakeChange(ChangeType.Delete, "Folder9"));
            histories.Add(history1);
            Results r = stubs.Attach(provider.GetLog, Return.Value(new LogItem(@"C:\", "", histories.ToArray())));
            request.Path = "http://localhost:8082/!svn/bc/5696";
            request.Input =
                "<S:log-report xmlns:S=\"svn:\"><S:start-revision>5696</S:start-revision><S:end-revision>1</S:end-revision><S:limit>100</S:limit><S:discover-changed-paths/><S:strict-node-history/><S:path></S:path></S:log-report>";

            string result = HandlerHandle(
                handler);

            string expected =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<S:log-report xmlns:S=\"svn:\" xmlns:D=\"DAV:\">\n" +
                "<S:log-item>\n" +
                "<D:version-name>5696</D:version-name>\n" +
                "<D:creator-displayname>jwanagel</D:creator-displayname>\n" +
                "<S:date>2007-08-20T03:23:41.054140Z</S:date>\n" +
                "<D:comment>1234</D:comment>\n" +
                "<S:deleted-path node-kind=\"dir\">/Folder9</S:deleted-path>\n" +
                "</S:log-item>\n" +
                "</S:log-report>\n";
            Assert.Equal(expected, result);
            Assert.Equal("/", r.Parameters[0]);
            Assert.Equal(1, r.Parameters[1]);
            Assert.Equal(5696, r.Parameters[2]);
            Assert.Equal(Recursion.Full, r.Parameters[3]);
            Assert.Equal(100, r.Parameters[4]);
        }

        [Fact]
        public void VerifyHandleProducesCorrectOutputForBranchedFile()
        {
            List<SourceItemHistory> histories = new List<SourceItemHistory>();
            SourceItemHistory history1 =
                new SourceItemHistory(5679, "jwanagel", DateTime.Parse("2007-08-17T21:47:11.400569Z"), "made a copy");
            history1.Changes.Add(TestHelper.MakeChange(ChangeType.Branch, "Test3Branch.txt", "Test3.txt", 5678));
            histories.Add(history1);
            Results r = stubs.Attach(provider.GetLog, Return.Value(new LogItem(@"C:\", "", histories.ToArray())));
            request.Path = "http://localhost:8082/!svn/bc/5679/Test3Branch.txt";
            request.Input =
                "<S:log-report xmlns:S=\"svn:\"><S:start-revision>5679</S:start-revision><S:end-revision>1</S:end-revision><S:limit>100</S:limit><S:discover-changed-paths/><S:strict-node-history/><S:path></S:path></S:log-report>";

            string result = HandlerHandle(
                handler);

            // Hmm, why node-kind="dir" rather than "file"?
            // Probably because the Change initially got registered as directory-type,
            // but one should investigate that...
            string expected =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<S:log-report xmlns:S=\"svn:\" xmlns:D=\"DAV:\">\n" +
                "<S:log-item>\n" +
                "<D:version-name>5679</D:version-name>\n" +
                "<D:creator-displayname>jwanagel</D:creator-displayname>\n" +
                "<S:date>2007-08-17T21:47:11.400569Z</S:date>\n" +
                "<D:comment>made a copy</D:comment>\n" +
                "<S:added-path copyfrom-path=\"/Test3.txt\" copyfrom-rev=\"5678\" node-kind=\"dir\">/Test3Branch.txt</S:added-path>\n" +
                "</S:log-item>\n" +
                "</S:log-report>\n";

            Assert.Equal(expected, result);
            Assert.Equal("text/xml; charset=\"utf-8\"", response.ContentType);
            Assert.Equal(Encoding.UTF8, response.ContentEncoding);
            Assert.Equal(200, response.StatusCode);
            Assert.True(response.SendChunked);
        }

        [Fact]
        public void VerifyHandleProducesCorrectOutputForRenamedFile()
        {
            List<SourceItemHistory> histories = new List<SourceItemHistory>();
            SourceItemHistory history1 =
                new SourceItemHistory(5531, "jwanagel", DateTime.Parse("2007-07-24T07:46:20.635845Z"), "Renamed file");
            history1.Changes.Add(
                TestHelper.MakeChange(ChangeType.Rename, "newFolder3/NewFileRename.txt", "newFolder3/NewFile.txt", 5530));
            histories.Add(history1);
            Results r = stubs.Attach(provider.GetLog, Return.Value(new LogItem(@"C:\", "newFolder2", histories.ToArray())));
            request.Path = "http://localhost:8082/!svn/bc/5522/File.txt";
            request.Input =
                "<S:log-report xmlns:S=\"svn:\"><S:start-revision>5531</S:start-revision><S:end-revision>1</S:end-revision><S:limit>100</S:limit><S:discover-changed-paths/><S:path></S:path></S:log-report>";

            string result = HandlerHandle(
                handler);

            string expected =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<S:log-report xmlns:S=\"svn:\" xmlns:D=\"DAV:\">\n" +
                "<S:log-item>\n" +
                "<D:version-name>5531</D:version-name>\n" +
                "<D:creator-displayname>jwanagel</D:creator-displayname>\n" +
                "<S:date>2007-07-24T07:46:20.635845Z</S:date>\n" +
                "<D:comment>Renamed file</D:comment>\n" +
                "<S:added-path copyfrom-path=\"/newFolder3/NewFile.txt\" copyfrom-rev=\"5530\" node-kind=\"dir\">/newFolder3/NewFileRename.txt</S:added-path>\n" +
                "<S:deleted-path node-kind=\"dir\">/newFolder3/NewFile.txt</S:deleted-path>\n" +
                "</S:log-item>\n" +
                "</S:log-report>\n";

            Assert.Equal(expected, result);
            Assert.Equal("text/xml; charset=\"utf-8\"", response.ContentType);
            Assert.Equal(Encoding.UTF8, response.ContentEncoding);
            Assert.Equal(200, response.StatusCode);
            Assert.True(response.SendChunked);
            Assert.Equal("/File.txt", r.Parameters[0]);
            Assert.Equal(1, r.Parameters[1]);
            Assert.Equal(5531, r.Parameters[2]);
            Assert.Equal(Recursion.Full, r.Parameters[3]);
            Assert.Equal(100, r.Parameters[4]);
        }

        [Fact]
        public void Handle_MergedItems_ProducesCorrectOutput()
        {
            List<SourceItemHistory> histories = new List<SourceItemHistory>();
            SourceItemHistory history1 =
                new SourceItemHistory(5531, "jwanagel", DateTime.Parse("2007-07-24T07:46:20.635845Z"), "Merged file");
            history1.Changes.Add(TestHelper.MakeChange(ChangeType.Merge, "newFolder3"));
            history1.Changes.Add(TestHelper.MakeChange(ChangeType.Merge | ChangeType.Edit, "newFolder3/NewFile.txt", "newFolder3/NewFile.txt", 5530));
            histories.Add(history1);
            Results r = stubs.Attach(provider.GetLog, Return.Value(new LogItem(@"C:\", "newFolder2", histories.ToArray())));
            request.Path = "http://localhost:8082/!svn/bc/5522/File.txt";
            request.Input =
                "<S:log-report xmlns:S=\"svn:\"><S:start-revision>5531</S:start-revision><S:end-revision>1</S:end-revision><S:limit>100</S:limit><S:discover-changed-paths/><S:path></S:path></S:log-report>";

            string result = HandlerHandle(
                handler);

            string expected =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<S:log-report xmlns:S=\"svn:\" xmlns:D=\"DAV:\">\n" +
                "<S:log-item>\n" +
                "<D:version-name>5531</D:version-name>\n" +
                "<D:creator-displayname>jwanagel</D:creator-displayname>\n" +
                "<S:date>2007-07-24T07:46:20.635845Z</S:date>\n" +
                "<D:comment>Merged file</D:comment>\n" +
                "<S:modified-path node-kind=\"dir\">/newFolder3/NewFile.txt</S:modified-path>\n" +
                "</S:log-item>\n" +
                "</S:log-report>\n";

            Assert.Equal(expected, result);
            Assert.Equal("text/xml; charset=\"utf-8\"", response.ContentType);
            Assert.Equal(Encoding.UTF8, response.ContentEncoding);
            Assert.Equal(200, response.StatusCode);
            Assert.True(response.SendChunked);
            Assert.Equal("/File.txt", r.Parameters[0]);
            Assert.Equal(1, r.Parameters[1]);
            Assert.Equal(5531, r.Parameters[2]);
            Assert.Equal(Recursion.Full, r.Parameters[3]);
            Assert.Equal(100, r.Parameters[4]);
        }
    }
}
