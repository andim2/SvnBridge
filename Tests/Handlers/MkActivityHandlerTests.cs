using Attach;
using SvnBridge.Interfaces;
using Xunit;
using SvnBridge.Infrastructure;
using SvnBridge.PathParsing;
using SvnBridge.SourceControl;

namespace SvnBridge.Handlers
{
    public class MkActivityHandlerTests : HandlerTestsBase
    {
        protected MkActivityHandler handler = new MkActivityHandler();

        [Fact]
        public void VerifyHandleCorrectlyCallsSourceControlService()
        {
            Results r = stubs.Attach(provider.MakeActivity);
            request.Path = "http://localhost:8080/!svn/act/c512ecbe-7577-ce46-939c-a9e81eb4d98e";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            Assert.Equal(1, r.CallCount);
            Assert.Equal("c512ecbe-7577-ce46-939c-a9e81eb4d98e", r.Parameters[0]);
        }
    }
}
