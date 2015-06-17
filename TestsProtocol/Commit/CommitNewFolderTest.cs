using System;
using SvnBridge.SourceControl;
using CodePlex.TfsLibrary;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using Xunit;
using Attach;
using Tests;

namespace ProtocolTests
{
    public class CommitNewFolderTest : ProtocolTestsBase
    {
        [Fact]
        public void Test1()
        {
            stubs.Attach(provider.ItemExists, true);

            string request =
                "OPTIONS /trunk HTTP/1.1\r\n" +
                "User-Agent: SVN/1.6.9 (r901367)/TortoiseSVN-1.6.7.18415 neon/0.29.3\r\n" +
                "Keep-Alive: \r\n" +
                "Connection: TE, Keep-Alive\r\n" +
                "TE: trailers\r\n" +
                "Host: localhost:8080\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 104\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:options xmlns:D=\"DAV:\"><D:activity-collection-set/></D:options>";

            string expected =
                "HTTP/1.1 200 OK\r\n" +
                "Date: Mon, 25 Jan 2010 06:21:31 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "DAV: 1,2\r\n" +
                "DAV: version-control,checkout,working-resource\r\n" +
                "DAV: merge,baseline,activity,version-controlled-collection\r\n" +
                "MS-Author-Via: DAV\r\n" +
                "Allow: OPTIONS,GET,HEAD,POST,DELETE,TRACE,PROPFIND,PROPPATCH,COPY,MOVE,LOCK,UNLOCK,CHECKOUT\r\n" +
                "Content-Length: 179\r\n" +
                "Keep-Alive: timeout=15, max=100\r\n" +
                "Connection: Keep-Alive\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:options-response xmlns:D=\"DAV:\">\n" +
                "<D:activity-collection-set><D:href>/!svn/act/</D:href></D:activity-collection-set></D:options-response>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test2()
        {
            stubs.Attach(provider.ItemExists, true);
            FolderMetaData folder = new FolderMetaData();
            folder.Name = "trunk";
            stubs.Attach(provider.GetItems, folder);

            string request =
                "PROPFIND /trunk HTTP/1.1\r\n" +
                "User-Agent: SVN/1.6.9 (r901367)/TortoiseSVN-1.6.7.18415 neon/0.29.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Host: localhost:8080\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 300\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><version-controlled-configuration xmlns=\"DAV:\"/><resourcetype xmlns=\"DAV:\"/><baseline-relative-path xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/><repository-uuid xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Mon, 25 Jan 2010 06:21:31 GMT\r\n" +
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
            stubs.Attach((MyMocks.MakeActivity)provider.MakeActivity, new NetworkAccessDeniedException());

            string request =
                "MKACTIVITY /!svn/act/3f197ee7-52ec-df47-ac36-d134654609fc HTTP/1.1\r\n" +
                "User-Agent: SVN/1.6.9 (r901367)/TortoiseSVN-1.6.7.18415 neon/0.29.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Host: localhost:8080\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 0\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 401 Authorization Required\r\n" +
                "Date: Mon, 25 Jan 2010 06:21:31 GMT\r\n" +
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
        public void Test4()
        {
            stubs.Attach(provider.MakeActivity);

            string request =
                "MKACTIVITY /!svn/act/3f197ee7-52ec-df47-ac36-d134654609fc HTTP/1.1\r\n" +
                "User-Agent: SVN/1.6.9 (r901367)/TortoiseSVN-1.6.7.18415 neon/0.29.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Host: localhost:8080\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 0\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 201 Created\r\n" +
                "Date: Mon, 25 Jan 2010 06:21:31 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Cache-Control: no-cache\r\n" +
                "Location: http://localhost:8080/!svn/act/3f197ee7-52ec-df47-ac36-d134654609fc\r\n" +
                "Content-Length: 312\r\n" +
                "Content-Type: text/html\r\n" +
                "X-Pad: avoid browser bug\r\n" +
                "\r\n" +
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>201 Created</title>\n" +
                "</head><body>\n" +
                "<h1>Created</h1>\n" +
                "<p>Activity /!svn/act/3f197ee7-52ec-df47-ac36-d134654609fc has been created.</p>\n" +
                "<hr />\n" +
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8080</address>\n" +
                "</body></html>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test5()
        {
            stubs.Attach(provider.GetLatestVersion, 5819);

            string request =
                "CHECKOUT /!svn/vcc/default HTTP/1.1\r\n" +
                "User-Agent: SVN/1.6.9 (r901367)/TortoiseSVN-1.6.7.18415 neon/0.29.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Host: localhost:8080\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 195\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:checkout xmlns:D=\"DAV:\"><D:activity-set><D:href>/!svn/act/3f197ee7-52ec-df47-ac36-d134654609fc</D:href></D:activity-set><D:apply-to-version/></D:checkout>";

            string expected =
                "HTTP/1.1 201 Created\r\n" +
                "Date: Mon, 25 Jan 2010 06:21:31 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Cache-Control: no-cache\r\n" +
                "Location: http://localhost:8080//!svn/wbl/3f197ee7-52ec-df47-ac36-d134654609fc/5819\r\n" +
                "Content-Length: 330\r\n" +
                "Content-Type: text/html\r\n" +
                "\r\n" +
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>201 Created</title>\n" +
                "</head><body>\n" +
                "<h1>Created</h1>\n" +
                "<p>Checked-out resource //!svn/wbl/3f197ee7-52ec-df47-ac36-d134654609fc/5819 has been created.</p>\n" +
                "<hr />\n" +
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8080</address>\n" +
                "</body></html>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test6()
        {
            stubs.Attach(provider.SetActivityComment);

            string request =
                "PROPPATCH //!svn/wbl/3f197ee7-52ec-df47-ac36-d134654609fc/5819 HTTP/1.1\r\n" +
                "User-Agent: SVN/1.6.9 (r901367)/TortoiseSVN-1.6.7.18415 neon/0.29.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Host: localhost:8080\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Content-Type: text/xml; charset=UTF-8\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 344\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\" ?><D:propertyupdate xmlns:D=\"DAV:\" xmlns:V=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:C=\"http://subversion.tigris.org/xmlns/custom/\" xmlns:S=\"http://subversion.tigris.org/xmlns/svn/\"><D:set><D:prop><S:log >Commit of new folder \"B !@#$%^&amp;()_-+={[}];',.~`\"</S:log></D:prop></D:set></D:propertyupdate>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Mon, 25 Jan 2010 06:21:31 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Cache-Control: no-cache\r\n" +
                "Content-Length: 455\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns3=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:ns2=\"http://subversion.tigris.org/xmlns/custom/\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/svn/\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response>\n" +
                "<D:href>//!svn/wbl/3f197ee7-52ec-df47-ac36-d134654609fc/5819</D:href>\n" +
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
        public void Test7()
        {
            stubs.Attach(provider.ItemExists, true);
            stubs.Attach(provider.GetLatestVersion, 5819);
            FolderMetaData item = new FolderMetaData();
            item.Name = "trunk";
            item.ItemRevision = 5817;
            stubs.Attach(provider.GetItems, item);

            string request =
                "PROPFIND /trunk HTTP/1.1\r\n" +
                "User-Agent: SVN/1.6.9 (r901367)/TortoiseSVN-1.6.7.18415 neon/0.29.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Host: localhost:8080\r\n" +
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
                "Date: Mon, 25 Jan 2010 06:21:32 GMT\r\n" +
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
                "<lp1:checked-in><D:href>/!svn/ver/5817/trunk</D:href></lp1:checked-in>\n" +
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
            ItemMetaData item = new ItemMetaData();
            stubs.Attach(provider.GetItems, item);

            string request =
                "CHECKOUT /!svn/ver/5817/trunk HTTP/1.1\r\n" +
                "User-Agent: SVN/1.6.9 (r901367)/TortoiseSVN-1.6.7.18415 neon/0.29.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Host: localhost:8080\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 174\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:checkout xmlns:D=\"DAV:\"><D:activity-set><D:href>/!svn/act/3f197ee7-52ec-df47-ac36-d134654609fc</D:href></D:activity-set></D:checkout>";

            string expected =
                "HTTP/1.1 201 Created\r\n" +
                "Date: Mon, 25 Jan 2010 06:21:32 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Cache-Control: no-cache\r\n" +
                "Location: http://localhost:8080//!svn/wrk/3f197ee7-52ec-df47-ac36-d134654609fc/trunk\r\n" +
                "Content-Length: 331\r\n" +
                "Content-Type: text/html\r\n" +
                "\r\n" +
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>201 Created</title>\n" +
                "</head><body>\n" +
                "<h1>Created</h1>\n" +
                "<p>Checked-out resource //!svn/wrk/3f197ee7-52ec-df47-ac36-d134654609fc/trunk has been created.</p>\n" +
                "<hr />\n" +
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8080</address>\n" +
                "</body></html>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test9()
        {
            stubs.Attach(provider.MakeCollection);

            string request =
                "MKCOL //!svn/wrk/3f197ee7-52ec-df47-ac36-d134654609fc/trunk/B%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60 HTTP/1.1\r\n" +
                "User-Agent: SVN/1.6.9 (r901367)/TortoiseSVN-1.6.7.18415 neon/0.29.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Host: localhost:8080\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 0\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 201 Created\r\n" +
                "Date: Mon, 25 Jan 2010 06:21:32 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Location: http://localhost:8080//!svn/wrk/3f197ee7-52ec-df47-ac36-d134654609fc/trunk/B !@#$%^&()_-+={[}];',.~`\r\n" +
                "Content-Length: 351\r\n" +
                "Content-Type: text/html\r\n" +
                "\r\n" +
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>201 Created</title>\n" +
                "</head><body>\n" +
                "<h1>Created</h1>\n" +
                "<p>Collection //!svn/wrk/3f197ee7-52ec-df47-ac36-d134654609fc/trunk/B !@#$%^&amp;()_-+={[}];',.~` has been created.</p>\n" +
                "<hr />\n" +
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8080</address>\n" +
                "</body></html>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test10()
        {
            stubs.Attach(provider.SetProperty);

            string request =
                "PROPPATCH //!svn/wrk/3f197ee7-52ec-df47-ac36-d134654609fc/trunk/B%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60 HTTP/1.1\r\n" +
                "User-Agent: SVN/1.6.9 (r901367)/TortoiseSVN-1.6.7.18415 neon/0.29.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Host: localhost:8080\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Content-Type: text/xml; charset=UTF-8\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 334\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\" ?><D:propertyupdate xmlns:D=\"DAV:\" xmlns:V=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:C=\"http://subversion.tigris.org/xmlns/custom/\" xmlns:S=\"http://subversion.tigris.org/xmlns/svn/\"><D:set><D:prop><C:bugtraq:message >Work Item: %BUGID%</C:bugtraq:message></D:prop></D:set></D:propertyupdate>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Mon, 25 Jan 2010 06:21:32 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Cache-Control: no-cache\r\n" +
                "Content-Length: 518\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns3=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:ns2=\"http://subversion.tigris.org/xmlns/custom/\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/svn/\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response>\n" +
                "<D:href>//!svn/wrk/3f197ee7-52ec-df47-ac36-d134654609fc/trunk/B%20!@%23$%25%5e&amp;()_-+=%7b%5b%7d%5d%3b',.~%60</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<ns2:bugtraq:message/>\r\n" +
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
            MergeActivityResponse mergeResponse =
                new MergeActivityResponse(5820, DateTime.Parse("2010-01-25T06:21:33.246002Z"), "jwanagel");
            mergeResponse.Items.Add(
                new MergeActivityResponseItem(ItemType.Folder, "/trunk/B !@#$%^&()_-+={[}];',.~`"));
            mergeResponse.Items.Add(new MergeActivityResponseItem(ItemType.Folder, "/trunk"));
            stubs.Attach(provider.MergeActivity, mergeResponse);

            string request =
                "MERGE /trunk HTTP/1.1\r\n" +
                "User-Agent: SVN/1.6.9 (r901367)/TortoiseSVN-1.6.7.18415 neon/0.29.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Host: localhost:8080\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "X-SVN-Options:  release-locks\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 297\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:merge xmlns:D=\"DAV:\"><D:source><D:href>/!svn/act/3f197ee7-52ec-df47-ac36-d134654609fc</D:href></D:source><D:no-auto-merge/><D:no-checkout/><D:prop><D:checked-in/><D:version-name/><D:resourcetype/><D:creationdate/><D:creator-displayname/></D:prop></D:merge>";

            string expected =
                "HTTP/1.1 200 OK\r\n" +
                "Date: Mon, 25 Jan 2010 06:21:32 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Cache-Control: no-cache\r\n" +
                "Transfer-Encoding: chunked\r\n" +
                "Content-Type: text/xml\r\n" +
                "\r\n" +
                "42f\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:merge-response xmlns:D=\"DAV:\">\n" +
                "<D:updated-set>\n" +
                "<D:response>\n" +
                "<D:href>/!svn/vcc/default</D:href>\n" +
                "<D:propstat><D:prop>\n" +
                "<D:resourcetype><D:baseline/></D:resourcetype>\n" +
                "\n" +
                "<D:version-name>5820</D:version-name>\n" +
                "<D:creationdate>2010-01-25T06:21:33.246002Z</D:creationdate>\n" +
                "<D:creator-displayname>jwanagel</D:creator-displayname>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "<D:response>\n" +
                "<D:href>/trunk/B%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60</D:href>\n" +
                "<D:propstat><D:prop>\n" +
                "<D:resourcetype><D:collection/></D:resourcetype>\n" +
                "<D:checked-in><D:href>/!svn/ver/5820/trunk/B%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60</D:href></D:checked-in>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "<D:response>\n" +
                "<D:href>/trunk</D:href>\n" +
                "<D:propstat><D:prop>\n" +
                "<D:resourcetype><D:collection/></D:resourcetype>\n" +
                "<D:checked-in><D:href>/!svn/ver/5820/trunk</D:href></D:checked-in>\n" +
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
        public void Test12()
        {
            stubs.Attach(provider.DeleteActivity);

            string request =
                "DELETE /!svn/act/3f197ee7-52ec-df47-ac36-d134654609fc HTTP/1.1\r\n" +
                "User-Agent: SVN/1.6.9 (r901367)/TortoiseSVN-1.6.7.18415 neon/0.29.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Host: localhost:8080\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 0\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 204 No Content\r\n" +
                "Date: Mon, 25 Jan 2010 06:21:33 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 0\r\n" +
                "Content-Type: text/plain\r\n" +
                "\r\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }
    }
}