using System;
using SvnBridge.SourceControl;
using CodePlex.TfsLibrary;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using Xunit;
using Attach;
using Tests;

namespace ProtocolTests
{
    public class CommitRenamedFileWithSecondFileRenamedToOriginalNameOfFirstFileTest : ProtocolTestsBase
    {
        [Fact]
        public void Test1()
        {
            stubs.Attach(provider.ItemExists, true);

            string request =
                "OPTIONS /trunk/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60 HTTP/1.1\r\n" +
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
                "Date: Sun, 08 Feb 2009 10:48:22 GMT\r\n" +
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
            stubs.Attach(provider.GetItems, CreateFolder("trunk/A !@#$%^&()_-+={[}];',.~`"));

            string request =
                "PROPFIND /trunk/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60 HTTP/1.1\r\n" +
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
                "Date: Sun, 08 Feb 2009 10:48:22 GMT\r\n" +
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
        public void Test3()
        {
            stubs.Attach(provider.ItemExists, true);

            string request =
                "OPTIONS /trunk/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60 HTTP/1.1\r\n" +
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
                "Date: Sun, 08 Feb 2009 10:48:22 GMT\r\n" +
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
            stubs.Attach((MyMocks.MakeActivity)provider.MakeActivity, Return.Exception(new NetworkAccessDeniedException()));

            string request =
                "MKACTIVITY /!svn/act/9f35ef46-29f0-8648-881a-7c7f26b1319c HTTP/1.1\r\n" +
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
                "Date: Sun, 08 Feb 2009 10:48:23 GMT\r\n" +
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
            stubs.Attach((MyMocks.MakeActivity)provider.MakeActivity, Return.Nothing);

            string request =
                "MKACTIVITY /!svn/act/9f35ef46-29f0-8648-881a-7c7f26b1319c HTTP/1.1\r\n" +
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
                "Date: Sun, 08 Feb 2009 10:48:23 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Cache-Control: no-cache\r\n" +
                "Location: http://localhost:8080/!svn/act/9f35ef46-29f0-8648-881a-7c7f26b1319c\r\n" +
                "Content-Length: 312\r\n" +
                "Content-Type: text/html\r\n" +
                "X-Pad: avoid browser bug\r\n" +
                "\r\n" +
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>201 Created</title>\n" +
                "</head><body>\n" +
                "<h1>Created</h1>\n" +
                "<p>Activity /!svn/act/9f35ef46-29f0-8648-881a-7c7f26b1319c has been created.</p>\n" +
                "<hr />\n" +
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8080</address>\n" +
                "</body></html>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test6()
        {
            stubs.Attach(provider.GetLatestVersion, 5798);

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
                "Date: Sun, 08 Feb 2009 10:48:23 GMT\r\n" +
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
                "<lp1:checked-in><D:href>/!svn/bln/5798</D:href></lp1:checked-in>\n" +
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
                "CHECKOUT /!svn/bln/5798 HTTP/1.1\r\n" +
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
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:checkout xmlns:D=\"DAV:\"><D:activity-set><D:href>/!svn/act/9f35ef46-29f0-8648-881a-7c7f26b1319c</D:href></D:activity-set></D:checkout>";

            string expected =
                "HTTP/1.1 201 Created\r\n" +
                "Date: Sun, 08 Feb 2009 10:48:23 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Cache-Control: no-cache\r\n" +
                "Location: http://localhost:8080//!svn/wbl/9f35ef46-29f0-8648-881a-7c7f26b1319c/5798\r\n" +
                "Content-Length: 330\r\n" +
                "Content-Type: text/html\r\n" +
                "\r\n" +
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>201 Created</title>\n" +
                "</head><body>\n" +
                "<h1>Created</h1>\n" +
                "<p>Checked-out resource //!svn/wbl/9f35ef46-29f0-8648-881a-7c7f26b1319c/5798 has been created.</p>\n" +
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
                "PROPPATCH //!svn/wbl/9f35ef46-29f0-8648-881a-7c7f26b1319c/5798 HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.3 (r33570)/TortoiseSVN-1.5.4.14259 neon/0.28.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml; charset=UTF-8\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 296\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\" ?><D:propertyupdate xmlns:D=\"DAV:\" xmlns:V=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:C=\"http://subversion.tigris.org/xmlns/custom/\" xmlns:S=\"http://subversion.tigris.org/xmlns/svn/\"><D:set><D:prop><S:log >Test</S:log></D:prop></D:set></D:propertyupdate>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Sun, 08 Feb 2009 10:48:23 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Cache-Control: no-cache\r\n" +
                "Content-Length: 455\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns3=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:ns2=\"http://subversion.tigris.org/xmlns/custom/\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/svn/\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response>\n" +
                "<D:href>//!svn/wbl/9f35ef46-29f0-8648-881a-7c7f26b1319c/5798</D:href>\n" +
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
            stubs.Attach(provider.GetItems, CreateFolder("trunk/A !@#$%^&()_-+={[}];',.~`", 5798));

            string request =
                "PROPFIND /trunk/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60 HTTP/1.1\r\n" +
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
                "Date: Sun, 08 Feb 2009 10:48:23 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 479\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/trunk/A%20!@%23$%25%5e&amp;()_-+=%7b%5b%7d%5d%3b',.~%60/</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:checked-in><D:href>/!svn/ver/5798/trunk/A%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60</D:href></lp1:checked-in>\n" +
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
            stubs.Attach(provider.GetItems, CreateFolder("trunk/A !@#$%^&()_-+={[}];',.~`"));

            string request =
                "CHECKOUT /!svn/ver/5798/trunk/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60 HTTP/1.1\r\n" +
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
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:checkout xmlns:D=\"DAV:\"><D:activity-set><D:href>/!svn/act/9f35ef46-29f0-8648-881a-7c7f26b1319c</D:href></D:activity-set></D:checkout>";

            string expected =
                "HTTP/1.1 201 Created\r\n" +
                "Date: Sun, 08 Feb 2009 10:48:23 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Cache-Control: no-cache\r\n" +
                "Location: http://localhost:8080//!svn/wrk/9f35ef46-29f0-8648-881a-7c7f26b1319c/trunk/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60\r\n" +
                "Content-Length: 381\r\n" +
                "Content-Type: text/html\r\n" +
                "\r\n" +
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>201 Created</title>\n" +
                "</head><body>\n" +
                "<h1>Created</h1>\n" +
                "<p>Checked-out resource //!svn/wrk/9f35ef46-29f0-8648-881a-7c7f26b1319c/trunk/A%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60 has been created.</p>\n" +
                "<hr />\n" +
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8080</address>\n" +
                "</body></html>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test11()
        {
            stubs.Attach((MyMocks.DeleteItem)provider.DeleteItem, Return.Value(true));

            string request =
                "DELETE //!svn/wrk/9f35ef46-29f0-8648-881a-7c7f26b1319c/trunk/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/H%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60%201.txt HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.3 (r33570)/TortoiseSVN-1.5.4.14259 neon/0.28.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "X-SVN-Version-Name: 5798\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 0\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 204 No Content\r\n" +
                "Date: Sun, 08 Feb 2009 10:48:24 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 0\r\n" +
                "Content-Type: text/plain\r\n" +
                "\r\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test12()
        {
            stubs.Attach((MyMocks.DeleteItem)provider.DeleteItem, Return.Value(true));

            string request =
                "DELETE //!svn/wrk/9f35ef46-29f0-8648-881a-7c7f26b1319c/trunk/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/H%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60%202.txt HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.3 (r33570)/TortoiseSVN-1.5.4.14259 neon/0.28.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "X-SVN-Version-Name: 5798\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 0\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 204 No Content\r\n" +
                "Date: Sun, 08 Feb 2009 10:48:24 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 0\r\n" +
                "Content-Type: text/plain\r\n" +
                "\r\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test13()
        {
            stubs.Attach(provider.ItemExists, true);
            stubs.Attach(provider.GetItems, CreateItem("trunk/A !@#$%^&()_-+={[}];',.~`/H !@#$%^&()_-+={[}];',.~` 1.txt"));

            string request =
                "PROPFIND /trunk/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/H%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60%201.txt HTTP/1.1\r\n" +
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
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Sun, 08 Feb 2009 10:48:24 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 808\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/trunk/A%20!@%23$%25%5e&amp;()_-+=%7b%5b%7d%5d%3b',.~%60/H%20!@%23$%25%5e&amp;()_-+=%7b%5b%7d%5d%3b',.~%60%201.txt</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:version-controlled-configuration><D:href>/!svn/vcc/default</D:href></lp1:version-controlled-configuration>\n" +
                "<lp1:resourcetype/>\n" +
                "<lp2:baseline-relative-path>trunk/A !@#$%^&amp;()_-+={[}];',.~`/H !@#$%^&amp;()_-+={[}];',.~` 1.txt</lp2:baseline-relative-path>\n" +
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
        public void Test14()
        {
            string request =
                "PROPFIND /!svn/vcc/default HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.3 (r33570)/TortoiseSVN-1.5.4.14259 neon/0.28.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Label: 5798\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 148\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><baseline-collection xmlns=\"DAV:\"/><version-name xmlns=\"DAV:\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Sun, 08 Feb 2009 10:48:24 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Vary: Label\r\n" +
                "Content-Length: 440\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/!svn/bln/5798</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:baseline-collection><D:href>/!svn/bc/5798/</D:href></lp1:baseline-collection>\n" +
                "<lp1:version-name>5798</lp1:version-name>\n" +
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
            stubs.Attach((MyMocks.CopyItem)provider.CopyItem, Return.Value(true));

            string request =
                "COPY /!svn/bc/5798/trunk/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/H%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60%201.txt HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.3 (r33570)/TortoiseSVN-1.5.4.14259 neon/0.28.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Destination: http://localhost:8080//!svn/wrk/9f35ef46-29f0-8648-881a-7c7f26b1319c/trunk/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/H%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60%202.txt\r\n" +
                "Depth: 0\r\n" +
                "Overwrite: T\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 0\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 201 Created\r\n" +
                "Date: Sun, 08 Feb 2009 10:48:24 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Location: http://localhost:8080//!svn/wrk/9f35ef46-29f0-8648-881a-7c7f26b1319c/trunk/A !@#$%^&()_-+={[}];',.~`/H !@#$%^&()_-+={[}];',.~` 2.txt\r\n" +
                "Content-Length: 388\r\n" +
                "Content-Type: text/html\r\n" +
                "\r\n" +
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>201 Created</title>\n" +
                "</head><body>\n" +
                "<h1>Created</h1>\n" +
                "<p>Destination //!svn/wrk/9f35ef46-29f0-8648-881a-7c7f26b1319c/trunk/A !@#$%^&amp;()_-+={[}];',.~`/H !@#$%^&amp;()_-+={[}];',.~` 2.txt has been created.</p>\n" +
                "<hr />\n" +
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8080</address>\n" +
                "</body></html>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test16()
        {
            stubs.Attach((MyMocks.SetProperty)provider.SetProperty, Return.Nothing);

            string request =
                "PROPPATCH //!svn/wrk/9f35ef46-29f0-8648-881a-7c7f26b1319c/trunk/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/H%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60%202.txt HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.3 (r33570)/TortoiseSVN-1.5.4.14259 neon/0.28.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml; charset=UTF-8\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 304\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\" ?><D:propertyupdate xmlns:D=\"DAV:\" xmlns:V=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:C=\"http://subversion.tigris.org/xmlns/custom/\" xmlns:S=\"http://subversion.tigris.org/xmlns/svn/\"><D:set><D:prop><S:mergeinfo ></S:mergeinfo></D:prop></D:set></D:propertyupdate>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Sun, 08 Feb 2009 10:48:24 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Cache-Control: no-cache\r\n" +
                "Content-Length: 570\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns3=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:ns2=\"http://subversion.tigris.org/xmlns/custom/\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/svn/\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response>\n" +
                "<D:href>//!svn/wrk/9f35ef46-29f0-8648-881a-7c7f26b1319c/trunk/A%20!@%23$%25%5e&amp;()_-+=%7b%5b%7d%5d%3b',.~%60/H%20!@%23$%25%5e&amp;()_-+=%7b%5b%7d%5d%3b',.~%60%202.txt</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<ns1:mergeinfo/>\r\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "</D:multistatus>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test17()
        {
            stubs.Attach((MyMocks.GetItemInActivity)provider.GetItemInActivity, Return.Value(null));

            string request =
                "PROPFIND //!svn/wrk/9f35ef46-29f0-8648-881a-7c7f26b1319c/trunk/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/H%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60%203.txt HTTP/1.1\r\n" +
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
                "Date: Sun, 08 Feb 2009 10:48:24 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 407\r\n" +
                "Content-Type: text/html; charset=iso-8859-1\r\n" +
                "\r\n" +
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>404 Not Found</title>\n" +
                "</head><body>\n" +
                "<h1>Not Found</h1>\n" +
                "<p>The requested URL /!svn/wrk/9f35ef46-29f0-8648-881a-7c7f26b1319c/trunk/A !@#$%^&amp;()_-+={[}];',.~`/H !@#$%^&amp;()_-+={[}];',.~` 3.txt was not found on this server.</p>\n" +
                "<hr>\n" +
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8080</address>\n" +
                "</body></html>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test18()
        {
            stubs.Attach(provider.ItemExists, true);
            stubs.Attach(provider.GetItems, CreateItem("trunk/A !@#$%^&()_-+={[}];',.~`/H !@#$%^&()_-+={[}];',.~` 2.txt"));

            string request =
                "PROPFIND /trunk/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/H%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60%202.txt HTTP/1.1\r\n" +
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
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Sun, 08 Feb 2009 10:48:25 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 808\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/trunk/A%20!@%23$%25%5e&amp;()_-+=%7b%5b%7d%5d%3b',.~%60/H%20!@%23$%25%5e&amp;()_-+=%7b%5b%7d%5d%3b',.~%60%202.txt</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:version-controlled-configuration><D:href>/!svn/vcc/default</D:href></lp1:version-controlled-configuration>\n" +
                "<lp1:resourcetype/>\n" +
                "<lp2:baseline-relative-path>trunk/A !@#$%^&amp;()_-+={[}];',.~`/H !@#$%^&amp;()_-+={[}];',.~` 2.txt</lp2:baseline-relative-path>\n" +
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
            string request =
                "PROPFIND /!svn/vcc/default HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.3 (r33570)/TortoiseSVN-1.5.4.14259 neon/0.28.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Label: 5798\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 148\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><baseline-collection xmlns=\"DAV:\"/><version-name xmlns=\"DAV:\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Sun, 08 Feb 2009 10:48:25 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Vary: Label\r\n" +
                "Content-Length: 440\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/!svn/bln/5798</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:baseline-collection><D:href>/!svn/bc/5798/</D:href></lp1:baseline-collection>\n" +
                "<lp1:version-name>5798</lp1:version-name>\n" +
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
            stubs.Attach((MyMocks.CopyItem)provider.CopyItem, Return.Value(true));

            string request =
                "COPY /!svn/bc/5798/trunk/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/H%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60%202.txt HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.3 (r33570)/TortoiseSVN-1.5.4.14259 neon/0.28.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Destination: http://localhost:8080//!svn/wrk/9f35ef46-29f0-8648-881a-7c7f26b1319c/trunk/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/H%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60%203.txt\r\n" +
                "Depth: 0\r\n" +
                "Overwrite: T\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 0\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 201 Created\r\n" +
                "Date: Sun, 08 Feb 2009 10:48:25 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Location: http://localhost:8080//!svn/wrk/9f35ef46-29f0-8648-881a-7c7f26b1319c/trunk/A !@#$%^&()_-+={[}];',.~`/H !@#$%^&()_-+={[}];',.~` 3.txt\r\n" +
                "Content-Length: 388\r\n" +
                "Content-Type: text/html\r\n" +
                "\r\n" +
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>201 Created</title>\n" +
                "</head><body>\n" +
                "<h1>Created</h1>\n" +
                "<p>Destination //!svn/wrk/9f35ef46-29f0-8648-881a-7c7f26b1319c/trunk/A !@#$%^&amp;()_-+={[}];',.~`/H !@#$%^&amp;()_-+={[}];',.~` 3.txt has been created.</p>\n" +
                "<hr />\n" +
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8080</address>\n" +
                "</body></html>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test21()
        {
            stubs.Attach((MyMocks.SetProperty)provider.SetProperty, Return.Nothing);

            string request =
                "PROPPATCH //!svn/wrk/9f35ef46-29f0-8648-881a-7c7f26b1319c/trunk/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/H%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60%203.txt HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.3 (r33570)/TortoiseSVN-1.5.4.14259 neon/0.28.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml; charset=UTF-8\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 304\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\" ?><D:propertyupdate xmlns:D=\"DAV:\" xmlns:V=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:C=\"http://subversion.tigris.org/xmlns/custom/\" xmlns:S=\"http://subversion.tigris.org/xmlns/svn/\"><D:set><D:prop><S:mergeinfo ></S:mergeinfo></D:prop></D:set></D:propertyupdate>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Sun, 08 Feb 2009 10:48:25 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Cache-Control: no-cache\r\n" +
                "Content-Length: 570\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns3=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:ns2=\"http://subversion.tigris.org/xmlns/custom/\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/svn/\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response>\n" +
                "<D:href>//!svn/wrk/9f35ef46-29f0-8648-881a-7c7f26b1319c/trunk/A%20!@%23$%25%5e&amp;()_-+=%7b%5b%7d%5d%3b',.~%60/H%20!@%23$%25%5e&amp;()_-+=%7b%5b%7d%5d%3b',.~%60%203.txt</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<ns1:mergeinfo/>\r\n" +
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
            MergeActivityResponse mergeResponse = new MergeActivityResponse(5799, DateTime.Parse("2009-02-08T10:48:26.285033Z"), "jwanagel");
            mergeResponse.Items.Add(new MergeActivityResponseItem(ItemType.Folder, "/trunk/A !@#$%^&()_-+={[}];',.~`"));
            mergeResponse.Items.Add(new MergeActivityResponseItem(ItemType.File, "/trunk/A !@#$%^&()_-+={[}];',.~`/H !@#$%^&()_-+={[}];',.~` 2.txt"));
            mergeResponse.Items.Add(new MergeActivityResponseItem(ItemType.File, "/trunk/A !@#$%^&()_-+={[}];',.~`/H !@#$%^&()_-+={[}];',.~` 3.txt"));
            stubs.Attach(provider.MergeActivity, mergeResponse);

            string request =
                "MERGE /trunk/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60 HTTP/1.1\r\n" +
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
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:merge xmlns:D=\"DAV:\"><D:source><D:href>/!svn/act/9f35ef46-29f0-8648-881a-7c7f26b1319c</D:href></D:source><D:no-auto-merge/><D:no-checkout/><D:prop><D:checked-in/><D:version-name/><D:resourcetype/><D:creationdate/><D:creator-displayname/></D:prop></D:merge>";

            string expected =
                "HTTP/1.1 200 OK\r\n" +
                "Date: Sun, 08 Feb 2009 10:48:25 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Cache-Control: no-cache\r\n" +
                "Transfer-Encoding: chunked\r\n" +
                "Content-Type: text/xml\r\n" +
                "\r\n" +
                "69a\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:merge-response xmlns:D=\"DAV:\">\n" +
                "<D:updated-set>\n" +
                "<D:response>\n" +
                "<D:href>/!svn/vcc/default</D:href>\n" +
                "<D:propstat><D:prop>\n" +
                "<D:resourcetype><D:baseline/></D:resourcetype>\n" +
                "\n" +
                "<D:version-name>5799</D:version-name>\n" +
                "<D:creationdate>2009-02-08T10:48:26.285033Z</D:creationdate>\n" +
                "<D:creator-displayname>jwanagel</D:creator-displayname>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "<D:response>\n" +
                "<D:href>/trunk/A%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60</D:href>\n" +
                "<D:propstat><D:prop>\n" +
                "<D:resourcetype><D:collection/></D:resourcetype>\n" +
                "<D:checked-in><D:href>/!svn/ver/5799/trunk/A%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60</D:href></D:checked-in>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "<D:response>\n" +
                "<D:href>/trunk/A%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60/H%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60%202.txt</D:href>\n" +
                "<D:propstat><D:prop>\n" +
                "<D:resourcetype/>\n" +
                "<D:checked-in><D:href>/!svn/ver/5799/trunk/A%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60/H%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60%202.txt</D:href></D:checked-in>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "<D:response>\n" +
                "<D:href>/trunk/A%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60/H%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60%203.txt</D:href>\n" +
                "<D:propstat><D:prop>\n" +
                "<D:resourcetype/>\n" +
                "<D:checked-in><D:href>/!svn/ver/5799/trunk/A%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60/H%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60%203.txt</D:href></D:checked-in>\n" +
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
        public void Test23()
        {
            stubs.Attach((MyMocks.DeleteActivity)provider.DeleteActivity, Return.Nothing);

            string request =
                "DELETE /!svn/act/9f35ef46-29f0-8648-881a-7c7f26b1319c HTTP/1.1\r\n" +
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
                "Date: Sun, 08 Feb 2009 10:48:26 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 0\r\n" +
                "Content-Type: text/plain\r\n" +
                "\r\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }
    }
}