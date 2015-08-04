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
            PropFindData propfind = GetPropFindData(request.InputStream);

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

        private static PropFindData GetPropFindData(
            Stream stream)
        {
            PropFindData propFindData;

            var streamLenPreReadBackup = stream.Length;
            // While having a request with an empty body may be somewhat rare,
            // it *is* a fully normally specified case
            // of RFC4918, "9.1 PROPFIND Method":
            // "A client may choose not to submit a request body.
            // An empty PROPFIND request body MUST be treated as if
            // it were an 'allprop' request.",
            // thus it is *not* exceptional
            // and should thus be handled regularly
            // rather than in exceptional irregular error path.
            //
            // Note that in my case
            // I hit the "empty PROPFIND request body" case *erroneously*
            // due to prior parsing failure of chunked-transfer body, though...
            bool isEmptyRequest = (0 == streamLenPreReadBackup);
            bool needHandleRFC4918EmptyPropFindAllPropSpecialCase = (isEmptyRequest);
            propFindData = needHandleRFC4918EmptyPropFindAllPropSpecialCase ?
                GetPropFindData_EmptyRequest_AllProp() :
                Helper.DeserializeXml<PropFindData>(stream);

            return propFindData;
        }

        private static PropFindData GetPropFindData_EmptyRequest_AllProp()
        {
            PropFindData propFindData = new PropFindData();

            propFindData.AllProp = new AllPropData();

            return propFindData;
        }

        private void HandleAllPropVccDefault(TFSSourceControlProvider sourceControlProvider, string requestPath, Stream stream)
        {
            int latestVersion = sourceControlProvider.GetLatestVersion();
            using (StreamWriter output = CreateStreamWriter(stream))
            {
                output.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
                output.Write("<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n");
                output.Write("<D:response xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n");
                output.Write("<D:href>" + VccPath + "</D:href>\n");
                output.Write("<D:propstat>\n");
                output.Write("<D:prop>\n");
                output.Write("<lp1:checked-in><D:href>" + GetLocalPath("/!svn/bln/" + latestVersion) + "</D:href></lp1:checked-in>\n");
                output.Write("<lp2:repository-uuid>" + sourceControlProvider.GetRepositoryUuid() + "</lp2:repository-uuid>\n");
                output.Write("<D:supportedlock>\n");
                output.Write("<D:lockentry>\n");
                output.Write("<D:lockscope><D:exclusive/></D:lockscope>\n");
                output.Write("<D:locktype><D:write/></D:locktype>\n");
                output.Write("</D:lockentry>\n");
                output.Write("</D:supportedlock>\n");
                output.Write("<D:lockdiscovery/>\n");
                output.Write("</D:prop>\n");
                output.Write("<D:status>HTTP/1.1 200 OK</D:status>\n");
                output.Write("</D:propstat>\n");
                output.Write("</D:response>\n");
                output.Write("</D:multistatus>\n");
            }
        }

        private static bool GetFolderInfo(TFSSourceControlProvider sourceControlProvider,
                                                    string depthHeader,
                                                    string itemPath,
                                                    int? version,
                                                    bool loadPropertiesFromFile,
                                                    out FolderMetaData folderInfo)
        {
            bool foundItem = false;

            folderInfo = null;

            Recursion recursion = ConvertDepthHeaderToRecursion(depthHeader);
            var versionToFetch = version.HasValue ? version.Value : TFSSourceControlProvider.LATEST_VERSION;
            if (recursion == Recursion.OneLevel)
            {
                ItemMetaData item =
                    GetItemsForProps(sourceControlProvider, versionToFetch, itemPath, recursion, loadPropertiesFromFile);
                foundItem = (null != item);
                folderInfo = (FolderMetaData)item;
            }
            else
            {
                folderInfo = new FolderMetaData();
                ItemMetaData item =
                    GetItemsForProps(sourceControlProvider, versionToFetch, itemPath, recursion, loadPropertiesFromFile);
                foundItem = (null != item);
                ItemHelpers.FolderOps_AddItem(folderInfo, item);
            }

            return foundItem;
        }

        private static ItemMetaData GetItemsForProps(TFSSourceControlProvider sourceControlProvider,
                                             int version,
                                             string itemPath,
                                             Recursion recursion,
                                             bool loadPropertiesFromFile)
        {
            // We expect itemPath to have been properly decoded already

            // FIXME BUG!?: those two calls actually map into the *same* underlying method call!!
            // Not sure about properties handling here, but if indeed it's expected
            // to have property storage items returned here, then it's probably wrong
            // (always returnPropertyFiles == false!). TODO add breakpoint here and investigate!
            if (loadPropertiesFromFile)
                return sourceControlProvider.GetItems(version, itemPath, recursion);
            else
                return sourceControlProvider.GetItemsWithoutProperties(version, itemPath, recursion);
        }

        private void HandleAllProp(TFSSourceControlProvider sourceControlProvider, string requestPath, Stream outputStream)
        {
            int revision;
            string itemPath;
            bool bcPath = false;
            // TODO: handle these two very similar types via a common helper or so.
            // Also, this section is semi-duplicated (and thus fragile)
            // in <see cref="GetHandler"/> and <see cref="PropFindHandler"/>
            // (should likely be provided by a method in request base class).
            if (requestPath.StartsWith("/!svn/"))
            {
                string itemPathUndecoded;
                if (requestPath.StartsWith("/!svn/bln"))
                {
                    revision = int.Parse(requestPath.Split('/')[3]);
                    itemPathUndecoded = requestPath.Substring("/!svn/bln/".Length + revision.ToString().Length);
                }
                else
                if (requestPath.StartsWith("/!svn/bc"))
                {
                    bcPath = true;
                    revision = int.Parse(requestPath.Split('/')[3]);
                    itemPathUndecoded = requestPath.Substring("/!svn/bc/".Length + revision.ToString().Length);
                }
                else
                if (requestPath.StartsWith("/!svn/ver"))
                {
                    revision = int.Parse(requestPath.Split('/')[3]);
                    itemPathUndecoded = requestPath.Substring("/!svn/ver/".Length + revision.ToString().Length);
                }
                else
                {
                    ReportUnsupportedSVNRequestPath(requestPath);
                    // Exception dummies:
                    itemPathUndecoded = requestPath;
                    revision = 0;
                }
                itemPath = Helper.Decode(itemPathUndecoded);
            }
            else
            {
                // There used to be a GetItems() call here simply to fetch the revision
                // to use for the subsequent main request below,
                // however I don't think that this is needed - if anything, it made matters worse
                // since it might have returned the older (*creation*) revision of the path
                // which might then below return older (non-updated)
                // corresponding properties of that path,
                // whereas a PROPFIND request most likely intends to return newest files/properties.
                // Not to mention that it did not check for null item case...
                revision = TFSSourceControlProvider.LATEST_VERSION;
                itemPath = requestPath;
            }

            ItemMetaData item = GetItemsForProps(sourceControlProvider, revision, itemPath, Recursion.None, true);

            if (item == null)
            {
                if (IsSvnRequestForProjectCreation(itemPath, revision, sourceControlProvider))
                {
                    item = GetItemsForProps(sourceControlProvider, revision, "", Recursion.None, true);
                    item.Name = "trunk";
                }

                if (item == null)
                    throw new FileNotFoundException("There is no item " + requestPath + " in revision " + revision);
            }

            using (StreamWriter output = CreateStreamWriter(outputStream))
            {
                if (item.ItemType == ItemType.Folder)
                {
                    WriteAllPropForFolder(output, requestPath, item, bcPath, sourceControlProvider);
                }
                else
                {
                    WriteAllPropForItem(output, requestPath, item, sourceControlProvider.ReadFile(item), sourceControlProvider);
                }
            }
        }

        private string GetSvnVerLocalPath(ItemMetaData item)
        {
		return GetLocalPath(SVNGeneratorHelpers.GetSvnVerFromRevisionLocation(item.Revision, item.Name, true));
        }

        private void WriteAllPropForFolder(TextWriter output, string requestPath, ItemMetaData item, bool bcPath, TFSSourceControlProvider sourceControlProvider)
        {
            output.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
            if (bcPath)
            {
                output.Write("<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n");
            }
            else
            {
                output.Write("<D:multistatus xmlns:D=\"DAV:\" xmlns:ns2=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:ns1=\"http://www.w3.org/2001/XMLSchema\" xmlns:ns0=\"DAV:\">\n");
            }
            output.Write("<D:response xmlns:S=\"http://subversion.tigris.org/xmlns/svn/\" xmlns:C=\"http://subversion.tigris.org/xmlns/custom/\" xmlns:V=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n");
            output.Write("<D:href>" + Helper.Encode(requestPath) + "/</D:href>\n");
            output.Write("<D:propstat>\n");
            output.Write("<D:prop>\n");
            foreach (var prop in item.Properties)
            {
                string elemname = prop.Key.StartsWith("svn:") ?
                    "S:" + prop.Key.Substring(4) :
                    "C:" + prop.Key;

                output.Write("<" + elemname + ">" + prop.Value + "</" + elemname + ">\n");
            }
            string svnVerLocalPath = GetSvnVerLocalPath(item);

            output.Write("<lp1:getcontenttype>text/html; charset=UTF-8</lp1:getcontenttype>\n");
            output.Write(WebDAVGeneratorHelpers.GetETag_revision_item("lp1", item.Revision, item.Name) + "\n");
            output.Write("<lp1:creationdate>" + Helper.FormatDate(item.LastModifiedDate) + "</lp1:creationdate>\n");
            output.Write("<lp1:getlastmodified>" + Helper.FormatDateB(item.LastModifiedDate) + "</lp1:getlastmodified>\n");
            output.Write("<lp1:checked-in><D:href>" + svnVerLocalPath + "</D:href></lp1:checked-in>\n");
            output.Write("<lp1:version-controlled-configuration><D:href>" + VccPath + "</D:href></lp1:version-controlled-configuration>\n");
            output.Write("<lp1:version-name>" + item.Revision + "</lp1:version-name>\n");
            output.Write("<lp1:creator-displayname>" + item.Author + "</lp1:creator-displayname>\n");
            if (item.Name != "")
            {
                output.Write("<lp2:baseline-relative-path>" + Helper.EncodeB(item.Name) + "</lp2:baseline-relative-path>\n");
            }
            else
            {
                output.Write("<lp2:baseline-relative-path/>\n");
            }
            output.Write("<lp2:repository-uuid>" + sourceControlProvider.GetRepositoryUuid() + "</lp2:repository-uuid>\n");
            output.Write("<lp2:deadprop-count>" + item.Properties.Count + "</lp2:deadprop-count>\n");
            output.Write("<lp1:resourcetype><D:collection/></lp1:resourcetype>\n");
            output.Write("<D:lockdiscovery/>\n");
            output.Write("</D:prop>\n");
            output.Write("<D:status>HTTP/1.1 200 OK</D:status>\n");
            output.Write("</D:propstat>\n");
            output.Write("</D:response>\n");
            output.Write("</D:multistatus>\n");
        }

        private void WriteAllPropForItem(TextWriter output, string requestPath, ItemMetaData item, byte[] itemData, TFSSourceControlProvider sourceControlProvider)
        {
            string svnVerLocalPath = GetSvnVerLocalPath(item);

            output.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
            output.Write("<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n");
            output.Write("<D:response xmlns:S=\"http://subversion.tigris.org/xmlns/svn/\" xmlns:C=\"http://subversion.tigris.org/xmlns/custom/\" xmlns:V=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:lp1=\"DAV:\" xmlns:lp2=\"http://subversion.tigris.org/xmlns/dav/\">\n");
            output.Write("<D:href>" + Helper.Encode(requestPath) + "</D:href>\n");
            output.Write("<D:propstat>\n");
            output.Write("<D:prop>\n");
            output.Write("<lp1:getcontentlength>" + itemData.Length + "</lp1:getcontentlength>\n");
            output.Write("<lp1:getcontenttype>text/plain</lp1:getcontenttype>\n");
            output.Write(WebDAVGeneratorHelpers.GetETag_revision_item("lp1", item.Revision, item.Name) + "\n");
            output.Write("<lp1:creationdate>" + Helper.FormatDate(item.LastModifiedDate) + "</lp1:creationdate>\n");
            output.Write("<lp1:getlastmodified>" + Helper.FormatDateB(item.LastModifiedDate) + "</lp1:getlastmodified>\n");
            output.Write("<lp1:checked-in><D:href>" + svnVerLocalPath + "</D:href></lp1:checked-in>\n");
            output.Write("<lp1:version-controlled-configuration><D:href>" + VccPath + "</D:href></lp1:version-controlled-configuration>\n");
            output.Write("<lp1:version-name>" + item.Revision + "</lp1:version-name>\n");
            output.Write("<lp1:creator-displayname>" + item.Author + "</lp1:creator-displayname>\n");
            output.Write("<lp2:baseline-relative-path>" + Helper.EncodeB(item.Name) + "</lp2:baseline-relative-path>\n");
            output.Write("<lp2:md5-checksum>" + Helper.GetMd5Checksum(itemData) + "</lp2:md5-checksum>\n");
            output.Write("<lp2:repository-uuid>" + sourceControlProvider.GetRepositoryUuid() + "</lp2:repository-uuid>\n");
            output.Write("<lp2:deadprop-count>0</lp2:deadprop-count>\n");
            output.Write("<lp1:resourcetype/>\n");
            output.Write("<D:supportedlock>\n");
            output.Write("<D:lockentry>\n");
            output.Write("<D:lockscope><D:exclusive/></D:lockscope>\n");
            output.Write("<D:locktype><D:write/></D:locktype>\n");
            output.Write("</D:lockentry>\n");
            output.Write("</D:supportedlock>\n");
            output.Write("<D:lockdiscovery/>\n");
            output.Write("</D:prop>\n");
            output.Write("<D:status>HTTP/1.1 200 OK</D:status>\n");
            output.Write("</D:propstat>\n");
            output.Write("</D:response>\n");
            output.Write("</D:multistatus>\n");
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

        /// <summary>
        /// SVN "Version Controlled Resource".
        /// </summary>
        /// <param name="sourceControlProvider"></param>
        /// <param name="requestPath">Full SVN request path</param>
        /// <param name="label">Some label</param>
        /// <param name="data">Property data to be dumped</param>
        /// <param name="outputStream">Dump sink</param>
        private void WriteVccResponse(TFSSourceControlProvider sourceControlProvider,
                                      string requestPath,
                                      string label,
                                      PropData data,
                                      Stream outputStream)
        {
            INode node = new SvnVccDefaultNode(sourceControlProvider, requestPath, label);

            using (StreamWriter output = CreateStreamWriter(outputStream))
            {
                output.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
                output.Write("<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n");
                WriteProperties(node, data.Properties, output);
                output.Write("</D:multistatus>\n");
            }
        }

        /// <summary>
        /// SVN "Baseline resource" ("bln").
        /// </summary>
        /// <param name="requestPath">Full SVN request path</param>
        /// <param name="data">Property data to be dumped</param>
        /// <param name="outputStream">Dump sink</param>
        private void WriteBlnResponse(string requestPath,
                                      PropData data,
                                      Stream outputStream)
        {
            int version = int.Parse(requestPath.Substring(10));
            INode node = new SvnBlnNode(requestPath, version);

            using (StreamWriter output = CreateStreamWriter(outputStream))
            {
                output.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
                output.Write("<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n");
                WriteProperties(node, data.Properties, output);
                output.Write("</D:multistatus>\n");
            }
        }

        /// <summary>
        /// SVN "Baseline collection resource" ("bc").
        /// </summary>
        /// <param name="sourceControlProvider">Provider</param>
        /// <param name="requestPath">Full SVN request path</param>
        /// <param name="depthHeader">WebDAV PROPFIND hierarchy depth specification</param>
        /// <param name="data">Property data to be dumped</param>
        /// <param name="outputStream">Dump sink</param>
        private void WriteBcResponse(TFSSourceControlProvider sourceControlProvider,
                                     string requestPath,
                                     string depthHeader,
                                     PropData data,
                                     Stream outputStream)
        {
            int version = int.Parse(requestPath.Split('/')[3]);
            string itemPathUndecoded = requestPath.Substring(9 + version.ToString().Length);
            string itemPath = Helper.Decode(itemPathUndecoded);
            bool setTrunkAsName = false;

            if (!sourceControlProvider.ItemExists(itemPath, version))
            {
                if (!IsSvnRequestForProjectCreation(itemPath, version, sourceControlProvider))
                    throw new FileNotFoundException();
                itemPath = "";
                setTrunkAsName = true;
            }

            FolderMetaData folderInfo;
            GetFolderInfo(sourceControlProvider, depthHeader, itemPath, version, false, out folderInfo);
            if (setTrunkAsName)
                folderInfo.Name = "trunk";

            using (StreamWriter output = CreateStreamWriter(outputStream))
            {
                output.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");

                WriteMultiStatusStart(output, data.Properties);

                if (depthHeader.Equals("1"))
                {
                    WriteBcFileNodeProperties(output, version, folderInfo, sourceControlProvider, data);
                }

                foreach (ItemMetaData item in folderInfo.Items)
                {
                    WriteBcFileNodeProperties(output, version, item, sourceControlProvider, data);
                }

                output.Write("</D:multistatus>\n");
            }

            if (_doLogFile)
            {
                string propdesc = "";
                foreach (XmlElement prop in data.Properties)
                {
                    propdesc += prop.LocalName + ":";
                }
                WriteLog(itemPathUndecoded + ":" + propdesc);
            }
        }

        private void WriteBcFileNodeProperties(StreamWriter output, int version, ItemMetaData item, TFSSourceControlProvider sourceControlProvider, PropData data)
        {
            INode node = new BcFileNode(version, item, sourceControlProvider);

            WriteProperties(node, data.Properties, output, item.ItemType == ItemType.Folder);
        }

        private void WritePathResponse(TFSSourceControlProvider sourceControlProvider,
                                       string requestPath,
                                       string depthHeader,
                                       PropData data,
                                       Stream outputStream)
        {
            string itemPathUndecoded = requestPath;
            string itemPath = Helper.Decode(itemPathUndecoded);
            if (!sourceControlProvider.ItemExists(GetLocalPathTrailingSlashStripped(itemPath), TFSSourceControlProvider.LATEST_VERSION))
            {
                throw new FileNotFoundException("Unable to find file '" + requestPath + "' in the source control repository",
                                                requestPath);
            }

            FolderMetaData folderInfo;
            GetFolderInfo(sourceControlProvider, depthHeader, itemPath, null, true, out folderInfo);

            using (StreamWriter output = CreateStreamWriter(outputStream))
            {
                output.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");

                WriteMultiStatusStart(output, data.Properties);

                foreach (ItemMetaData item in folderInfo.Items)
                {
                    INode node = new FileNode(item, sourceControlProvider);

                    WriteProperties(node, data.Properties, output, item.ItemType == ItemType.Folder);
                }

                output.Write("</D:multistatus>\n");
            }
        }

        /// <summary>
        /// SVN "Working resource" ("wrk").
        /// </summary>
        /// <param name="sourceControlProvider">Provider</param>
        /// <param name="requestPath">Full SVN request path</param>
        /// <param name="depthHeader">WebDAV PROPFIND hierarchy depth specification</param>
        /// <param name="data">Property data to be dumped</param>
        /// <param name="outputStream">Dump sink</param>
        private void WriteWrkResponse(TFSSourceControlProvider sourceControlProvider,
                                       string requestPath,
                                       string depthHeader,
                                       PropData data,
                                       Stream outputStream)
        {
            string activityId = requestPath.Split('/')[3];
            if (!(depthHeader.Equals("0")))
            {
                ReportUnsupportedDepthHeaderValue(depthHeader);
            }
            string itemPathUndecoded = requestPath.Substring(11 + activityId.Length);
            string itemPath = Helper.Decode(itemPathUndecoded);
            ItemMetaData item = sourceControlProvider.GetItemInActivity(activityId, itemPath);

            if (item == null)
            {
                throw new FileNotFoundException("Unable to find file '" + itemPathUndecoded + "' in the specified activity", itemPathUndecoded);
            }

            using (StreamWriter output = CreateStreamWriter(outputStream))
            {
                output.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
                WriteMultiStatusStart(output, data.Properties);
                INode node = new FileNode(item, sourceControlProvider);
                WriteProperties(node, data.Properties, output, item.ItemType == ItemType.Folder);
                output.Write("</D:multistatus>\n");
            }
        }

        private static void WriteMultiStatusStart(TextWriter output, List<XmlElement> properties)
        {
            if (properties.Count > 1 ||
               (properties.Count == 1 && IsPropertyName(properties[0], "deadprop-count")) ||
               (properties.Count == 1 && IsPropertyName(properties[0], "md5-checksum")))
            {
                output.Write("<D:multistatus xmlns:D=\"DAV:\" xmlns:ns1=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:ns0=\"DAV:\">\n");
            }
            else
            {
                output.Write("<D:multistatus xmlns:D=\"DAV:\" xmlns:ns0=\"DAV:\">\n");
            }
        }

        // TODO: rather than having a WriteProperties() directly within this class,
        // with a *very dirty* INode-abstraction-violating isFolder param,
        // one should have a nice derived class hierarchy with WebDAVNodeGenerator naming
        // (which should be based on something like a WebDAVGeneratorBase),
        // in combination with a per-INode-type factory method within this class
        // which constructs the proper node generator that's needed for the specific node type.
        // And then one would also be able to get rid of awkwardly keeping the provider object near generator stage
        // (a stream generator should be making use of nothing but static protocol data).
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
            var propertyResultsInitialCapacity = properties.Count;
            List<string> propertyResults = new List<string>(propertyResultsInitialCapacity);

            foreach (XmlElement prop in properties)
            {
                XmlElement property = doc.CreateElement(prop.LocalName, prop.NamespaceURI);
                if (!(isFolder && IsPropertyName(prop, "getcontentlength")))
                {
                    string propertyResult = node.GetProperty(this, property.LocalName); // debug helper var
                    propertyResults.Add(propertyResult);
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

        private static bool IsSvnRequestForProjectCreation(string itemPath, int version, TFSSourceControlProvider sourceControlProvider)
        {
            bool isSvnPath = IsSubPathOfSvnStdlayoutConvention(itemPath);
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

        private static bool DetermineSkipNotFoundResults(IHttpRequest request)
        {
            return DetermineBriefOutput(request);
        }
    }
}
