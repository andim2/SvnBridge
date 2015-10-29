using System; // InvalidOperationException
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CodePlex.TfsLibrary;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using SvnBridge.Utility; // Helper.DebugUsefulBreakpointLocation()

namespace SvnBridge.SourceControl
{
    public class FolderMetaData : ItemMetaData
    {
        public FolderMetaData(string name)
            : base(name)
        {
            _items = new NoNullAllowedItemsCollection(this);
        }

        public FolderMetaData()
        {
            _items = new NoNullAllowedItemsCollection(this);
        }

        private readonly IList<ItemMetaData> _items;

        public virtual IList<ItemMetaData> Items
        {
            get { return _items; }
        }

        public override ItemType ItemType
        {
            get { return ItemType.Folder; }
        }

        public ItemMetaData FindItem(string name)
        {
            // Shortcut (many incoming folder requests have completely foreign path names,
            // in which case traversing a whole completely foreign large directory
            // hierarchy would become super expensive):
            if (MightContain(name))
            {
                return FindItem_Internal(name);
            }
            return null;
        }

        /// <remarks>
        /// See also IsSamePath()
        /// </remarks>
        public bool MightContain(string pathCompare)
        {
            return IsSubElement(Name, pathCompare);
        }

        private ItemMetaData FindItem_Internal(string name)
        {
            return FindItem_Internal_Do(name);
        }

        /// <remarks>
        /// Implementation of this method seems a bit weird
        /// (e.g. duplicate IsSamePath() checks of same object when recursing,
        /// due to checking both the item-type and folder-type cases).
        /// This could be implemented more easily via virtuals
        /// (also to move the IsSamePath() checks to a handler of the base class),
        /// however since this sub item "find" method is quite obviously FolderMetaData-specific,
        /// it probably was decided to keep everything implemented
        /// at this derived class level.
        /// </remarks>
        private ItemMetaData FindItem_Internal_Do(string name)
        {
            if (IsSamePath(name))
                return this;
            foreach (ItemMetaData item in Items)
            {
                if (item.IsSamePath(name))
                {
                    return item;
                }
                FolderMetaData subFolder = item as FolderMetaData;
                if (subFolder != null)
                {
                    ItemMetaData result = subFolder.FindItem_Internal(name);
                    if (result != null)
                        return result;
                }
            }
            return null;
        }

        /// <summary>
        /// Helper to verify/check that no missing items (MissingItemMetaData)
        /// remained in our item space hierarchy.
        /// </summary>
        /// <remarks>
        /// While such a helper arguably
        /// perhaps should not be provided
        /// by such a container class,
        /// currently I don't know where else best to put it,
        /// so...
        /// </remarks>
        public void VerifyNoMissingItemMetaDataRemained()
        {
            VerifyNoMissingItemMetaDataRemained(this);
        }

        private static void VerifyNoMissingItemMetaDataRemained(
            FolderMetaData root)
        {
            foreach (ItemMetaData item in root.Items)
            {
                if (item is MissingItemMetaData)
                {
                    Helper.DebugUsefulBreakpointLocation();
                    throw new InvalidOperationException("Found missing item:" + item +
                                                        " but those should not be returned from this final(ized) filesystem item hierarchy space");
                }
                FolderMetaData folder = item as FolderMetaData;
                bool isFolder = (null != folder);
                if (isFolder)
                    VerifyNoMissingItemMetaDataRemained(folder);
            }
        }

        private class NoNullAllowedItemsCollection : Collection<ItemMetaData>
        {
            public NoNullAllowedItemsCollection(FolderMetaData parent)
            {
                this.parent = parent;
            }

            private readonly FolderMetaData parent;

            protected override void InsertItem(int index, ItemMetaData item)
            {
                Guard.ArgumentNotNull(item, "item");
                item.SetParent(parent);
                base.InsertItem(index, item);
            }

            protected override void SetItem(int index, ItemMetaData item)
            {
                Guard.ArgumentNotNull(item, "item");
                item.SetParent(parent);
                base.SetItem(index, item);
            }
        }
    }
}
