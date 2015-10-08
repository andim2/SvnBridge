using System;
using System.IO;
using System.Text;
using SvnBridge.Interfaces;
using SvnBridge.SourceControl;
using SvnBridge.Utility;
using CodePlex.TfsLibrary.RepositoryWebSvc;

namespace SvnBridge.Handlers
{
	public class GetHandler : RequestHandlerBase
	{
        private bool isHeadOnly /* = false */;

        public GetHandler(bool headOnly)
        {
            // For now decided to keep the member bool / ctor param solution
            // to discern between SVN GET and HEAD requests,
            // as provided by workitem #15338 -
            // however if it turns out that there are more differences between
            // GET and HEAD, then it probably is more efficient to
            // rename class into GetHandlerBase and implement derived
            // GetHandler / HeadHandler classes with deviating method behaviour.
            this.isHeadOnly = headOnly;
        }

        /// <summary>
        /// Legacy ctor, to retain external interface compatibility.
        /// </summary>
        public GetHandler()
        {
        }

        protected override void Handle(
            IHttpContext context,
            TFSSourceControlProvider sourceControlProvider)
		{
			IHttpRequest request = context.Request;
			IHttpResponse response = context.Response;

			string requestPath = GetPath(request);
			int itemVersion = 0;
			string itemPath = null;

            if (requestPath.EndsWith("/!svn/ver/0/.svn", StringComparison.InvariantCultureIgnoreCase))
            {
                // Note: Mercurial Convert sends across this specified path that it knows
                // doesn't actually exist. We detect it and return the WebDAV 404 message
                // which it expects. A normal HTML 404 message does not suffice. Also note
                // that IIS seems to be rewriting the 404 message, so using 400 still allows
                // the conversion process to continue.
                SetResponseSettings(response, "text/xml; charset=\"utf-8\"", Encoding.UTF8, 400);
                using (StreamWriter output = CreateStreamWriter(response.OutputStream))
                {
                    string error_string = "Path does not exist in repository."; // _with_ trailing dot, right?
                    WriteHumanReadableError(output, 160013, error_string);
                }
                return;
            }
            else if (requestPath.StartsWith("/!svn/"))
            {
                string itemPathUndecoded;
                // TODO: handle these two very similar types via a common helper or so.
                // Also, this section is semi-duplicated (and thus fragile)
                // in <see cref="GetHandler"/> and <see cref="PropFindHandler"/>
                // (should likely be provided by a method in request base class).
                if (requestPath.StartsWith("/!svn/bc/"))
			    {
				    string[] parts = requestPath.Split('/');
				    if (parts.Length >= 3)
					    int.TryParse(parts[3], out itemVersion);

				    itemPathUndecoded = requestPath.Substring("/!svn/bc/".Length + itemVersion.ToString().Length);
			    }
                else if (requestPath.StartsWith("/!svn/ver/"))
                {
                    string[] parts = requestPath.Split('/');
                    if (parts.Length >= 3)
                        int.TryParse(parts[3], out itemVersion);

                    itemPathUndecoded = requestPath.Substring("/!svn/ver/".Length + itemVersion.ToString().Length);
                }
                else
                {
                    ReportUnsupportedSVNRequestPath(requestPath);
                    itemPathUndecoded = requestPath;
                }
                itemPath = Helper.Decode(itemPathUndecoded);
            }

			if (itemVersion == 0)
				itemVersion = sourceControlProvider.GetLatestVersion();

			itemPath = itemPath ?? requestPath.Substring(1); //remove first '/'

			ItemMetaData item = sourceControlProvider.GetItemsWithoutProperties(itemVersion, itemPath, Recursion.OneLevel);
            if (item == null)
            {
                WriteFileNotFoundResponse(request, response);
            }
            else if (item.ItemType == ItemType.Folder)
            {
                if (!request.Url.ToString().EndsWith("/"))
                {
                    SetResponseSettings(response, "text/html; charset=iso-8859-1", Encoding.UTF8, 301);
                    response.AppendHeader("Location", request.Url + "/");
                    using (StreamWriter output = CreateStreamWriter(response.OutputStream))
                    {
                        output.Write("<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n");
                        output.Write("<html><head>\n");
                        output.Write("<title>301 Moved Permanently</title>\n");
                        output.Write("</head><body>\n");
                        output.Write("<h1>Moved Permanently</h1>\n");
                        output.Write("<p>The document has moved <a href=\"" + request.Url + "/\">here</a>.</p>\n");
                        output.Write("<hr>\n");
                        output.Write("<address>" + GetServerIdentificationString_HostPort(request.Url.Host, request.Url.Port.ToString()) + "</address>\n");
                        output.Write("</body></html>\n");
                    }
                }
                else
                    RenderFolder(context, sourceControlProvider, (FolderMetaData)item);
            }
            else
            {
                RenderFile(context, sourceControlProvider, item);
            }
		}

        private void RenderFile(IHttpContext context, TFSSourceControlProvider sourceControlProvider, ItemMetaData item)
        {
            IHttpResponse response = context.Response;
            SetResponseSettings(response, "text/plain", Encoding.Default, 200);
            response.AppendHeader("Last-Modified", Helper.FormatDateB(item.LastModifiedDate));
            response.AppendHeader("ETag", "\"" + item.ItemRevision + "//" + Helper.EncodeB(item.Name) + "\"");
            response.AppendHeader("Accept-Ranges", "bytes");

            if (!this.isHeadOnly)
            {
                byte[] itemData = sourceControlProvider.ReadFile(item);
                if (itemData.Length > 0) // Write throws exception if zero bytes
                {
                    response.OutputStream.Write(itemData, 0, itemData.Length);
                }
            }
        }

        private static void RenderFolder(IHttpContext context, TFSSourceControlProvider sourceControlProvider, FolderMetaData folder)
        {
            int latestVersion = sourceControlProvider.GetLatestVersion();
            IHttpResponse response = context.Response;
            SetResponseSettings(response, "text/html; charset=UTF-8", Encoding.UTF8, 200);
            response.AppendHeader("Last-Modified", Helper.FormatDateB(folder.LastModifiedDate));
            response.AppendHeader("ETag", "W/\"" + folder.ItemRevision + "//" + Helper.EncodeB(folder.Name) + "\"");
            response.AppendHeader("Accept-Ranges", "bytes");

            using (StreamWriter output = CreateStreamWriter(response.OutputStream))
            {
                output.Write("<html><head><title>");
                output.Write("Revision " + latestVersion + ": /" + folder.Name);
                output.Write("</title></head>\n");
                output.Write("<body>\n");
                output.Write(" <h2>Revision " + latestVersion + ": /" + folder.Name + "</h2>\n");
                output.Write(" <ul>\n");
                if (folder.Name != "")
                {
                    output.Write("  <li><a href=\"../\">..</a></li>\n");
                }
                foreach (ItemMetaData item in folder.Items)
                {
                    string itemName = item.Name;
                    if (itemName.Contains("/"))
                        itemName = itemName.Substring(itemName.LastIndexOf("/") + 1);

                    output.Write("  <li><a href=\"");
                    output.Write(Helper.Encode(itemName));
                    if (item.ItemType == ItemType.Folder)
                        output.Write("/");
                    output.Write("\">");
                    output.Write(Helper.EncodeB(itemName));
                    if (item.ItemType == ItemType.Folder)
                        output.Write("/");
                    output.Write("</a></li>\n");
                }
                output.Write(" </ul>\n");
                //output.Write(" <hr noshade><em>Powered by <a href=\"http://subversion.tigris.org/\">Subversion</a> version 1.4.2 (r22196).</em>\n");
                output.Write(" <hr noshade><em><a href=\"http://www.codeplex.com/\">CodePlex</a> powered by <a href=\"http://svnbridge.codeplex.com\">SvnBridge</a></em>\n");
                output.Write("</body></html>");
                output.Flush();
            }
         }
	}
}
