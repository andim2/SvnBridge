using System;
using SvnBridge.SourceControl;
using CodePlex.TfsLibrary;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using Xunit;
using Attach;
using Tests;

namespace ProtocolTests
{
    public class CommitNewFileTest : ProtocolTestsBase
    {
        [Fact]
        public void Test1()
        {
            stubs.Attach((MyMocks.ItemExists)provider.ItemExists, true);

            string request =
                "OPTIONS /trunk HTTP/1.1\r\n" +
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
                "Date: Mon, 26 Jan 2009 08:06:13 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "DAV: 1,2\r\n" +
                "DAV: version-control,checkout,working-resource\r\n" +
                "DAV: merge,baseline,activity,version-controlled-collection\r\n" +
                "MS-Author-Via: DAV\r\n" +
                "Allow: OPTIONS,GET,HEAD,POST,DELETE,TRACE,PROPFIND,PROPPATCH,COPY,MOVE,LOCK,UNLOCK,CHECKOUT\r\n" +
                "Content-Length: 0\r\n" +
                "Keep-Alive: timeout=15, max=100\r\n" +
                "Connection: Keep-Alive\r\n" +
                "Content-Type: text/plain\r\n" +
                "\r\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test2()
        {
            stubs.Attach(provider.ItemExists, true);
            stubs.Attach(provider.GetItems, CreateFolder("trunk"));

            string request =
                "PROPFIND /trunk HTTP/1.1\r\n" +
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
                "Date: Mon, 26 Jan 2009 08:06:13 GMT\r\n" +
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
        public void Test3()
        {
            stubs.Attach((MyMocks.ItemExists)provider.ItemExists, true);

            string request =
                "OPTIONS /trunk HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.3 (r33570)/TortoiseSVN-1.5.4.14259 neon/0.28.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 104\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:options xmlns:D=\"DAV:\"><D:activity-collection-set/></D:options>";

            string expected =
                "HTTP/1.1 200 OK\r\n" +
                "Date: Mon, 26 Jan 2009 08:06:13 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "DAV: 1,2\r\n" +
                "DAV: version-control,checkout,working-resource\r\n" +
                "DAV: merge,baseline,activity,version-controlled-collection\r\n" +
                "MS-Author-Via: DAV\r\n" +
                "Allow: OPTIONS,GET,HEAD,POST,DELETE,TRACE,PROPFIND,PROPPATCH,COPY,MOVE,LOCK,UNLOCK,CHECKOUT\r\n" +
                "Content-Length: 179\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:options-response xmlns:D=\"DAV:\">\n" +
                "<D:activity-collection-set><D:href>/!svn/act/</D:href></D:activity-collection-set></D:options-response>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test4()
        {
            stubs.Attach((MyMocks.MakeActivity)provider.MakeActivity, new NetworkAccessDeniedException());

            string request =
                "MKACTIVITY /!svn/act/7a6c73b2-7e7c-2141-817d-9bd653873445 HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.3 (r33570)/TortoiseSVN-1.5.4.14259 neon/0.28.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 0\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 401 Authorization Required\r\n" +
                "Date: Mon, 26 Jan 2009 08:06:13 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "WWW-Authenticate: Basic realm=\"CodePlex Subversion Repository\"\r\n" +
                "Content-Length: 493\r\n" +
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
        public void Test5()
        {
            stubs.Attach(provider.MakeActivity);

            string request =
                "MKACTIVITY /!svn/act/7a6c73b2-7e7c-2141-817d-9bd653873445 HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.3 (r33570)/TortoiseSVN-1.5.4.14259 neon/0.28.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 0\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 201 Created\r\n" +
                "Date: Mon, 26 Jan 2009 08:06:13 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Cache-Control: no-cache\r\n" +
                "Location: http://localhost:8080/!svn/act/7a6c73b2-7e7c-2141-817d-9bd653873445\r\n" +
                "Content-Length: 312\r\n" +
                "Content-Type: text/html\r\n" +
                "X-Pad: avoid browser bug\r\n" +
                "\r\n" +
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>201 Created</title>\n" +
                "</head><body>\n" +
                "<h1>Created</h1>\n" +
                "<p>Activity /!svn/act/7a6c73b2-7e7c-2141-817d-9bd653873445 has been created.</p>\n" +
                "<hr />\n" +
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8080</address>\n" +
                "</body></html>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test6()
        {
            stubs.Attach(provider.GetLatestVersion, 5793);

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
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><checked-in xmlns=\"DAV:\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Mon, 26 Jan 2009 08:06:13 GMT\r\n" +
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
                "<lp1:checked-in><D:href>/!svn/bln/5793</D:href></lp1:checked-in>\n" +
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
            string request =
                "CHECKOUT /!svn/bln/5793 HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.3 (r33570)/TortoiseSVN-1.5.4.14259 neon/0.28.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 174\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:checkout xmlns:D=\"DAV:\"><D:activity-set><D:href>/!svn/act/7a6c73b2-7e7c-2141-817d-9bd653873445</D:href></D:activity-set></D:checkout>";

            string expected =
                "HTTP/1.1 201 Created\r\n" +
                "Date: Mon, 26 Jan 2009 08:06:13 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Cache-Control: no-cache\r\n" +
                "Location: http://localhost:8080//!svn/wbl/7a6c73b2-7e7c-2141-817d-9bd653873445/5793\r\n" +
                "Content-Length: 330\r\n" +
                "Content-Type: text/html\r\n" +
                "\r\n" +
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>201 Created</title>\n" +
                "</head><body>\n" +
                "<h1>Created</h1>\n" +
                "<p>Checked-out resource //!svn/wbl/7a6c73b2-7e7c-2141-817d-9bd653873445/5793 has been created.</p>\n" +
                "<hr />\n" +
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8080</address>\n" +
                "</body></html>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test8()
        {
            stubs.Attach(provider.SetActivityComment);

            string request =
                "PROPPATCH //!svn/wbl/7a6c73b2-7e7c-2141-817d-9bd653873445/5793 HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.3 (r33570)/TortoiseSVN-1.5.4.14259 neon/0.28.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml; charset=UTF-8\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 334\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\" ?><D:propertyupdate xmlns:D=\"DAV:\" xmlns:V=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:C=\"http://subversion.tigris.org/xmlns/custom/\" xmlns:S=\"http://subversion.tigris.org/xmlns/svn/\"><D:set><D:prop><S:log >Add file A !@#$%^&amp;()_-+={[}];',.~`.txt</S:log></D:prop></D:set></D:propertyupdate>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Mon, 26 Jan 2009 08:06:13 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 455\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns3=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:ns2=\"http://subversion.tigris.org/xmlns/custom/\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/svn/\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response>\n" +
                "<D:href>//!svn/wbl/7a6c73b2-7e7c-2141-817d-9bd653873445/5793</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<ns1:log/>\r\n" +
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
            stubs.Attach(provider.ItemExists, true);
            stubs.Attach(provider.GetItems, CreateFolder("trunk", 5790));

            string request =
                "PROPFIND /trunk HTTP/1.1\r\n" +
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
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><checked-in xmlns=\"DAV:\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Mon, 26 Jan 2009 08:06:14 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 379\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/trunk/</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:checked-in><D:href>/!svn/ver/5790/trunk</D:href></lp1:checked-in>\n" +
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
            stubs.Attach(provider.GetItems, CreateFolder("trunk"));

            string request =
                "CHECKOUT /!svn/ver/5790/trunk HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.3 (r33570)/TortoiseSVN-1.5.4.14259 neon/0.28.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 174\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:checkout xmlns:D=\"DAV:\"><D:activity-set><D:href>/!svn/act/7a6c73b2-7e7c-2141-817d-9bd653873445</D:href></D:activity-set></D:checkout>";

            string expected =
                "HTTP/1.1 201 Created\r\n" +
                "Date: Mon, 26 Jan 2009 08:06:14 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Cache-Control: no-cache\r\n" +
                "Location: http://localhost:8080//!svn/wrk/7a6c73b2-7e7c-2141-817d-9bd653873445/trunk\r\n" +
                "Content-Length: 331\r\n" +
                "Content-Type: text/html\r\n" +
                "\r\n" +
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>201 Created</title>\n" +
                "</head><body>\n" +
                "<h1>Created</h1>\n" +
                "<p>Checked-out resource //!svn/wrk/7a6c73b2-7e7c-2141-817d-9bd653873445/trunk has been created.</p>\n" +
                "<hr />\n" +
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8080</address>\n" +
                "</body></html>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test11()
        {
            stubs.Attach((MyMocks.GetItemInActivity)provider.GetItemInActivity, null);

            string request =
                "PROPFIND //!svn/wrk/7a6c73b2-7e7c-2141-817d-9bd653873445/trunk/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60.txt HTTP/1.1\r\n" +
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
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><version-controlled-configuration xmlns=\"DAV:\"/><resourcetype xmlns=\"DAV:\"/><baseline-relative-path xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/><repository-uuid xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 404 Not Found\r\n" +
                "Date: Mon, 26 Jan 2009 08:06:14 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 375\r\n" +
                "Content-Type: text/html; charset=iso-8859-1\r\n" +
                "\r\n" +
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>404 Not Found</title>\n" +
                "</head><body>\n" +
                "<h1>Not Found</h1>\n" +
                "<p>The requested URL /!svn/wrk/7a6c73b2-7e7c-2141-817d-9bd653873445/trunk/A !@#$%^&amp;()_-+={[}];',.~`.txt was not found on this server.</p>\n" +
                "<hr>\n" +
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8080</address>\n" +
                "</body></html>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test12()
        {
            stubs.Attach(provider.GetItemInActivity, (ItemMetaData)null); // NO pre-existing resource! (--> write required).
            stubs.Attach(provider.WriteFile, true);

            string request =
                "PUT //!svn/wrk/7a6c73b2-7e7c-2141-817d-9bd653873445/trunk/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60.txt HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.3 (r33570)/TortoiseSVN-1.5.4.14259 neon/0.28.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Length: 14\r\n" +
                "X-SVN-Result-Fulltext-MD5: 098f6bcd4621d373cade4e832627b4f6\r\n" +
                "Content-Type: application/vnd.svn-svndiff\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "SVN\0\0\0\u0004\u0001\u0004\u0084test";

            string expected =
                "HTTP/1.1 201 Created\r\n" +
                "Date: Mon, 26 Jan 2009 08:06:14 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Location: http://localhost:8080//!svn/wrk/7a6c73b2-7e7c-2141-817d-9bd653873445/trunk/A !@#$%^&()_-+={[}];',.~`.txt\r\n" +
                "Content-Length: 353\r\n" +
                "Content-Type: text/html\r\n" +
                "\r\n" +
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>201 Created</title>\n" +
                "</head><body>\n" +
                "<h1>Created</h1>\n" +
                "<p>Resource //!svn/wrk/7a6c73b2-7e7c-2141-817d-9bd653873445/trunk/A !@#$%^&amp;()_-+={[}];',.~`.txt has been created.</p>\n" +
                "<hr />\n" +
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8080</address>\n" +
                "</body></html>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test13()
        {
            MergeActivityResponse mergeResponse = new MergeActivityResponse(5794, DateTime.Parse("2009-01-26T08:06:15.305320Z"), "jwanagel");
            mergeResponse.Items.Add(new MergeActivityResponseItem(ItemType.File, "/trunk/A !@#$%^&()_-+={[}];',.~`.txt"));
            mergeResponse.Items.Add(new MergeActivityResponseItem(ItemType.Folder, "/trunk"));
            stubs.Attach(provider.MergeActivity, mergeResponse);

            string request =
                "MERGE /trunk HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.3 (r33570)/TortoiseSVN-1.5.4.14259 neon/0.28.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "X-SVN-Options:  release-locks\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 297\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:merge xmlns:D=\"DAV:\"><D:source><D:href>/!svn/act/7a6c73b2-7e7c-2141-817d-9bd653873445</D:href></D:source><D:no-auto-merge/><D:no-checkout/><D:prop><D:checked-in/><D:version-name/><D:resourcetype/><D:creationdate/><D:creator-displayname/></D:prop></D:merge>";

            string expected =
                "HTTP/1.1 200 OK\r\n" +
                "Date: Mon, 26 Jan 2009 08:06:15 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Cache-Control: no-cache\r\n" +
                "Transfer-Encoding: chunked\r\n" +
                "Content-Type: text/xml\r\n" +
                "\r\n" +
                "418\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:merge-response xmlns:D=\"DAV:\">\n" +
                "<D:updated-set>\n" +
                "<D:response>\n" +
                "<D:href>/!svn/vcc/default</D:href>\n" +
                "<D:propstat><D:prop>\n" +
                "<D:resourcetype><D:baseline/></D:resourcetype>\n" +
                "\n" +
                "<D:version-name>5794</D:version-name>\n" +
                "<D:creationdate>2009-01-26T08:06:15.305320Z</D:creationdate>\n" +
                "<D:creator-displayname>jwanagel</D:creator-displayname>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "<D:response>\n" +
                "<D:href>/trunk/A%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60.txt</D:href>\n" +
                "<D:propstat><D:prop>\n" +
                "<D:resourcetype/>\n" +
                "<D:checked-in><D:href>/!svn/ver/5794/trunk/A%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60.txt</D:href></D:checked-in>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "<D:response>\n" +
                "<D:href>/trunk</D:href>\n" +
                "<D:propstat><D:prop>\n" +
                "<D:resourcetype><D:collection/></D:resourcetype>\n" +
                "<D:checked-in><D:href>/!svn/ver/5794/trunk</D:href></D:checked-in>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "</D:updated-set>\n" +
                "</D:merge-response>\n" +
                "\r\n" +
                "0\r\n" +
                "\r\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test14()
        {
            stubs.Attach(provider.DeleteActivity);

            string request =
                "DELETE /!svn/act/7a6c73b2-7e7c-2141-817d-9bd653873445 HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.3 (r33570)/TortoiseSVN-1.5.4.14259 neon/0.28.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 0\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 204 No Content\r\n" +
                "Date: Mon, 26 Jan 2009 08:06:15 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 0\r\n" +
                "Content-Type: text/plain\r\n" +
                "\r\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }
    }
}