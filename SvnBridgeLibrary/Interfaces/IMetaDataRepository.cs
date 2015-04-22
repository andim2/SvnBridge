using CodePlex.TfsLibrary.ObjectModel;
using SvnBridge.SourceControl;

namespace SvnBridge.Interfaces
{
    public interface IMetaDataRepository
    {
        SourceItem[] QueryItems(int revision, int itemId);
        SourceItem[] QueryItems(int revision, string path, Recursion recursion);
        SourceItem[] QueryItems(int revision, string[] paths, Recursion recursion);
    }
}
