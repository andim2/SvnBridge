using System;
using CodePlex.TfsLibrary;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using Xunit;
using SvnBridge.SourceControl;
using Tests;

namespace ProtocolTests
{
    public class CommitRenamedFolderWithRenamedFileAndNotRenamedFile : ProtocolTestsBase
    {
        [Fact]
        public void Test1()
        {
            stubs.Attach((MyMocks.ItemExists) provider.ItemExists, new NetworkAccessDeniedException());

            string request =
                "OPTIONS / HTTP/1.1\r\n" +
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
                "Date: Sat, 19 Jan 2008 23:18:59 GMT\r\n" +
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
        public void Test10()
        {
            stubs.Attach(provider.DeleteItem, true);

            string request =
                "DELETE //!svn/wrk/15407bc3-2250-aa4c-aa51-4e65b2c824c3/B%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60 HTTP/1.1\r\n" +
                "Host: localhost:8084\r\n" +
                "User-Agent: SVN/1.4.4 (r25188) neon/0.26.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "X-SVN-Version-Name: 5730\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 204 No Content\r\n" +
                "Date: Sat, 19 Jan 2008 23:19:05 GMT\r\n" +
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
            stubs.Attach(provider.ItemExists, true);
            stubs.Attach(provider.IsDirectory, true);
            FolderMetaData item = new FolderMetaData();
            item.Name = "B !@#$%^&()_-+={[}];',.~`";
            item.ItemRevision = 5730;
            stubs.Attach(provider.GetItems, item);

            string request =
                "PROPFIND /B%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60 HTTP/1.1\r\n" +
                "Host: localhost:8084\r\n" +
                "User-Agent: SVN/1.4.4 (r25188) neon/0.26.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Length: 300\r\n" +
                "Content-Type: text/xml\r\n" +
                "Depth: 0\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><version-controlled-configuration xmlns=\"DAV:\"/><resourcetype xmlns=\"DAV:\"/><baseline-relative-path xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/><repository-uuid xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Sat, 19 Jan 2008 23:19:05 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 736\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/B%20!@%23$%25%5e&amp;()_-+=%7b%5b%7d%5d%3b',.~%60/</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:version-controlled-configuration><D:href>/!svn/vcc/default</D:href></lp1:version-controlled-configuration>\n" +
                "<lp1:resourcetype><D:collection/></lp1:resourcetype>\n" +
                "<lp2:baseline-relative-path>B !@#$%^&amp;()_-+={[}];',.~`</lp2:baseline-relative-path>\n" +
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
            stubs.Attach(provider.GetLatestVersion, 5730);

            string request =
                "PROPFIND /!svn/vcc/default HTTP/1.1\r\n" +
                "Host: localhost:8084\r\n" +
                "User-Agent: SVN/1.4.4 (r25188) neon/0.26.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Length: 148\r\n" +
                "Content-Type: text/xml\r\n" +
                "Label: 5730\r\n" +
                "Depth: 0\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><baseline-collection xmlns=\"DAV:\"/><version-name xmlns=\"DAV:\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Sat, 19 Jan 2008 23:19:05 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Vary: Label\r\n" +
                "Content-Length: 440\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/!svn/bln/5730</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:baseline-collection><D:href>/!svn/bc/5730/</D:href></lp1:baseline-collection>\n" +
                "<lp1:version-name>5730</lp1:version-name>\n" +
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
            stubs.Attach(provider.CopyItem);

            string request =
                "COPY /!svn/bc/5730/B%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60 HTTP/1.1\r\n" +
                "Host: localhost:8084\r\n" +
                "User-Agent: SVN/1.4.4 (r25188) neon/0.26.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Depth: infinity\r\n" +
                "Destination: http://localhost:8084//!svn/wrk/15407bc3-2250-aa4c-aa51-4e65b2c824c3/BB%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60\r\n" +
                "Overwrite: T\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 201 Created\r\n" +
                "Date: Sat, 19 Jan 2008 23:19:05 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Location: http://localhost:8084//!svn/wrk/15407bc3-2250-aa4c-aa51-4e65b2c824c3/BB !@#$%^&()_-+={[}];',.~`\r\n" +
                "Content-Length: 347\r\n" +
                "Content-Type: text/html\r\n" +
                "\r\n" +
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>201 Created</title>\n" +
                "</head><body>\n" +
                "<h1>Created</h1>\n" +
                "<p>Destination //!svn/wrk/15407bc3-2250-aa4c-aa51-4e65b2c824c3/BB !@#$%^&amp;()_-+={[}];',.~` has been created.</p>\n" +
                "<hr />\n" +
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8084</address>\n" +
                "</body></html>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test14()
        {
            stubs.Attach(provider.DeleteItem, true);

            string request =
                "DELETE //!svn/wrk/15407bc3-2250-aa4c-aa51-4e65b2c824c3/BB%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/C%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60.txt HTTP/1.1\r\n" +
                "Host: localhost:8084\r\n" +
                "User-Agent: SVN/1.4.4 (r25188) neon/0.26.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "X-SVN-Version-Name: 5730\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 204 No Content\r\n" +
                "Date: Sat, 19 Jan 2008 23:19:05 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 0\r\n" +
                "Content-Type: text/plain\r\n" +
                "\r\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test15()
        {
            stubs.Attach(provider.ItemExists, true);
            stubs.Attach(provider.IsDirectory, false);
            ItemMetaData item = new ItemMetaData();
            item.Name = "B !@#$%^&()_-+={[}];',.~`/C !@#$%^&()_-+={[}];',.~`.txt";
            item.ItemRevision = 5730;
            stubs.Attach(provider.GetItems, item);

            string request =
                "PROPFIND /B%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/C%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60.txt HTTP/1.1\r\n" +
                "Host: localhost:8084\r\n" +
                "User-Agent: SVN/1.4.4 (r25188) neon/0.26.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Length: 300\r\n" +
                "Content-Type: text/xml\r\n" +
                "Depth: 0\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><version-controlled-configuration xmlns=\"DAV:\"/><resourcetype xmlns=\"DAV:\"/><baseline-relative-path xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/><repository-uuid xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Sat, 19 Jan 2008 23:19:05 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 790\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/B%20!@%23$%25%5e&amp;()_-+=%7b%5b%7d%5d%3b',.~%60/C%20!@%23$%25%5e&amp;()_-+=%7b%5b%7d%5d%3b',.~%60.txt</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:version-controlled-configuration><D:href>/!svn/vcc/default</D:href></lp1:version-controlled-configuration>\n" +
                "<lp1:resourcetype/>\n" +
                "<lp2:baseline-relative-path>B !@#$%^&amp;()_-+={[}];',.~`/C !@#$%^&amp;()_-+={[}];',.~`.txt</lp2:baseline-relative-path>\n" +
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
        public void Test16()
        {
            string request =
                "PROPFIND /!svn/vcc/default HTTP/1.1\r\n" +
                "Host: localhost:8084\r\n" +
                "User-Agent: SVN/1.4.4 (r25188) neon/0.26.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Length: 148\r\n" +
                "Content-Type: text/xml\r\n" +
                "Label: 5730\r\n" +
                "Depth: 0\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><baseline-collection xmlns=\"DAV:\"/><version-name xmlns=\"DAV:\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Sat, 19 Jan 2008 23:19:05 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Vary: Label\r\n" +
                "Content-Length: 440\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/!svn/bln/5730</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:baseline-collection><D:href>/!svn/bc/5730/</D:href></lp1:baseline-collection>\n" +
                "<lp1:version-name>5730</lp1:version-name>\n" +
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
            stubs.Attach(provider.CopyItem);

            string request =
                "COPY /!svn/bc/5730/B%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/C%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60.txt HTTP/1.1\r\n" +
                "Host: localhost:8084\r\n" +
                "User-Agent: SVN/1.4.4 (r25188) neon/0.26.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Depth: 0\r\n" +
                "Destination: http://localhost:8084//!svn/wrk/15407bc3-2250-aa4c-aa51-4e65b2c824c3/BB%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60/CC%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',.~%60.txt\r\n" +
                "Overwrite: T\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 201 Created\r\n" +
                "Date: Sat, 19 Jan 2008 23:19:06 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Location: http://localhost:8084//!svn/wrk/15407bc3-2250-aa4c-aa51-4e65b2c824c3/BB !@#$%^&()_-+={[}];',.~`/CC !@#$%^&()_-+={[}];',.~`.txt\r\n" +
                "Content-Length: 382\r\n" +
                "Content-Type: text/html\r\n" +
                "\r\n" +
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>201 Created</title>\n" +
                "</head><body>\n" +
                "<h1>Created</h1>\n" +
                "<p>Destination //!svn/wrk/15407bc3-2250-aa4c-aa51-4e65b2c824c3/BB !@#$%^&amp;()_-+={[}];',.~`/CC !@#$%^&amp;()_-+={[}];',.~`.txt has been created.</p>\n" +
                "<hr />\n" +
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8084</address>\n" +
                "</body></html>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test18()
        {
            MergeActivityResponse mergeResponse =
                new MergeActivityResponse(5731, DateTime.Parse("2008-01-19T23:19:06.280582Z"), "jwanagel");
            mergeResponse.Items.Add(new MergeActivityResponseItem(ItemType.Folder, "/"));
            mergeResponse.Items.Add(new MergeActivityResponseItem(ItemType.Folder, "/BB !@#$%^&()_-+={[}];',.~`"));
            mergeResponse.Items.Add(
                new MergeActivityResponseItem(ItemType.File,
                                              "/BB !@#$%^&()_-+={[}];',.~`/CC !@#$%^&()_-+={[}];',.~`.txt"));
            stubs.Attach(provider.MergeActivity, mergeResponse);

            string request =
                "MERGE / HTTP/1.1\r\n" +
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
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:merge xmlns:D=\"DAV:\"><D:source><D:href>/!svn/act/15407bc3-2250-aa4c-aa51-4e65b2c824c3</D:href></D:source><D:no-auto-merge/><D:no-checkout/><D:prop><D:checked-in/><D:version-name/><D:resourcetype/><D:creationdate/><D:creator-displayname/></D:prop></D:merge>";

            string expected =
                "HTTP/1.1 200 OK\r\n" +
                "Date: Sat, 19 Jan 2008 23:19:06 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Cache-Control: no-cache\r\n" +
                "Transfer-Encoding: chunked\r\n" +
                "Content-Type: text/xml\r\n" +
                "\r\n" +
                "5bd\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:merge-response xmlns:D=\"DAV:\">\n" +
                "<D:updated-set>\n" +
                "<D:response>\n" +
                "<D:href>/!svn/vcc/default</D:href>\n" +
                "<D:propstat><D:prop>\n" +
                "<D:resourcetype><D:baseline/></D:resourcetype>\n" +
                "\n" +
                "<D:version-name>5731</D:version-name>\n" +
                "<D:creationdate>2008-01-19T23:19:06.280582Z</D:creationdate>\n" +
                "<D:creator-displayname>jwanagel</D:creator-displayname>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "<D:response>\n" +
                "<D:href>/</D:href>\n" +
                "<D:propstat><D:prop>\n" +
                "<D:resourcetype><D:collection/></D:resourcetype>\n" +
                "<D:checked-in><D:href>/!svn/ver/5731/</D:href></D:checked-in>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "<D:response>\n" +
                "<D:href>/BB%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60</D:href>\n" +
                "<D:propstat><D:prop>\n" +
                "<D:resourcetype><D:collection/></D:resourcetype>\n" +
                "<D:checked-in><D:href>/!svn/ver/5731/BB%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60</D:href></D:checked-in>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "<D:response>\n" +
                "<D:href>/BB%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60/CC%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60.txt</D:href>\n" +
                "<D:propstat><D:prop>\n" +
                "<D:resourcetype/>\n" +
                "<D:checked-in><D:href>/!svn/ver/5731/BB%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60/CC%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',.~%60.txt</D:href></D:checked-in>\n" +
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
        public void Test19()
        {
            stubs.Attach(provider.DeleteActivity);

            string request =
                "DELETE /!svn/act/15407bc3-2250-aa4c-aa51-4e65b2c824c3 HTTP/1.1\r\n" +
                "Host: localhost:8084\r\n" +
                "User-Agent: SVN/1.4.4 (r25188) neon/0.26.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 204 No Content\r\n" +
                "Date: Sat, 19 Jan 2008 23:19:06 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 0\r\n" +
                "Content-Type: text/plain\r\n" +
                "\r\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test2()
        {
            stubs.Attach(provider.ItemExists, true);

            string request =
                "OPTIONS / HTTP/1.1\r\n" +
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
                "Date: Sat, 19 Jan 2008 23:19:04 GMT\r\n" +
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
                "MKACTIVITY /!svn/act/15407bc3-2250-aa4c-aa51-4e65b2c824c3 HTTP/1.1\r\n" +
                "Host: localhost:8084\r\n" +
                "User-Agent: SVN/1.4.4 (r25188) neon/0.26.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 201 Created\r\n" +
                "Date: Sat, 19 Jan 2008 23:19:04 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Cache-Control: no-cache\r\n" +
                "Location: http://localhost:8084/!svn/act/15407bc3-2250-aa4c-aa51-4e65b2c824c3\r\n" +
                "Content-Length: 312\r\n" +
                "Content-Type: text/html\r\n" +
                "X-Pad: avoid browser bug\r\n" +
                "\r\n" +
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>201 Created</title>\n" +
                "</head><body>\n" +
                "<h1>Created</h1>\n" +
                "<p>Activity /!svn/act/15407bc3-2250-aa4c-aa51-4e65b2c824c3 has been created.</p>\n" +
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
            folder.Name = "";
            stubs.Attach(provider.GetItems, folder);

            string request =
                "PROPFIND / HTTP/1.1\r\n" +
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
                "Date: Sat, 19 Jan 2008 23:19:04 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 414\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/</D:href>\n" +
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
            stubs.Attach(provider.GetLatestVersion, 5730);

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
                "Date: Sat, 19 Jan 2008 23:19:04 GMT\r\n" +
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
                "<lp1:checked-in><D:href>/!svn/bln/5730</D:href></lp1:checked-in>\n" +
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
                "CHECKOUT /!svn/bln/5730 HTTP/1.1\r\n" +
                "Host: localhost:8084\r\n" +
                "User-Agent: SVN/1.4.4 (r25188) neon/0.26.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Length: 174\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:checkout xmlns:D=\"DAV:\"><D:activity-set><D:href>/!svn/act/15407bc3-2250-aa4c-aa51-4e65b2c824c3</D:href></D:activity-set></D:checkout>";

            string expected =
                "HTTP/1.1 201 Created\r\n" +
                "Date: Sat, 19 Jan 2008 23:19:05 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Cache-Control: no-cache\r\n" +
                "Location: http://localhost:8084//!svn/wbl/15407bc3-2250-aa4c-aa51-4e65b2c824c3/5730\r\n" +
                "Content-Length: 330\r\n" +
                "Content-Type: text/html\r\n" +
                "\r\n" +
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>201 Created</title>\n" +
                "</head><body>\n" +
                "<h1>Created</h1>\n" +
                "<p>Checked-out resource //!svn/wbl/15407bc3-2250-aa4c-aa51-4e65b2c824c3/5730 has been created.</p>\n" +
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
                "PROPPATCH //!svn/wbl/15407bc3-2250-aa4c-aa51-4e65b2c824c3/5730 HTTP/1.1\r\n" +
                "Host: localhost:8084\r\n" +
                "User-Agent: SVN/1.4.4 (r25188) neon/0.26.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Length: 256\r\n" +
                "Content-Type: application/xml\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\" ?>\n" +
                "<D:propertyupdate xmlns:D=\"DAV:\"><D:set><D:prop><log xmlns=\"http://subversion.tigris.org/xmlns/svn/\">Rename file within renamed folder that also contains a not renamed file</log></D:prop></D:set>\n" +
                "</D:propertyupdate>\n";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Sat, 19 Jan 2008 23:19:05 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 348\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/svn/\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response>\n" +
                "<D:href>//!svn/wbl/15407bc3-2250-aa4c-aa51-4e65b2c824c3/5730</D:href>\n" +
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
            stubs.Attach(provider.IsDirectory, true);
            stubs.Attach(provider.GetLatestVersion, 5730);
            ItemMetaData item = new ItemMetaData();
            item.Name = "";
            item.ItemRevision = 5730;
            stubs.Attach(provider.GetItems, item);

            string request =
                "PROPFIND / HTTP/1.1\r\n" +
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
                "Date: Sat, 19 Jan 2008 23:19:05 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 368\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:checked-in><D:href>/!svn/ver/5730/</D:href></lp1:checked-in>\n" +
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
                "CHECKOUT /!svn/ver/5730/ HTTP/1.1\r\n" +
                "Host: localhost:8084\r\n" +
                "User-Agent: SVN/1.4.4 (r25188) neon/0.26.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Length: 174\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:checkout xmlns:D=\"DAV:\"><D:activity-set><D:href>/!svn/act/15407bc3-2250-aa4c-aa51-4e65b2c824c3</D:href></D:activity-set></D:checkout>";

            string expected =
                "HTTP/1.1 201 Created\r\n" +
                "Date: Sat, 19 Jan 2008 23:19:05 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Cache-Control: no-cache\r\n" +
                "Location: http://localhost:8084//!svn/wrk/15407bc3-2250-aa4c-aa51-4e65b2c824c3/\r\n" +
                "Content-Length: 326\r\n" +
                "Content-Type: text/html\r\n" +
                "\r\n" +
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>201 Created</title>\n" +
                "</head><body>\n" +
                "<h1>Created</h1>\n" +
                "<p>Checked-out resource //!svn/wrk/15407bc3-2250-aa4c-aa51-4e65b2c824c3/ has been created.</p>\n" +
                "<hr />\n" +
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8084</address>\n" +
                "</body></html>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }
    }
}