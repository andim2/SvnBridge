namespace CodePlex.TfsLibrary.ObjectModel
{
    public delegate void SyncupCallback(SourceItem item,
                                        SyncupAction actionTaken,
                                        SourceItemResult result);
}