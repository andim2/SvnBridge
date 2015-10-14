using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Attach;
using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using SvnBridge.Interfaces;
using Xunit;
using SvnBridge.Infrastructure;
using SvnBridge.SourceControl;
using Tests;
using SvnBridge.Handlers;

namespace UnitTests
{
    public class GetLocationsReportTests : HandlerTestsBase
    {
        protected ReportHandler handler = new ReportHandler();

        [Fact]
        public void Handle_OnRoot_ReturnsCorrectOutput()
        {
            stubs.Attach(provider.GetItems, new ItemMetaData());
            request.Path = "http://localhost:8082/!svn/bc/5696";
            request.Input =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><S:get-locations xmlns:S=\"svn:\" xmlns:D=\"DAV:\"><S:path></S:path><S:peg-revision>5696</S:peg-revision><S:location-revision>5597</S:location-revision></S:get-locations>";

            string result = HandlerHandle(
                handler);

            string expected =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<S:get-locations-report xmlns:S=\"svn:\" xmlns:D=\"DAV:\">\n" +
                "<S:location rev=\"5597\" path=\"/\"/>\n" +
                "</S:get-locations-report>\n";
            Assert.Equal(expected, result);
            Assert.Equal(200, context.Response.StatusCode);
            Assert.Equal("text/xml; charset=\"utf-8\"", context.Response.ContentType);
            Assert.Equal(true, context.Response.SendChunked);
        }

        [Fact]
        public void Handle_OnSubFolder_ReturnsCorrectOutput()
        {
            stubs.Attach(provider.GetItems, new ItemMetaData());

            request.Path = "http://localhost:8082/!svn/bc/5696/Folder1";
            request.Input =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><S:get-locations xmlns:S=\"svn:\" xmlns:D=\"DAV:\"><S:path></S:path><S:peg-revision>5696</S:peg-revision><S:location-revision>5573</S:location-revision></S:get-locations>";

            string result = HandlerHandle(
                handler);

            string expected =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<S:get-locations-report xmlns:S=\"svn:\" xmlns:D=\"DAV:\">\n" +
                "<S:location rev=\"5573\" path=\"/Folder1\"/>\n" +
                "</S:get-locations-report>\n";
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Handle_MultipleLocationRevisionsSpecified_ReturnsCorrectOutput()
        {
            stubs.Attach(provider.GetItems, new ItemMetaData());

            request.Path = "http://localhost:8080/!svn/bc/5788";
            request.Input =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><S:get-locations xmlns:S=\"svn:\" xmlns:D=\"DAV:\"><S:path></S:path><S:peg-revision>5788</S:peg-revision><S:location-revision>5787</S:location-revision><S:location-revision>5788</S:location-revision></S:get-locations>";

            string result = HandlerHandle(
                handler);

            string expected =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<S:get-locations-report xmlns:S=\"svn:\" xmlns:D=\"DAV:\">\n" +
                "<S:location rev=\"5787\" path=\"/\"/>\n" +
                "<S:location rev=\"5788\" path=\"/\"/>\n" +
                "</S:get-locations-report>\n";
            Assert.Equal(expected, result);
        }
    }
}
