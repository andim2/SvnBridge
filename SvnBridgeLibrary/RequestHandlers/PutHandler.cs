using System;
using System.IO;
using System.Text;
using SvnBridge.Interfaces;
using SvnBridge.SourceControl;
using SvnBridge.Utility; // Helper.DebugUsefulBreakpointLocation()

namespace SvnBridge.Handlers
{
    public class PutHandler : RequestHandlerBase
    {
        protected override void Handle(
            IHttpContext context,
            TFSSourceControlProvider sourceControlProvider)
        {
            IHttpRequest request = context.Request;
            IHttpResponse response = context.Response;

            string requestPath = GetPath(request);
            string itemPath = Helper.Decode(requestPath);
            bool created = Put(sourceControlProvider, requestPath, request.InputStream, request.Headers["X-SVN-Base-Fulltext-MD5"], request.Headers["X-SVN-Result-Fulltext-MD5"]);

            if (created)
            {
                SetResponseSettings(response, "text/html", Encoding.UTF8, 201);

                response.AppendHeader("Location", "http://" + request.Headers["Host"] + "/" + itemPath);

                string responseContent = GetResourceCreatedResponse(
                    WebDAVResourceType.Resource,
                    itemPath,
                    request);

                WriteToResponse(response, responseContent);
            }
            else
            {
                SetResponseSettings(response, "text/plain", Encoding.UTF8, 204);
            }
        }

        private static bool Put(TFSSourceControlProvider sourceControlProvider, string requestPath, Stream inputStream, string baseHash, string resultHash)
        {
            // Hmm, is this part really necessary??
            // See also MkColHandler where it's being fed into a regex match...
            if (!requestPath.StartsWith("//"))
            {
                requestPath = "/" + requestPath;
            }

            // FIXME: should be using BasePathParser (GetActivityId() or some such)
            // rather than doing this dirt-ugly open-coded something:
            const int startIndex = 11;
            string activityId = requestPath.Substring(startIndex, requestPath.IndexOf('/', startIndex) - startIndex);
            string serverPath = Helper.Decode(requestPath.Substring(startIndex + activityId.Length));
            byte[] sourceData = new byte[0];
            if (baseHash != null)
            {
                ItemMetaData item = sourceControlProvider.GetItemInActivity(activityId, serverPath);
                sourceData = sourceControlProvider.ReadFile(item);
                if (ChecksumMismatch(baseHash, sourceData))
                {
                    ReportErrorChecksumMismatch("with base file");
                }
            }
            byte[] fileData = SvnDiffParser.ApplySvnDiffsFromStream(inputStream, sourceData);
            if (fileData.Length > 0)
            {
                if (ChecksumMismatch(resultHash, fileData))
                {
                    ReportErrorChecksumMismatch("with new file");
                }
            }
            return sourceControlProvider.WriteFile(activityId, serverPath, fileData);
        }

        private static bool ChecksumMismatch(string hash, byte[] data)
        {
            // git will not pass the relevant checksum, so we need to ignore
            // this
            if(hash==null)
                return false;
            return Helper.GetMd5Checksum(data) != hash;
        }

        private static void ReportErrorChecksumMismatch(string details)
        {
            throw new ChecksumMismatchException(details);
        }

        public sealed class ChecksumMismatchException : InvalidOperationException
        {
            public ChecksumMismatchException(string details)
                : base("Checksum mismatch " + details)
            {
                Helper.DebugUsefulBreakpointLocation();
            }
        }
    }
}
