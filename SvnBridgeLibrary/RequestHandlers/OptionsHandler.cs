using System.IO;
using System.Text;
using SvnBridge.Interfaces;
using SvnBridge.Utility;
using SvnBridge.SourceControl;
using System.Xml;
using SvnBridge.Protocol;

namespace SvnBridge.Handlers
{
    public class OptionsHandler : RequestHandlerBase
    {
        protected override void Handle(
            IHttpContext context,
            TFSSourceControlProvider sourceControlProvider)
        {
            IHttpRequest request = context.Request;
            IHttpResponse response = context.Response;
            string requestPath = GetPath(request);

            response.AppendHeader("DAV", "1,2");
            response.AppendHeader("DAV", "version-control,checkout,working-resource");
            response.AppendHeader("DAV", "merge,baseline,activity,version-controlled-collection");
            response.AppendHeader("MS-Author-Via", "DAV");
            response.AppendHeader("Allow", "OPTIONS,GET,HEAD,POST,DELETE,TRACE,PROPFIND,PROPPATCH,COPY,MOVE,LOCK,UNLOCK,CHECKOUT");
            sourceControlProvider.ItemExists(Helper.Decode(requestPath)); // Verify permissions to access

            if (request.InputStream.Length != 0)
            {
                using (XmlReader reader = XmlReader.Create(request.InputStream, Helper.InitializeNewXmlReaderSettings()))
                {
                    reader.MoveToContent();
                    OptionsData data = Helper.DeserializeXml<OptionsData>(reader);

                    if (data != null)
                    {
                        SetResponseSettings(response, "text/xml; charset=\"utf-8\"", Encoding.UTF8, 200);
                        using (StreamWriter output = CreateStreamWriter(response.OutputStream))
                        {
                            Options(data, requestPath, output);
                        }
                    }
                }
            }
            else
            {
                if (requestPath.Equals("/"))
                    SetResponseSettings(response, "httpd/unix-directory", Encoding.UTF8, 200);
                else
                    SetResponseSettings(response, "text/plain", Encoding.UTF8, 200);
            }
        }

        /// <remarks>
        /// While the requestPath parameter is *currently* unused,
        /// I believe that handling of WebDAV OPTIONS is per-resource-specific,
        /// which means the resource-specific path may in fact easily become relevant.
        /// </remarks>
        private void Options(OptionsData data, string requestPath, StreamWriter output)
        {
            output.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
            output.Write("<D:options-response xmlns:D=\"DAV:\">\n");
            output.Write("<D:activity-collection-set><D:href>" + GetLocalPath( "/!svn/act/") + "</D:href></D:activity-collection-set></D:options-response>\n");
        }
    }
}
