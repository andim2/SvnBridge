using System;
using System.Collections.Generic;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using SvnBridge.Infrastructure; // Configuration
using SvnBridge.Utility; // SvnDiffParser, Helper

namespace SvnBridge.SourceControl
{
    public class ItemMetaData
    {
    	private FolderMetaData parent;

        public string Author;
        public bool OriginallyDeleted /* = false */;
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
    }
}
