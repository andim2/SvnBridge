namespace CodePlex.TfsLibrary.ObjectModel
{
    public enum SourceItemStatus
    {
        None,
        Unversioned,
        Unmodified,
        Modified,
        Missing,
        Delete,
        Add,
        Conflict,
    }
}