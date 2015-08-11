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
            string res = (full.Length > prefix.Length) ? full.Substring(prefix.Length) : "";
            return res;
        }

        public static void PathAppendElem(ref string path, string pathElem)
        {
            if (path != "" && !path.EndsWith(repo_separator_s))
                path += repo_separator_s + pathElem;
            else
                path += pathElem;
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
    }

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
            item.Name = FilesysHelpers.StripPrefix(rootPath, sourceItem.RemoteName);

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
        public FolderMap()
        {
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
            // Figure out whether there's an existing item which might be parent
            // of the new item:
            bool haveRoot = (null != rootItem);
            if (haveRoot)
            {
                if (newItem.IsBelowEqual(rootItem.Name))
                {
                    Debugger.Launch(); // TODO UNFINISHED
                }
            }

            if (newItem.ItemType == ItemType.Folder)
            {
                InsertFolder((FolderMetaData)newItem);
            }

            if (newItem.ItemType == ItemType.File)
            {
                string folderName = FilesysHelpers.GetFolderPathPart(newItem.Name);
                // Hmm... since this is a *helper-only* folder:
                // perhaps we should wrap a new FolderMetaData in a *stub*
                // StubFolderMetaData instead?
                FolderMetaData folder = new FolderMetaData();
                folder.Name = folderName;
                InsertFolder(folder);
                ItemHelpers.FolderOps_AddItem(folder, newItem);
                SubmitAsRootItem(newItem);
            }

            {
                FolderMetaData folder = FetchContainerFolderForItem(newItem);
                // NO null check here - I'm not quite certain about the rootItem mechanism yet,
                // thus if it crashes, then it does, which ensures that we'll be able to notice it.
                ItemHelpers.FolderOps_AddItem(folder, newItem);
            }
        }

        private void SubmitAsRootItem(ItemMetaData item)
        {
            if (null == rootItem)
            {
                rootItem = item;
            }
        }

        private void InsertFolder(FolderMetaData folder)
        {
            InsertFolderInMap(folder);
            SubmitAsRootItem(folder);
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
        /// <param name="item">The item (file, folder) to return a base folder for</param>
        /// <returns>Base folder item which may be used to contain the item</returns>
        private FolderMetaData FetchContainerFolderForItem(ItemMetaData item)
        {
            string folderName = FilesysHelpers.GetFolderPathPart(item.Name);
            string folderNameMangled = FilesysHelpers.GetCaseMangledName(folderName);

            FolderMetaData folder = TryGetFolder(folderNameMangled);
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
                    FindMatchingExistingFolderCandidate_CaseInsensitive(folders, folderNameMangled) : null;
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

        public ItemQueryCollector(TFSSourceControlProvider sourceControlProvider)
        {
            this.sourceControlProvider = sourceControlProvider;
        }

        public ItemMetaData process(ItemMetaData[] items, bool returnPropertyFiles)
        {
                FolderMap folderMap = new FolderMap();
                Dictionary<string, ItemProperties> dictPropertiesOfItems = new Dictionary<string, ItemProperties>();
                Dictionary<string, int> dictPropertiesRevisionOfItems = new Dictionary<string, int>();
                WebDAVPropertyStorageAdaptor propsSerializer = new WebDAVPropertyStorageAdaptor(sourceControlProvider);
                foreach (ItemMetaData item in items)
                {
                    bool isPropertyFile = WebDAVPropertyStorageAdaptor.IsPropertyFileType(item.Name);
                    bool wantReadPropertyData = (isPropertyFile && !returnPropertyFiles);
                    if (wantReadPropertyData)
                    {
                        string itemPath = WebDAVPropertyStorageAdaptor.GetItemFileNameFromPropertiesFileName(item.Name);
                        dictPropertiesRevisionOfItems[itemPath] = item.Revision;
                        dictPropertiesOfItems[itemPath] = propsSerializer.PropertiesRead(item);
                    }
                    bool wantQueueItem = ((!isPropertyFile && !WebDAVPropertyStorageAdaptor.IsPropertyFolderType(item.Name)) || returnPropertyFiles);
                    if (wantQueueItem)
                    {
                        folderMap.Insert(item);
                    }
                }
                // Could have added an expensive(?)/extraneous "need properties update" bool calculation here
                // to skip handling when possible,
                // but such semi-direct logic would be quite risky,
                // and the two sub functions have an empty-skip of their loops anyway...
                // (this sadly is a relatively common case though since not many items
                // are property-related).
                UpdatePropertiesOfItems(folderMap, dictPropertiesOfItems);
                UpdatePropertiesRevisionOfItems(folderMap, dictPropertiesRevisionOfItems);

                // Either (usually) a folder or sometimes even single-item:
                ItemMetaData root = folderMap.QueryRootItem();
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

        private readonly TFSSourceControlService sourceControlService;
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
        private WebDAVPropertyStorageAdaptor propsSerializer;

        public TFSSourceControlProvider(
            TFSSourceControlService sourceControlService,
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
                    this.rootPath);
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
                string targetPath = "/" + Helper.CombinePath(path, reportData.UpdateTarget);
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
        /// </remarks>
        /// <param name="histories">Array of histories to be tweaked</param>
        private void LogHistory_TweakIt_ForSVN(ref SourceItemHistory[] histories)
        {
            foreach (SourceItemHistory history in histories)
            {
                List<SourceItem> renamedItems = new List<SourceItem>();
                List<SourceItem> branchedItems = new List<SourceItem>();

                foreach (SourceItemChange change in history.Changes)
                {
                    bool isRename = ((change.ChangeType & ChangeType.Rename) == ChangeType.Rename);
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

                    if (isRename)
                    {
                        renamedItems.Add(change.Item);
                    }
                    else if (isBranch)
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
                    foreach (var change in history.Changes.Where(change => (change.ChangeType & ChangeType.Rename) == ChangeType.Rename))
                    {
                        ItemMetaData oldItem;
                        if (oldItemsById.TryGetValue(change.Item.ItemId, out oldItem))
                            change.Item = new RenamedSourceItem(change.Item, oldItem.Name, oldItem.Revision);
                        else
                            renamesWithNoPreviousVersion.Add(change);
                    }

                    // [this is slowpath (rare event),
                    // thus Remove() is better than Enumerable.Except() use:]
                    foreach (var rename in renamesWithNoPreviousVersion)
                        history.Changes.Remove(rename);

                    // Hmm, was .None handling really intended to be specifically done in this rename handling part only??
                    // OTOH perhaps .None entries *are* meaningful for (some?) changes other than renames
                    // (perhaps hinting about [non-]deleted identical items in various branches??) - who knows...
                    history.Changes.RemoveAll(change => change.ChangeType == ChangeType.None);
                    history.Changes.RemoveAll(change => change.ChangeType == ChangeType.Delete &&
                                              oldItems.Any(oldItem => oldItem != null && oldItem.Id == change.Item.ItemId));
                }
                if (branchedItems.Count > 0)
                {
                    var itemsBranched = branchedItems.Select(item => CreateItemSpec(MakeTfsPath(item.RemoteName), RecursionType.None)).ToArray();

                    ChangesetVersionSpec branchChangeset = new ChangesetVersionSpec();
                    branchChangeset.cs = history.ChangeSetID;
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
                                                string oldName = branchItem.BranchFromItem.item.Substring(rootPath.Length);
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
            }
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
            sourceItem.RemoteName = FilesysHelpers.StripPrefix(rootPath, sourceItem.RemoteName);
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
            SourceItemHistory historyOfSVNCommit = ConstructSourceItemHistoryFromChangeset(
                changeset);
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
                changeset.Changes[0].Item.cs,
                changeset.owner,
                changeset.date,
                changeset.Comment);
        }

        private IEnumerable<SourceItemChange> ConvertTFSChangesetToSVNSourceItemChanges(
            Changeset changeset)
        {
            List<SourceItemChange> sourceItemChanges = new List<SourceItemChange>();

            foreach (Change change in changeset.Changes)
            {
                bool isChangeRelevantForSVNHistory = !WebDAVPropertyStorageAdaptor.IsPropertyFolderType(change.Item.item);

                if (!(isChangeRelevantForSVNHistory))
                {
                    continue;
                }

                bool isChangeOfAnSVNProperty = WebDAVPropertyStorageAdaptor.IsPropertyFileType(change.Item.item);
                if (isChangeOfAnSVNProperty)
                {
                    string item = WebDAVPropertyStorageAdaptor.GetItemFileNameFromPropertiesFileName(change.Item.item);
                    bool itemFileIncludedInChanges = false;
                    foreach (Change itemChange in changeset.Changes)
                    {
                        if (item.Equals(itemChange.Item.item))
                        {
                            itemFileIncludedInChanges = true;
                            break;
                        }
                    }
                    if (!itemFileIncludedInChanges)
                    {
                        SourceItem sourceItem = ConvertChangeToSourceItem(change);
                        string item_actual = item;
                        ItemType itemType_actual = WebDAVPropertyStorageAdaptor.IsPropertyFileType_ForFolderProps(change.Item.item) ? ItemType.Folder : ItemType.File;
                        sourceItem.RemoteName = item_actual;
                        sourceItem.ItemType = itemType_actual;
                        ChangeType changeType_PropertiesWereModified = ChangeType.Edit;

                        sourceItemChanges.Add(new SourceItemChange(sourceItem, changeType_PropertiesWereModified));
                    }
                }
                else // change of a standard source control item
                {
                    SourceItem sourceItem = ConvertChangeToSourceItem(change);
                    ChangeType changeType = change.type;
                    if ((changeType == (ChangeType.Add | ChangeType.Edit | ChangeType.Encoding)) ||
                        (changeType == (ChangeType.Add | ChangeType.Encoding)))
                        changeType = ChangeType.Add;
                    sourceItemChanges.Add(new SourceItemChange(sourceItem, changeType));
                }
            }

            return sourceItemChanges;
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
        private List<SourceItemHistory> QueryHistory(
            string serverPath,
            VersionSpec itemVersion,
            int versionFrom,
            int versionTo,
            RecursionType recursionType,
            int maxCount,
            bool sortAscending)
        {
            List<SourceItemHistory> histories;

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
                    sortAscending);
            }
            catch (SoapException ex)
            {
                if ((recursionType == RecursionType.Full) && (ex.Message.EndsWith(" does not exist at the specified version.")))
                {
                    // Workaround for bug in TFS2008sp1
                    int latestVersion = GetLatestVersion();
                    // WARNING: TFS08 QueryHistory() is very problematic! (see comments here and in next inner layer)
                    List<SourceItemHistory> tempHistories = QueryHistory(
                        serverPath,
                        itemVersion,
                        1,
                        latestVersion,
                        RecursionType.None,
                        2,
                        sortAscending /* is this the value to pass to have this workaround still work properly? */);
                    if (tempHistories[0].Changes[0].ChangeType == ChangeType.Delete && tempHistories.Count == 2)
                        latestVersion = tempHistories[1].ChangeSetID;

                    if (versionTo == latestVersion)
                    {
                        // in this case, there are only 2 revisions in TFS
                        // the first being the initial checkin, and the second
                        // being the deletion, there is no need to query further
                        histories = tempHistories;
                    }
                    else
                    {
                        string itemFirstPath = tempHistories[0].Changes[0].Item.RemoteName; // debug helper
                        histories = QueryHistory(
                            itemFirstPath,
                            VersionSpec.FromChangeset(latestVersion),
                            1,
                            latestVersion,
                            RecursionType.Full,
                            int.MaxValue,
                            sortAscending);
                    }

                    // I don't know whether we actually want/need to do ugly manual version limiting here -
                    // perhaps it would be possible to simply restrict the queries above up to versionTo,
                    // but perhaps these queries were being done this way since perhaps e.g. for merge operations
                    // (nonlinear history) version ranges of a query do need to be specified in full.
                    Histories_RestrictToRangeWindow(
                        ref histories,
                        versionTo,
                        maxCount,
                        false);

                    return histories;
                }
                else
                    throw;
            }
            List<Changeset> changesetsTotal = new List<Changeset>();

            changesetsTotal.AddRange(changesets);

            int logItemsCount_ThisRun = changesets.Length;

            // TFS QueryHistory API won't return more than 256 items,
            // so need to call multiple times if more requested
            // IMPLEMENTATION WARNING: since the 256 items limit
            // clearly is a *TFS-side* limitation,
            // make sure to always keep this correction handling code
            // situated within inner TFS-side handling layers!!
            const int TFS_QUERY_LIMIT = 256;
            bool didHitPossiblyPrematureLimit = ((logItemsCount_ThisRun == TFS_QUERY_LIMIT) && (maxCount_Allowed > TFS_QUERY_LIMIT));
            if (didHitPossiblyPrematureLimit)
            {
                for (; ; )
                {
                    didHitPossiblyPrematureLimit = (TFS_QUERY_LIMIT == logItemsCount_ThisRun);
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
                        sortAscending);
                    changesetsTotal.AddRange(changesets);
                    logItemsCount_ThisRun = changesets.Length;
                }
            }

            histories = ConvertChangesetsToSourceItemHistory(changesetsTotal.ToArray()).ToList();

            return histories;
        }

        private Changeset[] Service_QueryHistory(
            ItemSpec itemSpec, VersionSpec itemVersion,
            VersionSpec versionSpecFrom, VersionSpec versionSpecTo,
            int maxCount,
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
                true, false, false,
                sortAscending);

            return changesets;
        }

        /// <summary>
        /// Restrict a possibly overly wide list of changesets to a certain desired range,
        /// by passing a maximum version to be listed,
        /// and by subsequently restricting the number of entries to maxCount.
        /// </summary>
        /// <param name="histories">List of changesets to be modified</param>
        /// <param name="versionTo">maximum version to keep listing</param>
        /// <param name="maxCount">maximum number of entries allowed</param>
        /// <param name="whenOverflowDiscardNewest">when true: remove newest version entries, otherwise remove oldest.
        /// Hmm... not sure whether offering a whenOverflowDiscardNewest choice is even helpful -
        /// perhaps the user should always expect discarding at a certain end and thus _always_
        /// have loop handling for missing parts...
        /// </param>
        private static void Histories_RestrictToRangeWindow(
            ref List<SourceItemHistory> histories,
            int versionTo,
            int maxCount,
            bool whenOverflowDiscardNewest)
        {
            while ((histories.Count > 0) && (histories[0].ChangeSetID > versionTo))
            {
                histories.RemoveAt(0);
            }
            var numElemsExceeding = histories.Count - maxCount;
            bool isCountWithinRequestedLimit = (0 >= numElemsExceeding);
            if (!(isCountWithinRequestedLimit))
            {
                // Order of the results that TFS returns is from _newest_ (index 0) to oldest (last index),
                // thus when whenOverflowDiscardNewest == true we need to remove the starting range,
                // else end range.
                var numElemsRemove = numElemsExceeding;
                int startIndex = whenOverflowDiscardNewest ? 0 : maxCount;
                histories.RemoveRange(startIndex, numElemsRemove);
            }
        }

        public virtual bool IsDirectory(int version, string path)
        {
            ItemMetaData item = GetItemsWithoutProperties(version, path, Recursion.None);
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
            bool itemExists = false;

            // Decide to do strip-slash at the very top, since otherwise it would be
            // done *both* by GetItems() internally (its inner copy of the variable)
            // *and* below, by ItemMetaData implementation.
            SVNPathStripLeadingSlash(ref path);
            bool returnPropertyFiles = true;
            ItemMetaData item = GetItems(version, path, Recursion.None, returnPropertyFiles);
            if (item != null)
            {
                itemExists = true;
                bool needCheckCaseSensitiveItemMatch = (Configuration.SCMWantCaseSensitiveItemMatch);
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
                }
            }
            return itemExists;
        }

        public virtual bool ItemExists(int itemId, int version)
        {
            if (0 == itemId)
                throw new ArgumentException("item id cannot be zero", "itemId");
            var items = metaDataRepository.QueryItems(version, itemId);
            return (items.Length != 0);
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
            // Keep handling in exception scope minimalistic to the operation which we may need to intercept:
            try
            {
                Changeset[] changesets = Service_QueryHistory(
                    itemSpec, VersionSpec.Latest,
                    VersionSpec.First, versionSpecAtDate,
                    1,
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
            if (dict.ContainsKey(path))
            {
                propsChanges = dict[path];
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

        /// <remarks>
        /// Hmm... this helper is a bit dirty... but it helps.
        /// Should be reworked into a class which assembles an itemPaths member
        /// via various helper methods that return property file names.
        /// </remarks>
        private void CollectItemPaths(
            string path,
            ref List<string> itemPaths,
            Recursion recursion)
        {
            itemPaths.Add(path);

            // shortcut
            if ((recursion != Recursion.None) && (recursion != Recursion.OneLevel))
                return;

            string propertiesForFile = WebDAVPropertyStorageAdaptor.GetPropertiesFileName(path, ItemType.File);
            string propertiesForFolder = WebDAVPropertyStorageAdaptor.GetPropertiesFileName(path, ItemType.Folder);
            string propertiesForFolderItems = path + "/" + Constants.PropFolder;

            if (recursion == Recursion.None)
            {
                if (propertiesForFile.Length <= maxLengthFromRootPath)
                    itemPaths.Add(propertiesForFile);

                if (propertiesForFolder.Length <= maxLengthFromRootPath)
                    itemPaths.Add(propertiesForFolder);
            }
            else if (recursion == Recursion.OneLevel)
            {
                if (propertiesForFile.Length <= maxLengthFromRootPath)
                    itemPaths.Add(propertiesForFile);

                if (propertiesForFolderItems.Length <= maxLengthFromRootPath)
                    itemPaths.Add(propertiesForFolderItems);
            }
        }

        private ItemMetaData GetItems(int version, string path, Recursion recursion, bool returnPropertyFiles)
        {
            // WARNING: this interface will (update: "might" - things are now improved...)
            // return filename items with a case-insensitive match,
            // due to querying into TFS-side APIs!
            // All users which rely on precise case-sensitive matching
            // will need to account for this.
            // Ideally we should offer a clean interface here
            // which ensures case-sensitive matching when needed.

            ItemMetaData rootItem = null;

            SVNPathStripLeadingSlash(ref path);

            if (version == 0 && path.Equals(""))
            {
                version = GetEarliestVersion(path);
            }

            if (version == LATEST_VERSION)
            {
                version = GetLatestVersion();
            }

            SourceItem[] sourceItems = GetTFSSourceItems(version, path, recursion);

            if (sourceItems.Length > 0)
            {
                var itemCollector = new ItemQueryCollector(this);
                // Authorship (== history) fetching is very expensive -
                // TODO: make intelligently configurable from the outside,
                // only where needed (perhaps via a parameterization struct
                // for this method?):
                bool needAuthorshipLookup = false;
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

        private SourceItem[] GetTFSSourceItems(int version, string path, Recursion recursion)
        {
            List<string> itemPathsToBeQueried = new List<string>();
            CollectItemPaths(
                path,
                ref itemPathsToBeQueried,
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
                bool needNewLookup = true;
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
        /// <param name="itemName">item to be queried</param>
        /// <param name="version">version to do the query for</param>
        /// <param name="changeChangeSetID">Changeset ID of most recent change</param>
        /// <param name="changeCommitDateTime">Changeset commit date of most recent change</param>
        /// <returns>true when successfully queried, else false</returns>
        private bool DetermineMostRecentChangesetInTree(
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
                LogItem logQueryPartial_Newest = GetLog(
                    itemName,
                    itemVersion,
                    versionFrom,
                    versionTo,
                    Recursion.Full,
                    1);
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
                LogItem logQueryAll_Newest = GetLog(
                    itemName,
                    itemVersion,
                    versionFrom,
                    versionTo,
                    Recursion.Full,
                    1);
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
                    DateTime maxLastModified = DateTime.MinValue;

                    foreach (ItemMetaData folderItem in folder.Items)
                    {
                        if (maxChangeset < folderItem.Revision)
                            maxChangeset = folderItem.Revision;

                        if (maxLastModified < folderItem.LastModifiedDate)
                            maxLastModified = folderItem.LastModifiedDate;
                    }
                    // Hmm... is this syntax mismatch (ItemRevision vs. SubItemRevision) intended here?
                    if (item.ItemRevision < maxChangeset)
                        item.SubItemRevision = maxChangeset;

                    if (item.LastModifiedDate < maxLastModified)
                        item.LastModifiedDate = maxLastModified;
                }
                else
                {
                    int changeChangeSetID;
                    DateTime changeCommitDateTime;
                    bool determinedMostRecentChangeset = DetermineMostRecentChangesetInTree(
                        item.Name,
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

        private void MergeResponse_GatherEntries(string activityId, MergeActivityResponse mergeResponse)
        {
            List<string> baseFolders = new List<string>();
            List<string> sortedMergeResponse = new List<string>();
            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                foreach (ActivityItem item in activity.MergeList)
                {
                    ActivityItem newItem = item;
                    if (!WebDAVPropertyStorageAdaptor.IsPropertyFolderType(item.Path))
                    {
                        if (WebDAVPropertyStorageAdaptor.IsPropertyFileType(item.Path))
                        {
                            string path = item.Path.Replace("/" + WebDAVPropertyStorageAdaptor.propFolderPlusSlash, "/");
                            ItemType newItemType = item.FileType;
                            if (path.EndsWith("/" + Constants.FolderPropFile))
                            {
                                path = path.Replace("/" + Constants.FolderPropFile, "");
                                newItemType = ItemType.Folder;
                            }
                            newItem = new ActivityItem(path, newItemType, item.Action);
                        }

                        if (!sortedMergeResponse.Contains(newItem.Path))
                        {
                            sortedMergeResponse.Add(newItem.Path);

                            string path = newItem.Path.Substring(rootPath.Length - 1);
                            if (path.Equals(""))
                                path = "/";

                            MergeActivityResponseItem responseItem =
                                new MergeActivityResponseItem(newItem.FileType, path);
                            if (newItem.Action != ActivityItemAction.Deleted && newItem.Action != ActivityItemAction.Branch &&
                                newItem.Action != ActivityItemAction.RenameDelete)
                            {
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

                ItemMetaData itemExisting_HEAD = GetItems(LATEST_VERSION, path.Substring(1), Recursion.None, true);
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
            return Helper.CombinePath(rootPath, itemPath);
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
                        if (activity.PendingRenames.ContainsKey(localPath))
                        {
                            pendRequestPending = activity.PendingRenames[localPath];
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

        private ItemProperties ReadPropertiesForItem(string path, ItemType itemType, int version)
        {
            ItemProperties properties = null;
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
            return ReadPropertiesForItem(item.Name, item.ItemType, item.ItemRevision);
        }

        private void UpdateProperties(string activityId)
        {
            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                ItemMetaData item;
                ItemType itemType;

                foreach (string path in activity.Properties.Keys)
                {
                    DAVPropertiesChanges propsChangesOfPath = activity.Properties[path];
                    ItemProperties properties = GetItemProperties(activity, path, out item, out itemType);
                    Dictionary<string, Property> propertiesToAdd = new Dictionary<string, Property>();
                    foreach (Property property in properties.Properties)
                    {
                        propertiesToAdd[property.Name] = property;
                    }
                    foreach (KeyValuePair<string, string> property in propsChangesOfPath.Added)
                    {
                        propertiesToAdd[property.Key] = new Property(property.Key, property.Value);
                    }
                    foreach (string removedProperty in propsChangesOfPath.Removed)
                    {
                        propertiesToAdd.Remove(removedProperty);
                    }
                    properties.Properties.AddRange(propertiesToAdd.Values);

                    string propertiesPath = WebDAVPropertyStorageAdaptor.GetPropertiesFileName(path, itemType);
                    string propertiesFolder = WebDAVPropertyStorageAdaptor.GetPropertiesFolderName(path, itemType);
                    ItemMetaData propertiesFolderItem = GetItems(LATEST_VERSION, propertiesFolder, Recursion.None, true);
                    if ((propertiesFolderItem == null) && !activity.Collections.Contains(propertiesFolder))
                    {
                        MakeCollection(activityId, propertiesFolder);
                    }

                    bool reportUpdatedFile = (null != item);
                    WebDAVPropsSerializer.PropertiesWrite(activityId, propertiesPath, properties, reportUpdatedFile);
                }
            });
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

            ItemProperties properties = ReadPropertiesForItem(path, itemType, version);
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
            ItemSpec spec = new ItemSpec();
            spec.item = MakeTfsPath(path);
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
            ItemMetaData pendingItem = new ItemMetaData();
            if (items[0][0].type == ItemType.Folder)
            {
                pendingItem = new FolderMetaData();
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
                    // about 1 second per 100 items).
                    // Note that some elements may end up null; known reasons so far:
                    // - renamed item already had "deleted" state
                    //   (one such situation may be one where the item's containing folder gets renamed).
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
            }

            return renamedItems;
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
            LogItem log = GetLog(
                path,
                1,
                GetLatestVersion(),
                Recursion.None,
                int.MaxValue);
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
    internal sealed class WebDAVPropertyStorageAdaptor
    {
        // FIXME: this member ought to be made private, with suitably slim helpers offered to users,
        // but for now I will not do that (in order to avoid bugs from excessive changes).
        // And all TFSSourceControlProvider uses of Constants.PropFolder ought to be moved here, too...
        public const string propFolderPlusSlash = Constants.PropFolder + "/";

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

        public static string GetItemFileNameFromPropertiesFileName(string path)
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
    }
}
