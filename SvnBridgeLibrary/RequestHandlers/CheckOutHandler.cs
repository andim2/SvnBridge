using System.Text;
using SvnBridge.Exceptions;
using SvnBridge.Interfaces;
using SvnBridge.Protocol;
using SvnBridge.SourceControl;
using SvnBridge.Utility;

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
                SetResponseHeader_CacheControl_Uncached(response);
                string locationUrl = "http://" + request.Headers["Host"] + Helper.EncodeC(location);
                response.AppendHeader("Location", Helper.UrlEncodeIfNecessary(locationUrl));
                string responseContent = GetResourceCreatedResponse(
                    WebDAVResourceType.ResourceCheckedOut,
                    location,
                    request);

                WriteToResponse(response, responseContent);
            }
            catch (ConflictException ex)
            {
                OnErrorRetainRequestInfo_RequestBody(data);
                GetDefaultLogger().ErrorFullDetails(ex, context);

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
                OnErrorRetainRequestInfo_RequestBody(data);
                throw;
            }
        }

        private string CheckOut(TFSSourceControlProvider sourceControlProvider, CheckoutData request, string requestPath)
        {
            string location;

            string activityId = PathParser.GetActivityId(request.ActivitySet.href);

            if (requestPath.Contains("/bln"))
                return GetLocalPath("//!svn/wbl/" + activityId + requestPath.Substring(9));

            if (requestPath.Equals(Constants.SvnVccPath))
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

            ItemMetaData item = sourceControlProvider.GetItemsWithoutProperties(TFSSourceControlProvider.LATEST_VERSION, Helper.Decode(itemPath), Recursion.None);
            // Possibly technically a NULL item shouldn't happen here
            // (this probably indicates client-side WC state out of sync with repository,
            // since the client shouldn't be doing requests on non-existing items?
            // E.g. in case of a repo-deleted locally non-deleted file).
            // Anyway, we'll handle this with a ConflictException as well,
            // since it's much better than crashing on NULL item access.
            if ((item == null) || // [deleted?]
                (item.ItemRevision > version || item.PropertyRevision > version))
            {
                throw new ConflictException();
            }

            location = GetLocalPath("//!svn/wrk/" + activityId + itemPath);

            return location;
        }
    }
}
