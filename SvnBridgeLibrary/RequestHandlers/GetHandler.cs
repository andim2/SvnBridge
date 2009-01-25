using System;
using System.IO;
using System.Text;
using SvnBridge.Handlers.Renderers;
using SvnBridge.Infrastructure;
using SvnBridge.Infrastructure.Statistics;
using SvnBridge.Interfaces;
using SvnBridge.Net;
using SvnBridge.SourceControl;
using SvnBridge.Utility;
using CodePlex.TfsLibrary.RepositoryWebSvc;

namespace SvnBridge.Handlers
{
	public class GetHandler : RequestHandlerBase
	{
		protected override void Handle(IHttpContext context, TFSSourceControlProvider sourceControlProvider)
		{
			IHttpRequest request = context.Request;
			IHttpResponse response = context.Response;

			string requestPath = GetPath(request);
			int itemVersion = 0;
			string itemPath = null;
            
			if (requestPath.StartsWith("/!svn/bc/"))
			{
				string[] parts = requestPath.Split('/');
				if (parts.Length >= 3)
					int.TryParse(parts[3], out itemVersion);

				itemPath = Helper.Decode(requestPath.Substring(9 + itemVersion.ToString().Length));
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
                    using (StreamWriter writer = new StreamWriter(response.OutputStream))
                    {
                        writer.Write("<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n");
                        writer.Write("<html><head>\n");
                        writer.Write("<title>301 Moved Permanently</title>\n");
                        writer.Write("</head><body>\n");
                        writer.Write("<h1>Moved Permanently</h1>\n");
                        writer.Write("<p>The document has moved <a href=\"" + request.Url + "/\">here</a>.</p>\n");
                        writer.Write("<hr>\n");
                        writer.Write("<address>Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2 Server at " + request.Url.Host + " Port " + request.Url.Port + "</address>\n");
                        writer.Write("</body></html>\n");
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
            byte[] itemData = sourceControlProvider.ReadFile(item);
            response.OutputStream.Write(itemData, 0, itemData.Length);
        }

        private void RenderFolder(IHttpContext context, TFSSourceControlProvider sourceControlProvider, FolderMetaData folder)
        {
            int latestVersion = sourceControlProvider.GetLatestVersion();
            IHttpResponse response = context.Response;
            SetResponseSettings(response, "text/html; charset=UTF-8", Encoding.UTF8, 200);
            response.AppendHeader("Last-Modified", Helper.FormatDateB(folder.LastModifiedDate));
            response.AppendHeader("ETag", "W/\"" + folder.ItemRevision + "//" + Helper.EncodeB(folder.Name) + "\"");
            response.AppendHeader("Accept-Ranges", "bytes");

            using (StreamWriter writer = new StreamWriter(context.Response.OutputStream))
            {
                writer.Write("<html><head><title>");
                writer.Write("Revision " + latestVersion + ": /" + folder.Name);
                writer.Write("</title></head>\n");
                writer.Write("<body>\n");
                writer.Write(" <h2>Revision " + latestVersion + ": /" + folder.Name + "</h2>\n");
                writer.Write(" <ul>\n");
                if (folder.Name != "")
                {
                    writer.Write("  <li><a href=\"../\">..</a></li>\n");
                }
                foreach (ItemMetaData item in folder.Items)
                {
                    string itemName = item.Name;
                    if (itemName.Contains("/"))
                        itemName = itemName.Substring(itemName.LastIndexOf("/") + 1);

                    writer.Write("  <li><a href=\"");
                    writer.Write(Helper.Encode(itemName));
                    if (item.ItemType == ItemType.Folder)
                        writer.Write("/");
                    writer.Write("\">");
                    writer.Write(Helper.EncodeB(itemName));
                    if (item.ItemType == ItemType.Folder)
                        writer.Write("/");
                    writer.Write("</a></li>\n");
                }
                writer.Write(" </ul>\n");
                //writer.Write(" <hr noshade><em>Powered by <a href=\"http://subversion.tigris.org/\">Subversion</a> version 1.4.2 (r22196).</em>\n");
                writer.Write(" <hr noshade><em><a href=\"http://www.codeplex.com/\">CodePlex</a> powered by <a href=\"http://svnbridge.codeplex.com\">SvnBridge</a></em>\n");
                writer.Write("</body></html>");
                writer.Flush();
            }
         }
	}
}