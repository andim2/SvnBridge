using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using SvnBridge.Handlers;
using SvnBridge.Interfaces;
using System.Net;
using System.Collections.Specialized;
using SvnBridge.Infrastructure.Statistics;
using SvnBridge.SourceControl;
using SvnBridge.PathParsing;
using SvnBridge.Infrastructure;
using System.IO; // StreamWriter
using System.Web;
using SvnBridge.Net;
using SvnBridge;
using CodePlex.TfsLibrary; // NetworkAccessDeniedException

namespace UnitTests
{
    public class HttpContextDispatcherTests : IDisposable
    {
        public HttpContextDispatcherTests()
        {
            RequestCache.Init();
        }

        public void Dispose()
        {
            RequestCache.Dispose();
        }

        private static void DispatcherDispatch(
            HttpContextDispatcher dispatcher,
            IHttpContext context,
            StreamWriter output)
        {
            dispatcher.Dispatch(
                context,
                output);
        }

        private static StreamWriter GetOutputWriter(
            IHttpContext context)
        {
            return CreateStreamWriter(
                context.Response.OutputStream);
        }

        private static StreamWriter CreateStreamWriter(
            Stream outputStream)
        {
            Encoding utf8WithoutBOM = new UTF8Encoding(false); // TODO: make this a class member?
            return new StreamWriter(outputStream, utf8WithoutBOM);
        }

        //[Fact]
        //public void Repro()
        //{
        //    BootStrapper.Start();
        //    IPathParser pathParser = new PathParserProjectInDomain("http://codeplex-team:8080", Container.Resolve<TFSSourceControlService>());
        //    HttpContextDispatcher dispatcher = new HttpContextDispatcher(pathParser, Container.Resolve<ActionTrackingViaPerfCounter>());

        //    StubHttpContext context = new StubHttpContext();
        //    StubHttpRequest request = new StubHttpRequest();
        //    StubHttpResponse response = new StubHttpResponse();
        //    context.Request = request;
        //    context.Response = response;
        //    response.OutputStream = new MemoryStream(Constants.BufferSize);

        //    RequestCache.Init();
        //    request.ApplicationPath = "/svn";
        //    request.HttpMethod = "REPORT";
        //    request.Path = "http://galleries.redmond.corp.microsoft.com/svn/!svn/vcc/default";
        //    request.Headers["Authorization"] = "Basic bWlrYmFfZGY6aGVyZXRpYw==";
        //    request.Input =
        //        //"<?xml version=\"1.0\" encoding=\"utf-16\"?>" +
        //        "<update-report xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" send-all=\"true\" xmlns=\"svn:\">" +
        //        "  <entry rev=\"6649\" start-empty=\"true\" />" +
        //        "  <entry rev=\"6649\" start-empty=\"true\">Galleries.AcceptanceTests</entry>" +
        //        "  <entry rev=\"6649\" start-empty=\"true\">Galleries.AcceptanceTests/Service References</entry>" +
        //        "  <entry rev=\"6649\" start-empty=\"true\">Galleries.AcceptanceTests/Service References/PartnerService</entry>" +
        //        "  <entry rev=\"6626\" start-empty=\"false\">Galleries.AcceptanceTests/app.config</entry>" +
        //        "  <entry rev=\"6626\" start-empty=\"false\">Galleries.AcceptanceTests/Data</entry>" +
        //        "  <entry rev=\"6626\" start-empty=\"false\">Galleries.AcceptanceTests/PartnerService</entry>" +
        //        "  <entry rev=\"6626\" start-empty=\"false\">Galleries.AcceptanceTests/Properties</entry>" +
        //        "  <entry rev=\"6626\" start-empty=\"false\">Galleries.AcceptanceTests/Galleries.AcceptanceTests.csproj</entry>" +
        //        "  <entry rev=\"6626\" start-empty=\"false\">Galleries.WebServices.deploy</entry>" +
        //        "  <entry rev=\"6626\" start-empty=\"false\">Galleries.WebServices</entry>" +
        //        "  <entry rev=\"6626\" start-empty=\"false\">DeploymentScripts</entry>" +
        //        "  <entry rev=\"6626\" start-empty=\"false\">.tfs-ignore</entry>" +
        //        "  <entry rev=\"6626\" start-empty=\"false\">Galleries.Repositories</entry>" +
        //        "  <entry rev=\"6626\" start-empty=\"false\">Templates</entry>" +
        //        "  <entry rev=\"6626\" start-empty=\"false\">Galleries.Common</entry>" +
        //        "  <entry rev=\"6650\" start-empty=\"false\">Galleries.Common/Extensions/Masthead.cs</entry>" +
        //        "  <entry rev=\"6650\" start-empty=\"false\">Galleries.Common/Galleries.Common.csproj</entry>" +
        //        "  <entry rev=\"6626\" start-empty=\"false\">Galleries.PartnerService</entry>" +
        //        "  <entry rev=\"6626\" start-empty=\"false\">Assets</entry>" +
        //        "  <entry rev=\"6626\" start-empty=\"false\">Galleries.sln</entry>" +
        //        "  <entry rev=\"6626\" start-empty=\"false\">Galleries.Website.deploy</entry>" +
        //        "  <entry rev=\"6626\" start-empty=\"false\">build.bat</entry>" +
        //        "  <entry rev=\"6626\" start-empty=\"false\">Galleries.Build</entry>" +
        //        "  <entry rev=\"6626\" start-empty=\"false\">Galleries.msbuild</entry>" +
        //        "  <entry rev=\"6626\" start-empty=\"false\">Galleries.Facts</entry>" +
        //        "  <entry rev=\"6626\" start-empty=\"false\">Galleries.Website</entry>" +
        //        "  <entry rev=\"6651\" start-empty=\"false\">Galleries.Website/Content/Application.css</entry>" +
        //        "  <entry rev=\"6651\" start-empty=\"false\">Galleries.Website/Content/VisualStudio/Custom.css</entry>" +
        //        "  <entry rev=\"6651\" start-empty=\"false\">Galleries.Website/Content/Expression/Custom.css</entry>" +
        //        "  <entry rev=\"6651\" start-empty=\"false\">Galleries.Website/Views/Shared</entry>" +
        //        "  <entry rev=\"6653\" start-empty=\"false\">Galleries.Website/Views/Shared/Galleries.Master</entry>" +
        //        "  <entry rev=\"6652\" start-empty=\"false\">Galleries.Website/Views/Shared/Galleries.Master.designer.cs</entry>" +
        //        "  <entry rev=\"6653\" start-empty=\"false\">Galleries.Website/Views/Shared/Galleries.Master.cs</entry>" +
        //        "  <entry rev=\"6655\" start-empty=\"false\">Galleries.Website/Views/Home/View.aspx</entry>" +
        //        "  <entry rev=\"6654\" start-empty=\"false\">Galleries.Website/Views/Item/View.aspx</entry>" +
        //        "  <entry rev=\"6654\" start-empty=\"false\">Galleries.Website/Views/Item/List.aspx</entry>" +
        //        "  <entry rev=\"6654\" start-empty=\"false\">Galleries.Website/Views/Item/Create.aspx</entry>" +
        //        "  <entry rev=\"6626\" start-empty=\"false\">3rdParty</entry>" +
        //        "  <entry rev=\"6626\" start-empty=\"false\">ForgesSetup.ps1</entry>" +
        //        "  <entry rev=\"6626\" start-empty=\"false\">environment.bat</entry>" +
        //        "  <entry rev=\"6626\" start-empty=\"false\">Galleries.Domain</entry>" +
        //        "  <src-path>http://galleries.redmond.corp.microsoft.com/svn/trunk</src-path>" +
        //        "  <target-revision>6655</target-revision>" +
        //        "</update-report>";
        //    dispatcher.Dispatch(context);

        //    string result = Encoding.Default.GetString(((MemoryStream)response.OutputStream).ToArray());
        //    System.Diagnostics.Debug.WriteLine(result);
        //}

//        [Fact]
//        public void Repro()
//        {
//            BootStrapper.Start();
//            IPathParser pathParser = new PathParserProjectInDomainCodePlex();
//            HttpContextDispatcher dispatcher = new HttpContextDispatcher(pathParser, Container.Resolve<ActionTrackingViaPerfCounter>());

//            StubHttpContext context = new StubHttpContext();
//            StubHttpRequest request = new StubHttpRequest();
//            StubHttpResponse response = new StubHttpResponse();
//            context.Request = request;
//            context.Response = response;
//            response.OutputStream = new MemoryStream(Constants.BufferSize);

//            RequestCache.Init();
//            request.ApplicationPath = "/svn";
//            request.HttpMethod = "REPORT";
//            request.Path = "https://bigvisiblecruise.redmond.corp.microsoft.com/svn/!svn/vcc/default";
//            request.Input =
//                //"<?xml version=\"1.0\" encoding=\"utf-16\"?>" +
//"<update-report xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" send-all=\"true\" xmlns=\"svn:\">" +
//"  <entry rev=\"11158\" start-empty=\"true\" />" +
//"  <entry rev=\"8902\" start-empty=\"false\">artifacts</entry>" +
//"  <entry rev=\"8902\" start-empty=\"false\">License.txt</entry>" +
//"  <entry rev=\"8902\" start-empty=\"false\">images</entry>" +
//"  <entry rev=\"8902\" start-empty=\"false\">install</entry>" +
//"  <entry rev=\"8904\" start-empty=\"false\">install/vs/BigVisibleCruise2Installer.vdproj</entry>" +
//"  <entry rev=\"8902\" start-empty=\"false\">lib</entry>" +
//"  <entry rev=\"8902\" start-empty=\"false\">src</entry>" +
//"  <entry rev=\"8921\" start-empty=\"false\">src/BigVisibleCruise/Services/HttpWebClient.cs</entry>" +
//"  <entry rev=\"8924\" start-empty=\"false\">src/BigVisibleCruise/app.config</entry>" +
//"  <entry rev=\"8904\" start-empty=\"false\">src/BigVisibleCruise/DummyProjectStatus.xml</entry>" +
//"  <entry rev=\"8921\" start-empty=\"false\">src/BigVisibleCruise/Skins/StackPhotoSkin.xaml</entry>" +
//"  <entry rev=\"8906\" start-empty=\"false\">src/BigVisibleCruise/Skins/GridSkin.xaml</entry>" +
//"  <entry rev=\"8906\" start-empty=\"false\">src/BigVisibleCruise/Skins/StackSkin.xaml</entry>" +
//"  <entry rev=\"8904\" start-empty=\"false\">src/BigVisibleCruise/Converters/BuildNameToMessageConverter.cs</entry>" +
//"  <entry rev=\"8916\" start-empty=\"false\">src/BigVisibleCruise/Converters/OneBreakerConverter.cs</entry>" +
//"  <entry rev=\"8921\" start-empty=\"false\">src/BigVisibleCruise/Converters/ImageSizeConverter.cs</entry>" +
//"  <entry rev=\"8916\" start-empty=\"false\">src/BigVisibleCruise/Converters/ImagePathConverter.cs</entry>" +
//"  <entry rev=\"8921\" start-empty=\"false\">src/BigVisibleCruise/Views/BigVisibleCruisePresenter.cs</entry>" +
//"  <entry rev=\"8921\" start-empty=\"false\">src/BigVisibleCruise/BigVisibleCruise2.csproj</entry>" +
//"  <entry rev=\"8921\" start-empty=\"false\">src/BigVisibleCruise.Tests/Converters/ImageWidthConverter_Tests.cs</entry>" +
//"  <entry rev=\"8904\" start-empty=\"false\">src/BigVisibleCruise.Tests/Converters/OneBreakerConverter_Tests.cs</entry>" +
//"  <entry rev=\"8902\" start-empty=\"false\">BigVisibleCruise2.sln</entry>" +
//"  <entry rev=\"8902\" start-empty=\"false\">ReleaseNotes.txt</entry>" +
//"  <entry rev=\"8902\" start-empty=\"false\">ReadMe.txt</entry>" +
//"  <missing>src/BigVisibleCruise/Converters/ImageBorderColorConverter.cs</missing>" +
//"  <missing>src/BigVisibleCruise/Converters/ImageWidthConverter.cs</missing>" +
//"  <missing>src/BigVisibleCruise/Images/Check.png</missing>" +
//"  <missing>src/BigVisibleCruise/Images/Stop.png</missing>" +
//"  <src-path>https://BigVisibleCruise.svn.codeplex.com/svn</src-path>" +
//"</update-report>";
//            dispatcher.Dispatch(context);

//            string result = Encoding.Default.GetString(((MemoryStream)response.OutputStream).ToArray());
//            System.Diagnostics.Debug.WriteLine(result);
//        }

//        [Fact]
//        public void Repro()
//        {
//            BootStrapper.Start();
//            IPathParser pathParser = new PathParserProjectInDomainCodePlex();
//            HttpContextDispatcher dispatcher = new HttpContextDispatcher(pathParser, Container.Resolve<ActionTrackingViaPerfCounter>());

//            StubHttpContext context = new StubHttpContext();
//            StubHttpRequest request = new StubHttpRequest();
//            StubHttpResponse response = new StubHttpResponse();
//            context.Request = request;
//            context.Response = response;
//            response.OutputStream = new MemoryStream(Constants.BufferSize);

//            RequestCache.Init();
//            request.ApplicationPath = "/svn";
//            request.HttpMethod = "REPORT";
//            request.Path = "http://dasblog.redmond.corp.microsoft.com/svn/!svn/bc/18879/trunk";
//            request.Input = 
//"<log-report xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns=\"svn:\">" +
//"  <discover-changed-paths />" +
//"  <end-revision>18879</end-revision>" +
//"  <path />" +
//"  <start-revision>15999</start-revision>" +
//"</log-report>";

//            dispatcher.Dispatch(context);

//            string result = Encoding.Default.GetString(((MemoryStream)response.OutputStream).ToArray());
//            System.Diagnostics.Debug.WriteLine(result);
//        }

        [Fact]
        public void Dispatch_ServerIsCodePlexAndUsernameIsMissingDomainAndSuffix_DomainAndSuffixIsAdded()
        {
            IHttpContext context = ConstructStubHttpContext(
                new Uri("https://tfs01.codeplex.com"));
            TestableHttpContextDispatcher dispatcher = new TestableHttpContextDispatcher();
            context.Request.Headers["Authorization"] = "Basic " + Convert.ToBase64String(Encoding.Default.GetBytes("username:password"));

            using (var output = GetOutputWriter(context))
            {
                DispatcherDispatch(dispatcher, context, output);
            }

            Assert.Equal(@"username_cp", dispatcher.Handler.Handle_credentials.UserName);
            Assert.Equal(@"snd", dispatcher.Handler.Handle_credentials.Domain);
        }

        [Fact]
        public void Dispatch_ProjectNotFound_Return404Response()
        {
            IHttpContext context = ConstructStubHttpContext(
                new Uri("https://tfs01.codeplex.com"));
            TestableHttpContextDispatcher dispatcher = new TestableHttpContextDispatcher();
            dispatcher.Parser.GetServerUrl_Return = null;

            using (var output = GetOutputWriter(context))
            {
                DispatcherDispatch(dispatcher, context, output);
            }

            Assert.Equal(404, context.Response.StatusCode);
        }

        [Fact]
        public void Dispatch_ServerIsCodePlexAndNoAuthorizationHeader_CredentialsAreNull()
        {
            IHttpContext context = ConstructStubHttpContext(
                new Uri("https://tfs01.codeplex.com"));
            TestableHttpContextDispatcher dispatcher = new TestableHttpContextDispatcher();

            using (var output = GetOutputWriter(context))
            {
                DispatcherDispatch(dispatcher, context, output);
            }

            Assert.Null(dispatcher.Handler.Handle_credentials);
        }

        [Fact(Skip = "Digest likely cannot be supported truly, and we should NOT return possibly session-foreign unchecked/unvalidated DefaultCredentials")]
        public void Dispatch_DigestAuthorizationHeader_ReturnDefaultCredentials()
        {
            IHttpContext context = ConstructStubHttpContext(
                new Uri("http://tfsserver"));
            TestableHttpContextDispatcher dispatcher = new TestableHttpContextDispatcher();
            context.Request.Headers["Authorization"] = "Digest ...";
            dispatcher.Parser.GetServerUrl_Return = "http://tfsserver";

            using (var output = GetOutputWriter(context))
            {
                DispatcherDispatch(dispatcher, context, output);
            }

            Assert.Same(CredentialCache.DefaultCredentials, dispatcher.Handler.Handle_credentials);
        }

        public void Dispatch_DigestAuthorizationHeader_ThrowsException()
        {
            IHttpContext context = ConstructStubHttpContext(
                new Uri("http://tfsserver"));
            TestableHttpContextDispatcher dispatcher = new TestableHttpContextDispatcher();
            context.Request.Headers["Authorization"] = "Digest ...";
            dispatcher.Parser.GetServerUrl_Return = "http://tfsserver";

            using (var output = GetOutputWriter(context))
            {
                Exception exception = Record.Exception(delegate { DispatcherDispatch(dispatcher, context, output); });

                Assert.NotNull(exception);
                Assert.IsType(typeof(NetworkAccessDeniedException), exception);
            }
        }

        [Fact]
        public void Dispatch_UnknownAuthorizationHeader_ThrowsException()
        {
            IHttpContext context = ConstructStubHttpContext(
                new Uri("http://tfsserver"));
            TestableHttpContextDispatcher dispatcher = new TestableHttpContextDispatcher();
            context.Request.Headers["Authorization"] = "abcdef ...";

            using (var output = GetOutputWriter(context))
            {
                Exception exception = Record.Exception(delegate { DispatcherDispatch(dispatcher, context, output); });

                Assert.NotNull(exception);
            }
        }

        [Fact]
        public void Dispatch_RequestIsCancelledUnderIIS6_ExceptionIsNotThrown()
        {
            IHttpContext context = ConstructStubHttpContext(
                new Uri("https://tfs01.codeplex.com"));
            TestableHttpContextDispatcher dispatcher = new TestableHttpContextDispatcher();
            dispatcher.Handler.Handle_Throw = new IOException();

            using (var output = GetOutputWriter(context))
            {
                Exception result = Record.Exception(delegate { DispatcherDispatch(dispatcher, context, output); });

                Assert.Null(result);
            }
        }

        [Fact]
        public void Dispatch_RequestIsCancelledUnderIIS7_ExceptionIsNotThrown()
        {
            IHttpContext context = ConstructStubHttpContext(
                new Uri("https://tfs01.codeplex.com"));
            TestableHttpContextDispatcher dispatcher = new TestableHttpContextDispatcher();

            using (var output = GetOutputWriter(context))
            {
                dispatcher.Handler.Handle_Throw = new HttpException("An error occurred while communicating with the remote host.");
                Exception result1 = Record.Exception(delegate { DispatcherDispatch(dispatcher, context, output); });

                dispatcher.Handler.Handle_Throw = new HttpException("The remote host closed the connection.");
                Exception result2 = Record.Exception(delegate { DispatcherDispatch(dispatcher, context, output); });

                Assert.Null(result1);
                Assert.Null(result2);
            }
        }

        private static IHttpContext ConstructStubHttpContext(
            Uri uri)
        {
            StubHttpContext context = new StubHttpContext();
            StubHttpRequest request = new StubHttpRequest();
            StubHttpResponse response = new StubHttpResponse();
            request.Url =  uri;
            request.Headers = new NameValueCollection();
            context.Request = request;
            context.Response = response;

            return context;
        }
    }

    public class TestableHttpContextDispatcher : HttpContextDispatcher
    {
        public StubHandler Handler = new StubHandler();

        public TestableHttpContextDispatcher() : base(new StubParser(), new StubActionTracking()) {}

        public StubParser Parser
        {
            get { return (StubParser)parser; }
        }

        public override RequestHandlerBase GetHttpHandler(string httpMethod)
        {
            return Handler;
        }
    }

    public class StubActionTracking : ActionTrackingViaPerfCounter
    {
        public override void Request(RequestHandlerBase handler) {}
        public override void Error() {}
        public override IDictionary<string, long> GetStatistics()
        {
            return null;
        }
    }

    public class StubParser : IPathParser
    {
        public string GetServerUrl_Return = "https://tfs01.codeplex.com";

        public string GetServerUrl(IHttpRequest request, ICredentials credentials)
        {
            return GetServerUrl_Return;
        }

        public string GetLocalPath(IHttpRequest request)
        {
            throw new NotImplementedException();
        }

        public string GetLocalPath(IHttpRequest request, string url)
        {
            throw new NotImplementedException();
        }

        public string GetProjectName(IHttpRequest request)
        {
            return "testproject";
        }

        public string GetApplicationPath(IHttpRequest request)
        {
            throw new NotImplementedException();
        }

        public string GetActivityId(string href)
        {
            throw new NotImplementedException();
        }

        public string GetActivityIdFromDestination(string href)
        {
            throw new NotImplementedException();
        }

        public string ToApplicationPath(IHttpRequest request, string href)
        {
            throw new NotImplementedException();
        }

        public string GetPathFromDestination(string href)
        {
            throw new NotImplementedException();
        }
    }

    public class StubHandler : RequestHandlerBase
    {
        public NetworkCredential Handle_credentials;
        public Exception Handle_Throw = null;

        public override void Handle(
            IHttpContext context,
            IPathParser pathParser,
            NetworkCredential credentials,
            StreamWriter output)
        {
            Handle_credentials = credentials;
            if (Handle_Throw != null)
                throw Handle_Throw;
        }

        protected override void Handle(
            IHttpContext context,
            TFSSourceControlProvider sourceControlProvider,
            StreamWriter output)
        {
        }
    }
}
