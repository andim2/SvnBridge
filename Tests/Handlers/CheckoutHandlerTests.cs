using System.Collections.Generic;
using System.IO;
using System.Text;
using Attach;
using SvnBridge.Interfaces;
using Xunit;
using SvnBridge.Infrastructure;
using SvnBridge.PathParsing;
using SvnBridge.SourceControl;
using SvnBridge.Handlers;

namespace UnitTests
{
    public class CheckoutHandlerTests : HandlerTestsBase
    {
        protected CheckOutHandler handler = new CheckOutHandler();

        [Fact]
        public void VerifyHandleEncodesCheckedOutResourceOutput()
        {
            ItemMetaData item = new ItemMetaData();
            item.ItemRevision = 0;
            Results r = stubs.Attach(provider.GetItems, item);
            request.Path = "http://localhost:8084/!svn/ver/5718/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',~%60..txt";
            request.Input =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:checkout xmlns:D=\"DAV:\"><D:activity-set><D:href>/!svn/act/f86c2543-a3d3-d04f-b458-8924481e51c6</D:href></D:activity-set></D:checkout>";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string output = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            Assert.True(
                output.Contains(
                    "Checked-out resource //!svn/wrk/f86c2543-a3d3-d04f-b458-8924481e51c6/A%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',~%60..txt"));
        }

        [Fact]
        public void VerifyHandleEncodesLocationResponseHeader()
        {
            ItemMetaData item = new ItemMetaData();
            item.ItemRevision = 0;
            Results r = stubs.Attach(provider.GetItems, item);
            request.Path = "http://localhost:8084/!svn/ver/5718/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',~%60..txt";
            request.Input =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:checkout xmlns:D=\"DAV:\"><D:activity-set><D:href>/!svn/act/f86c2543-a3d3-d04f-b458-8924481e51c6</D:href></D:activity-set></D:checkout>";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            Assert.True(
                response.Headers.Contains(
                    new KeyValuePair<string, string>("Location",
                                                     "http://localhost:8084//!svn/wrk/f86c2543-a3d3-d04f-b458-8924481e51c6/A%20!@%23$%25%5E&()_-+=%7B%5B%7D%5D%3B',~%60..txt")));
        }
    }
}
