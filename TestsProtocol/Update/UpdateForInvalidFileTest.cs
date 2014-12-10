using System;
using SvnBridge.SourceControl;
using CodePlex.TfsLibrary;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using Xunit;
using Attach;
using Tests;

namespace ProtocolTests
{
    public class UpdateForInvalidFileTest : ProtocolTestsBase
    {
        [Fact]
        public void Test1()
        {
            stubs.Attach(provider.GetItems, Return.Value(null));

            string request =
                "REPORT /!svn/vcc/default HTTP/1.1\r\n" +
                "Host: localhost:8080\r\n" +
                "User-Agent: SVN/1.5.4 (r33841) neon/0.25.4\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Length: 294\r\n" +
                "Content-Type: text/xml\r\n" +
                "Accept-Encoding: gzip, svndiff1;q=0.9,svndiff;q=0.8, gzip\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/depth\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/mergeinfo\r\n" +
                "DAV: http://subversion.tigris.org/xmlns/dav/svn/log-revprops\r\n" +
                "\r\n" +
                "<update-report xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" send-all=\"true\" xmlns=\"svn:\"><entry rev=\"5795\" start-empty=\"true\" /><src-path>http://localhost:8080/svn/robots.txt</src-path><target-revision>5795</target-revision></update-report>\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 500 Internal Server Error\r\n" +
                "Date: Thu, 05 Feb 2009 23:41:13 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 222\r\n" +
                "Connection: close\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:error xmlns:D=\"DAV:\" xmlns:m=\"http://apache.org/dav/xmlns\" xmlns:C=\"svn:\">\n" +
                "<C:error/>\n" +
                "<m:human-readable errcode=\"160005\">\n" +
                "Target path does not exist\n" +
                "</m:human-readable>\n" +
                "</D:error>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }
    }
}