using System;
using CodePlex.TfsLibrary;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using Xunit;
using SvnBridge.SourceControl;
using Tests;
using Attach;

namespace ProtocolTests
{
    public class CommitReplacedFileTest : ProtocolTestsBase
    {
        [Fact]
        public void Test1()
        {
            stubs.Attach((MyMocks.ItemExists) provider.ItemExists, new NetworkAccessDeniedException());

            string request =
                "OPTIONS /A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/B%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60 HTTP/1.1\r\n" +
                "Host: localhost:8084\r\n" +
                "User-Agent: SVN/1.4.4 (r25188) neon/0.26.3\r\n" +
                "Keep-Alive: \r\n" +
                "Connection: TE, Keep-Alive\r\n" +
                "TE: trailers\r\n" +
                "Content-Length: 104\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:options xmlns:D=\"DAV:\"><D:activity-collection-set/></D:options>";

            string expected =
                "HTTP/1.1 401 Authorization Required\r\n" +
                "Date: Wed, 02 Jan 2008 23:57:47 GMT\r\n" +
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
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8084</address>\n" +
                "</body></html>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test2()
        {
            stubs.Attach(provider.ItemExists, true);

            string request =
                "OPTIONS /A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/B%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60 HTTP/1.1\r\n" +
                "Host: localhost:8084\r\n" +
                "User-Agent: SVN/1.4.4 (r25188) neon/0.26.3\r\n" +
                "Keep-Alive: \r\n" +
                "Connection: TE, Keep-Alive\r\n" +
                "TE: trailers\r\n" +
                "Content-Length: 104\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:options xmlns:D=\"DAV:\"><D:activity-collection-set/></D:options>";

            string expected =
                "HTTP/1.1 200 OK\r\n" +
                "Date: Wed, 02 Jan 2008 23:57:52 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "DAV: 1,2\r\n" +
                "DAV: version-control,checkout,working-resource\r\n" +
                "DAV: merge,baseline,activity,version-controlled-collection\r\n" +
                "MS-Author-Via: DAV\r\n" +
                "Allow: OPTIONS,GET,HEAD,POST,DELETE,TRACE,PROPFIND,PROPPATCH,COPY,MOVE,LOCK,UNLOCK,CHECKOUT\r\n" +
                "Content-Length: 179\r\n" +
                "Keep-Alive: timeout=15, max=99\r\n" +
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
        public void Test3()
        {
            stubs.Attach(provider.MakeActivity);

            string request =
                "MKACTIVITY /!svn/act/b50ca3a0-05d8-5b4d-8b51-11fce9cbc603 HTTP/1.1\r\n" +
                "Host: localhost:8084\r\n" +
                "User-Agent: SVN/1.4.4 (r25188) neon/0.26.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 201 Created\r\n" +
                "Date: Wed, 02 Jan 2008 23:57:52 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Cache-Control: no-cache\r\n" +
                "Location: http://localhost:8084/!svn/act/b50ca3a0-05d8-5b4d-8b51-11fce9cbc603\r\n" +
                "Content-Length: 312\r\n" +
                "Content-Type: text/html\r\n" +
                "X-Pad: avoid browser bug\r\n" +
                "\r\n" +
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>201 Created</title>\n" +
                "</head><body>\n" +
                "<h1>Created</h1>\n" +
                "<p>Activity /!svn/act/b50ca3a0-05d8-5b4d-8b51-11fce9cbc603 has been created.</p>\n" +
                "<hr />\n" +
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8084</address>\n" +
                "</body></html>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test4()
        {
            stubs.Attach(provider.ItemExists, true);
            FolderMetaData folder = new FolderMetaData();
            folder.Name = "A !@#$%^&()_-+={[}];',.~`/B !@#$%^&()_-+={[}];',.~`";
            stubs.Attach(provider.GetItems, folder);

            string request =
                "PROPFIND /A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/B%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60 HTTP/1.1\r\n" +
                "Host: localhost:8084\r\n" +
                "User-Agent: SVN/1.4.4 (r25188) neon/0.26.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Length: 133\r\n" +
                "Content-Type: text/xml\r\n" +
                "Depth: 0\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><version-controlled-configuration xmlns=\"DAV:\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Wed, 02 Jan 2008 23:57:52 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 514\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/A%20!@%23$%25%5e&amp;()_-+=%7b%5b%7d%5d%3b',.~%60/B%20!@%23$%25%5e&amp;()_-+=%7b%5b%7d%5d%3b',.~%60/</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:version-controlled-configuration><D:href>/!svn/vcc/default</D:href></lp1:version-controlled-configuration>\n" +
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
            stubs.Attach(provider.GetLatestVersion, 5724);

            string request =
                "PROPFIND /!svn/vcc/default HTTP/1.1\r\n" +
                "Host: localhost:8084\r\n" +
                "User-Agent: SVN/1.4.4 (r25188) neon/0.26.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Length: 111\r\n" +
                "Content-Type: text/xml\r\n" +
                "Depth: 0\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><checked-in xmlns=\"DAV:\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Wed, 02 Jan 2008 23:57:52 GMT\r\n" +
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
                "<lp1:checked-in><D:href>/!svn/bln/5724</D:href></lp1:checked-in>\n" +
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
            string request =
                "CHECKOUT /!svn/bln/5724 HTTP/1.1\r\n" +
                "Host: localhost:8084\r\n" +
                "User-Agent: SVN/1.4.4 (r25188) neon/0.26.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Length: 174\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:checkout xmlns:D=\"DAV:\"><D:activity-set><D:href>/!svn/act/b50ca3a0-05d8-5b4d-8b51-11fce9cbc603</D:href></D:activity-set></D:checkout>";

            string expected =
                "HTTP/1.1 201 Created\r\n" +
                "Date: Wed, 02 Jan 2008 23:57:52 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Cache-Control: no-cache\r\n" +
                "Location: http://localhost:8084//!svn/wbl/b50ca3a0-05d8-5b4d-8b51-11fce9cbc603/5724\r\n" +
                "Content-Length: 330\r\n" +
                "Content-Type: text/html\r\n" +
                "\r\n" +
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>201 Created</title>\n" +
                "</head><body>\n" +
                "<h1>Created</h1>\n" +
                "<p>Checked-out resource //!svn/wbl/b50ca3a0-05d8-5b4d-8b51-11fce9cbc603/5724 has been created.</p>\n" +
                "<hr />\n" +
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8084</address>\n" +
                "</body></html>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test7()
        {
            stubs.Attach(provider.SetActivityComment);

            string request =
                "PROPPATCH //!svn/wbl/b50ca3a0-05d8-5b4d-8b51-11fce9cbc603/5724 HTTP/1.1\r\n" +
                "Host: localhost:8084\r\n" +
                "User-Agent: SVN/1.4.4 (r25188) neon/0.26.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Length: 198\r\n" +
                "Content-Type: application/xml\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\" ?>\n" +
                "<D:propertyupdate xmlns:D=\"DAV:\"><D:set><D:prop><log xmlns=\"http://subversion.tigris.org/xmlns/svn/\">Replaced file</log></D:prop></D:set>\n" +
                "</D:propertyupdate>\n";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Wed, 02 Jan 2008 23:57:53 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 348\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/svn/\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response>\n" +
                "<D:href>//!svn/wbl/b50ca3a0-05d8-5b4d-8b51-11fce9cbc603/5724</D:href>\n" +
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
        public void Test8()
        {
            stubs.Attach(provider.ItemExists, true);
            stubs.Attach(provider.GetLatestVersion, 5721);
            FolderMetaData item = new FolderMetaData();
            item.Name = "A !@#$%^&()_-+={[}];',.~`/B !@#$%^&()_-+={[}];',.~`";
            item.ItemRevision = 5721;
            stubs.Attach(provider.GetItems, item);

            string request =
                "PROPFIND /A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/B%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60 HTTP/1.1\r\n" +
                "Host: localhost:8084\r\n" +
                "User-Agent: SVN/1.4.4 (r25188) neon/0.26.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Length: 111\r\n" +
                "Content-Type: text/xml\r\n" +
                "Depth: 0\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><checked-in xmlns=\"DAV:\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Wed, 02 Jan 2008 23:57:53 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 567\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/A%20!@%23$%25%5e&amp;()_-+=%7b%5b%7d%5d%3b',.~%60/B%20!@%23$%25%5e&amp;()_-+=%7b%5b%7d%5d%3b',.~%60/</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:checked-in><D:href>/!svn/ver/5721/A%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60/B%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60</D:href></lp1:checked-in>\n" +
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
            ItemMetaData item = new ItemMetaData();
            item.ItemRevision = 0;
            stubs.Attach(provider.GetItems, item);

            string request =
                "CHECKOUT /!svn/ver/5721/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/B%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60 HTTP/1.1\r\n" +
                "Host: localhost:8084\r\n" +
                "User-Agent: SVN/1.4.4 (r25188) neon/0.26.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Length: 174\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:checkout xmlns:D=\"DAV:\"><D:activity-set><D:href>/!svn/act/b50ca3a0-05d8-5b4d-8b51-11fce9cbc603</D:href></D:activity-set></D:checkout>";

            string expected =
                "HTTP/1.1 201 Created\r\n" +
                "Date: Wed, 02 Jan 2008 23:57:53 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Cache-Control: no-cache\r\n" +
                "Location: http://localhost:8084//!svn/wrk/b50ca3a0-05d8-5b4d-8b51-11fce9cbc603/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/B%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60\r\n" +
                "Content-Length: 425\r\n" +
                "Content-Type: text/html\r\n" +
                "\r\n" +
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>201 Created</title>\n" +
                "</head><body>\n" +
                "<h1>Created</h1>\n" +
                "<p>Checked-out resource //!svn/wrk/b50ca3a0-05d8-5b4d-8b51-11fce9cbc603/A%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60/B%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60 has been created.</p>\n" +
                "<hr />\n" +
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8084</address>\n" +
                "</body></html>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test10()
        {
            stubs.Attach(provider.DeleteItem, true);

            string request =
                "DELETE //!svn/wrk/b50ca3a0-05d8-5b4d-8b51-11fce9cbc603/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/B%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/C%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60..txt HTTP/1.1\r\n" +
                "Host: localhost:8084\r\n" +
                "User-Agent: SVN/1.4.4 (r25188) neon/0.26.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "X-SVN-Version-Name: 5724\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 204 No Content\r\n" +
                "Date: Wed, 02 Jan 2008 23:57:53 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 0\r\n" +
                "Content-Type: text/plain\r\n" +
                "\r\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test11()
        {
            stubs.Attach(provider.GetItems, Return.Value(null));
            stubs.Attach(provider.WriteFile, true);

            string request =
                "PUT //!svn/wrk/b50ca3a0-05d8-5b4d-8b51-11fce9cbc603/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/B%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/C%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60..txt HTTP/1.1\r\n" +
                "Host: localhost:8084\r\n" +
                "User-Agent: SVN/1.4.4 (r25188) neon/0.26.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: application/vnd.svn-svndiff\r\n" +
                "X-SVN-Result-Fulltext-MD5: 91bb248359043fe98416e259c9bdf10d\r\n" +
                "Content-Length: 18\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "SVN\0\0\0\u0008\u0001\u0008\u0088replaced";

            string expected =
                "HTTP/1.1 201 Created\r\n" +
                "Date: Wed, 02 Jan 2008 23:57:53 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Location: http://localhost:8084//!svn/wrk/b50ca3a0-05d8-5b4d-8b51-11fce9cbc603/A !@#$%^&()_-+={[}];',.~`/B !@#$%^&()_-+={[}];',.~`/C !@#$%^&()_-+={[}];',.~`..txt\r\n" +
                "Content-Length: 408\r\n" +
                "Content-Type: text/html\r\n" +
                "\r\n" +
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>201 Created</title>\n" +
                "</head><body>\n" +
                "<h1>Created</h1>\n" +
                "<p>Resource //!svn/wrk/b50ca3a0-05d8-5b4d-8b51-11fce9cbc603/A !@#$%^&amp;()_-+={[}];',.~`/B !@#$%^&amp;()_-+={[}];',.~`/C !@#$%^&amp;()_-+={[}];',.~`..txt has been created.</p>\n" +
                "<hr />\n" +
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8084</address>\n" +
                "</body></html>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test12()
        {
            MergeActivityResponse mergeResponse =
                new MergeActivityResponse(5725, DateTime.Parse("2008-01-02T23:57:53.590563Z"), "jwanagel");
            mergeResponse.Items.Add(
                new MergeActivityResponseItem(ItemType.File,
                                              "/A !@#$%^&()_-+={[}];',.~`/B !@#$%^&()_-+={[}];',.~`/C !@#$%^&()_-+={[}];',.~`..txt"));
            mergeResponse.Items.Add(
                new MergeActivityResponseItem(ItemType.Folder, "/A !@#$%^&()_-+={[}];',.~`/B !@#$%^&()_-+={[}];',.~`"));
            stubs.Attach(provider.MergeActivity, mergeResponse);

            string request =
                "MERGE /A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/B%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60 HTTP/1.1\r\n" +
                "Host: localhost:8084\r\n" +
                "User-Agent: SVN/1.4.4 (r25188) neon/0.26.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Length: 297\r\n" +
                "Content-Type: text/xml\r\n" +
                "X-SVN-Options:  release-locks\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:merge xmlns:D=\"DAV:\"><D:source><D:href>/!svn/act/b50ca3a0-05d8-5b4d-8b51-11fce9cbc603</D:href></D:source><D:no-auto-merge/><D:no-checkout/><D:prop><D:checked-in/><D:version-name/><D:resourcetype/><D:creationdate/><D:creator-displayname/></D:prop></D:merge>";

            string expected =
                "HTTP/1.1 200 OK\r\n" +
                "Date: Wed, 02 Jan 2008 23:57:53 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Cache-Control: no-cache\r\n" +
                "Transfer-Encoding: chunked\r\n" +
                "Content-Type: text/xml\r\n" +
                "\r\n" +
                "592\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:merge-response xmlns:D=\"DAV:\">\n" +
                "<D:updated-set>\n" +
                "<D:response>\n" +
                "<D:href>/!svn/vcc/default</D:href>\n" +
                "<D:propstat><D:prop>\n" +
                "<D:resourcetype><D:baseline/></D:resourcetype>\n" +
                "\n" +
                "<D:version-name>5725</D:version-name>\n" +
                "<D:creationdate>2008-01-02T23:57:53.590563Z</D:creationdate>\n" +
                "<D:creator-displayname>jwanagel</D:creator-displayname>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "<D:response>\n" +
                "<D:href>/A%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60/B%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60/C%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60..txt</D:href>\n" +
                "<D:propstat><D:prop>\n" +
                "<D:resourcetype/>\n" +
                "<D:checked-in><D:href>/!svn/ver/5725/A%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60/B%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60/C%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60..txt</D:href></D:checked-in>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "<D:response>\n" +
                "<D:href>/A%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60/B%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60</D:href>\n" +
                "<D:propstat><D:prop>\n" +
                "<D:resourcetype><D:collection/></D:resourcetype>\n" +
                "<D:checked-in><D:href>/!svn/ver/5725/A%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60/B%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60</D:href></D:checked-in>\n" +
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
        public void Test13()
        {
            stubs.Attach(provider.DeleteActivity);

            string request =
                "DELETE /!svn/act/b50ca3a0-05d8-5b4d-8b51-11fce9cbc603 HTTP/1.1\r\n" +
                "Host: localhost:8084\r\n" +
                "User-Agent: SVN/1.4.4 (r25188) neon/0.26.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 204 No Content\r\n" +
                "Date: Wed, 02 Jan 2008 23:57:53 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 0\r\n" +
                "Content-Type: text/plain\r\n" +
                "\r\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }
    }
}