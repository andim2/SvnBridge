using System;
using SvnBridge.SourceControl;
using CodePlex.TfsLibrary;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using Xunit;
using Attach;
using System.Text;
using Tests;

namespace ProtocolTests
{
    public class DiffTwoUpdatedFilesTest : ProtocolTestsBase
    {
        [Fact]
        public void Test1()
        {
            stubs.Attach((MyMocks.ItemExists)provider.ItemExists, new NetworkAccessDeniedException());

            string request =
                "OPTIONS / HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Keep-Alive: \r\n" +
                "Connection: TE, Keep-Alive\r\n" +
                "TE: trailers\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 401 Authorization Required\r\n" +
                "Date: Tue, 16 Dec 2008 01:11:58 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "WWW-Authenticate: Basic realm=\"CodePlex Subversion Repository\"\r\n" +
                "Content-Length: 493\r\n" +
                "Keep-Alive: timeout=15, max=100\r\n" +
                "Connection: Keep-Alive\r\n" +
                "Content-Type: text/html; charset=iso-8859-1\r\n" +
                "\r\n" +
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>401 Authorization Required</title>\n" +
                "</head><body>\n" +
                "<h1>Authorization Required</h1>\n" +
                "<p>This server could not verify that you\n" +
                "are authorized to access the document\n" +
                "requested.  Either you supplied the wrong\n" +
                "credentials (e.g., bad password), or your\n" +
                "browser doesn't understand how to supply\n" +
                "the credentials required.</p>\n" +
                "<hr>\n" +
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8080</address>\n" +
                "</body></html>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test2()
        {
            stubs.Attach(provider.ItemExists, true);
            ItemMetaData item = new FolderMetaData();
            item.Name = "";
            stubs.Attach(provider.GetItems, item);

            string request =
                "OPTIONS / HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Keep-Alive: \r\n" +
                "Connection: TE, Keep-Alive\r\n" +
                "TE: trailers\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 200 OK\r\n" +
                "Date: Tue, 16 Dec 2008 01:11:58 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "DAV: 1,2\r\n" +
                "DAV: version-control,checkout,working-resource\r\n" +
                "DAV: merge,baseline,activity,version-controlled-collection\r\n" +
                "MS-Author-Via: DAV\r\n" +
                "Allow: OPTIONS,GET,HEAD,POST,DELETE,TRACE,PROPFIND,PROPPATCH,COPY,MOVE,LOCK,UNLOCK,CHECKOUT\r\n" +
                "Content-Length: 0\r\n" +
                "Keep-Alive: timeout=15, max=99\r\n" +
                "Connection: Keep-Alive\r\n" +
                "Content-Type: httpd/unix-directory\r\n" +
                "\r\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test3()
        {
            stubs.Attach(provider.ItemExists, true);
            ItemMetaData item = new FolderMetaData();
            item.Name = "";
            stubs.Attach(provider.GetItems, item);

            string request =
                "PROPFIND / HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 300\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><version-controlled-configuration xmlns=\"DAV:\"/><resourcetype xmlns=\"DAV:\"/><baseline-relative-path xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/><repository-uuid xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Tue, 16 Dec 2008 01:11:58 GMT\r\n" +
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
            stubs.Attach(provider.GetLatestVersion, 5788);

            string request =
                "PROPFIND /!svn/vcc/default HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 111\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><checked-in xmlns=\"DAV:\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Tue, 16 Dec 2008 01:11:58 GMT\r\n" +
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
                "<lp1:checked-in><D:href>/!svn/bln/5788</D:href></lp1:checked-in>\n" +
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
            stubs.Attach(provider.ItemExists, true);
            stubs.Attach(provider.IsDirectory, true);

            string request =
                "PROPFIND /!svn/bln/5788 HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 148\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><baseline-collection xmlns=\"DAV:\"/><version-name xmlns=\"DAV:\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Tue, 16 Dec 2008 01:11:58 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 440\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/!svn/bln/5788</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:baseline-collection><D:href>/!svn/bc/5788/</D:href></lp1:baseline-collection>\n" +
                "<lp1:version-name>5788</lp1:version-name>\n" +
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
            ItemMetaData item = new FolderMetaData();
            item.Name = "";
            stubs.Attach(provider.GetItems, item);

            string request =
                "PROPFIND / HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 300\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><version-controlled-configuration xmlns=\"DAV:\"/><resourcetype xmlns=\"DAV:\"/><baseline-relative-path xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/><repository-uuid xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Tue, 16 Dec 2008 01:11:58 GMT\r\n" +
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
            stubs.Attach(provider.ItemExists, true);
            stubs.Attach(provider.IsDirectory, true);

            string request =
                "PROPFIND /!svn/vcc/default HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Label: 5788\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 148\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><baseline-collection xmlns=\"DAV:\"/><version-name xmlns=\"DAV:\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Tue, 16 Dec 2008 01:11:59 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Vary: Label\r\n" +
                "Content-Length: 440\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/!svn/bln/5788</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:baseline-collection><D:href>/!svn/bc/5788/</D:href></lp1:baseline-collection>\n" +
                "<lp1:version-name>5788</lp1:version-name>\n" +
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
            stubs.Attach(provider.GetItems, new FolderMetaData());

            string request =
                "REPORT /!svn/bc/5788 HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 251\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><S:get-locations xmlns:S=\"svn:\" xmlns:D=\"DAV:\"><S:path></S:path><S:peg-revision>5788</S:peg-revision><S:location-revision>5787</S:location-revision><S:location-revision>5788</S:location-revision></S:get-locations>";

            string expected =
                "HTTP/1.1 200 OK\r\n" +
                "Date: Tue, 16 Dec 2008 01:11:59 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Transfer-Encoding: chunked\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "bc\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<S:get-locations-report xmlns:S=\"svn:\" xmlns:D=\"DAV:\">\n" +
                "<S:location rev=\"5787\" path=\"/\"/>\n" +
                "<S:location rev=\"5788\" path=\"/\"/>\n" +
                "</S:get-locations-report>\n" +
                "\r\n" +
                "0\r\n" +
                "\r\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test9()
        {
            stubs.Attach(provider.ItemExists, true);
            ItemMetaData item = new FolderMetaData();
            item.Name = "";
            stubs.Attach(provider.GetItems, item);

            string request =
                "PROPFIND / HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 300\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><version-controlled-configuration xmlns=\"DAV:\"/><resourcetype xmlns=\"DAV:\"/><baseline-relative-path xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/><repository-uuid xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Tue, 16 Dec 2008 01:11:59 GMT\r\n" +
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
        public void Test10()
        {
            stubs.Attach(provider.ItemExists, true);
            stubs.Attach(provider.IsDirectory, true);

            string request =
                "PROPFIND /!svn/vcc/default HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Label: 5788\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 148\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><baseline-collection xmlns=\"DAV:\"/><version-name xmlns=\"DAV:\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Tue, 16 Dec 2008 01:11:59 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Vary: Label\r\n" +
                "Content-Length: 440\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/!svn/bln/5788</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:baseline-collection><D:href>/!svn/bc/5788/</D:href></lp1:baseline-collection>\n" +
                "<lp1:version-name>5788</lp1:version-name>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "</D:multistatus>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test11()
        {
            stubs.Attach(provider.ItemExists, true);
            ItemMetaData item = new FolderMetaData();
            item.Name = "";
            stubs.Attach(provider.GetItems, item);

            string request =
                "PROPFIND /!svn/bc/5788 HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 300\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><version-controlled-configuration xmlns=\"DAV:\"/><resourcetype xmlns=\"DAV:\"/><baseline-relative-path xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/><repository-uuid xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Tue, 16 Dec 2008 01:12:00 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 642\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/!svn/bc/5788/</D:href>\n" +
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
        public void Test12()
        {
            stubs.Attach(provider.ItemExists, true);
            ItemMetaData item = new FolderMetaData();
            item.Name = "";
            stubs.Attach(provider.GetItems, item);

            string request =
                "PROPFIND / HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 300\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><version-controlled-configuration xmlns=\"DAV:\"/><resourcetype xmlns=\"DAV:\"/><baseline-relative-path xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/><repository-uuid xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Tue, 16 Dec 2008 01:12:00 GMT\r\n" +
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
        public void Test13()
        {
            stubs.Attach(provider.ItemExists, true);
            stubs.Attach(provider.IsDirectory, true);

            string request =
                "PROPFIND /!svn/vcc/default HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Label: 5787\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 148\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><baseline-collection xmlns=\"DAV:\"/><version-name xmlns=\"DAV:\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Tue, 16 Dec 2008 01:12:00 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Vary: Label\r\n" +
                "Content-Length: 440\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/!svn/bln/5787</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:baseline-collection><D:href>/!svn/bc/5787/</D:href></lp1:baseline-collection>\n" +
                "<lp1:version-name>5787</lp1:version-name>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "</D:multistatus>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test14()
        {
            stubs.Attach(provider.ItemExists, true);
            ItemMetaData item = new FolderMetaData();
            item.Name = "";
            stubs.Attach(provider.GetItems, item);

            string request =
                "PROPFIND /!svn/bc/5787 HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 300\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><version-controlled-configuration xmlns=\"DAV:\"/><resourcetype xmlns=\"DAV:\"/><baseline-relative-path xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/><repository-uuid xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Tue, 16 Dec 2008 01:12:00 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 642\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/!svn/bc/5787/</D:href>\n" +
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
        public void Test15()
        {
            stubs.Attach((MyMocks.ItemExists)provider.ItemExists, new NetworkAccessDeniedException());

            string request =
                "OPTIONS / HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Keep-Alive: \r\n" +
                "Connection: TE, Keep-Alive\r\n" +
                "TE: trailers\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 401 Authorization Required\r\n" +
                "Date: Tue, 16 Dec 2008 01:12:00 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "WWW-Authenticate: Basic realm=\"CodePlex Subversion Repository\"\r\n" +
                "Content-Length: 493\r\n" +
                "Keep-Alive: timeout=15, max=100\r\n" +
                "Connection: Keep-Alive\r\n" +
                "Content-Type: text/html; charset=iso-8859-1\r\n" +
                "\r\n" +
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>401 Authorization Required</title>\n" +
                "</head><body>\n" +
                "<h1>Authorization Required</h1>\n" +
                "<p>This server could not verify that you\n" +
                "are authorized to access the document\n" +
                "requested.  Either you supplied the wrong\n" +
                "credentials (e.g., bad password), or your\n" +
                "browser doesn't understand how to supply\n" +
                "the credentials required.</p>\n" +
                "<hr>\n" +
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8080</address>\n" +
                "</body></html>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test16()
        {
            stubs.Attach(provider.ItemExists, true);
            ItemMetaData item = new FolderMetaData();
            item.Name = "";
            stubs.Attach(provider.GetItems, item);

            string request =
                "OPTIONS / HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Keep-Alive: \r\n" +
                "Connection: TE, Keep-Alive\r\n" +
                "TE: trailers\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 200 OK\r\n" +
                "Date: Tue, 16 Dec 2008 01:12:00 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "DAV: 1,2\r\n" +
                "DAV: version-control,checkout,working-resource\r\n" +
                "DAV: merge,baseline,activity,version-controlled-collection\r\n" +
                "MS-Author-Via: DAV\r\n" +
                "Allow: OPTIONS,GET,HEAD,POST,DELETE,TRACE,PROPFIND,PROPPATCH,COPY,MOVE,LOCK,UNLOCK,CHECKOUT\r\n" +
                "Content-Length: 0\r\n" +
                "Keep-Alive: timeout=15, max=99\r\n" +
                "Connection: Keep-Alive\r\n" +
                "Content-Type: httpd/unix-directory\r\n" +
                "\r\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact(Skip="Not finished")]
        public void Test17()
        {
            string request =
                "REPORT /!svn/vcc/default HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Length: 318\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Accept-Encoding: svndiff1;q=0.9,svndiff;q=0.8\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<S:update-report send-all=\"false\" xmlns:S=\"svn:\"><S:src-path>http://localhost:8080</S:src-path><S:target-revision>5788</S:target-revision><S:dst-path>http://localhost:8080</S:dst-path><S:depth>unknown</S:depth><S:ignore-ancestry>yes</S:ignore-ancestry><S:entry rev=\"5787\" depth=\"infinity\" ></S:entry></S:update-report>";

            string expected =
                "HTTP/1.1 200 OK\r\n" +
                "Date: Tue, 16 Dec 2008 01:12:00 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Transfer-Encoding: chunked\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "8c5\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<S:update-report xmlns:S=\"svn:\" xmlns:V=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:D=\"DAV:\" >\n" +
                "<S:target-revision rev=\"5788\"/>\n" +
                "<S:open-directory rev=\"5787\">\n" +
                "<D:checked-in><D:href>/!svn/ver/5788/</D:href></D:checked-in>\n" +
                "<S:open-directory name=\"trunk\" rev=\"5787\">\n" +
                "<D:checked-in><D:href>/!svn/ver/5788/trunk</D:href></D:checked-in>\n" +
                "<S:open-directory name=\"A !@#$%^&amp;()_-+={[}];',.~`\" rev=\"5787\">\n" +
                "<D:checked-in><D:href>/!svn/ver/5788/trunk/A%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60</D:href></D:checked-in>\n" +
                "<S:open-file name=\"G !@#$%^&amp;_-+={[}];',.~`.txt\" rev=\"5787\">\n" +
                "<D:checked-in><D:href>/!svn/ver/5788/trunk/A%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60/G%20!@%23$%25%5E&amp;_-+=%7B%5B%7D%5D%3B',.~%60.txt</D:href></D:checked-in>\n" +
                "<S:fetch-file base-checksum=\"5a105e8b9d40e1329780d62ea2265d8a\"/>\n" +
                "<S:prop><V:md5-checksum>ad0234829205b9033196ba818f7a872b</V:md5-checksum><D:version-name>5788</D:version-name><D:creationdate>2008-12-16T01:04:17.085375Z</D:creationdate><D:creator-displayname>jwanagel</D:creator-displayname></S:prop>\n" +
                "</S:open-file>\n" +
                "<S:open-file name=\"H !@#$%^&amp;()_-+={[}];',.~`.txt\" rev=\"5787\">\n" +
                "<D:checked-in><D:href>/!svn/ver/5788/trunk/A%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60/H%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60.txt</D:href></D:checked-in>\n" +
                "<S:fetch-file base-checksum=\"ef62106567f5237204ea8bf14ce1b757\"/>\n" +
                "<S:prop><V:md5-checksum>73be831b01b9773f48e0bdfb1f223fd7</V:md5-checksum><D:version-name>5788</D:version-name><D:creationdate>2008-12-16T01:04:17.085375Z</D:creationdate><D:creator-displayname>jwanagel</D:creator-displayname></S:prop>\n" +
                "</S:open-file>\n" +
                "<S:prop><D:version-name>5788</D:version-name><D:creationdate>2008-12-16T01:04:17.085375Z</D:creationdate><D:creator-displayname>jwanagel</D:creator-displayname></S:prop>\n" +
                "</S:open-directory>\n" +
                "<S:prop><D:version-name>5788</D:version-name><D:creationdate>2008-12-16T01:04:17.085375Z</D:creationdate><D:creator-displayname>jwanagel</D:creator-displayname></S:prop>\n" +
                "</S:open-directory>\n" +
                "<S:prop><D:version-name>5788</D:version-name><D:creationdate>2008-12-16T01:04:17.085375Z</D:creationdate><D:creator-displayname>jwanagel</D:creator-displayname></S:prop>\n" +
                "</S:open-directory>\n" +
                "</S:update-report>\n" +
                "\r\n" +
                "0\r\n" +
                "\r\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test18()
        {
            stubs.Attach(provider.ItemExists, true);
            ItemMetaData item = new FolderMetaData();
            item.Name = "";
            stubs.Attach(provider.GetItems, item);

            string request =
                "PROPFIND / HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 300\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><version-controlled-configuration xmlns=\"DAV:\"/><resourcetype xmlns=\"DAV:\"/><baseline-relative-path xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/><repository-uuid xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Tue, 16 Dec 2008 01:12:01 GMT\r\n" +
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
        public void Test19()
        {
            stubs.Attach(provider.ItemExists, true);
            stubs.Attach(provider.IsDirectory, true);

            string request =
                "PROPFIND /!svn/vcc/default HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Label: 5787\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 148\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><baseline-collection xmlns=\"DAV:\"/><version-name xmlns=\"DAV:\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Tue, 16 Dec 2008 01:12:01 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Vary: Label\r\n" +
                "Content-Length: 440\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/!svn/bln/5787</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:baseline-collection><D:href>/!svn/bc/5787/</D:href></lp1:baseline-collection>\n" +
                "<lp1:version-name>5787</lp1:version-name>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "</D:multistatus>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test20()
        {
            stubs.Attach(provider.ItemExists, true);
            ItemMetaData item = new FolderMetaData();
            item.Name = "";
            item.Properties.Add("C:bugtraq:message", "Work Item: %BUGID%");
            stubs.Attach(provider.GetItems, item);

            string request =
                "PROPFIND /!svn/bc/5787 HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 150\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><deadprop-count xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Tue, 16 Dec 2008 01:12:01 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 410\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/!svn/bc/5787/</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp2:deadprop-count>1</lp2:deadprop-count>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "</D:multistatus>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test21()
        {
            ItemMetaData item = new FolderMetaData();
            item.Name = "";
            item.ItemRevision = 5787;
            item.Author = "jwanagel";
            item.LastModifiedDate = DateTime.Parse("2008-12-16T01:03:54.820035Z");
            item.Properties.Add("bugtraq:message", "Work Item: %BUGID%");
            stubs.Attach(provider.GetItems, item);

            string request =
                "PROPFIND /!svn/bc/5787 HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 82\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Tue, 16 Dec 2008 01:12:01 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 1273\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:S=\"http://subversion.tigris.org/xmlns/svn/\" xmlns:C=\"http://subversion.tigris.org/xmlns/custom/\" xmlns:V=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/!svn/bc/5787/</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<C:bugtraq:message>Work Item: %BUGID%</C:bugtraq:message>\n" +
                "<lp1:getcontenttype>text/html; charset=UTF-8</lp1:getcontenttype>\n" +
                "<lp1:getetag>\"5787//\"</lp1:getetag>\n" +
                "<lp1:creationdate>2008-12-16T01:03:54.820035Z</lp1:creationdate>\n" +
                "<lp1:getlastmodified>Tue, 16 Dec 2008 01:03:54 GMT</lp1:getlastmodified>\n" +
                "<lp1:checked-in><D:href>/!svn/ver/5787/</D:href></lp1:checked-in>\n" +
                "<lp1:version-controlled-configuration><D:href>/!svn/vcc/default</D:href></lp1:version-controlled-configuration>\n" +
                "<lp1:version-name>5787</lp1:version-name>\n" +
                "<lp1:creator-displayname>jwanagel</lp1:creator-displayname>\n" +
                "<lp2:baseline-relative-path/>\n" +
                "<lp2:repository-uuid>81a5aebe-f34e-eb42-b435-ac1ecbb335f7</lp2:repository-uuid>\n" +
                "<lp2:deadprop-count>1</lp2:deadprop-count>\n" +
                "<lp1:resourcetype><D:collection/></lp1:resourcetype>\n" +
                "<D:lockdiscovery/>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "</D:multistatus>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test22()
        {
            stubs.Attach(provider.ItemExists, true);
            ItemMetaData item = new FolderMetaData();
            item.Name = "trunk";
            stubs.Attach(provider.GetItems, item);

            string request =
                "PROPFIND /trunk HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 300\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><version-controlled-configuration xmlns=\"DAV:\"/><resourcetype xmlns=\"DAV:\"/><baseline-relative-path xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/><repository-uuid xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Tue, 16 Dec 2008 01:12:01 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 668\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/trunk/</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:version-controlled-configuration><D:href>/!svn/vcc/default</D:href></lp1:version-controlled-configuration>\n" +
                "<lp1:resourcetype><D:collection/></lp1:resourcetype>\n" +
                "<lp2:baseline-relative-path>trunk</lp2:baseline-relative-path>\n" +
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
        public void Test23()
        {
            stubs.Attach(provider.ItemExists, true);
            stubs.Attach(provider.IsDirectory, true);

            string request =
                "PROPFIND /!svn/vcc/default HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Label: 5787\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 148\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><baseline-collection xmlns=\"DAV:\"/><version-name xmlns=\"DAV:\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Tue, 16 Dec 2008 01:12:02 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Vary: Label\r\n" +
                "Content-Length: 440\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/!svn/bln/5787</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:baseline-collection><D:href>/!svn/bc/5787/</D:href></lp1:baseline-collection>\n" +
                "<lp1:version-name>5787</lp1:version-name>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "</D:multistatus>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test24()
        {
            stubs.Attach(provider.ItemExists, true);
            ItemMetaData item = new FolderMetaData();
            item.Name = "trunk";
            stubs.Attach(provider.GetItems, item);

            string request =
                "PROPFIND /!svn/bc/5787/trunk HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 150\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><deadprop-count xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Tue, 16 Dec 2008 01:12:02 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 416\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/!svn/bc/5787/trunk/</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp2:deadprop-count>0</lp2:deadprop-count>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "</D:multistatus>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test25()
        {
            ItemMetaData item = new FolderMetaData();
            item.Name = "trunk";
            item.ItemRevision = 5787;
            item.Author = "jwanagel";
            item.LastModifiedDate = DateTime.Parse("2008-12-16T01:03:54.820035Z");
            stubs.Attach(provider.GetItems, item);

            string request =
                "PROPFIND /!svn/bc/5787/trunk HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 82\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Tue, 16 Dec 2008 01:12:02 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 1264\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:S=\"http://subversion.tigris.org/xmlns/svn/\" xmlns:C=\"http://subversion.tigris.org/xmlns/custom/\" xmlns:V=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/!svn/bc/5787/trunk/</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:getcontenttype>text/html; charset=UTF-8</lp1:getcontenttype>\n" +
                "<lp1:getetag>\"5787//trunk\"</lp1:getetag>\n" +
                "<lp1:creationdate>2008-12-16T01:03:54.820035Z</lp1:creationdate>\n" +
                "<lp1:getlastmodified>Tue, 16 Dec 2008 01:03:54 GMT</lp1:getlastmodified>\n" +
                "<lp1:checked-in><D:href>/!svn/ver/5787/trunk</D:href></lp1:checked-in>\n" +
                "<lp1:version-controlled-configuration><D:href>/!svn/vcc/default</D:href></lp1:version-controlled-configuration>\n" +
                "<lp1:version-name>5787</lp1:version-name>\n" +
                "<lp1:creator-displayname>jwanagel</lp1:creator-displayname>\n" +
                "<lp2:baseline-relative-path>trunk</lp2:baseline-relative-path>\n" +
                "<lp2:repository-uuid>81a5aebe-f34e-eb42-b435-ac1ecbb335f7</lp2:repository-uuid>\n" +
                "<lp2:deadprop-count>0</lp2:deadprop-count>\n" +
                "<lp1:resourcetype><D:collection/></lp1:resourcetype>\n" +
                "<D:lockdiscovery/>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "</D:multistatus>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test26()
        {
            stubs.Attach(provider.ItemExists, true);
            ItemMetaData item = new FolderMetaData();
            item.Name = "trunk/A !@#$%^&()_-+={[}];',.~`";
            stubs.Attach(provider.GetItems, item);

            string request =
                "PROPFIND /trunk/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60 HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 300\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><version-controlled-configuration xmlns=\"DAV:\"/><resourcetype xmlns=\"DAV:\"/><baseline-relative-path xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/><repository-uuid xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Tue, 16 Dec 2008 01:12:02 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 748\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/trunk/A%20!@%23$%25%5e&amp;()_-+=%7b%5b%7d%5d%3b',.~%60/</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:version-controlled-configuration><D:href>/!svn/vcc/default</D:href></lp1:version-controlled-configuration>\n" +
                "<lp1:resourcetype><D:collection/></lp1:resourcetype>\n" +
                "<lp2:baseline-relative-path>trunk/A !@#$%^&amp;()_-+={[}];',.~`</lp2:baseline-relative-path>\n" +
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
        public void Test27()
        {
            stubs.Attach(provider.ItemExists, true);
            stubs.Attach(provider.IsDirectory, true);

            string request =
                "PROPFIND /!svn/vcc/default HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Label: 5787\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 148\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><baseline-collection xmlns=\"DAV:\"/><version-name xmlns=\"DAV:\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Tue, 16 Dec 2008 01:12:02 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Vary: Label\r\n" +
                "Content-Length: 440\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/!svn/bln/5787</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:baseline-collection><D:href>/!svn/bc/5787/</D:href></lp1:baseline-collection>\n" +
                "<lp1:version-name>5787</lp1:version-name>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "</D:multistatus>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test28()
        {
            stubs.Attach(provider.ItemExists, true);
            ItemMetaData item = new FolderMetaData();
            item.Name = "trunk/A !@#$%^&()_-+={[}];',.~`";
            stubs.Attach(provider.GetItems, item);

            string request =
                "PROPFIND /!svn/bc/5787/trunk/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60 HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 150\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><deadprop-count xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Tue, 16 Dec 2008 01:12:03 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 466\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/!svn/bc/5787/trunk/A%20!@%23$%25%5e&amp;()_-+=%7b%5b%7d%5d%3b',.~%60/</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp2:deadprop-count>0</lp2:deadprop-count>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "</D:multistatus>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test29()
        {
            ItemMetaData item = new FolderMetaData();
            item.Name = "trunk/A !@#$%^&()_-+={[}];',.~`";
            item.ItemRevision = 5787;
            item.Author = "jwanagel";
            item.LastModifiedDate = DateTime.Parse("2008-12-16T01:03:54.820035Z");
            stubs.Attach(provider.GetItems, item);

            string request =
                "PROPFIND /!svn/bc/5787/trunk/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60 HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 82\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Tue, 16 Dec 2008 01:12:03 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 1424\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:S=\"http://subversion.tigris.org/xmlns/svn/\" xmlns:C=\"http://subversion.tigris.org/xmlns/custom/\" xmlns:V=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/!svn/bc/5787/trunk/A%20!@%23$%25%5e&amp;()_-+=%7b%5b%7d%5d%3b',.~%60/</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:getcontenttype>text/html; charset=UTF-8</lp1:getcontenttype>\n" +
                "<lp1:getetag>\"5787//trunk/A !@#$%^&amp;()_-+={[}];',.~`\"</lp1:getetag>\n" +
                "<lp1:creationdate>2008-12-16T01:03:54.820035Z</lp1:creationdate>\n" +
                "<lp1:getlastmodified>Tue, 16 Dec 2008 01:03:54 GMT</lp1:getlastmodified>\n" +
                "<lp1:checked-in><D:href>/!svn/ver/5787/trunk/A%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60</D:href></lp1:checked-in>\n" +
                "<lp1:version-controlled-configuration><D:href>/!svn/vcc/default</D:href></lp1:version-controlled-configuration>\n" +
                "<lp1:version-name>5787</lp1:version-name>\n" +
                "<lp1:creator-displayname>jwanagel</lp1:creator-displayname>\n" +
                "<lp2:baseline-relative-path>trunk/A !@#$%^&amp;()_-+={[}];',.~`</lp2:baseline-relative-path>\n" +
                "<lp2:repository-uuid>81a5aebe-f34e-eb42-b435-ac1ecbb335f7</lp2:repository-uuid>\n" +
                "<lp2:deadprop-count>0</lp2:deadprop-count>\n" +
                "<lp1:resourcetype><D:collection/></lp1:resourcetype>\n" +
                "<D:lockdiscovery/>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "</D:multistatus>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test30()
        {
            stubs.Attach(provider.ItemExists, true);
            ItemMetaData item = new ItemMetaData();
            item.Name = "trunk/A !@#$%^&()_-+={[}];',.~`/G !@#$%^&_-+={[}];',.~`.txt";
            stubs.Attach(provider.GetItems, item);

            string request =
                "PROPFIND /trunk/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/G%20!@%23$%25%5E&_-+=%7B%5B%7D%5D%3B',.~%60.txt HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 300\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><version-controlled-configuration xmlns=\"DAV:\"/><resourcetype xmlns=\"DAV:\"/><baseline-relative-path xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/><repository-uuid xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Tue, 16 Dec 2008 01:12:03 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 798\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/trunk/A%20!@%23$%25%5e&amp;()_-+=%7b%5b%7d%5d%3b',.~%60/G%20!@%23$%25%5e&amp;_-+=%7b%5b%7d%5d%3b',.~%60.txt</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:version-controlled-configuration><D:href>/!svn/vcc/default</D:href></lp1:version-controlled-configuration>\n" +
                "<lp1:resourcetype/>\n" +
                "<lp2:baseline-relative-path>trunk/A !@#$%^&amp;()_-+={[}];',.~`/G !@#$%^&amp;_-+={[}];',.~`.txt</lp2:baseline-relative-path>\n" +
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
        public void Test31()
        {
            stubs.Attach(provider.ItemExists, true);
            stubs.Attach(provider.IsDirectory, true);

            string request =
                "PROPFIND /!svn/vcc/default HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Label: 5787\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 148\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><baseline-collection xmlns=\"DAV:\"/><version-name xmlns=\"DAV:\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Tue, 16 Dec 2008 01:12:03 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Vary: Label\r\n" +
                "Content-Length: 440\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/!svn/bln/5787</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:baseline-collection><D:href>/!svn/bc/5787/</D:href></lp1:baseline-collection>\n" +
                "<lp1:version-name>5787</lp1:version-name>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "</D:multistatus>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test32()
        {
            stubs.Attach(provider.ItemExists, true);
            ItemMetaData item = new ItemMetaData();
            item.ItemRevision = 5787;
            item.Name = "trunk/A !@#$%^&()_-+={[}];',.~`/G !@#$%^&_-+={[}];',.~`.txt";
            stubs.Attach(provider.GetItems, item);
            byte[] fileData = Encoding.UTF8.GetBytes("test1");
            stubs.Attach((MyMocks.ReadFile)provider.ReadFile, fileData);

            string request =
                "PROPFIND /!svn/bc/5787/trunk/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/G%20!@%23$%25%5E&_-+=%7B%5B%7D%5D%3B',.~%60.txt HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 148\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><md5-checksum xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Tue, 16 Dec 2008 01:12:04 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 544\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/!svn/bc/5787/trunk/A%20!@%23$%25%5e&amp;()_-+=%7b%5b%7d%5d%3b',.~%60/G%20!@%23$%25%5e&amp;_-+=%7b%5b%7d%5d%3b',.~%60.txt</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp2:md5-checksum>5a105e8b9d40e1329780d62ea2265d8a</lp2:md5-checksum>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "</D:multistatus>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test33()
        {
            stubs.Attach(provider.ItemExists, true);
            ItemMetaData item = new ItemMetaData();
            item.ItemRevision = 5787;
            item.LastModifiedDate = DateTime.Parse("Tue, 16 Dec 2008 01:03:54 GMT");
            item.Name = "trunk/A !@#$%^&()_-+={[}];',.~`/G !@#$%^&_-+={[}];',.~`.txt";
            stubs.Attach(provider.GetItems, item);
            byte[] fileData = Encoding.UTF8.GetBytes("test1");
            stubs.Attach((MyMocks.ReadFile)provider.ReadFile, fileData);

            string request =
                "GET /!svn/bc/5787/trunk/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/G%20!@%23$%25%5E&_-+=%7B%5B%7D%5D%3B',.~%60.txt HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 200 OK\r\n" +
                "Date: Tue, 16 Dec 2008 01:12:04 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Last-Modified: Tue, 16 Dec 2008 01:03:54 GMT\r\n" +
                "ETag: \"5787//trunk/A !@#$%^&amp;()_-+={[}];',.~`/G !@#$%^&amp;_-+={[}];',.~`.txt\"\r\n" +
                "Accept-Ranges: bytes\r\n" +
                "Content-Length: 5\r\n" +
                "Content-Type: text/plain\r\n" +
                "\r\n" +
                "test1";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test34()
        {
            ItemMetaData item = new ItemMetaData();
            item.Name = "trunk/A !@#$%^&()_-+={[}];',.~`/G !@#$%^&_-+={[}];',.~`.txt";
            item.ItemRevision = 5787;
            item.Author = "jwanagel";
            item.LastModifiedDate = DateTime.Parse("2008-12-16T01:03:54.820035Z");
            stubs.Attach(provider.GetItems, item);
            byte[] fileData = Encoding.UTF8.GetBytes("test1");
            stubs.Attach((MyMocks.ReadFile)provider.ReadFile, fileData);

            string request =
                "PROPFIND /!svn/bc/5787/trunk/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/G%20!@%23$%25%5E&_-+=%7B%5B%7D%5D%3B',.~%60.txt HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 82\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Tue, 16 Dec 2008 01:12:04 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 1803\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:S=\"http://subversion.tigris.org/xmlns/svn/\" xmlns:C=\"http://subversion.tigris.org/xmlns/custom/\" xmlns:V=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/!svn/bc/5787/trunk/A%20!@%23$%25%5e&amp;()_-+=%7b%5b%7d%5d%3b',.~%60/G%20!@%23$%25%5e&amp;_-+=%7b%5b%7d%5d%3b',.~%60.txt</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:getcontentlength>5</lp1:getcontentlength>\n" +
                "<lp1:getcontenttype>text/plain</lp1:getcontenttype>\n" +
                "<lp1:getetag>\"5787//trunk/A !@#$%^&amp;()_-+={[}];',.~`/G !@#$%^&amp;_-+={[}];',.~`.txt\"</lp1:getetag>\n" +
                "<lp1:creationdate>2008-12-16T01:03:54.820035Z</lp1:creationdate>\n" +
                "<lp1:getlastmodified>Tue, 16 Dec 2008 01:03:54 GMT</lp1:getlastmodified>\n" +
                "<lp1:checked-in><D:href>/!svn/ver/5787/trunk/A%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60/G%20!@%23$%25%5E&amp;_-+=%7B%5B%7D%5D%3B',.~%60.txt</D:href></lp1:checked-in>\n" +
                "<lp1:version-controlled-configuration><D:href>/!svn/vcc/default</D:href></lp1:version-controlled-configuration>\n" +
                "<lp1:version-name>5787</lp1:version-name>\n" +
                "<lp1:creator-displayname>jwanagel</lp1:creator-displayname>\n" +
                "<lp2:baseline-relative-path>trunk/A !@#$%^&amp;()_-+={[}];',.~`/G !@#$%^&amp;_-+={[}];',.~`.txt</lp2:baseline-relative-path>\n" +
                "<lp2:md5-checksum>5a105e8b9d40e1329780d62ea2265d8a</lp2:md5-checksum>\n" +
                "<lp2:repository-uuid>81a5aebe-f34e-eb42-b435-ac1ecbb335f7</lp2:repository-uuid>\n" +
                "<lp2:deadprop-count>0</lp2:deadprop-count>\n" +
                "<lp1:resourcetype/>\n" +
                "<D:supportedlock>\n" +
                "<D:lockentry>\n" +
                "<D:lockscope><D:exclusive/></D:lockscope>\n" +
                "<D:locktype><D:write/></D:locktype>\n" +
                "</D:lockentry>\n" +
                "</D:supportedlock>\n" +
                "<D:lockdiscovery/>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "</D:multistatus>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test35()
        {
            stubs.Attach(provider.ItemExists, true);
            ItemMetaData item = new ItemMetaData();
            item.ItemRevision = 5788;
            item.LastModifiedDate = DateTime.Parse("Tue, 16 Dec 2008 01:04:17 GMT");
            item.Name = "trunk/A !@#$%^&()_-+={[}];',.~`/G !@#$%^&_-+={[}];',.~`.txt";
            stubs.Attach(provider.GetItems, item);
            byte[] fileData = Encoding.UTF8.GetBytes("test2");
            stubs.Attach((MyMocks.ReadFile)provider.ReadFile, fileData);
            stubs.Attach(provider.GetLatestVersion, 5788);

            string request =
                "GET /!svn/ver/5788/trunk/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/G%20!@%23$%25%5E&_-+=%7B%5B%7D%5D%3B',.~%60.txt HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 200 OK\r\n" +
                "Date: Tue, 16 Dec 2008 01:12:04 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Last-Modified: Tue, 16 Dec 2008 01:04:17 GMT\r\n" +
                "ETag: \"5788//trunk/A !@#$%^&amp;()_-+={[}];',.~`/G !@#$%^&amp;_-+={[}];',.~`.txt\"\r\n" +
                "Accept-Ranges: bytes\r\n" +
                "Content-Length: 5\r\n" +
                "Content-Type: text/plain\r\n" +
                "\r\n" +
                "test2";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test36()
        {
            stubs.Attach(provider.ItemExists, true);
            ItemMetaData item = new ItemMetaData();
            item.Name = "trunk/A !@#$%^&()_-+={[}];',.~`/H !@#$%^&()_-+={[}];',.~`.txt";
            stubs.Attach(provider.GetItems, item);

            string request =
                "PROPFIND /trunk/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/H%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60.txt HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 300\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><version-controlled-configuration xmlns=\"DAV:\"/><resourcetype xmlns=\"DAV:\"/><baseline-relative-path xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/><repository-uuid xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Tue, 16 Dec 2008 01:12:04 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 802\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/trunk/A%20!@%23$%25%5e&amp;()_-+=%7b%5b%7d%5d%3b',.~%60/H%20!@%23$%25%5e&amp;()_-+=%7b%5b%7d%5d%3b',.~%60.txt</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:version-controlled-configuration><D:href>/!svn/vcc/default</D:href></lp1:version-controlled-configuration>\n" +
                "<lp1:resourcetype/>\n" +
                "<lp2:baseline-relative-path>trunk/A !@#$%^&amp;()_-+={[}];',.~`/H !@#$%^&amp;()_-+={[}];',.~`.txt</lp2:baseline-relative-path>\n" +
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
        public void Test37()
        {
            stubs.Attach(provider.ItemExists, true);
            stubs.Attach(provider.IsDirectory, true);

            string request =
                "PROPFIND /!svn/vcc/default HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Label: 5787\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 148\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><baseline-collection xmlns=\"DAV:\"/><version-name xmlns=\"DAV:\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Tue, 16 Dec 2008 01:12:04 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Vary: Label\r\n" +
                "Content-Length: 440\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/!svn/bln/5787</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:baseline-collection><D:href>/!svn/bc/5787/</D:href></lp1:baseline-collection>\n" +
                "<lp1:version-name>5787</lp1:version-name>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "</D:multistatus>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test38()
        {
            stubs.Attach(provider.ItemExists, true);
            ItemMetaData item = new ItemMetaData();
            item.ItemRevision = 5787;
            item.Name = "trunk/A !@#$%^&()_-+={[}];',.~`/H !@#$%^&()_-+={[}];',.~`.txt";
            stubs.Attach(provider.GetItems, item);
            byte[] fileData = Encoding.UTF8.GetBytes("test2 my change");
            stubs.Attach((MyMocks.ReadFile)provider.ReadFile, fileData);

            string request =
                "PROPFIND /!svn/bc/5787/trunk/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/H%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60.txt HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 148\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><md5-checksum xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Tue, 16 Dec 2008 01:12:04 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 546\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/!svn/bc/5787/trunk/A%20!@%23$%25%5e&amp;()_-+=%7b%5b%7d%5d%3b',.~%60/H%20!@%23$%25%5e&amp;()_-+=%7b%5b%7d%5d%3b',.~%60.txt</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp2:md5-checksum>ef62106567f5237204ea8bf14ce1b757</lp2:md5-checksum>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "</D:multistatus>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test39()
        {
            stubs.Attach(provider.ItemExists, true);
            ItemMetaData item = new ItemMetaData();
            item.ItemRevision = 5787;
            item.LastModifiedDate = DateTime.Parse("Tue, 16 Dec 2008 01:03:54 GMT");
            item.Name = "trunk/A !@#$%^&()_-+={[}];',.~`/H !@#$%^&()_-+={[}];',.~`.txt";
            stubs.Attach(provider.GetItems, item);
            byte[] fileData = Encoding.UTF8.GetBytes("test2 my change");
            stubs.Attach((MyMocks.ReadFile)provider.ReadFile, fileData);

            string request =
                "GET /!svn/bc/5787/trunk/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/H%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60.txt HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 200 OK\r\n" +
                "Date: Tue, 16 Dec 2008 01:12:05 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Last-Modified: Tue, 16 Dec 2008 01:03:54 GMT\r\n" +
                "ETag: \"5787//trunk/A !@#$%^&amp;()_-+={[}];',.~`/H !@#$%^&amp;()_-+={[}];',.~`.txt\"\r\n" +
                "Accept-Ranges: bytes\r\n" +
                "Content-Length: 15\r\n" +
                "Content-Type: text/plain\r\n" +
                "\r\n" +
                "test2 my change";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test40()
        {
            ItemMetaData item = new ItemMetaData();
            item.Name = "trunk/A !@#$%^&()_-+={[}];',.~`/H !@#$%^&()_-+={[}];',.~`.txt";
            item.ItemRevision = 5787;
            item.Author = "jwanagel";
            item.LastModifiedDate = DateTime.Parse("2008-12-16T01:03:54.820035Z");
            stubs.Attach(provider.GetItems, item);
            byte[] fileData = Encoding.UTF8.GetBytes("test2 my change");
            stubs.Attach((MyMocks.ReadFile)provider.ReadFile, fileData);

            string request =
                "PROPFIND /!svn/bc/5787/trunk/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/H%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60.txt HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 82\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Tue, 16 Dec 2008 01:12:05 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 1812\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:S=\"http://subversion.tigris.org/xmlns/svn/\" xmlns:C=\"http://subversion.tigris.org/xmlns/custom/\" xmlns:V=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/!svn/bc/5787/trunk/A%20!@%23$%25%5e&amp;()_-+=%7b%5b%7d%5d%3b',.~%60/H%20!@%23$%25%5e&amp;()_-+=%7b%5b%7d%5d%3b',.~%60.txt</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:getcontentlength>15</lp1:getcontentlength>\n" +
                "<lp1:getcontenttype>text/plain</lp1:getcontenttype>\n" +
                "<lp1:getetag>\"5787//trunk/A !@#$%^&amp;()_-+={[}];',.~`/H !@#$%^&amp;()_-+={[}];',.~`.txt\"</lp1:getetag>\n" +
                "<lp1:creationdate>2008-12-16T01:03:54.820035Z</lp1:creationdate>\n" +
                "<lp1:getlastmodified>Tue, 16 Dec 2008 01:03:54 GMT</lp1:getlastmodified>\n" +
                "<lp1:checked-in><D:href>/!svn/ver/5787/trunk/A%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60/H%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60.txt</D:href></lp1:checked-in>\n" +
                "<lp1:version-controlled-configuration><D:href>/!svn/vcc/default</D:href></lp1:version-controlled-configuration>\n" +
                "<lp1:version-name>5787</lp1:version-name>\n" +
                "<lp1:creator-displayname>jwanagel</lp1:creator-displayname>\n" +
                "<lp2:baseline-relative-path>trunk/A !@#$%^&amp;()_-+={[}];',.~`/H !@#$%^&amp;()_-+={[}];',.~`.txt</lp2:baseline-relative-path>\n" +
                "<lp2:md5-checksum>ef62106567f5237204ea8bf14ce1b757</lp2:md5-checksum>\n" +
                "<lp2:repository-uuid>81a5aebe-f34e-eb42-b435-ac1ecbb335f7</lp2:repository-uuid>\n" +
                "<lp2:deadprop-count>0</lp2:deadprop-count>\n" +
                "<lp1:resourcetype/>\n" +
                "<D:supportedlock>\n" +
                "<D:lockentry>\n" +
                "<D:lockscope><D:exclusive/></D:lockscope>\n" +
                "<D:locktype><D:write/></D:locktype>\n" +
                "</D:lockentry>\n" +
                "</D:supportedlock>\n" +
                "<D:lockdiscovery/>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "</D:multistatus>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test41()
        {
            stubs.Attach(provider.ItemExists, true);
            ItemMetaData item = new ItemMetaData();
            item.ItemRevision = 5788;
            item.LastModifiedDate = DateTime.Parse("Tue, 16 Dec 2008 01:04:17 GMT");
            item.Name = "trunk/A !@#$%^&()_-+={[}];',.~`/H !@#$%^&()_-+={[}];',.~`.txt";
            stubs.Attach(provider.GetItems, item);
            byte[] fileData = Encoding.UTF8.GetBytes("test4 my change");
            stubs.Attach((MyMocks.ReadFile)provider.ReadFile, fileData);
            stubs.Attach(provider.GetLatestVersion, 5788);

            string request =
                "GET /!svn/ver/5788/trunk/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/H%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60.txt HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.2 (r32768) neon/0.27.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 200 OK\r\n" +
                "Date: Tue, 16 Dec 2008 01:12:05 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Last-Modified: Tue, 16 Dec 2008 01:04:17 GMT\r\n" +
                "ETag: \"5788//trunk/A !@#$%^&amp;()_-+={[}];',.~`/H !@#$%^&amp;()_-+={[}];',.~`.txt\"\r\n" +
                "Accept-Ranges: bytes\r\n" +
                "Content-Length: 15\r\n" +
                "Content-Type: text/plain\r\n" +
                "\r\n" +
                "test4 my change";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }
    }
}