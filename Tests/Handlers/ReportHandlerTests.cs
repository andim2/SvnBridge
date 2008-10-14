using System;
using System.Collections.Generic;
using System.Text;
using SvnBridge.Infrastructure;
using SvnBridge.SourceControl;
using Xunit;
using Attach;
using SvnBridge.Net;
using SvnBridge.Handlers;
using SvnBridge.PathParsing;
using Tests;

namespace UnitTests
{
    public class ReportHandlerTests : HandlerTestsBase
    {
        protected ReportHandler handler = new ReportHandler();

        [Fact]
        public void Handle_ErrorOccurs_RequestBodyIsSetInRequestCache()
        {
            stubs.Attach(provider.GetLog, Return.Exception(new Exception("Test")));
            request.Path = "http://localhost:8082/!svn/bc/5532/newFolder4";
            request.Input = "<S:log-report xmlns:S=\"svn:\"><S:start-revision>5532</S:start-revision><S:end-revision>1</S:end-revision><S:limit>100</S:limit><S:discover-changed-paths/><S:path></S:path></S:log-report>";

            Record.Exception(delegate { handler.Handle(context, new PathParserSingleServerWithProjectInPath("http://tfsserver"), null); });

            Assert.NotNull(RequestCache.Items["RequestBody"]);
        }

        [Fact]
        public void Handle_UnknownReportSpecified_ReturnsUnknownReportResponse()
        {
            request.Path = "http://localhost:8080/!svn/bc/5775/trunk";
            request.Input = "<S:get-location-segments xmlns:S=\"svn:\" xmlns:D=\"DAV:\"><S:path></S:path><S:peg-revision>5775</S:peg-revision><S:start-revision>5775</S:start-revision><S:end-revision>0</S:end-revision></S:get-location-segments>";

            handler.Handle(context, new PathParserSingleServerWithProjectInPath("http://tfsserver"), null);

            string expected =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:error xmlns:D=\"DAV:\" xmlns:m=\"http://apache.org/dav/xmlns\" xmlns:C=\"svn:\">\n" +
                "<C:error/>\n" +
                "<m:human-readable errcode=\"200007\">\n" +
                "The requested report is unknown.\n" +
                "</m:human-readable>\n" +
                "</D:error>\n";

            Assert.Equal(expected, response.Output);
            Assert.Equal(501, response.StatusCode);
            Assert.Equal("text/xml; charset=\"utf-8\"", response.ContentType);
            Assert.Equal("close", response.GetHeader("Connection"));
        }

        [Fact]
        public void Cancel_LoaderHasNotBeenInitialized_Succeeds()
        {
            Exception result = Record.Exception(delegate { handler.Cancel(); });

            Assert.Null(result);
        }

        [Fact]
        public void Cancel_LoaderHasBeenInitialized_CancelsLoader()
        {
            TestableReportHandler reportHandler = new TestableReportHandler();
            AsyncItemLoader loader = stubs.CreateObject<AsyncItemLoader>(null, null);
            Results cancelMethod = stubs.Attach((MyMocks.Cancel)loader.Cancel, Return.Nothing);
            reportHandler.Loader = loader;

            reportHandler.Cancel();

            Assert.True(cancelMethod.WasCalled);
        }

        class TestableReportHandler : ReportHandler
        {
            public AsyncItemLoader Loader
            {
                set { this.loader = value; }   
            }
        }
    }
}
