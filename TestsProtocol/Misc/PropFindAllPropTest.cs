using System;
using SvnBridge.SourceControl;
using CodePlex.TfsLibrary;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using Xunit;
using Attach;
using Tests;

namespace ProtocolTests
{
    public class PropFindAllPropTest : ProtocolTestsBase
    {
        [Fact]
        public void VccDefault()
        {
            stubs.Attach(provider.GetLatestVersion, 5795);

            string request =
                "PROPFIND /!svn/vcc/default HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.3 (r33570) neon/0.28.3\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml\r\n" +
                "Depth: 0\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "Content-Length: 82\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><propfind xmlns=\"DAV:\"><allprop/></propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Fri, 06 Feb 2009 06:49:52 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 626\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/!svn/vcc/default</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:checked-in><D:href>/!svn/bln/5795</D:href></lp1:checked-in>\n" +
                "<lp2:repository-uuid>81a5aebe-f34e-eb42-b435-ac1ecbb335f7</lp2:repository-uuid>\n" +
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
        public void Path()
        {
            FolderMetaData item = new FolderMetaData();
            item.Name = "svn";
            item.ItemRevision = 5782;
            item.Author = "jwanagel";
            item.LastModifiedDate = DateTime.Parse("2008-10-16T00:39:59.089062Z");
            item.Properties.Add("bugtraq:message", "Work Item: %BUGID%");
            stubs.Attach(provider.GetItems, item);

            string request =
                "PROPFIND /svn HTTP/1.1\r\n" +
                "Host: codeplex-source\r\n" +
                "User-Agent: SVN/1.5.4 SVNKit/1.2.1 (http://svnkit.com/) rSNAPSHOT\r\n" +
                "Connection: TE, Keep-Alive\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Depth: 0\r\n" +
                "Content-Length: 190\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                "<propfind xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns=\"DAV:\">\r\n" +
                "  <allprop />\r\n" +
                "</propfind>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Mon, 30 Mar 2009 08:00:32 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 1400\r\n" +
                "Keep-Alive: timeout=15, max=100\r\n" +
                "Connection: Keep-Alive\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns2=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:ns1=\"http://www.w3.org/2001/XMLSchema\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:S=\"http://subversion.tigris.org/xmlns/svn/\" xmlns:C=\"http://subversion.tigris.org/xmlns/custom/\" xmlns:V=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/svn/</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<C:bugtraq:message>Work Item: %BUGID%</C:bugtraq:message>\n" +
                "<lp1:getcontenttype>text/html; charset=UTF-8</lp1:getcontenttype>\n" +
                "<lp1:getetag>W/\"5782//svn\"</lp1:getetag>\n" +
                "<lp1:creationdate>2008-10-16T00:39:59.089062Z</lp1:creationdate>\n" +
                "<lp1:getlastmodified>Thu, 16 Oct 2008 00:39:59 GMT</lp1:getlastmodified>\n" +
                "<lp1:checked-in><D:href>/!svn/ver/5782/svn</D:href></lp1:checked-in>\n" +
                "<lp1:version-controlled-configuration><D:href>/!svn/vcc/default</D:href></lp1:version-controlled-configuration>\n" +
                "<lp1:version-name>5782</lp1:version-name>\n" +
                "<lp1:creator-displayname>jwanagel</lp1:creator-displayname>\n" +
                "<lp2:baseline-relative-path>svn</lp2:baseline-relative-path>\n" +
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
    }
}