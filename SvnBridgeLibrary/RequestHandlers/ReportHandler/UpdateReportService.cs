using System;
using System.Collections.Generic;
using System.IO;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using SvnBridge.Handlers;
using SvnBridge.Infrastructure; // Configuration
using SvnBridge.Protocol;
using SvnBridge.SourceControl;
using SvnBridge.Utility; // DebugRandomActivator, Helper.DebugUsefulBreakpointLocation(), Helper.Encode() etc.

namespace SvnBridge.Infrastructure
{
    /// <summary>
    /// Provides some non-report helpers.
    /// </summary>
    /// <remarks>
    /// Methods should possibly be moved over to more central helpers classes eventually.
    /// NOTE: should probably keep any specifics about content generation format (XML syntax)
    /// out of these helpers...
    /// </remarks>
    internal class URSHelpers
    {
        private static char[] path_separators = new char[] { '/', '\\' };

        public static string GetEncodedNamePart(ItemMetaData item)
        {
            return Helper.EncodeB(GetFileName(item.Name));
        }

        private static string GetFileName(string path)
        {
            int slashIndex = path.LastIndexOfAny(path_separators);
            return path.Substring(slashIndex + 1);
        }

        public static void StreamItemDataAsTxDelta(
            StreamWriter output,
            TFSSourceControlProvider sourceControlProvider,
            ItemMetaData item)
        {
            byte[] itemData = sourceControlProvider.ReadFile(item);
            item = null; // enable release (large object)
            string txdelta = SvnDiffParser.GetBase64SvnDiffData(itemData);
            itemData = null; // enable release (large object)
            PushTxDeltaData(
                output,
                txdelta);
        }

        public static void PushTxDeltaData(
            TextWriter output,
            string result_Base64DiffData)
        {
            output.Write(
                result_Base64DiffData);
        }

        /// <summary>
        /// Grabs Base64 diff data and MD5 hash of an item.
        /// Will wait for the crawler thread to have provided that data.
        /// </summary>
        /// <param name="item">The item which the data is to be fetched of</param>
        /// <param name="base64DiffData">Item content data (base64 diff)</param>
        /// <param name="Md5Hash">MD5 hash of item content data</param>
        public static void GrabItemDeltaAndHash(
            AsyncItemLoader loader,
            ItemMetaData item,
            out string item_Base64DiffData,
            out string item_Md5Hash)
        {
            TimeSpan spanLoadTimeout = TimeSpan.FromHours(2);
            bool gotData = loader.TryRobItemData(
                item,
                spanLoadTimeout,
                out item_Base64DiffData,
                out item_Md5Hash);
            if (!(gotData))
            {
                ReportErrorItemDataRetrievalTimeout();
            }
        }

        private static void ReportErrorItemDataRetrievalTimeout()
        {
            Helper.DebugUsefulBreakpointLocation();
            throw new TimeoutException("Timeout while waiting for retrieval of filesystem item data");
        }
    }

    /// <summary>
    /// Internal class for recursion-based hierarchy-crawling
    /// which will generate format-specific output
    /// from various attributes
    /// which have been passed for this request-to-be-processed
    /// and will be kept as fixed members
    /// during our recursion implementation.
    /// </summary>
    internal class UpdateReportGenerator
    {
        private readonly StreamWriter output;
        private readonly UpdateReportData updateReportRequest;
        private readonly string srcPath;
        private readonly TFSSourceControlProvider sourceControlProvider;
        private readonly AsyncItemLoader loader;
        private readonly RequestHandlerBase handler;
        private readonly FolderMetaData root;
        private readonly bool requestedTxDelta;
        private readonly DebugRandomActivator debugRandomActivator;

        public UpdateReportGenerator(
            StreamWriter output,
            UpdateReportData updateReportRequest,
            string srcPath,
            TFSSourceControlProvider sourceControlProvider,
            AsyncItemLoader loader,
            RequestHandlerBase handler,
            FolderMetaData root)
        {
            this.output = output;
            this.updateReportRequest = updateReportRequest;
            this.srcPath = srcPath;
            this.handler = handler;
            this.sourceControlProvider = sourceControlProvider;
            this.loader = loader;
            this.root = root;
            this.requestedTxDelta = HaveRequestTxDelta(updateReportRequest);
            this.debugRandomActivator = new DebugRandomActivator();
        }

        public void Generate()
        {
            ProcessUpdateReportForDirectory(root, false);
        }

		private void ProcessUpdateReportForDirectory(FolderMetaData folder, bool parentFolderWasDeleted)
		{
			if (folder is DeleteFolderMetaData)
			{
                if (!parentFolderWasDeleted)
                {
                    output.Write("<S:delete-entry name=\"" + URSHelpers.GetEncodedNamePart(folder) + "\"/>\n");
                }
			}
			else
			{
                bool isRootFolder = (folder == root);
				bool isExistingFolder = false;
                bool folderWasDeleted = parentFolderWasDeleted;
                if (isRootFolder)
				{
                    // root folder --> no "name" attribute specified.
					output.Write("<S:open-directory rev=\"" + updateReportRequest.Entries[0].Rev + "\">\n");
				}
				else
				{
                    int clientRevisionForItem = GetClientRevisionFor(
                        folder);
					if (ItemExistsAtTheClient(folder, clientRevisionForItem))
					{
						isExistingFolder = true;
					}

          string itemNameEncoded = URSHelpers.GetEncodedNamePart(folder);
					// If another item with the same name already exists, need to remove it first.
					if (!parentFolderWasDeleted && ShouldDeleteItemBeforeSendingToClient(folder, clientRevisionForItem, isExistingFolder))
					{
						output.Write("<S:delete-entry name=\"" + itemNameEncoded + "\"/>\n");
                        folderWasDeleted = true;
					}

					if (isExistingFolder)
					{
						output.Write("<S:open-directory name=\"" + itemNameEncoded +
									 "\" rev=\"" + updateReportRequest.Entries[0].Rev + "\">\n");
					}
					else
					{
						output.Write("<S:add-directory name=\"" + itemNameEncoded +
									 "\" bc-url=\"" + handler.GetLocalPath("/!svn/bc/" + folder.Revision + "/" + Helper.Encode(folder.Name, true)) +
									 "\">\n");
					}
				}
				if (!isRootFolder || updateReportRequest.UpdateTarget == null)
				{
          UpdateReportWriteItemAttributes(output, folder);
				}

				foreach (ItemMetaData item in folder.Items)
				{
          FolderMetaData subFolder = item as FolderMetaData;
          bool isFolder = (null != subFolder);
					if (isFolder)
					{
						ProcessUpdateReportForDirectory(subFolder, folderWasDeleted);
					}
					else
					{
						ProcessUpdateReportForFile(item, folderWasDeleted);
					}
				}
				output.Write("<S:prop></S:prop>\n");
				if (isRootFolder || isExistingFolder)
				{
					output.Write("</S:open-directory>\n");
				}
				else
				{
					output.Write("</S:add-directory>\n");
				}
			}
		}

		private void ProcessUpdateReportForFile(ItemMetaData item, bool parentFolderWasDeleted)
		{
			if (item is DeleteMetaData)
			{
                if (!parentFolderWasDeleted)
                {
                    output.Write("<S:delete-entry name=\"" + URSHelpers.GetEncodedNamePart(item) + "\"/>\n");
                }
			}
			else
			{
				bool isExistingFile = false;
                int clientRevisionForItem = GetClientRevisionFor(
                    item);
				if (ItemExistsAtTheClient(item, clientRevisionForItem))
				{
					isExistingFile = true;
				}

        string itemNameEncoded = URSHelpers.GetEncodedNamePart(item);
				// If another item with the same name already exists, need to remove it first.
				if (!parentFolderWasDeleted && ShouldDeleteItemBeforeSendingToClient(item, clientRevisionForItem, isExistingFile))
				{
					output.Write("<S:delete-entry name=\"" + itemNameEncoded + "\"/>\n");
				}

				if (isExistingFile)
				{
					output.Write("<S:open-file name=\"" + itemNameEncoded + "\" rev=\"" +
                                 clientRevisionForItem + "\">\n");
				}
				else
				{
					output.Write("<S:add-file name=\"" + itemNameEncoded + "\">\n");
				}

        UpdateReportWriteItemAttributes(output, item);

                string result_Md5Hash;
                if (requestedTxDelta)
                {
                    string result_Base64DiffData;
                    URSHelpers.GrabItemDeltaAndHash(
                        loader,
                        item,
                        out result_Base64DiffData,
                        out result_Md5Hash);

				output.Write("<S:txdelta>");
                // KEEP THIS WRITE ACTION SEPARATE! (avoid huge-string alloc):
                URSHelpers.PushTxDeltaData(
                    output,
                    result_Base64DiffData);
				output.Write("\n"); // \n EOL belonging to entire line (XML elem start plus payload)
                output.Write("</S:txdelta>"); // XXX hmm, no \n EOL after this elem spec:ed / needed?
                }
                else
                {
                    bool isNewlyAdded = !(isExistingFile);
                    if (!(isNewlyAdded)) // not newly added (implicitly fetched)? Produce explicit fetch request for client to fetch whole file.
                    {
                        // TODO: missing sha1 checksumming attrs.
                        // "base-checksum" seems to constitute the "base checksum"
                        // as calculated of the "base file" (*.svn-base),
                        // i.e. the previous client revision of the file.
                        // FIXME: instead of having such a rough fetch of the item
                        // (whereas we're already doing ItemExists() queries at other places!),
                        // we should probably always keep a reference member to the previous-version item
                        // for quick access (but we'll still have to subsequently read the file data).
                        ItemMetaData itemBase = sourceControlProvider.GetItemsWithoutProperties(clientRevisionForItem, item.Name, Recursion.None);
                        string base_Md5Hash = null;
                        if (itemBase != null)
                        {
                            var base_Data = sourceControlProvider.ReadFile(itemBase);
                            base_Md5Hash = Helper.GetMd5Checksum(base_Data);
                        }
                        output.Write("<S:fetch-file");
                        if (base_Md5Hash != null)
                        {
                            output.Write(" base-checksum=\"" + base_Md5Hash + "\"");
                        }
                        output.Write("/>\n");
                    }
                    // Request result item data only *after* our base item query above,
                    // since fetching gets (hopefully got) done (in parallel??) by crawler thread...
                    string result_Base64DiffData;
                    URSHelpers.GrabItemDeltaAndHash(
                        loader,
                        item,
                        out result_Base64DiffData,
                        out result_Md5Hash);
                    result_Base64DiffData = null; // huge data (we don't need it here)
                }
                output.Write("<S:prop><V:md5-checksum>" + result_Md5Hash + "</V:md5-checksum></S:prop>\n");

                if (isExistingFile)
				{
					output.Write("</S:open-file>\n");
				}
				else
				{
					output.Write("</S:add-file>\n");
				}
			}
		}

        private int GetClientRevisionFor(
            ItemMetaData item)
        {
            return GetClientRevisionFor(
                updateReportRequest.Entries,
                FilesysHelpers.StripBasePath(item.Name, srcPath));
        }

		private bool ItemExistsAtTheClient(ItemMetaData item, int clientRevisionForItem)
		{
            bool existsAtClient = false;

			// Prefer implementing the order of conditional checks
			// from fastest to slowest...
			existsAtClient = HaveItemExistingAtClient(
                item.Name);
          if (existsAtClient)
          {
              bool isItemExistingInSCM = ItemExistsAtRevision(item, clientRevisionForItem);

              existsAtClient = isItemExistingInSCM;
          }

          return existsAtClient;
		}

        /// <summary>
        /// This method might need to be offered more centrally
        /// (while not at the provider,
        /// since this would cause the provider interface to not be orthogonal,
        /// some outer wrapper [SVN provider?] could provide that).
        /// </summary>
        private bool ItemExistsAtRevision(ItemMetaData item, int clientRevisionForItem)
        {
            // Side comment: the user account (~ workspace) used here
            // is preferred to be per-source-control-client unique,
            // otherwise risk of conflicts in TFS server-side tracking
            // of client workspace!
            // For details, see the comment at credentials handling.

            // we need to check both name and id to ensure that the item was not renamed
            // [this comment might have been made due to TFS2008 ID-based QueryItems() NOT
            // bailing out on a deleted item whereas path-based QueryItems() does return NULL.
            // See comments in inner layers]

            // Old implementation did up to *two* Exists() checks with all their associated
            // web service request overhead - however this should not be necessary,
            // since one should be able to gather list of items and then compare both name and id
            // for those items. Note that this quite likely also means that the old query method
            // was NOT *atomic*, i.e. it could happen that both
            // "item id existed" check succeeded
            // and "required name existed" check succeeded
            // since *another*(!!) item with the required name happens to exist,
            // i.e. a status that would completely flunk the "ensure that item was not renamed" requirement.

            // Implement the new check method as an *additional* method,
            // with old vs. new result randomly compared and an exception thrown
            // in case a result mismatch happened to occur.
            bool wantNewQuery = true;
            bool wantOldQuery = false;

            if (!wantOldQuery)
            {
                int doVerificationPercentage = 5;
                wantOldQuery = debugRandomActivator.YieldTrueOnPercentageOfCalls(doVerificationPercentage);
            }

            bool isProperItem_NewQuery = false;
            bool isProperItem_OldQuery = false;

            if (wantNewQuery)
            {
                // FIXME: not sure whether we need property storage item results here.
                // However it might be that this is the only way that
                // property-only changes can be signalled,
                // and that ItemExists() does or does not return such items...
                isProperItem_NewQuery = sourceControlProvider.ItemExists(item.Id, item.Name, clientRevisionForItem);
            }
            if (wantOldQuery)
            {
                bool existsItem_id = sourceControlProvider.ItemExists(item.Id, clientRevisionForItem);
                if (existsItem_id)
                {
                    bool existsItem_name = sourceControlProvider.ItemExists(item.Name, clientRevisionForItem);
                    isProperItem_OldQuery = existsItem_name;
                }
            }

            bool isComparisonPossible = (wantNewQuery && wantOldQuery);
            if (isComparisonPossible)
            {
                bool isMatch = (isProperItem_NewQuery == isProperItem_OldQuery);
                if (!(isMatch))
                {
                    ReportErrorQueryResultMismatch(
                        item,
                        clientRevisionForItem,
                        isProperItem_NewQuery,
                        isProperItem_OldQuery);
                }
            }

            bool exists = wantNewQuery ? isProperItem_NewQuery : isProperItem_OldQuery;

            return exists;
        }

        private static bool ReportErrorQueryResultMismatch(ItemMetaData item, int clientRevisionForItem, bool result_NewQuery, bool result_OldQuery)
        {
            // If this error happens, then you should determine
            // why the server's changeset content (item id, item name) caused these results.
            throw new InvalidOperationException(
                String.Format("MISMATCH of new vs. old algorithm, for item with id {0} and name {1}, at {2}: {3} vs. {4}!",
                    item.Id,
                    item.Name,
                    clientRevisionForItem,
                    result_NewQuery,
                    result_OldQuery)
            );
        }

		private bool ShouldDeleteItemBeforeSendingToClient(ItemMetaData item,
			int clientRevisionForItem,
			bool isExistingItem)
		{
			return isExistingItem == false &&
                HaveItemExistingAtClient(
                    item.Name) &&
            // NOTE: .ItemExists() call here is very expensive.
            // Could think of an optimization where one fetches
            // all items (at their revision(s)) beforehand
            // to a container which then will be queried here.
            // OTOH service.QueryItems() is single-revision only,
            // which might be difficult in case of mixed-revisions data.
				   sourceControlProvider.ItemExists(item.Name, clientRevisionForItem);
		}

        private bool HaveItemExistingAtClient(
            string itemPath)
        {
            bool existsAtClient = false;

            // No pre-existing files whatsoever at client working copy (WC) yet,
            // thus no need to check in the first place?
            bool isCleanSlate = (updateReportRequest.IsCheckOut != false);
            bool needCheckClient = !(isCleanSlate);
            if (needCheckClient)
            {
                existsAtClient = (IsMissing(updateReportRequest, srcPath, itemPath) == false);
            }

            return existsAtClient;
        }

        private void UpdateReportWriteItemAttributes(TextWriter output, ItemMetaData item)
        {
            string svnVer = handler.GetLocalPath(SVNGeneratorHelpers.GetSvnVerFromRevisionLocation(item.Revision, item.Name, true));
            output.Write("<D:checked-in><D:href>" + svnVer + "</D:href></D:checked-in>\n");
            output.Write("<S:set-prop name=\"svn:entry:committed-rev\">" + item.Revision + "</S:set-prop>\n");
            output.Write("<S:set-prop name=\"svn:entry:committed-date\">" + Helper.FormatDate(item.LastModifiedDate) + "</S:set-prop>\n");
            output.Write("<S:set-prop name=\"svn:entry:last-author\">" + item.Author + "</S:set-prop>\n");
            output.Write("<S:set-prop name=\"svn:entry:uuid\">" + sourceControlProvider.GetRepositoryUuid() + "</S:set-prop>\n");
            foreach (KeyValuePair<string, string> property in item.Properties)
            {
                output.Write("<S:set-prop name=\"" + property.Key + "\">" + property.Value + "</S:set-prop>\n");
            }
        }

        /// <summary>
        /// Almost comment-only helper.
        /// </summary>
        /// <remarks>
        /// http://grokbase.com/p/subversion/dev/122axpnndm/error-while-checking-out-git-repository
        ///   "The txdelta element should only be delivered to the client when send-all=true."
        ///
        /// Well, proper txdelta handling actually is a lot more involved (previous implementation brains
        /// failed when doing a simple diff [it requested send-all=false]).
        /// SEE ALSO RELATED send-all ELEMENT ANNOUNCEMENT at parent handler impl!!
        /// "mod_dav_svn doing wasteful text-delta calculation and transmission for 'svn st -u'"
        ///   http://subversion.tigris.org/issues/show_bug.cgi?id=2259
        /// "Re: svn commit: rev 7712 - branches/issue-1429-dev/subversion/mod_dav_svn"
        ///   http://svn.haxx.se/dev/archive-2003-11/0712.shtml
        /// http://svn.apache.org/repos/asf/subversion/trunk/notes/http-and-webdav/webdav-protocol
        /// "Re: Error While Checking out Git Repository"
        ///  http://mail-archives.apache.org/mod_mbox/subversion-dev/201202.mbox/%3C20120210025308.GA16328@daniel3.local%3E
        /// In short: seems txdelta element has to _always_ (possibly empty!) get sent, regardless of SendAll active?
        /// Client analysis does not suggest this to be the case though...
        /// </remarks>
        private static bool HaveRequestTxDelta(UpdateReportData updateReportRequest)
        {
            bool requestedSendAll = (true == updateReportRequest.SendAll);
            bool requestedTxDelta = (requestedSendAll);
            return requestedTxDelta;
        }

        private static int GetClientRevisionFor(List<EntryData> entries, string name)
        {
            EntryData bestMatch = GetBestMatchingEntry(entries, name);
            return int.Parse(bestMatch.Rev);
        }

        private static EntryData GetBestMatchingEntry(List<EntryData> entries, string name)
        {
            EntryData bestMatch = entries[0];

            StringComparison stringCompareMode =
                Configuration.SCMWantCaseSensitiveItemMatch ? StringComparison.InvariantCulture : StringComparison.InvariantCultureIgnoreCase;

            foreach (EntryData entry in entries)
            {
                if (entry.path == name)// found a best match
                {
                    bestMatch = entry;
                    break;
                }

                if (entry.path == null || name.StartsWith(entry.path, stringCompareMode) == false)
                    continue;

                // if the current entry is longer than the previous best match, then this
                // is a better match, because it is more deeply nested, so likely
                // to be a better parent
                if (bestMatch.path == null || bestMatch.path.Length < entry.path.Length)
                    bestMatch = entry;
            }
            return bestMatch;
        }

        /// <summary>
        /// Checks whether name is a (sub-)element of any entries in Missing.
        /// </summary>
        private static bool IsMissing(UpdateReportData data, string localPath, string name)
        {
            return IsWithin(data.Missing, localPath, name);
        }

        private static bool IsWithin(List<string> entries, string localPath, string name)
        {
            if (entries == null || entries.Count == 0)
                return false;

            string path = localPath.Substring(1);
            if (path.EndsWith("/") == false)
                path += "/";
            if (name.StartsWith(path))
                name = name.Substring(path.Length);

            if (entries.Contains(name))
                return true;
            foreach (string pathEntry in entries)
            {
                if (name.StartsWith(pathEntry))// the current entry is the parent of this item
                    return true;
            }
            return false;
        }
    }

	internal class UpdateReportService
	{
		private readonly RequestHandlerBase handler;
        private readonly TFSSourceControlProvider sourceControlProvider;
        private readonly AsyncItemLoader loader;

        public UpdateReportService(RequestHandlerBase handler, TFSSourceControlProvider sourceControlProvider, AsyncItemLoader loader)
		{
			this.handler = handler;
			this.sourceControlProvider = sourceControlProvider;
			this.loader = loader;
		}

        /// <summary>
        /// Helper to minimize externally visible interface changes (hide recursion-specific parameters)
        /// </summary>
        public void ProcessUpdateReport(
            UpdateReportData updateReportRequest,
            string srcPath,
            FolderMetaData folder,
            StreamWriter output)
        {
            UpdateReportGenerator generator = new UpdateReportGenerator(
                output,
                updateReportRequest,
                srcPath,
                sourceControlProvider,
                loader,
                handler,
                folder);

            generator.Generate();
        }
    }
}
