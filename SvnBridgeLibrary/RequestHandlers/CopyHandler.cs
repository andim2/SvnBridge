using System.IO; // StreamWriter
using System.Text;
using SvnBridge.Interfaces;
using SvnBridge.Utility;
using SvnBridge.SourceControl;

namespace SvnBridge.Handlers
{
    /// <summary>
    /// WebDAV COPY is supposed to add an alternative *reference* to an existing resource, AFAICS,
    /// without modifying that resource in any way, shape or form (PUT would be used for doing that).
    ///
    /// "Copy, Move and Rename files and folders in the repository"
    ///   https://www.coderesort.com/about/wiki/HowTo/Subversion/CopyMoveRename
    /// "Re: svn copy and history - quick question"
    ///    http://mail-archives.apache.org/mod_mbox/subversion-users/201302.mbox/%3C511D4FF2.8000405@collab.net%3E
    /// "svn copy question (Is it always 'with history'?)"
    ///   http://svn.haxx.se/users/archive-2005-01/1848.shtml
    ///
    /// TODO: should probably implement MOVE here, too,
    /// by splitting off a base class for shared COPY/MOVE functionality,
    /// then provide both a COPY and a MOVE class.
    /// </summary>
    public class CopyHandler : RequestHandlerBase
    {
        protected override void Handle(
            IHttpContext context,
            TFSSourceControlProvider sourceControlProvider,
            StreamWriter output)
        {
            IHttpRequest request = context.Request;
            IHttpResponse response = context.Response;

            SetResponseSettings(response, "text/html", Encoding.UTF8, 201);

            string destinationHeader = request.Headers["Destination"];
            string activityId = PathParser.GetActivityIdFromDestination(destinationHeader);

            string requestPath = GetPath(request);

            int itemVersion = DetermineItemVersion(requestPath);

            string serverPath = GetServerSidePath(requestPath);

            string destinationHeaderDecoded = Helper.DecodeC(destinationHeader);
            string destination = PathParser.GetPathFromDestination(destinationHeaderDecoded);
            string targetPath = destination.Substring(destination.IndexOf('/', 12));
            bool overwrite = DetermineOverwriteFlag(request);
            // Hmm, do we need to evaluate a (currently not implemented) CopyItem() result here,
            // and then provide different response content depending on whether COPY was successful?
            sourceControlProvider.CopyItem(activityId, itemVersion, serverPath, targetPath, overwrite);

            response.AppendHeader("Location", destinationHeaderDecoded);

            string responseContent = GetResourceCreatedResponse(
                WebDAVResourceType.Copy,
                destination,
                request);

            output.Write(responseContent);
        }

        private static int DetermineItemVersion(string requestPath)
        {
            int itemVersion = TFSSourceControlProvider.LATEST_VERSION;
            if (requestPath.StartsWith("/!svn/bc/"))
            {
                string[] parts = requestPath.Split('/');
                if (parts.Length >= 3)
                    int.TryParse(parts[3], out itemVersion);
            }
            return itemVersion;
        }

        private static bool DetermineOverwriteFlag(IHttpRequest request)
        {
            bool overwrite_default = true;
            bool overwrite = overwrite_default;
            string overwriteHeader = request.Headers["Overwrite"];
            if ((null != overwriteHeader) && (overwriteHeader.Equals("F")))
                overwrite = false;
            return overwrite;
        }
    }
}
