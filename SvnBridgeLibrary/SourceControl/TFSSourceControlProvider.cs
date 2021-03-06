using System.Net.Sockets; // SocketException
using CodePlex.TfsLibrary;
using SvnBridge.Net; // RequestCache

namespace SvnBridge.SourceControl
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics; // Debug.WriteLine()
    using System.IO; // FileNotFoundException only
    using System.Net; // ICredentials
    using System.Text.RegularExpressions; // Regex
    using CodePlex.TfsLibrary.ObjectModel;
    using CodePlex.TfsLibrary.RepositoryWebSvc;
    using Dto;
    using Exceptions; // FolderAlreadyExistsException
    using Infrastructure;
    using Interfaces; // IMetaDataRepository
    using Protocol; // UpdateReportData only (layer violation?)
    using Proxies; // TracingInterceptor, RetryOnExceptionsInterceptor
    using Utility; // DebugRandomActivator, Helper.ArrayCombine()
    using SvnBridge.Cache;
    using System.Web.Services.Protocols; // SoapException
    using System.Linq; // System.Array extensions

    /// <summary>
    /// I don't quite know yet where to place these things,
    /// but I do know that they shouldn't be needlessly restricted to
    /// use within TFSSourceControlProvider only...
    /// These file system path calculation helpers
    /// are purely about path handling
    /// i.e.: NOT ItemMetaData-based.
    /// </summary>
    public sealed class FilesysHelpers
    {
        private const string repo_separator_s = "/";
        private const char repo_separator_c = '/';

        public static void StripRootSlash(ref string path)
        {
            if (path.StartsWith(repo_separator_s))
                path = path.Substring(1);
        }

        public static string StripBasePath(string name, string basePath)
        {
            StripRootSlash(ref name);

            StripRootSlash(ref basePath);

            basePath = basePath + "/";

            if (name.StartsWith(basePath))
            {
                name = name.Substring(basePath.Length);
                StripRootSlash(ref name);
            }
            return name;
        }

        // A helper not unlike UNIX "dirname"
        // (albeit for filename-only arguments it will return "" rather than ".").
        public static string GetFolderPathPart(string path)
        {
            string folderName = "";
            var idxLastSep = path.LastIndexOf(repo_separator_c);
            bool haveLastSep = (-1 != idxLastSep);
            if (haveLastSep)
            {
                folderName = path.Substring(0, idxLastSep);
            }
            return folderName;
        }

        public static string StripPrefix(string prefix, string full)
        {
            // Hmm, there's an off-by-1 API mismatch
            // between StripPrefix() and GetSubPath().
            // Would be nice to possibly have that cleaned up
            // to not need StripPrefix()...
            string res = (full.Length > prefix.Length) ? full.Substring(prefix.Length) : "";
            return res;
        }

        public static string GetSubPath(string pathRoot, string pathFull)
        {
            // Hmm, perhaps have an additional check
            // against pathFull being only a part of pathRoot?
            // (see StripPrefix() above)
            return pathFull.Substring(pathRoot.Length + 1);
        }

        /// <summary>
        /// Given input such as
        /// root: Proj
        /// sub: Proj/sub/folder/file.txt
        /// this method is expected
        /// to be returning this string
        /// (which represents all intermediate folder element parts):
        /// sub/folder
        /// </summary>
        public static string GetIntermediatePathElems(string root, string sub)
        {
            string folderParentOfSub_absolute = sub.Substring(0, sub.LastIndexOf('/'));
            var idxSeparator = folderParentOfSub_absolute.IndexOf('/'); // XXX this does not really handle the (potentially multi-elem) "root" param
            string folderParentOfSub_relative = (-1 != idxSeparator) ? folderParentOfSub_absolute.Substring(idxSeparator + 1) : "";
            string folderPathElemsIntermediate = folderParentOfSub_relative;
            return folderPathElemsIntermediate;
        }

        /// <summary>
        /// Calculates the common parent folder path of two items
        /// (returning a path which is able to contain both path items),
        /// irrespective of whether that path then happens to already exist or not.
        /// "parent" is not to be confused with "ancestor" (we're not talking history here...).
        /// </summary>
        /// <param name="path1">Location one</param>
        /// <param name="path2">Location two</param>
        public static string CalculateCommonParentFolderPath(string path1, string path2)
        {
            string pathCommonParent = "";

            // First, handle special cases:
            // [ermm, is null input even supposed to be valid?
            // After all one could argue
            // that it should be caught/handled by the user of this API
            // since it knows best what exactly to do in such a case,
            // and one could also argue
            // that generally unconditionally placing a "null" item
            // within the root of the other valid item is "strange".
            // Thus, keep a watch on such uses!]
            if ((null == path1) || (null == path2))
            {
                Helper.DebugUsefulBreakpointLocation();
            }
            if (null == path1)
            {
                pathCommonParent = path2;
            }
            else
            if (null == path2)
            {
                pathCommonParent = path1;
            }
            else
            {
                pathCommonParent = NeedCalculateCommonParentFolderPath(path1, path2);
            }

            return pathCommonParent;
        }

        private static string NeedCalculateCommonParentFolderPath(string path1, string path2)
        {
            string pathCommonParent = "";

            string[] path1Elems = GetPathElems(path1);
            string[] path2Elems = GetPathElems(path2);
            int pathElemsLen = Math.Min(path1Elems.Length, path2Elems.Length);

            // Performance opt: first do a query-only comparison of elems,
            // *then* laboriously assemble final result string.
            int idxMatch = 0;
            for (idxMatch = 0; idxMatch < pathElemsLen; ++idxMatch)
            {
                bool isStillMatching = (path1Elems[idxMatch] == path2Elems[idxMatch]);
                if (!isStillMatching)
                {
                    break;
                }
            }

            bool foundSomeCommonPathElems = (0 < idxMatch);
            if (foundSomeCommonPathElems)
            {
                pathCommonParent = string.Join(repo_separator_s, path1Elems, 0, idxMatch);
            }

            // Not sure whether we need this path tweak
            // (this would spoil the otherwise very clean symmetric handling
            // "determine common data *only* given two items"
            // by *manually* adding a slash,
            // so quite likely this should only be applied externally if needed):
            //if (!pathCommonParent.StartsWith("/"))
            //    pathCommonParent = "/" + pathCommonParent;

            return pathCommonParent;
        }

        public static string[] GetPathElems(string path)
        {
            // We might need to apply StringSplitOptions.RemoveEmptyEntries here
            // (possibly indicated by a bool parameter of our method).
            return path.Split(repo_separator_c);
        }

        /// <summary>
        /// Appends a path element to a path.
        /// </summary>
        /// While FxCop emits DoNotPassTypesByReference warning,
        /// I decided to keep it that way
        /// rather than returning the extended path string,
        /// since otherwise there may easily be confusion
        /// between both non-reference same-type (string) method parameters.
        public static void PathAppendElem(ref string path, string pathElem)
        {
            if (path != "" && !path.EndsWith(repo_separator_s))
                path += repo_separator_s + pathElem;
            else
                path += pathElem;
        }

        /// <summary>
        /// Appends a path element to a path.
        /// I decided to add this alternative
        /// to the ref-based PathAppendElem()
        /// since I'm now less sure
        /// whether a ref-based method is really useful
        /// (since that thwarts proper RAII-style use).
        /// </summary>
        public static string PathJoin(string path, string pathElem)
        {
            bool isSomeArgNotSet = (string.IsNullOrEmpty(path) || (string.IsNullOrEmpty(pathElem)));
            bool isBothArgsValid = !(isSomeArgNotSet);
            bool needSeparator = (isBothArgsValid && !path.EndsWith(repo_separator_s));
            string append = needSeparator ?
                repo_separator_s + pathElem :
                pathElem;

            return path + append;
        }

        /// <summary>
        /// Helper to abstract/hide away the *internal* decision
        /// on whether names of filesystem items ought to be case-mangled
        /// (to repair TFS-side case-insensitive / case sensitivity handling issues).
        /// It seems we have issues with caching file information wrongly
        /// due to ToLower()ing filenames when in fact there are cases of similar-name
        /// (changed case) renaming for certain changesets.
        /// Thus we decide to NOT do ToLower() in case of case-sensitive operation mode...
        /// </summary>
        /// <param name="nameOrig">Original (likely not-yet-mangled) name</param>
        public static string GetCaseMangledName(string nameOrig)
        {
            // I don't think it's useful to have this bool
            // be made a local class member -
            // after all this functionality
            // should always directly follow the current Configuration-side setting.
            bool wantCaseSensitiveMatch = Configuration.SCMWantCaseSensitiveItemMatch; // CS0429 warning workaround
            return wantCaseSensitiveMatch ? nameOrig : nameOrig.ToLower();
        }

        /// <summary>
        /// Tweaks a path from full (prefix-prepended) syntax
        /// to sub path syntax.
        /// </summary>
        /// <param name="prefixPath">The prefix to be stripped</param>
        /// <param name="fullPath">Full (prefix-prepended) path string</param>
        public static string PathPrefix_Checked_Strip(string prefixPath, string fullPath)
        {
            if (!fullPath.StartsWith(prefixPath))
            {
                throw new ArgumentException("Logic error: expected clean layer transition *from* full path *to* prefix-stripped path!");
            }
            return StripPrefix(prefixPath, fullPath);
        }

        /// <summary>
        /// Tweaks a path from sub path syntax
        /// to full (prefix-prepended) syntax.
        /// </summary>
        /// <param name="prefixPath">The prefix to be prepended</param>
        /// <param name="subPath">Prefix-less sub path string</param>
        public static string PathPrefix_Checked_Prepend(string prefixPath, string subPath)
        {
            if (subPath.StartsWith(prefixPath))
            {
                throw new ArgumentException("Logic error: expected clean layer transition *from* sub path *to* prefix-enhanced full path!");
            }
            return Helper.CombinePath(prefixPath, subPath);
        }
    }

    /// <summary>
    /// Delegate definition for tweaking one local level within a folder hierarchy.
    /// Note that we're expressly offering an isLastPathElem bool to indicate having reached the final element,
    /// since this interface should minimally be concerned about manipulation within one path element level only
    /// and thus NOT offer any whole-path strings (which could be compared to have the bool determined internally).
    /// If any whole-path information happens to actually be needed,
    /// then it should be supplied by outside yet scope-visible variables
    /// which are to be directly accessed within an open-coded delegate part.
    /// </summary>
    /// <param name="folder">Item indicating the current base folder level to be modified</param>
    /// <param name="itemPath">Path to the item that is supposed to be treated</param>
    /// <param name="isLastPathElem">Indicates that the current item will be the final level</param>
    /// <param name="requestFinish">May be set to true by user to indicate that we ought to bail out of folder iteration loop.
    /// Decided to have it as ref rather than out
    /// since user side of an out param would (usually...)
    /// need to have false assigned initially
    /// with those places which then do want to have it bail out
    /// then needing to do an *additional* true assignment...
    /// </param>
    /// <returns>The item that is to become the base folder of the next iteration</returns>
    public delegate ItemMetaData FolderTweaker(FolderMetaData folder, string itemPath, bool isLastPathElem, ref bool requestFinish);

    public sealed class ItemHelpers
    {
        /// <summary>
        /// Assigns or updates an SVN property name/value pair to a filesystem item.
        /// </summary>
        /// <param name="item">Item to have its properties updated</param>
        /// <param name="property">Property (name/value pair) to be updated</param>
        public static void UpdateItemProperty(ItemMetaData item, Property property)
        {
            item.Properties[property.Name] = property.Value;
        }

        public static void UpdateItemProperties(ItemMetaData item, ItemProperties itemProperties)
        {
            foreach (Property property in itemProperties.Properties)
            {
                UpdateItemProperty(item, property);
            }
        }

        public static FolderMetaData WrapFolderAsStubFolder(FolderMetaData folder)
        {
            StubFolderMetaData stubFolder = new StubFolderMetaData();
            stubFolder.RealFolder = folder;
            stubFolder.Name = folder.Name;
            stubFolder.ItemRevision = folder.ItemRevision;
            stubFolder.PropertyRevision = folder.PropertyRevision;
            stubFolder.LastModifiedDate = folder.LastModifiedDate;
            stubFolder.Author = folder.Author;
            return stubFolder;
        }

        public static void FolderOps_AddItem(FolderMetaData folder, ItemMetaData item)
        {
            folder.Items.Add(item);
        }

        public static void FolderOps_RemoveItem(FolderMetaData folder, ItemMetaData itemVictim)
        {
            folder.Items.Remove(itemVictim);
        }

        public static ItemMetaData FolderOps_ReplaceItem(FolderMetaData folder, ItemMetaData itemVictim, ItemMetaData itemWinner)
        {
            FolderOps_RemoveItem(folder, itemVictim);
            FolderOps_AddItem(folder, itemWinner);
            return itemWinner;
        }

        public static ItemMetaData FolderOps_UnwrapStubFolder(FolderMetaData folder, StubFolderMetaData itemStubFolder)
        {
            // Note that the reason that we don't seem to need to reassign
            // any existing .Items from stubFolder to RealFolder here
            // appears to be that StubFolderMetaData class has an override
            // which properly ensures
            // that .Items operations always directly get done on the real folder anyway.
            return ItemHelpers.FolderOps_ReplaceItem(folder, itemStubFolder, itemStubFolder.RealFolder);
        }

        public static ItemMetaData PathIterator(FolderMetaData root, string pathRoot, string pathSub, FolderTweaker folderTweaker)
        {
            ItemMetaData itemNext = null;

            FolderMetaData folder = root;
            string itemPath = pathRoot;
            string[] pathElems = FilesysHelpers.GetPathElems(pathSub);

            int pathElemsCount = pathElems.Length;
            for (int i = 0; i < pathElemsCount; ++i)
            {
                bool isLastPathElem = (i == pathElemsCount - 1);

                FilesysHelpers.PathAppendElem(ref itemPath, pathElems[i]);

                bool requestFinish = false;

                // Note that in case access to local context members is required
                // from within the delegate code,
                // this is expected to be dealt with by open-coding the delegate call at the callsite
                // and thereby ensuring that those members can be accessed there.
                itemNext = folderTweaker(folder, itemPath, isLastPathElem, ref requestFinish);

                if (requestFinish)
                {
                    break;
                }

                if (isLastPathElem == false) // this conditional merely required to prevent cast of non-FolderMetaData-type objects below :(
                {
                    folder = (FolderMetaData)itemNext;
                }
            }

            return itemNext;
        }
    }

    public sealed class SCMHelpers
    {
        // Marker string to be used for cases where the author of an SCM item
        // can not be readily determined.
        // FIXME: is the "unknown" string a *fixed*, "widely known", "magic" value
        // by which SVN publicly indicates "unknown author",
        // or could this be replaced by a *much* more suitable
        // (think otherwise totally anonymous, context-free output logging)
        // "UnknownAuthor" string?
        // SVN source does not indicate any use of such a specific user string
        // (except for already using the identical "unknown" word for the node kind),
        // thus it would be possible.
        private const string unknownAuthorMarker = "unknown";

        public static string UnknownAuthorMarker
        {
            get { return unknownAuthorMarker; }
        }

        /// <remarks>
        /// Forcing user to supply a sourceItem-related author ID here
        /// rather than choosing (re-)assignment after the fact, since:
        /// - author is the only "specially missing" attribute
        /// during this otherwise 1:1 conversion
        /// - construction-time assignment is "faster" / "much more elegant"
        /// </remarks>
        public static ItemMetaData ConvertSourceItem(SourceItem sourceItem, string rootPath, string author)
        {
            ItemMetaData item;
            if (sourceItem.ItemType == ItemType.Folder)
            {
                item = new FolderMetaData();
            }
            else
            {
                item = new ItemMetaData();
            }

            item.Id = sourceItem.ItemId;
            item.Name = FilesysHelpers.PathPrefix_Checked_Strip(rootPath, sourceItem.RemoteName);

            item.Author = author;
            item.LastModifiedDate = sourceItem.RemoteDate;
            item.ItemRevision = sourceItem.RemoteChangesetId;
            item.DownloadUrl = sourceItem.DownloadUrl;
            return item;
        }
    }

    // TODO: this class (or parts of it, perhaps via a base class)
    // perhaps should be used outside of TFSSourceControlProvider, too
    // (UpdateDiffEngine.cs).

    /// <summary>
    /// This class will manage a filesystem item space (files, folders).
    /// Given newly inserted items, it will arrange them within the pre-existing content
    /// and/or create helper items (parent folders, etc.) as necessary.
    /// Also, it will maintain a root item member which always points
    /// at the topmost root item of the filesystem space tree.
    /// Additionally, all folder-type items will be added to a map,
    /// for efficient lookup of items at specific locations.
    ///
    /// Exhaustive list of example situations that this class will have to handle correctly
    /// (revisit proper handling from time to time, or rather TODO:
    /// write sufficiently abstract yet precise unit tests for these cases):
    /// - single non-folder item only: rootItem needs to point at that item
    /// - insert item at one location, then item at another: rootItem needs to be a common parent folder
    ///   of both locations, with intermediate folder items being StubFolderMetaData
    /// - insert folder at a location which previously had to be created as a StubFolderMetaData helper:
    ///   properly replace by real folder item:
    ///   - possibly existing parent folder needs to contain this real folder now
    ///   - new real folder needs to be made to contain all items that the former stub folder had
    ///     (OTOH since the former stub-real-folder originates from the same provider query than
    ///     the current insertion candidate, "inserting" the new item translates to a simple
    ///     transform-stub-to-real operation instead)
    ///   - add new folder to folder map (i.e., need to ensure update of existing map entry)
    /// - insert two items at one folder location: order needs to remain in insertion order
    /// </summary>
    public class FolderMap
    {
        public Dictionary<string, FolderMetaData> folders = new Dictionary<string, FolderMetaData>();
        /// <summary>
        /// The item which currently sits at the root of the filesystem space tree.
        /// May be either a folder item or also a non-folder item in case of the filesystem space
        /// simply having item-only content.
        /// Please note that rootItem should only be populated once we *do* have a valid item,
        /// i.e. it should *never* be a stub item type (external users implement item existence checks by querying it!).
        /// </summary>
        private ItemMetaData rootItem;
        private readonly TFSSourceControlProvider sourceControlProvider;
        private readonly int _targetVersion;
        public FolderMap(TFSSourceControlProvider sourceControlProvider, int version)
        {
            this.sourceControlProvider = sourceControlProvider;
            this._targetVersion = version;
        }

        public ItemMetaData QueryRootItem()
        {
            return rootItem;
        }

        /// <summary>
        /// Inserts an item into our filesystem hierarchy space,
        /// fully hooking up items via possibly temporary intermediate folders.
        /// Please note that users usually pass correctly ordered lists of items
        /// into this function, thus usually we will not encounter any missing intermediate
        /// directories, thus there won't be too many extra TFS queries
        /// which would be the case when Insert()ing the actual item at a later time.
        /// And even if there are, these will be marked as temporary stub folders,
        /// to be unwrapped into real folders once these get Insert()ed for real.
        /// </summary>
        /// <param name="newItem">The item to be inserted</param>
        public void Insert(ItemMetaData newItem)
        {
            // We'll start operations with determining a path that's
            // the common parent of root item vs. new item,
            // since that one will become the new root item,
            // and it's important to hook up items starting from the root direction
            // since existing parent folders need to have their children added.
            // So, first figure out the new value of the root item.

            //if (newItem.Name.ToLowerInvariant().Contains("somefile.h"))
            //{
            //    Helper.DebugUsefulBreakpointLocation();
            //}

            // === STEP 1: DETERMINE LOCATION OF NEW ROOT ITEM ===

            ItemMetaData oldRootItem = QueryRootItem();
            string pathToOldRootItem = (null != oldRootItem) ? oldRootItem.Name : null;

            string pathToNewRootItem;

            bool newItemIsFolder = (ItemType.Folder == newItem.ItemType);
            if (null == oldRootItem)
            {
                pathToNewRootItem = newItem.Name; // either folder *or* item!

                bool newItemIsFile = !newItemIsFolder;
                if (newItemIsFile)
                {
                    // HACK: previous code added a container folder
                    // in case of a file-only root item
                    // (but it carefully did NOT register that folder item as the root item then!)
                    // So, keep doing the (almost) same thing...
                    FolderMetaData baseFolderForFileRootItem = new FolderMetaData();
                    baseFolderForFileRootItem.Name = FilesysHelpers.GetFolderPathPart(newItem.Name);
                    ItemHelpers.FolderOps_AddItem(baseFolderForFileRootItem, newItem);
                    // and register it as a *stub* folder!
                    // (previous code - likely erroneously - used a real folder)
                    InsertFolderInMap(ItemHelpers.WrapFolderAsStubFolder(baseFolderForFileRootItem));
                }
            }
            else
            {
                string pathToNewFolder = newItemIsFolder ? newItem.Name : FilesysHelpers.GetFolderPathPart(newItem.Name);
                pathToNewRootItem = FilesysHelpers.CalculateCommonParentFolderPath(pathToOldRootItem, pathToNewFolder);
            }

            // === STEP 2: UPDATE ROOT ITEM IF NEEDED ===

            ItemMetaData newRootItem;

            bool rootItemNeedsUpdate = (pathToNewRootItem != pathToOldRootItem);
            if (rootItemNeedsUpdate)
            {
                bool newItemIsNewRootItem = (newItem.Name == pathToNewRootItem);
                newRootItem = (newItemIsNewRootItem) ? newItem : QueryItem(pathToNewRootItem);
                if (ItemType.Folder == newRootItem.ItemType)
                    InsertFolderInMap((FolderMetaData)newRootItem);
                rootItem = newRootItem;
            }
            else
                newRootItem = oldRootItem;

            // AT THIS POINT WE EXPECT TO HAVE DETERMINED A VALID NEW ROOT ITEM!

            FolderMetaData newRootFolder = newRootItem as FolderMetaData;
            bool newRootItemIsFolder = (null != newRootFolder);
            bool needRelinkRootFolderSubContent = (newRootItemIsFolder);

            if (needRelinkRootFolderSubContent)
            {
                // === STEP 3: IF NEEDED, COMPLETE PATH FROM NEW ROOT ITEM TO OLD ROOT ITEM ===

                bool rootItemLocationMoved = ((null != oldRootItem) && (newRootFolder != oldRootItem));
                if (rootItemLocationMoved)
                {
                    CompletePath(newRootFolder, oldRootItem);
                }

                // === STEP 4: IF NEEDED, COMPLETE PATH FROM NEW ROOT ITEM TO NEW ITEM ===

                bool itemDiffersFromRootItem = (newRootFolder != newItem);
                if (itemDiffersFromRootItem)
                {
                    CompletePath(newRootFolder, newItem);
                }
            }
        }

        private ItemMetaData QueryItem(string itemName)
        {
            return sourceControlProvider.GetItemsWithoutProperties(_targetVersion, itemName, Recursion.None);
        }

        /// <summary>
        /// Completes the "path to an item",
        /// by adding/linking as many interim folders as needed, up to the actual final item.
        /// </summary>
        /// <param name="folderFrom">Start folder</param>
        /// <param name="itemTo">The item to provide a complete path to</param>
        private void CompletePath(FolderMetaData folderFrom, ItemMetaData itemTo)
        {
            string pathRoot = folderFrom.Name;
            string pathSub = FilesysHelpers.GetSubPath(pathRoot, itemTo.Name);
            // NOTE: we'll do operations relatively openly in one big loop rather than sub methods
            // since handling always needs to be done from the view of the parent item
            // (we may need to update list linking), as provided by the previous loop iteration.
            ItemHelpers.PathIterator(folderFrom, pathRoot, pathSub,
                delegate(FolderMetaData folder, string itemPath, bool isLastPathElem, ref bool requestFinish) {
                    // Detect our possibly pre-existing record of this item within the changeset version range
                    // that we're in the process of analyzing/collecting...
                    // This existing item may possibly be a placeholder (stub folder).
                    ItemMetaData item = folder.FindItem(itemPath);
                    bool doReplaceByNewItem = (null == item);
                    if (!doReplaceByNewItem) // further checks...
                    {
                        if (isLastPathElem) // only if final item...
                        {
                            // Our new item gets actively inserted, thus it does need to properly end up as the replacement:
                            doReplaceByNewItem = true;
                        }
                    }
                    // So... do we actively want to grab a new item?
                    if (doReplaceByNewItem)
                    {
                        // First remove this prior item...
                        if (item != null)
                        {
                            ItemHelpers.FolderOps_RemoveItem(folder, item);
                        }

                        item = (!isLastPathElem) ?
                               ItemHelpers.WrapFolderAsStubFolder(ProvideFolderTypeItemForPath(itemPath))
                             :
                               itemTo
                             ;

                        if (null == item)
                        {
                            bool edit = false; // FIXME correct value!?
                            Helper.DebugUsefulBreakpointLocation();
                            item = new MissingItemMetaData(itemPath, _targetVersion, edit);
                        }
                        ItemHelpers.FolderOps_AddItem(folder, item);

                        if (ItemType.Folder == item.ItemType)
                        {
                            InsertFolderInMap((FolderMetaData)item);
                        }
                    }
                    else if (isLastPathElem)
                    {
                        StubFolderMetaData itemFolderStub = item as StubFolderMetaData;
                        bool isItemStubFolder = (null != itemFolderStub);
                        if (isItemStubFolder)
                        {
                            item = ItemHelpers.FolderOps_UnwrapStubFolder(folder, itemFolderStub);
                            InsertFolderInMap((FolderMetaData)item); // update map to contain new folder item
                        }
                    }

                    return item;
                });
        }

        private void InsertFolderInMap(FolderMetaData folder)
        {
            string folderNameMangled = FilesysHelpers.GetCaseMangledName(folder.Name);
            folders[folderNameMangled] = folder;
        }

        /// <remarks>
        /// Future rework idea:
        /// Rather than storing folders in a folders map,
        /// one could have metadata-only hierarchy lookup.
        /// For case insensitive lookup, we currently insensitive-compare the *whole*
        /// multi-directory string, whereas it would be more precise if we managed to
        /// do lookup on each hierarchy level and then on each level chose insensitive lookup
        /// *only* in case sensitive lookup failed.
        /// Or, IOW, currently the following case fails:
        /// Location1: /PROJECT/myFolder/
        /// Location2: /project/MyFolder/
        /// Query: /project/myFolder/
        /// This would return Location1, despite Location2 arguably being more relevant
        /// (the root entry is precise, and only the sub dir is different).
        /// However, please note that we encountered veritable, true cases of TFS SCM history
        /// indicating WRONGLY-cased, non-existent items (even within single commits!)
        /// as a sibling of the actually CORRECTLY-cased, non-referenced item, *within one commit*
        /// (i.e., one could very strongly argue that such inconsistent commit data is TERRIBLY B0RKEN,
        /// irrespective of whether TFS does or does not have contracted
        /// the case insensitive disease in general).
        /// UPDATE: such damage is now hopefully being sanitized properly,
        /// right after having received b0rken data from our TFS-side interfaces.
        /// </remarks>
        public FolderMetaData TryGetFolder(string key)
        {
            FolderMetaData folder;
            // http://stackoverflow.com/questions/9382681/what-is-more-efficient-dictionary-trygetvalue-or-containskeyitem
            folders.TryGetValue(key, out folder);
            return folder;
        }

        /// <summary>
        /// *Guarantees* returning a (not necessarily having pre-existed) folder item.
        /// </summary>
        /// <param name="itemPath">Path to the folder to fetch</param>
        /// <returns>Folder item</returns>
        FolderMetaData ProvideFolderTypeItemForPath(string itemPath)
        {
            FolderMetaData item;

            FolderMetaData itemFolderExisting = GetExistingContainerFolderForPath(itemPath);
            bool haveItemFolderExisting = (null != itemFolderExisting);
            item = (haveItemFolderExisting) ?
                itemFolderExisting :
                QueryFolder(itemPath);

            return item;
        }

        private FolderMetaData QueryFolder(string itemPath)
        {
            FolderMetaData folder = null;

            ItemMetaData itemNew = QueryItem(itemPath);
            if (null != itemNew)
            {
                folder = itemNew as FolderMetaData;
            }

            return folder;
        }

        private FolderMetaData GetExistingContainerFolderForPath(string path)
        {
            FolderMetaData folder = TryGetFolder(path);
            if (null == folder)
            {
                // NOT FOUND?? (due to obeying a proper strict case sensitivity mode!?)
                // Try very special algo to detect likely candidate folder.

                // This problem has been observed with a Changeset
                // where a whopping 50 files were correctly named
                // yet 2 lone others (the elsewhere case-infamous resource.h sisters)
                // had *DIFFERENT* folder case.
                // Thus call this helper to (try to) locate the actually matching
                // *pre-registered* folder via a case-insensitive lookup.
                bool wantCaseSensitiveMatch = Configuration.SCMWantCaseSensitiveItemMatch; // workaround for CS0162 unreachable code
                bool acceptingCaseInsensitiveResults = !wantCaseSensitiveMatch;

                folder = (acceptingCaseInsensitiveResults) ?
                    FindMatchingExistingFolderCandidate_CaseInsensitive(folders, path) : null;
            }
            return folder;
        }

        /// <summary>
        /// Helper method for case-*insensitive* comparison of paths:
        /// manually iterate through the folder map
        /// and do an insensitive string compare to figure out the likely candidate folder.
        /// </summary>
        private static FolderMetaData FindMatchingExistingFolderCandidate_CaseInsensitive(Dictionary<string, FolderMetaData> dict, string folderName)
        {
            FolderMetaData folderResult = null;

            // To achieve a case-insensitive comparison, we
            // unfortunately need to manually *iterate* over all hash entries:
            foreach (var pair in dict)
            {
                //if (pair.Key.ToLowerInvariant().Contains("somefile.h"))
                //{
                //    Helper.DebugUsefulBreakpointLocation();
                //}

                // Make sure to also use the method that's commonly used
                // for such path comparison purposes.
                // And do explicitly call the *insensitive* method (i.e. not IsSamePath()),
                // independent of whether .wantCaseSensitiveMatch is set
                // (this is a desperate last-ditch attempt, thus we explicitly do want insensitive).
                if (ItemMetaData.IsSamePathCaseInsensitive(folderName, pair.Key))
                {
                    folderResult = pair.Value;
                    break;
                }
            }

            return folderResult;
        }
    }

    public class ItemQueryCollector
    {
        private readonly TFSSourceControlProvider sourceControlProvider;
        private readonly int version;

        public ItemQueryCollector(TFSSourceControlProvider sourceControlProvider, int version)
        {
            this.sourceControlProvider = sourceControlProvider;
            this.version = version;
        }

        public ItemMetaData process(ItemMetaData[] items, bool returnPropertyFiles)
        {
                FolderMap folderMap = new FolderMap(sourceControlProvider, version);
                Dictionary<string, ItemProperties> dictPropertiesOfItems = null;
                Dictionary<string, int> dictPropertiesRevisionOfItems = null;
                bool havePropertyData = false;
                WebDAVPropertyStorageAdaptor propsSerializer = new WebDAVPropertyStorageAdaptor(sourceControlProvider);

                //if (items.Length > 1)
                //{
                //    // DEBUG_SITE:
                //    System.Diagnostics.Debugger.Launch();
                //    Helper.DebugUsefulBreakpointLocation();
                //}
                foreach (ItemMetaData item in items)
                {
                    bool isPropertyFile = WebDAVPropertyStorageAdaptor.IsPropertyFileType(item.Name);
                    bool wantReadPropertyData = (isPropertyFile && !returnPropertyFiles);
                    if (wantReadPropertyData)
                    {
                        // Implements lazy init:
                        if (null == dictPropertiesOfItems)
                        {
                            dictPropertiesOfItems = new Dictionary<string, ItemProperties>(items.Length);
                            dictPropertiesRevisionOfItems = new Dictionary<string, int>(items.Length);
                            havePropertyData = true;
                        }
                        string itemPath = WebDAVPropertyStorageAdaptor.GetPathOfDataItemFromPathOfPropStorageItem(item.Name);
                        dictPropertiesRevisionOfItems[itemPath] = item.Revision;
                        dictPropertiesOfItems[itemPath] = propsSerializer.PropertiesRead(item);
                    }
                    bool wantQueueItem = ((!isPropertyFile && !WebDAVPropertyStorageAdaptor.IsPropertyFolderType(item.Name)) || returnPropertyFiles);
                    if (wantQueueItem)
                    {
                        folderMap.Insert(item);
                    }
                }
                if (havePropertyData)
                {
                    UpdatePropertiesOfItems(folderMap, dictPropertiesOfItems);
                    UpdatePropertiesRevisionOfItems(folderMap, dictPropertiesRevisionOfItems);
                }

                // Either (usually) a folder or sometimes even single-item:
                ItemMetaData root = folderMap.QueryRootItem();

                // Make sure to invoke our VerifyNoMissingItemMetaDataRemained() helper here.
                FolderMetaData folder = root as FolderMetaData;
                bool isValidFolderItem = (null != folder);
                if (isValidFolderItem)
                {
                    folder.VerifyNoMissingItemMetaDataRemained();
                }

                return root;
        }

        private static void UpdatePropertiesOfItems(FolderMap folderMap, IEnumerable<KeyValuePair<string, ItemProperties>> properties)
        {
            foreach (KeyValuePair<string, ItemProperties> pairItemProperties in properties)
            {
                string itemPath = pairItemProperties.Key;
                ItemMetaData item = FindTargetItemForItemProperties(folderMap, itemPath);
                if (item != null)
                {
                    ItemHelpers.UpdateItemProperties(item, pairItemProperties.Value);
                }
            }
        }

        private static ItemMetaData FindTargetItemForItemProperties(FolderMap folderMap, string itemPath)
        {
            ItemMetaData item = null;

            ItemMetaData itemRoot = folderMap.QueryRootItem();

            if (null != itemRoot)
            {
                // First, try case sensitive standard item lookup...
                FolderMetaData folderRoot = itemRoot as FolderMetaData;
                bool isRootItemOfFolderType = (null != folderRoot);
                if (isRootItemOfFolderType)
                {
                    item = folderRoot.FindItem(itemPath);
                }
                else
                {
                    if (itemRoot.IsSamePath(itemPath))
                        item = itemRoot;
                }

                // ...then, if not found, try old case insensitive lookup in map.
                if (null == item)
                {
                    item = FindTargetItemForItemProperties_LegacyCaseInsensitiveLookupIfAllowed(folderMap, itemPath);
                }
            }

            return item;
        }

        private static ItemMetaData FindTargetItemForItemProperties_LegacyCaseInsensitiveLookupIfAllowed(FolderMap folderMap, string itemPath)
        {
            ItemMetaData item = null;

            bool wantCaseSensitiveMatch = Configuration.SCMWantCaseSensitiveItemMatch; // CS0429 warning workaround
            if (!wantCaseSensitiveMatch)
            {
                item = FindTargetItemForItemProperties_LegacyCaseInsensitiveLookup(folderMap, itemPath);
            }

            return item;
        }

        private static ItemMetaData FindTargetItemForItemProperties_LegacyCaseInsensitiveLookup(FolderMap folderMap, string itemPath)
        {
            ItemMetaData item = null;

            string itemPathMangled = itemPath.ToLowerInvariant();
            FolderMetaData itemFolder = folderMap.TryGetFolder(itemPathMangled);
            if (null != itemFolder)
            {
                item = itemFolder;
            }
            else
            {
                string folderNameMangled = FilesysHelpers.GetFolderPathPart(itemPath)
                    .ToLowerInvariant();
                itemFolder = folderMap.TryGetFolder(folderNameMangled);
                if (null != itemFolder)
                {
                    item = itemFolder.FindItem(itemPath);
                }
            }

            return item;
        }

        private static void UpdateItemPropertiesRevision(ItemMetaData item, int revision)
        {
            item.PropertyRevision = revision;
        }

        private static void UpdatePropertiesRevisionOfItems(FolderMap folderMap, IEnumerable<KeyValuePair<string, int>> pairsPropertiesRevisionOfItems)
        {
            foreach (KeyValuePair<string, int> pairItemPropertiesRevision in pairsPropertiesRevisionOfItems)
            {
                string itemPath = pairItemPropertiesRevision.Key;

                string itemPathMangled = itemPath.ToLower();
                FolderMetaData itemFolder = folderMap.TryGetFolder(itemPathMangled);
                if (null != itemFolder)
                {
                    ItemMetaData item = itemFolder;
                    UpdateItemPropertiesRevision(item, pairItemPropertiesRevision.Value);
                }
                else
                {
                    string baseFolderNameMangled = FilesysHelpers.GetFolderPathPart(itemPath).ToLowerInvariant();

                    itemFolder = folderMap.TryGetFolder(baseFolderNameMangled);
                    if (null != itemFolder)
                    {
                        foreach (ItemMetaData item in itemFolder.Items)
                        {
                            if (item.Name == itemPath)
                            {
                                UpdateItemPropertiesRevision(item, pairItemPropertiesRevision.Value);
                                // Hmm... to break; or not to break;?
                                // Case (in)sensitivity might play a role here
                                // (or multiple same-name items in folder!?),
                                // but I don't think so after all...
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// This provider class (God Class :-{) is meant to offer an interface for *SVN-side* tasks
    /// (e.g. it's directly being used by all handlers of SVN/WebDAV HTTP request methods).
    /// While internally of course this particular implementation happens to consult TFS APIs,
    /// this is not supposed to be relevant at all
    /// for the outer interface of this class
    /// (which is expected to be pretty much SVN-conforming/-generic).
    /// Thereby one could almost say that either TFSSourceControlProvider name is a misnomer,
    /// or that it should be (or already have been?) wrapped by a provider class
    /// that offers SVN/WebDAV conforming APIs,
    /// and quite possibly should be based on an interface
    /// which is intended to provide SVN-conforming services.
    ///
    /// Side note:
    /// I guess "provider" (c.f. class naming) is:
    /// a specific (web) *service* type
    /// as provided by a specific *location*/*session*
    /// (represented by the server location / credentials pair).
    ///
    /// Side note #2 (testing / reliability):
    /// Testing of SvnBridge reliability can be done
    /// by means of e.g. one of
    /// unit tests, FxCop, git-svn,
    /// command line svn (e.g. svn --diff --summarize),
    /// WebDAV (Cadaver, Konqueror webdav://),
    /// special Windows behaviour (TortoiseSVN)
    ///
    /// General FIXME comment:
    /// when going towards a clean class for SVN-only interface purposes,
    /// it would be very useful to guard all its interfaces with various checks that ensure
    /// that we never accidentally leak non-SVN specifics to the outside:
    /// - don't return any property storage item locations (i.e., internal implementation knowledge)
    /// - don't return any path strings with TFS Team Project syntax
    /// </summary>
    [Interceptor(typeof(TracingInterceptor))]
    [Interceptor(typeof(RetryOnExceptionsInterceptor<SocketException>))]
    public class TFSSourceControlProvider : MarshalByRefObject
    {
        private static readonly Regex s_associatedWorkItems = new Regex(@"(?:(?:(?:fixe?|close|resolve)(?:s|d)?)|(?:Work ?Items?))(?: |:|: )(#?\d+(?:, ?#?\d+)*)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline);
        private const char c_workItemChar = '#';

        private readonly ITFSSourceControlService sourceControlService;
        private readonly string serverUrl;
        private readonly ICredentials credentials;
        private readonly string rootPath;
        private readonly int maxLengthFromRootPath;
        private readonly IWorkItemModifier workItemModifier;
        private readonly DefaultLogger logger;
        private readonly WebCache cache;
        private readonly IMetaDataRepository metaDataRepository;
        private readonly FileRepository fileRepository;
        private const string repoLatestVersion = "Repository.Latest.Version";
        // TODO: LATEST_VERSION is an interface-related magic value,
        // thus it should obviously not be within this specific *implementation* class
        // but rather be provided by a corresponding *interface* or base class.
        public const int LATEST_VERSION = -1;
        private readonly DebugRandomActivator debugRandomActivator;
        private readonly bool davPropertiesIsAllowedRead = Configuration.DAVPropertiesIsAllowedRead;
        private readonly bool davPropertiesIsAllowedWrite = Configuration.DAVPropertiesIsAllowedWrite;
        private WebDAVPropertyStorageAdaptor propsSerializer;

        public TFSSourceControlProvider(
            ITFSSourceControlService sourceControlService,
            string serverUrl,
            ICredentials credentials,
            string projectName,
            IWorkItemModifier workItemModifier,
            DefaultLogger logger,
            WebCache cache,
            FileRepository fileRepository)
        {
            this.sourceControlService = sourceControlService;
            this.serverUrl = serverUrl;
            this.credentials = CredentialsHelper.GetCredentialsForServer(this.serverUrl, credentials);

            // NOTE: currently all uses of this class are short-lived and frequent,
            // thus ctor should remain sufficiently *fast*.

            rootPath = Constants.ServerRootPath;
            if (!string.IsNullOrEmpty(projectName))
            {
                rootPath += projectName + "/";
            }
            // Hmm, what is the actual reason for the magic 259 value??
            // Probably it's due to Win32 MAX_PATH (260) "minus 1 something" (most likely trailing \0).
            // Since there's no MAX_PATH constant in C#, we'll just keep it open-coded.
            // If the MAX_PATH limitation turns out to be too painful, then perhaps the UNC path convention
            // ("\\?\" prefix, 32k chars limit) might actually be usable here.
            this.maxLengthFromRootPath = 259 - rootPath.Length;

            this.workItemModifier = workItemModifier;
            this.logger = logger;
            this.cache = cache;

            if (Configuration.CacheEnabled)
            {
                this.metaDataRepository = new MetaDataRepositoryCache(
                    this.sourceControlService,
                    this.serverUrl,
                    this.credentials,
                    this.rootPath,
                    Container.Resolve<MemoryBasedPersistentCache>());
            }
            else
            {
                this.metaDataRepository = new MetaDataRepositoryNoCache(
                    this.sourceControlService,
                    this.serverUrl,
                    this.credentials,
                    this.rootPath,
                    Container.Resolve<IRegistrationService>());
            }

            this.fileRepository = fileRepository;
            this.debugRandomActivator = new DebugRandomActivator();
        }

        private WebDAVPropertyStorageAdaptor WebDAVPropsSerializer
        {
            get
            {
                if (propsSerializer == null)
                {
                    propsSerializer = new WebDAVPropertyStorageAdaptor(this);
                }
                return propsSerializer;
            }
        }

        /// <summary>
        /// The main public interface handler for WebDAV COPY request.
        /// </summary>
        /// <param name="activityId">ID of the activity (transaction)</param>
        /// <param name="versionFrom">The version of the originating item</param>
        /// <param name="path">Location of originating item</param>
        /// <param name="targetPath">Location of destination item</param>
        /// <param name="overwrite">Specifies whether overwriting an existing item is allowed</param>
        public virtual void CopyItem(string activityId, int versionFrom, string path, string targetPath, bool overwrite)
        {
            // CopyAction is not capable of recording a version number that an item has been copied from,
            // thus I assume that a Copy operation is about currently *existing* items only:
            // "copying" == "file existing in HEAD to other file"
            // "writing" == "foreign-revision file to new file (regardless of whether same-path or different-path)"

            // I'm not really happy with this method layering / implementation - it's not very symmetric.
            // But as a first guess it's ok I... guess. ;)
            // FIXME: I'm also not sure about that revision handling here:
            // the source file might have been created at an older revision yet still *exists* currently -
            // we quite likely don't handle this properly here...
            // All in all I'm still feeling very uncertain
            // about how and what we're doing here...

            // Query both magic placeholder for "latest version" *and* do actual latest version HEAD value verify.
            bool copy_head_item = ((LATEST_VERSION == versionFrom) || (GetLatestVersion() == versionFrom));
            copy_head_item = true; // hotfix (branch below IS BROKEN, NEEDS FIXING!!! - probably some transaction management issue in WriteFile() used below)
            if (copy_head_item)
            {
                CopyAction copyAction = new CopyAction(path, targetPath, false);
                ActivityRepository.Use(activityId, delegate(Activity activity)
                {
                    activity.CopiedItems.Add(copyAction);
                });
                // FIXME: obey overwrite param!
                ProcessCopyItem(activityId, versionFrom, copyAction, false);
            }
            else
            {
                // This implements handling for e.g. TortoiseSVN "Revert changes from this revision" operation
                // as described by tracker #15317.
                ItemMetaData itemDestination = GetItemsWithoutProperties(LATEST_VERSION, targetPath, Recursion.None);
                bool can_write = ((null == itemDestination) || (overwrite));
                if (can_write)
                {
                    ItemMetaData itemSource = GetItemsWithoutProperties(versionFrom, path, Recursion.None);
                    byte[] sourceData = ReadFile(itemSource);
                    bool reportUpdatedFile = (null != itemDestination);

                    CopyAction copyAction = new CopyAction(path, targetPath, false);
                    ActivityRepository.Use(activityId, delegate(Activity activity)
                    {
                        activity.CopiedItems.Add(copyAction);
                    });

                    // FIXME: in case of a formerly deleted file, this erases all former file history
                    // due to adding a new file! However, a native-interface undelete operation on TFS2008
                    // (which could be said to be similar in its outcome to this operation in certain situations)
                    // *does* preserve history and gets logged as an Undelete
                    // (not to mention that doing this on an actual SVN is a copy *with* history, too!).
                    // I'm unsure whether we can massage things to have it improved,
                    // especially since flagging things as an Undelete seems to be out of reach in our API.
                    WriteFile(activityId, targetPath, sourceData, reportUpdatedFile);
                }
            }
        }

        public virtual void CopyItem(string activityId, string path, string targetPath)
        {
            CopyItem(activityId, LATEST_VERSION, path, targetPath, true);
        }

        /// <summary>
        /// The main public interface handler for WebDAV DELETE request.
        /// </summary>
        /// <param name="activityId">ID of the activity (transaction)</param>
        /// <param name="victimPath">path to file item</param>
        /// <returns>true when successfully deleted, else false</returns>
        public virtual bool DeleteItem(string activityId, string victimPath)
        {
            if ((GetItems(LATEST_VERSION, victimPath, Recursion.None, true) == null) && (GetPendingItem(activityId, victimPath) == null))
            {
                return false;
            }

            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                bool haveAnyPostCommitDeletes = false;
                foreach (CopyAction copy in activity.CopiedItems)
                {
                    bool item_sitting_below_deleted_path = copy.Path.StartsWith(victimPath + "/");
                    if (item_sitting_below_deleted_path)
                    {
                        if (!activity.PostCommitDeletedItems.Contains(victimPath))
                        {
                            activity.PostCommitDeletedItems.Add(victimPath);
                        }

                        if (!copy.Rename)
                        {
                            ConvertCopyToRename(activityId, copy);
                        }

                        haveAnyPostCommitDeletes = true;
                    }
                }

                if (!haveAnyPostCommitDeletes)
                {
                    bool deleteIsRename = false;
                    foreach (CopyAction copy in activity.CopiedItems)
                    {
                        if (copy.Path.Equals(victimPath))
                        {
                            ConvertCopyToRename(activityId, copy);
                            deleteIsRename = true;
                        }
                    }
                    if (!deleteIsRename)
                    {
                        ProcessDeleteItem(activityId, victimPath);
                        activity.DeletedItems.Add(victimPath);
                    }
                }
            });
            return true;
        }

        /// <remarks>
        /// Would be nice to be able to get rid of the very SVN-specific
        /// UpdateReportData method param dependency
        /// (this is the sole reason for the Protocol assembly dependency here),
        /// but since UpdateDiffCalculator below depends on it as well
        /// it's not possible even mid-term.
        /// OTOH, one could (rather strongly) argue
        /// that this entire bloated-interface method
        /// is somewhat misplaced within the provider class
        /// and should thus be external to it.
        /// OTOH this probably is done here to do a favour
        /// to the many tests that depend on it
        /// (and make use of the provider as their central object under test).
        /// So, perhaps do keep a "changed items" method after all
        /// and eventually decide to convert it to using a non-SVN update info class.
        /// </remarks>
        public virtual FolderMetaData GetChangedItems(
            string path,
            int versionFrom,
            int versionTo,
            UpdateReportData reportData)
        {
            SVNPathStripLeadingSlash(ref path);

            var root = (FolderMetaData)GetItems(versionTo, path, Recursion.None);

            if (root != null)
            {
                root.Properties.Clear();
            }

            // the item doesn't exist and the request was for a specific target
            if (root == null && reportData.UpdateTarget != null)
            {
                root = new FolderMetaData();
                var deletedFile = new DeleteMetaData
                {
                    ItemRevision = versionTo,
                    Name = reportData.UpdateTarget
                };
                ItemHelpers.FolderOps_AddItem(root, deletedFile);
                return root;
            }
            if (root == null)
            {
                throw new FileNotFoundException(path);
            }

            var udc = new UpdateDiffCalculator(this);
            udc.CalculateDiff(path, versionTo, versionFrom, root, reportData);
            if (reportData.UpdateTarget != null)
            {
                // FIXME: this one quite likely is WRONG (does not handle subpath expressions
                // of UpdateTarget - checks one hierarchy level only!).
                // Should be using a common UpdateTarget infrastructure helper like other
                // places which need that.
                // Hmm, and <see cref="UpdateReportService"/> implements a GetSrcPath()
                // (combines .SrcPath with .UpdateTarget), whereas we don't use that here -
                // but maybe possibly we should?
                // Well, ok, our *caller* (GetMetadataForUpdate(), i.e. *one* caller at least)
                // did determine path via .SrcPath after all,
                // but that kind of handling is terribly asymmetric :(
                // (evaluating reportData stuff outside *and* then here again)
                //
                // Should NOT add leading '/' here,
                // since items are not leading-'/'-based anyway!
                // Also, CombinePath() erroneously(?) adds leading slash
                // in case of empty arg1.
                string targetPath = /* "/" + */ Helper.CombinePath(path, reportData.UpdateTarget);
                // [cannot easily use List.RemoveAll() here]
                foreach (ItemMetaData item in new List<ItemMetaData>(root.Items))
                {
                    if (!item.IsSamePath(targetPath))
                        ItemHelpers.FolderOps_RemoveItem(root, item);
                }
            }
            return root;
        }

        /// <summary>
        /// This method appears to iterate over all prior actions within this transaction,
        /// in order to figure out where a soon-processed (and thus relocated, once the transaction
        /// gets processed) item used to originate *from*.
        /// I somewhat doubt that its implementation is fully precise, though -
        /// let's hope that order of activity.CopiedItems is precise, otherwise it might happen
        /// that current-path vs. current-TargetPath values as assigned within the loop
        /// fail to match, thus we won't make the next transition.
        /// Also, it might be that one CopyAction item that happens to have matching TargetPath
        /// actually is about copy of a *foreign* item, thus we'll enter the wrong source/dest path
        /// action chain and never make it back to the actually correct location sequence.
        /// Thus, FIXME (after a couple more hard looks at this method's behaviour).
        /// </summary>
        /// <param name="activityId">ID of the activity (transaction)</param>
        /// <param name="pathQuery">Location of the item to be searched within recorded activity</param>
        /// <returns>Valid ItemMetaData on success, else null</returns>
        public virtual ItemMetaData GetItemInActivity(string activityId, string pathQuery)
        {
            // The path that eventually is to contain the most original path of the actual item,
            // after having taken into account all recorded movements (if any) within the activity:
            string pathOrigin = pathQuery;
            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                foreach (CopyAction copy in activity.CopiedItems)
                {
                    if (pathOrigin.StartsWith(copy.TargetPath))
                    {
                        string priorSourcePath = copy.Path;
                        string targetPathSubdirPart = pathOrigin.Substring(copy.TargetPath.Length);

                        // Let's have path point to the likely candidate that it was prior to when
                        // the current (**hopefully** relevant!) CopyAction happened.
                        pathOrigin = priorSourcePath + targetPathSubdirPart;
                    }
                }
            });
            // Now that we have the most authoritative path, grab the actual item:
            return GetItemsWithoutProperties(LATEST_VERSION, pathOrigin, Recursion.None);
        }

        /// <summary>
        /// Probably a legacy name, with implementation identical to GetItemsWithoutProperties()
        /// (IOW, prefer the explicit naming of that one).
        /// </summary>
        /// <param name="version">Version (revision) to fetch the items of</param>
        /// <param name="path">Location to fetch the items of</param>
        /// <param name="recursion">Indicates mode of recursing into sub tree</param>
        /// <returns>Valid ItemMetaData on success, else null</returns>
        public virtual ItemMetaData GetItems(int version, string path, Recursion recursion)
        {
            return GetItems(version, path, recursion, false);
        }

        public virtual ItemMetaData GetItemsWithoutProperties(int version, string path, Recursion recursion)
        {
            return GetItems(version, path, recursion, false);
        }

        /// <summary>
        /// We are caching the value, to avoid expensive remote calls. 
        /// This is safe to do because <see cref="TFSSourceControlProvider"/> is a transient
        /// type, and will only live for the current request.
        /// </summary>
        /// <returns></returns>
        public virtual int GetLatestVersion()
        {
            var cacheItem = RequestCache.Items[repoLatestVersion];
            if (cacheItem == null)
            {
                cacheItem = sourceControlService.GetLatestChangeset(serverUrl, credentials);
                RequestCache.Items[repoLatestVersion] = cacheItem;
            }
            return (int)cacheItem;
        }

        public virtual LogItem GetLog(
            string path,
            int versionFrom,
            int versionTo,
            Recursion recursion,
            int maxCount,
            bool sortAscending = false)
        {
            return GetLog(
                path,
                LATEST_VERSION,
                versionFrom,
                versionTo,
                recursion,
                maxCount,
                sortAscending);
        }

        public virtual LogItem GetLog(
            string path,
            int itemVersion,
            int versionFrom,
            int versionTo,
            Recursion recursion,
            int maxCount,
            bool sortAscending = false)
        {
            bool isInterestedInItemChanges = true; // DEFINITELY yes!
            bool includeFiles = (isInterestedInItemChanges);
            bool generateDownloadUrls = false;
            bool slotMode = false;
            return GetLogImpl(
                path,
                itemVersion,
                versionFrom,
                versionTo,
                recursion,
                maxCount,
                includeFiles,
                generateDownloadUrls,
                slotMode,
                sortAscending);
        }

        private LogItem GetLogImpl(
            string path,
            int itemVersion,
            int versionFrom,
            int versionTo,
            Recursion recursion,
            int maxCount,
            bool includeFiles,
            bool generateDownloadUrls,
            bool slotMode,
            bool sortAscending)
        {
            SVNPathStripLeadingSlash(ref path);

            string serverPath = MakeTfsPath(path);

            // Prefer to avoid construction via ternary (would require ugly cast).
            VersionSpec itemVersionSpec = VersionSpec.Latest;
            if (itemVersion != LATEST_VERSION)
                itemVersionSpec = VersionSpec.FromChangeset(itemVersion);

            RecursionType recursionType = GetLogRecursionType(recursion);

            // WARNING: TFS08 QueryHistory() is very problematic! (see comments in next inner layer)
            SourceItemHistory[] histories = QueryHistory(
                serverPath,
                itemVersionSpec,
                versionFrom,
                versionTo,
                recursionType,
                maxCount,
                includeFiles,
                generateDownloadUrls,
                slotMode,
                sortAscending).ToArray();

            LogHistory_TweakIt_ForSVN(ref histories);

            LogItem logItem = new LogItem(null, serverPath, histories);

            return logItem;
        }

        private static RecursionType GetLogRecursionType(Recursion recursion)
        {
            // SVNBRIDGE_WARNING_REF_RECURSION
            RecursionType recursionType;
            switch (recursion)
            {
                case Recursion.None:
                    recursionType = RecursionType.None;
                    break;
                case Recursion.OneLevel:
                    // Hmm, why is this translated to .None here?
                    // There was neither a comment here nor was it encapsulated into a self-explanatory
                    // helper method.
                    // Perhaps it's for correcting OneLevel requests
                    // which probably don't make sense with log-type SVN queries... right?
                    recursionType = RecursionType.None;
                    break;
                case Recursion.Full:
                    recursionType = RecursionType.Full;
                    break;
                default: // unsupported/corrupt case!?
                    throw new NotSupportedException();
            }
            return recursionType;
        }

        /// <remarks>
        /// I don't fully know what exactly it is that we're doing in here,
        /// but I do know that whatever it is, it should be done in this separate helper here
        /// (ok, I think it's about converting log stuff towards SVN requirements).
        /// While this helper actually might be completely specific to UpdateDiffCalculator,
        /// I guess it's currently(?) implemented in this class since it needs to make direct use
        /// of internal sourceControlService member. Hmm.
        /// Warning: this handler is doing to-SVN post-processing
        /// not completely unsimilar to what is being done at the end of QueryHistory(), too
        /// (i.e. possibly we are talking about dirtily duplicate/redundant implementation
        /// of common requirements).
        /// </remarks>
        /// <param name="histories">Array of histories to be tweaked</param>
        private void LogHistory_TweakIt_ForSVN(ref SourceItemHistory[] histories)
        {
            foreach (SourceItemHistory history in histories)
            {
                List<SourceItem> renamedItems = new List<SourceItem>();
                List<SourceItem> deletedItems = new List<SourceItem>();
                List<SourceItem> branchedItems = new List<SourceItem>();

                foreach (SourceItemChange change in history.Changes)
                {
                    bool isRename = ((change.ChangeType & ChangeType.Rename) == ChangeType.Rename);
                    bool isDelete = ((change.ChangeType & ChangeType.Delete) == ChangeType.Delete);
                    bool isBranch = ((change.ChangeType & ChangeType.Branch) == ChangeType.Branch);

                    // WARNING: I wanted to add this check here to skip doing string handling
                    // for non-relevant entries, however that's a problem - see the comment below...
                    //                    bool relevantChange = (isRename || isBranch);
                    //                    if (!relevantChange)
                    //                        continue;

                    // Tweak/bend TfsLibrary SourceItem into SVN-side syntax via a grave HACK,
                    // and for *all* items, not just rename or branch!
                    // As long as TFS <-> SVN layering remains this unclean,
                    // please take great care when changing this call, since the exact place (i.e., here)
                    // to do it might be critical - if there are subsequent path comparisons,
                    // they might be done with the full expectation of non-TFS path syntax.
                    SourceItem_TFStoSVNsyntaxHACK(ref change.Item);

                    // Single change may contain *multiple* change types:
                    if (isRename)
                    {
                        renamedItems.Add(change.Item);
                    }
                    if (isDelete)
                    {
                        deletedItems.Add(change.Item);
                    }
                    if (isBranch)
                    {
                        branchedItems.Add(change.Item);
                    }
                }
                if (renamedItems.Count > 0)
                {
                    // Keep using the query method variant that's providing *ItemMetaData*-based results here -
                    // this generically SVN-side handling (after all we're shuffling things according to expressly SVN-side protocol requirements!)
                    // should avoid keeping messing with TfsLibrary-side API dependency types *as much as possible*,
                    // thus getting SourceItem-typed array results is undesirable.
                    // [with the problem remaining
                    // that we then keep working on TfsLibrary-side types
                    // such as SourceItemHistory... oh well].
                    //
                    // I had pondered "improving" naming of variables old* to preRename*,
                    // however that might be imprecise
                    // since it's possibly not only in a _rename_ change
                    // that there are "old items",
                    // but also with _copy_ (branch) or even other changes.
                    ItemMetaData[] oldItems = GetPreviousVersionOfItems(renamedItems.ToArray(), history.ChangeSetID);
                    var oldItemsById = new Dictionary<int, ItemMetaData>(oldItems.Length);
                    // [remember that renamedItems and oldItems containers have a same-index compatibility requirement]

                    // I pondered changing this loop into the (faster) decrementing type,
                    // but I'm unsure: I wonder whether
                    // having rename actions/items processed in strict incrementing order
                    // is actually *required* (since they might be inter-dependent).
                    var numRenamedItems = renamedItems.Count;
                    for (var i = 0; i < numRenamedItems; i++)
                    {
                        if (oldItems[i] != null)
                            oldItemsById[renamedItems[i].ItemId] = oldItems[i];
                    }

                    var renamesWithNoPreviousVersion = new List<SourceItemChange>();
                    var changesNone_Processed = // will contain those ChangeType.None changes which ought to be removed e.g. due to having discovered their corresponding match
                        new List<SourceItemChange>();
                    foreach (var change in history.Changes.Where(change => (change.ChangeType & ChangeType.Rename) == ChangeType.Rename))
                    {
                        ItemMetaData oldItem;
                        bool renameWithPreviousVersion = false;
                        bool haveOldItem = oldItemsById.TryGetValue(change.Item.ItemId, out oldItem);
                        bool haveValidOldItem = false;
                        if (haveOldItem)
                        {
                            haveValidOldItem = IsValidRenameChange(
                                oldItem,
                                change.Item);
                        }
                        if (haveValidOldItem)
                        {
                            RenamedSourceItem itemRenamed = new RenamedSourceItem(change.Item, oldItem.Name, oldItem.Revision);
                            change.Item = itemRenamed;
                            bool changeIncludesDeletionHint = ((change.ChangeType & ChangeType.Delete) == ChangeType.Delete);
                            bool needSpecialCheckForDeletedSubItemsMovedByParentRename = (changeIncludesDeletionHint);
                            if (needSpecialCheckForDeletedSubItemsMovedByParentRename)
                            {
                                foreach (var changeNoneCandidate in history.Changes.Where(changeCandidate => (changeCandidate.ChangeType & ChangeType.None) == ChangeType.None))
                                {
                                    bool thisChangeMatchesSourceOfRename = (changeNoneCandidate.Item.RemoteName == itemRenamed.OriginalRemoteName);
                                    if (thisChangeMatchesSourceOfRename)
                                    {
                                        var changeNone_previously_deleted_item_placeholder_marker = changeNoneCandidate;
                                        // I don't know WHY TFS decides to indicate relocation of prior-delete items
                                        // the way it does it
                                        // (it may have very good reasons,
                                        // such as perhaps enabling proper folding during branch merge operations...),
                                        // but what I do know is:
                                        // definitely do NOT indicate an *active* Delete change to the SVN side
                                        // when relocating a previously-deleted (read: no-longer-existing) item!
                                        // UPDATE: nope, in fact we SHOULD keep the Delete change indicated!
                                        // Currently known situations where this occurs:
                                        // - relocation of a parent folder containing a sub item which had been *previously deleted*:
                                        //   TFS indicates a Rename | Delete change for this sub item
                                        //   (probably to ensure that after folder relocation the non-existent sub item in fact *also does not exist there*),
                                        //   together with annotating (hinting?) the previously-deleted sub item at source side as a None change
                                        // - relocation of a parent folder containing an existing (*non-deleted*) sub item
                                        //   which then *additionally* gets Delete:d by the user at destination side *within this same commit*!:
                                        //   TFS indicates a Rename | Delete change for this sub item
                                        //change.ChangeType &= ~(ChangeType.Delete);
                                        changesNone_Processed.Add(changeNone_previously_deleted_item_placeholder_marker);
                                    }
                                }
                            }
                            renameWithPreviousVersion = true;
                        }
                        if (!renameWithPreviousVersion)
                        {
                            // I suspect that such cases might in fact
                            // always be processing errors
                            // rather than "special" TFS input.
                            // Thus, since after some further fixes
                            // such cases now should be sufficiently rare,
                            // it now ought to be actively analyzed when occurring:
                            Helper.DebugUsefulBreakpointLocation();

                            renamesWithNoPreviousVersion.Add(change);
                        }
                    }

                    // [this is slowpath (rare event),
                    // thus Remove() is better than Enumerable.Except() use:]
                    foreach (var victim in changesNone_Processed)
                        history.Changes.Remove(victim);
                    foreach (var victim in renamesWithNoPreviousVersion)
                        history.Changes.Remove(victim);

                    history.Changes.RemoveAll(change => change.ChangeType == ChangeType.Delete &&
                                              oldItems.Any(oldItem => oldItem != null && oldItem.Id == change.Item.ItemId));
                }
                if (branchedItems.Count > 0)
                {
                    var itemsBranched = branchedItems.Select(item => CreateItemSpec(MakeTfsPath(item.RemoteName), RecursionType.None)).ToArray();

                    ChangesetVersionSpec branchChangeset = VersionSpec.FromChangeset(history.ChangeSetID);
                    BranchRelative[][] branches = sourceControlService.QueryBranches(serverUrl, credentials, null, itemsBranched, branchChangeset);

                    // NOTE performance/efficiency/scaling issue:
                    // at least for some handling, three of the loop-processed arrays here
                    // contain exactly the same amount of items,
                    // in other words these loops yield roughly cubic (^3) complexity.
                    // I'm not quite sure whether there should be done anything about it now though,
                    // since processing penalty most likely is network-bound (e.g. QueryBranches() above).
                    // However for very large amounts of items (> 1000?),
                    // processing penalty will likely become CPU-bound.
                    foreach (BranchRelative[] branch in branches)
                    {
                        foreach (SourceItem item in branchedItems)
                        {
                            foreach (BranchRelative branchItem in branch)
                            {
                                if (item.ItemId == branchItem.BranchToItem.itemid)
                                {
                                    foreach (SourceItemChange change in history.Changes)
                                    {
                                        if (change.Item.ItemId == item.ItemId)
                                        {
                                            // The branching actions history table of an item
                                            // lists *all* actions of that item,
                                            // no matter whether:
                                            // actual branching, or renaming, or initial creation...
                                            bool newlyAdded = (null == branchItem.BranchFromItem);
                                            bool bRenamed = (!newlyAdded);
                                            if (bRenamed)
                                            {
                                                string oldName = FilesysHelpers.PathPrefix_Checked_Strip(rootPath, branchItem.BranchFromItem.item);
                                                // FIXME: decrement-by-1 might happen to just work, or actually be too hardcoded after all -
                                                // so if things fail then we might need to replace it by a call to GetPreviousVersionOfItems().
                                                int oldRevision = item.RemoteChangesetId - 1;
                                                change.Item = new RenamedSourceItem(item, oldName, oldRevision);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                // ChangeType.None seems to happen e.g. in cases where parent items get renamed/deleted away,
                // yet where there's a prior-deletion sub item
                // which then seems to be intended to remain "annotated" (via ChangeType.None).
                // (this might actually be new behaviour in TFS2013 vs. TFS2008).
                // Also, perhaps .None entries *are* meaningful for (some?) changes other than renames/deletes
                // (perhaps hinting about [non-]deleted identical items in various branches,
                // for correct branching overlay operation handling??) - who knows...
                // Hmm, there remain the following unsolved issues:
                // - was .None handling really intended to be done in such a coarse manner??
                //   (delete *all* .None entries in case any rename happened [and now added for deletes, too])
                // - quite likely .None entries should only be deleted whenever they are directly affected
                //   by a rename/delete of a parent item...
                // - also, I'm not really certain about the order of operations in this handler -
                //   .None entries probably ought to be removed only *after* Branching handling,
                //   but in general perhaps important Branching ops ought to be done *prior* to rename/delete handling, too!
                //   (and perhaps this order ought to be re-executed *per-item* even, not after the whole loop has been analyzed!?)
                //   Is there any information on how other SCMs process such things?
                //
                // OK, one case of None Change being indicated is:
                // Cleanly simply rename of an entire sub/ directory into its root folder
                // (i.e., all of its items), where the interesting items then are:
                // $/TeamProj/sub/foo/file1.txt None
                // $/TeamProj/foo/file1.txt Rename|Delete
                // , at least in this case where
                // $/TeamProj/sub/foo/file1.txt
                // got Added:ed and then also Delete:d (*prior* to this commit) -
                // this prior-delete item status likely is the reason
                // that we're getting indicated a None (as opposed to an active Delete!) Change
                // for this *source item* during the folder rename!
                // *and also* the destination item
                // $/TeamProj/foo/file1.txt
                // got Add:ed and then also Delete:d (*prior* to this commit).
                // This would seem to indicate that we would need to match up any None Changes
                // on the source side with the Change on the destination side,
                // i.e. we would need to watch all Renames
                // to see whether they also indicate a Delete
                // and if so check
                // whether the reason for the Delete
                // is a matching None Change on the source side,
                // and if so remove the Delete indication on that Change
                // (since the destination side does NOT have that item existing!),
                // but quite possibly
                // this removal should be done only at the *very end* of our processing,
                // since these "annotation helper" Change flags
                // might be very important e.g. during prior branch-merge-folding processing.
                bool needProcessRemovalOfChangeTypeNoneItems = ((renamedItems.Count > 0) || (deletedItems.Count > 0));
                if (needProcessRemovalOfChangeTypeNoneItems)
                {
                    history.Changes.RemoveAll(change => change.ChangeType == ChangeType.None);
                }
            }
        }

        /// <summary>
        /// Special helper to deal with the strange case
        /// of getting veritable "ghost"/"zombie" rename commits from TFS
        /// (AFAICS this changeset content is what we are getting from inner TFS layers,
        /// and AFAICS it indeed does not make sense!):
        /// changesets containing change(s) with ChangeType.Rename,
        /// yet everything IDENTICAL:
        /// - origin and destination name same-name same-case
        /// - origin and destination item IDs
        /// - item size
        ///
        /// Method signature is strangely type-asymmetric
        /// due to asymmetry at existing user, but oh well...
        /// </summary>
        /// <remarks>
        /// Of course a whole-folder compare
        /// in TFS2013 Source Control Explorer (MSVS2010sp1)
        /// of that changeset
        /// DOES come up completely empty, too!
        /// Turns out that this was a changeset
        /// where people did same-name different-case renames
        /// of some base folders.
        /// So it's understandable after all
        /// that Source Control Explorer would filter away
        /// all case-only changes
        /// since that seems to be their strange design intent.
        /// What *isn't* understandable
        /// is that that a part of the changes within this changeset
        /// were completely IDENTICAL ones,
        /// rather than case-only changes.
        /// And this is when things started falling apart
        /// in SvnBridge areas, understandably,
        /// prior to this fix...
        /// Hmmmmmmm, thinking about it some more,
        /// it might be the case
        /// that for these problematic (completely IDENTICAL) origin items,
        /// our inner low-level filters
        /// which do TFS case issues sanitize-correcting
        /// failed to resolve the actually correct case in advance.
        /// So perhaps our filter algo
        /// still might need some improving
        /// (perhaps lookup of the most recent older revision
        /// doesn't suffice,
        /// since the correct old-item case will be revealed by TFS
        /// only for even older revisions - who knows...).
        /// AND INDEED, it looks like I failed to
        /// take into account special properties of ItemType.Any there
        /// (ItemType.File != ItemType.Any *is* ok
        /// where "any" type i.e. ItemType.Any is requested),
        /// causing us to skip path checks,
        /// since we generally do that for all non-matching item types.
        /// FIXME: Hmmm, something still seems to be fishy -
        /// many items (at least some of those also have a Delete change,
        /// while actually having been Delete:d before)
        /// within a commit which does case-only renames of folders
        /// do NOT end up with a renamed path
        /// once past our sanitizing filters.
        /// </remarks>
        /// <param name="oldItem"></param>
        /// <param name="newItem"></param>
        /// <returns></returns>
        private static bool IsValidRenameChange(
            ItemMetaData oldItem,
            SourceItem newItem)
        {
            bool isValidRename;

            // PERFORMANCE: definitely compare numeric values first,
            // *then* expensive strings!
            bool isMatch = (
                (oldItem.Id == newItem.ItemId) &&
                (oldItem.Name.Equals(newItem.RemoteName))
            );
            isValidRename = !(isMatch);

            return isValidRename;
        }

        /// <summary>
        /// GRAVE WARNING: this is an active tweaking of SourceItem.RemoteName conventions
        /// from TeamProject "$/PROJ/..." syntax to "PROJ/..." syntax.
        /// IMHO this is a rather ILLEGAL operation here since it changes syntax expectations
        /// in a manner that's incompatible with usual TFS SourceItem protocol,
        /// *while remaining in / re-using original object type space of those parts*!!
        /// However subsequent handling of course currently relies on the current behaviour,
        /// thus we should not skip it! (FIXME perhaps gradually move away from such questionable
        /// ad-hoc tweaking, by providing some switchable helper methods)
        /// </summary>
        /// <param name="sourceItem">TFS source item object to be tweaked</param>
        private void SourceItem_TFStoSVNsyntaxHACK(ref SourceItem sourceItem)
        {
            // Tweaks a path from full TFS team project syntax ("$/some/path")
            // to project sub path syntax ("some/path").
            // HACK: actively modifying an *existing* member within an improper-layer object type.
            sourceItem.RemoteName = FilesysHelpers.PathPrefix_Checked_Strip(rootPath, sourceItem.RemoteName);
        }

        private IEnumerable<SourceItemHistory> ConvertChangesetsToSourceItemHistory(Changeset[] changesets)
        {
            IEnumerable<SourceItemHistory> commits;

            // Attention: .Select() has the opportunity
            // of getting executed dynamically i.e. once index requested by user only!
            // Thus make sure to avoid flattening conversions
            // ("materializing" content) right until outer users,
            // which in the ideal situation
            // will enable us to (web-service-)request only
            // the particular subset of container indices
            // which is actually being requested by the API user.
            // However given that TfsLibrary-side infrastructure
            // already uses List members (e.g. SourceItemHistory.Changes),
            // IEnumerable.Select() won't really help all that much anyway...
            // (not to mention that that would perhaps mean
            // doing web service requests for each individual container index
            // rather than efficiently fetching one entire semi-large container
            // in one go).
            commits = changesets.Select(changeset => ConvertTFSChangesetToSVNCommit(changeset));

            return commits;
        }

        private SourceItemHistory ConvertTFSChangesetToSVNCommit(
            Changeset changeset)
        {
            // Warning: this handler is doing to-SVN post-processing
            // not completely unsimilar to what is being done at the end of GetLogImpl(), too.
            // Possibly the *end result* of this currently disparate-call-hierarchy handling
            // ought to become the *same* in all cases,
            // since I'd expect SVN-side requirements to always be the same
            // no matter which of the SVN protocol parts (REPORT, ...) we're getting called from
            // (famous last words...).

            SourceItemHistory historyOfSVNCommit = ConstructSourceItemHistoryFromChangeset(
                changeset);

            // We'll try to implement handling in a way that preserves
            // the *sort order* of changes as gotten from TFS as much as possible
            // (by adding all potential changes linearly,
            // then after having established the full list
            // filtering out any unwanted entries as needed).
            // I'm not certain whether preserving proper order of changes is an important requirement on SVN,
            // but better do implement it this way.
            // And do abstract away property storage details etc. as much as possible, too.

            historyOfSVNCommit.Changes = ConvertTFSChangesetToSVNSourceItemChanges(
                changeset).ToList();

            return historyOfSVNCommit;
        }

        private static SourceItemHistory ConstructSourceItemHistoryFromChangeset(
            Changeset changeset)
        {
            // Username used to get set to changeset.cmtr, but at
            // least for VSS-migrated repositories and for gated
            // checkins this is wrong, thus try using changeset.owner.
            // For details and possible variants to get this fixed,
            // please see "Log Messages Issue - Committer vs. Owner when
            // using Gated Check-In"
            //   http://svnbridge.codeplex.com/discussions/260147
            return new SourceItemHistory(
                GetChangesetRevisionValueRelevantForHistory(
                    changeset),
                changeset.owner,
                changeset.date,
                changeset.Comment);
        }

        /// <summary>
        /// Figures out the revision (Changeset ID)
        /// which is the one relevant for a SVN-protocol commit's content description.
        /// </summary>
        /// <remarks>
        /// Since there are cases where the Changeset does not contain any Changes
        /// (e.g. QueryHistory() param includeFiles == false),
        /// we assume that we in fact want to return the Changeset-side revision value
        /// rather than the revision value of the *first* Change in the Changeset.
        /// </remarks>
        /// <param name="changeset"></param>
        /// <returns>SVN commit revision ID</returns>
        private static int GetChangesetRevisionValueRelevantForHistory(
            Changeset changeset)
        {
            //return changeset.Changes[0].Item.cs;
            EnsureRevisionValueConsistency(changeset);
            return changeset.cset;
        }

        [Conditional("DEBUG")]
        private static void EnsureRevisionValueConsistency(Changeset changeset)
        {
            bool haveChangesElements = (0 < changeset.Changes.Length);
            if (haveChangesElements)
            {
                bool isRevisionMatch = (changeset.cset == changeset.Changes[0].Item.cs);
                if (!(isRevisionMatch))
                {
                    throw new RevisionValueMismatchException(changeset);
                }
            }
        }

        public sealed class RevisionValueMismatchException : NotSupportedException
        {
            public RevisionValueMismatchException(Changeset changeset)
                : base("Changeset " + changeset + "has revision mismatch between main revision and first-item revision")
            {
                Helper.DebugUsefulBreakpointLocation();
            }
        }

        private IEnumerable<SourceItemChange> ConvertTFSChangesetToSVNSourceItemChanges(
            Changeset changeset)
        {
            IEnumerable<SourceItemChange> changesSVNValid;

            List<SourceItemChangeClassifier> changeClassifiers = new List<SourceItemChangeClassifier>(changeset.Changes.Length);

            foreach (Change change in changeset.Changes)
            {
                // Design intent: go towards an implementation of pluggable SourceItemChange generators/modifiers, to:
                // - have TFS-side property storage specifics fully abstracted away
                // - veto some SourceItemChange additions (as e.g. in the case of the [non-SVN-related] root folder that's used for property storage items)
                // - generate standard changes for standard source control items
                SourceItemChange sourceItemChange = null;

                bool isChangeRelevantForSVNHistory = !WebDAVPropertyStorageAdaptor.IsPropertyFolderType(change.Item.item);

                if (!(isChangeRelevantForSVNHistory))
                {
                    continue;
                }

                sourceItemChange = GetSourceItemChange_For_Potential_PropertyChange(change);
                // Handle various change reasons (prop change, source item change, ...)
                // In future might possibly sometimes even end up generating multiple changes for a single original change!
                bool isPropertyChange = (null != sourceItemChange);
                if (isPropertyChange)
                {
                    SourceItemChangeClassifier changeClassifier = new SourceItemChangeClassifier(SourceItemChangeClassifier.ChangeModifierType.PropEdit, sourceItemChange);
                    changeClassifiers.Add(changeClassifier);
                }
                else
                {
                    SourceItem sourceItem = ConvertChangeToSourceItem(change);
                    ChangeType changeType = change.type;
                    if ((changeType == (ChangeType.Add | ChangeType.Edit | ChangeType.Encoding)) ||
                        (changeType == (ChangeType.Add | ChangeType.Encoding)))
                        changeType = ChangeType.Add;
                    sourceItemChange = new SourceItemChange(sourceItem, changeType);
                    SourceItemChangeClassifier changeClassifier = new SourceItemChangeClassifier(SourceItemChangeClassifier.ChangeModifierType.AuthoritativeTFS, sourceItemChange);
                    changeClassifiers.Add(changeClassifier);
                }
            }

            changesSVNValid = CollapseChangeClassifiersForCleanSVNHistory(changeClassifiers);

            return changesSVNValid;
        }

        /// <summary>
        /// Purpose: abstracts away property storage location specifics.
        /// </summary>
        /// <param name="change">The change to be analyzed</param>
        /// <returns>Suitable SourceItemChange for the data item that had a property edit</returns>
        private static SourceItemChange GetSourceItemChange_For_Potential_PropertyChange(Change change)
        {
            SourceItemChange sourceItemChange = null;

            bool isChangeOfAnSVNProperty = WebDAVPropertyStorageAdaptor.IsPropertyFileType(change.Item.item);
            if (isChangeOfAnSVNProperty)
            {
                SourceItem sourceItem = ConvertChangeToSourceItem(change);
                string item_actual = WebDAVPropertyStorageAdaptor.GetPathOfDataItemFromPathOfPropStorageItem(change.Item.item);
                ItemType itemType_actual = WebDAVPropertyStorageAdaptor.IsPropertyFileType_ForFolderProps(change.Item.item) ? ItemType.Folder : ItemType.File;
                sourceItem.RemoteName = item_actual;
                sourceItem.ItemType = itemType_actual;
                ChangeType changeType_PropertiesWereModified = ChangeType.Edit;

                sourceItemChange = new SourceItemChange(sourceItem, changeType_PropertiesWereModified);
            }
            return sourceItemChange;
        }

        private static SourceItem ConvertChangeToSourceItem(Change change)
        {
            return ConvertItemToSourceItem(change.Item);
        }

        private static SourceItem ConvertItemToSourceItem(Item item)
        {
            SourceItem sourceItem;

            // Side note: .durl may turn out to be null in certain occasions...

            // Do this conversion via internal, central (cache-friendly) handling
            // of standard toolkit APIs (TfsLibrary),
            // and choose the most direct API variant.
            //sourceItem = SourceItem.FromRemoteItem(item.itemid, item.type, item.item, item.cs, item.len, item.date, item.durl);
            sourceItem = SourceItem.FromRemoteItem(item);
            sourceItem.DownloadUrl = item.durl;

            return sourceItem;
        }

        private IEnumerable<SourceItemChange> CollapseChangeClassifiersForCleanSVNHistory(IEnumerable<SourceItemChangeClassifier> changeClassifiers)
        {
            List<SourceItemChange> changesSVNValid = new List<SourceItemChange>(changeClassifiers.Count());

            // Now that we managed to create an *SVN-side* view of the changes
            // (independent of TFS-internal property storage specifics, etc.),
            // filter them as needed:
            foreach (SourceItemChangeClassifier changeClassifierAdd in changeClassifiers)
            {
                switch (changeClassifierAdd.modifierType)
                {
                    case SourceItemChangeClassifier.ChangeModifierType.AuthoritativeTFS:
                        // Straight pass-through:
                        changesSVNValid.Add(changeClassifierAdd.change);
                        break;
                    case SourceItemChangeClassifier.ChangeModifierType.PropEdit:
                        SourceItemChange changePropEdit = changeClassifierAdd.change;
                        // Determine whether there's already a change listed for the data item
                        // corresponding to our WebDAV property change,
                        // otherwise add an invented, virtual change of the data item
                        // (in SVN, a property change is to be signalled as an "Edit" of the data item file).
                        bool itemFileIncludedInChanges = false;
                        foreach (SourceItemChangeClassifier changeClassifier in changeClassifiers)
                        {
                            bool isSameName = (changePropEdit.Item.RemoteName.Equals(changeClassifier.change.Item.RemoteName));
                            if (isSameName)
                            {
                                bool hitSameObject = (changeClassifier.change == changePropEdit);
                                if (!hitSameObject)
                                {
                                    itemFileIncludedInChanges = true;
                                    break;
                                }
                            }
                        }
                        bool addPropEditChangeForItem = !itemFileIncludedInChanges;
                        if (addPropEditChangeForItem)
                        {
                            // Need to guard against the pathological case of there being a property storage item
                            // yet no corresponding data item (WTH!?) - may happen in at least two cases:
                            // - property storage handling implementation bugs
                            // - accidental modifications/deletes of these SCM items
                            //   (partially even SVN-specific ones, as in the property storage items)
                            //   by non-SVN (i.e., TFS) clients
                            // When there's no data item file at all, it's a bit "inconvenient" (tm)
                            // to announce an invented "Edit" on it (and inventing an "Add" would be equally wrong)...

                            // HACK: need to *temporarily* go from TFS path to SVN syntax
                            // (*actual* TFS->SVN conversion of item members will be done over all changes later in processing,
                            // thus do NOT modify this single one here!).
                            string nonTfsPath = FilesysHelpers.PathPrefix_Checked_Strip(rootPath, changePropEdit.Item.RemoteName);
                            // Yup, verified to be that revision proper, not minus 1 or some such:
                            int itemVersion_VerifyExistence = changePropEdit.Item.RemoteChangesetId;
                            bool haveValidDataItemFile = SVNItemExists(nonTfsPath, itemVersion_VerifyExistence);
                            if (!haveValidDataItemFile)
                            {
                                addPropEditChangeForItem = false;
                            }
                        }
                        if (addPropEditChangeForItem)
                        {
                            changesSVNValid.Add(changePropEdit);
                        }
                        break;
                }
            }

            return changesSVNValid;
        }

        /// WARNING: the service-side QueryHistory() API will silently discard **older** entries
        /// in case maxCount is not big enough to hold all history entries!
        /// When attempting to linearly iterate from a very old revision to a much newer one (i.e., huge range)
        /// this is a *PROBLEM* since it's not easy to pick up where the result left us
        /// (i.e. within your older->newer loop you definitely end up
        /// with an unwanted newer-entries result part first,
        /// despite going from old to new).
        /// Certain (newer, it seems) variants of MSDN VersionControlServer.QueryHistory()
        /// include a sortAscending param which might be helpful to resolve this,
        /// but I don't know whether it can be used.
        /// Thus currently the only way to ensure non-clipped history
        /// is to supply maxCount int.MaxValue.
        ///
        /// Since call hierarchy of history handling is a multitude of private calls,
        /// it might be useful to move all this class-bloating handling
        /// into a separate properly isolated class specifically for this purpose.
        private List<Changeset> QueryChangesets_TFS_sanitize_querylimit_etc(
            string serverPath,
            VersionSpec itemVersion,
            int versionFrom,
            int versionTo,
            RecursionType recursionType,
            int maxCount,
            bool includeFiles,
            bool generateDownloadUrls,
            bool slotMode,
            bool sortAscending)
        {
            List<Changeset> changesetsTotal = new List<Changeset>();

            ItemSpec itemSpec = CreateItemSpec(serverPath, recursionType);
            VersionSpec versionSpecFrom = VersionSpec.FromChangeset(versionFrom);
            VersionSpec versionSpecTo = VersionSpec.FromChangeset(versionTo);
            // Since we'll potentially have multi-query,
            // maintain a helper to track how many additional items we're allowed to add:
            int maxCount_Allowed = maxCount;
            Changeset[] changesets;
            try
            {
                changesets = Service_QueryHistory(
                    itemSpec, itemVersion,
                    versionSpecFrom, versionSpecTo,
                    maxCount_Allowed,
                    includeFiles,
                    generateDownloadUrls,
                    slotMode,
                    sortAscending);
                changesetsTotal.AddRange(changesets);
            }
            catch (SoapException ex)
            {
                if ((recursionType == RecursionType.Full) && (ex.Message.EndsWith(" does not exist at the specified version.")))
                {
                    // Workaround for bug in TFS2008sp1
                    int latestVersionToBeQueried = GetLatestVersion();
                    // WARNING: TFS08 QueryHistory() is very problematic! (see comments here and in next inner layer)
                    List<Changeset> tempChangesets = QueryChangesets_TFS_sanitize_querylimit_etc(
                        serverPath,
                        itemVersion,
                        1,
                        latestVersionToBeQueried,
                        RecursionType.None,
                        2,
                        includeFiles,
                        generateDownloadUrls,
                        slotMode,
                        sortAscending /* is this the value to pass to have this workaround still work properly? */);
                    if (tempChangesets[0].Changes[0].type == ChangeType.Delete && tempChangesets.Count == 2)
                        latestVersionToBeQueried = tempChangesets[1].cset;

                    if (versionTo == latestVersionToBeQueried)
                    {
                        // in this case, there are only 2 revisions in TFS
                        // the first being the initial checkin, and the second
                        // being the deletion, there is no need to query further
                        changesetsTotal = tempChangesets;
                    }
                    else
                    {
                        string itemFirstPath = tempChangesets[0].Changes[0].Item.item; // debug helper
                        VersionSpec versionSpecLatestToBeQueried = VersionSpec.FromChangeset(latestVersionToBeQueried); // NOT necessarily == VersionSpec.Latest!
                        changesetsTotal = QueryChangesets_TFS_sanitize_querylimit_etc(
                            itemFirstPath,
                            versionSpecLatestToBeQueried,
                            1,
                            latestVersionToBeQueried,
                            RecursionType.Full,
                            int.MaxValue,
                            includeFiles,
                            generateDownloadUrls,
                            slotMode,
                            sortAscending);
                    }

                    // I don't know whether we actually want/need to do ugly manual version limiting here -
                    // perhaps it would be possible to simply restrict the queries above up to versionTo,
                    // but perhaps these queries were being done this way since perhaps e.g. for merge operations
                    // (nonlinear history) version ranges of a query do need to be specified in full.
                    Changesets_RestrictToRangeWindow(
                        ref changesetsTotal,
                        versionTo,
                        maxCount,
                        false);

                    return changesetsTotal;
                }
                else
                    throw;
            }

            int logItemsCount_ThisRun = changesets.Length;

            // TFS QueryHistory API won't return more than 256 items,
            // so need to call multiple times if more requested
            // IMPLEMENTATION WARNING: since the 256 items limit
            // clearly is a *TFS-side* limitation,
            // make sure to always keep this correction handling code
            // situated within inner TFS-side handling layers!!
            const int TFS_QUERY_LIMIT = 256;
            bool isRequestedLimitLargerThanMaxQueriesLimit = (maxCount_Allowed > TFS_QUERY_LIMIT);
            bool needCheckHitMaxQueriesLimit = (isRequestedLimitLargerThanMaxQueriesLimit);
            if (needCheckHitMaxQueriesLimit)
            {
                for (; ; )
                {
                    bool didHitPossiblyPrematureLimit = (TFS_QUERY_LIMIT == logItemsCount_ThisRun);
                    bool needContinueQuery = (didHitPossiblyPrematureLimit);
                    if (!(needContinueQuery))
                    {
                        break;
                    }
                    // Confirmed! We *did* get TFS_QUERY_LIMIT entries this time,
                    // yet request *was* larger than that,
                    // so there might be further entries remaining...

                    int earliestVersionFound = changesets[changesets.Length - 1].cset - 1;
                    if (earliestVersionFound == versionFrom)
                        break;

                    changesets = null; // GC (large obj / long-running op)

                    maxCount_Allowed -= logItemsCount_ThisRun;

                    versionSpecTo = VersionSpec.FromChangeset(earliestVersionFound);

                    changesets = Service_QueryHistory(
                        itemSpec, itemVersion,
                        versionSpecFrom, versionSpecTo,
                        maxCount_Allowed,
                        includeFiles,
                        generateDownloadUrls,
                        slotMode,
                        sortAscending);
                    changesetsTotal.AddRange(changesets);
                    logItemsCount_ThisRun = changesets.Length;
                }
            }

            return changesetsTotal;
        }

        private List<SourceItemHistory> QueryHistory(
            string serverPath,
            VersionSpec itemVersion,
            int versionFrom, int versionTo,
            RecursionType recursionType,
            int maxCount,
            bool includeFiles,
            bool generateDownloadUrls,
            bool slotMode,
            bool sortAscending)
        {
            List<SourceItemHistory> histories;

            Changeset[] changesets = QueryChangesets_TFS_sanitize_querylimit_etc(
                serverPath,
                itemVersion,
                versionFrom, versionTo,
                recursionType,
                maxCount,
                includeFiles,
                generateDownloadUrls,
                slotMode,
                sortAscending).ToArray();
            histories = ConvertChangesetsToSourceItemHistory(changesets).ToList();

            return histories;
        }

        private Changeset[] Service_QueryHistory(
            ItemSpec itemSpec, VersionSpec itemVersion,
            VersionSpec versionSpecFrom, VersionSpec versionSpecTo,
            int maxCount,
            bool includeFiles,
            bool generateDownloadUrls,
            bool slotMode,
            bool sortAscending)
        {
            Changeset[] changesets;

            // WARNING!! QueryHistory() (at least on TFS08) is very problematic, to say the least!
            // E.g. for a folder renamed-away into a subdir,
            // doing a query on its *previous* location, with an itemVersion/versionFrom/versionTo config that's properly pointing
            // at the prior state, will fail to yield any history. Only by doing a query on the still-existing *parent* folder
            // with these revision ranges will one manage to retrieve the proper history records of the prior folder location.
            // A somewhat related (but then not really...) SVN attribute is strict-node-history.
            changesets = sourceControlService.QueryHistory(serverUrl, credentials,
                null, null,
                itemSpec, itemVersion,
                null,
                versionSpecFrom, versionSpecTo,
                maxCount,
                includeFiles,
                generateDownloadUrls,
                slotMode,
                sortAscending);

            return changesets;
        }

        /// <summary>
        /// Restrict a possibly overly wide list of changesets to a certain desired range,
        /// by passing a maximum version to be listed,
        /// and by subsequently restricting the number of entries to maxCount.
        /// </summary>
        /// <param name="changesets">List of changesets to be modified</param>
        /// <param name="versionTo">maximum version to keep listing</param>
        /// <param name="maxCount">maximum number of entries allowed</param>
        /// <param name="whenOverflowDiscardNewest">when true: remove newest version entries, otherwise remove oldest.
        /// Hmm... not sure whether offering a whenOverflowDiscardNewest choice is even helpful -
        /// perhaps the user should always expect discarding at a certain end and thus _always_
        /// have loop handling for missing parts...
        /// </param>
        private static void Changesets_RestrictToRangeWindow(
            ref List<Changeset> changesets,
            int versionTo,
            int maxCount,
            bool whenOverflowDiscardNewest)
        {
            while ((changesets.Count > 0) && (changesets[0].cset > versionTo))
            {
                changesets.RemoveAt(0);
            }
            var numElemsExceeding = changesets.Count - maxCount;
            bool isCountWithinRequestedLimit = (0 >= numElemsExceeding);
            if (!(isCountWithinRequestedLimit))
            {
                // Order of the results that TFS returns is from _newest_ (index 0) to oldest (last index),
                // thus when whenOverflowDiscardNewest == true we need to remove the starting range,
                // else end range.
                var numElemsRemove = numElemsExceeding;
                int startIndex = whenOverflowDiscardNewest ? 0 : maxCount;
                changesets.RemoveRange(startIndex, numElemsRemove);
            }
        }

        public virtual bool IsDirectory(int version, string path)
        {
            ItemMetaData item = ItemExists_get(path, version, false);
            // Hmm, no null check here!?
            // But which result to indicate in case of failure? (hint: then it's not really a "file"...).
            // And IsDirectory() is not really being used (stubbed by tests only) anyway...
            return item.ItemType == ItemType.Folder;
        }

        /// <summary>
        /// Determines whether an item exists (plus, that it is not deleted).
        /// Often used to verify that there's sufficient permission to access this item
        /// (possibly including verifying its property storage areas, though!!).
        /// </summary>
        /// <param name="path">Path of the item to be queried</param>
        /// <returns>true if this non-deleted item exists, else false</returns>
        public virtual bool ItemExists(string path)
        {
            return ItemExists(path, LATEST_VERSION);
        }

        public virtual bool ItemExists(string path, int version)
        {
            // FIXME imprecise interface, gradually correct use
            // (e.g. by using more specifically named methods such as SVNItemExists() below).
            // Explanation: since this is a public interface method,
            // it would have been expected to be used by SVN-side users *only*,
            // in which case there *are* no (non-SVN) property storage implementation items to be returned!
            // (only *internal* Item existence checks would possibly need to query property storage,
            // but since those sometimes call into *this* ItemExists() variant
            // we currently need to have it return prop storage items, too...).
            // Not to mention that some users use this method
            // to verify item permission access,
            // i.e. property storage areas *are* relevant
            // (as they need to be accessible), too...
            bool returnPropertyFiles = true;
            return ItemExists(path, version, returnPropertyFiles);
        }

        /// <summary>
        /// Does item existence check for SVN-side purposes,
        /// i.e. in cases where we definitely want to avoid
        /// having (potentially problematic) handling of
        /// SVN-unrelated property storage item locations included.
        /// </summary>
        /// <param name="path">Path to SVN item</param>
        /// <param name="version">Version of SVN item</param>
        /// <returns>true if item exists, else false</returns>
        public virtual bool SVNItemExists(string path, int version)
        {
            return ItemExists(path, version, false);
        }

        private bool ItemExists(string path, int version, bool returnPropertyFiles)
        {
            bool itemExists = false;
            ItemMetaData item = ItemExists_get(path, version, returnPropertyFiles);
            itemExists = (null != item);
            return itemExists;
        }

        private ItemMetaData ItemExists_get(string path, int version, bool returnPropertyFiles)
        {
            // Decide to do strip-slash at the very top, since otherwise it would be
            // done *both* by GetItems() internally (its inner copy of the variable)
            // *and* below, by ItemMetaData implementation.
            SVNPathStripLeadingSlash(ref path);
            bool needAuthorshipLookup = false; // item existence check only!!
            ItemMetaData item = GetItems(
                version,
                path,
                Recursion.None,
                returnPropertyFiles,
                needAuthorshipLookup);
            if (item != null)
            {
                // Now that these checks are implemented
                // in a nicely efficient and central manner in lower layers,
                // we can disable it here:
                //bool needCheckCaseSensitiveItemMatch = (Configuration.SCMWantCaseSensitiveItemMatch);
                bool needCheckCaseSensitiveItemMatch = false;
                // FIXME: one could say that this case sensitivity check here
                // shouldn't be at this layer
                // (most likely it should be handled fully internally
                // by GetItems(), or somewhere where all the other accesses to .wantCaseSensitiveMatch
                // are being done).
                // Problem is that this is somewhat difficult:
                // while at this place here we're easily able to case-compare
                // the *single* result against the *single* and *authoritative* original query path,
                // within GetItems() it's an entirely different matter
                // since we're dealing with *multiple* and *unknown* results
                // returned from a query with *multiple* query paths
                // with potentially *full* recursion parm.
                if (needCheckCaseSensitiveItemMatch)
                {
                    bool itemExists = true;
                    // If the result item is a folder,
                    // then we'll have to do a hierarchy lookup,
                    // otherwise (single file), then we can do a direct compare.
                    if (ItemType.Folder == item.ItemType)
                    {
                        FolderMetaData folder = (FolderMetaData)item;
                        itemExists = (null != folder.FindItem(path));
                    }
                    else
                    {
                        // Comparison for path being data item vs. result being property storage item
                        // would fail, thus we need to do an additional comparison against data item path where needed:
                        itemExists = false;
                        bool haveCorrectlyCasedItem = item.Name.Equals(path);
                        if (haveCorrectlyCasedItem)
                            itemExists = true;
                        if (!itemExists)
                        {
                            bool itemMightBePropStorageItem = (returnPropertyFiles);
                            if (itemMightBePropStorageItem)
                            {
                                string itemPropPath = WebDAVPropertyStorageAdaptor.GetPropertiesFileName(path, item.ItemType);
                                bool haveCorrectlyCasedItem_Prop = item.Name.Equals(itemPropPath);
                                if (haveCorrectlyCasedItem_Prop)
                                    itemExists = true;
                            }
                        }
                    }

                    // FIXME! Case sensitivity handling at this wrongly-layered place
                    // actually should NOT be required nowadays,
                    // since inner layers do seem to handle things correctly.
                    // Thus, add a check to verify that, and if that check never triggers,
                    // then special-casing case sensitivity here can be removed.
                    if (!itemExists)
                    {
                        throw new ItemCaseMismatchException(item);
                    }
                }
            }
            return item;
        }

        public sealed class ItemCaseMismatchException : Exception
        {
            public ItemCaseMismatchException(ItemMetaData item)
                : base("Huh, item (" + item + ") returned and then found to be a case sensitivity mismatch despite inner layers already supposed to have filtered such items!?")
            {
            }
        }

        public virtual bool ItemExists(int itemId, int version)
        {
            return ItemExists(itemId, null, version);
        }

        /// <summary>
        /// Determines whether an item exists which has both this ID and (if not null) a specific path,
        /// at a specific version.
        /// Note that it will properly return null
        /// for items with deleted state,
        /// as opposed to some TFS APIs.
        /// </summary>
        /// <param name="itemId">ID of the item to query the existence of</param>
        /// <param name="path">Path of the item to query the existence of, else null</param>
        /// <param name="version">Version to query item existence at</param>
        /// <returns>true if non-deleted item exists, else false</returns>
        public virtual bool ItemExists(int itemId, string path, int version)
        {
            bool item_exists = false;
            if (0 == itemId)
                throw new ArgumentException("item id cannot be zero", "itemId");

            if (null == path)
            {
                // If ID-only query, we need to determine a path
                // to be able to subsequently do the path-based query
                // (in order to determine whether an item was deleted!).
                SourceItem[] sourceItems = metaDataRepository.QueryItems(version, itemId);
                if (sourceItems.Length != 0)
                {
                    string serverPath = sourceItems[0].RemoteName;
                    path = FilesysHelpers.PathPrefix_Checked_Strip(rootPath, serverPath);
                }
            }
            else
            {
                SVNPathStripLeadingSlash(ref path);
            }

            if (null != path)
            {
                // Doing SVN existence checking via ID-only TFS QueryItems() is NOT a good idea:
                // The item will get delivered, even if it has deleted state
                // (IOW, items will persist beyond deletion and simply carry a deletion state
                // [which cannot be determined with the SourceItem result members, though!!]).
                // Thus, since we do want to ignore deleted items on SVN side,
                // we need to do a path-based query
                // since that one actually does not return any deleted items.
                // And _then_ we also need to verify that the result's item id matches the one that was requested.
                ItemMetaData item = ItemExists_get(path, version, false);

                if (null != item)
                {
                    // Performance: first int-based compare, then string-based.
                    bool item_matches = ((itemId == item.Id) && (path == item.Name));
                    item_exists = item_matches;
                }
            }
            return item_exists;
        }

        /// <summary>
        /// Creates a DeltaV activity/transaction (WebDAV MKACTIVITY: RFC3253), emulated via TFS workspace.
        /// http://www.webdav.org/deltav/protocol/rfc3253-issues-list.htm
        /// "Use of WebDAV in Subversion"
        ///    http://svn.apache.org/repos/asf/subversion/trunk/notes/http-and-webdav/webdav-usage.html
        /// </summary>
        /// <param name="activityId">ID of the activity (transaction)</param>
        public virtual void MakeActivity(string activityId)
        {
            ClearExistingTempWorkspaces(true);

            sourceControlService.CreateWorkspace(serverUrl, credentials, activityId, Constants.WorkspaceComment);
            string localPath = GetLocalPath(activityId, "");
            sourceControlService.AddWorkspaceMapping(serverUrl, credentials, activityId, rootPath, localPath, 0);
            ActivityRepository.Create(activityId);
        }

        /// <summary>
        /// Is it one of our special activity workspaces?
        /// </summary>
        /// <param name="workspace">Workspace to examine</param>
        /// <returns>true in case it's one of our special workspaces, else false</returns>
        private static bool IsActivityWorkspace(WorkspaceInfo workspace)
        {
            return Constants.WorkspaceComment == workspace.Comment;
        }

        /// <summary>
        /// Deletes an activity/transaction (WebDAV DELETE /!svn/act/), emulated via TFS workspace.
        /// </summary>
        /// <param name="activityId">ID of the activity (transaction)</param>
        public virtual void DeleteActivity(string activityId)
        {
            // Did create-workspace then create-activity,
            // thus now delete-activity then delete-workspace (better obey scoped order).
            ActivityRepository.Delete(activityId);
            sourceControlService.DeleteWorkspace(serverUrl, credentials, activityId);
        }

        private void ClearExistingTempWorkspaces(bool skipExistingActivities)
        {
            WorkspaceInfo[] workspaces = sourceControlService.GetWorkspaces(serverUrl, credentials, WorkspaceComputers.ThisComputer, 0);
            foreach (WorkspaceInfo workspace in workspaces)
            {
                if (!IsActivityWorkspace(workspace))
                    continue;
                if (skipExistingActivities && ActivityRepository.Exists(workspace.Name))
                    continue;
                DeleteActivity(workspace.Name);
            }
        }

        public virtual void MakeCollection(string activityId, string path)
        {
            if (ItemExists(path))
            {
                // [Folder pre-existing in repository??
                // Possibly your repo working copy was not up-to-date
                // - try updating and *then* retry commit...]
                throw new FolderAlreadyExistsException();
            }

            ItemMetaData itemExistingBase = DetermineOutermostExistingBaseDirectoryItem(path, false);
            string localPath = GetLocalPath(activityId, path);
            string localBasePath = localPath.Substring(0, localPath.LastIndexOf('\\'));
            UpdateLocalVersion(activityId, itemExistingBase, localBasePath);

            string serverPath = MakeTfsPath(path);
            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                // This pend-req submission, as opposed to all others,
                // previously was *outside* of the activity lock above (at least the one here),
                // thus probably risking AB-BA deadlock in case of network issues.
                ServiceSubmitPendingRequest(activityId, PendRequest.AddFolder(localPath));
                activity.MergeList.Add(
                    new ActivityItem(serverPath, ItemType.Folder, ActivityItemAction.New));
                activity.Collections.Add(path);
            });

        }

        private int Service_Commit(
            string activityId,
            string comment,
            List<string> commitServerList,
            bool doWipeWorkspaceOnFailure)
        {
            try
            {
                // DEBUG_SITE: useful breakpoint location, pre-Commit()
                Helper.DebugUsefulBreakpointLocation();
                // (and you should also follow the docs / debug hints
                // at our Pending Changes helpers).
                // Or enable bool for safe commit testing
                // without actually completing any transactions
                // (probably especially useful for stress testing,
                // via prohibitively large transaction sizes).
                // Or perhaps even have that bool provided by a policy setting in .config?
                // OTOH that's probably not worth it, since you could almost just as well
                // simply make SvnBridge use a TFS user that does not have repo write perms.
                bool preventRepoModifications = false;
                if (preventRepoModifications)
                {
                    // Throw an obviously worded exception
                    // which makes it all the way through to the user's error window:
                    throw new InvalidOperationException("Source code configured to prevent repository modifications, by avoiding commits!");
                }
            }
            catch (InvalidOperationException)
            {
                // Do identical handling as below,
                // but keep both paths specific each
                // to only those exception types that they're supposed to intercept!
                if (doWipeWorkspaceOnFailure)
                {
                    ClearExistingTempWorkspaces(false);
                }

                throw;
            }

            int changesetId;
            try
            {
                changesetId =
                    sourceControlService.Commit(serverUrl, credentials,
                        activityId,
                        comment,
                        commitServerList,
                        false, 0);
            }
            catch (TfsFailureException)
            {
                if (doWipeWorkspaceOnFailure)
                {
                    // we just failed a commit, this tends to happen when we have a conflict
                    // between previously partially committed changes and the current changes.
                    // We will wipe all the user's temporary workspaces and allow the user to
                    // try again
                    ClearExistingTempWorkspaces(false);
                }

                throw;
            }
            return changesetId;
        }

        /// <summary>
        /// The main public interface handler for WebDAV MERGE request.
        /// Commits the recorded transaction contents on the server.
        /// </summary>
        /// <param name="activityId">ID of the activity (transaction)</param>
        /// <param name="disableMergeResponse">Indicates whether to laboriously collect a list of individual merge entries</param>
        /// <returns>MergeActivityResponse object, either with or without merge entries</returns>
        public virtual MergeActivityResponse MergeActivity(string activityId, bool disableMergeResponse)
        {
            MergeActivityResponse mergeResponse = null;
            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                UpdateProperties(activityId);
                List<string> commitServerList = new List<string>();
                foreach (ActivityItem item in activity.MergeList)
                {
                    if (item.Action != ActivityItemAction.RenameDelete)
                    {
                        commitServerList.Add(item.Path);
                    }
                    if (item.Action == ActivityItemAction.Branch)
                    {
                        // Keep GetLatestVersion() call within the loop for now,
                        // since it's used by the Branch case only (would penalize all other cases),
                        // and it has internal caching anyway.
                        // OTOH it's likely cached already from the get-go,
                        // so perhaps one initial call above would be better...
                        SourceItem[] items = metaDataRepository.QueryItems(GetLatestVersion(), item.SourcePath, Recursion.Full);
                        foreach (SourceItem sourceItem in items)
                        {
                            string branchedPath = item.Path + sourceItem.RemoteName.Substring(item.SourcePath.Length);
                            if (commitServerList.Contains(branchedPath) == false)
                                commitServerList.Add(branchedPath);
                        }
                    }
                }

                int changesetId;
                if (commitServerList.Count > 0)
                {
                    changesetId =
                        Service_Commit(
                                activityId,
                                activity.Comment,
                                commitServerList,
                                true);
                }
                else
                {
                    changesetId = GetLatestVersion();
                }

                if (activity.PostCommitDeletedItems.Count > 0)
                {
                    commitServerList.Clear();
                    foreach (string path in activity.PostCommitDeletedItems)
                    {
                        ProcessDeleteItem(activityId, path);
                        commitServerList.Add(MakeTfsPath(path));
                    }
                    changesetId =
                        Service_Commit(
                            activityId,
                            activity.Comment,
                            commitServerList,
                            false); // Hmm, really false?
                }
                AssociateWorkItemsWithChangeSet(activity.Comment, changesetId);
                mergeResponse = new MergeActivityResponse(changesetId, DateTime.Now, SCMHelpers.UnknownAuthorMarker);
                bool needGatherEntriesForMergeResponse = !(disableMergeResponse);
                if (needGatherEntriesForMergeResponse)
                {
                    MergeResponse_GatherEntries(activityId, mergeResponse);
                }
            });

            return mergeResponse;
        }

        /// <summary>
        /// Probably legacy-only handler (for unit tests etc.)
        /// </summary>
        /// <param name="activityId">ID of the current activity (transaction)</param>
        /// <returns>MergeActivityResponse result</returns>
        public virtual MergeActivityResponse MergeActivity(string activityId)
        {
            return MergeActivity(activityId, false);
        }

        public virtual void AssociateWorkItemsWithChangeSet(string comment, int changesetId)
        {
            MatchCollection matches = s_associatedWorkItems.Matches(comment ?? string.Empty);
            foreach (Match match in matches)
            {
                Group group = match.Groups[1];
                string[] workItemIds = group.Value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < workItemIds.Length; i++)
                {
                    string workItemId = workItemIds[i].Trim();

                    if (!string.IsNullOrEmpty(workItemId))
                    {
                        if (workItemId[0] == c_workItemChar)
                        {
                            workItemId = workItemId.Remove(0, 1);
                        }

                        int id;
                        if (int.TryParse(workItemId, out id) == false)
                        {
                            continue;
                        }
                        DoAssociate(id, changesetId);
                    }
                }
            }
        }

        private void DoAssociate(int workItemId, int changesetId)
        {
            try
            {
                string username = GetUsername();
                workItemModifier.Associate(workItemId, changesetId);
                workItemModifier.SetWorkItemFixed(workItemId, changesetId);
                DefaultLogger logger = Container.Resolve<DefaultLogger>();
                logger.Error("Associated changeset (would have used username " + username + " if that was implemented (FIXME)", null);
            }
            catch (Exception e)
            {
                // We can't really raise an error here, because
                // we would fail the commit from the client side, while the changes
                // were already committed to the source control provider;
                // since we consider associating with work items nice but not essential,
                // we will log the error and ignore it.
                // In many cases this errored out
                // due to not having provided an XML template file with correct content
                // (thus it's using often unsupported default CodePlex-specific tags in web request
                // rather than properly supported plain TFS-only parts).
                // FIXME: forcing a manual template config on unsuspecting users is rather cumbersome -
                // this should be handled as automatically as possible.
                // For helpful resources, see
                // http://svnbridge.codeplex.com/wikipage?title=Work Items Integration
                // "Work Item Association doesn't work"
                //   http://svnbridge.codeplex.com/workitem/9889?ProjectName=svnbridge
                // "Work Item Associations"
                //   http://svnbridge.codeplex.com/workitem/12411?ProjectName=svnbridge
                logger.Error("Failed to associate work item with changeset", e);
            }
        }

        /// <summary>
        /// Abstraction helper - somehow knows how to gather the user name
        /// which happens to be associated with the current session.
        /// </summary>
        private string GetUsername()
        {
            return TfsUtil.GetUsername(credentials, serverUrl);
        }

        public virtual IAsyncResult BeginReadFile(
            ItemMetaData item,
            AsyncCallback callback)
        {
            return fileRepository.BeginReadFile(
                item.DownloadUrl,
                GetRepositoryUuid(),
                callback);
        }

        public virtual byte[] EndReadFile(
            IAsyncResult ar)
        {
            return fileRepository.EndReadFile(
                ar);
        }

        /// <summary>
        /// OUTDATED (non-asynchronous i.e. blocking) API variant, DO NOT USE.
        /// </summary>
        public virtual byte[] ReadFile(ItemMetaData item)
        {
            return fileRepository.GetFile(item, GetRepositoryUuid());
        }

        public virtual void ReadFileAsync(ItemMetaData item)
        {
            fileRepository.ReadFileAsync(item, GetRepositoryUuid());
        }

        /// <summary>
        /// Returns a UUID/GUID precisely identifying the provider's repository
        /// (specific to the particular server URL).
        /// Note that this ID will be used
        /// both for item queries
        /// *and* for publishing in certain DAV elements.
        /// </summary>
        public virtual Guid GetRepositoryUuid()
        {
            string cacheKey = "GetRepositoryUuid_" + serverUrl;
            CachedResult result = cache.Get(cacheKey);
            if (result != null)
                return (Guid)result.Value;
            Guid id = sourceControlService.GetRepositoryId(serverUrl, credentials);
            cache.Set(cacheKey, id);
            return id;
        }

        public virtual int GetVersionForDate(DateTime date)
        {
            ItemSpec itemSpec = CreateItemSpec(rootPath, RecursionType.Full); // SVNBRIDGE_WARNING_REF_RECURSION
            VersionSpec versionSpecAtDate = ConvertToVersionSpec(date);
            bool isInterestedInItemChanges = false; // DEFINITELY not!
            bool includeFiles = (isInterestedInItemChanges);
            bool generateDownloadUrls = false;
            bool slotMode = false;
            // Keep handling in exception scope minimalistic to the operation which we may need to intercept:
            try
            {
                Changeset[] changesets = Service_QueryHistory(
                    itemSpec, VersionSpec.Latest,
                    VersionSpec.First, versionSpecAtDate,
                    1,
                    includeFiles,
                    generateDownloadUrls,
                    slotMode,
                    false);

                // If no results then date is before project existed
                if (changesets.Length == 0)
                    return 0;

                return changesets[0].cset;
            }
            catch (Exception e)
            {
                if (e.Message.StartsWith("TF14021:")) // Date is before repository started
                    return 0;

                throw;
            }
        }

        private static VersionSpec ConvertToVersionSpec(DateTime date)
        {
            // FIXME: is UTC normalization really required here??
            // We simply convert a DateTime into a VersionSpec,
            // thus TFS ought to be able to query the correct changeset anyway,
            // irrespective of whether our date input is UTC or not...
            DateTime dateUTC = date.ToUniversalTime();
            VersionSpec versionAtDate = VersionSpec.FromDate(dateUTC);
            return versionAtDate;
        }

        public virtual void SetActivityComment(string activityId, string comment)
        {
            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                activity.Comment = comment;
            });
        }

        public virtual void SetProperty(string activityId, string path, string propName, string propValue)
        {
            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                DAVPropertiesChanges propsChanges = FetchPathProperties(activity.Properties, path);
                propsChanges.Added[propName] = propValue;
            });
        }

        public virtual void RemoveProperty(string activityId, string path, string propName)
        {
            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                DAVPropertiesChanges propsChanges = FetchPathProperties(activity.Properties, path);
                propsChanges.Removed.Add(propName);
            });
        }

        private static DAVPropertiesChanges FetchPathProperties(IDictionary<string, DAVPropertiesChanges> dict, string path)
        {
            DAVPropertiesChanges propsChanges;
            if (dict.TryGetValue(path, out propsChanges))
            {
            }
            else
            {
                propsChanges = new DAVPropertiesChanges();
                dict[path] = propsChanges;
            }
            return propsChanges;
        }

        /// <summary>
        /// The main public interface handler for WebDAV PUT request.
        /// </summary>
        /// <param name="activityId">ID of the activity (transaction)</param>
        /// <param name="path">path to file item</param>
        /// <param name="fileData">data to be written into file</param>
        /// <returns>true if a new file has been created, else (updated only) false</returns>
        public virtual bool WriteFile(string activityId, string path, byte[] fileData)
        {
            return WriteFile(activityId, path, fileData, false);
        }

        /// <summary>
        /// Collects the list of item paths to be queried on TFS -
        /// a combination of the location of the actual data item
        /// and the locations of its accompanying property storage items.
        /// </summary>
        /// <param name="path">Location of the plain standard data item to be queried</param>
        /// <param name="recursion">Indicates the requested recursion type (None, OneLevel)</param>
        /// <returns>List of locations of relevant items to be queried</returns>
        private IEnumerable<string> CollectItemPaths(
            string path,
            Recursion recursion)
        {
            List<string> itemPaths;

            IEnumerable<string> propItemPaths = WebDAVPropertyStorageAdaptor.CollectPropertyItemLocationsToBeQueried(
                path,
                recursion);
            itemPaths = new List<string>(1 + propItemPaths.Count());
            itemPaths.Add(path);

            foreach(string propItemPath in propItemPaths)
            {
                if (propItemPath.Length <= maxLengthFromRootPath)
                    itemPaths.Add(propItemPath);
            }

            return itemPaths;
        }

        private ItemMetaData GetItems(
            int version,
            string path,
            Recursion recursion,
            bool returnPropertyFiles,
            bool needAuthorshipLookup)
        {
            ItemMetaData rootItem = null;

            SVNPathStripLeadingSlash(ref path);

            DetermineDesiredRevision(ref version, path);

            SourceItem[] sourceItems = GetTFSSourceItems(version, path, recursion);

            if (sourceItems.Length > 0)
            {
                var itemCollector = new ItemQueryCollector(this, version);
                // Authorship (== history) fetching is very expensive -
                // TODO: make intelligently configurable from the outside,
                // only where needed (perhaps via a parameterization struct
                // for this method?).
                // UPDATE: now that internal handling is streamlined,
                // should be sufficiently fast
                // (sample execution time: 4m27s vs. 5m36s)
                // thus enable users to request lookup:
                ItemMetaData[] items = ConvertSourceItemsWithAuthorship(
                    sourceItems,
                    needAuthorshipLookup);
                rootItem = itemCollector.process(items, returnPropertyFiles);

                if (!returnPropertyFiles)
                {
                    if (null != rootItem)
                    {
                        UpdateFolderRevisions(rootItem, version, recursion);
                    }
                }
            } // sourceItems.Length > 0

            return rootItem;
        }

        /// <summary>
        /// Legacy-signature helper.
        /// </summary>
        private ItemMetaData GetItems(
            int version,
            string path,
            Recursion recursion,
            bool returnPropertyFiles)
        {
            bool needAuthorshipLookup = true; // default to safe full-support
            return GetItems(
                version,
                path,
                recursion,
                returnPropertyFiles,
                needAuthorshipLookup);
        }

        /// <summary>
        /// Small helper (moves unrelated code out of the way,
        /// e.g. in order to ease debugger stepping)
        /// </summary>
        private void DetermineDesiredRevision(ref int version, string path)
        {
            if (version == LATEST_VERSION)
            {
                version = GetLatestVersion();
            }
            else
            if (version == 0 && path.Equals(""))
            {
                version = GetEarliestVersion(path);
            }
        }

        private SourceItem[] GetTFSSourceItems(int version, string path, Recursion recursion)
        {
            IEnumerable<string> itemPathsToBeQueried = CollectItemPaths(
                path,
                recursion);

            SourceItem[] sourceItems = metaDataRepository.QueryItems(version, itemPathsToBeQueried.ToArray(), recursion);
            if (sourceItems.Length > 0)
            {
                if (recursion == Recursion.OneLevel)
                {
                    if (sourceItems.Length > 0 && sourceItems[0].ItemType == ItemType.Folder)
                    {
                        List<string> propertiesForSubFolders = new List<string>();
                        foreach (SourceItem item in sourceItems)
                        {
                            if (item.ItemType == ItemType.Folder && !WebDAVPropertyStorageAdaptor.IsPropertyFolderType(item.RemoteName))
                            {
                                string propertiesForFolder = WebDAVPropertyStorageAdaptor.GetPropertiesFileName(item.RemoteName, ItemType.Folder);
                                if (propertiesForFolder.Length <= maxLengthFromRootPath)
                                    propertiesForSubFolders.Add(propertiesForFolder);
                            }
                        }
                        SourceItem[] subFolderProperties = metaDataRepository.QueryItems(version, propertiesForSubFolders.ToArray(), Recursion.None);
                        sourceItems = Helper.ArrayCombine(sourceItems, subFolderProperties);
                    }
                }
            }

            return sourceItems;
        }

        private ItemMetaData[] ConvertSourceItemsWithAuthorship(
            SourceItem[] sourceItems,
            bool needAuthorshipLookup)
        {
            IEnumerable<ItemMetaData> result;
            bool skipAuthorshipLookup = !(needAuthorshipLookup);
            if (skipAuthorshipLookup)
            {
                result = sourceItems.Select(sourceItem => SCMHelpers.ConvertSourceItem(sourceItem, rootPath, UnknownAuthorMarker));
            }
            else
            {
                result = ConvertSourceItemsWithAuthorship_LookupAuthor(
                    sourceItems);
            }
            return result.ToArray();
        }

        /// <remarks>
        /// I strongly suspect
        /// that "per-item" authors within the same revision
        /// *are* same author since it's simply content of the very same commit!!
        ///
        /// XXX Note that there's a bug remaining:
        /// It seems for folders (as opposed to files!),
        /// ItemSet[] TFSSourceControlService.QueryItems()
        /// will return the item's *creation* rev rather than *update* rev,
        /// and we'll thus do lookup on the creation rev.
        /// TortoiseSVN repo browser will thus display the current rev value
        /// in combination with the author of the creation rev. WTH???
        /// While certain requests will go here for a certain rev
        /// which we could use to do our queries on,
        /// I am not convinced that this will help and/or be correct.
        /// OTOH we could do such a workaround
        /// for the imprecision faced with folder-type items only.
        /// </remarks>
        private List<ItemMetaData> ConvertSourceItemsWithAuthorship_LookupAuthor(
            SourceItem[] sourceItems)
        {
            List<ItemMetaData> listItems = new List<ItemMetaData>(sourceItems.Length);
            // Clever trick: have construction penalty initially only,
            // then simply keep re-using same object with specific revision each:
            ChangesetVersionSpec versionSpecSourceItem = VersionSpec.FromChangeset(0);
            string author = null;
            foreach (SourceItem sourceItem in sourceItems)
            {
                bool isSameRevision = (versionSpecSourceItem.cs == sourceItem.RemoteChangesetId);
                bool needNewLookup = (!isSameRevision) || (null == author);
                if (needNewLookup)
                {
                    versionSpecSourceItem.cs = sourceItem.RemoteChangesetId;
                    author = ConvertSourceItemsWithAuthorship_LookupAuthorQuery(
                        sourceItem,
                        versionSpecSourceItem);
                }
                bool haveAuthor = (null != author);
                ItemMetaData item = SCMHelpers.ConvertSourceItem(
                    sourceItem,
                    rootPath,
                    haveAuthor ? author : UnknownAuthorMarker);
                listItems.Add(item);
            }

            return listItems;
        }

        private string ConvertSourceItemsWithAuthorship_LookupAuthorQuery(
            SourceItem sourceItem,
            VersionSpec versionSpecSourceItem)
        {
            string author = null;

            bool isInterestedInItemChanges = false; // DEFINITELY not!
            bool includeFiles = (isInterestedInItemChanges);
            bool generateDownloadUrls = false;
            bool slotMode = false;
                // AFAICS this lookup parameter *must* be the item revision rather than GetLatestVersion(), else incorrect-revision's author
                int latestVersion = sourceItem.RemoteChangesetId;
                // FIXME: which recursion type to use? Possibly we need to forward
                // a recursion config param from the caller...
                List<SourceItemHistory> hist = QueryHistory(
                    sourceItem.RemoteName,
                    versionSpecSourceItem,
                    1,
                    latestVersion,
                    RecursionType.Full,
                    1,
                    includeFiles,
                    generateDownloadUrls,
                    slotMode,
                    false);
                bool haveHistory = (0 < hist.Count);
                if (haveHistory)
                {
                    author = hist[0].Username;
                }

            return author;
        }

        private static string UnknownAuthorMarker
        {
            get
            {
                return SCMHelpers.UnknownAuthorMarker;
            }
        }

        /// <summary>
        /// Small helper to strip off the leading slash
        /// that may be fed from outer users of this interface,
        /// i.e. to be predominantly used in (usually) public methods of this class
        /// (inner methods should try to not need to call this any more,
        /// since public -> private transition is expected
        /// to already have catered for it).
        /// Longer explanation: since leading-slash paths are an SVN protocol characteristic
        /// (see e.g. raw paths passed into PROPFIND request)
        /// and TFSSourceControlProvider is our exact raw SVN-conformant interface,
        /// and ItemMetaData specs are always non-leading-slash,
        /// this is *exactly* the right internal layer to do path stripping.
        /// </summary>
        /// <param name="path">Path value to be processed</param>
        private static void SVNPathStripLeadingSlash(ref string path)
        {
            FilesysHelpers.StripRootSlash(ref path);
        }

        internal sealed class MostRecent_results
        {
            public int changeChangeSetID;
            public DateTime changeCommitDateTime;
            public bool result;
        };

        /// <summary>
        /// Helper to determine the most recent change somewhere within the directory tree of this (possibly folder) item.
        /// Desperately tries to do some shortcuts of this otherwise very expensive network request operation,
        /// since use of this function is an absolute hotpath, i.e. time spent here is rather dominant.
        /// Implemented in minimal-interface manner (the only thing that is of interest is whether it was successful,
        /// and then changeset ID and commit time - SourceItemHistory is NOT used anywhere near the caller
        /// thus shouldn't be leaked outside).
        /// </summary>
        /// This signature will NOT use ref-based parameters
        /// - to ease debugging, via combination of indicating success
        ///   and indicating result values to be assigned
        /// -  ref would be less clean
        /// <param name="item">item to be queried</param>
        /// <param name="version">version to do the query for</param>
        /// <param name="changeChangeSetID">Changeset ID of most recent change</param>
        /// <param name="changeCommitDateTime">Changeset commit date of most recent change</param>
        /// <returns>true when successfully queried, else false</returns>
        private bool DetermineMostRecentChangesetInTree(
            ItemMetaData item,
            int version,
            out int changeChangeSetID,
            out DateTime changeCommitDateTime)
        {
            // Admittedly I'm unsure of
            // what exactly the very expensive
            // DetermineMostRecentChangesetInTree_ExpensiveQuery() helper is about,
            // thus I'm doing some shortcut and comparison here,
            // to gain further insight
            // once things have been successfully determined to "break" here.
            MostRecent_results simple = null;
            MostRecent_results expensive = null;
            bool wantSimple = (item.Revision == version);
            bool wantExpensive = false;
            // Have breakpointable syntax:
            if (!wantExpensive)
            {
                if (!wantSimple)
                {
                    wantExpensive = true;
                }
            }
            if (!wantExpensive)
            {
                int doVerificationPercentage = 5;
                wantExpensive = GotRandom_percentage(doVerificationPercentage);
            }

            if (wantSimple)
            {
                simple = new MostRecent_results();
                simple.changeChangeSetID = item.Revision;
                simple.changeCommitDateTime = item.LastModifiedDate;
                simple.result = true;
            }
            if (wantExpensive)
            {
                expensive = new MostRecent_results();
                expensive.result = DetermineMostRecentChangesetInTree_ExpensiveQuery(
                    item.Name,
                    version,
                    out expensive.changeChangeSetID,
                    out expensive.changeCommitDateTime);
            }
            bool canCompareResults = false;
            //if (wantSimple && wantExpensive) // not needed
            if (true)
            {
                canCompareResults = ((null != simple) && (null != expensive));
            }
            if (canCompareResults)
            {
                // .Equals() does NOT work (automatically) here!
                //bool isMatch = simple.Equals(expensive);
                // [performance: comparison of less complex yet "decisive" objects first!]
                bool isMatch = (
                    (simple.changeChangeSetID == expensive.changeChangeSetID) &&
                    (simple.changeCommitDateTime == expensive.changeCommitDateTime) &&
                    (simple.result == expensive.result)
                );
                if (!(isMatch))
                {
                    Helper.DebugUsefulBreakpointLocation();
                    throw new InvalidOperationException("ERROR: simple vs. expensive lookup mismatch, please report!!");
                }
            }
            MostRecent_results results = (null != expensive) ? expensive : simple;

            changeChangeSetID = results.changeChangeSetID;
            changeCommitDateTime = results.changeCommitDateTime;
            return results.result;
        }

        private bool DetermineMostRecentChangesetInTree_ExpensiveQuery(
            string itemName,
            int version,
            out int changeChangeSetID,
            out DateTime changeCommitDateTime)
        {
            // Warning: use of very limited maxCount for TFS QueryHistory() (as done in this method: maxCount 1)
            // is problematic - since we're interested in the most recent change only,
            // this should be ok, but watch out...
            // Plus, QueryHistory() results (at least on TFS08) seem to be very unreliable:
            // a) querying a folder location which got renamed-away, with a proper *prior* ("still-existing") revision
            //    will fail to list the result (one needs to resort to querying parent folders
            //    to successfully get a large history which also includes that folder's activity - ARGH)
            // b) [AFAIR] querying a folder location which got renamed-away, with a proper *modern* revision
            //    will bogusly contain a changeset where that moved-away folder originally got *created*. WTF??
            // To verify this, simply modify data of the current query in a debugger,
            // then check expected results, then correct/comment here.

            SourceItemHistory logQueryAll_Newest_history = null;
            SourceItemHistory logQueryPartial_Newest_history = null;
            int itemVersion = version; // debugging helper
            int versionTo = version;
            bool isInterestedInItemChanges = false; // DEFINITELY not!
            bool includeFiles = (isInterestedInItemChanges);
            bool generateDownloadUrls = false;
            bool slotMode = false;
            bool sortAscending = false;
            bool wantShortcutForLargeRepository = (version > 20000);
            bool wantQueryPartial = wantShortcutForLargeRepository;
            bool wantQueryFull = false;
            if (wantQueryPartial)
            {
                // Have the perhaps optimistic hope that there was a commit done
                // in the last 10% of most recent versions.
                // If such a shortcut for a *very* large range fails, then it's no problem
                // since we had only that percentage additionally, for our request wait time.
                //
                // [no need to manually ensure that versionFrom ends up >= 1 here - input values are much larger anyway]
                int versionFrom = ((versionTo * 9) / 10);
                LogItem logQueryPartial_Newest = GetLogImpl(
                    itemName,
                    itemVersion,
                    versionFrom,
                    versionTo,
                    Recursion.Full,
                    1,
                    includeFiles,
                    generateDownloadUrls,
                    slotMode,
                    sortAscending);
                if (0 != logQueryPartial_Newest.History.Length)
                    logQueryPartial_Newest_history = logQueryPartial_Newest.History[0];
            }
            // Determine wantQueryFull desires:
            bool isFailedQueryPartial = (null == logQueryPartial_Newest_history);
            wantQueryFull = isFailedQueryPartial;
            // Have breakpointable syntax:
            if (!wantQueryFull)
            {
                int doVerificationPercentage = 2; // now reduced (prolonged testing was ok)
                wantQueryFull = GotRandom_percentage(doVerificationPercentage);
            }
            //wantQueryFull = true; // DEBUG_SITE

            if (wantQueryFull)
            {
                int versionFrom = 1;
                // SVNBRIDGE_WARNING_REF_RECURSION - additional comments:
                // the reason for specifying .Full here probably is to get a full history,
                // which ensures that the first entry (due to sort order)
                // does provide the *newest* Change(set) anywhere below that item.
                LogItem logQueryAll_Newest = GetLogImpl(
                    itemName,
                    itemVersion,
                    versionFrom,
                    versionTo,
                    Recursion.Full,
                    1,
                    includeFiles,
                    generateDownloadUrls,
                    slotMode,
                    sortAscending);
                if (0 != logQueryAll_Newest.History.Length)
                    logQueryAll_Newest_history = logQueryAll_Newest.History[0];
            }
            // Verification step:
            bool canCompareResults = false;
            //if (wantQueryPartial && wantQueryFull) // not needed
            if (true)
            {
                canCompareResults = ((null != logQueryPartial_Newest_history) && (null != logQueryAll_Newest_history));
            }
            if (canCompareResults)
            {
                // [performance: comparison of less complex objects first!]
                bool isMatch = (
                    (logQueryPartial_Newest_history.ChangeSetID == logQueryAll_Newest_history.ChangeSetID) &&
                    (logQueryPartial_Newest_history.CommitDateTime == logQueryAll_Newest_history.CommitDateTime)
                );
                if (!(isMatch))
                {
                    ReportErrorMostRecentChangesetQueryMismatch(
                        logQueryPartial_Newest_history,
                        logQueryAll_Newest_history);
                }
            }

            SourceItemHistory logQueryResult_Newest_history = (null != logQueryPartial_Newest_history) ? logQueryPartial_Newest_history : logQueryAll_Newest_history;
            if (null != logQueryResult_Newest_history)
            {
                changeChangeSetID = logQueryResult_Newest_history.ChangeSetID;
                changeCommitDateTime = logQueryResult_Newest_history.CommitDateTime;
                return true;
            }
            else
            {
                changeChangeSetID = -1;
                changeCommitDateTime = DateTime.MinValue;
                return false;
            }
        }

        private static void ReportErrorMostRecentChangesetQueryMismatch(
            SourceItemHistory logQueryPartial,
            SourceItemHistory logQueryAll)
        {
            string logMessage = string.Format(
                "Mismatch: partial query data (rev {0}, date {1}) vs. full query data (rev {2}, date {3}), please report!",
                logQueryPartial.ChangeSetID, logQueryPartial.CommitDateTime,
                logQueryAll.ChangeSetID, logQueryAll.CommitDateTime);
            bool doThrowException = true;
            //doThrowException = false; // UNCOMMENT TO CONTINUE COLLECTING MISMATCHES
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

        private bool GotRandom_percentage(int percentage)
        {
            return debugRandomActivator.YieldTrueOnPercentageOfCalls(percentage);
        }

        private void UpdateFolderRevisions(ItemMetaData item, int version, Recursion recursion)
        {
            // Make sure to do all DateTime comparisons with
            // affected values already drawn into (== normalized to) UTC space!!
            // (which should always be done that way in data model layer handling,
            // as opposed to non-UTC-based [since human-based]
            // presentation layer stuff)

            if (item != null && item.ItemType == ItemType.Folder)
            {
                FolderMetaData folder = (FolderMetaData)item;
                foreach (ItemMetaData folderItem in folder.Items)
                {
                    UpdateFolderRevisions(folderItem, version, recursion);
                }
                if (recursion == Recursion.Full)
                {
                    int maxChangeset = int.MinValue;
                    DateTime maxLastModifiedUTC = DateTime.MinValue /* .ToUniversalTime(); MSDN: "The value of this constant is equivalent to 00:00:00.0000000 UTC" */;

                    foreach (ItemMetaData folderItem in folder.Items)
                    {
                        if (maxChangeset < folderItem.Revision)
                            maxChangeset = folderItem.Revision;

                        DateTime folderLastModifiedUTC = folderItem.LastModifiedDate.ToUniversalTime();
                        if (maxLastModifiedUTC < folderLastModifiedUTC)
                            maxLastModifiedUTC = folderLastModifiedUTC;
                    }
                    // Hmm... is this syntax mismatch (ItemRevision vs. SubItemRevision) intended here?
                    if (item.ItemRevision < maxChangeset)
                        item.SubItemRevision = maxChangeset;

                    if (item.LastModifiedDate.ToUniversalTime() < maxLastModifiedUTC)
                        item.LastModifiedDate = maxLastModifiedUTC;
                }
                else
                {
                    int changeChangeSetID;
                    DateTime changeCommitDateTime;
                    bool determinedMostRecentChangeset = DetermineMostRecentChangesetInTree(
                        item,
                        version,
                        out changeChangeSetID,
                        out changeCommitDateTime);
                    if (determinedMostRecentChangeset)
                    {
                        item.SubItemRevision = changeChangeSetID;
                        item.LastModifiedDate = changeCommitDateTime;
                    }
                }
            }
        }

        // These public-interface property file system helpers,
        // while not necessarily being a precise part of this class,
        // probably ought to remain here since the provider class
        // is the main SCM reference for other parts of the library,
        // and, most importantly, since the specific property storage method used
        // is an internal implementation detail of the provider interface,
        // thus it is the one responsible for offering this info.
        // Also, kept them non-static since otherwise
        // one would have to invoke them using the specific class *type*
        // rather than any flexibly-assigned class *object*,
        // which is an abstraction handling inconvenience that I would not want.
        public bool IsPropertyFile(string path)
        {
            return WebDAVPropertyStorageAdaptor.IsPropertyFileType(path);
        }

        public bool IsPropertyFolder(string path)
        {
            return WebDAVPropertyStorageAdaptor.IsPropertyFolderType(path);
        }

        public bool IsPropertyFolderElement(string path)
        {
            return WebDAVPropertyStorageAdaptor.IsPropertyFolderElement(path);
        }

        private static bool HaveActivity_Deletion(string activityId, string path)
        {
            bool result = false;
            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                if (activity.DeletedItems.Contains(path))
                {
                    result = true;
                }
            });
            return result;
        }

        private void RevertDelete(string activityId, string path)
        {
            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                string serverItemPath = MakeTfsPath(path);
                ServiceUndoPendingRequests(activityId, new string[] { serverItemPath });
                activity.DeletedItems.Remove(path);
                //for (int j = activity.MergeList.Count - 1; j >= 0; j--)
                //{
                //    if (activity.MergeList[j].Action == ActivityItemAction.Deleted
                //        && activity.MergeList[j].Path.Equals(serverItemPath))
                //    {
                //        activity.MergeList.RemoveAt(j);
                //    }
                //}
                activity.MergeList.RemoveAll(
                  elem => (elem.Action == ActivityItemAction.Deleted) && (elem.Path.Equals(serverItemPath))
                );
            });
        }

        private static bool MergeResponse_ShouldBeIgnored(string itemPath)
        {
            return WebDAVPropertyStorageAdaptor.IsPropertyFolderType(itemPath);
        }

        private static void MergeResponse_OverrideItem(ref ActivityItem item)
        {
            bool needOverride = false;
            string itemPath = item.Path;
            ItemType itemType = item.FileType;

            bool isPropertyStorageItem = WebDAVPropertyStorageAdaptor.IsPropertyStorageItem(ref itemPath, ref itemType);
            if (isPropertyStorageItem)
            {
                needOverride = true;
            }

            if (needOverride)
            {
                item = new ActivityItem(itemPath, itemType, item.Action);
            }
        }

        private void MergeResponse_GatherEntries(string activityId, MergeActivityResponse mergeResponse)
        {
            List<string> baseFolders = new List<string>();
            List<string> sortedMergeResponse = new List<string>();
            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                foreach (ActivityItem item in activity.MergeList)
                {
                    if (MergeResponse_ShouldBeIgnored(item.Path))
                    {
                        continue;
                    }

                    ActivityItem newItem = item;

                    MergeResponse_OverrideItem(ref newItem);

                    bool isAlreadyListed = sortedMergeResponse.Contains(newItem.Path);
                    if (!isAlreadyListed)
                    {
                        sortedMergeResponse.Add(newItem.Path);

                        string path = newItem.Path.Substring(rootPath.Length - 1);
                        if (path.Equals(""))
                            path = "/";

                        if (newItem.Action != ActivityItemAction.Deleted && newItem.Action != ActivityItemAction.Branch &&
                            newItem.Action != ActivityItemAction.RenameDelete)
                        {
                            MergeActivityResponseItem responseItem =
                                new MergeActivityResponseItem(newItem.FileType, path);
                            mergeResponse.Items.Add(responseItem);
                        }

                        bool mayNeedBaseFolder =
                            (newItem.Action == ActivityItemAction.New) || (newItem.Action == ActivityItemAction.Deleted) ||
                            (newItem.Action == ActivityItemAction.RenameDelete);

                        if (mayNeedBaseFolder)
                        {
                            MergeResponse_AddBaseFolderIfRequired(activityId, newItem, baseFolders, mergeResponse);
                        }
                    }
                }
            });
        }

        private void MergeResponse_AddBaseFolderIfRequired(string activityId, ActivityItem item, ICollection<string> baseFolders, MergeActivityResponse mergeResponse)
        {
            string folderName = FilesysHelpers.GetFolderPathPart(item.Path);
            if (!baseFolders.Contains(folderName))
            {
                baseFolders.Add(folderName);
                bool folderFound = false;

                ActivityRepository.Use(activityId, delegate(Activity activity)
                {
                    foreach (ActivityItem folderItem in activity.MergeList)
                    {
                        if (folderItem.FileType == ItemType.Folder && folderItem.Path.Equals(folderName))
                        {
                            folderFound = true;
                            break;
                        }
                    }
                });

                if (!folderFound)
                {
                    folderName = FilesysHelpers.GetFolderPathPart(item.Path.Substring(rootPath.Length));
                    if (!folderName.StartsWith("/"))
                        folderName = "/" + folderName;
                    MergeActivityResponseItem responseItem = new MergeActivityResponseItem(ItemType.Folder, folderName);
                    mergeResponse.Items.Add(responseItem);
                }
            }
        }

        public bool WriteFile(string activityId, string path, byte[] fileData, bool reportUpdatedFile)
        {
            bool isNewFile = true;

            bool replaced = false;
            // Combo of prior deleteAction plus Write ends up as replace:
            if (HaveActivity_Deletion(activityId, path))
            {
                replaced = true;
                RevertDelete(activityId, path);
            }

            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                ItemMetaData folderExistingBase_HEAD = DetermineOutermostExistingBaseDirectoryItem(path, true);
                string localPath = GetLocalPath(activityId, path);
                string localBasePath = localPath.Substring(0, localPath.LastIndexOf('\\'));
                List<LocalUpdate> updates = new List<LocalUpdate>();
                updates.Add(LocalUpdate.FromLocal(folderExistingBase_HEAD.Id,
                                                  localBasePath,
                                                  folderExistingBase_HEAD.Revision));

                bool needAuthorshipLookup = false; // correct, right?
                ItemMetaData itemExisting_HEAD = GetItems(
                    LATEST_VERSION,
                    path.Substring(1),
                    Recursion.None,
                    true,
                    needAuthorshipLookup);
                if (itemExisting_HEAD != null)
                {
                    updates.Add(LocalUpdate.FromLocal(itemExisting_HEAD.Id, localPath, itemExisting_HEAD.Revision));
                }

                ServiceUpdateLocalVersions(activityId, updates);

                List<PendRequest> pendRequests = new List<PendRequest>();
                if (itemExisting_HEAD != null)
                {
                    pendRequests.Add(PendRequest.Edit(localPath));
                    isNewFile = false;
                }
                else
                {
                    ItemMetaData pendingItem = GetPendingItem(activityId, path);
                    if (pendingItem == null)
                    {
                        pendRequests.Add(PendRequest.AddFile(localPath, TfsUtil.CodePage_ANSI));
                    }
                    else
                    {
                        UpdateLocalVersion(activityId, pendingItem, localPath);
                        pendRequests.Add(PendRequest.Edit(localPath));
                        isNewFile = false;
                    }
                }
                ServiceSubmitPendingRequests(activityId, pendRequests);

                string pathFile = MakeTfsPath(path);
                sourceControlService.UploadFileFromBytes(serverUrl, credentials, activityId, fileData, pathFile);

                bool addToMergeList;
                if (null != itemExisting_HEAD)
                {
                    addToMergeList = true;
                }
                else
                {
                    addToMergeList = true;
                    foreach (CopyAction copy in activity.CopiedItems)
                    {
                        if (copy.TargetPath.Equals(path))
                        {
                            addToMergeList = false;
                            break;
                        }
                    }
                }

                if (addToMergeList)
                {
                    bool isUpdated = (!replaced && (!isNewFile || reportUpdatedFile));
                    ActivityItemAction item_action = isUpdated ? ActivityItemAction.Updated : ActivityItemAction.New;

                    activity.MergeList.Add(new ActivityItem(pathFile, ItemType.File, item_action));
                }
            });

            return isNewFile;
        }

        /// <summary>
        /// Given a path, figures out the item of the outermost existing
        /// base directory of that path, at HEAD revision (LATEST_VERSION).
        /// </summary>
        /// <param name="path"></param>
        /// <param name="returnPropertyFiles">
        /// whether to consider existing property storage filesystem items as well
        /// (i.e., this indicates whether the caller of this method
        /// may intend the result to be used for property storage purposes)
        /// </param>
        /// <returns>Base directory item</returns>
        private ItemMetaData DetermineOutermostExistingBaseDirectoryItem(string path, bool returnPropertyFiles)
        {
            ItemMetaData item;
            string existingPath = path.Substring(1);

            do
            {
                int lastIndexOf = existingPath.LastIndexOf('/');
                if (lastIndexOf != -1)
                    existingPath = existingPath.Substring(0, lastIndexOf);
                else
                    existingPath = "";

                if (returnPropertyFiles)
                    item = GetItems(LATEST_VERSION, existingPath, Recursion.None, true);
                else
                    item = GetItemsWithoutProperties(LATEST_VERSION, existingPath, Recursion.None);
                // We assume that loop abort condition is covered under all circumstances
                // (i.e., existingPath "" --> valid item, right? Famous last words...)
            } while (item == null);
            return item;
        }

        private void UndoPendingRequests(string activityId, Activity activity, string path)
        {
            ServiceUndoPendingRequests(activityId,
                                       new string[] { path });
            //for (int i = activity.MergeList.Count - 1; i >= 0; i--)
            //{
            //    if (activity.MergeList[i].Path.Equals(path))
            //    {
            //        activity.MergeList.RemoveAt(i);
            //    }
            //}
            activity.MergeList.RemoveAll(elem => (elem.Path.Equals(path)));
        }

        private void ConvertCopyToRename(string activityId, CopyAction copy)
        {
            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                string pathTargetFull = MakeTfsPath(copy.TargetPath);
                UndoPendingRequests(activityId, activity, pathTargetFull);

                ProcessCopyItem(activityId, LATEST_VERSION, copy, true);
            });
        }

        /// <summary>
        /// Returns a full TFS path (combination of rootPath plus the item's sub path).
        /// </summary>
        /// <param name="itemPath">sub path of the item</param>
        /// <returns>combined/full TFS path to item</returns>
        private string MakeTfsPath(string itemPath)
        {
            return FilesysHelpers.PathPrefix_Checked_Prepend(rootPath, itemPath);
        }

        private static string GetLocalPath(string activityId, string path)
        {
            return Constants.LocalPrefix + activityId + path.Replace('/', '\\');
        }

        private void UpdateLocalVersion(string activityId, ItemMetaData item, string localPath)
        {
            UpdateLocalVersion(activityId, item.Id, item.ItemRevision, localPath);
        }

        private void UpdateLocalVersion(string activityId, int itemId, int itemRevision, string localPath)
        {
            List<LocalUpdate> updates = new List<LocalUpdate>(1);
            updates.Add(LocalUpdate.FromLocal(itemId, localPath, itemRevision));
            ServiceUpdateLocalVersions(activityId, updates);
        }

        /// <summary>
        /// TFS API docs sez:
        /// "Called to update the local version of an item which is stored for a workspace.
        /// Clients should call this after successfully calling DownloadFile() based on instructions from Get()."
        /// </summary>
        /// <param name="activityId">ID of an activity (transaction)</param>
        /// <param name="updates">The updates to be applied</param>
        private void ServiceUpdateLocalVersions(string activityId, IEnumerable<LocalUpdate> updates)
        {
            sourceControlService.UpdateLocalVersions(serverUrl, credentials, activityId, updates);
        }

        private void ProcessCopyItem(string activityId, int versionFrom, CopyAction copyAction, bool forceRename)
        {
            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                string localPath = GetLocalPath(activityId, copyAction.Path);
                string localTargetPath = GetLocalPath(activityId, copyAction.TargetPath);

                bool copyIsRename = false;
                // Combo of prior deleteAction plus copyAction ends up as rename:
                if (HaveActivity_Deletion(activityId, copyAction.Path))
                {
                    copyIsRename = true;
                    RevertDelete(activityId, copyAction.Path);
                }
                ItemMetaData item = GetItemsWithoutProperties(LATEST_VERSION, copyAction.Path, Recursion.None);
                // NOTE: this method assumes that the source item to be copied from does exist at HEAD.
                // If that is not the case, then another outer method (write file, ...)
                // should have been chosen.
                UpdateLocalVersion(activityId, item, localPath);

                if (copyIsRename)
                {
                    activity.MergeList.Add(new ActivityItem(MakeTfsPath(copyAction.Path), item.ItemType, ActivityItemAction.RenameDelete));
                }

                if (!copyIsRename)
                {
                    foreach (CopyAction copy in activity.CopiedItems)
                    {
                        if (copyAction.Path.StartsWith(copy.Path + "/"))
                        {
                            string path = copy.TargetPath + copyAction.Path.Substring(copy.Path.Length);
                            for (int i = activity.DeletedItems.Count - 1; i >= 0; i--)
                            {
                                string activityDeletedItem_Current = activity.DeletedItems[i];
                                if (activityDeletedItem_Current == path)
                                {
                                    copyIsRename = true;

                                    string pathDeletedFull = MakeTfsPath(activityDeletedItem_Current);
                                    UndoPendingRequests(activityId, activity, pathDeletedFull);

                                    activity.DeletedItems.RemoveAt(i);

                                    localPath = GetLocalPath(activityId, path);
                                    ItemMetaData pendingItem = GetPendingItem(activityId, path);
                                    UpdateLocalVersion(activityId, pendingItem, localPath);
                                }
                            }
                        }
                    }
                }
                if (!copyIsRename)
                {
                    for (int i = activity.DeletedItems.Count - 1; i >= 0; i--)
                    {
                        string activityDeletedItem_Current = activity.DeletedItems[i];
                        if (copyAction.Path.StartsWith(activityDeletedItem_Current + "/"))
                        {
                            copyIsRename = true;
                            activity.PostCommitDeletedItems.Add(activityDeletedItem_Current);

                            string pathDeletedFull = MakeTfsPath(activityDeletedItem_Current);
                            UndoPendingRequests(activityId, activity, pathDeletedFull);

                            activity.DeletedItems.RemoveAt(i);
                        }
                    }
                }
                if (!copyIsRename)
                {
                    foreach (string deletedItem in activity.PostCommitDeletedItems)
                    {
                        if (copyAction.Path.StartsWith(deletedItem + "/"))
                        {
                            copyIsRename = true;
                            break;
                        }
                    }
                }
                // Finally, check whether localPath vs. localTargetPath has a case mismatch only.
                // If so, that copy needs to be a rename, too, since on case insensitive systems
                // only one case variant can occupy the target place,
                // i.e. duplication caused by a COPY would be a problem.
                // Hmm, should we be using the ConvertCopyToRename() helper here?
                if (!copyIsRename)
                {
                    if (ItemMetaData.IsSamePathCaseInsensitive(localPath, localTargetPath))
                        copyIsRename = true;
                }

                PendRequest pendRequest = null;
                PendRequest pendRequestPending = null;
                if (copyIsRename || forceRename)
                {
                    PendRequest pendRequestRename = PendRequest.Rename(localPath, localTargetPath);
                    bool targetFreeForTheTaking = HaveActivity_Deletion(activityId, copyAction.TargetPath);
                    if (targetFreeForTheTaking)
                    {
                        activity.PendingRenames[localTargetPath] = pendRequestRename;
                    }
                    else
                    {
                        pendRequest = pendRequestRename;
                        if (activity.PendingRenames.TryGetValue(localPath, out pendRequestPending))
                        {
                            activity.PendingRenames.Remove(localPath);
                        }
                    }
                    copyAction.Rename = true;
                }
                else
                {
                    pendRequest = PendRequest.Copy(localPath, localTargetPath);
                }
                if (pendRequest != null)
                {
                    ServiceSubmitPendingRequest(activityId, pendRequest);
                    UpdateLocalVersion(activityId, item, localTargetPath);
                    if (pendRequestPending != null)
                    {
                        ServiceSubmitPendingRequest(activityId, pendRequestPending);
                    }
                }
                string pathCopyTarget = MakeTfsPath(copyAction.TargetPath);
                ActivityItem activityItem;
                if (copyAction.Rename)
                {
                    activityItem = new ActivityItem(pathCopyTarget, item.ItemType, ActivityItemAction.New);
                }
                else
                {
                    string pathCopySource = MakeTfsPath(copyAction.Path);
                    activityItem = new ActivityItem(pathCopyTarget, item.ItemType, ActivityItemAction.Branch,
                            pathCopySource);
                }
                activity.MergeList.Add(activityItem);
            });
        }

        private void ProcessDeleteItem(string activityId, string path)
        {
            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                string localPath = GetLocalPath(activityId, path);

                ItemMetaData itemVictim = GetItems(LATEST_VERSION, path, Recursion.None, true);
                if (itemVictim == null)
                {
                    itemVictim = GetPendingItem(activityId, path);
                }

                UpdateLocalVersion(activityId, itemVictim, localPath);

                // FIXME: is that actually correct!?
                // AFAICT folders do potentially have properties, too...
                bool itemMightHavePropertiesFile = (itemVictim.ItemType != ItemType.Folder);
                if (itemMightHavePropertiesFile)
                {
                    string propertiesFile = WebDAVPropertyStorageAdaptor.GetPropertiesFileName(path, itemVictim.ItemType);
                    DeleteItem(activityId, propertiesFile);
                }

                ServiceSubmitPendingRequest(activityId, PendRequest.Delete(localPath));

                activity.MergeList.Add(new ActivityItem(MakeTfsPath(path), itemVictim.ItemType, ActivityItemAction.Deleted));

            });
        }

        private ItemMetaData GetItemForItemProperties(string path, ItemType itemType, int version)
        {
            ItemMetaData itemForItemProperties;
            string propertiesPath = WebDAVPropertyStorageAdaptor.GetPropertiesFileName(path, itemType);
            string cacheKey = "ReadPropertiesForItem_" + propertiesPath;
            CachedResult cachedResult = cache.Get(cacheKey);

            if (cachedResult == null)
            {
                itemForItemProperties = GetItems(version, propertiesPath, Recursion.None, true);
                cache.Set(cacheKey, itemForItemProperties);
            }
            else
            {
                itemForItemProperties = (ItemMetaData)cachedResult.Value;
            }
            return itemForItemProperties;
        }

        /// <summary>
        /// Given a *data* item location, grabs the set of DAV properties
        /// that are stored somewhere else for that item.
        /// This is currently implemented by figuring out
        /// the corresponding internal *storage* item of its DAV properties,
        /// and then deserializing the properties from that storage-purposed file item.
        /// </summary>
        /// <param name="path">Location of data item</param>
        /// <param name="itemType">Type of data item (folder, file, ...)</param>
        /// <param name="version">Requested version of the data item</param>
        /// <returns>ItemProperties object providing all WebDAV properties that are set for this data item.</returns>
        private ItemProperties ReadDAVPropertiesForItem(string path, ItemType itemType, int version)
        {
            ItemProperties properties = null;

            bool isAllowedAccess = DAVPropertiesIsAllowedRead;
            // Keep this check located right before access layer implementation:
            if (!(isAllowedAccess))
            {
                return null;
            }

            {
                ItemMetaData itemForItemProperties = GetItemForItemProperties(path, itemType, version);

                if (itemForItemProperties != null)
                {
                    properties = WebDAVPropsSerializer.PropertiesRead(itemForItemProperties);
                }
            }
            return properties;
        }

        public ItemProperties ReadPropertiesForItem(ItemMetaData item)
        {
            return ReadDAVPropertiesForItem(item.Name, item.ItemType, item.ItemRevision);
        }

        private void UpdateProperties(string activityId)
        {
            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                foreach (string path in activity.Properties.Keys)
                {
                    DAVPropertiesChanges propsChangesOfPath = activity.Properties[path];
                    UpdateDAVPropertiesOfItem(activityId, activity, path, propsChangesOfPath);
                }
            });
        }

        private void UpdateDAVPropertiesOfItem(string activityId, Activity activity, string path, DAVPropertiesChanges propsChangesOfPath)
        {
            // NOTE: I believe the prior item prop implementation here to have been highly buggy:
            // - did an **AddRange()** to *existing* properties.Properties, of propertiesToAdd
            //   that the prior properties.Properties content has already been added to!!
            // - propertiesToAdd got instantiated *out-of-loop*, whereas most likely it's
            //   supposed to be per-item only values, *within-loop*.

            bool isAllowedAccess = DAVPropertiesIsAllowedWrite;
            // Keep this check located right before access layer implementation:
            if (!(isAllowedAccess))
            {
                return;
            }

            ItemMetaData item;
            ItemType itemType;

            ItemProperties priorItemProperties = GetItemProperties(activity, path, out item, out itemType);
            ItemProperties newItemProperties = CalculateNewSetOfDAVProperties(priorItemProperties, propsChangesOfPath);

            string propertiesPath = WebDAVPropertyStorageAdaptor.GetPropertiesFileName(path, itemType);
            string propertiesFolder = WebDAVPropertyStorageAdaptor.GetPropertiesFolderName(path, itemType);
            ItemMetaData propertiesFolderItem = GetItems(LATEST_VERSION, propertiesFolder, Recursion.None, true);
            if ((propertiesFolderItem == null) && !activity.Collections.Contains(propertiesFolder))
            {
                MakeCollection(activityId, propertiesFolder);
            }

            bool reportUpdatedFile = (null != item);
            WebDAVPropsSerializer.PropertiesWrite(activityId, propertiesPath, newItemProperties, reportUpdatedFile);
        }

        private bool DAVPropertiesIsAllowedRead
        {
            get
            {
                return this.davPropertiesIsAllowedRead;
            }
        }

        /// <summary>
        /// Helper to indicate whether writing of DAV property storage data is allowed.
        /// </summary>
        /// <remarks>
        /// See description at config layer.
        /// </remarks>
        private bool DAVPropertiesIsAllowedWrite
        {
            get
            {
                return this.davPropertiesIsAllowedWrite;
            }
        }

        private static ItemProperties CalculateNewSetOfDAVProperties(ItemProperties priorProperties, DAVPropertiesChanges propsChangesOfPath)
        {
            ItemProperties newProperties = new ItemProperties();

            Dictionary<string, Property> newSetOfProperties = new Dictionary<string, Property>();

            // In order to calculate the new total set of properties for an item:
            // first take into account the existing/old/prior properties...
            foreach (Property priorProperty in priorProperties.Properties)
            {
                newSetOfProperties[priorProperty.Name] = priorProperty;
            }
            // ...then add the Added ones...
            foreach (KeyValuePair<string, string> addedProperty in propsChangesOfPath.Added)
            {
                newSetOfProperties[addedProperty.Key] = new Property(addedProperty.Key, addedProperty.Value);
            }
            // ...and it's probably best to *then* remove the Removed ones
            // (XXX - or perhaps it *should* be Remove *prior* to Add?
            // - the props mechanism might be such
            // that we end up having an *updated* value to be added
            // with an *outdated* value to be removed!!):
            foreach (string removedProperty in propsChangesOfPath.Removed)
            {
                newSetOfProperties.Remove(removedProperty);
            }
            newProperties.Properties = newSetOfProperties.Values.ToList();

            return newProperties;
        }


        /// <summary>
        /// Has some unorthodox itemType handling since it needs
        /// to correctly discern between items and item-less WebDAV collections. Ugh.
        /// Perhaps there's a way to cleanup things, but...
        /// </summary>
        /// <param name="activity">Current activity (transaction)</param>
        /// <param name="path">path of the item</param>
        /// <param name="item">returns the item residing at that path, if available</param>
        /// <param name="itemType">returns the type of item (file, folder)</param>
        /// <returns>List of Subversion properties of an item</returns>
        private ItemProperties GetItemProperties(Activity activity, string path, out ItemMetaData item, out ItemType itemType)
        {
            int version = LATEST_VERSION;
            itemType = ItemType.File;
            item = GetItems(version, path, Recursion.None);
            if (item != null)
            {
                itemType = item.ItemType;
            }
            else if (activity.Collections.Contains(path))
            {
                itemType = ItemType.Folder;
            }

            ItemProperties properties = ReadDAVPropertiesForItem(path, itemType, version);
            if (properties == null)
            {
                properties = new ItemProperties();
            }
            return properties;
        }

        /// <summary>
        /// Uses QueryItemsExtended() (workspace-enhanced variant of QueryItems())
        /// to query an item that has been registered as pending
        /// within the current {activity|workspace|transaction}.
        /// </summary>
        /// <param name="activityId">ID of an activity (transaction)</param>
        /// <param name="path">Path to be queried</param>
        /// <returns>Meta data of the item, else null</returns>
        private ItemMetaData GetPendingItem(string activityId, string path)
        {
            ItemSpec spec = new ItemSpec { item = MakeTfsPath(path), recurse = RecursionType.None };
            ExtendedItem[][] items =
                sourceControlService.QueryItemsExtended(serverUrl,
                                                        credentials,
                                                        activityId,
                                                        new ItemSpec[1] { spec },
                                                        DeletedState.NonDeleted,
                                                        ItemType.Any,
                                                        0);
            if (items[0].Length == 0)
                return null;
            ItemMetaData pendingItem;
            if (items[0][0].type == ItemType.Folder)
            {
                pendingItem = new FolderMetaData();
            }
            else
            {
                pendingItem = new ItemMetaData();
            }

            pendingItem.Id = items[0][0].itemid;
            pendingItem.ItemRevision = items[0][0].latest;
            return pendingItem;
        }

        public virtual ItemMetaData[] GetPreviousVersionOfItems(SourceItem[] items, int changeset_Newer)
        {
            ItemMetaData[] result;

            SourceItem[] itemsPrev = QueryPreviousVersionOfSourceItems(
                items,
                changeset_Newer);

            result = itemsPrev.Select(sourceItem => (null != sourceItem) ? SCMHelpers.ConvertSourceItem(sourceItem, rootPath, SCMHelpers.UnknownAuthorMarker) : null).ToArray();

            return result;
        }

        /// <summary>
        /// Helper to figure out the previous version of a list of source items,
        /// properly symmetric within the same implementation layer!
        /// (*from* SourceItem-typed input *to* SourceItem-typed output).
        /// Returns a *same-size*/*same-index* (potentially using NULL filler entries) array.
        /// Need to ensure that items returned are same-tree filesystem items
        /// (i.e., returning item results belonging to a different Team Project
        /// - e.g. due to them having been merged into the local one at some point - would be illegal).
        /// Not entirely sure whether this foreign-TP removal filtering ought to be done by this _TFS_SCP class,
        /// but it definitely needs to be done somewhere prominently.
        /// WARNING: QueryItems() given an input of an array of IDs returns a result list with *jumbled order*
        /// (order of IDs may end up wrong).
        /// </summary>
        ///
        /// References:
        /// http://stackoverflow.com/questions/8946508/tfs-2010-api-get-old-name-location-of-renamed-moved-item
        ///
        /// And of course this method is required to return an accurately index-matching result list... Argh.
        /// (need to account for this by doing proper sorting).
        /// Note that I'm still very unsure of this method's reliability for all of the various use cases.
        /// I fixed handling to try to account for a rather pathological case:
        /// changeset 1: create test directory with test sub dir
        /// changeset 2: branch foreign-TeamProject content into test sub dir
        /// changeset 3: rename test sub dir
        /// (it's best to try this in a small test TP)
        ///
        /// FIXME: note that I'm not sure any more whether that code is suitable for non-TFS08 servers
        /// since it was completely reworked using a TFS08. And TFS10 *is* different, so there may be trouble.
        /// <param name="items">List of items to be queried</param>
        /// <param name="changeset_Newer">The changeset that is newer than the result that we're supposed to determine</param>
        /// <returns>Container of items at the older changeset revision</returns>
        private SourceItem[] QueryPreviousVersionOfSourceItems(
            SourceItem[] items,
            int changeset_Newer)
        {
            SourceItem[] result;

            // Processing steps:
            // - given the this-changeset source items,
            //   figure out the corresponding maximally-authoritative representation (numeric IDs) of these items
            // - do a QueryItems() with these IDs, on the *previous* changeset

            BranchItem[] renamedItems = GetRenamedItems(items, changeset_Newer);

            var numRenamedItems = renamedItems.Length;

            var previousRevision = changeset_Newer - 1;

            // Rather than the prior hard if/else skipping of branches,
            // we'll now do handling of TFS08/multi/batched right in the very same code flow,
            // to gain the capability of dynamically choosing (via configurable high-level bools)
            // which branch to actually add to the execution.
            // The reason for that is that I'm entirely unsure about the reasoning/differences
            // between the <=TFS08 implementation and OTOH the multi/batched stuff
            // (and currently there's a data mismatch of members in case of a RenamedSourceItem
            // vs. the results queried here!),
            // thus debugging needs to be very easy, to finally gain sufficient insight.
            SourceItem[] resultTFS08Fallback = null;
            SourceItem[] resultMulti = null;
            SourceItem[] resultBatched = null;

            // What *exactly* is the significance of this check? Rename/comment variable as needed...
            bool isAllItemsWithNullContent = (renamedItems.All(item => (null == item) || (null == item.FromItem)));
            bool needTfs08FallbackAlgo = isAllItemsWithNullContent;

            // I believe (while working on TFS08) that the handling done by the TFS08 branch below is buggy
            // (as determined by mismatch checks triggering in UpdateDiffEngine.Rename()),
            // thus we'll simply decide to skip use of any of its results.
            needTfs08FallbackAlgo = false;

            bool wantTfs08FallbackAlgo = needTfs08FallbackAlgo;

            bool wantMultiRequestMode = false;
            bool wantBatchedRequestMode = false;
            if (true != wantTfs08FallbackAlgo)
            {
                // Do old multiple-requests handling for low counts only
                // (avoid socket exceptions, happening after around the standard range of ~ 4000 ports [XP]).
                // Now decreased activation condition to relatively few items
                // (no comparison mismatches turned up,
                // thus it's wasteful to have hundreds of web service requests).
                wantMultiRequestMode = (numRenamedItems < 50);
                wantBatchedRequestMode = true;
            }
            bool wantDebugResults = false;
            //wantDebugResults = true; // DEBUG_SITE: UNCOMMENT IF DESIRED (or simply ad-hoc toggle var in debugger)
            if (wantDebugResults)
            {
                wantTfs08FallbackAlgo = true;
                wantMultiRequestMode = true;
                wantBatchedRequestMode = true;
            }

            if (wantTfs08FallbackAlgo)
            {
                // fallback for TFS08 and earlier
                var previousSourceItemIds = items.Select(item => item.ItemId).ToArray();
                resultTFS08Fallback = metaDataRepository.QueryItems(
                    previousRevision,
                    previousSourceItemIds);
            }
            // This multi-request style is O(n) as opposed to ~ O(1), network-wise
            // (and network-side processing complexity is all that matters!),
            // thus it's prone to socket exhaustion exceptions and terrible performance.
            if (wantMultiRequestMode)
            {
                List<SourceItem> resultMulti_List = new List<SourceItem>(numRenamedItems);
                for (var i = 0; i < numRenamedItems; i++)
                {
                    var renamedItem = renamedItems[i];
                    var previousSourceItemId = GetItemIdOfRenamedItem(renamedItem, items[i]);
                    var previousSourceItems = metaDataRepository.QueryItems(
                        previousRevision,
                        previousSourceItemId
                    );
                    // Yes, do actively append this slot even if no result
                    // (caller requires index-consistent behaviour of input vs. result storage)
                    resultMulti_List.Add(previousSourceItems.Length > 0 ? previousSourceItems[0] : null);
                }
                resultMulti = resultMulti_List.ToArray();
            }
            if (wantBatchedRequestMode)
            {
                resultBatched = QueryPreviousVersionOfSourceItems_batched(items, renamedItems, previousRevision);
            }

            if (wantMultiRequestMode && wantBatchedRequestMode)
            {
                // Implement some nice result comparison:
                // Throw exception in case old and new results don't match!
                // For some reason trying to simply use .SequenceEqual() fails,
                // despite all members seemingly being equal
                // (possibly because the items *are* different instantiations).
                // http://stackoverflow.com/questions/4423318/how-to-compare-arrays-in-c
                // XXX: old multi-request handling and this check
                // can be removed at some future moment
                // in case results did not turn out problematic for a while
                // (for a rather loooong while, that is...).
                int resultCount = Math.Min(resultBatched.Length, resultMulti.Length);
                bool isMatch = (resultBatched.Length == resultCount);
                if (isMatch) // important: definitely don't proceed with comparison then (potential out-of-bounds e.g. on zero resultBatched.Count()!)
                {
                    for (int i = 0; i < resultCount; ++i)
                    {
                        var batch = resultBatched[i];
                        var multi = resultMulti[i];
                        // Definitely avoid null object access!
                        // And prefer ReferenceEquals(): http://stackoverflow.com/a/10104842
                        // http://stackoverflow.com/questions/155458/c-sharp-object-is-not-null-but-myobject-null-still-return-false
                        // http://stackoverflow.com/questions/6417902/checking-if-object-is-null-in-c-sharp
                        bool isNullBatch = Object.ReferenceEquals(null, batch);
                        bool isNullMulti = Object.ReferenceEquals(null, multi);
                        if (isNullBatch || isNullMulti)
                        {
                            // Special check via bool-null true/false (avoid check via object comparison operators)
                            if (isNullBatch != isNullMulti)
                            {
                                isMatch = false;
                            }
                        }
                        else
                        if (batch.ItemId != multi.ItemId)
                        {
                            isMatch = false;
                        }
                        if (!(isMatch))
                        {
                            break;
                        }
                    }
                }
                if (!(isMatch))
                {
                    throw new InvalidOperationException("ERROR: old multi-request and new batched-request lists DO NOT MATCH, please report!!");
                }
            }
            result = needTfs08FallbackAlgo ? resultTFS08Fallback : resultBatched;

            return result;
        }

        private SourceItem[] QueryPreviousVersionOfSourceItems_batched(SourceItem[] items, BranchItem[] renamedItems, int previousRevision)
        {
            SourceItem[] result;

            var numRenamedItems = renamedItems.Length;
            List<SourceItem> resultBatched_List = new List<SourceItem>(numRenamedItems);
            // the number of rename reports which we'll request per web service request
            // We'll choose a sufficiently but not overly large number (avoid server DoS).
            // Perhaps we should increase that number (to minimize network requests:
            // socket exhaustion exception occurring too frequently,
            // and latency is not nice either).
            // Hmm, the best idea probably is to simply make use of the same number
            // as the TFS_QUERY_LIMIT (256) which is imposed by TFS QueryHistory() API,
            // since processing circumstances here might happen to be in the same ballpark:
            const int batchSize = 256;
            for (var iterBatchBase = 0; iterBatchBase < numRenamedItems; iterBatchBase += batchSize)
            {
                SourceItem[] resultThisBatch = null;
                {
                    int numRemaining = (numRenamedItems - iterBatchBase);
                    int numThisTime = Math.Min(batchSize, numRemaining);
                    List<int> previousSourceItemIds = new List<int>(numThisTime);

                    var iterBatchEnd = iterBatchBase + numThisTime;
                    // Performance WARNING: in execution time terms,
                    // this iteration can end up astonishingly longer than the actual web request!
                    // Thus prefer operator[] rather than .ElementAt()
                    // (please note that this does indeed make a dramatic execution difference).
                    for (var i = iterBatchBase; i < iterBatchEnd; ++i)
                    {
                        var renamedItem = renamedItems[i];
                        var previousSourceItemId = GetItemIdOfRenamedItem(renamedItem, items[i]);
                        previousSourceItemIds.Add(previousSourceItemId);
                    }

                    var arrPreviousSourceItemIds = previousSourceItemIds.ToArray();
                    resultThisBatch = QueryItems_WithTfsLibraryJumbledOrderSanitized(arrPreviousSourceItemIds, previousRevision);

                    // IMPORTANT NOTE: examples kept there for future debugging/development purposes:
                    //var previousItemsForQueryBranches = previousSourceItems.Select(item => CreateItemSpec(item.RemoteName, RecursionType.None)).ToArray();
                    //var previousSourceItemsEx = sourceControlService.QueryItemsExtended(serverUrl, credentials, null, previousItemsForQueryBranches, DeletedState.Any, ItemType.Any);
                    //BranchRelative[][] previousRevBranchesRel = sourceControlService.QueryBranches(serverUrl, credentials, null, previousItemsForQueryBranches, VersionSpec.FromChangeset(previousRevision));
                    //var previousRevBranches = sourceControlService.QueryBranches(serverUrl, credentials, previousItemsForQueryBranches, VersionSpec.FromChangeset(previousRevision));
                    //var parentPreviousRevBranches = sourceControlService.QueryBranches(serverUrl, credentials, previousItemsForQueryBranches, VersionSpec.FromChangeset(previousRevision - 1));

                    bool isMatchingCounts = (resultThisBatch.Length == previousSourceItemIds.Count());
                    if (!isMatchingCounts)
                    {
                        throw new InvalidOperationException("ERROR: counts of input and result renamedItems lists DO NOT MATCH, please report!!");
                    }
                }
                resultBatched_List.AddRange(resultThisBatch.ToList());
            }
            result = resultBatched_List.ToArray();

            return result;
        }

        private BranchItem[] GetRenamedItems(SourceItem[] items, int changeset)
        {
            BranchItem[] renamedItems;

            {
                var itemSpecs = items.Select(item => CreateItemSpec(MakeTfsPath(item.RemoteName), RecursionType.None)).ToArray();
                ChangesetVersionSpec versionSpecChangeset = VersionSpec.FromChangeset(changeset);
                BranchItem[][] thisRevBranches;
                {
                    // FIXME: I'm totally in the dark about the reason for doing QueryBranches()/renamedItems.
                    // Possibly this is required for a different constellation. Please comment properly
                    // once it's known what this is for.
                    // FIXME_PERFORMANCE: QueryBranches() is very slow!
                    // (note that behaviour of this web service request delay seems to be linear:
                    // about 1 second per 100 items
                    // in the raw [uncooked / uncorrected] direct request case).
                    // Note that some elements may end up null; known reasons so far:
                    // - renamed item already had "deleted" state
                    //   (one such situation may be one where the item's containing folder gets renamed).
                    // - item occurring in this changeset is an *interim* location
                    //   (e.g. whole-hierarchy Rename:d from one location to another,
                    //   then parts of that hierarchy immediately Delete:d within-same-changeset
                    //   --> there *is* no existing item at this Changeset
                    //   [however a Rename | Delete change *does* get listed for this location in this changeset!],
                    //   which probably is the reason that QueryBranches() yields null)
                    thisRevBranches = sourceControlService.QueryBranches(serverUrl,
                                                                         credentials,
                                                                         itemSpecs,
                                                                         versionSpecChangeset);
                }
                renamedItems = items.Select((item, i) =>
                    thisRevBranches[i].FirstOrDefault(branchItem =>
                        branchItem.ToItem != null &&
                        branchItem.ToItem.RemoteChangesetId == changeset &&
                        branchItem.ToItem.RemoteName == MakeTfsPath(item.RemoteName))).ToArray();

                // Do some special workarounds for unsuccessful elements -
                // or perhaps we should often simply skip QueryBranches() completely,
                // and directly go via history instead...
                GetRenamedItems_TryFixupUnsuccessfulElements(
                    ref renamedItems,
                    itemSpecs,
                    changeset);
            }

            return renamedItems;
        }

        private void GetRenamedItems_TryFixupUnsuccessfulElements(
            ref BranchItem[] renamedItems,
            ItemSpec[] itemSpecs,
            int changeset)
        {
            ChangesetVersionSpec versionSpecChangeset = VersionSpec.FromChangeset(changeset);
            bool isInterestedInItemChanges = true; // DEFINITELY yes!
            bool includeFiles = (isInterestedInItemChanges);
            bool generateDownloadUrls = false;
            bool slotMode = false;
            int idxItem = 0;
            foreach (var renamedItem in renamedItems)
            {
                bool haveProperRenameHistory = (null != renamedItem);
                if (!(haveProperRenameHistory))
                {
                    ItemSpec itemSpec = itemSpecs[idxItem];
                    var changesets = QueryChangesets_TFS_sanitize_querylimit_etc(
                        itemSpec.item,
                        versionSpecChangeset,
                        1,
                        changeset,
                        RecursionType.None,
                        int.MaxValue,
                        includeFiles,
                        generateDownloadUrls,
                        slotMode,
                        false);
                    var itemRenameDeterminedFromHistory = GetItemRenameDeterminedFromHistory(
                        changesets);
                    renamedItems[idxItem] = itemRenameDeterminedFromHistory;
                }
                ++idxItem;
            }
        }

        private static BranchItem GetItemRenameDeterminedFromHistory(
            List<Changeset> changesets)
        {
            BranchItem itemRenameDeterminedFromHistory = null;

            if (changesets.Count >= 2)
            {
                Change changeCurr = changesets[0].Changes[0];
                if (
                    ((changeCurr.type & ChangeType.Rename) == ChangeType.Rename)
                )
                {
                    Change changePrev = changesets[1].Changes[0];
                    var fromItem = ConvertChangeToSourceItem(changePrev);
                    var toItem = ConvertChangeToSourceItem(changeCurr);
                    bool isValidRename = true;
                    if (isValidRename)
                    {
                        itemRenameDeterminedFromHistory = TfsLibraryHelpers.ConstructBranchItem(
                            fromItem,
                            toItem);
                    }
                }
            }

            return itemRenameDeterminedFromHistory;
        }

        /// <summary>
        /// At least TFS2008 returns items in an order *different* from the order of IDs found in the array
        /// (UPDATE: in fact root cause seems to be
        /// a LAYER VIOLATION in CodePlex.TfsLibrary's QueryItems() code parts:
        /// it decides to apply a totally bogus and unhelpful Sort() -
        /// such awful mangling of payload data
        /// should only be done manually by certain user layers
        /// which for strange reasons have the constraint of requiring the result to be sorted).
        /// Thus add this helper, to apply a correction to this broken CodePlex library API.
        ///
        /// Perhaps it's not a good idea to intermingle the sorting-correction concern
        /// with the TFS query concern
        /// (implement this as an array-correction-only helper instead?).
        /// OTOH this is a quirk specific to ID-based QueryItems(), thus it's probably good to keep it combined.
        /// </summary>
        /// <param name="arrSourceItemIds">Array of source item IDs to be queried</param>
        /// <param name="revision">Revision to query the items at</param>
        /// <returns>SourceItem array containing the query results</returns>
        private SourceItem[] QueryItems_WithTfsLibraryJumbledOrderSanitized(int[] arrSourceItemIds, int revision)
        {
            var sourceItems_UnknownOrder = metaDataRepository.QueryItems(revision, arrSourceItemIds);

            // First shortcut: (almost) empty result? (--> no sorting necessary)
            // [this check could be taken over by SequenceEquals() below instead as well,
            // however that check below will require some pre-processing]
            if (sourceItems_UnknownOrder.Length <= 1)
            {
                // ...but ONLY IF request vs. result same-length!!
                // (avoid the case of result with empty-sized elements due to e.g. deleted items!
                // Else we'll have to do usual correction handling further below
                // to have corresponding null elements properly indicated
                // which thus manages to fulfill same-index guarantee...)
                bool bSameSize = (sourceItems_UnknownOrder.Length == arrSourceItemIds.Length);
                if (bSameSize)
                {
                    return sourceItems_UnknownOrder;
                }
            }

            // Second shortcut: same order?
            // (slightly more involved due to requiring some pre-processing,
            // thus we tried to prevent executing even this check)
            var sourceItemIds_Result = sourceItems_UnknownOrder.Select(item => item.ItemId);
            if (sourceItemIds_Result.SequenceEqual(arrSourceItemIds))
            {
                return sourceItems_UnknownOrder;
            }

            var itemsById = new Dictionary<int, SourceItem>(sourceItems_UnknownOrder.Length);
            foreach (SourceItem item in sourceItems_UnknownOrder)
            {
                if (null != item)
                {
                    itemsById[item.ItemId] = item;
                }
            }
            List<SourceItem> sourceItems_CorrectedOrder = new List<SourceItem>(arrSourceItemIds.Length);
            foreach(int id in arrSourceItemIds)
            {
                SourceItem item = null;
                itemsById.TryGetValue(id, out item);
                sourceItems_CorrectedOrder.Add(item);
            }
            return sourceItems_CorrectedOrder.ToArray();
        }

        /// <summary>
        /// Returns the ID of the item (the one *prior* to a rename/branching operation).
        /// </summary>
        /// <returns>Item ID</returns>
        static int GetItemIdOfRenamedItem(BranchItem renamedItem, SourceItem sourceItem)
        {
            // [[
            // I believe that the previous use of .FromItem was wrong: we need to use .ToItem
            // since we want to do a query with the *current* item ID, on the *previous* changeset.
            // Otherwise we will not get the correct path (would return foreign-TeamProject path
            // rather than the now-renamed one that had previously been branched into our TeamProject).
            // FIXME: *previous*SourceItemId thus most likely is now a misnomer.
            // ]]
            // NOPE, this change caused an issue around changeset 1356 in our main repo -
            // That file *was* merely renamed (not branched!), with its itemId thus changed to a new one.
            // This meant that when grabbing .ToItem.ItemId,
            // that *new* item ID then was not available at the *older* revision -->
            // null item result!!
            // (TODO: add further descriptions of evidence cases here).
            // Thus I'm afraid we'll have to revisit things
            // (in fact keep it as .FromItem, and add support code
            // to in the branching case then somehow derive the proper name).
            // And in fact renamedItem gets gathered
            // via a complex evaluation from a QueryBranches() call,
            // so perhaps that previous handling simply was wrong!?
            // (or not clever enough?)
            // UPDATE: yup, I have a hunch
            // that one may need to discern between
            // renames of items (criteria for detecting renames likely is
            //                   that .FromItem and .ToItem have *same* changeset value)
            // and
            // branches (/copies) (.ToItem will have new changeset value
            //                     which adopted a branch / copy of .FromItem - at its last changeset)
            // , so it's this difference between renames and branches
            // which probably plays a role here...
            return (renamedItem != null && renamedItem.FromItem != null) ? renamedItem.FromItem.ItemId : sourceItem.ItemId;
        }

        private static ItemSpec CreateItemSpec(string item, RecursionType recurse)
        {
            return new ItemSpec { item = item, recurse = recurse };
        }

        public virtual int GetEarliestVersion(string path)
        {
            bool isInterestedInItemChanges = false; // DEFINITELY not!
            bool includeFiles = (isInterestedInItemChanges);
            bool generateDownloadUrls = false;
            bool slotMode = false;
            bool sortAscending = false;
            LogItem log = GetLogImpl(
                path,
                LATEST_VERSION,
                1,
                GetLatestVersion(),
                Recursion.None,
                int.MaxValue,
                includeFiles,
                generateDownloadUrls,
                slotMode,
                sortAscending);
            return log.History[log.History.Length - 1].ChangeSetID;
        }

        // TODO: these helpers should perhaps eventually be moved
        // into a helper class (SourceControlSession?)
        // which encapsulates sourceControlService, serverUrl, credentials members,
        // as a member of this provider class,
        // thereby simplifying common invocations.
        private void ServiceSubmitPendingRequest(string activityId, PendRequest pendRequest)
        {
            List<PendRequest> pendRequests = new List<PendRequest>(1);
            pendRequests.Add(pendRequest);
            ServiceSubmitPendingRequests(activityId, pendRequests);
        }

        /// <summary>
        /// Will register (stage) file changes in our temporary TFS workspace
        /// as ready for commit.
        /// This is aka TFS Pending Changes,
        /// which can actually be seen in Visual Studio Source Control Explorer
        /// as filed in its proper SvnBridge-created temporary Workspace ID
        /// while debugging (but note that at least on VS10+TFS08,
        /// Workspace status gets refreshed rather very lazily -
        /// use Context Menu -> Refresh to force a refresh to current status).
        /// TFS error message in case Pending Changes were not done correctly:
        /// "The item" ... "could not be found in your workspace."
        ///
        /// This is a queueing-only operation - final atomic commit transaction
        /// of all these elements queued within this activity
        /// will then happen on invocation of .Commit().
        /// </summary>
        /// <param name="activityId">ID of the activity to file these requests under</param>
        /// <param name="pendRequests">list of pending requests (TFS Pending Changes)</param>
        private void ServiceSubmitPendingRequests(string activityId, IEnumerable<PendRequest> pendRequests)
        {
            // Watch all pending items submitted for the TFS-side transaction / workspace
            // during an entire SVN-side WebDAV
            // MKACTIVITY
            //   ... CHECKOUT / COPY / PUT / DELETE ...
            // DELETE
            // transaction lifecycle:
            Helper.DebugUsefulBreakpointLocation();

            // SAME-FOLDER DIFFERENT-CASE RENAME NOTES:
            // When submitting a case-only rename of a *folder*
            // (not *file* - that seems to work...),
            // TFS2013 will react with returning a Failure[] element
            // (in TfsLibrary SourceControlService.cs PendChangesHelper())
            // which indicates
            // ItemExistsException "The item $/...ToNewCaseRenamedFolder already exists.".
            // Unfortunately, no amount of fiddling with
            // pendChangesOptions / supportedFeatures params
            // (roughly documented at
            // Microsoft.TeamFoundation.VersionControl.Common
            // PendChangesOptions,
            // and judging from TFS errors
            // it seems supportedFeatures value needs to match pendChangesOptions)
            // helped (setting pendChangesOptions "silent"
            // managed to skip producing a Failure[] element,
            // however having a closer look at temporary SvnBridge workspace
            // in MSVS Source Control Explorer showed that there were
            // no pending changes registered, IOW it was [rightfully...]
            // "silently ignored").
            // So, while we now do have
            // tons of filesystem item case bug filter functionality in place,
            // it seems we're still partially stuck
            // since on the *commit* side of things (rather than working copy *update*)
            // TFS will not accept submitting a "slightly-different" item path
            // (even trying to submit a PendChange to different-name
            // and a subsequent one to case-renamed-name will be caught by TFS).
            // For certain controlled situations it might be ok
            // to do an interim commit
            // which actively renames the item away
            // and *then* renames it to the real case-only-renamed name.
            // Especially in order to support non-controlled situations
            // (think import of a large *pre-existing* commit history),
            // this could possibly also be provided as an automated mechanism
            // (also, opt-out via config flag) by SvnBridge,
            // by doing a "preparatory" commit which does the rename-away
            // (and contains both a "preparatory" explanation *plus*
            // the commit description of the following "real" commit),
            // then doing the real commit
            // which then also contains the rename-back-to-case-only-change.
            // "TFS – ItemExistsException Fix"
            //   http://www.donnfelker.com/2008/01/ says: "
            // Robert Horvick had some great insight into this error on this post. Here’s what he says:
            // “At a high level what is happening here is that there are two distinct items
            // which happen to have the same path name (at different points in time, obviously).
            // When you are labeling TFS sees what you want to put into the label (the new item)
            // and what is already in the old label (the old item) and sees the name conflict.
            // Since /child:replace means to replace the label on the same item
            // it can’t drop the label off the old item
            // which means it can’t add it to the new item so it issues this error message.“
            // THANK YOU Robert.
            // "
            // Interesting discussion related to git-tfs:
            // "TFS side folder upper/lower case renaming not handled properly"
            //   https://github.com/git-tfs/git-tfs/issues/104
            sourceControlService.PendChanges(
                serverUrl, credentials,
                activityId, pendRequests,
                0, 0
            );
        }

        /// <summary>
        /// Will undo file changes in our temporary TFS workspace
        /// for an item.
        /// Note that this unfortunately is limited to undoing
        /// *all* pending changes of a path,
        /// which might turn out to be a problem in case of
        /// *multiple* prior Pending Changes registered for one item.
        /// </summary>
        /// <param name="activityId">ID of the activity to file these requests under</param>
        /// <param name="pendRequests">list of items to have all their Pending Changes undone</param>
        private void ServiceUndoPendingRequests(string activityId, IEnumerable<string> serverItems)
        {
            sourceControlService.UndoPendingChanges(
                serverUrl, credentials,
                activityId, serverItems
            );
        }
    }

    /// <summary>
    /// Helper class to have an additional change layer
    /// (e.g. enables us to abstract away property changes from internal TFS-side property impl specifics).
    /// </summary>
    internal sealed class SourceItemChangeClassifier
    {
        public enum ChangeModifierType
        {
            AuthoritativeTFS, // this is a legal authoritative TFS entry which ought to be preserved (pass-through)
            PropEdit, // *invented* (non-TFS) change, to mark a data item as having experienced a WebDAV property change (SVN "Edit" change)
        }
        public readonly ChangeModifierType modifierType;
        // Warning: (ab-)using TfsLibrary SourceItemChange type here, for SVN purposes! -
        // should quite certainly introduce our own SVN-side corresponding classes here,
        // to achieve *clean* separation from TFS-side behaviour/specifics.
        public readonly SourceItemChange change;

        public SourceItemChangeClassifier(ChangeModifierType modifierType, SourceItemChange change)
        {
            this.modifierType = modifierType;
            this.change = change;
        }
    }

    /// <summary>
    /// Since I "hate" XML (c.f.
    /// https://en.wikiquote.org/wiki/Linus_Torvalds
    ///   "XML is crap. Really. There are no excuses. XML is nasty to
    ///    parse for humans, and it's a disaster to parse even for computers.
    ///    There's just no reason for that horrible crap to exist.")
    /// , I'm completely willing to spend extra effort
    /// to properly abstract the WebDAV properties'
    /// persistence/serialization/storage format away.
    /// While the property storage format is written in stone
    /// (many existing items containing this format)
    /// and thus quite likely should NEVER be changed
    /// at any point in future,
    /// the fact is that if we ever happen to still need to do it,
    /// then yes we scan.
    /// </summary>
    internal interface IWebDAVPropertySerializeFormatProvider
    {
        byte[] Serialize(ItemProperties newItemProperties);
        ItemProperties Deserialize(byte[] content);
    }

    internal class WebDAVPropertySerializeFormatProviderXML : IWebDAVPropertySerializeFormatProvider
    {
        public virtual byte[] Serialize(ItemProperties newItemProperties)
        {
            return Helper.SerializeXml(newItemProperties);
        }

        public virtual ItemProperties Deserialize(byte[] content)
        {
            return Helper.DeserializeXml<ItemProperties>(content);
        }
    }

    /// <summary>
    /// Encapsulates internal knowledge about the locations
    /// where storage of WebDAV/SVN-specific properties happens.
    /// Perfect encapsulation would result in simply being able to swap this implementation for another one
    /// and have a completely differently implemented storage mechanism for WebDAV properties.
    /// </summary>
    /// XXX layer violation: this class ought to be doing properties reading/writing
    /// by getting passed the *SCM item* paths only
    /// (i.e. the items that ought to have their DAV properties updated)
    /// and then *internally* figuring out
    /// the property-storage-dedicated items
    /// which correspond to specific SCM items.
    public sealed class WebDAVPropertyStorageAdaptor
    {
        private const string propFolderPlusSlash = Constants.PropFolder + "/";

        private readonly TFSSourceControlProvider sourceControlProvider;
        private readonly IWebDAVPropertySerializeFormatProvider formatProvider;

        public WebDAVPropertyStorageAdaptor(TFSSourceControlProvider sourceControlProvider)
        {
            this.sourceControlProvider = sourceControlProvider;
            this.formatProvider = new WebDAVPropertySerializeFormatProviderXML();
        }

        public static string GetPropertiesFolderName(string path, ItemType itemType)
        {
            if (itemType == ItemType.Folder)
            {
                if (path.Equals("/"))
                    return "/" + Constants.PropFolder;
                return path + "/" + Constants.PropFolder;
            }
            int indexLastSlash = path.LastIndexOf('/');
            if (indexLastSlash != -1)
                return path.Substring(0, indexLastSlash) + "/" + Constants.PropFolder;
            return Constants.PropFolder;
        }

        public static string GetPropertiesFileName(string path, ItemType itemType)
        {
            if (itemType == ItemType.Folder)
            {
                string subPathToPropFileOfFolder = "/" + propFolderPlusSlash + Constants.FolderPropFile;

                return (path.Equals("/")) ? subPathToPropFileOfFolder : path + subPathToPropFileOfFolder;
            }
            int indexLastSlash = path.LastIndexOf('/');
            if (indexLastSlash != -1)
            {
                return
                    path.Substring(0, indexLastSlash) + "/" + Constants.PropFolder +
                    path.Substring(indexLastSlash);
            }
            return propFolderPlusSlash + path;
        }

        public ItemProperties PropertiesRead(ItemMetaData itemForItemProperties)
        {
            var itemContent = sourceControlProvider.ReadFile(itemForItemProperties);
            ItemProperties properties = formatProvider.Deserialize(itemContent);
            return properties;
        }

        public void PropertiesWrite(string activityId, string propertiesPath, ItemProperties newItemProperties, bool reportUpdatedFile)
        {
            var itemContent = formatProvider.Serialize(newItemProperties);
            sourceControlProvider.WriteFile(activityId, propertiesPath, itemContent, reportUpdatedFile);
        }

        public static bool IsPropertyStorageItem(ref string itemPath, ref ItemType itemType)
        {
            bool isPropertyStorageItem = false;
            if (IsPropertyFileType(itemPath))
            {
                itemPath = itemPath.Replace("/" + propFolderPlusSlash, "/");
                if (itemPath.EndsWith("/" + Constants.FolderPropFile))
                {
                    itemPath = itemPath.Replace("/" + Constants.FolderPropFile, "");
                    // itemType externally pre-set - prefer doing explicit assignment in folder case only
                    itemType = ItemType.Folder;
                }
                isPropertyStorageItem = true;
            }
            return isPropertyStorageItem;
        }

        public static string GetPathOfDataItemFromPathOfPropStorageItem(string path)
        {
            string itemPath = path;
            if (itemPath.StartsWith(propFolderPlusSlash))
            {
                if (itemPath.Equals(propFolderPlusSlash + Constants.FolderPropFile))
                {
                    itemPath = "";
                }
                else
                    itemPath = path.Substring(propFolderPlusSlash.Length);
            }
            else
            {
                itemPath = itemPath.Replace("/" + propFolderPlusSlash + Constants.FolderPropFile, "");
                itemPath = itemPath.Replace("/" + propFolderPlusSlash, "/");
            }
            return itemPath;
        }

        public static string GetRemoteNameOfPropertyChange(string remoteName)
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

        /// <summary>
        /// Hotpath performance tweak helper [user code first calls this fast unspecific check,
        /// then iff(!) found (rarely) does more specific ones, whether property file/folder...]
        /// </summary>
        /// <param name="path">item path</param>
        /// <returns>true in case path seems to be a path used for storage of SVN properties, else false</returns>
        private static bool IsSuspectedPropertyStuff(string path)
        {
            return (path.Contains(Constants.PropFolder));
        }

        public static bool IsPropertyFileType(string path)
        {
            if (IsSuspectedPropertyStuff(path))
            { // found!? --> do precise checks.
                if (path.StartsWith(propFolderPlusSlash) || path.Contains("/" + propFolderPlusSlash))
                    return true;
            }
            return false;
        }

        public static bool IsPropertyFolderType(string path)
        {
            if (IsSuspectedPropertyStuff(path))
            { // found!? --> do precise checks.
                if (path.Equals(Constants.PropFolder) || path.EndsWith("/" + Constants.PropFolder))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Indicates whether the location given is the property file
        /// which stores the properties of a folder.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool IsPropertyFileType_ForFolderProps(string path)
        {
            return path.EndsWith(WebDAVPropertyStorageAdaptor.propFolderPlusSlash + Constants.FolderPropFile);
        }

        public static bool IsPropertyFolderElement(string path)
        {
            if (IsSuspectedPropertyStuff(path))
            {
                return (
                     (path.StartsWith(propFolderPlusSlash) ||
                      path.EndsWith("/" + Constants.PropFolder) ||
                      path.Contains("/" + propFolderPlusSlash))
                );
            }
            return false;
        }

        /// <summary>
        /// Given a certain recursion operation,
        /// returns the list of locations of items where we manage our property storage.
        /// </summary>
        /// <param name="recursion">Indicates the requested recursion type (None, OneLevel)</param>
        /// <returns>List of locations of relevant items for property storage</returns>
        public static IEnumerable<string> CollectPropertyItemLocationsToBeQueried(
            string path,
            Recursion recursion)
        {
            List<string> propItemPaths = new List<string>();

            // Implementation here intentionally (at least currently)
            // coded to be an efficient no-op for the non-None/non-OneLevel cases...

            if (recursion == Recursion.None)
            {
                string propertiesForFile = WebDAVPropertyStorageAdaptor.GetPropertiesFileName(path, ItemType.File);
                string propertiesForFolder = WebDAVPropertyStorageAdaptor.GetPropertiesFileName(path, ItemType.Folder);
                propItemPaths.Add(propertiesForFile);
                propItemPaths.Add(propertiesForFolder);
            }
            else if (recursion == Recursion.OneLevel)
            {
                string propertiesForFile = WebDAVPropertyStorageAdaptor.GetPropertiesFileName(path, ItemType.File);
                string propertiesForFolderItems = path + "/" + Constants.PropFolder;
                propItemPaths.Add(propertiesForFile);
                propItemPaths.Add(propertiesForFolderItems);
            }

            return propItemPaths;
        }
    }
}
