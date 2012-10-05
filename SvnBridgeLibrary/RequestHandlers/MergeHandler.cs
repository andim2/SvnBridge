using System.IO;
using System.Text;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using SvnBridge.Exceptions;
using SvnBridge.Interfaces;
using SvnBridge.Protocol;
using SvnBridge.SourceControl;
using SvnBridge.Utility;

namespace SvnBridge.Handlers
{
	public class MergeHandler : RequestHandlerBase
	{
        protected override void Handle(
            IHttpContext context,
            TFSSourceControlProvider sourceControlProvider)
		{
			IHttpRequest request = context.Request;
			IHttpResponse response = context.Response;

			MergeData data = Helper.DeserializeXml<MergeData>(request.InputStream);
			string activityId = PathParser.GetActivityId(data.Source.Href);
			response.AppendHeader("Cache-Control", "no-cache");

            // Provide an all-items merge-response only if wanted. Coincidentally this is how we fix a
            // "A MERGE response for "/tfsserver:8080/proj/fs_item" is not a child of the destination
            // ('/tfsserver%3A8080/proj') error occurring with git-svn, desktop SvnBridge.
            // The actual problem seems to be that while git-svn stores the ':' %3A-encoded in .git/svn/.metadata
            // (which seems to be legal according to RFC3986 "pchar"'s full set of allowed elements
            // for a "segment" part within "path-absolute",
            // since that char is not "reserved"
            // and thus *may* be but does not *need* to be percent-encoded),
            // and uses that %3A-containing string for the MERGE request,
            // SvnBridge merge-response sends a D:href
            // with its own pre-set and thus slightly *differing* LocalPath string
            // rather than reusing the request's argument
            // --> same-root check FAILS (well, "would" fail if we did output an all-items merge-response).
            // If needed, then we might be able to resolve this percent-transcoding issue
            // e.g. by using HttpUtility.UrlEncode()/HttpUtility.UrlDecode()
            // to construct an encoded compare-against string
            // (definitely avoid needlessly unencoding
            // potentially required-encoded URI contents!!).

            // TODO: should be implementing a MergeActivityWithoutResponse() method,
            // to really benefit from no-merge-response mode on the source control side!
            bool disableMergeResponse = GetSvnOption_NoMergeResponse();
            bool wantMergeResponseItems = true;
            if (disableMergeResponse)
            {
                wantMergeResponseItems = false;
            }
			try
			{
				MergeActivityResponse mergeResponse = sourceControlProvider.MergeActivity(activityId);
				SetResponseSettings(response, "text/xml", Encoding.UTF8, 200);
				response.SendChunked = true;
				using (StreamWriter output = CreateStreamWriter(response.OutputStream))
				{
					WriteMergeResponse(request, mergeResponse, wantMergeResponseItems, output);
				}
			}
			catch (ConflictException ex)
			{
				SetResponseSettings(response, "text/xml; charset=\"utf-8\"", Encoding.UTF8, 409);
                using (StreamWriter output = CreateStreamWriter(response.OutputStream))
                {
                    WriteHumanReadableError(output, 160024, ex.Message);
                }
			}
		}

		private void WriteMergeResponse(IHttpRequest request, MergeActivityResponse mergeResponse,
                                    bool wantMergeResponseItems,
										TextWriter output)
		{
			output.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
			output.Write("<D:merge-response xmlns:D=\"DAV:\">\n");
			output.Write("<D:updated-set>\n");
			output.Write("<D:response>\n");
			output.Write("<D:href>" + VccPath + "</D:href>\n");
			output.Write("<D:propstat><D:prop>\n");
			output.Write("<D:resourcetype><D:baseline/></D:resourcetype>\n");
			output.Write("\n");
			output.Write("<D:version-name>" + mergeResponse.Version.ToString() + "</D:version-name>\n");
			output.Write("<D:creationdate>" + Helper.FormatDate(mergeResponse.CreationDate) + "</D:creationdate>\n");
			output.Write("<D:creator-displayname>" + mergeResponse.Creator + "</D:creator-displayname>\n");
			output.Write("</D:prop>\n");
			output.Write("<D:status>HTTP/1.1 200 OK</D:status>\n");
			output.Write("</D:propstat>\n");
			output.Write("</D:response>\n");

			if (wantMergeResponseItems)
			{
				GenerateMergeResponseItems(request, mergeResponse, output);
			}

			output.Write("</D:updated-set>\n");
			output.Write("</D:merge-response>\n");
		}

		private void GenerateMergeResponseItems(IHttpRequest request, MergeActivityResponse mergeResponse,
										TextWriter output)
		{
			foreach (MergeActivityResponseItem item in mergeResponse.Items)
			{
				output.Write("<D:response>\n");
				output.Write("<D:href>" + PathParser.ToApplicationPath(request, Helper.Encode(item.Path, true)) + "</D:href>\n");
				output.Write("<D:propstat><D:prop>\n");
				if (item.Type == ItemType.Folder)
				{
					output.Write("<D:resourcetype><D:collection/></D:resourcetype>\n");
				}
				else
				{
					output.Write("<D:resourcetype/>\n");
				}

				output.Write("<D:checked-in><D:href>");

				output.Write(GetLocalPath(SVNGeneratorHelpers.GetSvnVerFromRevisionLocation(mergeResponse.Version, item.Path, false)));

				output.Write("</D:href></D:checked-in>\n");

				output.Write("</D:prop>\n");
				output.Write("<D:status>HTTP/1.1 200 OK</D:status>\n");
				output.Write("</D:propstat>\n");
				output.Write("</D:response>\n");
			}
		}

        private bool GetSvnOption_NoMergeResponse()
        {
            bool disableMergeResponse = false;
            foreach (string option in GetSvnOptions())
            {
                if (option.Equals("no-merge-response"))
                {
                    disableMergeResponse = true;
                    break;
                }
            }
            return disableMergeResponse;
        }
	}
}
