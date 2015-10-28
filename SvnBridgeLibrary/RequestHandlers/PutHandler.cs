using System;
using System.IO; // StreamWriter
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
            TFSSourceControlProvider sourceControlProvider,
            StreamWriter output)
        {
            IHttpRequest request = context.Request;
            IHttpResponse response = context.Response;

            string requestPath = GetPath(request);
            string itemPathUndecoded = requestPath;
            string itemPath = Helper.Decode(itemPathUndecoded);
            bool isWebdavResourceNewlyCreated = Put(
                sourceControlProvider,
                requestPath,
                request.InputStream,
                request);

            if (isWebdavResourceNewlyCreated)
            {
                SetResponseSettings(response, "text/html", Encoding.UTF8, 201);

                response.AppendHeader("Location", "http://" + request.Headers["Host"] + "/" + itemPath);

                string responseContent = GetResourceCreatedResponse(
                    WebDAVResourceType.Resource,
                    itemPath,
                    request);

                output.Write(responseContent);
            }
            else
            {
                // "204 No Content" == "source successfully copied to pre-existing destination resource"
                SetResponseSettings(response, "text/plain", Encoding.UTF8, 204);
            }
        }

        private enum PUT_Mode
        {
            Activity,
            Collection,
            Resource
        }
        private static bool Put(
            TFSSourceControlProvider sourceControlProvider,
            string requestPath,
            Stream inputStream,
            IHttpRequest request)
        {
            // Hmm, is this part really necessary??
            // See also MkColHandler where it's being fed into a regex match...
            if (!requestPath.StartsWith("//"))
            {
                requestPath = "/" + requestPath;
            }

            // FIXME: I'm not sure whether SVN-specific PUT really always is done
            // within a corresponding activity (MKACTIVITY) or collection (MKCOL),
            // however if not (standard PUT does NOT seem to have that),
            // then we should be able to flexibly handle NOT having an activity here.
            // I'm not sure whether this switching here is even marginally useful,
            // but let's keep it like that...
            //
            // FIXME: should be using BasePathParser (GetActivityId() or some such)
            // rather than doing this dirt-ugly open-coded something:
            const int startIndex = 11;
            string activityId = requestPath.Substring(startIndex, requestPath.IndexOf('/', startIndex) - startIndex);
            string itemPathUndecoded = requestPath.Substring(startIndex + activityId.Length);
            string itemPath = Helper.Decode(itemPathUndecoded);
            ItemMetaData itemBase;
            PUT_Mode mode = PUT_Mode.Activity;

            switch (mode)
            {
                case PUT_Mode.Activity:
                    itemBase = sourceControlProvider.GetItemInActivity(
                        activityId,
                        itemPath);
                    break;
                default:
                    throw new NonActivityPUTNotSupportedException(
                        );
            }

            // Hmm, should perhaps assign hashes only in case string.IsNullOrEmpty() false...
            string baseHashSvnProvided = request.Headers["X-SVN-Base-Fulltext-MD5"];
            string resultHashSvnProvided = request.Headers["X-SVN-Result-Fulltext-MD5"];

            return WebDAV_PUT_SVN(
                sourceControlProvider,
                activityId,
                itemBase,
                itemPath,
                inputStream,
                baseHashSvnProvided,
                resultHashSvnProvided);
        }

        /// <remarks>
        /// I have a hunch that given the security shenanigans of HTTP PUT,
        /// we might want to skip support of Non-WebDAV PUT anyway...
        /// </remarks>
        public sealed class NonActivityPUTNotSupportedException : NotSupportedException
        {
            public NonActivityPUTNotSupportedException()
                : base(
                    "Non-activity PUT not supported yet!")
            {
            }
        }

        private static bool WebDAV_PUT_SVN(
            TFSSourceControlProvider sourceControlProvider,
            string activityId,
            ItemMetaData itemBase,
            string itemPath,
            Stream inputStream,
            string baseHashSvnProvided,
            string resultHashSvnProvided)
        {
            bool isWebdavResourceNewlyCreated = false;

            // Adopt proper SVN speak: "base" == original file, "result" == updated file.
            // In SVN source code, *both* base and result hash are handled as *optional*
            // (and git-svn does not pass the relevant checksum in the result hash case at least).
            bool baseExists = (null != itemBase);
            byte[] baseData = new byte[0];
            if (baseExists)
                baseData = sourceControlProvider.ReadFile(itemBase);
            string baseHashCalc = Helper.GetMd5Checksum(baseData);
            if (null != baseHashSvnProvided)
            {
                bool isMatchingBase = (baseHashSvnProvided == baseHashCalc);
                if (!(isMatchingBase))
                {
                    ReportErrorChecksumMismatch("with base file");
                }
            }
            // We read/apply diff stream *after* the base checksum validate above,
            // since existing unit tests implementation expects this order.
            byte[] resultData = SvnDiffParser.ApplySvnDiffsFromStream(inputStream, baseData);

            string resultHashCalc = Helper.GetMd5Checksum(resultData);
            if ((null != resultHashSvnProvided)
            && (resultData.Length > 0))
            {
                bool isMatchingResult = (resultHashSvnProvided == resultHashCalc);
                if (!(isMatchingResult))
                {
                    ReportErrorChecksumMismatch("with updated result file");
                }
            }

            // Subversion likes to do COPY of a resource,
            // directly followed by a PUT with actually *unchanged* data
            // [[[and subsequently sending a DELETE in case a rename needs to be achieved]]],
            // possibly with the intention to get a blue skies "acknowledge" via a "204 No Content".
            // Thus make sure to invoke WriteFile() (== record pending changes)
            // only in case there actually *is* something to be changed,
            // otherwise results of our transaction collapsing of complementary activities
            // will get terribly imprecise,
            // causing TFS TF14050 "incompatible pending changes" exceptions...
            // Need update only in case of changes - unless the base file does not even exist yet:
            bool isUpdateNeeded = ((baseHashCalc != resultHashCalc) || (!baseExists));
            if (isUpdateNeeded)
            {
                isWebdavResourceNewlyCreated = sourceControlProvider.WriteFile(activityId, itemPath, resultData);
            }
            return isWebdavResourceNewlyCreated;
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
