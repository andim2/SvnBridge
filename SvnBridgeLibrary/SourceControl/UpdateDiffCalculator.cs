using System;
using System.Collections.Generic;
using System.Diagnostics; // Conditional
using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using SvnBridge.Infrastructure; // Configuration
using SvnBridge.Protocol;
using SvnBridge.Utility; // Helper.SortHistories()

namespace SvnBridge.SourceControl
{
    using ClientExistingFiles = Dictionary<string, int>;
    using ClientMissingFiles = Dictionary<string, string>;

    public sealed class ClientStateTracker
    {
        private readonly ClientExistingFiles clientExistingFiles;
        private readonly ClientMissingFiles clientMissingFiles;

        public ClientStateTracker(
            ClientExistingFiles clientExistingFiles,
            ClientMissingFiles clientMissingFiles)
        {
            this.clientExistingFiles = clientExistingFiles;
            this.clientMissingFiles = clientMissingFiles;
        }

        public ClientStateTracker(
            )
        {
            this.clientExistingFiles = new ClientExistingFiles();
            this.clientMissingFiles = new ClientMissingFiles();
        }

        public void SetFileExisting(
            string itemPath,
            int revision)
        {
            clientExistingFiles.Add(
                itemPath,
                revision);
        }

        public void SetFileMissing(
            string itemPath,
            string itemFileName)
        {
            clientMissingFiles.Add(
                itemPath,
                itemFileName);
        }

        public bool IsChangeAlreadyCurrentInClientState(
            ChangeType changeType,
            string itemPath,
            int itemRevision)
        {
            string changePath = itemPath;
            if (changePath.StartsWith("/") == false)
                changePath = "/" + changePath;
            if (((changeType & ChangeType.Add) == ChangeType.Add) ||
                ((changeType & ChangeType.Edit) == ChangeType.Edit))
            {
                int revisionClientExistingFile = 0;
                if ((HaveClientExistingFile(changePath, ref revisionClientExistingFile)) && (revisionClientExistingFile >= itemRevision))
                {
                    return true;
                }

                foreach (string clientExistingFile in clientExistingFiles.Keys)
                {
                    if (changePath.StartsWith(clientExistingFile + "/") &&
                        (clientExistingFiles[clientExistingFile] >= itemRevision))
                    {
                        return true;
                    }
                }
            }
            else if ((changeType & ChangeType.Delete) == ChangeType.Delete)
            {
                int revisionClientExistingFile = 0;
                if (clientMissingFiles.ContainsKey(changePath) ||
                    (HaveClientExistingFile(changePath, ref revisionClientExistingFile) && (revisionClientExistingFile >= itemRevision)))
                {
                    return true;
                }

                foreach (string clientDeletedFile in clientMissingFiles.Keys)
                {
                    if (changePath.StartsWith(clientDeletedFile + "/"))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool HaveClientExistingFile(string path, ref int revisionClientExistingFile)
        {
            bool haveFile = false;

            if (clientExistingFiles.ContainsKey(path))
            {
                haveFile = true;
                revisionClientExistingFile = clientExistingFiles[path];
            }

            return haveFile;
        }

        public ClientMissingFiles ClientMissingFiles
        {
            get
            {
                return clientMissingFiles;
            }
        }
    }

    public class ChangeTypeAnalyzer
    {
        public static bool IsRenameOperation(SourceItemChange change)
        {
            return IsChangeTypeRename(change.ChangeType);
        }

        public static bool IsMergeOperation(SourceItemChange change)
        {
            return IsChangeTypeMerge(change.ChangeType);
        }

        /// <remarks>
        /// IsDeleteOperation()/IsAddOperation() could be streamlined by
        /// collapsing everything into simple ternary-only operations
        /// (after all the updatingForwardInTime bools are hard-coded thus it's obvious
        /// which path it takes),
        /// but then it was probably fully intentional to have a fully complementary
        /// "if not this one, then call the fully complementary method" implementation,
        /// so maybe we should keep implementation that way.
        /// </remarks>
        public static bool IsDeleteOperation(SourceItemChange change, bool updatingForwardInTime)
        {
            if (updatingForwardInTime == false)
            {
                return IsAddOperation(change, true);
            }
            return IsChangeTypeDelete(change.ChangeType);
        }

        public static bool IsAddOperation(SourceItemChange change, bool updatingForwardInTime)
        {
            if (updatingForwardInTime == false)
            {
                return IsDeleteOperation(change, true);
            }
            return IsChangeTypeAddKind(change.ChangeType);
        }

        public static bool IsEditOperation(SourceItemChange change)
        {
            return IsChangeTypeEdit(change.ChangeType);
        }

        static bool IsChangeTypeMerge(ChangeType changeType)
        {
            return (changeType & ChangeType.Merge) == ChangeType.Merge;
        }

        static bool IsChangeTypeRename(ChangeType changeType)
        {
            return (changeType & ChangeType.Rename) == ChangeType.Rename;
        }

        static bool IsChangeTypeDelete(ChangeType changeType)
        {
            return (changeType & ChangeType.Delete) == ChangeType.Delete;
        }

        public static bool IsChangeTypeAddKind(ChangeType changeType)
        {
            bool isAdd = false;
            // First a rough but fast check...
            if ((changeType & (ChangeType.Add | ChangeType.Branch | ChangeType.Undelete)) > 0)
            {
                // ...then specifics:
                isAdd =
                ((changeType & ChangeType.Add) == ChangeType.Add) ||
                ((changeType & ChangeType.Branch) == ChangeType.Branch) ||
                ((changeType & ChangeType.Undelete) == ChangeType.Undelete);
            }
            return isAdd;
        }

        static bool IsChangeTypeEdit(ChangeType changeType)
        {
            return (changeType & ChangeType.Edit) == ChangeType.Edit;
        }

        /// <summary>
        /// Simplistic (read: likely incorrect
        /// due to insufficiently precise / incomplete parameterization) variant.
        /// AVOID ITS USE.
        /// </summary>
        public static bool IsAddOperation(SourceItemChange change)
        {
            return IsAddOperation(change, true);
        }
    }

    public class ShouldBeIgnoredException : Exception
    {
    }

    public class UpdateDiffCalculator
    {
        private readonly TFSSourceControlProvider sourceControlProvider;
        private ClientStateTracker clientStateTracker;
        private readonly Dictionary<ItemMetaData, bool> additionForPropertyChangeOnly = new Dictionary<ItemMetaData, bool>();
        private readonly List<string> renamedItemsToBeCheckedForDeletedChildren = new List<string>();
        private string debugInterceptCheck /* = null */ = null /* CS0649 */;

        public UpdateDiffCalculator(TFSSourceControlProvider sourceControlProvider)
        {
            this.sourceControlProvider = sourceControlProvider;
        }

        public void CalculateDiff(string checkoutRootPath, int versionTo, int versionFrom, FolderMetaData checkoutRoot, UpdateReportData updateReportData)
        {
            // Initially populate our _member_ variable:
            clientStateTracker = ConstructClientStateFromSVNUpdateReportData(
                checkoutRootPath,
                updateReportData);

            string projectRootPath = GetProjectRoot(checkoutRootPath);

            if (updateReportData.Entries != null)
            {
                // pre-calculate rootPath prior to subsequent loop:
                string rootPath = checkoutRootPath;
                if (updateReportData.UpdateTarget != null)
                    rootPath += "/" + updateReportData.UpdateTarget;

                foreach (EntryData data in updateReportData.Entries)
                {
                    int itemVersionFrom = int.Parse(data.Rev);
                    if (itemVersionFrom < versionFrom)
                    {
                        string targetPath = rootPath + "/" + data.path;

                        FilesysHelpers.StripRootSlash(ref targetPath);

                        CalculateChangeBetweenVersions(projectRootPath, targetPath, itemVersionFrom, checkoutRoot, itemVersionFrom, versionFrom);
                    }
                }
            }

            if (versionFrom != versionTo)
            {
                // we have to calculate the difference from the project root
                // this is because we may have a file move from below the checkoutRootPath, 
                // which we still need to consider
                FolderMetaData projectRoot = checkoutRoot;
                if (!(projectRootPath.Equals(checkoutRootPath)))
                {
                    projectRoot = (FolderMetaData)sourceControlProvider.GetItems(versionTo, projectRootPath, Recursion.None);
                    string path = checkoutRootPath.Substring(0, checkoutRootPath.LastIndexOf('/'));
                    path = path.Substring(path.IndexOf('/') + 1);
                    FolderMetaData result = (FolderMetaData)FindItemOrCreateItem(projectRoot, projectRootPath, path, versionTo, Recursion.None);
                    ItemHelpers.FolderOps_AddItem(result, checkoutRoot);
                }

                CalculateChangeBetweenVersions(projectRootPath, TFSSourceControlProvider.LATEST_VERSION, projectRoot, versionFrom, versionTo);
            }

            ClientMissingFiles clientMissingFiles = clientStateTracker.ClientMissingFiles;
            foreach (string missingItem in clientMissingFiles.Values)
            {
                if (sourceControlProvider.ItemExists(checkoutRootPath + "/" + missingItem, versionTo))
                {
                    // SVNBRIDGE_WARNING_REF_RECURSION
                    FindItemOrCreateItem(checkoutRoot, checkoutRootPath, missingItem, versionTo, Recursion.Full);
                }
            }
            RemoveMissingItemsWhichAreChildrenOfRenamedItem(checkoutRoot);
            checkoutRoot.VerifyNoMissingItemMetaDataRemained();
        }

        private static string GetProjectRoot(string path)
        {
            string[] parts = path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            return (parts.Length > 0) ? parts[0] : "";
        }

        private ItemMetaData FindItemOrCreateItem(FolderMetaData root, string pathRoot, string path, int targetVersion, Recursion recursion)
        {
            ItemMetaData itemFound = ItemHelpers.PathIterator(root, pathRoot, path,
                delegate(FolderMetaData folder, string itemPath, bool isLastPathElem, ref bool requestFinish)
                {
                    ItemMetaData itemPrev = folder.FindItem(itemPath);
                    ItemMetaData item = itemPrev;
                    if (itemPrev == null)
                    {
                        ItemMetaData itemFetched = sourceControlProvider.GetItems(targetVersion, itemPath, recursion);

                        bool isFolder = (!isLastPathElem);
                        if (isFolder)
                        {
                            FolderMetaData subFolder = (FolderMetaData)itemFetched;
                            item = subFolder;
                        }
                        else
                        {
                            item = itemFetched;
                        }

                        if (null == item)
                        {
                            item = new MissingItemMetaData(itemPath, targetVersion, false);
                        }
                        ItemHelpers.FolderOps_AddItem(folder, item);
                    }

                    return item;
                });
            return itemFound;
        }

        private void CalculateChangeBetweenVersions(string checkoutRootPath, int checkoutRootVersion, FolderMetaData root, int sourceVersion, int targetVersion)
        {
            CalculateChangeBetweenVersions(checkoutRootPath, checkoutRootPath, checkoutRootVersion, root, sourceVersion, targetVersion);
        }

        /// <summary>
        /// This helper is intended to do the following:
        /// - fetch a full revision log of the affected item(s) from TFS,
        ///   ranging from sourceVersion to targetVersion
        /// - iterate through all gathered Changesets
        ///   - iterate through all Changes contained within a Changeset version
        ///     - then help transforming all Changes
        ///       into appropriate SVN-compatible diff history,
        ///       by progressively collecting all updated items (folders, files)
        ///       within a root FolderMetaData hierarchy
        ///       (updates may actually cancel each other out
        ///       in case of Adds/Deletes!)
        /// And all this in a way that is compatible with
        /// both forward and backward updates/logs.
        /// </summary>
        private void CalculateChangeBetweenVersions(string checkoutRootPath, string changePath, int changeVersion, FolderMetaData root, int sourceVersion, int targetVersion)
        {
            bool updatingForwardInTime = sourceVersion <= targetVersion;
            int lastVersion = sourceVersion;
            // Need loop iteration (the history fetching below
            // might be configured to return partial history parts):
            while (lastVersion != targetVersion)
            {
                int versionFrom = Math.Min(lastVersion, targetVersion) + 1;
                int versionTo = Math.Max(lastVersion, targetVersion);
                var historiesSorted = FetchSortedHistory(changePath, changeVersion, versionFrom, versionTo, updatingForwardInTime);

                bool madeProgress = false;
                bool needProcessHistories = (null != historiesSorted); // shortcut
                if (needProcessHistories)
                {
                    int previousLoopLastVersion = lastVersion;
                    CalculateChangeViaSourceItemHistories(historiesSorted, checkoutRootPath, root, updatingForwardInTime, ref lastVersion);
                    madeProgress = (previousLoopLastVersion != lastVersion);
                }
                if (!(madeProgress))
                {
                    break;
                }
            }
        }

        private IList<SourceItemHistory> FetchSortedHistory(string changePath, int changeVersion, int versionFrom, int versionTo, bool updatingForwardInTime)
        {
            IList<SourceItemHistory> historiesSorted = null;

            LogItem logItem = sourceControlProvider.GetLog(
                changePath,
                changeVersion,
                versionFrom, versionTo,
                Recursion.Full, // SVNBRIDGE_WARNING_REF_RECURSION
                // Hmm, why only 256 *here*?? This would match <see cref="TFSSourceControlProvider"/> TFS_QUERY_LIMIT
                // (which is being worked around over there),
                // thus I don't think we want to artificially specify it here as a constraint...
                // Well, now I think it is useful, to have a nicely observable limited amount to go through per each loop,
                // also to avoid excessive resource use on server/client sides.
                // UPDATE: we definitely need to do an *unrestricted* (int.MaxValue) query here instead,
                // since TFS QueryHistory() for an *older->newer* query unhelpfully prefers to discard *older* version entries
                // in case of insufficient request size.
                // The most cleanly doable way to deal with this might be
                // to reverse our UpdateDiffEngine loop to start with newest entries first,
                // but I don't think that we're prepared for this
                // and from my limited understanding I'm not even sure at all
                // whether it's fully possible to sanely construct a diff tree going from new->old.
                int.MaxValue);

            var numHistories = logItem.History.Length;
            bool needProcessHistories = (numHistories > 0); // shortcut
            if (needProcessHistories)
            {
                bool needCreateSortedCopy = (numHistories > 1); // common-case shortcut
                historiesSorted = (needCreateSortedCopy) ? Helper.SortHistories(updatingForwardInTime, logItem.History) : logItem.History;
            }

            return historiesSorted;
        }

        /// <summary>
        /// Side note: for larger data (large Changes.Count) processing might take very long,
        /// which may cause HTTP timeouts on client side.
        /// Since the data is being compiled by an instance of a clean independent class
        /// prior to it getting sent, we cannot do any interim "keep going" signalling
        /// (e.g. by sending XML <!-- ...> comment lines [would that be a valid operation in SVN protocol?]).
        /// Thus (barring reworking things into a more streamed way of doing things)
        /// there's no cure other than increasing a client's HTTP timeout
        /// (subversion: ~/.subversion/servers http-timeout setting).
        /// (well, yes there probably is: the TCP Keep-Alive setting
        /// should suffice to have this pain automatically dealt with).
        ///
        /// Debugging hint:
        /// changeset calc issues may be reproduced more easily
        /// by using something like
        /// <c>svn diff --xml --summarize -r rev_prev:rev_next [SPECIFIC_PATH...]</c>
        /// within an SVN working copy
        /// (pathspec may be e.g. "." or "SERVER_URL/repo_path" - see "svn info").
        /// (side note: near-uncurable syntax issues in XML here
        /// as used by code-doc, see
        /// http://stackoverflow.com/questions/9101673/android-xml-comments-with-double-dashes )
        /// Also, it is very useful to directly get a grasp
        /// of actual TFS-side changeset range differences:
        /// Snapshot them
        /// via init of an adhoc git repo within a TFS working copy
        /// and then doing git show --stat -M,
        /// to determine the truly authentic set of differences
        /// that SvnBridge then ought to end up capable
        /// of reliably producing as well.
        ///
        /// we need to go over the changeset in reverse order so we will process
        /// all the files first, and build the folder hierarchy that way
        /// Hmm, ok - we could be creating a reverse list in advance
        /// (and with all to-be-ignored entries already removed, too!),
        /// but since such reversal allocation likely would be a huge penalty
        /// for the common case of rather small changesets,
        /// decide to not do it.
        /// </summary>
        private void CalculateChangeViaSourceItemHistories(IList<SourceItemHistory> historiesSorted, string checkoutRootPath, FolderMetaData root, bool updatingForwardInTime, ref int lastVersion)
        {
            foreach (SourceItemHistory history in historiesSorted)
            {
                lastVersion = history.ChangeSetID;
                if (updatingForwardInTime == false)
                {
                    lastVersion -= 1;
                }

                // TODO UpdateDiffEngine should probably be changed to be instantiated *once* per the entire loop
                // (and also have it take the updatingForwardInTime bool as a member),
                // and then implement an Apply(change) method there which does all modifications
                // of an individual Change within the loop. After all the root (FolderMetaData)
                // will remain the same content instance for all engines (dito for all other members of Engine class!),
                // thus it does not make much sense to continually reinstantiate it
                // (but lastVersion value [_targetVersion member] would have to be supplied more dynamically per-method then, too).
                // [hmm, however it's possibly conceivable
                // that certain branching ops might require us
                // to apply the reverse direction for certain changes??]
                // Also, this would enable us to somehow centralize
                // the currently duplicated-use (UpdateDiffEngine _and_ UpdateDiffCalculator)
                // IsAddOperation() etc. helpers within UpdateDiffEngine
                // (then it's that class only which will be concerned with ChangeType evaluation).

                // Fixed MEGA BUG: the UpdateDiffEngine's "target" version
                // should be the one of the current Changeset i.e. lastVersion!!
                // (since this specific loop iteration is supposed to apply changes relevant to this Changeset only,
                // and *not* the targetVersion of the global loop processing).
                UpdateDiffEngine engine = new UpdateDiffEngine(
                    root,
                    checkoutRootPath,
                    lastVersion,
                    sourceControlProvider,
                    clientStateTracker,
                    additionForPropertyChangeOnly,
                    renamedItemsToBeCheckedForDeletedChildren);

                for (int i = history.Changes.Count - 1; i >= 0; --i)
                {
                    SourceItemChange change = history.Changes[i];

                    try
                    {
                        CheckShouldBeIgnored(change.Item.RemoteName);
                    }
                    catch(ShouldBeIgnoredException)
                    {
                        continue;
                    }

                    ApplyChangeOps(engine, change, updatingForwardInTime);
                }
                PerCommitPostProcessing(root);
            }
        }

        /// <summary>
        /// Temporary(?) helper. It probably should be refactored to become a method of UpdateDiffEngine instead.
        /// Well, no: that would mean having to move all Is*Operation() helpers into the engine as well,
        /// which is a bad idea. Hmm, OTOH all uses of such helpers are exclusively for engine processing.
        /// Since engine impl *is* dependent on TfsLibrary-side SourceItemChange knowledge,
        /// perhaps this *is* the proper thing to do after all.
        /// </summary>
        /// Certain combinations of change operations
        /// to be executed at this point (applying SVN-style changes)
        /// should possibly already have been resolved into sweet nothingness
        /// by earlier filtering in case they are complementary
        /// (e.g. a combined Rename | Delete change).
        /// Anyway, even if prior filtering ain't the case
        /// (perhaps the code does not do it, or we decided to not do it, ...),
        /// cleanly incremental/complementary handling
        /// as intended to be implemented here
        /// always ought to be able to come
        /// to the same final item status conclusion...
        private void ApplyChangeOps(UpdateDiffEngine engine, SourceItemChange change, bool updatingForwardInTime)
        {
            DebugIntercept(change);

            // ATTENTION ORDER: IsEditOperation() branch internally checks IsRenameOperation(), TOO!
            // (in general, these comparisons work on a possibly combined multi-bit mask!!)
            if (ChangeTypeAnalyzer.IsAddOperation(change, updatingForwardInTime))
            {
                engine.Add(change, updatingForwardInTime);
            }
            else if (ChangeTypeAnalyzer.IsDeleteOperation(change, updatingForwardInTime))
            {
                engine.Delete(change);
            }
            else if (ChangeTypeAnalyzer.IsEditOperation(change))
            {
                // We may have edit & rename operations
                if (ChangeTypeAnalyzer.IsRenameOperation(change))
                {
                    engine.Rename(change, updatingForwardInTime);
                }
                if (updatingForwardInTime == false)
                {
                    // FIXME: rather than dirtily fumbling a member of *foreign* objects,
                    // could we supply updatingForwardInTime param to Edit() as well,
                    // and then simply pass that into internal methods?
                    // But perhaps that's not equivalent
                    // since maybe it's the *item*'s .RemoteChangesetId
                    // which *needs* to be decremented to achieve the proper effect
                    // for all related uses of this item...
                    change.Item.RemoteChangesetId -= 1; // we turn the edit around, basically
                }
                engine.Edit(change);
            }
            else if (ChangeTypeAnalyzer.IsRenameOperation(change))
            {
                engine.Rename(change, updatingForwardInTime);
            }
            else
            {
                if (ChangeTypeAnalyzer.IsMergeOperation(change) && (change.Item.RemoteItemStatus == SourceItemStatus.Unmodified))
                {
                    // Simply skip this merge operation which is "logical" (no-op) (right?)
                    // We're very lucky in this no-op case...
                    // OTOH perhaps we still do need to add some special protocol parts
                    // (perhaps svn client should be able to show a 'G' for such a non-changed file, too).
                    // BTW for other Merge changes in many cases they have a flag *combination*
                    // i.e. they're already cleanly being handled via the Edit etc. handlers above anyway.
                    // I observed a Merge-only change
                    // where the reason for it not indicating any Edit, Delete etc. change ops
                    // was that someone in Merge source branch
                    // had done an Edit of the file
                    // in a prior commit,
                    // yet the Merge target branch
                    // already had the very same Edit in an unrelated commit
                    // done by someone else
                    // --> no indication of Edit change needed
                    // since Merge source vs. Merge target items were identical.
                }
                else
                {
                    // See also http://svnbridge.codeplex.com/workitem/13545
                    // (not trying to improve handling right now
                    // since I'm rather unsure of the suggested fix there
                    // and I'd think one can easily get it wrong
                    // and I don't have a test case...)
                    throw new NotSupportedException("Unsupported change type " + change.ChangeType);
                }
            }
        }

        [Conditional("DEBUG")]
        private void DebugIntercept(SourceItemChange change)
        {
            // To be user-modified in debugger watch during live session,
            // to be able to examine exactly the parts that are interesting.
            // Also, once it's known that something needs to be fixed,
            // best have it fixed in a verified manner via an xUnit test case.
            bool skipPathPatternSearch = (null == debugInterceptCheck);
            if (skipPathPatternSearch)
            {
                return;
            }

            string pathPattern = debugInterceptCheck;
            bool checkEndsWith = false;
            if (pathPattern.StartsWith("*"))
            {
                checkEndsWith = true;
                pathPattern = pathPattern.Substring(1);
            }

            bool caseInsensitive = true;
            bool found = SearchString(change.Item.RemoteName, pathPattern, caseInsensitive, checkEndsWith);
            if (found)
            {
                //    // DEBUG_SITE:
                //    Helper.DebugUsefulBreakpointLocation();
                //    System.Diagnostics.Debugger.Launch();
            }
        }

        private static bool SearchString(string candidate, string pattern, bool caseInsensitive, bool checkEndsWith)
        {
            bool found = false;

            string candidateCooked = candidate;
            string patternCooked = pattern;
            StringComparison comparison = caseInsensitive ?
                StringComparison.OrdinalIgnoreCase :
                StringComparison.Ordinal;
            // .Contains() does not support StringComparison --> need to use .IndexOf()
            // (http://stackoverflow.com/a/444818).
            found = checkEndsWith ?
                 candidateCooked.EndsWith(patternCooked, comparison) :
                 (-1 != candidateCooked.IndexOf(patternCooked, comparison));

            return found;
        }

        /// <summary>
        /// Does per-commit post-processing of all changes within that atomic(!) commit
        /// (note that one commit is the *atomically scoped* unit
        /// where all changes within that commit do get taken into account [immediately],
        /// irrespective of whether the working copy's version change
        /// is single-commit only or a larger version range,
        /// IOW such actions in fact need to be done immediately after each commit).
        /// Its per-commit tasks entail:
        /// - flattening deleted folders (removing all deleted sub items)
        /// </summary>
        private static void PerCommitPostProcessing(FolderMetaData root)
        {
            FlattenDeletedFolders(root);
        }

        /// <summary>
        /// This method ensures that we are not sending useless deletes to the client -
        /// if a folder is to be deleted, all its children are as well, which we remove
        /// at this phase:
        /// namely e.g. when transitioning a per-commit atomicity boundary,
        /// where a Delete (result of incremental/complementary calculation implementation)
        /// *is* the final non-revocable item state,
        /// prior to potential changes of subsequent commits.
        /// </summary>
        /// <param name="parentFolder">Folder where any deleted items ought to be recursively removed from</param>
        private static void FlattenDeletedFolders(FolderMetaData parentFolder)
        {
            foreach (ItemMetaData item in parentFolder.Items)
            {
                FolderMetaData folder = item as FolderMetaData;
                if (folder == null)
                {
                    continue;
                }
                if (folder is DeleteFolderMetaData)
                {
                    folder.Items.Clear();
                }
                else
                {
                    FlattenDeletedFolders(folder);
                }
            }
        }

        private void RemoveMissingItemsWhichAreChildrenOfRenamedItem(FolderMetaData root)
        {
            foreach (string item in renamedItemsToBeCheckedForDeletedChildren)
            {
                RemoveMissingItemsWhichAreChildrenOfRenamedItem(item, root);
            }
        }

        private static void RemoveMissingItemsWhichAreChildrenOfRenamedItem(string itemPath, FolderMetaData root)
        {
            FilesysHelpers.StripRootSlash(ref itemPath);

            StringComparison stringCompareMode =
                Configuration.SCMWantCaseSensitiveItemMatch ? StringComparison.InvariantCulture : StringComparison.InvariantCultureIgnoreCase;

            // Since we're proceeding destructively (removing some entries) below,
            // need to do iteration via an untouched *copy*.
            var items_destructive_iteration_copy = new List<ItemMetaData>(root.Items);
            foreach (ItemMetaData data in items_destructive_iteration_copy)
            {
                // a child of the currently renamed item?
                if (data is MissingItemMetaData)
                {
                    string nameMatchingSourceItemConvention = data.Name;
                    FilesysHelpers.StripRootSlash(ref nameMatchingSourceItemConvention);

                    if (nameMatchingSourceItemConvention.StartsWith(itemPath, stringCompareMode))
                    {
                        ItemHelpers.FolderOps_RemoveItem(root, data);
                        continue;
                    }
                }
                FolderMetaData folder = data as FolderMetaData;
                bool isFolder = (null != folder);
                if (isFolder)
                {
                    RemoveMissingItemsWhichAreChildrenOfRenamedItem(itemPath, folder);
                }
            }
        }

        private static ClientMissingFiles GetClientMissingFiles(string path, UpdateReportData reportData)
        {
            ClientMissingFiles clientMissingFiles = new ClientMissingFiles();
            if (reportData.Missing != null)
            {
                string pathPrefix = MakePrefixPath(path);
                foreach (string missingPath in reportData.Missing)
                {
                    clientMissingFiles[pathPrefix + missingPath] = missingPath;
                }
            }
            return clientMissingFiles;
        }

        private static ClientExistingFiles GetClientExistingFiles(string path, UpdateReportData reportData)
        {
            ClientExistingFiles clientExistingFiles = new ClientExistingFiles();
            if (reportData.Entries != null)
            {
                string pathPrefix = MakePrefixPath(path);
                foreach (EntryData entryData in reportData.Entries)
                {
                    clientExistingFiles[pathPrefix + entryData.path] = int.Parse(entryData.Rev);
                }
            }
            return clientExistingFiles;
        }

        private static string MakePrefixPath(string path)
        {
            string pathPrefix =
                string.IsNullOrEmpty(path) ?
                "/" :
                "/" + path + "/";
            return pathPrefix;
        }

        private static ClientStateTracker ConstructClientStateFromSVNUpdateReportData(
            string checkoutRootPath,
            UpdateReportData updateReportData)
        {
            // Prefer constructing ClientStateTracker members here
            // rather than having a ClientStateTracker ctor which takes UpdateReportData param.
            // That way we keep the SVN-specific UpdateReportData dependency away from ClientStateTracker.
            ClientExistingFiles clientExistingFiles = GetClientExistingFiles(checkoutRootPath, updateReportData);
            ClientMissingFiles clientMissingFiles = GetClientMissingFiles(checkoutRootPath, updateReportData);
            ClientStateTracker clientStateTracker = new ClientStateTracker(clientExistingFiles, clientMissingFiles);

            return clientStateTracker;
        }

        private static void CheckShouldBeIgnored(string file)
        {
            // This check can be completely disabled
            // since currently(!?) the history fetched from the provider
            // already is pre-processed to not mention any useless (thus unwanted)
            // *base container folders* for property storage
            // any more anyway...
            ////return Path.GetFileName(file).Equals(Constants.PropFolder);
            //return sourceControlProvider.IsPropertyFolder(file);
            //return false;
            //if (shouldBeIgnored)
            //{
            //    throw new ShouldBeIgnoredException();
            //}
        }
    }
}
