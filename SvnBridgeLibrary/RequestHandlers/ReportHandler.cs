using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Xml;
using CodePlex.TfsLibrary.ObjectModel; // LogItem, SourceItem, SourceItemChange
using CodePlex.TfsLibrary.RepositoryWebSvc; // ChangeType, ItemType
using SvnBridge.Infrastructure;
using SvnBridge.Interfaces;
using SvnBridge.Protocol;
using SvnBridge.SourceControl;
using SvnBridge.Utility; // Helper.EncodeB() etc.

namespace SvnBridge.Handlers
{
    // Use a Dictionary to fake a HashSet (.NET >= 3.5 only).
    // Not using our local SvnBridge HashSet impl here either... (yet?)
    using ChangesDict = Dictionary<int, bool>;

    /// <remarks>
    /// TODO: should possibly be developed into a class hierarchy for various item operations.
    /// </remarks>
    public class SvnReportHelpers
    {
        public static string FormatNodeKindAttribute(
            SourceItem item)
        {
            return FormatQuotedAttribute(
                "node-kind",
                GetNodeKind(
                    item));
        }
        public static string FormatQuotedAttribute(
            string attr_key,
            string attr_val)
        {
            return attr_key + "=\"" + attr_val + "\"";
        }

        public static string FormatAbsolutePathString(
            string path)
        {
            return "/" + Helper.EncodeB(
                path);
        }

        private static string GetNodeKind(
            SourceItem item)
        {
            return GetNodeKind(
                item.ItemType);
        }

        /// <remarks>
        /// Node kind choices seem to be:
        /// "node" / "file" / "dir" / "symlink" / "unknown"
        /// (gathered from subversion/libsvn_subr/types.c).
        /// </remarks>
        private static string GetNodeKind(
            ItemType itemType)
        {
            string svn_node_kind = "none"; // hmm... where to use "none" and where "unknown"?

            switch(itemType)
            {
              case ItemType.Folder:
                  svn_node_kind = "dir";
                  break;
              case ItemType.File:
                  svn_node_kind = "file";
                  break;
              case ItemType.Any:
              default:
                  svn_node_kind = "unknown";
                  break;
            }

            return svn_node_kind;
        }
    }

    public class ReportHandler : RequestHandlerBase
    {
        protected AsyncItemLoader loader;

        protected override void Handle(
            IHttpContext context,
            TFSSourceControlProvider sourceControlProvider,
            StreamWriter output)
        {
            IHttpRequest request = context.Request;
            IHttpResponse response = context.Response;
            string requestPath = GetPath(request);

            using (XmlReader reader = XmlReader.Create(request.InputStream, Helper.InitializeNewXmlReaderSettings()))
            {
                reader.MoveToContent();
                object data = null; // need to have this as a central variable for the error-path logging below
                try
                {
                    ConfigureResponse_SendChunked();

                    // At least for now, we'll decide to not implement via switch/case -
                    // this is less flexible than if/else conditionals.

                    if (reader.NamespaceURI == WebDav.Namespaces.SVN && reader.LocalName == "get-locks-report")
                    {
                        SetResponseSettings(response, "text/xml; charset=\"utf-8\"", Encoding.UTF8, 200);
                            GetLocksReport(output);
                    }
                    else if (reader.NamespaceURI == WebDav.Namespaces.SVN && reader.LocalName == "update-report")
                    {
                        UpdateReportData data_UpdateReport = Helper.DeserializeXml<UpdateReportData>(reader);
                        data = data_UpdateReport;
                        int targetRevision;
                        FolderMetaData update = GetMetadataForUpdate(request, data_UpdateReport, sourceControlProvider, out targetRevision);
                        if (null != update)
                        {
                            SetResponseSettings(response, "text/xml; charset=\"utf-8\"", Encoding.UTF8, 200);
                                UpdateReport(sourceControlProvider, data_UpdateReport, output, update, targetRevision);
                        }
                        else
                        {
                            SendTargetDoesNotExistResponse(response, output);
                        }
                    }
                    else if (reader.NamespaceURI == WebDav.Namespaces.SVN && reader.LocalName == "replay-report")
                    {
                        ReplayReportData data_ReplayReport = Helper.DeserializeXml<ReplayReportData>(reader);
                        data = data_ReplayReport;
                        ReplayReport(request, response, sourceControlProvider, data_ReplayReport, output);
                    }
                    else if (reader.NamespaceURI == WebDav.Namespaces.SVN && reader.LocalName == "log-report")
                    {
                        LogReportData data_LogReport = Helper.DeserializeXml<LogReportData>(reader);
                        data = data_LogReport;
                        SetResponseSettings(response, "text/xml; charset=\"utf-8\"", Encoding.UTF8, 200);
                        response.BufferOutput = false;
                            LogReport(sourceControlProvider, data_LogReport, requestPath, output);
                    }
                    else if (reader.NamespaceURI == WebDav.Namespaces.SVN && reader.LocalName == "get-locations")
                    {
                        GetLocationsReportData data_GetLocationsReport = Helper.DeserializeXml<GetLocationsReportData>(reader);
                        data = data_GetLocationsReport;
                        SetResponseSettings(response, "text/xml; charset=\"utf-8\"", Encoding.UTF8, 200);
                            GetLocationsReport(sourceControlProvider, data_GetLocationsReport, requestPath, output);
                    }
                    else if (reader.NamespaceURI == WebDav.Namespaces.SVN && reader.LocalName == "dated-rev-report")
                    {
                        DatedRevReportData data_DatedRevReport = Helper.DeserializeXml<DatedRevReportData>(reader);
                        data = data_DatedRevReport;
                        SetResponseSettings(response, "text/xml; charset=\"utf-8\"", Encoding.UTF8, 200);
                            GetDatedRevReport(sourceControlProvider, data_DatedRevReport, output);
                    }
                    else if (reader.NamespaceURI == WebDav.Namespaces.SVN && reader.LocalName == "file-revs-report")
                    {
                        FileRevsReportData data_FileRevsReport = Helper.DeserializeXml<FileRevsReportData>(reader);
                        data = data_FileRevsReport;
                        string serverPath = GetServerSidePath(requestPath);
                        SendBlameResponse(request, response, sourceControlProvider, serverPath, data_FileRevsReport, output);
                        return;
                    }
                    else
                    {
                        SendUnknownReportResponse(response, output);
                    }
                    //if (data != null)
                    //{
                    //    OnErrorRetainRequestInfo_RequestBody(data);
                    //    DefaultLogger logger = Container.Resolve<DefaultLogger>();
                    //    logger.ErrorFullDetails(new Exception("Logging"), context);
                    //}
                }
                catch
                {
                    OnErrorRetainRequestInfo_RequestBody(data);
                    throw;
                }
            }
        }

        private static void SendUnknownReportResponse(IHttpResponse response, StreamWriter output)
        {
            SetResponseSettings(response, "text/xml; charset=\"utf-8\"", Encoding.UTF8, (int)HttpStatusCode.NotImplemented);
            response.AppendHeader("Connection", "close");

            WriteHumanReadableError(output, 200007, "The requested report is unknown."); // yup, _with_ trailing dot.
            return;
        }

        private static void SendTargetDoesNotExistResponse(IHttpResponse response, StreamWriter output)
        {
            SetResponseSettings(response, "text/xml; charset=\"utf-8\"", Encoding.UTF8, (int)HttpStatusCode.InternalServerError);
            response.AppendHeader("Connection", "close");

            WriteHumanReadableError(output, 160005, "Target path does not exist"); // yup, _without_ trailing dot.
            return;
        }

        private void ReplayReport(IHttpRequest request, IHttpResponse response, TFSSourceControlProvider sourceControlProvider, ReplayReportData replayReport, StreamWriter output)
        {
            if (replayReport.Revision == 0)
            {
                response.StatusCode = (int) HttpStatusCode.OK;
                output.Write(
                        @"<?xml version=""1.0"" encoding=""utf-8""?>
<S:editor-report xmlns:S=""svn:"">
<S:target-revision rev=""0""/>
</S:editor-report>");
                return;
            }

            var data = new UpdateReportData();
            data.SrcPath = request.Url.AbsoluteUri;
            data.Entries = new List<EntryData>();
            var item = new EntryData();
            string localPath = PathParser.GetLocalPath(request);
            LogItem log = sourceControlProvider.GetLog(
                localPath,
                0,
                sourceControlProvider.GetLatestVersion(),
                Recursion.None,
                1);
            if (log.History.Length == 0)
            {
                WriteFileNotFoundResponse(request, response, output);
            }

            item.Rev = (replayReport.Revision - 1).ToString();
            data.TargetRevision = (replayReport.Revision).ToString();
            data.Entries.Add(item);
            SetResponseSettings(response, "text/xml; charset=\"utf-8\"", Encoding.UTF8, 200);
            response.SendChunked = true;
                try
                {
                    output.Write(@"<?xml version=""1.0"" encoding=""utf-8""?>
<S:editor-report xmlns:S=""svn:"">");

                    int targetRevision;
                    FolderMetaData metadata = GetMetadataForUpdate(request, data, sourceControlProvider,
                                                                   out targetRevision);
                    output.WriteLine("<S:target-revision rev=\"{0}\"/>", targetRevision);

                    OutputEditorReport(sourceControlProvider, metadata, replayReport.Revision,
                                       localPath.Equals("/"),
                                       output);

                    output.Write("</S:editor-report>");
                }
                catch (FileNotFoundException)
                {
                    WriteFileNotFoundResponse(request, response, output);
                }
        }

        private void OutputEditorReport(TFSSourceControlProvider sourceControlProvider, FolderMetaData folder, int revision, bool isRoot, StreamWriter output)
        {
            if (isRoot)
            {
                output.WriteLine("<S:open-root rev=\"-1\"/>");
            }
            else if (sourceControlProvider.ItemExists(folder.Name, revision - 1))
            {
                output.Write("<S:open-directory name=\"{0}\" rev=\"-1\" />\n", Helper.EncodeB(folder.Name));
            }
            else
            {
                output.Write("<S:add-directory name=\"{0}\" />\n", Helper.EncodeB(folder.Name));
            }

            foreach (var property in folder.Properties)
            {
                output.Write("<S:change-dir-prop name=\"{0}\">{1}\n", property.Key, property.Value);
                output.Write("</S:change-dir-prop>\n");
            }

            TimeSpan spanLoadTimeout = TimeSpan.FromHours(2);
            foreach (ItemMetaData item in folder.Items)
            {
                if (item.ItemRevision != revision)
                    continue;
                if (item is DeleteMetaData || item is DeleteFolderMetaData)
                {
                    output.Write("<S:delete-entry name=\"{0}\" rev=\"-1\"/>\n", Helper.EncodeB(item.Name));
                    continue;
                }

                if (item.ItemType == ItemType.Folder)
                {
                    OutputEditorReport(sourceControlProvider, (FolderMetaData) item, revision, false, output);
                }
                else
                {
                    if (sourceControlProvider.ItemExists(item.Name, revision - 1))
                    {
                        output.Write("<S:open-file name=\"{0}\" rev=\"-1\"/>\n", Helper.EncodeB(item.Name));
                    }
                    else
                    {
                        output.Write("<S:add-file name=\"{0}\"/>\n", Helper.EncodeB(item.Name));
                    }

                    FlushWriter(output);

                    Stream base64DiffDataStream;
                    string md5Hash;
                    // This may throw exceptions -
                    // I believe we can simply leave them rippling through unhandled,
                    // since this will likely properly terminate output stream generation.
                    loader.RobItemData(
                        item,
                        spanLoadTimeout,
                        out base64DiffDataStream,
                        out md5Hash);

                    output.Write("<S:apply-textdelta>");
                    // KEEP THIS WRITE ACTION SEPARATE! (avoid huge-string alloc):
                    URSHelpers.PushTxDeltaData(
                        output,
                        base64DiffDataStream);
                    output.Write("\n"); // \n EOL belonging to entire line (XML elem start plus payload)
                    output.Write("</S:apply-textdelta>\n");
                    output.Write("<S:close-file checksum=\"{0}\"/>\n", md5Hash);
                }
            }
            output.Write("<S:close-directory/>\n");
        }

        private void SendBlameResponse(IHttpRequest request, IHttpResponse response, TFSSourceControlProvider sourceControlProvider, string serverPath, FileRevsReportData data, StreamWriter output)
        {
            LogItem log = sourceControlProvider.GetLog(
                serverPath,
                data.StartRevision,
                data.EndRevision,
                // SVNBRIDGE_WARNING_REF_RECURSION - additional comments:
                // since SVN blame activity is a single-object (e.g. file) activity
                // (and we did end up with bogus unrelated files listed!),
                // we definitely should not specify .Full here.
                Recursion.None,
                data.EndRevision - data.StartRevision);

            if (log.History.Length == 0)
            {
                WriteFileNotFoundResponse(request, response, output);
            }

            foreach (SourceItemHistory history in log.History)
            {
                foreach (SourceItemChange change in history.Changes)
                {
                    if (change.Item.ItemType == ItemType.Folder)
                    {
                        SendErrorResponseCannotRunBlameOnFolder(response, serverPath, output);
                        return;
                    }
                }
            }
                response.StatusCode = (int) HttpStatusCode.OK;
                output.Write(
                    @"<?xml version=""1.0"" encoding=""utf-8""?>
<S:file-revs-report xmlns:S=""svn:"" xmlns:D=""DAV:"">");

                var historySorted = Helper.SortHistories(true, log.History);
                foreach (SourceItemHistory history in historySorted)
                {
                    foreach (SourceItemChange change in history.Changes)
                    {
                        ItemMetaData item = sourceControlProvider.GetItems(change.Item.RemoteChangesetId, change.Item.RemoteName, Recursion.None);

                        output.Write(@"<S:file-rev path=""" + change.Item.RemoteName + @""" rev=""" +
                            change.Item.RemoteChangesetId + @""">
                            <S:rev-prop name=""svn:log"">" + Helper.EncodeB(history.Comment) + @"</S:rev-prop>
                            <S:rev-prop name=""svn:author"">" + history.Username + @"</S:rev-prop>
                            <S:rev-prop name=""svn:date"">" + Helper.FormatDate(change.Item.RemoteDate) + @"</S:rev-prop>
                            "
                        );
                        StreamItemDataAsTxDeltaElem(
                            output,
                            sourceControlProvider,
                            item);
                        output.Write("</S:file-rev>");
                    }
                }
                output.Write("</S:file-revs-report>");
        }

        private static void StreamItemDataAsTxDeltaElem(
            StreamWriter output,
            TFSSourceControlProvider sourceControlProvider,
            ItemMetaData item)
        {
            output.Write("<S:txdelta>");
            // KEEP THIS WRITE ACTION SEPARATE! (avoid huge-string alloc):
            URSHelpers.StreamItemDataAsTxDelta(
                output,
                sourceControlProvider,
                item);
            // NOTE: while other tx-delta generators have a trailing \n here,
            // this one doesn't
            // (likely doesn't need to, since for unknown reasons
            // the *whole* XML content here does not have \n EOL).
            output.Write("</S:txdelta>");
        }

        private static void SendErrorResponseCannotRunBlameOnFolder(IHttpResponse response, string serverPath, StreamWriter output)
        {
            response.StatusCode = (int) HttpStatusCode.InternalServerError;
            response.ContentType = "text/xml; charset=\"utf-8\"";
            string error_string = "'" + serverPath + "' is not a file"; // yup, _without_ trailing dot.
            WriteHumanReadableError(output, 160017, error_string);
            return;
        }

        private static void GetDatedRevReport(TFSSourceControlProvider sourceControlProvider, DatedRevReportData data, TextWriter output)
        {
            int targetRevision = sourceControlProvider.GetVersionForDate(data.CreationDate);

            output.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
            output.Write("<S:dated-rev-report xmlns:S=\"svn:\" xmlns:D=\"DAV:\">\n");
            output.Write("<D:version-name>");
            output.Write(targetRevision);
            output.Write("</D:version-name></S:dated-rev-report>");
        }

        private static void GetLocksReport(StreamWriter output)
        {
            output.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
            output.Write("<S:get-locks-report xmlns:S=\"svn:\" xmlns:D=\"DAV:\">\n");
            output.Write("</S:get-locks-report>\n");
        }

        private static void GetLocationsReport(TFSSourceControlProvider sourceControlProvider, GetLocationsReportData getLocationsReport, string requestPath, StreamWriter output)
        {
            string serverPath = GetServerSidePath(requestPath);

            output.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
            output.Write("<S:get-locations-report xmlns:S=\"svn:\" xmlns:D=\"DAV:\">\n");
            foreach (string locationRevision in getLocationsReport.LocationRevision)
            {
                ItemMetaData item = sourceControlProvider.GetItemsWithoutProperties(int.Parse(locationRevision), serverPath, Recursion.None);
                if (item != null)
                {
                    output.Write("<S:location rev=\"" + locationRevision + "\" path=\"" + serverPath + "\"/>\n");
                }
            }

            output.Write("</S:get-locations-report>\n");
        }

        private void UpdateReport(TFSSourceControlProvider sourceControlProvider, UpdateReportData updatereport, StreamWriter output, FolderMetaData metadata, int targetRevision)
        {
            // See also UpdateReportService impl!!
            string sendAllSetting = (updatereport.SendAll == true) ? "true" : "false";
            output.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
            output.Write("<S:update-report xmlns:S=\"svn:\" xmlns:V=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:D=\"DAV:\" send-all=\"" + sendAllSetting + "\">\n");
            output.Write("<S:target-revision rev=\"" + targetRevision + "\"/>\n");

            UpdateReportService updateReportService = new UpdateReportService(this, sourceControlProvider, loader);
            var srcPath = DetermineSrcPath(this, updatereport);
            updateReportService.ProcessUpdateReport(updatereport, srcPath, metadata, output);

            output.Write("</S:update-report>\n");
        }

		private static string DetermineSrcPath(RequestHandlerBase handler, UpdateReportData updateReportRequest)
		{
			string srcPath = handler.GetLocalPathFromUrl(updateReportRequest.SrcPath);
			if (updateReportRequest.UpdateTarget != null)
				return srcPath + "/" + updateReportRequest.UpdateTarget;
			return srcPath;
		}

        /// <summary>
        /// For non-null UpdateTarget cases,
        /// returns the subset of the entire directory tree that we're interested in.
        /// FIXME: move this to a common infrastructure file,
        /// to also be used by UpdateTarget handling done in <see cref="TFSSourceControlProvider"/>.
        /// </summary>
        /// <param name="rootFolder">Root folder of the hierarchy to be actively filtered (item removal)</param>
        /// <param name="targetPath">Sub path to be preserved</param>
        private static void FilterUpdateTargetSpecificTree(ref FolderMetaData rootFolder, string targetPath)
        {
            // [cannot easily use List.RemoveAll() here]
            foreach (ItemMetaData item in new List<ItemMetaData>(rootFolder.Items))
            {
                if (item.IsBelowEqual(targetPath))
                {
                    if (item.ItemType == ItemType.Folder)
                    {
                        FolderMetaData folder = (FolderMetaData)item;
                        int idxExisting = rootFolder.Items.IndexOf(item);
                        FilterUpdateTargetSpecificTree(ref folder, targetPath);
                        rootFolder.Items[idxExisting] = folder;
                    }
                }
                else
                    rootFolder.Items.Remove(item);
            }
        }

        private FolderMetaData GetMetadataForUpdate(IHttpRequest request, UpdateReportData updatereport, TFSSourceControlProvider sourceControlProvider, out int targetRevision)
        {
            string basePath = PathParser.GetLocalPath(request, updatereport.SrcPath);
            FolderMetaData metadata;
            if (updatereport.TargetRevision != null)
            {
                targetRevision = int.Parse(updatereport.TargetRevision);
            }
            else
            {
                // While you'd think one could simply assign the LATEST_VERSION magic here
                // (would then cause the provider to query the actual version value internally),
                // that would be terribly wrong
                // since targetRevision here is an *out* param for further use by the caller,
                // thus we are the ones to actively figure out the correct value for the caller.
                targetRevision = sourceControlProvider.GetLatestVersion();
            }
            if (updatereport.IsCheckOut)
            {
                // SVNBRIDGE_WARNING_REF_RECURSION
                metadata = (FolderMetaData)sourceControlProvider.GetItemsWithoutProperties(targetRevision, basePath, Recursion.Full);
            }
            else
            {
                int versionFrom = int.Parse(updatereport.Entries[0].Rev);
                metadata = sourceControlProvider.GetChangedItems(
                    basePath,
                    versionFrom,
                    targetRevision,
                    updatereport);
            }
            if (metadata != null)
            {
                // Handle the case where UpdateTarget != null
                // (occurs e.g. when doing "svn up some_sub_dir").
                // In this case we need to return a *subset* of the tree,
                // to contain only all those tree items which are specific to the UpdateTarget sub item expression requested.
                // We'll certainly choose to do this filtering up-front
                // rather than adding expensive checks evaluated within each recursion step of subsequent processing.
                //
                // FIXME: GetChangedItems() above already has its own UpdateTarget-specific tree handling, too.
                // Should think of how to cleanly merge this mechanism.
                if (updatereport.UpdateTarget != null)
                {
                    string targetPath = DetermineSrcPath(this, updatereport);
                    FilterUpdateTargetSpecificTree(ref metadata, targetPath);
                }

                // Start async crawler thread which will populate our
                // existing metadata items with actual-data base64 content:
                loader = new AsyncItemLoader(metadata, sourceControlProvider, Helper.GetCacheBufferTotalSizeRecommendedLimit());
                ThreadPool.QueueUserWorkItem(state => loader.Start());
            }
            return metadata;
        }

        public override void Cancel()
        {
            if (loader != null)
                loader.Cancel();
            base.Cancel();
        }

        private static void LogReport(TFSSourceControlProvider sourceControlProvider, LogReportData logreport, string requestPath, TextWriter output)
        {
            string serverPath = GetServerSidePath(requestPath);

            int end = int.Parse(logreport.EndRevision);
            int start = int.Parse(logreport.StartRevision);
            int maxCount = (null != logreport.Limit) ? int.Parse(logreport.Limit) : 1000000;
            // SVNBRIDGE_WARNING_REF_RECURSION
            LogItem logItem = sourceControlProvider.GetLog(
                serverPath,
                Math.Min(start, end),
                Math.Max(start, end),
                Recursion.Full,
                maxCount,
                (start < end));

            // Testing of svn log -v -r BEGIN:END vs. non-[-v]
            // showed that adding -v
            // causes subversion to add a discover-changed-paths request property (<S:discover-changed-paths/> element) -
            // while real Subversion (1.6.17) server did honour (non-)existence of this property,
            // SvnBridge didn't (it always added path information). Doh.
            bool discoverChangedPaths = (logreport.DiscoverChangedPaths != null);
            // TODO: we currently completely ignore strict-node-history request property -
            // we don't even have a logreport.StrictNodeHistory member yet...
            // http://docs.sharpsvn.net/current/html/P_SharpSvn_SvnLogArgs_StrictNodeHistory.htm
            // http://subversion.tigris.org/ds/viewMessage.do?dsForumId=495&dsMessageId=975874
            // "[PATCH]implement a FIXME for handling moved paths in copyfrom_info_receiver"
            //   http://comments.gmane.org/gmane.comp.version-control.subversion.devel/88310
            // "How to read merge-history?"
            //   http://subversion.1072662.n5.nabble.com/How-to-read-merge-history-td3520.html
            // "svn commit: r29751 - trunk/subversion/libsvn_repos"
            //   http://comments.gmane.org/gmane.comp.version-control.subversion.svn/25956
            // http://tortoisesvn.googlecode.com/svn/trunk/src/LogCache/CacheLogQuery.cpp


            bool requestInclusionOfPathChanges = (discoverChangedPaths);

            bool needProcessFilterImplicitChanges = false;
            if (requestInclusionOfPathChanges)
            {
                needProcessFilterImplicitChanges = true;
            }

            if (needProcessFilterImplicitChanges)
            {
                // For debugging usability purposes
                // (definitely remain able to *directly compare* prior/post state),
                // keep a copy of the unfiltered original data
                // if so requested (performance optimization):
                LogItem logItemOrig;
                bool keepBackup = false; // debug toggle helper
                if (keepBackup)
                {
                    logItemOrig = TfsLibraryHelpers.LogItemClone(logItem);
                }

                // FIXME layering violation!?: the changes should actually have been filtered already -
                // this is supposed to be a simple reporting-only class,
                // yet the input data should be in suitable format already,
                // thus our SVN protocol provider class ought to have done that.
                // Unless Changes usually *should* contain unfiltered content
                // for use by SVN generators and REPORT happens to be
                // the only component which actually requires filtering...
                FilterImplicitLogItemHistoryChanges(ref logItem);
            }

            LogReportFromLogItem(logItem, output, discoverChangedPaths);
        }

        private static void FilterImplicitLogItemHistoryChanges(ref LogItem logItem)
        {
            var sourceItemHistories = logItem.History;
            foreach (SourceItemHistory history in sourceItemHistories)
            {
                // We'll keep the filtering conditionals part inline,
                // since we access .Count, which requires ICollection at least
                // (would be less suitable to pass as a sub method param).
                bool needFiltering = (history.Changes.Count > 1);
                if (needFiltering) // optimization shortcut
                {
                    FilterImplicitChanges(ref history.Changes);
                }
            }
        }

        /// <summary>
        /// Pre-filters entries, to achieve properly compliant SVN-side output.
        /// Example: deleted/renamed directories need to end up as a
        /// single action on the directory (as expected by SVN clients)
        /// rather than incompatibly additionally listing all individual sub files, too!
        /// </summary>
        /// <param name="changesFiltered">Reference to original (unfiltered) list of changes (will get filtered in-place when needed)</param>
        /// <returns>Nothing</returns>
        private static void FilterImplicitChanges(ref List<SourceItemChange> changesFiltered)
        {
            ChangesDict dictToBeRemoved = DetermineImplicitChanges(changesFiltered);
            bool needFiltering = (dictToBeRemoved.Count > 0);
            if (needFiltering)
            {
                changesFiltered.RemoveAll(elem => (dictToBeRemoved.ContainsKey(elem.GetHashCode())));
            }
        }

        private static ChangesDict DetermineImplicitChanges(IEnumerable<SourceItemChange> changes)
        {
            ChangesDict dictToBeRemoved = new ChangesDict();
            foreach (SourceItemChange change in changes)
            {
                if (!(change.Item.ItemType == ItemType.Folder))
                {
                    continue;
                }

                // Hmm, perhaps Undelete should be handled here as well?
                // (perhaps it also reincarnates an entire folder
                // without having to list any sub entries?)
                if ((change.ChangeType & ChangeType.Delete) == ChangeType.Delete)
                {
                    SourceItem itemFolder = change.Item;
                    string folderRemoteName = itemFolder.RemoteName;
                    // Remove all within-folder deletion-type changes,
                    // since they're redundant on SVN protocol:
                    foreach (SourceItemChange changeVictim in changes)
                    {
                        if (!((changeVictim.ChangeType & ChangeType.Delete) == ChangeType.Delete))
                        {
                            continue;
                        }
                        SourceItem itemVictim = changeVictim.Item;
                        if (IsBelowBaseFolder(folderRemoteName, itemVictim.RemoteName))
                        {
                            // Make sure we skip removing ourselves...
                            if (itemFolder != itemVictim)
                            {
                                // Add() would throw exception when pre-existing...
                                dictToBeRemoved[changeVictim.GetHashCode()] = true;
                            }
                        }
                    }
                }
                else
                if ((change.ChangeType & ChangeType.Rename) == ChangeType.Rename)
                {
                    RenamedSourceItem itemFolder = (RenamedSourceItem)change.Item;
                    string folderOriginalRemoteName = itemFolder.OriginalRemoteName;
                    string folderRemoteName = itemFolder.RemoteName;
                    foreach (SourceItemChange changeVictim in changes)
                    {
                        if (!((changeVictim.ChangeType & ChangeType.Rename) == ChangeType.Rename))
                        {
                            continue;
                        }
                        RenamedSourceItem itemVictim = (RenamedSourceItem)changeVictim.Item;
                        if (
                               (IsBelowBaseFolder(folderOriginalRemoteName, itemVictim.OriginalRemoteName))
                            && (IsBelowBaseFolder(folderRemoteName, itemVictim.RemoteName))
                        )
                        {
                            // Make sure we skip removing ourselves...
                            if (itemFolder != itemVictim)
                            {
                                // Add() would throw exception when pre-existing...
                                dictToBeRemoved[changeVictim.GetHashCode()] = true;
                            }
                        }
                    }
                }
            }
            return dictToBeRemoved;
        }

        private static bool IsBelowBaseFolder(string itemBaseFolder, string itemCandidate)
        {
            // FIXME: should do this path check in a _precise_ manner
            // (compare against trailing-'/' strings).
            return itemCandidate.StartsWith(itemBaseFolder);
        }

        private static void LogReportFromLogItem(LogItem logItem, TextWriter output, bool discoverChangedPaths)
        {
            output.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
            output.Write("<S:log-report xmlns:S=\"svn:\" xmlns:D=\"DAV:\">\n");

            var sourceItemHistories = logItem.History;
            foreach (SourceItemHistory history in sourceItemHistories)
            {
                output.Write("<S:log-item>\n");
                output.Write("<D:version-name>" + history.ChangeSetID + "</D:version-name>\n");
                output.Write("<D:creator-displayname>" + history.Username + "</D:creator-displayname>\n");
                output.Write("<S:date>" + Helper.FormatDate(history.CommitDateTime) + "</S:date>\n");
                output.Write("<D:comment>" + Helper.EncodeB(history.Comment) + "</D:comment>\n");

                if (discoverChangedPaths)
                {
                    LogReportChangedPaths(history.Changes, output);
                }

                output.Write("</S:log-item>\n");
            }
            output.Write("</S:log-report>\n");
        }

        private static void LogReportChangedPaths(IEnumerable<SourceItemChange> changes, TextWriter output)
        {
            foreach (SourceItemChange change in changes)
            {
                SourceItem item = change.Item;
                if ((change.ChangeType & ChangeType.Add) == ChangeType.Add ||
                    (change.ChangeType & ChangeType.Undelete) == ChangeType.Undelete)
                {
                    output.Write("<S:added-path " +
                    SvnReportHelpers.FormatNodeKindAttribute(item) +
                    ">" +
                    SvnReportHelpers.FormatAbsolutePathString(item.RemoteName) +
                    "</S:added-path>\n");
                }
                else if ((change.ChangeType & ChangeType.Edit) == ChangeType.Edit)
                {
                    output.Write("<S:modified-path " +
                    SvnReportHelpers.FormatNodeKindAttribute(item) +
                    ">" +
                    SvnReportHelpers.FormatAbsolutePathString(item.RemoteName) +
                    "</S:modified-path>\n");
                }
                else if ((change.ChangeType & ChangeType.Delete) == ChangeType.Delete)
                {
                    output.Write("<S:deleted-path " +
                    SvnReportHelpers.FormatNodeKindAttribute(item) +
                    ">" +
                    SvnReportHelpers.FormatAbsolutePathString(item.RemoteName) +
                    "</S:deleted-path>\n");
                }
                else if ((change.ChangeType & ChangeType.Rename) == ChangeType.Rename)
                {
                    var renamedItem = (RenamedSourceItem)item;
                    output.Write(
                        "<S:added-path " +
                        SvnReportHelpers.FormatQuotedAttribute(
                          "copyfrom-path",
                          SvnReportHelpers.FormatAbsolutePathString(renamedItem.OriginalRemoteName)) + " " +
                        SvnReportHelpers.FormatQuotedAttribute(
                          "copyfrom-rev",
                          renamedItem.OriginalRevision.ToString()) + " " +
                        SvnReportHelpers.FormatNodeKindAttribute(renamedItem) +
                        ">" +
                        SvnReportHelpers.FormatAbsolutePathString(item.RemoteName) +
                        "</S:added-path>\n");
                    output.Write("<S:deleted-path " +
                        SvnReportHelpers.FormatNodeKindAttribute(renamedItem) +
                        ">" +
                        SvnReportHelpers.FormatAbsolutePathString(renamedItem.OriginalRemoteName) +
                                 "</S:deleted-path>\n");
                }
                else if ((change.ChangeType & ChangeType.Branch) == ChangeType.Branch)
                {
                    var renamedItem = (RenamedSourceItem)item;
                    output.Write(
                        "<S:added-path " +
                        SvnReportHelpers.FormatQuotedAttribute(
                          "copyfrom-path",
                          SvnReportHelpers.FormatAbsolutePathString(renamedItem.OriginalRemoteName)) + " " +
                        SvnReportHelpers.FormatQuotedAttribute(
                          "copyfrom-rev",
                          renamedItem.OriginalRevision.ToString()) + " " +
                        SvnReportHelpers.FormatNodeKindAttribute(renamedItem) +
                        ">" +
                        SvnReportHelpers.FormatAbsolutePathString(item.RemoteName) +
                        "</S:added-path>\n");
                }
                else if (change.ChangeType == ChangeType.Merge)
                {
                    // Ignore merge entries that are not an add, edit, delete, or rename
                }
                else
                {
                    throw new InvalidOperationException("Unrecognized change type " + change.ChangeType);
                }
            }
        }
    }
}
