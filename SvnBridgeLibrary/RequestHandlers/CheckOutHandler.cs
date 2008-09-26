using System.Text;
using SvnBridge.Exceptions;
using SvnBridge.Interfaces;
using SvnBridge.Net;
using SvnBridge.Protocol;
using SvnBridge.SourceControl;
using SvnBridge.Utility;

namespace SvnBridge.Handlers
{
	public class CheckOutHandler : RequestHandlerBase
	{
		protected override void Handle(IHttpContext context,
                                       TFSSourceControlProvider sourceControlProvider)
		{
			IHttpRequest request = context.Request;
			IHttpResponse response = context.Response;

			string path = GetPath(request);
        	CheckoutData data = Helper.DeserializeXml<CheckoutData>(request.InputStream);

			try
			{
				string location = CheckOut(sourceControlProvider, data, path);
				SetResponseSettings(response, "text/html", Encoding.UTF8, 201);
				response.AppendHeader("Cache-Control", "no-cache");
				string locationUrl = "http://" + request.Headers["Host"] + Helper.EncodeC(location);
				response.AppendHeader("Location", Helper.UrlEncodeIfNeccesary(locationUrl));
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
			catch (ConflictException)
			{
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
		}

		private string CheckOut(
            TFSSourceControlProvider sourceControlProvider,
			CheckoutData request,
			string path)
		{
			string activityId = PathParser.GetActivityId(request.ActivitySet.href);

			if (path.Contains("/bln"))
				return GetLocalPath("//!svn/wbl/" + activityId + path.Substring(9));

			int revisionStart = path.IndexOf("/ver/") + 5;
			int revisionEnd = path.IndexOf('/', revisionStart + 1);
			string itemPath = path.Substring(revisionEnd);
       
			int version = int.Parse(path.Substring(revisionStart, revisionEnd - revisionStart));
		    itemPath = itemPath.Replace("//", "/");
			string location = GetLocalPath("//!svn/wrk/" + activityId + itemPath);
		
            ItemMetaData item = sourceControlProvider.GetItemsWithoutProperties(-1, Helper.Decode(itemPath), Recursion.None);
			if (item.Revision > version)
			{
				throw new ConflictException();
			}

			return location;
		}
	}
}