using System; // InvalidOperationException , StringComparison
using System.Collections.Generic; // Dictionary , List
using System.Diagnostics; // Debug.WriteLine()
using CodePlex.TfsLibrary.ObjectModel; // SourceItemChange
using CodePlex.TfsLibrary.RepositoryWebSvc; // ChangeType , ItemType
using SvnBridge.Infrastructure; // Configuration
using SvnBridge.Utility; // DebugRandomActivator

namespace SvnBridge.SourceControl
{
    public class UpdateDiffEngine
    {
        private readonly TFSSourceControlProvider sourceControlProvider;
        private readonly ClientStateTracker clientStateTracker;
        private readonly List<string> renamedItemsToBeCheckedForDeletedChildren;
        private readonly Dictionary<ItemMetaData, bool> additionForPropertyChangeOnly;
        private readonly FolderMetaData _root;
        private readonly string _checkoutRootPath;
        private readonly int _targetVersion;
        private readonly DebugRandomActivator debugRandomActivator;

        public UpdateDiffEngine(FolderMetaData root,
                    string checkoutRootPath,
                    int targetVersion,
                    TFSSourceControlProvider sourceControlProvider,
                    ClientStateTracker clientStateTracker,
                    Dictionary<ItemMetaData, bool> additionForPropertyChangeOnly,
                    List<string> renamedItemsToBeCheckedForDeletedChildren)
        {
            this._root = root;
            this._checkoutRootPath = checkoutRootPath;
            this._targetVersion = targetVersion;
            this.debugRandomActivator = new DebugRandomActivator();
            this.sourceControlProvider = sourceControlProvider;
            this.clientStateTracker = clientStateTracker;
            this.additionForPropertyChangeOnly = additionForPropertyChangeOnly;
            this.renamedItemsToBeCheckedForDeletedChildren = renamedItemsToBeCheckedForDeletedChildren;
        }

        public void Add(SourceItemChange change, bool updatingForwardInTime)
        {
            PerformAddOrUpdate(change, false, updatingForwardInTime);
        }

        /// <summary>
        /// Small helper to retain interface backwards compat.
        /// </summary>
        public void Add(SourceItemChange change)
        {
            Add(change, true);
        }

        public void Edit(SourceItemChange change)
        {
            PerformAddOrUpdate(change, true, true);
        }

        public void Delete(SourceItemChange change)
        {
            string remoteName = change.Item.RemoteName;

            // we ignore it here because this only happens when the related item
            // is deleted, and at any rate, this is a SvnBridge implementation detail
            // which the client is not concerned about
            if (sourceControlProvider.IsPropertyFolderElement(remoteName))
            {
                return;
            }
            ProcessDeletedItem(remoteName, change);
        }

        public void Rename(SourceItemChange change, bool updatingForwardInTime)
        {
            string itemOldName = RenameDetermineItemOldName(change);
            string itemNewName = change.Item.RemoteName;

            // origin item not within (somewhere below) our checkout root? --> skip indication of Add or Delete!
            bool attentionOriginItemOfForeignScope = IsOriginItemOfForeignScope(
                change,
                itemOldName);

            string itemOldNameIndicated;
            string itemNewNameIndicated;
            bool processDelete = true, processAdd = true;
            if (updatingForwardInTime)
            {
                itemOldNameIndicated = itemOldName;
                itemNewNameIndicated = itemNewName;
                if (attentionOriginItemOfForeignScope)
                {
                    processDelete = false;
                }
            }
            else
            {
                itemOldNameIndicated = itemNewName;
                itemNewNameIndicated = itemOldName;
                if (attentionOriginItemOfForeignScope)
                {
                    processAdd = false;
                }
            }

            // svn diff output of a real Subversion server
            // always _first_ generates '-' diff for old file and _then_ '+' diff for new file
            // irrespective of whether one is doing forward- or backward-diffs,
            // and tools such as "svn up" or git-svn
            // do rely on that order being correct
            // (e.g. svn would otherwise suffer from
            // failing property queries on non-existent files),
            // thus need to always use _fixed order_ of delete/add.
            // NOTE: maybe/possibly this constraint of diff ordering
            // should be handled on UpdateReportService side only
            // and not during ItemMetaData queueing here yet,
            // in case subsequent ItemMetaData handling
            // happened to expect a different order.
            if (processDelete)
            {
                ProcessDeletedItem(itemOldNameIndicated, change);
            }
            if (processAdd)
            {
                ProcessAddedOrUpdatedItem(itemNewNameIndicated, change, false, false, updatingForwardInTime);
            }

            if (change.Item.ItemType == ItemType.Folder)
            {
                renamedItemsToBeCheckedForDeletedChildren.Add(itemNewNameIndicated);
            }
        }

        /// <summary>
        /// Depending on change type, proceeds to determine old name via RenamedSourceItem members
        /// or (if we can't help it) fetch that info via very expensive multi-request network activity.
        /// </summary>
        /// <param name="change">SourceItemChange to be analyzed</param>
        /// <returns>Old name of the item</returns>
        private string RenameDetermineItemOldName(SourceItemChange change)
        {
            string itemOldNameMember = null;
            string itemOldNameQueried = null;
            int itemOldRevMember = -1;
            int itemOldRevQueried = -1;

            bool haveMemberData = false, haveQueryData = false;
            // Special handling - in case of a RenamedSourceItem,
            // we're fortunately able to skip the very expensive GetPreviousVersionOfItems() requests call
            // since the desired content is already provided by RenamedSourceItems members.
            // This is very important and beneficial in the case of huge mass renames
            // (moving content into another directory).
            if (ChangeTypeAnalyzer.IsRenameOperation(change))
            {
                var renamedItem = (RenamedSourceItem)change.Item;
                itemOldNameMember = renamedItem.OriginalRemoteName;
                itemOldRevMember = renamedItem.OriginalRevision;
                haveMemberData = true;
            }

            bool needQueryOldName = (!haveMemberData);
            if (!needQueryOldName)
            {
                int doVerificationPercentage = 5; // do a verification in x% of random results.
                bool wantQueryOldName = debugRandomActivator.YieldTrueOnPercentageOfCalls(doVerificationPercentage);

                needQueryOldName = wantQueryOldName;
            }
            //needQueryOldName = true; // UNCOMMENT IN CASE OF TROUBLE!
            if (needQueryOldName)
            {
                ItemMetaData oldItem = sourceControlProvider.GetPreviousVersionOfItems(new SourceItem[] { change.Item }, change.Item.RemoteChangesetId)[0];
                itemOldNameQueried = oldItem.Name;
                itemOldRevQueried = oldItem.Revision;
                haveQueryData = true;
            }

            bool canCompareResults = (haveMemberData && haveQueryData);
            if (canCompareResults)
            {
                // [performance: comparison of less complex objects first!]
                bool isMatch = (
                    (itemOldRevMember == itemOldRevQueried) &&
                    (itemOldNameMember == itemOldNameQueried)
                );
                if (!(isMatch))
                {
                    // Repeat a query locally, to retain full debugging possibility/content here.
                    ItemMetaData oldItem = sourceControlProvider.GetPreviousVersionOfItems(new SourceItem[] { change.Item }, change.Item.RemoteChangesetId)[0];

                    string logMessage = string.Format("Mismatch: RenamedSourceItem member data (rev {0}, {1}) vs. queried data (rev {2}, {3}), please report!",
                                                                        itemOldRevMember, itemOldNameMember, itemOldRevQueried, itemOldNameQueried);
                    bool doThrowException = true;
                    // doThrowException = false; // UNCOMMENT TO CONTINUE COLLECTING MISMATCHES
                    if (doThrowException)
                    {
                        throw new InvalidOperationException(logMessage);
                    }
                    else
                    {
                        // It is said that to have output appear in VS Output window, we need to use Debug.WriteLine rather than Console.WriteLine.
                        Debug.WriteLine(logMessage + "\n");
                    }
                }
            }

            return haveMemberData ? itemOldNameMember : itemOldNameQueried;
        }

        /// <remarks>
        /// SPECIAL CASE for *foreign-location* "renames"
        /// (e.g. foreign Team Project, or probably simply not within (somewhere below) our checkout root):
        /// Some Change may indicate Edit | Rename | Merge,
        /// with oldItem.Name being REPO1/somefolder/file.h and
        /// change.Item.RemoteName being REPO2/somefolder/file.h
        /// IOW we actually have a *merge* rather than a *rename*.
        /// Since the foreign-repo (REPO1) part will *not* be modified (deleted),
        /// and our handling would modify this into a Delete of REPO2/somefolder/file.h,
        /// which is a *non-existing, newly added* file,
        /// this means that the Delete part of the Delete/Add combo should not be indicated
        /// to the SVN side (would croak on the non-existing file).
        /// Now the question remains whether this special-casing here is still fine as well
        /// in case the user's checkout root ends up somewhere *below* the actual repository root,
        /// IOW both old and new name are within the *same* repo and thus Delete
        /// of the pre-existing item is valid/required (TODO?).
        /// </remarks>
        private bool IsOriginItemOfForeignScope(
            SourceItemChange change,
            string itemOriginName)
        {
            bool isOriginItemOfForeignScope = false;

            bool needCheckForSkippingForeignScopeItems = (ChangeTypeAnalyzer.IsMergeOperation(change));
            if (needCheckForSkippingForeignScopeItems)
            {
                StringComparison stringCompareMode =
                    Configuration.SCMWantCaseSensitiveItemMatch ? StringComparison.InvariantCulture : StringComparison.InvariantCultureIgnoreCase;

                bool isItemResidingWithinCheckoutRoot = itemOriginName.StartsWith(_checkoutRootPath, stringCompareMode);
                if (!(isItemResidingWithinCheckoutRoot))
                {
                    isOriginItemOfForeignScope = true;
                }
            }

            return isOriginItemOfForeignScope;
        }

        private void PerformAddOrUpdate(SourceItemChange change, bool edit, bool updatingForwardInTime)
        {
            string remoteName = change.Item.RemoteName;

            bool propertyChange = false;

            // Property stuff, similar to Delete() handler...
            if (sourceControlProvider.IsPropertyFolderElement(remoteName))
            {
                if (sourceControlProvider.IsPropertyFolder(remoteName))
                {
                    return;
                }

                if (sourceControlProvider.IsPropertyFile(remoteName))
                {
                    propertyChange = true;
                    remoteName = GetRemoteNameOfPropertyChange(remoteName);
                }
            }

            ProcessAddedOrUpdatedItem(remoteName, change, propertyChange, edit, updatingForwardInTime);
        }

        private static string GetRemoteNameOfPropertyChange(string remoteName)
        {
            string propFolderPlusSlash = Constants.PropFolder + "/";
            string propFolderSlashPrefix = "/" + propFolderPlusSlash;
            if (remoteName.Contains(propFolderSlashPrefix))
            {
                if (remoteName.EndsWith(propFolderSlashPrefix + Constants.FolderPropFile))
                {
                    remoteName = remoteName.Substring(0, remoteName.Length - (propFolderSlashPrefix + Constants.FolderPropFile).Length);
                }
                else
                {
                    remoteName = remoteName.Replace(propFolderSlashPrefix, "/");
                }
            }
            else if (remoteName.StartsWith(propFolderPlusSlash))
            {
                if (remoteName.Equals(propFolderPlusSlash + Constants.FolderPropFile))
                {
                    remoteName = "";
                }
                else
                {
                    remoteName = remoteName.Substring(propFolderPlusSlash.Length);
                }
            }
            return remoteName;
        }

        private void ProcessAddedOrUpdatedItem(string remoteName, SourceItemChange change, bool propertyChange, bool edit, bool updatingForwardInTime)
        {
            bool isChangeAlreadyCurrentInClientState = clientStateTracker.IsChangeAlreadyCurrentInClientState(
                ChangeType.Add,
                remoteName,
                change.Item.RemoteChangesetId);
            if (isChangeAlreadyCurrentInClientState)
            {
                return;
            }

            // Special case for changes of source root item (why? performance opt?):
            if (ItemMetaData.IsSamePath(remoteName, _checkoutRootPath))
            {
                ItemMetaData item = sourceControlProvider.GetItems(_targetVersion, remoteName, Recursion.None);
                if (item != null)
                {
                    _root.Properties = item.Properties;
                }
            }
            else // standard case (other items)
            {
                FolderMetaData folder = _root;
                string remoteNameStart = _checkoutRootPath;
                string itemPath = remoteNameStart;

                string[] pathElems = GetSubPathElems_PossiblyBelowSpecificRoot(remoteNameStart, remoteName);
                int pathElemsCount = pathElems.Length;
                for (int i = 0; i < pathElemsCount; i++)
                {
                    bool isLastPathElem = (i == pathElemsCount - 1);

                    FilesysHelpers.PathAppendElem(ref itemPath, pathElems[i]);

                    // Detect our possibly pre-existing record of this item within the changeset version range
                    // that we're in the process of analyzing/collecting...
                    // This existing item may possibly be a placeholder (stub folder).
                    ItemMetaData item = folder.FindItem(itemPath);
                    bool doReplaceByNewItem = (item == null);
                    if (!doReplaceByNewItem) // further checks...
                    {
                        if (isLastPathElem) // only if final item...
                        {
                            bool existingItemsVersionIsOutdated =
                                updatingForwardInTime ?
                                    (item.Revision < change.Item.RemoteChangesetId) : (item.Revision > change.Item.RemoteChangesetId);
                            // I seem to not like this IsDeleteMetaDataKind() check here...
                            // (reasoning: we're doing processing from Changeset to Changeset,
                            // thus if an old Changeset item happened to be a Delete yet a new non-Delete
                            // comes along, why *shouldn't* the Delete get superceded??)
                            // Ah... because it's the *other* code branch below
                            // which then deals with IsDeleteMetaDataKind() updating...
                            // Still, that sounds like our logic evaluation here
                            // is a bit too complex, could be simplified.
                            // Well, the whole special-casing below seems to be
                            // for properly setting the OriginallyDeleted member...
                            if (existingItemsVersionIsOutdated && !IsDeleteMetaDataKind(item))
                                doReplaceByNewItem = true;
                        }
                    }
                    // So... do we actively want to grab a new item?
                    if (doReplaceByNewItem)
                    {
                        // First remove this prior item...
                        if (item != null)
                        {
                            folder.Items.Remove(item);
                        }
                        // ...and fetch the updated one
                        // (forward *or* backward change)
                        // for the currently processed version:
                        var processedVersion = _targetVersion;
                        Recursion recursionMode = GetRecursionModeForItemAdd(updatingForwardInTime);
                        item = sourceControlProvider.GetItems(processedVersion, itemPath, recursionMode);
                        if (item == null)
                        {
                            // THIS IS *NOT* TRUE!!
                            // A subsequently executed delete operation handler depends on being able to discover a previously added item
                            // in order to have it *cleanly* removed (reverted!) into oblivion
                            // rather than queueing a (bogus) *active* DeleteItemMetaData indication.
                            // If there's no item remaining to revert against,
                            // this means the bogus delete operation will remain standing,
                            // which is a fatal error
                            // when sending an SVN delete op to the client
                            // which then complains
                            // that *within this particular changeset update range* (for incremental updates!)
                            // the file did not actually go from existence to non-existence
                            // (--> "is not under version control").
                            // Or, in other words,
                            // we simply CANNOT TAKE ANY SHORTCUTS WITHIN THE FULLY INCREMENTAL HANDLING OF CHANGESET UPDATES,
                            // each step needs to be properly accounted for
                            // in order for subsequent steps to be able to draw their conclusions from prior state!
                            // Adding dirty final "post-processing" to get rid of improper delete-only entries
                            // (given an actually pre-non-existing item at client's start version!)
                            // would be much less desirable
                            // than having all incremental change recordings
                            // resolve each other
                            // in a nicely fully complementary manner.
#if false
                            if (ChangeTypeAnalyzer.IsRenameOperation(change))
                            {
                                // TFS will report renames even for deleted items -
                                // since TFS reported above that this was renamed,
                                // but it doesn't exist in this revision,
                                // we know it is a case of renaming a deleted file.
                                // We can safely ignore this and any of its children.
                                return;
                            }
#endif
                            if (isLastPathElem && propertyChange)
                            {
                                return;
                            }
                            item = new MissingItemMetaData(itemPath, processedVersion, edit);
                        }
                        if (!isLastPathElem)
                        {
                            StubFolderMetaData stubFolder = new StubFolderMetaData();
                            stubFolder.RealFolder = (FolderMetaData)item;
                            stubFolder.Name = item.Name;
                            stubFolder.ItemRevision = item.ItemRevision;
                            stubFolder.PropertyRevision = item.PropertyRevision;
                            stubFolder.LastModifiedDate = item.LastModifiedDate;
                            stubFolder.Author = item.Author;
                            item = stubFolder;
                        }
                        folder.Items.Add(item);
                        SetAdditionForPropertyChangeOnly(item, propertyChange);
                    }
                    else if ((item is StubFolderMetaData) && isLastPathElem)
                    {
                        folder.Items.Remove(item);
                        folder.Items.Add(((StubFolderMetaData)item).RealFolder);
                    }
                    else if (IsDeleteMetaDataKind(item))
                    { // former item was a DELETE...

                        // ...and new one then _resurrects_ the (_actually_ deleted) item:
                        if (ChangeTypeAnalyzer.IsAddOperation(change))
                        {
                          if (!propertyChange)
                          {
                              folder.Items.Remove(item);
                              item = sourceControlProvider.GetItems(change.Item.RemoteChangesetId, itemPath, Recursion.None);
                              item.OriginallyDeleted = true;
                              folder.Items.Add(item);
                          }
                        }
//                        // [section below was a temporary patch which should not be needed any more now that our processing is much better]
//#if false
                        // ...or _renames_ the (pseudo-deleted) item!
                        // (OBEY VERY SPECIAL CASE: _similar-name_ rename (EXISTING ITEM LOOKUP SUCCESSFUL ABOVE!!), i.e. filename-case-only change)
                        else if (ChangeTypeAnalyzer.IsRenameOperation(change))
                        {
                          // Such TFS-side renames need to be reflected
                          // as a SVN delete/add (achieve rename *with* history!) operation,
                          // thus definitely *append* an ADD op to the *existing* DELETE op.
                          // [Indeed, for different-name renames,
                          // upon "svn diff" requests
                          // SvnBridge does generate both delete and add diffs,
                          // whereas for similar-name renames it previously did not -> buggy!]
                          item = sourceControlProvider.GetItems(change.Item.RemoteChangesetId, itemPath, Recursion.None);
                          folder.Items.Add(item);
                        }
//#endif
                    }
                    if (isLastPathElem == false) // this conditional merely required to prevent cast of non-FolderMetaData-type objects below :(
                    {
                        folder = (FolderMetaData)item;
                    }
                }
            }
        }

        /// <remarks>
        /// In case of *backward*-adding a *forward*-deleted directory,
        /// we also need to resurrect the entire deleted directory's hierarchy (sub files etc.).
        /// UPDATE: undoing this "questionable" feature now -
        /// I believe that the actual reason that this commit decided to undo it
        /// is that TFS history will already supply full Change info
        /// (about all those sub items affected by a base directory state change,
        /// that is).
        /// IOW, again: never ever do strange "shortcuts"
        /// rather than implementing fully incremental diff engine handling!
        /// </remarks>
        private static Recursion GetRecursionModeForItemAdd(bool updatingForwardInTime)
        {
            //return updatingForwardInTime ? Recursion.None : Recursion.Full;
            return Recursion.None;
        }

        private void SetAdditionForPropertyChangeOnly(ItemMetaData item, bool propertyChange)
        {
            if (item == null)
                return;
            if (propertyChange == false)
            {
                additionForPropertyChangeOnly[item] = propertyChange;
            }
            else
            {
                if (additionForPropertyChangeOnly.ContainsKey(item) == false)
                    additionForPropertyChangeOnly[item] = propertyChange;
            }
        }

        private bool IsAdditionForPropertyChangeOnly(ItemMetaData item)
        {
            bool bResult = false;

            // http://stackoverflow.com/questions/9382681/what-is-more-efficient-dictionary-trygetvalue-or-containskeyitem
            if (additionForPropertyChangeOnly.ContainsKey(item))
            {
                bool bDictEntry = additionForPropertyChangeOnly[item];
                bResult = bDictEntry;
            }

            return bResult;
        }

        private void ProcessDeletedItem(string remoteName, SourceItemChange change)
        {
            bool isChangeAlreadyCurrentInClientState = clientStateTracker.IsChangeAlreadyCurrentInClientState(
                ChangeType.Delete,
                remoteName,
                change.Item.RemoteChangesetId);
            if (isChangeAlreadyCurrentInClientState)
            {
                RemoveMissingItem(remoteName, _root);
                return;
            }

            FolderMetaData folder = _root;
            // deactivated [well... that turned out to be the SAME value!! Perhaps it was either deprecated or future handling...]:
            //string remoteNameStart = remoteName.StartsWith(_checkoutRootPath) ? _checkoutRootPath : itemPath;
            string remoteNameStart = _checkoutRootPath;
            string itemPath = remoteNameStart;

            string[] pathElems = GetSubPathElems_PossiblyBelowSpecificRoot(remoteNameStart, remoteName);
            int pathElemsCount = pathElems.Length;
            for (int i = 0; i < pathElemsCount; i++)
            {
                bool isLastPathElem = (i == pathElemsCount - 1);

                FilesysHelpers.PathAppendElem(ref itemPath, pathElems[i]);

                bool isFullyHandled = HandleDeleteItem(remoteName, change, itemPath, ref folder, isLastPathElem);
                if (isFullyHandled)
                    break;
            }
            if (pathElemsCount == 0)//we have to delete the checkout root itself
            {
                HandleDeleteItem(remoteName, change, itemPath, ref folder, true);
            }
        }

        private bool HandleDeleteItem(string remoteName, SourceItemChange change, string itemPath, ref FolderMetaData folder, bool isLastPathElem)
        {
            ItemMetaData item = folder.FindItem(itemPath);
            // Shortcut: valid item in our cache, and it's a delete already? We're done :)
            if (IsDeleteMetaDataKind(item))
                return true;

            if (item == null)
            {
                if (isLastPathElem)
                {
                    if (change.Item.ItemType == ItemType.File)
                        item = new DeleteMetaData();
                    else
                        item = new DeleteFolderMetaData();

                    item.Name = remoteName;
                    item.ItemRevision = change.Item.RemoteChangesetId;
                }
                else
                {
                    var processedVersion = _targetVersion;
                    item = sourceControlProvider.GetItemsWithoutProperties(processedVersion, itemPath, Recursion.None);
                    if (item == null)
                    {
                        // FIXME: hmm, are we really supposed to actively Delete a non-isLastPathElem item
                        // rather than indicating a MissingItemMetaData!?
                        // After all the actual delete operation is expected to be carried out (possibly later) properly, too...
                        item = new DeleteFolderMetaData();
                        item.Name = itemPath;
                        item.ItemRevision = processedVersion;
                    }
                    else
                    {
                        // This item is NOT the final one (isLastPathElem == true), only a helper,
                        // thus it certainly shouldn't
                        // directly indicate real actions (add/delete) yet
                        // within the currently processed Changeset,
                        // which it would if we now queued the live item directly rather than
                        // providing a StubFolderMetaData indirection for it...
                        StubFolderMetaData stubFolder = new StubFolderMetaData();
                        stubFolder.RealFolder = (FolderMetaData)item;
                        stubFolder.Name = item.Name;
                        stubFolder.ItemRevision = item.ItemRevision;
                        stubFolder.PropertyRevision = item.PropertyRevision;
                        stubFolder.LastModifiedDate = item.LastModifiedDate;
                        stubFolder.Author = item.Author;
                        item = stubFolder;
                    }
                }
                folder.Items.Add(item);
            }
            else if (isLastPathElem) // we need to revert the item addition
            {
                var processedVersion = _targetVersion;
                if (item.OriginallyDeleted) // convert back into a delete
                {
                    folder.Items.Remove(item);
                    if (change.Item.ItemType == ItemType.File)
                        item = new DeleteMetaData();
                    else
                        item = new DeleteFolderMetaData();

                    item.Name = remoteName;
                    item.ItemRevision = change.Item.RemoteChangesetId;
                    folder.Items.Add(item);
                }
                else if (item is StubFolderMetaData)
                {
                    DeleteFolderMetaData itemDeleteFolder = new DeleteFolderMetaData();
                    itemDeleteFolder.Name = item.Name;
                    itemDeleteFolder.ItemRevision = processedVersion;
                    folder.Items.Remove(item);
                    folder.Items.Add(itemDeleteFolder);
                }
                else if (IsAdditionForPropertyChangeOnly(item))
                {
                    ItemMetaData itemDelete = item is FolderMetaData
                                                    ? (ItemMetaData)new DeleteFolderMetaData()
                                                    : new DeleteMetaData();
                    itemDelete.Name = item.Name;
                    itemDelete.ItemRevision = processedVersion;
                    folder.Items.Remove(item);
                    folder.Items.Add(itemDelete);
                }
                else if (item is MissingItemMetaData && ((MissingItemMetaData)item).Edit == true)
                {
                    ItemMetaData itemDelete = new DeleteMetaData();
                    itemDelete.Name = item.Name;
                    itemDelete.ItemRevision = processedVersion;
                    folder.Items.Remove(item);
                    folder.Items.Add(itemDelete);
                }
                else
                {
                    folder.Items.Remove(item);
                }
            }
            folder = (item as FolderMetaData) ?? folder;
            return false;
        }

        private static string[] GetSubPathElems_PossiblyBelowSpecificRoot(string root, string path)
        {
            string[] pathElems;

            string pathSub = GetSubPath_PossiblyBelowSpecificRoot(root, path);
            pathElems = pathSub.Split('/');

            return pathElems;
        }

        private static string GetSubPath_PossiblyBelowSpecificRoot(string root, string path)
        {
            string subPath;

            // NOTE: former duplicated code locations seem to have been *buggy*:
            // one did .Length + 1 and the other .Length only!
            // This is why I also believe that the StringSplitOptions.RemoveEmptyEntries
            // was a workaround against one initial '/' not getting removed due to missing "+ 1".
            // So, for now on the user side try doing .Split() without .RemoveEmptyEntries...
            //   pathElems = path.Substring(root.Length + 1).Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            bool haveNoRoot = root.Equals("");
            bool isRootSpecified = !(haveNoRoot);
            subPath = isRootSpecified ? path.Substring(root.Length + 1) : path;

            return subPath;
        }

        private bool RemoveMissingItem(string name, FolderMetaData folder)
        {
            foreach (ItemMetaData item in folder.Items)
            {
                if (item.Name.Equals(name) && item is MissingItemMetaData)
                {
                    folder.Items.Remove(item);
                    return true;
                }
                FolderMetaData subFolder = item as FolderMetaData;
                if (subFolder != null)
                {
                    if (RemoveMissingItem(name, subFolder))
                        return true;
                }
            }
            return false;
        }

        private static bool IsDeleteMetaDataKind(ItemMetaData item)
        {
          return (item is DeleteFolderMetaData || item is DeleteMetaData);
        }
    }
}
