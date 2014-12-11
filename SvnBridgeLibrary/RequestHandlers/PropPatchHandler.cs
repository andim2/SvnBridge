using System.IO;
using System.Text;
using System.Xml;
using SvnBridge.Infrastructure;
using SvnBridge.Interfaces;
using SvnBridge.Net;
using SvnBridge.Protocol;
using SvnBridge.Utility;
using SvnBridge.SourceControl;

namespace SvnBridge.Handlers
{
	public class PropPatchHandler : RequestHandlerBase
	{
		protected override void Handle(IHttpContext context, TFSSourceControlProvider sourceControlProvider)
		{
			IHttpRequest request = context.Request;
			IHttpResponse response = context.Response;

            string originalXml;
			using (StreamReader sr = new StreamReader(request.InputStream))
			{
                originalXml = sr.ReadToEnd();
			}
            try
            {
                string correctXml = BrokenXml.Escape(originalXml);
                string path = GetPath(request);

                bool extendedNamespaces = false;
                if (correctXml.Contains("http://subversion.tigris.org/xmlns/custom/"))
                    extendedNamespaces = true;

                PropertyUpdateData data = Helper.DeserializeXml<PropertyUpdateData>(correctXml);
                SetResponseSettings(response, "text/xml; charset=\"utf-8\"", Encoding.UTF8, 207);

                using (StreamWriter output = new StreamWriter(response.OutputStream))
                {
                    PropPatch(sourceControlProvider, data, extendedNamespaces, path, output);
                }
            }
            catch
            {
                RequestCache.Items["RequestBody"] = originalXml;
                throw;
            }
		}

        private void PropPatch(TFSSourceControlProvider sourceControlProvider, PropertyUpdateData request, bool extendedNamespaces, string path, TextWriter output)
		{
			string activityPath = path.Substring(10);
			if (activityPath.StartsWith("/"))
			{
				activityPath = activityPath.Substring(1);
			}

			string itemPath = Helper.Decode(activityPath.Substring(activityPath.IndexOf('/')));
			string activityId = activityPath.Split('/')[0];
			if (request.Set.Prop.Properties.Count > 0)
			{
				if (request.Set.Prop.Properties[0].LocalName == "log")
					OutputLogResponse(path, request, sourceControlProvider, extendedNamespaces, activityId, output);
				else
					OutputSetPropertiesResponse(path, request, sourceControlProvider, activityId, output, itemPath);
			}
			else if (request.Remove.Prop.Properties.Count > 0)
			{
				output.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
				output.Write("<D:multistatus xmlns:D=\"DAV:\" xmlns:ns3=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:ns2=\"http://subversion.tigris.org/xmlns/custom/\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/svn/\" xmlns:ns0=\"DAV:\">\n");
				output.Write("<D:response>\n");
				output.Write("<D:href>" + GetLocalPath("/" + Helper.Encode(path.Substring(0, path.Length-1))) + "</D:href>\n");
				output.Write("<D:propstat>\n");
                output.Write("<D:prop>\n");

				foreach (XmlElement element in request.Remove.Prop.Properties)
				{
                    sourceControlProvider.RemoveProperty(activityId, itemPath, GetPropertyName(element));
					OutputElement(output, element);
                }

                output.Write("</D:prop>\n");
                output.Write("<D:status>HTTP/1.1 200 OK</D:status>\n");
				output.Write("</D:propstat>\n");
				output.Write("</D:response>\n");
				output.Write("</D:multistatus>\n");
			}
		}

		private static string GetPropertyName(XmlElement element)
		{
			string propertyName = BrokenXml.UnEscape(element.LocalName);
			if (element.NamespaceURI == WebDav.Namespaces.TIGRISSVN)
				propertyName = "svn:" + propertyName;
			return propertyName;
		}

        private void OutputSetPropertiesResponse(string path, PropertyUpdateData request, TFSSourceControlProvider sourceControlProvider, string activityId, TextWriter output, string itemPath)
		{
			foreach (XmlElement prop in request.Set.Prop.Properties)
			{
				sourceControlProvider.SetProperty(activityId, itemPath, GetPropertyName(prop), prop.InnerText);
			}
			output.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
			output.Write("<D:multistatus xmlns:D=\"DAV:\" xmlns:ns3=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:ns2=\"http://subversion.tigris.org/xmlns/custom/\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/svn/\" xmlns:ns0=\"DAV:\">\n");
			output.Write("<D:response>\n");
			output.Write("<D:href>" + GetLocalPath("/"+Helper.Encode(path)) + "</D:href>\n");
			output.Write("<D:propstat>\n");
            output.Write("<D:prop>\n");
            foreach (XmlElement element in request.Set.Prop.Properties)
			{
				OutputElement(output, element);
			}
            output.Write("</D:prop>\n");
            output.Write("<D:status>HTTP/1.1 200 OK</D:status>\n");
			output.Write("</D:propstat>\n");
			output.Write("</D:response>\n");
			output.Write("</D:multistatus>\n");
		}

		private void OutputElement(TextWriter output, XmlElement element)
		{
            string elementName = BrokenXml.UnEscape(element.LocalName);

			if (element.NamespaceURI == WebDav.Namespaces.SVNDAV)
                output.Write("<ns3:" + elementName + "/>\r\n");
			else if (element.NamespaceURI == WebDav.Namespaces.TIGRISSVN)
                output.Write("<ns1:" + elementName + "/>\r\n");
			else if (element.NamespaceURI == WebDav.Namespaces.DAV)
                output.Write("<ns0:" + elementName + "/>\r\n");
			else //custom
                output.Write("<ns2:" + elementName + "/>\r\n");
		}

        private void OutputLogResponse(string path, PropertyUpdateData request, TFSSourceControlProvider sourceControlProvider, bool extendedNamespaces, string activityId, TextWriter output)
		{
			sourceControlProvider.SetActivityComment(activityId, request.Set.Prop.Properties[0].InnerText);
			output.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
            if (extendedNamespaces)
                output.Write("<D:multistatus xmlns:D=\"DAV:\" xmlns:ns3=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:ns2=\"http://subversion.tigris.org/xmlns/custom/\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/svn/\" xmlns:ns0=\"DAV:\">\n");
            else
                output.Write("<D:multistatus xmlns:D=\"DAV:\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/svn/\" xmlns:ns0=\"DAV:\">\n");

			output.Write("<D:response>\n");
			output.Write("<D:href>" + GetLocalPath("/"+path) + "</D:href>\n");
			output.Write("<D:propstat>\n");
			output.Write("<D:prop>\n");
			output.Write("<ns1:log/>\r\n");
			output.Write("</D:prop>\n");
			output.Write("<D:status>HTTP/1.1 200 OK</D:status>\n");
			output.Write("</D:propstat>\n");
			output.Write("</D:response>\n");
			output.Write("</D:multistatus>\n");
		}
	}
}