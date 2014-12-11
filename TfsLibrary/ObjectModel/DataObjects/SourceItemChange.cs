using CodePlex.TfsLibrary.RepositoryWebSvc;

namespace CodePlex.TfsLibrary.ObjectModel
{
    public class SourceItemChange
    {
        public ChangeType ChangeType;
        public SourceItem Item;

        public SourceItemChange() {}

        public SourceItemChange(SourceItem item,
                                ChangeType changeType)
        {
            Item = item;
            ChangeType = changeType;
        }
    }
}