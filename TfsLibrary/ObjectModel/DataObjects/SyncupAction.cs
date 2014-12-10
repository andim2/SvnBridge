namespace CodePlex.TfsLibrary.ObjectModel
{
    public enum SyncupAction
    {
        None = UpdateAction.None,
        ServerAdded = UpdateAction.Added,
        ServerConflicted = UpdateAction.Conflicted,
        ServerDeleted = UpdateAction.Deleted,
        ServerMerged = UpdateAction.Merged,
        ServerUpdated = UpdateAction.Updated,
        LocalAdded,
        LocalDeleted,
        LocalReverted = UpdateAction.Reverted,
    }
}