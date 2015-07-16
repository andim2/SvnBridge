using System; // InvalidOperationException, StringComparison
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
                    ItemMetaData result = subFolder.FindItem(name);
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
                if (item is FolderMetaData)
                    VerifyNoMissingItemMetaDataRemained((FolderMetaData)item);
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
