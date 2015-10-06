using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Attach;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using SvnBridge.Interfaces;
using Xunit;
using SvnBridge.Exceptions;
using SvnBridge.Infrastructure;
using SvnBridge.PathParsing;
using SvnBridge.SourceControl;
using Tests;
using SvnBridge.Handlers;

namespace UnitTests
{
    public class MergeHandlerTests : HandlerTestsBase
    {
        protected MergeHandler handler = new MergeHandler();

        [Fact]
        [Trait("TestName", "VHECIHE")]
        public void VerifyHandleEncodesCheckedInHrefElement()
        {
            MergeActivityResponse mergeResponse =
                new MergeActivityResponse(5719, DateTime.Parse("2007-12-14T02:38:56.191037Z"), "bradwils");
            mergeResponse.Items.Add(new MergeActivityResponseItem(ItemType.File, "/A !@#$%^&()_-+={[}];',~`..txt"));
            Results r = stubs.Attach((MyMocks.MergeActivity) provider.MergeActivity, mergeResponse);
            request.Path = "http://localhost:8082/";
            request.Input =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:merge xmlns:D=\"DAV:\"><D:source><D:href>/!svn/act/f86c2543-a3d3-d04f-b458-8924481e51c6</D:href></D:source><D:no-auto-merge/><D:no-checkout/><D:prop><D:checked-in/><D:version-name/><D:resourcetype/><D:creationdate/><D:creator-displayname/></D:prop></D:merge>";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            string expected_part = "<D:checked-in><D:href>/!svn/ver/5719/A%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',~%60..txt</D:href></D:checked-in>";
            Assert.True(
                result.Contains(expected_part));
        }

        [Fact]
        [Trait("TestName", "VHEHE")]
        public void VerifyHandleEncodesHrefElement()
        {
            MergeActivityResponse mergeResponse =
                new MergeActivityResponse(5719, DateTime.Parse("2007-12-14T02:38:56.191037Z"), "bradwils");
            mergeResponse.Items.Add(new MergeActivityResponseItem(ItemType.File, "/A !@#$%^&()_-+={[}];',~`..txt"));
            Results r = stubs.Attach((MyMocks.MergeActivity) provider.MergeActivity, mergeResponse);
            request.Path = "http://localhost:8082/";
            request.Input =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:merge xmlns:D=\"DAV:\"><D:source><D:href>/!svn/act/f86c2543-a3d3-d04f-b458-8924481e51c6</D:href></D:source><D:no-auto-merge/><D:no-checkout/><D:prop><D:checked-in/><D:version-name/><D:resourcetype/><D:creationdate/><D:creator-displayname/></D:prop></D:merge>";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string result = Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray());
            string expected_part = "<D:href>/A%20!@%23$%25%5E&amp;()_-+=%7B%5B%7D%5D%3B',~%60..txt</D:href>";
            Assert.True(result.Contains(expected_part));
        }

        [Fact]
        public void VerifyHandleProducesCorrectOutputForConflict()
        {
            stubs.Attach((MyMocks.MergeActivity) provider.MergeActivity, new ConflictException("Conflict at '/Test.txt'"));
            request.Path = "http://localhost:8082/";
            request.Input =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:merge xmlns:D=\"DAV:\"><D:source><D:href>/!svn/act/61652fe8-44cd-8d43-810f-c95deccc6db3</D:href></D:source><D:no-auto-merge/><D:no-checkout/><D:prop><D:checked-in/><D:version-name/><D:resourcetype/><D:creationdate/><D:creator-displayname/></D:prop></D:merge>";

        	handler.Handle(context, new PathParserSingleServerWithProjectInPath(tfsUrl), null);

            string expected =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<D:error xmlns:D=\"DAV:\" xmlns:m=\"http://apache.org/dav/xmlns\" xmlns:C=\"svn:\">\n" +
                "<C:error/>\n" +
                "<m:human-readable errcode=\"160024\">\n" +
                "Conflict at '/Test.txt'\n" +
                "</m:human-readable>\n" +
                "</D:error>\n";
            Assert.Equal(expected, Encoding.Default.GetString(((MemoryStream) response.OutputStream).ToArray()));
            Assert.Equal(409, response.StatusCode);
            Assert.Equal("text/xml; charset=\"utf-8\"", response.ContentType);
            Assert.True(response.Headers.Contains(new KeyValuePair<string, string>("Cache-Control", "no-cache")));
        }
    }
}
