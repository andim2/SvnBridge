using System;
using SvnBridge.SourceControl;
using CodePlex.TfsLibrary;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using Xunit;
using Attach;
using Tests;

namespace ProtocolTests
{
    public class BrowseTests : ProtocolTestsBase
    {
        [Fact]
        public void Test1()
        {
            stubs.Attach(provider.GetLatestVersion, 5793);
            FolderMetaData folder = CreateFolder("", 5793, "Sun, 11 Jan 2009 23:40:35 GMT");
            folder.Items.Add(CreateFolder("Quick Starts"));
            folder.Items.Add(CreateFolder("branch"));
            folder.Items.Add(CreateItem("foo.bar"));
            folder.Items.Add(CreateFolder("svn"));
            folder.Items.Add(CreateFolder("trunk"));
            stubs.Attach(provider.GetItems, folder);

            string request =
                "GET / HTTP/1.1\r\n" +
                "Accept: image/gif, image/x-xbitmap, image/jpeg, image/pjpeg, application/x-ms-application, application/vnd.ms-xpsdocument, application/xaml+xml, application/x-ms-xbap, application/vnd.ms-excel, application/vnd.ms-powerpoint, application/msword, application/x-shockwave-flash, */*\r\n" +
                "Accept-Language: en-us\r\n" +
                "UA-CPU: x86\r\n" +
                "Accept-Encoding: gzip, deflate\r\n" +
                "User-Agent: Mozilla/4.0 (compatible; MSIE 7.0; Windows NT 6.0; SLCC1; .NET CLR 2.0.50727; .NET CLR 3.0.30618; .NET CLR 3.5.30729; InfoPath.1)\r\n" +
                "Host: localhost:8080\r\n" +
                "Connection: Keep-Alive\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 200 OK\r\n" +
                "Date: Sun, 25 Jan 2009 11:59:40 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Last-Modified: Sun, 11 Jan 2009 23:40:35 GMT\r\n" +
                "ETag: W/\"5793//\"\r\n" +
                "Accept-Ranges: bytes\r\n" +
                "Content-Length: 458\r\n" +
                "Keep-Alive: timeout=15, max=100\r\n" +
                "Connection: Keep-Alive\r\n" +
                "Content-Type: text/html; charset=UTF-8\r\n" +
                "\r\n" +
                "<html><head><title>Revision 5793: /</title></head>\n" +
                "<body>\n" +
                " <h2>Revision 5793: /</h2>\n" +
                " <ul>\n" +
                "  <li><a href=\"Quick%20Starts/\">Quick Starts/</a></li>\n" +
                "  <li><a href=\"branch/\">branch/</a></li>\n" +
                "  <li><a href=\"foo.bar\">foo.bar</a></li>\n" +
                "  <li><a href=\"svn/\">svn/</a></li>\n" +
                "  <li><a href=\"trunk/\">trunk/</a></li>\n" +
                " </ul>\n" +
                " <hr noshade><em><a href=\"http://www.codeplex.com/\">CodePlex</a> powered by <a href=\"http://svnbridge.codeplex.com\">SvnBridge</a></em>\n" +
                "</body></html>";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test2()
        {
            stubs.Attach(provider.GetLatestVersion, 5793);
            FolderMetaData folder = CreateFolder("Quick", 5784, "Sun, 14 Dec 2008 11:23:39 GMT");
            folder.Items.Add(CreateItem("Quick/Test.txt"));
            stubs.Attach(provider.GetItems, folder);
            string request =
                "GET /Quick/ HTTP/1.1\r\n" +
                "Accept: image/gif, image/x-xbitmap, image/jpeg, image/pjpeg, application/x-ms-application, application/vnd.ms-xpsdocument, application/xaml+xml, application/x-ms-xbap, application/vnd.ms-excel, application/vnd.ms-powerpoint, application/msword, application/x-shockwave-flash, */*\r\n" +
                "Referer: http://localhost:8080/\r\n" +
                "Accept-Language: en-us\r\n" +
                "UA-CPU: x86\r\n" +
                "Accept-Encoding: gzip, deflate\r\n" +
                "User-Agent: Mozilla/4.0 (compatible; MSIE 7.0; Windows NT 6.0; SLCC1; .NET CLR 2.0.50727; .NET CLR 3.0.30618; .NET CLR 3.5.30729; InfoPath.1)\r\n" +
                "Host: localhost:8080\r\n" +
                "Connection: Keep-Alive\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 200 OK\r\n" +
                "Date: Sun, 25 Jan 2009 12:30:10 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Last-Modified: Sun, 14 Dec 2008 11:23:39 GMT\r\n" +
                "ETag: W/\"5784//Quick\"\r\n" +
                "Accept-Ranges: bytes\r\n" +
                "Content-Length: 332\r\n" +
                "Keep-Alive: timeout=15, max=100\r\n" +
                "Connection: Keep-Alive\r\n" +
                "Content-Type: text/html; charset=UTF-8\r\n" +
                "\r\n" +
                "<html><head><title>Revision 5793: /Quick</title></head>\n" +
                "<body>\n" +
                " <h2>Revision 5793: /Quick</h2>\n" +
                " <ul>\n" +
                "  <li><a href=\"../\">..</a></li>\n" +
                "  <li><a href=\"Test.txt\">Test.txt</a></li>\n" +
                " </ul>\n" +
                " <hr noshade><em><a href=\"http://www.codeplex.com/\">CodePlex</a> powered by <a href=\"http://svnbridge.codeplex.com\">SvnBridge</a></em>\n" +
                "</body></html>";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test3()
        {
            stubs.Attach(provider.GetLatestVersion, 5793);
            FolderMetaData folder = CreateFolder("branch/test", 5775, "Wed, 24 Sep 2008 08:17:28 GMT");
            folder.Items.Add(CreateFolder("branch/test/A !@#$%^&()_-+={[}];',.~`"));
            folder.Items.Add(CreateItem("branch/test/H !@#$%^&()_-+={[}];',.~`.txt"));
            stubs.Attach(provider.GetItems, folder);
            string request =
                "GET /branch/test/ HTTP/1.1\r\n" +
                "Accept: image/gif, image/x-xbitmap, image/jpeg, image/pjpeg, application/x-ms-application, application/vnd.ms-xpsdocument, application/xaml+xml, application/x-ms-xbap, application/vnd.ms-excel, application/vnd.ms-powerpoint, application/msword, application/x-shockwave-flash, */*\r\n" +
                "Referer: http://localhost:8080/branch/\r\n" +
                "Accept-Language: en-us\r\n" +
                "UA-CPU: x86\r\n" +
                "Accept-Encoding: gzip, deflate\r\n" +
                "User-Agent: Mozilla/4.0 (compatible; MSIE 7.0; Windows NT 6.0; SLCC1; .NET CLR 2.0.50727; .NET CLR 3.0.30618; .NET CLR 3.5.30729; InfoPath.1)\r\n" +
                "Host: localhost:8080\r\n" +
                "Connection: Keep-Alive\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 200 OK\r\n" +
                "Date: Sun, 25 Jan 2009 12:30:20 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Last-Modified: Wed, 24 Sep 2008 08:17:28 GMT\r\n" +
                "ETag: W/\"5775//branch/test\"\r\n" +
                "Accept-Ranges: bytes\r\n" +
                "Content-Length: 521\r\n" +
                "Keep-Alive: timeout=15, max=100\r\n" +
                "Connection: Keep-Alive\r\n" +
                "Content-Type: text/html; charset=UTF-8\r\n" +
                "\r\n" +
                "<html><head><title>Revision 5793: /branch/test</title></head>\n" +
                "<body>\n" +
                " <h2>Revision 5793: /branch/test</h2>\n" +
                " <ul>\n" +
                "  <li><a href=\"../\">..</a></li>\n" +
                "  <li><a href=\"A%20!@%23$%25%5e&amp;()_-+=%7b%5b%7d%5d%3b',.~%60/\">A !@#$%^&amp;()_-+={[}];',.~`/</a></li>\n" +
                "  <li><a href=\"H%20!@%23$%25%5e&amp;()_-+=%7b%5b%7d%5d%3b',.~%60.txt\">H !@#$%^&amp;()_-+={[}];',.~`.txt</a></li>\n" +
                " </ul>\n" +
                " <hr noshade><em><a href=\"http://www.codeplex.com/\">CodePlex</a> powered by <a href=\"http://svnbridge.codeplex.com\">SvnBridge</a></em>\n" +
                "</body></html>";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        
        [Fact]
        public void Test4()
        {
            stubs.Attach(provider.GetLatestVersion, 5793);
            FolderMetaData folder = CreateFolder("branch/test", 5775, "Wed, 24 Sep 2008 08:17:28 GMT");
            folder.Items.Add(CreateFolder("branch/test/A !@#$%^&()_-+={[}];',.~`"));
            folder.Items.Add(CreateItem("branch/test/H !@#$%^&()_-+={[}];',.~`.txt"));
            stubs.Attach(provider.GetItems, folder);
            string request =
                "GET /branch HTTP/1.1\r\n" +
                "Accept: image/gif, image/x-xbitmap, image/jpeg, image/pjpeg, application/x-ms-application, application/vnd.ms-xpsdocument, application/xaml+xml, application/x-ms-xbap, application/vnd.ms-excel, application/vnd.ms-powerpoint, application/msword, application/x-shockwave-flash, */*\r\n" +
                "Accept-Language: en-us\r\n" +
                "UA-CPU: x86\r\n" +
                "Accept-Encoding: gzip, deflate\r\n" +
                "User-Agent: Mozilla/4.0 (compatible; MSIE 7.0; Windows NT 6.0; SLCC1; .NET CLR 2.0.50727; .NET CLR 3.0.30618; .NET CLR 3.5.30729; InfoPath.1)\r\n" +
                "Host: localhost:8080\r\n" +
                "Connection: Keep-Alive\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 301 Moved Permanently\r\n" +
                "Date: Sun, 25 Jan 2009 13:10:36 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Location: http://localhost:8080/branch/\r\n" +
                "Content-Length: 329\r\n" +
                "Keep-Alive: timeout=15, max=100\r\n" +
                "Connection: Keep-Alive\r\n" +
                "Content-Type: text/html; charset=iso-8859-1\r\n" +
                "\r\n" +
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>301 Moved Permanently</title>\n" +
                "</head><body>\n" +
                "<h1>Moved Permanently</h1>\n" +
                "<p>The document has moved <a href=\"http://localhost:8080/branch/\">here</a>.</p>\n" +
                "<hr>\n" +
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8080</address>\n" +
                "</body></html>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test5()
        {
            stubs.Attach(provider.GetLatestVersion, 5793);
            stubs.Attach(provider.GetItems, Attach.ReturnValue.Value(null));
            string request =
                "GET /nofile.txt HTTP/1.1\r\n" +
                "Accept: image/gif, image/x-xbitmap, image/jpeg, image/pjpeg, application/x-ms-application, application/vnd.ms-xpsdocument, application/xaml+xml, application/x-ms-xbap, application/vnd.ms-excel, application/vnd.ms-powerpoint, application/msword, application/x-shockwave-flash, */*\r\n" +
                "Accept-Language: en-us\r\n" +
                "UA-CPU: x86\r\n" +
                "Accept-Encoding: gzip, deflate\r\n" +
                "User-Agent: Mozilla/4.0 (compatible; MSIE 7.0; Windows NT 6.0; SLCC1; .NET CLR 2.0.50727; .NET CLR 3.0.30618; .NET CLR 3.5.30729; InfoPath.1)\r\n" +
                "Host: localhost:8080\r\n" +
                "Connection: Keep-Alive\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 404 Not Found\r\n" +
                "Date: Sun, 25 Jan 2009 13:23:51 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 300\r\n" +
                "Keep-Alive: timeout=15, max=100\r\n" +
                "Connection: Keep-Alive\r\n" +
                "Content-Type: text/html; charset=iso-8859-1\r\n" +
                "\r\n" +
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>404 Not Found</title>\n" +
                "</head><body>\n" +
                "<h1>Not Found</h1>\n" +
                "<p>The requested URL /nofile.txt was not found on this server.</p>\n" +
                "<hr>\n" +
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8080</address>\n" +
                "</body></html>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }
    }
}