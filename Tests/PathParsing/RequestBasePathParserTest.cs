using System;
using Moq;
using SvnBridge.Interfaces;
using Xunit;
using SvnBridge.Infrastructure;
using SvnBridge.PathParsing;

namespace UnitTests
{
    public class RequestBasePathParserTest
    {
        [Fact]
        public void CanParseServerFromUrl()
        {
            var urlValidator = new Mock<TfsUrlValidator>(null);
            urlValidator.Setup(x => x.IsValidTfsServerUrl("http://tfs03.codeplex.com")).Returns(true);
                
            var parser = new PathParserServerAndProjectInPath(urlValidator.Object);
            var request = new StubHttpRequest { Url = new Uri("http://localhost:8081/tfs03.codeplex.com/SvnBridge") };
            string url = parser.GetServerUrl(request, null);
            Assert.Equal("http://tfs03.codeplex.com", url);
        }
        
        [Fact]
        public void CanParseServerFromUrl_WillUseHttpIfHttpsIsNotValid()
        {
            var urlValidator = new Mock<TfsUrlValidator>(null);
            urlValidator.Setup(x => x.IsValidTfsServerUrl("https://tfs03.codeplex.com:8443/tfs")).Returns(false);
            urlValidator.Setup(x => x.IsValidTfsServerUrl("http://tfs03.codeplex.com:8080/tfs")).Returns(true);

            var parser = new PathParserServerAndProjectInPath(urlValidator.Object);
            IHttpRequest request = new StubHttpRequest { Url = new Uri("http://localhost:8081/tfs03.codeplex.com/SvnBridge") };
            string url = parser.GetServerUrl(request, null);
            Assert.Equal("http://tfs03.codeplex.com:8080/tfs", url);
        }

        [Fact]
        public void CanParseServerFromUrl_WithPort()
        {
            var urlValidator = new Mock<TfsUrlValidator>(null);
            urlValidator.Setup(x => x.IsValidTfsServerUrl(It.IsAny<string>())).Returns(false);
            urlValidator.Setup(x => x.IsValidTfsServerUrl("http://tfs03.codeplex.com:8080")).Returns(true);

            var parser = new PathParserServerAndProjectInPath(urlValidator.Object);
            IHttpRequest request = new StubHttpRequest { Url = new Uri("http://localhost:8081/tfs03.codeplex.com:8080/SvnBridge") };

            string url = parser.GetServerUrl(request, null);
            Assert.Equal("http://tfs03.codeplex.com:8080", url);
        }

        [Fact]
        public void CanParseServerFromUrl_WithPortAndNestedFolder()
        {
            var urlValidator = new Mock<TfsUrlValidator>(null);
            urlValidator.Setup(x => x.IsValidTfsServerUrl(It.IsAny<string>())).Returns(false);
            urlValidator.Setup(x => x.IsValidTfsServerUrl("http://tfs03.codeplex.com:8080")).Returns(true);
            
            var parser = new PathParserServerAndProjectInPath(urlValidator.Object);
            IHttpRequest request = new StubHttpRequest { Url = new Uri("http://localhost:8081/tfs03.codeplex.com:8080/SvnBridge/Foo") };

            string url = parser.GetServerUrl(request,null);
            Assert.Equal("http://tfs03.codeplex.com:8080", url);
        }
        
        [Fact]
        public void CanGetLocalPath_WithoutServerUrl()
        {
            var urlValidator = new Mock<TfsUrlValidator>(null);
            urlValidator.Setup(x => x.IsValidTfsServerUrl(It.IsAny<string>())).Returns(false);

            var parser = new PathParserServerAndProjectInPath(urlValidator.Object);
            var request = new StubHttpRequest { Url = new Uri("http://localhost:8081/tfs03.codeplex.com:8080/SvnBridge") };

            string url = parser.GetLocalPath(request);
            Assert.Equal("/SvnBridge", url);
        }
        
        [Fact]
        public void CanParseServerFromUrl_WithCollection()
        {
            var urlValidator = new Mock<TfsUrlValidator>(null);
            urlValidator.Setup(x => x.IsValidTfsServerUrl(It.IsAny<string>())).Returns(false);
            urlValidator.Setup(x => x.IsValidTfsServerUrl("https://tfs03.codeplex.com/tfs/DefaultCollection")).Returns(true);
            
            var parser = new PathParserServerAndProjectInPath(urlValidator.Object);
            var request = new StubHttpRequest { Url = new Uri("http://localhost:8081/tfs03.codeplex.com/tfs/DefaultCollection/$/SvnBridge") };

            string url = parser.GetServerUrl(request, null);
            Assert.Equal("https://tfs03.codeplex.com/tfs/DefaultCollection", url);
        }
        
        [Fact]
        public void CanParseServerFromUrl_WithCollectionAndPortAndNestedFolder()
        {
            var urlValidator = new Mock<TfsUrlValidator>(null);
            urlValidator.Setup(x => x.IsValidTfsServerUrl(It.IsAny<string>())).Returns(false);
            urlValidator.Setup(x => x.IsValidTfsServerUrl("https://tfs03.codeplex.com:8080/tfs/DefaultCollection")).Returns(true);
            
            var parser = new PathParserServerAndProjectInPath(urlValidator.Object);
            var request = new StubHttpRequest { Url = new Uri("http://localhost:8081/tfs03.codeplex.com:8080/tfs/DefaultCollection/$/SvnBridge/Foo") };

            string url = parser.GetServerUrl(request, null);
            Assert.Equal("https://tfs03.codeplex.com:8080/tfs/DefaultCollection", url);
        }
        
        [Fact]
        public void CanGetLocalPath_WithCollection()
        {
            var urlValidator = new Mock<TfsUrlValidator>(null);
            urlValidator.Setup(x => x.IsValidTfsServerUrl(It.IsAny<string>())).Returns(false);
            
            var request = new StubHttpRequest { Url = new Uri("http://localhost:8081/tfs03.codeplex.com/tfs/DefaultCollection/$/SvnBridge") };
            var parser = new PathParserServerAndProjectInPath(urlValidator.Object);
            
            string url = parser.GetLocalPath(request);
            Assert.Equal("/SvnBridge", url);
        }
        
        [Fact]
        public void CanGetLocalPath_WithCollectionAndPortAndNestedFolder()
        {
            var urlValidator = new Mock<TfsUrlValidator>(null);
            urlValidator.Setup(x => x.IsValidTfsServerUrl(It.IsAny<string>())).Returns(false);

            var parser = new PathParserServerAndProjectInPath(urlValidator.Object);
            var request = new StubHttpRequest { Url = new Uri("http://localhost:8081/tfs03.codeplex.com:8080/tfs/DefaultCollection/$/SvnBridge/Foo") };

            string url = parser.GetLocalPath(request);
            Assert.Equal("/SvnBridge/Foo", url);
        }
    }
}