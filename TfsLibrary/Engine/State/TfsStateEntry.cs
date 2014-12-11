using System;
using System.Xml.Serialization;
using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using CodePlex.TfsLibrary.Utility;

namespace CodePlex.TfsLibrary.ClientEngine
{
    [Serializable]
    [XmlType(TypeName = "entry")]
    public class TfsStateEntry : INamedEntry
    {
        int changeset;
        int conflictChangeset;
        int id;
        ItemType itemType;
        string name;
        string serverPath;
        string shadow;
        SourceItemStatus status = SourceItemStatus.Unmodified;
        string tfsServerUrl;

        [XmlAttribute("changeset")]
        public int ChangesetId
        {
            get { return changeset; }
            set { changeset = value; }
        }

        [XmlIgnore]
        public int ConflictChangesetId
        {
            get { return conflictChangeset; }
            set { conflictChangeset = value; }
        }

        [XmlAttribute("conflict-changeset")]
        public string ConflictChangesetIdSerialized
        {
            get
            {
                if (conflictChangeset == Constants.NullChangesetId)
                    return null;
                return conflictChangeset.ToString();
            }
            set
            {
                if (value == null)
                    conflictChangeset = Constants.NullChangesetId;
                else
                    conflictChangeset = int.Parse(value);
            }
        }

        [XmlAttribute("id")]
        public int ItemId
        {
            get { return id; }
            set { id = value; }
        }

        [XmlIgnore]
        public ItemType ItemType
        {
            get { return itemType; }
            set { itemType = value; }
        }

        [XmlAttribute("kind")]
        public string ItemTypeSerialized
        {
            get { return Enum.GetName(typeof(ItemType), itemType).ToLowerInvariant(); }
            set { itemType = (ItemType)Enum.Parse(typeof(ItemType), value, true); }
        }

        [XmlAttribute("name")]
        public string Name
        {
            get { return name; }
            set { name = value; }
        }

        [XmlAttribute("path")]
        public string ServerPath
        {
            get { return serverPath; }
            set { serverPath = value; }
        }

        [XmlAttribute("shadow")]
        public string Shadow
        {
            get
            {
                if (itemType == ItemType.File)
                    return null;

                if (shadow == null)
                    shadow = Guid.NewGuid().ToString("N");
                return shadow;
            }
            set { shadow = value; }
        }

        [XmlIgnore]
        public SourceItemStatus Status
        {
            get { return status; }
            set { status = value; }
        }

        [XmlAttribute("status")]
        public string StatusSerialized
        {
            get
            {
                if (status == SourceItemStatus.Unmodified)
                    return null;
                return status.ToString().ToLowerInvariant();
            }
            set
            {
                if (value == null)
                    status = SourceItemStatus.Unmodified;
                else
                    status = (SourceItemStatus)Enum.Parse(typeof(SourceItemStatus), value, true);
            }
        }

        [XmlAttribute("url")]
        public string TfsServerUrl
        {
            get { return tfsServerUrl; }
            set { tfsServerUrl = value; }
        }

        public static TfsStateEntry NewFileEntry(string name,
                                                 int itemId,
                                                 int changesetId,
                                                 SourceItemStatus status)
        {
            TfsStateEntry entry = new TfsStateEntry();

            entry.tfsServerUrl = null;
            entry.serverPath = null;
            entry.id = itemId;
            entry.name = name;
            entry.itemType = ItemType.File;
            entry.changeset = changesetId;
            entry.conflictChangeset = Constants.NullChangesetId;
            entry.status = status;

            return entry;
        }

        public static TfsStateEntry NewFolderEntry(string name,
                                                   int itemId,
                                                   int changesetId,
                                                   SourceItemStatus status,
                                                   string shadow)
        {
            TfsStateEntry entry = new TfsStateEntry();

            entry.tfsServerUrl = null;
            entry.serverPath = null;
            entry.id = itemId;
            entry.name = name;
            entry.itemType = ItemType.Folder;
            entry.changeset = changesetId;
            entry.conflictChangeset = Constants.NullChangesetId;
            entry.status = status;
            entry.shadow = shadow;

            return entry;
        }

        public static TfsStateEntry NewRootEntry(string tfsUrl,
                                                 string serverPath,
                                                 int itemId,
                                                 int changesetId,
                                                 SourceItemStatus status)
        {
            TfsStateEntry entry = new TfsStateEntry();

            entry.tfsServerUrl = tfsUrl;
            entry.serverPath = serverPath;
            entry.id = itemId;
            entry.name = "";
            entry.itemType = ItemType.Folder;
            entry.changeset = changesetId;
            entry.conflictChangeset = Constants.NullChangesetId;
            entry.status = status;
            entry.shadow = Guid.NewGuid().ToString("N");

            return entry;
        }

        public static TfsStateEntry NewRootEntry(string tfsUrl,
                                                 string serverPath,
                                                 int itemId,
                                                 int changesetId,
                                                 SourceItemStatus status,
                                                 string shadow)
        {
            TfsStateEntry entry = new TfsStateEntry();

            entry.tfsServerUrl = tfsUrl;
            entry.serverPath = serverPath;
            entry.id = itemId;
            entry.name = "";
            entry.itemType = ItemType.Folder;
            entry.changeset = changesetId;
            entry.conflictChangeset = Constants.NullChangesetId;
            entry.status = status;
            entry.shadow = shadow;

            return entry;
        }
    }
}