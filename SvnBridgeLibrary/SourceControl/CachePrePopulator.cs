using System.Collections.Generic;
using CodePlex.TfsLibrary.ObjectModel;
using SvnBridge.Cache;
using SvnBridge.Interfaces;
using SvnBridge.Utility;

namespace SvnBridge.SourceControl
{
    public class CachePrePopulator
    {
        private readonly TFSSourceControlProvider sourceControlProvider;
        readonly Dictionary<string, HashSet<string>> hierarchy = new Dictionary<string, HashSet<string>>();

        public CachePrePopulator(TFSSourceControlProvider sourceControlProvider)
        {
            this.sourceControlProvider = sourceControlProvider;
        }

        public void PrePopulateCacheWithChanges(SourceItemHistory history, int revision)
        {
            CreateHierarchy(history);
            RemoveItemsAlreadyLoadedByTheirParents();

            foreach (var kvp in hierarchy)
            {
                // we will call the parent instead of the children if by that
                // we can save significant amount of remote calls
                if (kvp.Value.Count>5 && kvp.Key != Constants.ServerRootPath)
                {
                    sourceControlProvider.GetItems(revision, kvp.Key, Recursion.Full);
                    continue;
                }
                foreach (var change in kvp.Value)
                {
                    sourceControlProvider.GetItems(revision, change, Recursion.Full);
                }
            }
        }

        private void RemoveItemsAlreadyLoadedByTheirParents()
        {
            var keysToRemove = new List<string>();
            foreach (var key in hierarchy.Keys)
            {
                HashSet<string> alreadyLoadedByParentKey;
                if (hierarchy.TryGetValue(key, out alreadyLoadedByParentKey) == false)
                    continue;
                keysToRemove.AddRange(alreadyLoadedByParentKey);
            }
            keysToRemove.ForEach(s => hierarchy.Remove(s));
        }

        private void CreateHierarchy(SourceItemHistory history)
        {
            foreach (var change in history.Changes)
            {
                string itemName = change.Item.RemoteName;
                while (itemName != Constants.ServerRootPath)
                {
                    string parentName = Helper.GetFolderNameUsingServerRootPath(itemName);
                    HashSet<string> children;
                    if (hierarchy.TryGetValue(parentName, out children) == false)
                    {
                        hierarchy[parentName] = children = new HashSet<string>();
                    }
                    children.Add(itemName);
                    itemName = parentName;
                }
            }
            if(hierarchy.Count==1)
                return;// we don't clear the root if it is the onyl one there.
            hierarchy.Remove(Constants.ServerRootPath);
        }
    }
}