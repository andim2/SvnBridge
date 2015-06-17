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
    public class UpdateWithModifiedFileAfterCommitModifiedFileTest : ProtocolTestsBase
    {
        [Fact]
        public void Test1()
        {
            stubs.Attach((MyMocks.ItemExists)provider.ItemExists, new NetworkAccessDeniedException());

            string request =
                "PROPFIND /svn HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.4.3 (r23084) neon/0.26.1\r\n" +
                "Keep-Alive: \r\n" +
                "Connection: TE, Keep-Alive\r\n" +
                "TE: trailers\r\n" +
                "Content-Length: 300\r\n" +
                "Content-Type: text/xml\r\n" +
                "Depth: 0\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><version-controlled-configuration xmlns=\"DAV:\"/><resourcetype xmlns=\"DAV:\"/><baseline-relative-path xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/><repository-uuid xmlns=\"http://subversion.tigris.org/xmlns/dav/\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 401 Authorization Required\r\n" +
                "Date: Thu, 16 Oct 2008 00:40:14 GMT\r\n" +
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
            item.Name = "svn";
            stubs.Attach(provider.GetItems, item);

            string request =
                "PROPFIND /svn HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.4.3 (r23084) neon/0.26.1\r\n" +
                "Keep-Alive: \r\n" +
                "Connection: TE, Keep-Alive\r\n" +
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
                "Date: Thu, 16 Oct 2008 00:40:14 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 664\r\n" +
                "Keep-Alive: timeout=15, max=99\r\n" +
                "Connection: Keep-Alive\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/svn/</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:version-controlled-configuration><D:href>/!svn/vcc/default</D:href></lp1:version-controlled-configuration>\n" +
                "<lp1:resourcetype><D:collection/></lp1:resourcetype>\n" +
                "<lp2:baseline-relative-path>svn</lp2:baseline-relative-path>\n" +
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
            stubs.Attach(provider.GetLatestVersion, 5782);

            string request =
                "PROPFIND /!svn/vcc/default HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.4.3 (r23084) neon/0.26.1\r\n" +
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
                "Date: Thu, 16 Oct 2008 00:40:14 GMT\r\n" +
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
                "<lp1:checked-in><D:href>/!svn/bln/5782</D:href></lp1:checked-in>\n" +
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
            string request =
                "PROPFIND /!svn/bln/5782 HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.4.3 (r23084) neon/0.26.1\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Length: 148\r\n" +
                "Content-Type: text/xml\r\n" +
                "Depth: 0\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><prop><baseline-collection xmlns=\"DAV:\"/><version-name xmlns=\"DAV:\"/></prop></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Thu, 16 Oct 2008 00:40:14 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 440\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/!svn/bln/5782</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:baseline-collection><D:href>/!svn/bc/5782/</D:href></lp1:baseline-collection>\n" +
                "<lp1:version-name>5782</lp1:version-name>\n" +
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
            ItemMetaData item = new FolderMetaData();
            item.Name = "svn";
            stubs.Attach(provider.GetItems, item);

            string request =
                "PROPFIND /svn HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.4.3 (r23084) neon/0.26.1\r\n" +
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
                "Date: Thu, 16 Oct 2008 00:40:14 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 664\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/svn/</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:version-controlled-configuration><D:href>/!svn/vcc/default</D:href></lp1:version-controlled-configuration>\n" +
                "<lp1:resourcetype><D:collection/></lp1:resourcetype>\n" +
                "<lp2:baseline-relative-path>svn</lp2:baseline-relative-path>\n" +
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
        public void Test6()
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

            string request =
                "REPORT /!svn/vcc/default HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.4.3 (r23084) neon/0.26.1\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Length: 194\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: svndiff1;q=0.9,svndiff;q=0.8\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<S:update-report send-all=\"true\" xmlns:S=\"svn:\"><S:src-path>http://localhost:8080/svn</S:src-path><S:entry rev=\"5780\" ></S:entry><S:entry rev=\"5781\" >Commerce.MVC.sln</S:entry></S:update-report>";

            string expected =
                "HTTP/1.1 200 OK\r\n" +
                "Date: Thu, 16 Oct 2008 00:40:14 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Transfer-Encoding: chunked\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "4b8\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<S:update-report xmlns:S=\"svn:\" xmlns:V=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:D=\"DAV:\" send-all=\"true\">\n" +
                "<S:target-revision rev=\"5782\"/>\n" +
                "<S:open-directory rev=\"5780\">\n" +
                "<D:checked-in><D:href>/!svn/ver/5782/svn</D:href></D:checked-in>\n" +
                "<S:set-prop name=\"svn:entry:committed-rev\">5782</S:set-prop>\n" +
                "<S:set-prop name=\"svn:entry:committed-date\">2008-10-16T00:39:59.089062Z</S:set-prop>\n" +
                "<S:set-prop name=\"svn:entry:last-author\">jwanagel</S:set-prop>\n" +
                "<S:set-prop name=\"svn:entry:uuid\">81a5aebe-f34e-eb42-b435-ac1ecbb335f7</S:set-prop>\n" +
                "<S:open-file name=\"Commerce.MVC.sln\" rev=\"5781\">\n" +
                "<D:checked-in><D:href>/!svn/ver/5782/svn/Commerce.MVC.sln</D:href></D:checked-in>\n" +
                "<S:set-prop name=\"svn:entry:committed-rev\">5782</S:set-prop>\n" +
                "<S:set-prop name=\"svn:entry:committed-date\">2008-10-16T00:39:59.089062Z</S:set-prop>\n" +
                "<S:set-prop name=\"svn:entry:last-author\">jwanagel</S:set-prop>\n" +
                "<S:set-prop name=\"svn:entry:uuid\">81a5aebe-f34e-eb42-b435-ac1ecbb335f7</S:set-prop>\n" +
                //"<S:txdelta>U1ZOAQAHCgILAYoKMw0KNA0KNQ0KNg==\n" +
                "<S:txdelta>U1ZOAAAACgEKijMNCjQNCjUNCjY=\n" +
                "</S:txdelta><S:prop><V:md5-checksum>92e36386f7788cc10cabdf113fc858bb</V:md5-checksum></S:prop>\n" +
                "</S:open-file>\n" +
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