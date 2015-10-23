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

        public void Edit(SourceItemChange change, bool updatingForwardInTime)
        {
            PerformAddOrUpdate(change, true, updatingForwardInTime);
        }

        /// <summary>
        /// Simplistic (read: likely incorrect
        /// due to insufficiently precise / incomplete parameterization) variant.
        /// AVOID ITS USE.
        /// </summary>
        /// <param name="change"></param>
        public void Edit(SourceItemChange change)
        {
            Edit(change, true);
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
            ProcessDeletedItem(remoteName, change, true);
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
                ProcessDeletedItem(itemOldNameIndicated, change, updatingForwardInTime);
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
        /// UPDATE: NOPE, we should *not* focus on this being a Merge change -
        /// rather, the criteria is that some side of this change
        /// is NOT located within our checkout root,
        /// and this may be the case
        /// for either Merge *or* Rename (at least those two).
        /// </remarks>
        private bool IsOriginItemOfForeignScope(
            SourceItemChange change,
            string itemOriginName)
        {
            bool isOriginItemOfForeignScope = false;

            bool needCheckForSkippingForeignScopeItems = (ChangeTypeAnalyzer.IsRenameOperation(change) || ChangeTypeAnalyzer.IsMergeOperation(change));
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
            string remoteName = updatingForwardInTime ? change.Item.RemoteName : RenameDetermineItemOldName(change);

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
                    remoteName = WebDAVPropertyStorageAdaptor.GetRemoteNameOfPropertyChange(remoteName);
                }
            }

            ProcessAddedOrUpdatedItem(remoteName, change, propertyChange, edit, updatingForwardInTime);
        }

        private void ProcessAddedOrUpdatedItem(string remoteName, SourceItemChange change, bool propertyChange, bool edit, bool updatingForwardInTime)
        {
            var itemFetchRevision = GetFetchRevision(
                change.Item.RemoteChangesetId,
                updatingForwardInTime);

            bool isChangeAlreadyCurrentInClientState = clientStateTracker.IsChangeAlreadyCurrentInClientState(
                ChangeType.Add,
                remoteName,
                itemFetchRevision);
            if (isChangeAlreadyCurrentInClientState)
            {
                return;
            }

            bool newlyAddedLastElem = (!edit);

            // Special case for changes of source root item (why? performance opt?):
            if (ItemMetaData.IsSamePath(remoteName, _checkoutRootPath))
            {
                ItemMetaData item = sourceControlProvider.GetItems(itemFetchRevision, remoteName, Recursion.None);
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
                for (int i = 0; i < pathElemsCount; ++i)
                {
                    bool newlyAdded = newlyAddedLastElem;

                    bool isLastPathElem = (i == pathElemsCount - 1);

                    FilesysHelpers.PathAppendElem(ref itemPath, pathElems[i]);

                    // Detect our possibly pre-existing record of this item within the changeset version range
                    // that we're in the process of analyzing/collecting...
                    // This existing item may possibly be a placeholder (stub folder).
                    ItemMetaData itemPrev = folder.FindItem(itemPath);
                    ItemMetaData item = itemPrev;
                    bool doReplaceByNewItem = (itemPrev == null);
                    if (!doReplaceByNewItem) // further checks...
                    {
                        if (isLastPathElem) // only if final item...
                        {
                            // Hmm, to properly fully incrementally handle
                            // interim/*temporary* item Changes within *one single* Changeset, too
                            // (e.g. Rename from item source1 to destination1,
                            // then Rename/Merge/whatever something to source1),
                            // this isOutdated check might be exactly wrong
                            // since it might need to do <= / >= checks then,
                            // to have subsequent "conflicting-location" Changes
                            // properly replace old items
                            // (currently we're doing handling of such direct
                            // replacements further below,
                            // which might or might not be fully correct).
                            bool existingItemsVersionIsOutdated =
                                updatingForwardInTime ?
                                    (itemPrev.Revision < change.Item.RemoteChangesetId) : (itemPrev.Revision > change.Item.RemoteChangesetId);
                            // I seem to not like this IsDeleteMetaDataKind() check here...
                            // (reasoning: we're doing processing from Changeset to Changeset,
                            // thus if an old Changeset item happened to be a Delete yet a new non-Delete
                            // comes along, why *shouldn't* the Delete get superceded??)
                            // Ah... because it's the *other* code branch below
                            // which then deals with IsDeleteMetaDataKind() updating...
                            // Still, that sounds like our logic evaluation here
                            // is a bit too complex, could be simplified.
                            // Well, the whole special-casing below seemed to be
                            // for properly setting the now-reworked (replaced)
                            // OriginallyDeleted member...
                            if (existingItemsVersionIsOutdated && !IsDeleteMetaDataKind(itemPrev))
                                doReplaceByNewItem = true;
                        }
                    }
                    // So... do we actively want to grab a new item?
                    if (doReplaceByNewItem)
                    {
                        // First remove this prior item...
                        if (itemPrev != null)
                        {
                            ItemHelpers.FolderOps_RemoveItem(folder, itemPrev);
                            // FIXME: rather than doing a non-atomic separate Remove/Add,
                            // should likely be *swapping* related items
                            // directly in one atomic op,
                            // to have their existing item status such as .NewlyAdded
                            // taken into account centrally internally.
                            newlyAdded = itemPrev.NewlyAdded;
                        }
                        // ...and fetch the updated one
                        // (forward *or* backward change)
                        // for the currently processed version:
                        Recursion recursionMode = GetRecursionModeForItemAdd(updatingForwardInTime);
                        item = sourceControlProvider.GetItems(itemFetchRevision, itemPath, recursionMode);
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
                            item = new MissingItemMetaData(itemPath, itemFetchRevision, edit);
                            // From an incremental-handling POV even a "missing item"
                            // (which may indicate e.g. a previously-deleted item
                            // within a parent-folder rename)
                            // *has* been *newly* added
                            // (*actively* entered SCM history within the processed commit range)
                            // to occupy its location,
                            // i.e. its addition *should* be cleanly symmetrically revertable
                            // by a subsequent Delete-change request,
                            // thus we should NOT force indicating .NewlyAdded as false.
                            //newlyAdded = false;
                            // Well in fact we SHOULD actively (locally) override-assign newlyAdded state to true here:
                            newlyAdded = true;
                        }
                        if (!isLastPathElem)
                        {
                            // Currently determined required state of newlyAdded
                            // belongs to *this* item
                            // (rather than its stub folder wrapper)!
                            item.NewlyAdded = newlyAdded;
                            newlyAdded = false;
                            item = ProvideHelperFolderWhileItsChangeStatusNonFinal((FolderMetaData)item);
                        }
                        Folder_AddItem(folder, item, newlyAdded);
                        SetAdditionForPropertyChangeOnly(item, propertyChange);
                    }
                    else if ((itemPrev is StubFolderMetaData) && isLastPathElem)
                    {
                        ItemHelpers.FolderOps_UnwrapStubFolder(folder, (StubFolderMetaData)itemPrev);
                    }
                    else if (IsDeleteMetaDataKind(itemPrev))
                    { // former item was a DELETE...

                        //System.Diagnostics.Debugger.Launch();

                        // ...and new one then _resurrects_ the (_actually_ deleted) item:
                        bool isItemPlaced = (
                            ChangeTypeAnalyzer.IsAddOperation(change, updatingForwardInTime) ||
                            ChangeTypeAnalyzer.IsRenameOperation(change) ||
                            ChangeTypeAnalyzer.IsMergeOperation(change));
                        if (isItemPlaced)
                        {
                          if (!propertyChange)
                          {
                              item = sourceControlProvider.GetItems(itemFetchRevision, itemPath, Recursion.None);
                              Folder_ReplaceItem(folder, itemPrev, item);
                          }
                        }
                        // [section below was a temporary patch which should not be needed any more now that our processing is much better]
#if false
                        // ...or _renames_ the (pseudo-deleted) item!
                        // (OBEY VERY SPECIAL CASE: _similar-name_ rename (EXISTING ITEM LOOKUP SUCCESSFUL ABOVE!!), i.e. filename-case-only change)
                        // UPDATE: I don't think that this is correct here,
                        // especially now that we DON'T get an "existing item" (case-incorrect) any more
                        // (since we have working case filtering).
                        // These Add/Update | Edit | Rename handlers
                        // simply ought to do
                        // straight clean isolated *fully incremental*
                        // per-item update fetch handling
                        // which focusses each
                        // on the *current* item update
                        // to grab from TFS into our filesystem hierarchy,
                        // for the individual *part* (c.f. add/delete!)
                        // of the *currently* executed TFS Change.
                        // The case where this impl broke down
                        // was Rename|Edit of an item with its *parent* folder renamed,
                        // where .GetItems() of that folder on !updatingForwardInTime
                        // failed (null) since the folder name at that revision
                        // was obviously different from itemPath.
                        // One could say "simply don't add null item",
                        // but I believe the impl itself to be not correct here;
                        // also, it kept adding (rather than replacing) duplicate folder items.
                        else if (ChangeTypeAnalyzer.IsRenameOperation(change))
                        {
                          // Such TFS-side renames need to be reflected
                          // as a SVN delete/add (achieve rename *with* history!) operation,
                          // thus definitely *append* an ADD op to the *existing* DELETE op.
                          // [Indeed, for different-name renames,
                          // upon "svn diff" requests
                          // SvnBridge does generate both delete and add diffs,
                          // whereas for similar-name renames it previously did not -> buggy!]
                          item = sourceControlProvider.GetItems(itemFetchRevision, itemPath, Recursion.None);
                          Folder_AddItem(folder, item, newlyAdded);
                        }
#endif
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
            bool needUpdateContainer = false;
            if (propertyChange == false)
            {
                needUpdateContainer = true;
            }
            else
            {
                if (additionForPropertyChangeOnly.ContainsKey(item) == false)
                {
                    needUpdateContainer = true;
                }
            }
            if (needUpdateContainer)
            {
                additionForPropertyChangeOnly[item] = propertyChange;
            }
        }

        private bool IsAdditionForPropertyChangeOnly(ItemMetaData item)
        {
            bool bResult = false;

            // http://stackoverflow.com/questions/9382681/what-is-more-efficient-dictionary-trygetvalue-or-containskeyitem
            //if (additionForPropertyChangeOnly.ContainsKey(item))
            bool bDictEntry = false;
            if (additionForPropertyChangeOnly.TryGetValue(item, out bDictEntry))
            {
                bResult = bDictEntry;
            }

            return bResult;
        }

        private void ProcessDeletedItem(string remoteName, SourceItemChange change, bool updatingForwardInTime)
        {
            var itemFetchRevision = GetFetchRevision(
                change.Item.RemoteChangesetId,
                updatingForwardInTime);

            bool isChangeAlreadyCurrentInClientState = clientStateTracker.IsChangeAlreadyCurrentInClientState(
                ChangeType.Delete,
                remoteName,
                itemFetchRevision);
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
            for (int i = 0; i < pathElemsCount; ++i)
            {
                bool isLastPathElem = (i == pathElemsCount - 1);

                FilesysHelpers.PathAppendElem(ref itemPath, pathElems[i]);

                bool isFullyHandled = HandleDeleteItem(remoteName, change, itemPath, ref folder, isLastPathElem, updatingForwardInTime);
                if (isFullyHandled)
                    break;
            }
            if (pathElemsCount == 0)//we have to delete the checkout root itself
            {
                HandleDeleteItem(remoteName, change, itemPath, ref folder, true, updatingForwardInTime);
            }
        }

        private bool HandleDeleteItem(string remoteName, SourceItemChange change, string itemPath, ref FolderMetaData folder, bool isLastPathElem, bool updatingForwardInTime)
        {
            ItemMetaData itemPrev = folder.FindItem(itemPath);
            // Shortcut: valid item in our cache, and it's a delete already? We're done :)
            if (IsDeleteMetaDataKind(itemPrev))
                return true;

            var itemFetchRevision = GetFetchRevision(change.Item.RemoteChangesetId, updatingForwardInTime);

            ItemMetaData item = itemPrev;

            if (itemPrev == null)
            {
                if (isLastPathElem)
                {
                    item = ConstructDeletedItem(
                        (ItemType.File != change.Item.ItemType));

                    item.Name = remoteName;
                    item.ItemRevision = itemFetchRevision;
                }
                else
                {
                    item = sourceControlProvider.GetItemsWithoutProperties(itemFetchRevision, itemPath, Recursion.None);
                    if (item == null)
                    {
                        // FIXME: hmm, are we really supposed to actively Delete a non-isLastPathElem item
                        // rather than indicating a MissingItemMetaData!?
                        // After all the actual delete operation is expected to be carried out (possibly later) properly, too...
                        // Nope - while I think we *do* need to mark it as processed (via Missing),
                        // I have the expectation
                        // that a later Change of this final (last-elem) item
                        // will transform it into its permanent type.
#if false
                        item = new DeleteFolderMetaData();
                        item.Name = itemPath;
                        item.ItemRevision = itemFetchRevision;
#else
                        item = new MissingItemMetaData(itemPath, itemFetchRevision, false);
#endif
                    }
                    else
                    {
                        // This item type is NOT a final one (isLastPathElem == true),
                        // only a temporarily necessary base (container) management helper.
                        item = ProvideHelperFolderWhileItsChangeStatusNonFinal((FolderMetaData)item);
                    }
                }
                Folder_AddItem(folder, item, false);
            }
            else if (isLastPathElem)
            {
                bool isItemNewlyAddedWithinDiffRevisionRange = (itemPrev.NewlyAdded);
                bool haveExistingRecordedItemChangeAvailToDiscard = (isItemNewlyAddedWithinDiffRevisionRange);
                bool needIndicateRealDelete = (!haveExistingRecordedItemChangeAvailToDiscard);
                if (needIndicateRealDelete)
                {
                    item = ConstructDeletedItem(
                        (ItemType.File != change.Item.ItemType));

                    item.Name = remoteName;
                    item.ItemRevision = itemFetchRevision;
                    Folder_ReplaceItem(folder, itemPrev, item);
                }
                else if (itemPrev is StubFolderMetaData)
                {
                    DeleteFolderMetaData itemDeleteFolder = new DeleteFolderMetaData();
                    itemDeleteFolder.Name = itemPrev.Name;
                    itemDeleteFolder.ItemRevision = itemFetchRevision;
                    Folder_ReplaceItem(folder, itemPrev, itemDeleteFolder);
                }
                else if (IsAdditionForPropertyChangeOnly(itemPrev))
                {
                    ItemMetaData itemDelete = ConstructDeletedItem(
                        itemPrev is FolderMetaData);
                    itemDelete.Name = itemPrev.Name;
                    itemDelete.ItemRevision = itemFetchRevision;
                    Folder_ReplaceItem(folder, itemPrev, itemDelete);
                }
                else if (itemPrev is MissingItemMetaData && ((MissingItemMetaData)itemPrev).Edit == true)
                {
                    ItemMetaData itemDelete = new DeleteMetaData();
                    itemDelete.Name = itemPrev.Name;
                    itemDelete.ItemRevision = itemFetchRevision;
                    Folder_ReplaceItem(folder, itemPrev, itemDelete);
                }
                else
                {
                    Folder_RemoveItem(folder, itemPrev);
                }
            }
            folder = (item as FolderMetaData) ?? folder;
            return false;
        }

        private static int GetFetchRevision(
            int changesetId,
            bool updatingForwardInTime)
        {
            return TfsLibraryHelpers.GetFetchRevision(
                changesetId,
                updatingForwardInTime);
        }

        private static string[] GetSubPathElems_PossiblyBelowSpecificRoot(string root, string path)
        {
            string[] pathElems;

            string pathSub = GetSubPath_PossiblyBelowSpecificRoot(root, path);
            bool isValidSubPath = !(string.IsNullOrEmpty(pathSub));
            pathElems = isValidSubPath ? FilesysHelpers.GetPathElems(pathSub) : new string[] {};

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
            bool needDoRootRestrictionCheck = (isRootSpecified);
            if (needDoRootRestrictionCheck)
            {
                bool isBelowRoot = (path.StartsWith(root));
                if (isBelowRoot)
                {
                    subPath = path.Substring(root.Length + 1);
                }
                else
                {
                    subPath = "";
                }
            }
            else
            {
                subPath = path;
            }

            return subPath;
        }

        private static ItemMetaData ConstructDeletedItem(
            bool isFolder)
        {
            return isFolder ?
                (ItemMetaData)new DeleteFolderMetaData() :
                new DeleteMetaData();
        }

        private bool RemoveMissingItem(string name, FolderMetaData folder)
        {
            foreach (ItemMetaData item in folder.Items)
            {
                if (item.Name.Equals(name) && item is MissingItemMetaData)
                {
                    ItemMetaData itemPrev = item;
                    Folder_RemoveItem(folder, itemPrev);
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

        /// <summary>
        /// Almost comment-only helper.
        /// Returns a temporary, helper folder
        /// for interim base / container folder purposes.
        /// </summary>
        /// <remarks>
        /// This helper wraps the temporary item information as gotten from SCM
        /// as long as during processing
        /// there was no actual Change encountered yet
        /// for this-hierarchy item proper
        /// (IOW once there *will be* an actual SCM Change for this item processed
        /// this stub folder definitely needs to be unwrapped!!).
        /// The item passed here is NOT a final one (isLastPathElem == true),
        /// IOW only a temporarily necessary base (container) management helper
        /// to contain child items,
        /// thus it certainly shouldn't
        /// directly indicate real Changes (Add/Delete) yet
        /// within the currently processed Changeset,
        /// which it would
        /// if we now queued the live item type directly
        /// rather than providing a StubFolderMetaData-typed indirection for it...
        /// </remarks>
        private static FolderMetaData ProvideHelperFolderWhileItsChangeStatusNonFinal(FolderMetaData folderFetched)
        {
            return ItemHelpers.WrapFolderAsStubFolder(folderFetched);
        }

        // Folder operation encapsulation helpers:
        // may be needed to be able to centrally apply
        // certain precisely controlled housekeeping activities.

        private static void Folder_ReplaceItem(FolderMetaData folder, ItemMetaData itemVictim, ItemMetaData itemWinner)
        {
            itemWinner.NewlyAdded = itemVictim.NewlyAdded; // carry over important state
            ItemHelpers.FolderOps_ReplaceItem(folder, itemVictim, itemWinner);
        }

        private static void Folder_AddItem(FolderMetaData folder, ItemMetaData item, bool newlyAdded)
        {
            item.NewlyAdded = newlyAdded;
            ItemHelpers.FolderOps_AddItem(folder, item);
        }

        private static void Folder_RemoveItem(FolderMetaData folder, ItemMetaData itemVictim)
        {
            ItemHelpers.FolderOps_RemoveItem(folder, itemVictim);
        }
    }
}
