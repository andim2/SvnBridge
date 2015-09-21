using System;
using System.Collections.Generic;
using System.Diagnostics; // Debug.WriteLine()
using System.Text; // StringBuilder
using CodePlex.TfsLibrary.RepositoryWebSvc;
using SvnBridge.Infrastructure; // Configuration
using SvnBridge.Utility; // SvnDiffParser, Helper

namespace SvnBridge.SourceControl
{
    // http://blogs.msdn.com/b/jaredpar/archive/2011/03/18/debuggerdisplay-attribute-best-practices.aspx
    //[DebuggerDisplay("{DebuggerDisplay}")]
    //[DebuggerDisplay("{DebuggerDisplay,nq}")]
    //[DebuggerDisplay("{DebugShowContent.ToString()}")]
    public class ItemMetaData
    {
    	private FolderMetaData parent;

        public string Author;
        // .NewlyAdded member is for UpdateDiffEngine use only
        // (this has been semi-dirtily added as a member here
        // rather than being remembered by a map member at UpdateDiffEngine,
        // since it's an intrinsic feature of an item
        // at a certain path location,
        // thus housekeeping ought to be easier
        // when keeping it recorded directly within the item).
        // .NewlyAdded indicates whether an item has been *newly* introduced
        // *within the particular commit range* that we are processing.
        // This is intended to enable us
        // to reliably decide
        // whether a Delete op of an item
        // should simply revert an already-inserted previous change,
        // rather than getting staged as an *active* Delete item change indication.
        // Put differently: if the item at start revision
        // was *already* existing
        // and we get a Delete op
        // --> *should* be an *active* Delete
        // as opposed to the case where
        // the item gets newly introduced
        // within the diffed commit range
        // via an Add,
        // followed by a Delete
        // --> simply revert (*discard*) the recorded item change.
        // Please note that the state of this flag
        // most certainly needs to be maintained
        // for *all* item conversions that may happen within the diff range, e.g.:
        // replacement of this item by its DeleteMetaData counterpart,
        // renames, ..
        public bool NewlyAdded /* = false */;
        public bool DataLoaded /* = false */;
        public string Base64DiffData /* = null */;
        public string Md5Hash /* = null */; // Important helper to support maintaining a properly end-to-end checksummed data chain
        //public Exception DataLoadedError;
        public string DownloadUrl /* = null */;
        public int Id;
        public int ItemRevision;
        // This DateTime member may NOT always be set to UTC kind!
        // Since for any DateTime use itself it's never known whether it's UTC or not,
        // we'll *keep* this member unspecified as well -
        // IOW make sure to *always* use .ToUniversalTime()
        // (and using .UtcNow wherever you might be tempted to use .Now)
        // *whenever* doing DateTime comparisons or assignment towards clearly-UTC destinations!
        // Plus, it has MUCH faster/better performance.
        // http://blogs.msdn.com/b/kirillosenkov/archive/2012/01/10/datetime-utcnow-is-generally-preferable-to-datetime-now.aspx
        // http://stackoverflow.com/a/6930542
        public DateTime LastModifiedDate;
        public string Name;
        public Dictionary<string, string> Properties = new Dictionary<string, string>();
        public int PropertyRevision;
        public int SubItemRevision;
        private const int RenderContentAsString_indent = 2;

        public ItemMetaData()
        {
        }

        public ItemMetaData(string name)
        {
            Name = name;
        }

        public virtual ItemType ItemType
        {
            get { return ItemType.File; }
        }

        /// <summary>
        /// Adopts item data loaded from SCM,
        /// by calculating internal members.
        /// Input data param *may* be handed in as null value
        /// (e.g. to properly indicate final retrieval failure
        /// to the item consumer side).
        /// </summary>
        public virtual void ContentDataAdopt(byte[] dataIn)
        {
            string base64 = null;
            string md5 = null;
            if (null != dataIn)
            {
                base64 = SvnDiffParser.GetBase64SvnDiffData(dataIn);
                md5 = Helper.GetMd5Checksum(dataIn);
            }
            else
            {
                Helper.DebugUsefulBreakpointLocation();
            }
            // Now centrally assign members
            Base64DiffData = base64;
            Md5Hash = md5;
            // Indicate successful completion even in case of retrieval failure:
            DataLoaded = true; // IMPORTANT MARKER - SET LAST!
        }

        /// <summary>
        /// Clean helper to ensure proper "bracketing"
        /// of data "fetching and releasing" ("robbing") ops.
        /// </summary>
        public virtual string ContentDataRobAsBase64(out string md5Hash)
        {
            bool isDataValid = (DataLoaded && (null != Base64DiffData) && (null != Md5Hash));
            if (!(isDataValid))
            {
                throw new ItemHasInvalidDataException(
                    this);
            }

            var base64DiffData = Base64DiffData;
            var base64DiffDataLength = base64DiffData.Length; // debug convenience helper
            md5Hash = Md5Hash;

            ContentDataRelease();

            return base64DiffData;
        }

        /// <summary>
        /// Make sure to have an improper result
        /// aggressively communicated to consumer side
        /// (via exception, since it is an exceptional case).
        /// </summary>
        public sealed class ItemHasInvalidDataException : InvalidOperationException
        {
            public ItemHasInvalidDataException(
                ItemMetaData item)
                : base("Item (" + item + ") does not contain valid data - retrieval failure!?")
            {
            }
        }

        /// <summary>
        /// Releases data memory from item's reach
        /// (reduces GC memory management pressure).
        /// </summary>
        private void ContentDataRelease()
        {
            DataLoaded = false; // IMPORTANT MARKER - CLEAR FIRST!
            Base64DiffData = null;
            Md5Hash = null;
        }

        public int ContentStorageLength
        {
            get
            {
                int length = 0;

                if (DataLoaded)
                {
                    // Handle "load failure" case (.DataLoaded true / data null)
                    if (null != Base64DiffData)
                    {
                        length = Base64DiffData.Length;
                    }
                }

                return length;
            }
        }

        public virtual int Revision
        {
            get
            {
                if (SubItemRevision > PropertyRevision && SubItemRevision > ItemRevision)
                {
                    return SubItemRevision;
                }
                else if (PropertyRevision > ItemRevision)
                {
                    return PropertyRevision;
                }
                else
                {
                    return ItemRevision;
                }
            }
        }

		public override string ToString()
		{
			return Name + " @" + Revision;
		}

    	public void SetParent(FolderMetaData parentFolder)
    	{
    		parent = parentFolder;
    	}

        public bool IsBelowEqual(string pathCompare)
        {
            return IsSubElement(pathCompare, Name);
        }

        protected static bool IsSubElement(string basePath, string candidate)
        {
            FilesysHelpers.StripRootSlash(ref basePath);
            FilesysHelpers.StripRootSlash(ref candidate);

            return (candidate.StartsWith(basePath,
                WantCaseSensitiveMatch ? StringComparison.InvariantCulture : StringComparison.InvariantCultureIgnoreCase)
            );
        }

        public static bool IsSamePathCaseSensitive(string itemPath, string pathCompare)
        {
            return IsSamePath_Internal(itemPath, pathCompare, true);
        }
        public static bool IsSamePathCaseInsensitive(string itemPath, string pathCompare)
        {
            return IsSamePath_Internal(itemPath, pathCompare, false);
        }

        public bool IsSamePath(string path)
        {
            return IsSamePath(Name, path);
        }

        /// <remarks>
        /// See also FolderMetaData.MightContain()
        /// </remarks>
        public static bool IsSamePath(string itemPath, string pathCompare)
        {
            return IsSamePath_Internal(itemPath, pathCompare, WantCaseSensitiveMatch);
        }

        private static bool IsSamePath_Internal(string itemPath, string pathCompare, bool wantCaseSensitiveMatchHere)
        {
            FilesysHelpers.StripRootSlash(ref itemPath);
            FilesysHelpers.StripRootSlash(ref pathCompare);

            return (string.Equals(
                itemPath, pathCompare,
                wantCaseSensitiveMatchHere ? StringComparison.InvariantCulture : StringComparison.InvariantCultureIgnoreCase)
            );
        }

        protected static bool WantCaseSensitiveMatch
        {
            get { return Configuration.SCMWantCaseSensitiveItemMatch; }
        }

        /// <summary>
        /// *VERY* important debug helper
        /// (for quick and elegant analysis in MSVS watch areas).
        /// </summary>
        public string DebugShowContent
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                RenderContentAsString(sb, 0);

                // Since things such as DebuggerDisplay, TextVisualizer etc.
                // do not (sufficiently easily?)
                // work for *formatted* content (the list that I'm trying to show),
                // simply resort to using Debug.WriteLine().
                string content = sb.ToString();
                Debug.WriteLine(content);
                return "Please see Debug.WriteLine() result in Output window";
            }
        }

        public void RenderContentAsString(StringBuilder sb, int indent)
        {
            RenderContentAsString_Header(sb, indent);
            sb.Append(":");
            RenderContentAsString_nextline(sb);
            RenderContentAsString_IndentIncr(ref indent); RenderContentAsString_Content(sb, indent);
        }

        protected static void RenderContentAsString_IndentIncr(ref int indent)
        {
            indent += RenderContentAsString_indent;
        }

        protected static string RenderContentAsString_IndentGet(int indent)
        {
            // http://www.dotnetexamples.com/2012/06/c-indent-stringtext-with-spaces.html
            return "".PadLeft(indent);
        }

        protected virtual void RenderContentAsString_Header(StringBuilder sb, int indent)
        {
            sb.Append(RenderContentAsString_IndentGet(indent)); sb.Append(GetType().Name);
        }

        /// <summary>
        /// This method is supposed to render
        /// a string-based equivalent
        /// of the entire (possibly recursive) content within this item.
        /// </summary>
        /// I decided to have a StringBuilder passed directly as a method param
        /// (somewhat in violation of proper per-object-result modularity),
        /// since that way
        /// there is only one central StringBuilder object maintained
        /// which we generate the result in.
        protected virtual void RenderContentAsString_Content(StringBuilder sb, int indent)
        {
            sb.Append(RenderContentAsString_IndentGet(indent));
            // We'll restrict things to displaying only those attributes
            // that are "usually" of interest
            // in a huge hierarchical dump
            // of such items.
            sb.Append("Name ");
            sb.Append(Name);
            sb.Append(", id ");
            sb.Append(Id);
            sb.Append(", rev ");
            sb.Append(Revision);
            sb.Append(", itemRev ");
            sb.Append(ItemRevision);
            sb.Append(", loaded ");
            sb.Append(DataLoaded);
            sb.Append(", lastMod ");
            sb.Append(LastModifiedDate);
            sb.Append(", newlyAdded ");
            sb.Append(NewlyAdded);
            RenderContentAsString_nextline(sb);
        }

        protected static void RenderContentAsString_nextline(StringBuilder sb)
        {
            sb.Append("\r\n");
        }

        //private string DebuggerDisplay
        //{
        //    get
        //    {
        //        return string.Format("{0}", DebugShowContent);
        //    }
        //}
    }
}
