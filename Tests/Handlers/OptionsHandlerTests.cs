using Attach;
using SvnBridge.Interfaces;
using Xunit;
using SvnBridge.Infrastructure;
using SvnBridge.SourceControl;
using System.IO;
using System.Text;
using SvnBridge.Handlers;

namespace UnitTests
{
    public class OptionsHandlerTests : HandlerTestsBase
    {
        protected OptionsHandler handler = new OptionsHandler();

        [Fact]
        public void Handle_RequestBodyIsSpecified_ReturnCorrectOutput()
        {
            Results r = stubs.Attach(provider.ItemExists, true);
            request.Path = "http://localhost:8082/Spikes/SvnFacade/trunk/New%20Folder%207";
            request.Input = "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:options xmlns:D=\"DAV:\"><D:activity-collection-set/></D:options>";

            string result = HandlerHandle(
                handler);

            string expected = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:options-response xmlns:D=\"DAV:\">\n" +
                "<D:activity-collection-set><D:href>/!svn/act/</D:href></D:activity-collection-set></D:options-response>\n";

            Assert.Equal(expected, result);
            Assert.Equal("text/xml; charset=\"utf-8\"", response.ContentType);
        }

        [Fact]
        public void Handle_NoRequestBodyIsSpecified_ReturnCorrectOutput()
        {
            Results r = stubs.Attach(provider.ItemExists, true);
            request.Headers["Content-Type"] = "text/xml";
            request.Path = "http://localhost:8082/Spikes/SvnFacade/trunk/New%20Folder%207";

            string result = HandlerHandle(
                handler);

            string expected = "";

            Assert.Equal(expected, result);
            Assert.Equal("text/plain", response.ContentType);
        }

        [Fact]
        [Trait("TestName", "HPIRRCCT")]
        public void Handle_PathIsRoot_ReturnCorrectContentType()
        {
            Results r = stubs.Attach(provider.ItemExists, true);
            request.Headers["Accept-Encoding"] = "gzip";
            request.Path = "http://localhost:8082";

            //System.Diagnostics.Debugger.Launch();
            HandlerHandle(
                handler);

            Assert.Equal("httpd/unix-directory", response.ContentType);
        }

        [Fact]
        public void Handle_PathContainsSpecialCharacters_DecodesPathWhenInvokingSourceControlProvider()
        {
            Results r = stubs.Attach(provider.ItemExists, true);
            request.Path = "http://localhost:8082/Spikes/SvnFacade/trunk/New%20Folder%207";

            HandlerHandle(
                handler);

            Assert.Equal("/Spikes/SvnFacade/trunk/New Folder 7", r.Parameters[0]);
        }
    }
}
