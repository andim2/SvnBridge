namespace CodePlex.TfsLibrary.ObjectModel
{
    public delegate void UpdateCallback(SourceItem item,
                                        UpdateAction actionTaken,
                                        SourceItemResult result);
}