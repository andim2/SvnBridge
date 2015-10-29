using System;
using System.Collections.Generic;
using System.IO; // Path.Combine(), Stream
using System.Text;
using System.Xml;
using CodePlex.TfsLibrary.RepositoryWebSvc; // TFS's ItemType only (layer violation)
using SvnBridge.Interfaces; // IHttpContext, IHttpRequest
using SvnBridge.Nodes; // INode
using SvnBridge.Protocol; // PropData only
using SvnBridge.SourceControl;
using SvnBridge.Utility; // Helper.*

namespace SvnBridge.Handlers
{
    public class PropFindHandler : PropRequestHandlerBase
    {
        private static bool _doLogFile /* = false */ = false /* CS0649 */;

        protected override void Handle(
            IHttpContext context,
            TFSSourceControlProvider sourceControlProvider)
        {
            IHttpRequest request = context.Request;
            IHttpResponse response = context.Response;
            PropFindData propfind = Helper.DeserializeXml<PropFindData>(request.InputStream);

            try
            {
                string requestPath = GetPath(request);
                string depthHeader = request.Headers["Depth"];
                string labelHeader = request.Headers["Label"];

                // RFC4918 PROPFIND: "Servers SHOULD treat a request without a Depth header
                // as if a "Depth: infinity" header was included."
                // Additionally considering empty-case
                // (IsNullOrEmpty() rather than != null) is ok, right?
                if (String.IsNullOrEmpty(depthHeader))
                    depthHeader = "infinity";

                SetResponseSettings(response, "text/xml; charset=\"utf-8\"", Encoding.UTF8, 207);

                if (labelHeader != null)
                {
                    response.AppendHeader("Vary", "Label");
                }

                if (propfind.AllProp != null)
                {
                    if(requestPath.EndsWith(Constants.SvnVccPath))
                        HandleAllPropVccDefault(sourceControlProvider, requestPath, response.OutputStream);
                    else
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
                Helper.DebugUsefulBreakpointLocation();
                WriteFileNotFoundResponse(request, response);
            }
            catch
            {
                OnErrorRetainRequestInfo_RequestBody(propfind);
                throw;
            }
        }

        private void HandleAllPropVccDefault(TFSSourceControlProvider sourceControlProvider, string requestPath, Stream stream)
        {
            int latestVersion = sourceControlProvider.GetLatestVersion();
            using (StreamWriter writer = CreateStreamWriter(stream))
            {
                writer.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
                writer.Write("<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n");
                writer.Write("<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n");
                writer.Write("<D:href>" + VccPath + "</D:href>\n");
                writer.Write("<D:propstat>\n");
                writer.Write("<D:prop>\n");
                writer.Write("<lp1:checked-in><D:href>" + GetLocalPath("/!svn/bln/" + latestVersion) + "</D:href></lp1:checked-in>\n");
                writer.Write("<lp2:repository-uuid>" + sourceControlProvider.GetRepositoryUuid() + "</lp2:repository-uuid>\n");
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
        }

        private static FolderMetaData GetFolderInfo(TFSSourceControlProvider sourceControlProvider,
                                                    string depth,
                                                    string path,
                                                    int? version,
                                                    bool loadPropertiesFromFile)
        {
            Recursion recursion = ConvertDepthHeaderToRecursion(depth);
            var versionToFetch = version.HasValue ? version.Value : TFSSourceControlProvider.LATEST_VERSION;
            if (recursion == Recursion.OneLevel)
                return (FolderMetaData)GetItems(sourceControlProvider, versionToFetch, path, recursion, loadPropertiesFromFile);

            FolderMetaData folderInfo = new FolderMetaData();
            ItemMetaData item = GetItems(sourceControlProvider, versionToFetch, path, recursion, loadPropertiesFromFile);
            folderInfo.Items.Add(item);
            return folderInfo;
        }

        private static ItemMetaData GetItems(TFSSourceControlProvider sourceControlProvider,
                                             int version,
                                             string path,
                                             Recursion recursion,
                                             bool loadPropertiesFromFile)
        {
            // Make sure path is decoded

            // FIXME BUG!?: those two calls actually map into the *same* underlying method call!!
            // Not sure about properties handling here, but if indeed it's expected
            // to have property storage items returned here, then it's probably wrong
            // (always returnPropertyFiles == false!). TODO add breakpoint here and investigate!
            if (loadPropertiesFromFile)
                return sourceControlProvider.GetItems(version, Helper.Decode(path), recursion);
            else
                return sourceControlProvider.GetItemsWithoutProperties(version, Helper.Decode(path), recursion);
        }

        private void HandleAllProp(TFSSourceControlProvider sourceControlProvider, string requestPath, Stream outputStream)
        {
            int revision;
            string path;
            bool bcPath = false;
            // TODO: handle these two very similar types via a common helper or so.
            // Also, this section is semi-duplicated (and thus fragile)
            // in <see cref="GetHandler"/> and <see cref="PropFindHandler"/>
            // (should likely be provided by a method in request base class).
            if (requestPath.StartsWith("/!svn/"))
            {
                if (requestPath.StartsWith("/!svn/bc"))
                {
                    bcPath = true;
                    revision = int.Parse(requestPath.Split('/')[3]);
                    path = requestPath.Substring("/!svn/bc/".Length + revision.ToString().Length);
                }
                else
                if (requestPath.StartsWith("/!svn/ver"))
                {
                    revision = int.Parse(requestPath.Split('/')[3]);
                    path = requestPath.Substring("/!svn/ver/".Length + revision.ToString().Length);
                }
                else
                {
                    ReportUnsupportedSVNRequestPath(requestPath);
                    // Exception dummies:
                    path = requestPath;
                    revision = 0;
                }
            }
            else
            {
                revision = sourceControlProvider.GetItems(TFSSourceControlProvider.LATEST_VERSION, requestPath, Recursion.None).Revision;
                path = requestPath;
            }

            ItemMetaData item = GetItems(sourceControlProvider, revision, path, Recursion.None, true);

            if (item == null)
            {
                if (IsSvnRequestForProjectCreation(path, revision, sourceControlProvider))
                {
                    item = GetItems(sourceControlProvider, revision, "", Recursion.None, true);
                    item.Name = "trunk";
                }

                if (item == null)
                    throw new FileNotFoundException("There is no item " + requestPath + " in revision " + revision);
            }

            using (StreamWriter writer = CreateStreamWriter(outputStream))
            {
                if (item.ItemType == ItemType.Folder)
                {
                    WriteAllPropForFolder(writer, requestPath, item, bcPath, sourceControlProvider);
                }
                else
                {
                    WriteAllPropForItem(writer, requestPath, item, sourceControlProvider.ReadFile(item), sourceControlProvider);
                }
            }
        }

        private string GetSvnVerLocalPath(ItemMetaData item)
        {
		return GetLocalPath(SVNGeneratorHelpers.GetSvnVerFromRevisionLocation(item.Revision, item.Name, true));
        }

        private void WriteAllPropForFolder(TextWriter writer, string requestPath, ItemMetaData item, bool bcPath, TFSSourceControlProvider sourceControlProvider)
        {
            writer.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
            if (bcPath)
            {
                writer.Write("<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n");
            }
            else
            {
                writer.Write("<D:multistatus xmlns:D=\"DAV:\" xmlns:ns2=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:ns1=\"http://www.w3.org/2001/XMLSchema\" xmlns:ns0=\"DAV:\">\n");
            }
            writer.Write("<D:response xmlns:S=\"http://subversion.tigris.org/xmlns/svn/\" xmlns:C=\"http://subversion.tigris.org/xmlns/custom/\" xmlns:V=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n");
            writer.Write("<D:href>" + Helper.Encode(requestPath) + "/</D:href>\n");
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
            string svnVerLocalPath = GetSvnVerLocalPath(item);

            writer.Write("<lp1:getcontenttype>text/html; charset=UTF-8</lp1:getcontenttype>\n");
            writer.Write(WebDAVGeneratorHelpers.GetETag_revision_item("lp1", item.Revision, item.Name) + "\n");
            writer.Write("<lp1:creationdate>" + Helper.FormatDate(item.LastModifiedDate) + "</lp1:creationdate>\n");
            writer.Write("<lp1:getlastmodified>" + Helper.FormatDateB(item.LastModifiedDate) + "</lp1:getlastmodified>\n");
            writer.Write("<lp1:checked-in><D:href>" + svnVerLocalPath + "</D:href></lp1:checked-in>\n");
            writer.Write("<lp1:version-controlled-configuration><D:href>" + VccPath + "</D:href></lp1:version-controlled-configuration>\n");
            writer.Write("<lp1:version-name>" + item.Revision + "</lp1:version-name>\n");
            writer.Write("<lp1:creator-displayname>" + item.Author + "</lp1:creator-displayname>\n");
            if (item.Name != "")
            {
                writer.Write("<lp2:baseline-relative-path>" + Helper.EncodeB(item.Name) + "</lp2:baseline-relative-path>\n");
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
            string svnVerLocalPath = GetSvnVerLocalPath(item);

            writer.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
            writer.Write("<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n");
            writer.Write("<D:response xmlns:S=\"http://subversion.tigris.org/xmlns/svn/\" xmlns:C=\"http://subversion.tigris.org/xmlns/custom/\" xmlns:V=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n");
            writer.Write("<D:href>" + Helper.Encode(requestPath) + "</D:href>\n");
            writer.Write("<D:propstat>\n");
            writer.Write("<D:prop>\n");
            writer.Write("<lp1:getcontentlength>" + itemData.Length + "</lp1:getcontentlength>\n");
            writer.Write("<lp1:getcontenttype>text/plain</lp1:getcontenttype>\n");
            writer.Write(WebDAVGeneratorHelpers.GetETag_revision_item("lp1", item.Revision, item.Name) + "\n");
            writer.Write("<lp1:creationdate>" + Helper.FormatDate(item.LastModifiedDate) + "</lp1:creationdate>\n");
            writer.Write("<lp1:getlastmodified>" + Helper.FormatDateB(item.LastModifiedDate) + "</lp1:getlastmodified>\n");
            writer.Write("<lp1:checked-in><D:href>" + svnVerLocalPath + "</D:href></lp1:checked-in>\n");
            writer.Write("<lp1:version-controlled-configuration><D:href>" + VccPath + "</D:href></lp1:version-controlled-configuration>\n");
            writer.Write("<lp1:version-name>" + item.Revision + "</lp1:version-name>\n");
            writer.Write("<lp1:creator-displayname>" + item.Author + "</lp1:creator-displayname>\n");
            writer.Write("<lp2:baseline-relative-path>" + Helper.EncodeB(item.Name) + "</lp2:baseline-relative-path>\n");
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
            if (_doLogFile)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    DoHandleProp(sourceControlProvider, requestPath, depthHeader, labelHeader, data, ms);

                    // Initiate write to network *prior* to hitting file (perf opt!!)
                    CopyMemoryStream(ms, outputStream);

                    string pathLogFile = Path.Combine(LogBasePath, DateTime.Now.ToString("HH_mm_ss_ffff") + ".txt");
                    using (FileStream file = new FileStream(pathLogFile, FileMode.Create, System.IO.FileAccess.Write))
                    {
                        CopyMemoryStream(ms, file);
                    }
                }
            }
            else
            {
                DoHandleProp(sourceControlProvider, requestPath, depthHeader, labelHeader, data, outputStream);
            }
        }

        private static void CopyMemoryStream(MemoryStream inputStream, Stream outputStream)
        {
            var positionBackup = inputStream.Position;
            try
            {
                CopyMemoryStreamDo(inputStream, outputStream);
            }
            finally
            {
                inputStream.Position = positionBackup;
            }
        }

        private static void CopyMemoryStreamDo(MemoryStream inputStream, Stream outputStream)
        {
            inputStream.Seek(0, SeekOrigin.Begin);
            // Potentially relevant:
            // http://stackoverflow.com/questions/230128/how-do-i-copy-the-contents-of-one-stream-to-another
            inputStream.WriteTo(outputStream);
        }

        private void DoHandleProp(TFSSourceControlProvider sourceControlProvider, string requestPath, string depthHeader, string labelHeader, PropData data, Stream outputStream)
        {
            // "Use of WebDAV in Subversion"
            //    http://svn.apache.org/repos/asf/subversion/trunk/notes/http-and-webdav/webdav-usage.html
            //      "URL Layout"
            bool requestHandled = false;
            if (requestPath.StartsWith("/!svn/"))
            {
                if (requestPath.Equals(Constants.SvnVccPath))
                {
                    WriteVccResponse(sourceControlProvider, requestPath, labelHeader, data, outputStream);
                    requestHandled = true;
                }
                else if (requestPath.StartsWith("/!svn/bln/"))
                {
                    WriteBlnResponse(requestPath, data, outputStream);
                    requestHandled = true;
                }
                else if (requestPath.StartsWith("/!svn/bc/"))
                {
                    WriteBcResponse(sourceControlProvider, requestPath, depthHeader, data, outputStream);
                    requestHandled = true;
                }
                else if (requestPath.StartsWith("/!svn/wrk/"))
                {
                    WriteWrkResponse(sourceControlProvider, requestPath, depthHeader, data, outputStream);
                    requestHandled = true;
                }
                else
                {
                    ReportUnsupportedSVNRequestPath(requestPath);
                }
            }
            if (!requestHandled)
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

            using (StreamWriter writer = CreateStreamWriter(outputStream))
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
            int version = int.Parse(requestPath.Substring(10));
            INode node = new SvnBlnNode(requestPath, version);

            using (StreamWriter writer = CreateStreamWriter(outputStream))
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
            bool setTrunkAsName = false;

            if (!sourceControlProvider.ItemExists(Helper.Decode(path), version))
            {
                if (!IsSvnRequestForProjectCreation(path, version, sourceControlProvider))
                    throw new FileNotFoundException();
                path = "";
                setTrunkAsName = true;
            }

            FolderMetaData folderInfo = GetFolderInfo(sourceControlProvider, depthHeader, path, version, false);
            if (setTrunkAsName)
                folderInfo.Name = "trunk";

            using (StreamWriter writer = CreateStreamWriter(outputStream))
            {
                writer.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");

                WriteMultiStatusStart(writer, data.Properties);

                if (depthHeader.Equals("1"))
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

            if (_doLogFile)
            {
                string propdesc = "";
                foreach (XmlElement prop in data.Properties)
                {
                    propdesc += prop.LocalName + ":";
                }
                WriteLog(path + ":" + propdesc);
            }
        }

        private void WritePathResponse(TFSSourceControlProvider sourceControlProvider,
                                       string requestPath,
                                       string depth,
                                       PropData data,
                                       Stream outputStream)
        {
            if (!sourceControlProvider.ItemExists(GetLocalPathTrailingSlashStripped(Helper.Decode(requestPath)), TFSSourceControlProvider.LATEST_VERSION))
            {
                throw new FileNotFoundException("Unable to find file '" + requestPath + "' in the source control repository",
                                                requestPath);
            }

            FolderMetaData folderInfo = GetFolderInfo(sourceControlProvider, depth, requestPath, null, true);

            using (StreamWriter writer = CreateStreamWriter(outputStream))
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

        private void WriteWrkResponse(TFSSourceControlProvider sourceControlProvider,
                                       string requestPath,
                                       string depth,
                                       PropData data,
                                       Stream outputStream)
        {
            string activityId = requestPath.Split('/')[3];
            if (!(depth.Equals("0")))
            {
                ReportUnsupportedDepthHeaderValue(depth);
            }
            string path = requestPath.Substring(11 + activityId.Length);
            ItemMetaData item = sourceControlProvider.GetItemInActivity(activityId, Helper.Decode(path));

            if (item == null)
            {
                throw new FileNotFoundException("Unable to find file '" + path + "' in the specified activity", path);
            }

            using (StreamWriter writer = CreateStreamWriter(outputStream))
            {
                writer.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
                WriteMultiStatusStart(writer, data.Properties);
                INode node = new FileNode(item, sourceControlProvider);
                WriteProperties(node, data.Properties, writer, item.ItemType == ItemType.Folder);
                writer.Write("</D:multistatus>\n");
            }
        }

        private static void WriteMultiStatusStart(TextWriter writer, List<XmlElement> properties)
        {
            if (properties.Count > 1 ||
               (properties.Count == 1 && IsPropertyName(properties[0], "deadprop-count")) ||
               (properties.Count == 1 && IsPropertyName(properties[0], "md5-checksum")))
            {
                writer.Write("<D:multistatus xmlns:D=\"DAV:\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:ns0=\"DAV:\">\n");
            }
            else
            {
                writer.Write("<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n");
            }
        }

        private void WriteProperties(INode node, List<XmlElement> properties, TextWriter output)
        {
            WriteProperties(node, properties, output, false);
        }

        private void WriteProperties(INode node, List<XmlElement> properties, TextWriter output, bool isFolder)
        {
            bool writeGetContentLengthForFolder = isFolder && PropertiesContains(properties, "getcontentlength");

            output.Write("<D:response xmlns:lp1=\"DAV:\"");
            if (!(properties.Count == 1 && IsPropertyName(properties[0], "resourcetype")))
            {
                output.Write(" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\"");
            }
            if (writeGetContentLengthForFolder)
            {
                output.Write(" xmlns:g0=\"DAV:\"");
            }
            output.Write(">\n");
            // FIXME: there's a problem with Cadaver 0.22.3 needlessly(?) having %5f encoded an underscore ('_')
            // in the PROPFIND request line, and then expecting to get back an identically encoded D:href
            // despite the transcoding not being necessary (or even annoying, since it increases URL length).
            // This shows up on "cd some_path" within Cadaver.
            // Usually clients are encouraged to do a decoding normalization
            // prior to doing any request vs. response string comparisons, however Cadaver does not seem to do so,
            // which causes all the pain and suffering in this world.
            // Thus it would be best to return D:href in exactly the same encoding as has been passed in originally.
            // However, since deep within this handler (which handles the many items that have been discovered)
            // our result handling is derived from ItemMetaData content, there's no sufficiently simple way
            // to reliably generate the exact same string of response vs. initial request,
            // for *all* the items that we indicate.
			output.Write("<D:href>" + Helper.UrlEncodeIfNecessary(node.Href(this)) + "</D:href>\n");

            XmlDocument doc = new XmlDocument();
            List<string> propertyResults = new List<string>();

            foreach (XmlElement prop in properties)
            {
                XmlElement property = doc.CreateElement(prop.LocalName, prop.NamespaceURI);
                if (!(isFolder && IsPropertyName(prop, "getcontentlength")))
                {
                    propertyResults.Add(node.GetProperty(this, property.LocalName));
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
                if (IsPropertyName(property, propertyName))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSvnRequestForProjectCreation(string requestPath, int version, TFSSourceControlProvider sourceControlProvider)
        {
            bool isSvnPath = IsSubPathOfSvnStdlayoutConvention(Helper.Decode(requestPath));
            return isSvnPath && version == sourceControlProvider.GetEarliestVersion(string.Empty);
        }

        private static bool IsSubPathOfSvnStdlayoutConvention(string itemPath)
        {
            // Check against all components of an svn standard layout (stdlayout).
            bool isStdlayoutSubPath = (itemPath.Equals("/trunk", StringComparison.OrdinalIgnoreCase)
                              || itemPath.Equals("/branches", StringComparison.OrdinalIgnoreCase)
                              || itemPath.Equals("/tags", StringComparison.OrdinalIgnoreCase));
            return isStdlayoutSubPath;
        }
    }
}
