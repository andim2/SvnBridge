using System;
using System.Collections.Generic;
using System.IO;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using SvnBridge.Handlers;
using SvnBridge.Infrastructure; // Configuration
using SvnBridge.Protocol;
using SvnBridge.SourceControl;
using SvnBridge.Utility; // Helper.CooperativeSleep(), Helper.Encode() etc.

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
    }

	internal class UpdateReportService
	{
		private readonly RequestHandlerBase handler;
        private readonly TFSSourceControlProvider sourceControlProvider;
        private static char[] path_separators = new char[] { '/', '\\' };

        public UpdateReportService(RequestHandlerBase handler, TFSSourceControlProvider sourceControlProvider)
		{
			this.handler = handler;
			this.sourceControlProvider = sourceControlProvider;
		}

		public void ProcessUpdateReportForDirectory(UpdateReportData updateReportRequest, FolderMetaData folder, StreamWriter output, bool isRootFolder, bool parentFolderWasDeleted)
		{
			if (folder is DeleteFolderMetaData)
			{
                if (!parentFolderWasDeleted)
                {
                    output.Write("<S:delete-entry name=\"" + GetEncodedNamePart(folder) + "\"/>\n");
                }
			}
			else
			{
				bool isExistingFolder = false;
                bool folderWasDeleted = parentFolderWasDeleted;
                if (isRootFolder)
				{
                    // root folder --> no "name" attribute specified.
					output.Write("<S:open-directory rev=\"" + updateReportRequest.Entries[0].Rev + "\">\n");
				}
				else
				{
					string srcPath = GetSrcPath(updateReportRequest);
                    int clientRevisionForItem = GetClientRevisionFor(updateReportRequest.Entries, StripBasePath(folder, srcPath));
					if (ItemExistsAtTheClient(folder, updateReportRequest, srcPath, clientRevisionForItem))
					{
						isExistingFolder = true;
					}

          string itemNameEncoded = GetEncodedNamePart(folder);
					// If another item with the same name already exists, need to remove it first.
					if (!parentFolderWasDeleted && ShouldDeleteItemBeforeSendingToClient(folder, updateReportRequest, srcPath, clientRevisionForItem, isExistingFolder))
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
						ProcessUpdateReportForDirectory(updateReportRequest, subFolder, output, false, folderWasDeleted);
					}
					else
					{
						ProcessUpdateReportForFile(updateReportRequest, item, output, folderWasDeleted);
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

		public void ProcessUpdateReportForFile(UpdateReportData updateReportRequest, ItemMetaData item, StreamWriter output, bool parentFolderWasDeleted)
		{
			if (item is DeleteMetaData)
			{
                if (!parentFolderWasDeleted)
                {
                    output.Write("<S:delete-entry name=\"" + GetEncodedNamePart(item) + "\"/>\n");
                }
			}
			else
			{
				bool isExistingFile = false;
				string srcPath = GetSrcPath(updateReportRequest);
                int clientRevisionForItem = GetClientRevisionFor(updateReportRequest.Entries, StripBasePath(item, srcPath));
				if (ItemExistsAtTheClient(item, updateReportRequest, srcPath, clientRevisionForItem))
				{
					isExistingFile = true;
				}

        string itemNameEncoded = GetEncodedNamePart(item);
				// If another item with the same name already exists, need to remove it first.
				if (!parentFolderWasDeleted && ShouldDeleteItemBeforeSendingToClient(item, updateReportRequest, srcPath, clientRevisionForItem, isExistingFile))
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

                bool requestedTxDelta = HaveRequestTxDelta(updateReportRequest);

				// wait for data (required by *both* txdelta [optional] and md5 below!)
				while (item.DataLoaded == false)
					Helper.CooperativeSleep(100);
                                var base64DiffData = item.Base64DiffData;
                                // Immediately release data memory from item's reach
                                // (reduce GC memory management pressure)
                                item.DataLoaded = false;
                                item.Base64DiffData = null;

                if (requestedTxDelta)
                {
				output.Write("<S:txdelta>");
                // KEEP THIS WRITE ACTION SEPARATE! (avoid huge-string alloc):
                URSHelpers.PushTxDeltaData(
                    output,
                    base64DiffData);
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
                }
                output.Write("<S:prop><V:md5-checksum>" + item.Md5Hash + "</V:md5-checksum></S:prop>\n");

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

        private static string GetEncodedNamePart(ItemMetaData item)
        {
            return Helper.EncodeB(GetFileName(item.Name));
        }

		private string GetSrcPath(UpdateReportData updateReportRequest)
		{
			string url = handler.GetLocalPathFromUrl(updateReportRequest.SrcPath);
			if (updateReportRequest.UpdateTarget != null)
				return url + "/" + updateReportRequest.UpdateTarget;
			return url;
		}

		private bool ItemExistsAtTheClient(ItemMetaData item, UpdateReportData updateReportRequest, string srcPath, int clientRevisionForItem)
		{
			return HaveItemExistingAtClient(
                updateReportRequest,
                srcPath,
                item.Name) &&
			       // we need to check both name and id to ensure that the item was not renamed
			       sourceControlProvider.ItemExists(item.Name, clientRevisionForItem) &&
			       sourceControlProvider.ItemExists(item.Id, clientRevisionForItem);
		}

		private bool ShouldDeleteItemBeforeSendingToClient(ItemMetaData item,
			UpdateReportData updateReportRequest,
			string srcPath,
			int clientRevisionForItem,
			bool isExistingItem)
		{
			return isExistingItem == false &&
                HaveItemExistingAtClient(
                    updateReportRequest,
                    srcPath,
                    item.Name) &&
				   sourceControlProvider.ItemExists(item.Name, clientRevisionForItem);
		}

        private static bool HaveItemExistingAtClient(
            UpdateReportData updateReportRequest,
            string srcPath,
            string itemPath)
        {
            return updateReportRequest.IsCheckOut == false &&
                IsMissing(updateReportRequest, srcPath, itemPath) == false;
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
            // TODO: this bool should best be made a class _member_
            // (which requires updateReportRequest to be a proper class member, too).
            bool requestedSendAll = (true == updateReportRequest.SendAll);
            bool requestedTxDelta = (requestedSendAll);
            return requestedTxDelta;
        }

		private static string GetFileName(string path)
		{
			int slashIndex = path.LastIndexOfAny(path_separators);
			return path.Substring(slashIndex + 1);
		}

        private static string StripBasePath(ItemMetaData item, string basePath)
        {
            string name = item.Name;

            FilesysHelpers.StripRootSlash(ref name);

            FilesysHelpers.StripRootSlash(ref basePath);

            basePath = basePath + "/";

            if (name.StartsWith(basePath))
            {
                name = name.Substring(basePath.Length);
                FilesysHelpers.StripRootSlash(ref name);
            }
            return name;
        }

        private static int GetClientRevisionFor(List<EntryData> entries, string name)
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
            return int.Parse(bestMatch.Rev);
        }

        private static bool IsMissing(UpdateReportData data, string localPath, string name)
        {
            if (data.Missing == null || data.Missing.Count == 0)
                return false;

            string path = localPath.Substring(1);
            if (path.EndsWith("/") == false)
                path += "/";
            if (name.StartsWith(path))
                name = name.Substring(path.Length);

            if (data.Missing.Contains(name))
                return true;
            foreach (string missing in data.Missing)
            {
                if (name.StartsWith(missing))// the missing is the parent of this item
                    return true;
            }
            return false;
        }
    }
}
