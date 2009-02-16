using System;
using SvnBridge.SourceControl;
using CodePlex.TfsLibrary;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using Xunit;
using Attach;
using Tests;

namespace ProtocolTests
{
    public class UpdateWithDeletedFolderThenAddedAgainTest : ProtocolTestsBase
    {
        [Fact]
        public void Test1()
        {
            stubs.Attach(provider.ItemExists, true);

            string request =
                "OPTIONS / HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.3 (r33570)/TortoiseSVN-1.5.4.14259 neon/0.28.3\r\n" +
                "Keep-Alive: \r\n" +
                "Connection: TE, Keep-Alive\r\n" +
                "TE: trailers\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 200 OK\r\n" +
                "Date: Mon, 16 Feb 2009 02:05:17 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "DAV: 1,2\r\n" +
                "DAV: version-control,checkout,working-resource\r\n" +
                "DAV: merge,baseline,activity,version-controlled-collection\r\n" +
                "MS-Author-Via: DAV\r\n" +
                "Allow: OPTIONS,GET,HEAD,POST,DELETE,TRACE,PROPFIND,PROPPATCH,COPY,MOVE,LOCK,UNLOCK,CHECKOUT\r\n" +
                "Content-Length: 0\r\n" +
                "Keep-Alive: timeout=15, max=100\r\n" +
                "Connection: Keep-Alive\r\n" +
                "Content-Type: httpd/unix-directory\r\n" +
                "\r\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test2()
        {
            stubs.Attach(provider.ItemExists, true);
            stubs.Attach(provider.GetItems, CreateFolder(""));

            string request =
                "PROPFIND / HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.3 (r33570)/TortoiseSVN-1.5.4.14259 neon/0.28.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 300\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><version-controlled-configuration xmlns=\"DAV:\"/><resourcetype xmlns=\"DAV:\"/><baseline-relative-path xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/><repository-uuid xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Mon, 16 Feb 2009 02:05:17 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 629\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:version-controlled-configuration><D:href>/!svn/vcc/default</D:href></lp1:version-controlled-configuration>\n" +
                "<lp1:resourcetype><D:collection/></lp1:resourcetype>\n" +
                "<lp2:baseline-relative-path/>\n" +
                "<lp2:repository-uuid>81a5aebe-f34e-eb42-b435-ac1ecbb335f7</lp2:repository-uuid>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "</D:multistatus>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test3()
        {
            stubs.Attach(provider.ItemExists, true);
            stubs.Attach(provider.GetItems, CreateFolder(""));

            string request =
                "PROPFIND / HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.3 (r33570)/TortoiseSVN-1.5.4.14259 neon/0.28.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 300\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><version-controlled-configuration xmlns=\"DAV:\"/><resourcetype xmlns=\"DAV:\"/><baseline-relative-path xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/><repository-uuid xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Mon, 16 Feb 2009 02:05:17 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 629\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:version-controlled-configuration><D:href>/!svn/vcc/default</D:href></lp1:version-controlled-configuration>\n" +
                "<lp1:resourcetype><D:collection/></lp1:resourcetype>\n" +
                "<lp2:baseline-relative-path/>\n" +
                "<lp2:repository-uuid>81a5aebe-f34e-eb42-b435-ac1ecbb335f7</lp2:repository-uuid>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "</D:multistatus>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test4()
        {
            stubs.Attach(provider.GetLatestVersion, 5803);

            string request =
                "PROPFIND /!svn/vcc/default HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.3 (r33570)/TortoiseSVN-1.5.4.14259 neon/0.28.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 111\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><checked-in xmlns=\"DAV:\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Mon, 16 Feb 2009 02:05:17 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 383\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/!svn/vcc/default</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:checked-in><D:href>/!svn/bln/5803</D:href></lp1:checked-in>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "</D:multistatus>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test5()
        {
            string request =
                "PROPFIND /!svn/bln/5803 HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.3 (r33570)/TortoiseSVN-1.5.4.14259 neon/0.28.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 148\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><baseline-collection xmlns=\"DAV:\"/><version-name xmlns=\"DAV:\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Mon, 16 Feb 2009 02:05:18 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 440\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/!svn/bln/5803</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:baseline-collection><D:href>/!svn/bc/5803/</D:href></lp1:baseline-collection>\n" +
                "<lp1:version-name>5803</lp1:version-name>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "</D:multistatus>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test6()
        {
            stubs.Attach(provider.ItemExists, true);
            stubs.Attach(provider.GetItems, CreateFolder(""));

            string request =
                "PROPFIND / HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.3 (r33570)/TortoiseSVN-1.5.4.14259 neon/0.28.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 300\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><version-controlled-configuration xmlns=\"DAV:\"/><resourcetype xmlns=\"DAV:\"/><baseline-relative-path xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/><repository-uuid xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Mon, 16 Feb 2009 02:05:18 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 629\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:version-controlled-configuration><D:href>/!svn/vcc/default</D:href></lp1:version-controlled-configuration>\n" +
                "<lp1:resourcetype><D:collection/></lp1:resourcetype>\n" +
                "<lp2:baseline-relative-path/>\n" +
                "<lp2:repository-uuid>81a5aebe-f34e-eb42-b435-ac1ecbb335f7</lp2:repository-uuid>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "</D:multistatus>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test7()
        {
            stubs.Attach(provider.GetLatestVersion, 5803);

            string request =
                "PROPFIND /!svn/vcc/default HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.3 (r33570)/TortoiseSVN-1.5.4.14259 neon/0.28.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 111\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><checked-in xmlns=\"DAV:\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Mon, 16 Feb 2009 02:05:18 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 383\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/!svn/vcc/default</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:checked-in><D:href>/!svn/bln/5803</D:href></lp1:checked-in>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "</D:multistatus>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test8()
        {
            string request =
                "PROPFIND /!svn/bln/5803 HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.3 (r33570)/TortoiseSVN-1.5.4.14259 neon/0.28.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 148\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><baseline-collection xmlns=\"DAV:\"/><version-name xmlns=\"DAV:\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Mon, 16 Feb 2009 02:05:18 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 440\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/!svn/bln/5803</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:baseline-collection><D:href>/!svn/bc/5803/</D:href></lp1:baseline-collection>\n" +
                "<lp1:version-name>5803</lp1:version-name>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "</D:multistatus>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test9()
        {
            FolderMetaData metadata = new FolderMetaData();
            metadata.Name = "";
            metadata.ItemRevision = 5803;
            metadata.Author = "jwanagel";
            metadata.LastModifiedDate = DateTime.Parse("2009-02-16T02:04:19.600411Z");
            FolderMetaData item2 = new FolderMetaData();
            item2.Name = "folder";
            item2.ItemRevision = 5803;
            item2.Author = "jwanagel";
            item2.LastModifiedDate = DateTime.Parse("2009-02-16T02:04:19.600411Z");
            item2.Properties.Add("bugtraq:message", "Work Item: %BUGID%");
            metadata.Items.Add(item2);
            stubs.Attach(provider.GetChangedItems, metadata);
            stubs.Attach((MyMocks.ItemExists)provider.ItemExists, Return.DelegateResult(delegate(object[] p)
            {
                if (p[0].ToString() == "folder" && p[1].ToString() == "5801")
                    return true;
                else
                    return false;
            }));

            string request =
                "REPORT /!svn/vcc/default HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.3 (r33570)/TortoiseSVN-1.5.4.14259 neon/0.28.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Length: 277\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: svndiff1;q=0.9,svndiff;q=0.8\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "\r\n" +
                "<S:update-report send-all=\"true\" xmlns:S=\"svn:\"><S:src-path>http://localhost:8080</S:src-path><S:target-revision>5803</S:target-revision><S:depth>unknown</S:depth><S:send-copyfrom-args>yes</S:send-copyfrom-args><S:entry rev=\"5801\" depth=\"infinity\" ></S:entry></S:update-report>";

            string expected =
                "HTTP/1.1 200 OK\r\n" +
                "Date: Mon, 16 Feb 2009 02:05:18 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Transfer-Encoding: chunked\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "4a2\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<S:update-report xmlns:S=\"svn:\" xmlns:V=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:D=\"DAV:\" send-all=\"true\">\n" +
                "<S:target-revision rev=\"5803\"/>\n" +
                "<S:open-directory rev=\"5801\">\n" +
                "<D:checked-in><D:href>/!svn/ver/5803/</D:href></D:checked-in>\n" +
                "<S:set-prop name=\"svn:entry:committed-rev\">5803</S:set-prop>\n" +
                "<S:set-prop name=\"svn:entry:committed-date\">2009-02-16T02:04:19.600411Z</S:set-prop>\n" +
                "<S:set-prop name=\"svn:entry:last-author\">jwanagel</S:set-prop>\n" +
                "<S:set-prop name=\"svn:entry:uuid\">81a5aebe-f34e-eb42-b435-ac1ecbb335f7</S:set-prop>\n" +
                "<S:delete-entry name=\"folder\"/>\n" +
                "<S:add-directory name=\"folder\" bc-url=\"/!svn/bc/5803/folder\">\n" +
                "<D:checked-in><D:href>/!svn/ver/5803/folder</D:href></D:checked-in>\n" +
                "<S:set-prop name=\"svn:entry:committed-rev\">5803</S:set-prop>\n" +
                "<S:set-prop name=\"svn:entry:committed-date\">2009-02-16T02:04:19.600411Z</S:set-prop>\n" +
                "<S:set-prop name=\"svn:entry:last-author\">jwanagel</S:set-prop>\n" +
                "<S:set-prop name=\"svn:entry:uuid\">81a5aebe-f34e-eb42-b435-ac1ecbb335f7</S:set-prop>\n" +
                "<S:set-prop name=\"bugtraq:message\">Work Item: %BUGID%</S:set-prop>\n" +
                "<S:prop></S:prop>\n" +
                "</S:add-directory>\n" +
                "<S:prop></S:prop>\n" +
                "</S:open-directory>\n" +
                "</S:update-report>\n" +
                "\r\n" +
                "0\r\n" +
                "\r\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }
    }
}