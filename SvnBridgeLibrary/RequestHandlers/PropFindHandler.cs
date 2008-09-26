using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Xml;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using SvnBridge.Interfaces;
using SvnBridge.Net;
using SvnBridge.Nodes;
using SvnBridge.Protocol;
using SvnBridge.SourceControl;
using SvnBridge.Utility;

namespace SvnBridge.Handlers
{
    public class PropFindHandler : RequestHandlerBase
    {
        protected override void Handle(IHttpContext context, TFSSourceControlProvider sourceControlProvider)
        {
            IHttpRequest request = context.Request;
            IHttpResponse response = context.Response;

            string requestPath = GetPath(request);

            PropFindData propfind = Helper.DeserializeXml<PropFindData>(request.InputStream);

            string depthHeader = request.Headers["Depth"];
            string labelHeader = request.Headers["Label"];

            try
            {
                SetResponseSettings(response, "text/xml; charset=\"utf-8\"", Encoding.UTF8, 207);

                if (request.Headers["Label"] != null)
                {
                    response.AppendHeader("Vary", "Label");
                }

                if (propfind.AllProp != null && requestPath.EndsWith("/!svn/vcc/default"))
                {
                    HandleSvnSyncProp(sourceControlProvider, requestPath, response.OutputStream);
                }
                else if (propfind.AllProp != null)
                {
                    HandleAllProp(sourceControlProvider, requestPath, response.OutputStream);
                }
                else if (propfind.Prop != null)
                {
                    HandleProp(sourceControlProvider, requestPath, depthHeader, labelHeader, propfind.Prop, response.OutputStream);
                }
                else
                {
                    throw new InvalidOperationException("Only <allprop> and <prop> are currently supported.");
                }
            }
            catch (FileNotFoundException)
            {
                WriteFileNotFoundResponse(request, response);
            }
        }

        private void HandleSvnSyncProp(TFSSourceControlProvider sourceControlProvider, string requestPath, Stream stream)
        {
            ItemMetaData firstVersion = GetItems(sourceControlProvider, 1, Constants.ServerRootPath, Recursion.None, true);

            using (StreamWriter sw = new StreamWriter(stream))
            {
                
                sw.Write(@"<?xml version=""1.0"" encoding=""utf-8""?>
<D:multistatus xmlns:D=""DAV:"" xmlns:ns0=""DAV:"">
<D:response xmlns:S=""http://subversion.tigris.org/xmlns/svn/"" xmlns:C=""http://subversion.tigris.org/xmlns/custom/"" xmlns:V=""http://subversion.tigris.org/xmlns/dav/"" xmlns:lp1=""DAV:"" xmlns:lp2=""http://subversion.tigris.org/xmlns/dav/"">
<D:href>" + Helper.UrlEncodeIfNeccesary(GetLocalPath("/!svn/bln/0")) + @"</D:href>
<D:propstat>
<D:prop>
<S:date>" + Helper.FormatDate(firstVersion.LastModifiedDate) + @"</S:date>
<lp1:getetag/>
<lp1:creationdate>" + Helper.FormatDate(firstVersion.LastModifiedDate) + @"</lp1:creationdate>
<lp1:getlastmodified>" + Helper.FormatDateB(firstVersion.LastModifiedDate) + @"</lp1:getlastmodified>
<lp1:baseline-collection><D:href>" + GetLocalPath("/!svn/bc/0/") + @"</D:href></lp1:baseline-collection>
<lp1:version-name>0</lp1:version-name>
<lp2:repository-uuid>" + sourceControlProvider.GetRepositoryUuid() + @"</lp2:repository-uuid>
<lp1:resourcetype><D:baseline/></lp1:resourcetype>
<D:supportedlock>
<D:lockentry>
<D:lockscope><D:exclusive/></D:lockscope>
<D:locktype><D:write/></D:locktype>
</D:lockentry>
</D:supportedlock>
<D:lockdiscovery/>
</D:prop>
<D:status>HTTP/1.1 200 OK</D:status>
</D:propstat>
</D:response>
</D:multistatus>
");
            }
        }

        private static FolderMetaData GetFolderInfo(TFSSourceControlProvider sourceControlProvider,
                                                    string depth,
                                                    string path,
                                                    int? version,
                                                    bool loadPropertiesFromFile)
        {
            if (depth == "0")
            {
                FolderMetaData folderInfo = new FolderMetaData();
                ItemMetaData item =
                    GetItems(sourceControlProvider, version.HasValue ? version.Value : -1, path, Recursion.None, loadPropertiesFromFile);
                folderInfo.Items.Add(item);
                return folderInfo;
            }
            else if (depth == "1")
            {
                return
                    (FolderMetaData)GetItems(sourceControlProvider, version.Value, path, Recursion.OneLevel, loadPropertiesFromFile);
            }
            else
            {
                throw new InvalidOperationException(String.Format("Depth not supported: {0}", depth));
            }
        }

        private static ItemMetaData GetItems(TFSSourceControlProvider sourceControlProvider,
                                             int version,
                                             string path,
                                             Recursion recursion,
                                             bool loadPropertiesFromFile)
        {
            // Make sure path is decoded
            if (loadPropertiesFromFile)
                return sourceControlProvider.GetItems(version, Helper.Decode(path), recursion);
            else
                return sourceControlProvider.GetItemsWithoutProperties(version, Helper.Decode(path), recursion);
        }

        private void HandleAllProp(TFSSourceControlProvider sourceControlProvider, string requestPath, Stream outputStream)
        {
            string revision = requestPath.Split('/')[3];
            string path = requestPath.Substring("/!svn/vcc".Length + revision.Length);

            ItemMetaData item = GetItems(sourceControlProvider, int.Parse(revision), path, Recursion.None, true);

            if (item == null)
            {
                throw new FileNotFoundException("There is no item " + requestPath + " in revision " + revision);
            }

            using (StreamWriter writer = new StreamWriter(outputStream))
            {
                if (item.ItemType == ItemType.Folder)
                {
                    WriteAllPropForFolder(writer, requestPath, item, sourceControlProvider);
                }
                else
                {
                    WriteAllPropForItem(writer, requestPath, item, sourceControlProvider.ReadFile(item), sourceControlProvider);
                }
            }
        }

        private void WriteAllPropForFolder(TextWriter writer, string requestPath, ItemMetaData item, TFSSourceControlProvider sourceControlProvider)
        {
            writer.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
            writer.Write("<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n");
            writer.Write("<D:response xmlns:S=\"http://subversion.tigris.org/xmlns/svn/\" xmlns:C=\"http://subversion.tigris.org/xmlns/custom/\" xmlns:V=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n");
            writer.Write("<D:href>" + Helper.UrlEncodeIfNeccesary(requestPath) + "/</D:href>\n");
            writer.Write("<D:propstat>\n");
            writer.Write("<D:prop>\n");
            foreach (var prop in item.Properties)
            {
                if (prop.Key.StartsWith("svn:"))
                {
                    writer.Write("<S:" + prop.Key.Substring(4) + ">" + prop.Value + "</S:" + prop.Key.Substring(4) + ">\n");
                }
                else
                {
                    writer.Write("<C:" + prop.Key + ">" + prop.Value + "</C:" + prop.Key + ">\n");
                }
            }
            writer.Write("<lp1:getcontenttype>text/html; charset=UTF-8</lp1:getcontenttype>\n");
            writer.Write("<lp1:getetag>W/\"" + item.Revision + "//" + item.Name + "\"</lp1:getetag>\n");
            writer.Write("<lp1:creationdate>" + Helper.FormatDate(item.LastModifiedDate) + "</lp1:creationdate>\n");
            writer.Write("<lp1:getlastmodified>" + Helper.FormatDateB(item.LastModifiedDate) + "</lp1:getlastmodified>\n");
            string svrVerLocalPath = GetLocalPath("/!svn/ver/" + item.Revision + "/" + Helper.Encode(item.Name));
            writer.Write("<lp1:checked-in><D:href>" + Helper.UrlEncodeIfNeccesary(svrVerLocalPath) + "</D:href></lp1:checked-in>\n");
            writer.Write("<lp1:version-controlled-configuration><D:href>" + VccPath + "</D:href></lp1:version-controlled-configuration>\n");
            writer.Write("<lp1:version-name>" + item.Revision + "</lp1:version-name>\n");
            writer.Write("<lp1:creator-displayname>" + item.Author + "</lp1:creator-displayname>\n");
            if (item.Name != "")
            {
                writer.Write("<lp2:baseline-relative-path>" + item.Name + "</lp2:baseline-relative-path>\n");
            }
            else
            {
                writer.Write("<lp2:baseline-relative-path/>\n");
            }
            writer.Write("<lp2:repository-uuid>" + sourceControlProvider.GetRepositoryUuid() + "</lp2:repository-uuid>\n");
            writer.Write("<lp2:deadprop-count>" + item.Properties.Count + "</lp2:deadprop-count>\n");
            writer.Write("<lp1:resourcetype><D:collection/></lp1:resourcetype>\n");
            writer.Write("<D:lockdiscovery/>\n");
            writer.Write("</D:prop>\n");
            writer.Write("<D:status>HTTP/1.1 200 OK</D:status>\n");
            writer.Write("</D:propstat>\n");
            writer.Write("</D:response>\n");
            writer.Write("</D:multistatus>\n");
        }

        private void WriteAllPropForItem(TextWriter writer, string requestPath, ItemMetaData item, byte[] itemData, TFSSourceControlProvider sourceControlProvider)
        {
            writer.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
            writer.Write("<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n");
            writer.Write("<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n");
            writer.Write("<D:href>" + Helper.UrlEncodeIfNeccesary(requestPath) + "</D:href>\n");
            writer.Write("<D:propstat>\n");
            writer.Write("<D:prop>\n");
            writer.Write("<lp1:getcontenttype>text/plain</lp1:getcontenttype>\n");
            writer.Write("<lp1:getetag>W/\"" + item.Revision + "//" + item.Name + "\"</lp1:getetag>\n");
            writer.Write("<lp1:creationdate>" + Helper.FormatDate(item.LastModifiedDate) + "</lp1:creationdate>\n");
            writer.Write("<lp1:getlastmodified>" + Helper.FormatDateB(item.LastModifiedDate) + "</lp1:getlastmodified>\n");
            string svnVerLocalPath = GetLocalPath("/!svn/ver/" + item.Revision + "/" + Helper.Encode(item.Name));
			writer.Write("<lp1:checked-in><D:href>" + Helper.UrlEncodeIfNeccesary(svnVerLocalPath) + "</D:href></lp1:checked-in>\n");
            writer.Write("<lp1:version-controlled-configuration><D:href>" + VccPath + "</D:href></lp1:version-controlled-configuration>\n");
            writer.Write("<lp1:version-name>" + item.Revision + "</lp1:version-name>\n");
            writer.Write("<lp1:creator-displayname>" + item.Author + "</lp1:creator-displayname>\n");
            writer.Write("<lp2:baseline-relative-path>" + item.Name + "</lp2:baseline-relative-path>\n");
            writer.Write("<lp2:md5-checksum>" + Helper.GetMd5Checksum(itemData) + "</lp2:md5-checksum>\n");
            writer.Write("<lp2:repository-uuid>" + sourceControlProvider.GetRepositoryUuid() + "</lp2:repository-uuid>\n");
            writer.Write("<lp2:deadprop-count>0</lp2:deadprop-count>\n");
            writer.Write("<lp1:resourcetype/>\n");
            writer.Write("<D:supportedlock>\n");
            writer.Write("<D:lockentry>\n");
            writer.Write("<D:lockscope><D:exclusive/></D:lockscope>\n");
            writer.Write("<D:locktype><D:write/></D:locktype>\n");
            writer.Write("</D:lockentry>\n");
            writer.Write("</D:supportedlock>\n");
            writer.Write("<D:lockdiscovery/>\n");
            writer.Write("</D:prop>\n");
            writer.Write("<D:status>HTTP/1.1 200 OK</D:status>\n");
            writer.Write("</D:propstat>\n");
            writer.Write("</D:response>\n");
            writer.Write("</D:multistatus>\n");
        }

        private void HandleProp(TFSSourceControlProvider sourceControlProvider, string requestPath, string depthHeader, string labelHeader, PropData data, Stream outputStream)
        {
            if (requestPath == Constants.SvnVccPath)
            {
                WriteVccResponse(sourceControlProvider, requestPath, labelHeader, data, outputStream);
            }
            else if (requestPath.StartsWith("/!svn/bln/"))
            {
                WriteBlnResponse(requestPath, data, outputStream);
            }
            else if (requestPath.StartsWith("/!svn/bc/"))
            {
                WriteBcResponse(sourceControlProvider, requestPath, depthHeader, data, outputStream);
            }
            else
            {
                WritePathResponse(sourceControlProvider, requestPath, depthHeader, data, outputStream);
            }
        }

        private void WriteVccResponse(TFSSourceControlProvider sourceControlProvider,
                                      string requestPath,
                                      string label,
                                      PropData data,
                                      Stream outputStream)
        {
            INode node = new SvnVccDefaultNode(sourceControlProvider, requestPath, label);

            using (StreamWriter writer = new StreamWriter(outputStream))
            {
                writer.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
                writer.Write("<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n");
                WriteProperties(node, data.Properties, writer);
                writer.Write("</D:multistatus>\n");
            }
        }

        private void WriteBlnResponse(string requestPath,
                                      PropData data,
                                      Stream outputStream)
        {
            INode node = new SvnBlnNode(requestPath, int.Parse(requestPath.Substring(10)));

            using (StreamWriter writer = new StreamWriter(outputStream))
            {
                writer.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
                writer.Write("<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n");
                WriteProperties(node, data.Properties, writer);
                writer.Write("</D:multistatus>\n");
            }
        }

        private void WriteBcResponse(TFSSourceControlProvider sourceControlProvider,
                                     string requestPath,
                                     string depthHeader,
                                     PropData data,
                                     Stream outputStream)
        {
            int version = int.Parse(requestPath.Split('/')[3]);
            string path = requestPath.Substring(9 + version.ToString().Length);

            if (!sourceControlProvider.ItemExists(Helper.Decode(path), version))
            {
                throw new FileNotFoundException();
            }

            FolderMetaData folderInfo = GetFolderInfo(sourceControlProvider, depthHeader, path, version, false);

            using (StreamWriter writer = new StreamWriter(outputStream))
            {
                writer.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");

                WriteMultiStatusStart(writer, data.Properties);


                if (depthHeader == "1")
                {
                    INode node = new BcFileNode(version, folderInfo, sourceControlProvider);

                    WriteProperties(node, data.Properties, writer, folderInfo.ItemType == ItemType.Folder);
                }

                foreach (ItemMetaData item in folderInfo.Items)
                {
                    INode node = new BcFileNode(version, item, sourceControlProvider);

                    WriteProperties(node, data.Properties, writer, item.ItemType == ItemType.Folder);
                }

                writer.Write("</D:multistatus>\n");
            }
        }

        private void WritePathResponse(TFSSourceControlProvider sourceControlProvider,
                                       string requestPath,
                                       string depth,
                                       PropData data,
                                       Stream outputStream)
        {
            if (!sourceControlProvider.ItemExists(Helper.Decode(requestPath), -1))
            {
                throw new FileNotFoundException("Unable to find file '" + requestPath + "'in the source control repository",
                                                requestPath);
            }

            FolderMetaData folderInfo = GetFolderInfo(sourceControlProvider, depth, requestPath, null, true);

            using (StreamWriter writer = new StreamWriter(outputStream))
            {
                writer.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");

                WriteMultiStatusStart(writer, data.Properties);

                foreach (ItemMetaData item in folderInfo.Items)
                {
                    INode node = new FileNode(item, sourceControlProvider);

                    WriteProperties(node, data.Properties, writer, item.ItemType == ItemType.Folder);
                }

                writer.Write("</D:multistatus>\n");
            }
        }

        private void WriteMultiStatusStart(TextWriter writer, List<XmlElement> properties)
        {
            if (properties.Count > 1 ||
               (properties.Count == 1 && properties[0].LocalName == "deadprop-count"))
            {
                writer.Write("<D:multistatus xmlns:D=\"DAV:\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:ns0=\"DAV:\">\n");
            }
            else
            {
                writer.Write("<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n");
            }
        }

        private void WriteProperties(INode node,
                                     List<XmlElement> properties,
                                     TextWriter output)
        {
            WriteProperties(node, properties, output, false);
        }

        private void WriteProperties(INode node,
                                     List<XmlElement> properties,
                                     TextWriter output,
                                     bool isFolder)
        {
            bool writeGetContentLengthForFolder = isFolder && PropertiesContains(properties, "getcontentlength");

            output.Write("<D:response xmlns:lp1=\"DAV:\"");
            if (!(properties.Count == 1 && properties[0].LocalName == "resourcetype"))
            {
                output.Write(" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\"");
            }
            if (writeGetContentLengthForFolder)
            {
                output.Write(" xmlns:g0=\"DAV:\"");
            }
            output.Write(">\n");
			output.Write("<D:href>" + Helper.UrlEncodeIfNeccesary(node.Href(this)) + "</D:href>\n");

            XmlDocument doc = new XmlDocument();
            List<string> propertyResults = new List<string>();

            foreach (XmlElement prop in properties)
            {
                XmlElement property = doc.CreateElement(prop.LocalName, prop.NamespaceURI);
                if (!(isFolder && prop.LocalName == "getcontentlength"))
                {
                    propertyResults.Add(node.GetProperty(this, property));
                }
            }

            output.Write("<D:propstat>\n");
            output.Write("<D:prop>\n");
            foreach (string propertyResult in propertyResults)
            {
                output.Write(propertyResult + "\n");
            }
            output.Write("</D:prop>\n");
            output.Write("<D:status>HTTP/1.1 200 OK</D:status>\n");
            output.Write("</D:propstat>\n");

            if (writeGetContentLengthForFolder)
            {
                output.Write("<D:propstat>\n");
                output.Write("<D:prop>\n");
                output.Write("<g0:getcontentlength/>\n");
                output.Write("</D:prop>\n");
                output.Write("<D:status>HTTP/1.1 404 Not Found</D:status>\n");
                output.Write("</D:propstat>\n");
            }

            output.Write("</D:response>\n");
        }

        private static bool PropertiesContains(IEnumerable<XmlElement> properties,
                                               string propertyName)
        {
            foreach (XmlElement property in properties)
            {
                if (property.LocalName == propertyName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
