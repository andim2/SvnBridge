using System;
using SvnBridge.SourceControl;
using CodePlex.TfsLibrary;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using Xunit;
using Attach;
using Tests;

namespace ProtocolTests
{
    public class PropFindAllPropVccDefaultTest : ProtocolTestsBase
    {
        [Fact]
        public void Test1()
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
    }
}