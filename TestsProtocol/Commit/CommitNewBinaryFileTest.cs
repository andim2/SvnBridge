using System;
using CodePlex.TfsLibrary;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using Xunit;
using SvnBridge.SourceControl;
using Tests;
using Attach;

namespace ProtocolTests
{
    public class Tests : ProtocolTestsBase
    {
        [Fact]
        public void CommitNewBinaryFileTest()
        {
            stubs.Attach((MyMocks.ItemExists) provider.ItemExists, new NetworkAccessDeniedException());

            string request =
                "OPTIONS /Spikes/SvnFacade/trunk/New%20Folder%2010 HTTP/1.1\r\n" +
                "Host: localhost:8082\r\n" +
                "User-Agent: SVN/1.4.2 (r22196) neon/0.26.2\r\n" +
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
                "Date: Fri, 15 Jun 2007 23:02:11 GMT\r\n" +
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
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8082</address>\n" +
                "</body></html>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test10()
        {
            stubs.Attach(provider.ItemExists, false);

            string request =
                "PROPFIND /Spikes/SvnFacade/trunk/New%20Folder%2010/banner_top_project.jpg HTTP/1.1\r\n" +
                "Host: localhost:8082\r\n" +
                "User-Agent: SVN/1.4.2 (r22196) neon/0.26.2\r\n" +
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
                "HTTP/1.1 404 Not Found\r\n" +
                "Date: Fri, 15 Jun 2007 23:02:17 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 349\r\n" +
                "Content-Type: text/html; charset=iso-8859-1\r\n" +
                "\r\n" +
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>404 Not Found</title>\n" +
                "</head><body>\n" +
                "<h1>Not Found</h1>\n" +
                "<p>The requested URL /Spikes/SvnFacade/trunk/New Folder 10/banner_top_project.jpg was not found on this server.</p>\n" +
                "<hr>\n" +
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8082</address>\n" +
                "</body></html>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test11()
        {
            stubs.Attach(provider.GetItems, Return.Value(null));
            stubs.Attach(provider.WriteFile, true);

            string request =
                "PUT //!svn/wrk/208d5649-1590-0247-a7d6-831b1e447dbf/Spikes/SvnFacade/trunk/New%20Folder%2010/banner_top_project.jpg HTTP/1.1\r\n" +
                "Host: localhost:8082\r\n" +
                "User-Agent: SVN/1.4.2 (r22196) neon/0.26.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Type: application/vnd.svn-svndiff\r\n" +
                "X-SVN-Result-Fulltext-MD5: 9e93631358f04f1cc9ecd4241dab8275\r\n" +
                "Content-Length: 3997\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "SVN\0\0\0\u009Fp\u0081\u0011\u009E\0\u00BDI<\u0081DC\u0082DH\u0082DR\u0083EY\u008FNm\u0097EmG\u0081\u0011\u0081G\u0081\u0019V\u0081&\u009FI\u0081[\u008EL\u0081Y\u0082DN\u0082F\u0081\u007F\u0085F\u0081{\u00BAD\u0081\u007F\u0086F\u0081Z\u00A3E\u0082\u0017\u0080\u0082=D\u00853\u0082Q\u00859\u00B3F\u0085h\u00B8E\u0085i\u0093D\u0085\u007F\u0080\u0081KD\u00877\u009DD\u0088\"\u0080PD\u0087'\u008FG\u0088\u0006\u008DD\u0089;\u0082D\u0088V\u00BCF\u0089i\u00AAF\u008A0\u0091D\u0089j\u0080\\H\u008B\u0010\u0080@D\u008B'\u0080@K\u008C\u0004\u00A5E\u008Bm\u0080\u0092p\u00FF\u00D8\u00FF\u00E0\0\u0010JFIF\0\u0001\u0002\0\0d\0d\0\0\u00FF\u00EC\0\u0011Ducky\0\u0001\0\u0004\0\0\0P\0\0\u00FF\u00EE\0\u000EAdobe\0d\u00C0\0\0\0\u0001\u00FF\u00DB\0\u0084\0\u0002\u0003\u0004\u0003\u0005\u0004\u0005\u0006\u0005\u0006\u0006\u0007\u0007\u0008\u0007\u0007\u0006\u0009\u0009\n" +
                "\n" +
                "\u0009\u0009\u000C\u0001\u0003\u0003\u0003\u0005\u0004\u0005\u0009\u0006\u0006\u0009\r\u000B\u0009\u000B\r\u000F\u000E\u000E\u000E\u000E\u000F\u000F\u000C\u00FF\u00C0\0\u0011\u0008\02\u0001\u008D\u0003\u0001\u0011\0\u0002\u0011\u0001\u0003\u0011\u0001\u00FF\u00C4\0\u00AE\0\0\u0002\u0002\u0003\u0001\u0001\0\u0003\u0004\u0001\u0002\0\u0005\u0007\u0006\u0008\u0001\0\u0003\u0001\u0001\0\u0001\u0006\u0010\u0007\u0005\n" +
                "\u000B\u0009\0\u0002\u0011\u0003\u0004!1A\u0005Q\u0012\u00D3\u0006\u0007aq\u0081\"2\u0013\u00A4\u0091B\u00D23\u00A3\u00A1\u00C1b\u0082#\u0014TE\u0016V\u00F0R\u0092\u00A2C\u0083t\u00845UF\u00D1\u00E1rs4\u0015&6\u0017\u0011\u0001\u0005\u0008\u0001\u0004\u0002\u0003\u0001\u0011\u0002\u00A1\u00D1\u0003QRS\u0004\u0014!1\u00B1\u0012\u00A2\u0005\u0015\u0016\u00D2AB\u0013\u0006aq\"23C\u00FF\u00DA\0\u000C\u0003\u0011\0?\0\u00F4n\u00E1n\u00E2\u000F\u00A8\u00BCUV\u00D5|O\u00CA\u00F3;\u00D8FO+\u00A9\u00D4\u00DB\u00E0\u0003\u00B8e\u00B8r\u00DA\u00E7\u00CC\u00C9\u0003X\u00C0\\\u00F7\u009A\u00AA\u00A8\0-$\u00FC\u00AAs\u00EE\u00BC\u00CC~\u00EC#!\u00D4\u00EAm\u00F0xO\u00B2\u001B\u00A5X\u00E9\u00D3h\u00F2c.K\u00A6\u0096SK\u0013\u00EA\u001C` \u0005\u00F3\u0009\u0089\u00BDx\u00FA\u00DF\u00D89\u00DF=-\u00BF\u00B3\u00FD[\u0093\u009A\u00EE{Z\u00BD\u0093\u0084:\u00A5/\u00067\u000E\u009B.\u00A79\u009EW2f`Y\u00ADR[U=\u00A1\u00AE6\u00EA\u0080\u001F\u000E\u00AD\u00CB\u00D4\u00D3\u00F7Nj-\u008F5\u00FD\u00BF\u00EA\u00DC\u009D\u0011\u00CC\u00EAS\u00B6|\u001Ay\u00BC2\u00E1\u00CB\u000C\u0006Y4~\u00F5;\u00D3]\u001F'\u00AF\u00BF\u0084d\u00D3\u00A8\u00BFo\u0080m\u00E1\u00A7\u000EI\u00FE\u00197\u009B\u00E7S\u00BD4\u00FEO_\u007F\u0008\u00C8\u00FA\u008B\u00F6\u00F86\u00128O\u00C3\u0099\u00F6\u000C\u00B2p\u0088\u00B0\u008A\u00A9\u00D6~r\u00A8\u00F7\u001Dy\u00FD\u00F8FG\u00FC\u00F7\u00ED\u00F0q\u009C\u00E7r\u00B2\u00EC\u009B2\u00AA\u00CB\u00A7\u00D1\u00C5\u00D4\u00EF\u00EAL\u00D7\u0098\u0003\u00D8mc\u0087[\u0010\u00B85=\u00DB\u009B\u00B2\u00E9\u0089\u00BF\u0008\u00C9\u008C\u00F3:\u00B1=\u00F8CW\u00F6{'\u00FA'\u00B4\u0099\u00E9(\u00F9\u009Eo\u007F\u0008\u00C8\u00BA\u00ADM\u00BE\u000C\u00FB?\u0093\u00FD\u000F\u00DAL\u00F4\u0091\u00F3<\u00DE\u00FE\u0016\u00E4:\u001B\u00BF\u000B\u00FD\u009E\u00C9\u00BE\u0087\u00ED&zI|\u00CF7\u00BF\u0084d]V\u00AE\u00DF\u0004\u001D\u00DE\u00C9\u00FE\u0087\u00CD\u00F2\u0093=$\u00FEg\u009B\u00DF\u00C2\u00DC\u0087U\u00AB\u00B7\u00C0#\u0090e\u001FD\u00F6\u0093=$}V\u00AE\u00DF\0\u00CEC\u0094\u008B\u00A9=\u00A4\u00CFI?\u0099\u00E6\u00F7\u00F0\u008C\u008F\u00AA\u00D4\u00DB\u00E0\u00A1\u00C8\u00B2\u00BC)\u007F=\u00FE\u0092>c\u009B\u00DF\u00C22\u001DV\u00A6\u00DF\u0005\u009B\u0090\u00E5f\u00FA_\u00CF\u007F\u00A4\u0094\u00FBS\u00CDjm\u00F09/v\u00F2\u0087B4~\u00D2g\u00A4\u00A6}\u00EF\u0091<\u00E6\u00AE\u00DF\u0006\u00FE\u0087r\u00B2*\u0096\u00B6d\u00CA\u0002\u0003ng{7\u00E59G]ew\u00BE\u00F3\u007F\u00A5\u00F8[\u0093;\u00B9\u00ED_\u00D2p\u0086\u00C8n>\u00ECc\u0096{y\u00DBE\u0097\u00CFs\u00BB\u00F8[\u0093>\u00BF[{\u0008\u00C9q\u00B8\u00FB\u00AD\u00FC\u00AF\u00DB\u00CF\u00DA#\u00E7\u00B9\u00DE&\u0016\u00E4]~\u00BE\u00F6\u0011\u0092]\u00B9\u001B\u00A6\u00C6\u0097;+\u0080h\u0089=\u00FC\u00FD\u00A2_=\u00CFq0\u00B7!\u00D7\u00EB\u00EFa\u0019\u0011\u00FB\u001D\u00BB.tFY\u00AA\u00D3s{\u00E9\u00DBE\u007F;\u00CE\u00F10\u00B7%u\u00DA\u00DB\u00D8FI\u001B\u0097\u00BB_\u00CB}\u00B4\u00EFM/\u009E\u00E7w\u00F0\u00B7#\u00EB\u00B5\u00B7\u00B0\u008C\u0097n\u00E5n\u00C9\u00FA\u00B2?\u00AE\u009D\u00E9\u00A2}\u00FB\u009D\u00E2anG\u00D7koa\u0019\u0019f\u00E2\u00EE\u00C3\u0088\u0003+$\u00FF\0\u009D?h\u00A7\u00E7\u00F9:\u00EDm\u00EC##\u00F2\u00F8}\u00BA\u007F\u00A4\u00CA\u00A2tw\u00F3\u00F6\u008A'\u00FB\u0007=\u00C4\u00C2\u00DC\u0093\u00D7\u00EBg\u000Fw;\u00CE\u00C9\u00E3\u00FB\u00C5F\u00D5D\u00FF\0`\u00E7\u00F8\u009E\u009Br.\u00BB[{\u0008\u00C8Q\u00C3\u00DD\u00C9wg(\u0089\u00D1\u00F3\u008A\u008D\u00AA\u008F\u00B1{\u0084w\u00EAzm\u00C8u\u00FA\u00F1\u00FB\u00B0\u008C\u0095<;\u00DC\u00F8\u00FF\0\u0005\u00D5\u00FD\u00E2\u00A7j\u00AA?\u00B1s\u00D3\u00FF\0\u00D3\u00D3nJ\u008E{#\u0087{\u009B\u00FC\u009F\u00C4TmQ\u00F6\u001E\u007F\u0089\u00E9\u0084\u001C:\u00DC\u00C3\u00F57\u0088\u00A8\u00DA\u00A5\u00F6.!\u00D6\n" +
                "\u00DE\u001B\u00EEQ\u00BF%\u00F15;T}\u008B\u009F\u00E2zm\u00C8\u00FA\u00DDm\u00EC#!\u009B\u00C3M\u00C97\u00E4\u009E&\u00A7j\u0097\u00D8\u00B9\u00FE'\u00A6\u00DC\u008F\u00AD\u00D5\u00DB\u0084d;xc\u00B8\u00C6\u00FC\u008F\u00C4\u00D4\u00EDT\u00FD\u008F\u00CD]\u00B8@\u00CD\u00E1v\u00E2\u009F\u00A8\u00FCUV\u00D5/\u00B2s\u00FCOM\u00B9\u000E\u00B3W{\u0008\u00C8v\u00F0\u00AFp\u00C8\u00B7\"\u00F1U[d\u00BE\u00C9\u00EE\u001Cn\u0011\u0090\u00CD\u00E1N\u00E0\u009B\u00F2\u001F\u0015U\u00B6S\u00F6_p\u00BA\u00DD]\u00B8FC7\u0084\u00DC?7\u00E4\u001E*\u00AFl\u0097\u00D9\u00BD\u00C3\u0089\u00E9\u00B7\u00F1>\u00B3Wn\u00103xI\u00C3\u00D3\u00FD?\u00E2\u00EA\u00F6\u00CA~\u00CD\u00EE<OM\u00BF\u0088\u00EB5v\u00E1\u0002\u008E\u0010\u00F0\u00F0\u00C3\u00FE=\u00E2\u00EA\u00F6\u00C9}\u009F\u00DCx\u00BE\u009B\u007F\u0011\u00D6j\u00ED\u00C2\u0005\u001C\u001F\u00E1\u00D1\u00FE\u009D\u00F1u{d\u00BE\u00CF\u00EE<_\u000E\u000Fp\u00E7\u00EE\u00EF\u008B\u00AC\u00DB%\u00F6\u008Fq\u00E2\u00FAm\u00FCO\u00AC\u00D5\u00DB\u0084\u000C\u00DE\r\u00F0\u00DC\u00DF\u00BB\u009E2\u00B3n\u00A6\u007F\u00B4{\u008F\u0017\u00D3o\u00E2}f\u00AE\u00DC C\u00C1\u00BE\u001A\u0001\u0013\u00BB\u0090\u001F\u00B6Vm\u00D4\u00FD\u00A7\u00DC\u00B8\u00D5\u00EA\u00ED\u00C2\u0003<\u001D\u00E1\u00B1\u00EC\u00EE\u00DC\u0007\u00ED\u0095\u009Bu_h\u00F7./\u00A6\u00DF\u00C4\u00FA\u00BD]\u00B8B\u00E3\u0083\\6\u00FB\u00B9\u00E3+6\u00E8\u00FBO\u00B9q}6\u00FE#\u00AB\u00D5\u00DB\u0084\u000881\u00C3S\u00FD7\u00E3+6\u00E9}\u0004\u001C\u0016\u00E1\u00A7\u00DD\u00AF\u0019Y\u00B7K\u00ED>\u00E5\u00C5\u00F4\u00DB\u00F8\u008E\u00AFWn\u0010\u00BF\u00FE+\u00C3?\u00BB^2\u00B7n\u008F\u00B4\u00FB\u0097^\u00AE\u00DF\u0006\u00C5\u00ED\u0016\u00D9\u0015\u00EE\u00C4\u00B2\u0087?\u00DFL\u00CCS\u00C8nY\"\u00C9\u00B5CZ\u00A4\u0082b\u00D9x\u000F\u008C}\u00C5\u00873\u00ABH\u00F2\u00C2/\u00BB\u00F45\u00C3|\u009F\u00BF\u00EFsZ\u00A9q\u00A5\u00CB\u00E6\u00FC\u00807:q\0\u008F\u00C9\u00BF\u009E\u000B\u0093CJ\u00B7\u00F9\u00E7\u00BA<Y\u00D9mf\u00AFO\u009E\u00E7:\u009D\u00E8\u0002\u00F0G72\u00EB\u00BFU\u00AC\u00DC\u00E5U\u0019\u00EB\u00C3\u00DC/$\u009B\u0015\u00C6\u00A2\u00A2\u00E2\u00ED\u00CF&k\rk\u0001\u00B8\\\u009F\u00F2\u001D^\u00A7'\u00CE\u00DC\\\u00DDn\u00B0[Y\u00A8\u00A8\u0091\u00F7\u00D7,\u0019\u00CEV\u00CC\u00DA\u0099\u0080\u00D6e\u008D=\u00F3[{\u00E4^\u007F\"\u00FEh\u00AA\u00E6,\u00F3\u00DB\u00E6\u008E\u00F8;\u00E2\u00B1W\u0017+\u00CFb\u00C2\u0080\u00CE\u009B\u0090\u0012\u000F\"\u0002A\u00111\u001A\u00C0`\u0080\u001B\u0088\u00B6\r\"=)\u0098E0\u00C0\u00D8\u00A52S&e\u00CB\u00BB\u0095D\u00CAf[\u00DC\u00BE\u0088Nwx\u00E1\u00F2L\u00C0\u00E2t,\u00AF\u00BA\u008Cn\u00B9\u00EA\u0080f\u00A8\r\u0096\u0018F \u0095\u0084\u00CB\u0019\u0091\u00E1\u00DE]\u00EB1\u001F\u008D\u00CA9R\u00EF \u00AE\u00F7\u00D2&\u00BA|\u00F19\u00FA\u00AD\u008Ft\u00D3e\u00B7\u0095Q\u0014\\B\u00A0\u0088\u00E8\u0082f& \u000Fq#;&I0.\u001A\u00A3F*fJ\u00AD\u0083L\u00B9B\u0010\r\u00F7J\u0089\u000B\u0089\u00ED\u0083\u00A0\u00D3\u001F4\u00A5@\u0080\u00F7:\u00F3\u00D0\u0090\u001D\u00AE\u0082\u009A\u0003,\u009D\u0083\u00BA\u00C1D\u00D8(;u]\u00D96\u00E8*k1\u00DE}\u00C2\u0081\u000B\u00ECN\u00B1'\u0002\u00B5\u0006e\u0085I\u0099aS&e\u00A5H2\u00CB\u0094\u0081\u00DA\u00A4\u0019b@\u00C3T\u00C8\u001D\u00AAd\u00C6\u0009\u0001AH\u00D8g\u0086\u00F6m>\u00E2T4k9\u00C6$\u00C5*\u0018\u00CDH\u000E\u00D4\u0080\u00CDH\u00C5\u0009\u0001\u00127&\u009F\u00BC9D\u00A9s\u001E3\u0009S\u000B\u001A\\%\u00B1\u00DDg\u0010.\u001C\u00A5}\u00C7\u00F3\u00D9N\u00F3\u00F3\u00C3\u0090=\u00F5\u00D9\u00D6j\u0005\u00B3*\u00F3\u0009\u00C1\u00B2\u00E5\u0083tn\u001C\u0081\u00A3\u001D\u000B\u008E\"\u00ED[\u00BB;\u00E5\u0094D\u00DD.\u00F9G.^Q\u0094\u008C\u00B6\u009C\u00F5)\u0083b\u00EC\\\u00E2\u000E\u00B3\u008F)+\u00BF^\u00C8\u00D2\u00B6-\u0086\u00D7\u00DB\u00E5\u008AC\u0096g\u00B5S\u009D2`\u00B4\u008Bm\u0082\u00E0\u00BA\u00E9\u00962\u00E6Sf=\u00D3`/1\u0082\u00DE\u00D9\u00ECk\u001D\u00C1w\u008Ek\u00B4\u009F\"f\u00DDe\u00B5\u00AF\u0096\u00F6\u009DhZ\u00B4\u00B6\u00E3\u0089u\u00CC\u0082\u00BF\u00BDhc\u00E0\u00F6\u00B8A\u00C1\u00D6\u0082\r\u0084\u0010\u00BB\u00B4\u00AEkl\u00B9f\u00F6\u00E4g#\u00CDfK\u0096\u00D2(\u00AA\u0087}B\u00EC5\u0009\u00B5\u00B1\u00D2\u00D3g4\u0017\u0016\u00B6\u009F\u0092\u00EF\u00F0\u00CA\u00EBi/1\u00D1\u00E5Y%\u00908\u0003\u00CAR\u000C\u00B5\u0001a\u0011q@U\u00DA\u00D0 \u0098\u00E2\u0080\u0010\u0004\u00E0\u0099\u0099d\u00BB\u00ACQ2\u0089\u0096\u00C6\u0096\u0099\u00D3\u00A65\u008D\u001C\u00AEv\u0080\u00B3\u00BAh\u00CA\u00E9z\u00D9rD\u00B6\u00B5\u0081\u00BA\u00A1\u00A2\0,.\u00963&\u00E0\u00F0\u0006\u00B0\u00B1L\u00A6Yu\u00BA\u0012\"\u0095S\u009D2,h-p\u00F5\u00AE\u0085\u00E1\\B\u00A2\u0008\u00C4\u008D\u001C\u00D0MB\0\u00EDa\u00ADgBF}\u009A\u00AC\0\u0086\u0097;\u00F1\u008A\u0099\u00ED\u0002\u0097\u00CD<\u00DC\u009F\u00DA\u0095\n" +
                "\u008B4EH0#bR\u0012\n" +
                "T8[]\u0014T@\u008Dx\u0088J\u0087C,\u0099\u00A3\u00CA\u00A6`P\u00E3&\u0091\r`H\u00C1g6\u0015\r\u00B0\u00B5\u00C4\u0001y\u00C1G\u00FC\u00A0RL\u0086\u0091d.K\u00CD\u0002$v\u0084\u0019\u0096\u00A93,6\u0015 \u00C3\n" +
                "\u0099\u0006\u0018\u0094\u0083\r\u008A\u0099\u0003\0R6\u0019\u00AD`\u008B\u008D\u00BA\u0012\u00A1\u00C0fs\u009F`\u00B1\u00A7\u0004\u00A8\u00AA\u0008\u00C4\u0081\u0096\u00952\u000C4\u00A4\u0007\u0005#\u0019\u00B7\u00A4\u0005\u0017$b\u00A4\u001F\u0016\u00CE\u00A9~\u00B6\u00AB\u0001q\u00C0\u00E0\u00BE\u00AA\u00CD=\u00AC\u00AD\u00B6\u00AFo\u00C3y\u00F4\u00CC\u00CF'\u008A\u00D1\n" +
                "\u00C9\u00D2\u001C\u00DC\u00B6c\u0088\u00D5\u0006\u00F7\u00B4|\"\u00DB\u00B9\"1^\u008F%6E\u00D4\u00FD]:T\u0089t\u00CA\u00C7\u00BE5L&\u00D2\u001AG\u0095>z&\u00905\u009C\u00AB73[5\u00F1$D\u0095\u00E5U\u00CA\u00F0\u0013_0Lx\u00D6\u00C4\u00AE\u00BBg\u00B1\u00B4w\u0007\u00AC\u00E3y\u008E\u0011Tgi\u0001\u00D7o:\u00A87S\u00DD\u00BE\u00F0\u0016@\u00C0Yb\u00EB\u00D2ikg\u00C4j\u008A\u0016\u00E44r*:\u00D9\u008C\u00C9\u00C1\u00D4\u0010\u0084Z\u00D0>P\u009F\u0082D\u00044\u00C3B\u00BEjc\u00C9\u0011=\u00E7\u00A9=\u008E\u001AN$\u00AF=\u008B\u0001\u00BF\u00AD\u000EOy\u0001\u009A\u00CE\u00D2\u0080\u00B5\u00B6u\u0087*\u0002\u00D7\u0008k\u00B7\u009D\u0001\ry\u0017\u00BA\u0009II\u00C93\u000B\u00ECk\u00A2p\n" +
                "'\u00B1\u0017v=e\u001C\u0093&P\u0007\u00D6:\u00D7\u009E]\u000B\u009A\u00EB\u00AB,.\u00B9\u00B0\u001A\u00D0\u0004\u00C6\u0006\u00E2\u00A2Y\u00CC\u0089\u00ACF%\"\u00A9:\u008A\u008E\u00EF\u00AA\u00DE\u00D9\u00F79S\u00888\u0082\0\u00BD\u00CE\u00B0\u00978\u00E2\u00ADB\u00BB\u00BB`\0\u009Di\u0098\u00C2\u00E4\u00A0\u00D0\u00D2I\u0008\u0090e\u00A0\u0095 \u00C3F\u0094\u00A4\u000C.H\u000C\u00D21\u008CpJB\u00F1\u008D\u0097\u0001\u00A5%+gJ\u0015\u000B7\u0004\u008C\u00CB\u001Caz@PI\u00C5I\u009A\u0097\u0010A\u008D\u00A1L\u0086\u00D2\\\u00E2!\u0013\u001Eu\u0095\u00D6\u00D548\u00D9\u0080\u008B\u00D6s\u0013\u0002\u00928q\u00D2\u0097\u0098\u00C4k\u008Au\u00A8\u001D\u00AE\u0082GC-\u009B\u00054\u0014_\u00E7,m\u00EE\u0011SC\u00A2\ra\"\u000C\u00F2\u0094P\u00E8\u00A3\\I\u0089$\u0092\u0091\u0099a\u00B9L\u0099\u00A6$\u000C5L\u0083\rS \u00C3R\u0090;R\u0002\u008B\u0092\u0002\u00A4o\u008BK\u00A1\u0012\u001A\u00D2y\u0097\u00D6\u00B1\u0082\u008C\u00A9\u00A8\u00A6\u00A9\u0095S-\u00E6T\u00F9\u000Fl\u00C93\u001B\u0083\u009Ab\u0008Z\u00D9\u00D9\u00DB\r\u00AD\u00FF\0\u000F\u00A22\u008A\u00F99\u00FE_O\u0099Jk5\u00E6\u00B0\u00B6\u00AAU\u00FA\u0093Y\rf\u00FB\u00E3\u0091w\u00EA]\u001A\u009Aumt\u00D6\u00DA\u00BCf\u00F1S\u0002\u00F3\u00AA\u00C8@\u009BW\u0093u\u0094r\u00CC9]d\u00A2\u00C9\u00CF\u0010\u00C5m\u00A7\u001D\u008D-\u008E\u00C0\u001B)\u00C6\u00F0\u00AE\u008Aor\u00EAB\u00F7\u00B6\u00C8\u00DA\u00B4\u00B2\u00DA\u009CC\u00AFdt\u008C\u00A6\u0092g\u00CE\u00D5\u0097*KK\u00E6Lu\u0081\u00ADh\u0089$\u00F2\u0005\u00DD\u00A7m\"\u00ADa\u00C7\u00B7\u0093:\u0099\u009E\u00E6\u00B3\u00EB\u000CD\u0086\u00FC\u009D\u0014\u00A3\u00E6\u00CAi\u00EA\u00F4\u009B\u00CF)\\:\u00BA\u009E{\u00AA\u00CA\u00EB\u00AB-\r\u00BA\u0016iE\u00BC\u00A8\u0008\u00B7B\u0002\u000B\u0088\u00F7\u0090l\u0004\u009B\u0010\u0017l\u00A78\u00A52Ss\u007F\u0093\u00D1\u00BC\u00CE\u00EFKC\u0099+\u0002,\u008A\u00C7R\u00FE\u00C6:\u0097\u00BDp\0\u00F9\u00A1sU\u00CDQ5\\\u00EB!\u00AB\u000B\u00A3b\u0002\u008F\u00EA\u00B4\u00BB\u00B4F\u0001\u0014*4\u00E6.s\u009C\u00E3\u0002OX\u0095k\u00A2\u000C\u00C8uY\u00D5\u0006\u00F7bQCU\u00B6\u0094\u0019\u0096\u000B@)I\u001BcM\u008A&H`\u00D2\u0012\u00A8\u0014:\r\u0086\u00A8\u008E\u0098$l\u00D7v\r\u0008S\u001A\u00F7\u0012C\u0080\u0003E\u00C8\u00A1\u00D0H\u0013p\u008F H\u00D7h6D\u0018$b\u0088\u0081\u00CC\u0090]\u00B3\u0012\u00A2\u00A8f[\u00A3`S \u00FC\u00B1\u00F0\u0094\u00C9\u001B\u0006\r\u0088\u00C1A\u00B0VKe\u00EF\u00B7@\u00B5)\u00B6\u00A7\u00E5\u0010f\u0003\u00CD\u0096O=\u008A'LyV\u0015\u0093\u009Dt\u001B\u00CC\u0012\u00F2\u00D0\u00E8\u00B8\u0098\u00F7v\u0089<\u0085\u0006;\u0009*d\u001A\u0097\u0082\u0099\u0007\u0018\u0014\u00C86\u00C1r\u0099\u0006\u0098\u0014\u00C82\u00C0\u00900\u00D0\u00A4\u0018hR\u0007j@P\u0080\u00BAF\u00F8\u00B4\u0085\u00F5\u00AC\u0001\u0099,<@\u00F4\u0014\u00E2h\u00A8\u009A=\u009F\u000Fs\u009F\u00F6\u009C\u00E9\u0094\u0015O\u0085\u000Eh\u00E1)\u00C4\u00DC\u00C9\u00A6\u00C6?\u00A60?\u00DC\u00BA\u00B4\u00B5)X\u009E\u00E9om\u00CE\u00B5\u009Ed\u00E5\u00E5\u00C46\u00F4_\u00A4Sk\u0096\u00E6\u0019\u0003\u00FB\u00E2u/\u00E4OOO\u00B0\u00ED\u00B4\u0094\u00BC\u0085\u00FA\u00C3\u00A8\u00B4\u008D%\u00F9^\u00AF*\u00C8\u00CB\\\u00D8\u00B5mf\u009A\u00A2\u000B\u00EF\u00E6l\u00DC\u00BE\u0086VCJ\u00F8O\u00ABh\u0099\\E\u00ED\u0095\u00E6\u00B7\u00E3\u0011\u0013\u00C89R\u00E6o\u00F2\u00C7\u0096\u000BRi\u0014q\u00F5\u00C0\u00C9\u0009\u0085J\r\u0008\u000C(\u000B0F\u00C8\u00A2Jd\u00F4\u00A6\u00F2,\u00E6Y\u00CC\u00BD\u0085\u0013>o)\u008D\u0016:\u0011w9\u00BDs]5\u0097=\u00D2|\u0012Vl\u00D2\u00E7\u00EA\u0082\u00E7\u001B\0\u00B4\u0092\u0080\u00D7M\u009E\u00E9\u00A0\u0080Hn\u0001\\E\u0015\u001D\u0085Ca\u0012|\u008A\u0095Ub\"\u0010t\u0011\u00A7\u00AC4\u00A4(q\u00AD\u000E \u00C6\n" +
                "e'\u0018\u00D2\u0016d`k\u0001m\u0081\u0001\u0005\u00CD\u0008T*&1\u00A4\u009D7\u00A2aTT\u00CEe\u00F0\u00B5:\u001CB\u0086~\u008B\u0011E\u00C5\u00A9\u0015\u0006\u00CBQEPARai\0r\u00A9\u00F2\u008F*\u00BF<\u0094\u00DF?X\u00E8\u0016\u00A2\u008A\u00A2\u00ED\u00CC z\u008D=%L\u00C1\u00D0v\u00D7\u00CFu\u00C7W\u0099L\u00C1yE\u0013\u00A6\u00CC\u00ED<\u009Er\u0095\u000E\u0086e\u00C5D\u0083\u0092\u00F0S \u00E3\u000E\u0085\u0012F\u00A5\u0083b\u0099\u0007e\u00B4\u00D8\u00A2A\u00D9mS$q\u008DS2U6\u00C6\u00A9\u00A8\u00A9\u00B6\u000553\r\u0009\u0003\r\n" +
                "@\u00CD\u0017$\u0006j@F\u00A2M|\u00127\u00C5\u00DF\u0085\u00AB\u00EBX \u00E0\u0080\u0086z\u00D9?\u00E3\u001C\u00F7\u00A2{\u00A5P\u00FA\u00D2g\u00FA*[\u00FDC=\u007F\u00AC\u00EC\u008E\u00DF\u00C2\u00D3\u00CA\u00BD8\u00EE\u0087L<e\u007F\u00AC\u00FD\u0017J\u00D3Mv\u0092\u0097\u00FA\u0095\u00AA\u009B\u00FA\u001B\u00C7\u00AB\u00E8Wi\u00BE\u007F\u00DF?\u00FBFq\u00EB}p\u00F5\u00B7\u00F6[w\u00C1\u00FC^H/7\u0098\u00FF\0\u00D2X_\u00FF\0iy\u008D>\u00FA\u00C1(7&\u0015(4 \u0098oL\u00C4\u0096\u00A6SsgM\u00EBe\u00DD\u00DAm\u00F7^\u00B3\u00B9\u0095\u00DD\u00CFa/\u00A1r\u00CB\u00090\u00DB\u0094\u00CA\0\u00AB\u00F5G\u00A1;N\u0008\u00FE\u0016*0\u00DD\u00D2\u009C*\0\u0018\u00AAT\u0008\u00CB\u00C5\u00E9H\u0096\u00C2V\u000B;\u0091-\u0094\u00BE\u00D6\n" +
                "\u0013\u000B>\u00ECQ\u0007\u0005_\u00D2\u00A9\u00A4\u0002\u00EE\u0094\u00E1P\u0019\u00C6\u00F4\u00D7\u0008\u00F2\u00A4\u00A6;\u00B3\u00E7|[\u00D0p\u00D7\u00BE\u00F3\u00DB\u00F8\u00E8P\u0092\u00EE\u0009I\u009C\u0097\u0082\u0089#\u00B2\u00F0Q \u00F4\u00BC\u0014\u00C9\u001Ef\n" +
                "$\u001Ce\u00C1L\u0091\u00C9x\\\u00A2A\u00E9]\n" +
                "$\u008E\u00B1L\u0083\u00B2\u00D4I\u001Cb\u0099\u0006\u009B\u0082\u0089#MH\u00C7j\u0090;pH\u000C\u00D4\u0018\u00A1 (\u00B8$k`\u0083\u007F\u00FF\u00D9";

            string expected =
                "HTTP/1.1 201 Created\r\n" +
                "Date: Fri, 15 Jun 2007 23:02:17 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Location: http://localhost:8082//!svn/wrk/208d5649-1590-0247-a7d6-831b1e447dbf/Spikes/SvnFacade/trunk/New Folder 10/banner_top_project.jpg\r\n" +
                "Content-Length: 373\r\n" +
                "Content-Type: text/html\r\n" +
                "\r\n" +
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>201 Created</title>\n" +
                "</head><body>\n" +
                "<h1>Created</h1>\n" +
                "<p>Resource //!svn/wrk/208d5649-1590-0247-a7d6-831b1e447dbf/Spikes/SvnFacade/trunk/New Folder 10/banner_top_project.jpg has been created.</p>\n" +
                "<hr />\n" +
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8082</address>\n" +
                "</body></html>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test12()
        {
            stubs.Attach(provider.SetProperty);

            string request =
                "PROPPATCH //!svn/wrk/208d5649-1590-0247-a7d6-831b1e447dbf/Spikes/SvnFacade/trunk/New%20Folder%2010/banner_top_project.jpg HTTP/1.1\r\n" +
                "Host: localhost:8082\r\n" +
                "User-Agent: SVN/1.4.2 (r22196) neon/0.26.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Length: 327\r\n" +
                "Content-Type: text/xml; charset=UTF-8\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\" ?><D:propertyupdate xmlns:D=\"DAV:\" xmlns:V=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:C=\"http://subversion.tigris.org/xmlns/custom/\" xmlns:S=\"http://subversion.tigris.org/xmlns/svn/\"><D:set><D:prop><S:mime-type>application/octet-stream</S:mime-type></D:prop></D:set></D:propertyupdate>";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Fri, 15 Jun 2007 23:02:17 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 520\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns3=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:ns2=\"http://subversion.tigris.org/xmlns/custom/\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/svn/\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response>\n" +
                "<D:href>//!svn/wrk/208d5649-1590-0247-a7d6-831b1e447dbf/Spikes/SvnFacade/trunk/New%20Folder%2010/banner_top_project.jpg</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<ns1:mime-type/>\r\n" +
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
            MergeActivityResponse mergeResponse =
                new MergeActivityResponse(5477, DateTime.Parse("2007-06-15T23:02:18.152000Z"), "jwanagel");
            mergeResponse.Items.Add(
                new MergeActivityResponseItem(ItemType.File,
                                              "/Spikes/SvnFacade/trunk/New Folder 10/banner_top_project.jpg"));
            mergeResponse.Items.Add(
                new MergeActivityResponseItem(ItemType.Folder, "/Spikes/SvnFacade/trunk/New Folder 10"));
            stubs.Attach(provider.MergeActivity, mergeResponse);

            string request =
                "MERGE /Spikes/SvnFacade/trunk/New%20Folder%2010 HTTP/1.1\r\n" +
                "Host: localhost:8082\r\n" +
                "User-Agent: SVN/1.4.2 (r22196) neon/0.26.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Length: 297\r\n" +
                "Content-Type: text/xml\r\n" +
                "X-SVN-Options:  release-locks\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Accept-Encoding: gzip\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:merge xmlns:D=\"DAV:\"><D:source><D:href>/!svn/act/208d5649-1590-0247-a7d6-831b1e447dbf</D:href></D:source><D:no-auto-merge/><D:no-checkout/><D:prop><D:checked-in/><D:version-name/><D:resourcetype/><D:creationdate/><D:creator-displayname/></D:prop></D:merge>";

            string expected =
                "HTTP/1.1 200 OK\r\n" +
                "Date: Fri, 15 Jun 2007 23:02:17 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Cache-Control: no-cache\r\n" +
                "Transfer-Encoding: chunked\r\n" +
                "Content-Type: text/xml\r\n" +
                "\r\n" +
                "466\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:merge-response xmlns:D=\"DAV:\">\n" +
                "<D:updated-set>\n" +
                "<D:response>\n" +
                "<D:href>/!svn/vcc/default</D:href>\n" +
                "<D:propstat><D:prop>\n" +
                "<D:resourcetype><D:baseline/></D:resourcetype>\n" +
                "\n" +
                "<D:version-name>5477</D:version-name>\n" +
                "<D:creationdate>2007-06-15T23:02:18.152000Z</D:creationdate>\n" +
                "<D:creator-displayname>jwanagel</D:creator-displayname>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "<D:response>\n" +
                "<D:href>/Spikes/SvnFacade/trunk/New%20Folder%2010/banner_top_project.jpg</D:href>\n" +
                "<D:propstat><D:prop>\n" +
                "<D:resourcetype/>\n" +
                "<D:checked-in><D:href>/!svn/ver/5477/Spikes/SvnFacade/trunk/New%20Folder%2010/banner_top_project.jpg</D:href></D:checked-in>\n" +
                "</D:prop>\n" +
                "<D:status>HTTP/1.1 200 OK</D:status>\n" +
                "</D:propstat>\n" +
                "</D:response>\n" +
                "<D:response>\n" +
                "<D:href>/Spikes/SvnFacade/trunk/New%20Folder%2010</D:href>\n" +
                "<D:propstat><D:prop>\n" +
                "<D:resourcetype><D:collection/></D:resourcetype>\n" +
                "<D:checked-in><D:href>/!svn/ver/5477/Spikes/SvnFacade/trunk/New%20Folder%2010</D:href></D:checked-in>\n" +
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
                "DELETE /!svn/act/208d5649-1590-0247-a7d6-831b1e447dbf HTTP/1.1\r\n" +
                "Host: localhost:8082\r\n" +
                "User-Agent: SVN/1.4.2 (r22196) neon/0.26.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 204 No Content\r\n" +
                "Date: Fri, 15 Jun 2007 23:02:18 GMT\r\n" +
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
                "OPTIONS /Spikes/SvnFacade/trunk/New%20Folder%2010 HTTP/1.1\r\n" +
                "Host: localhost:8082\r\n" +
                "User-Agent: SVN/1.4.2 (r22196) neon/0.26.2\r\n" +
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
                "Date: Fri, 15 Jun 2007 23:02:15 GMT\r\n" +
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
                "MKACTIVITY /!svn/act/208d5649-1590-0247-a7d6-831b1e447dbf HTTP/1.1\r\n" +
                "Host: localhost:8082\r\n" +
                "User-Agent: SVN/1.4.2 (r22196) neon/0.26.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n";

            string expected =
                "HTTP/1.1 201 Created\r\n" +
                "Date: Fri, 15 Jun 2007 23:02:15 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Cache-Control: no-cache\r\n" +
                "Location: http://localhost:8082/!svn/act/208d5649-1590-0247-a7d6-831b1e447dbf\r\n" +
                "Content-Length: 312\r\n" +
                "Content-Type: text/html\r\n" +
                "X-Pad: avoid browser bug\r\n" +
                "\r\n" +
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>201 Created</title>\n" +
                "</head><body>\n" +
                "<h1>Created</h1>\n" +
                "<p>Activity /!svn/act/208d5649-1590-0247-a7d6-831b1e447dbf has been created.</p>\n" +
                "<hr />\n" +
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8082</address>\n" +
                "</body></html>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test4()
        {
            stubs.Attach(provider.ItemExists, true);
            FolderMetaData folder = new FolderMetaData();
            folder.Name = "Spikes/SvnFacade/trunk/New Folder 10";
            stubs.Attach(provider.GetItems, folder);

            string request =
                "PROPFIND /Spikes/SvnFacade/trunk/New%20Folder%2010 HTTP/1.1\r\n" +
                "Host: localhost:8082\r\n" +
                "User-Agent: SVN/1.4.2 (r22196) neon/0.26.2\r\n" +
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
                "Date: Fri, 15 Jun 2007 23:02:15 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 455\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/Spikes/SvnFacade/trunk/New%20Folder%2010/</D:href>\n" +
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
            stubs.Attach(provider.GetLatestVersion, 5476);

            string request =
                "PROPFIND /!svn/vcc/default HTTP/1.1\r\n" +
                "Host: localhost:8082\r\n" +
                "User-Agent: SVN/1.4.2 (r22196) neon/0.26.2\r\n" +
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
                "Date: Fri, 15 Jun 2007 23:02:16 GMT\r\n" +
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
                "<lp1:checked-in><D:href>/!svn/bln/5476</D:href></lp1:checked-in>\n" +
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
                "CHECKOUT /!svn/bln/5476 HTTP/1.1\r\n" +
                "Host: localhost:8082\r\n" +
                "User-Agent: SVN/1.4.2 (r22196) neon/0.26.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Length: 174\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:checkout xmlns:D=\"DAV:\"><D:activity-set><D:href>/!svn/act/208d5649-1590-0247-a7d6-831b1e447dbf</D:href></D:activity-set></D:checkout>";

            string expected =
                "HTTP/1.1 201 Created\r\n" +
                "Date: Fri, 15 Jun 2007 23:02:16 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Cache-Control: no-cache\r\n" +
                "Location: http://localhost:8082//!svn/wbl/208d5649-1590-0247-a7d6-831b1e447dbf/5476\r\n" +
                "Content-Length: 330\r\n" +
                "Content-Type: text/html\r\n" +
                "\r\n" +
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>201 Created</title>\n" +
                "</head><body>\n" +
                "<h1>Created</h1>\n" +
                "<p>Checked-out resource //!svn/wbl/208d5649-1590-0247-a7d6-831b1e447dbf/5476 has been created.</p>\n" +
                "<hr />\n" +
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8082</address>\n" +
                "</body></html>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Test7()
        {
            stubs.Attach(provider.SetActivityComment);

            string request =
                "PROPPATCH //!svn/wbl/208d5649-1590-0247-a7d6-831b1e447dbf/5476 HTTP/1.1\r\n" +
                "Host: localhost:8082\r\n" +
                "User-Agent: SVN/1.4.2 (r22196) neon/0.26.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Length: 203\r\n" +
                "Content-Type: application/xml\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\" ?>\n" +
                "<D:propertyupdate xmlns:D=\"DAV:\"><D:set><D:prop><log xmlns=\"http://subversion.tigris.org/xmlns/svn/\">Adding binary file</log></D:prop></D:set>\n" +
                "</D:propertyupdate>\n";

            string expected =
                "HTTP/1.1 207 Multi-Status\r\n" +
                "Date: Fri, 15 Jun 2007 23:02:16 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 348\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/svn/\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response>\n" +
                "<D:href>//!svn/wbl/208d5649-1590-0247-a7d6-831b1e447dbf/5476</D:href>\n" +
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
            stubs.Attach(provider.GetLatestVersion, 5476);
            ItemMetaData item = new FolderMetaData();
            item.Name = "Spikes/SvnFacade/trunk/New Folder 10";
            item.ItemRevision = 5476;
            stubs.Attach(provider.GetItems, item);

            string request =
                "PROPFIND /Spikes/SvnFacade/trunk/New%20Folder%2010 HTTP/1.1\r\n" +
                "Host: localhost:8082\r\n" +
                "User-Agent: SVN/1.4.2 (r22196) neon/0.26.2\r\n" +
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
                "Date: Fri, 15 Jun 2007 23:02:16 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Content-Length: 449\r\n" +
                "Content-Type: text/xml; charset=\"utf-8\"\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n" +
                "<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n" +
                "<D:href>/Spikes/SvnFacade/trunk/New%20Folder%2010/</D:href>\n" +
                "<D:propstat>\n" +
                "<D:prop>\n" +
                "<lp1:checked-in><D:href>/!svn/ver/5476/Spikes/SvnFacade/trunk/New%20Folder%2010</D:href></lp1:checked-in>\n" +
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
                "CHECKOUT /!svn/ver/5476/Spikes/SvnFacade/trunk/New%20Folder%2010 HTTP/1.1\r\n" +
                "Host: localhost:8082\r\n" +
                "User-Agent: SVN/1.4.2 (r22196) neon/0.26.2\r\n" +
                "Connection: TE\r\n" +
                "TE: trailers\r\n" +
                "Content-Length: 174\r\n" +
                "Authorization: Basic andhbmFnZWw6UGFzc0B3b3JkMQ==\r\n" +
                "\r\n" +
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:checkout xmlns:D=\"DAV:\"><D:activity-set><D:href>/!svn/act/208d5649-1590-0247-a7d6-831b1e447dbf</D:href></D:activity-set></D:checkout>";

            string expected =
                "HTTP/1.1 201 Created\r\n" +
                "Date: Fri, 15 Jun 2007 23:02:17 GMT\r\n" +
                "Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2\r\n" +
                "Cache-Control: no-cache\r\n" +
                "Location: http://localhost:8082//!svn/wrk/208d5649-1590-0247-a7d6-831b1e447dbf/Spikes/SvnFacade/trunk/New%20Folder%2010\r\n" +
                "Content-Length: 366\r\n" +
                "Content-Type: text/html\r\n" +
                "\r\n" +
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>201 Created</title>\n" +
                "</head><body>\n" +
                "<h1>Created</h1>\n" +
                "<p>Checked-out resource //!svn/wrk/208d5649-1590-0247-a7d6-831b1e447dbf/Spikes/SvnFacade/trunk/New%20Folder%2010 has been created.</p>\n" +
                "<hr />\n" +
                "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at localhost Port 8082</address>\n" +
                "</body></html>\n";

            string actual = ProcessRequest(request, ref expected);

            Assert.Equal(expected, actual);
        }
    }
}