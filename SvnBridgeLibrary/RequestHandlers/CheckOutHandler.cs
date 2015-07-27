using System.Text;
using SvnBridge.Exceptions;
using SvnBridge.Interfaces;
using SvnBridge.Net; // RequestCache
using SvnBridge.Protocol;
using SvnBridge.SourceControl;
using SvnBridge.Utility;
using SvnBridge.Infrastructure; // DefaultLogger

namespace SvnBridge.Handlers
{
    public class CheckOutHandler : RequestHandlerBase
    {
        protected override void Handle(
            IHttpContext context,
            TFSSourceControlProvider sourceControlProvider)
        {
            IHttpRequest request = context.Request;
            IHttpResponse response = context.Response;
            CheckoutData data = Helper.DeserializeXml<CheckoutData>(request.InputStream);

            try
            {
                string requestPath = GetPath(request);
                string location = CheckOut(sourceControlProvider, data, requestPath);
                SetResponseSettings(response, "text/html", Encoding.UTF8, 201);
                response.AppendHeader("Cache-Control", "no-cache");
                string locationUrl = "http://" + request.Headers["Host"] + Helper.EncodeC(location);
                response.AppendHeader("Location", Helper.UrlEncodeIfNecessary(locationUrl));
                string responseContent =
                    "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                    "<html><head>\n" +
                    "<title>201 Created</title>\n" +
                    "</head><body>\n" +
                    "<h1>Created</h1>\n" +
                    "<p>Checked-out resource " + Helper.Encode(location, true) + " has been created.</p>\n" +
                    "<hr />\n" +
                    "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at " + request.Url.Host + " Port " +
                    request.Url.Port + "</address>\n" +
                    "</body></html>\n";

                WriteToResponse(response, responseContent);
            }
            catch (ConflictException ex)
            {
                RequestCache.Items["RequestBody"] = data;
                DefaultLogger logger = Container.Resolve<DefaultLogger>();
                logger.ErrorFullDetails(ex, context);

                SetResponseSettings(response, "text/xml; charset=\"utf-8\"", Encoding.UTF8, 409);
                string responseContent =
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                    "<D:error xmlns:D=\"DAV:\" xmlns:m=\"http://apache.org/dav/xmlns\" xmlns:C=\"svn:\">\n" +
                    "<C:error/>\n" +
                    "<m:human-readable errcode=\"160024\">\n" +
                    "The version resource does not correspond to the resource within the transaction.  Either the requested version resource is out of date (needs to be updated), or the requested version resource is newer than the transaction root (restart the commit).\n" +
                    "</m:human-readable>\n" +
                    "</D:error>\n";

                WriteToResponse(response, responseContent);
            }
            catch
            {
                RequestCache.Items["RequestBody"] = data;
                throw;
            }
        }

        private string CheckOut(TFSSourceControlProvider sourceControlProvider, CheckoutData request, string requestPath)
        {
            string activityId = PathParser.GetActivityId(request.ActivitySet.href);

            if (requestPath.Contains("/bln"))
                return GetLocalPath("//!svn/wbl/" + activityId + requestPath.Substring(9));

            if (requestPath == "/!svn/vcc/default")
            {
                int latestVersion = sourceControlProvider.GetLatestVersion();
                return GetLocalPath("//!svn/wbl/" + activityId + "/" + latestVersion.ToString());
            }

            int revisionStart = requestPath.IndexOf("/ver/") + 5;
            int version;
            string itemPath;
            if (requestPath.IndexOf('/', revisionStart + 1) != -1)
            {
                int revisionEnd = requestPath.IndexOf('/', revisionStart + 1);
                version = int.Parse(requestPath.Substring(revisionStart, revisionEnd - revisionStart));
                itemPath = requestPath.Substring(revisionEnd);
            }
            else
            {
                version = int.Parse(requestPath.Substring(revisionStart));
                itemPath = "/";
            }

            itemPath = itemPath.Replace("//", "/");
            string location = GetLocalPath("//!svn/wrk/" + activityId + itemPath);

            ItemMetaData item = sourceControlProvider.GetItemsWithoutProperties(-1, Helper.Decode(itemPath), Recursion.None);
            if (item.ItemRevision > version || item.PropertyRevision > version)
            {
                throw new ConflictException();
            }

            return location;
        }
    }
}
