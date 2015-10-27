using System;
using System.IO;
using System.Text;
using SvnBridge.Interfaces;
using SvnBridge.SourceControl;
using SvnBridge.Utility;

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

                string responseContent = "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                                         "<html><head>\n" +
                                         "<title>201 Created</title>\n" +
                                         "</head><body>\n" +
                                         "<h1>Created</h1>\n" +
                                         "<p>Resource /" + Helper.EncodeB(itemPath) +
                                         " has been created.</p>\n" +
                                         "<hr />\n" +
                                         "<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at " + request.Url.Host +
                                         " Port " + request.Url.Port + "</address>\n" +
                                         "</body></html>\n";

                WriteToResponse(response, responseContent);
            }
            else
            {
                SetResponseSettings(response, "text/plain", Encoding.UTF8, 204);
            }
        }

        private bool Put(TFSSourceControlProvider sourceControlProvider, string requestPath, Stream inputStream, string baseHash, string resultHash)
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
                    throw new Exception("Checksum mismatch with base file");
                }
            }
            byte[] fileData = SvnDiffParser.ApplySvnDiffsFromStream(inputStream, sourceData);
            if (fileData.Length > 0)
            {
                if (ChecksumMismatch(resultHash, fileData))
                {
                    throw new Exception("Checksum mismatch with new file");
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
    }
}
