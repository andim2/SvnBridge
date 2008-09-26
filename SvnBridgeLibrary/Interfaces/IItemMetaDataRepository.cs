using SvnBridge.Protocol;
using SvnBridge.SourceControl;

namespace SvnBridge.Interfaces
{
    public interface IItemMetaDataRepository
    {
        ItemMetaData GetItems(int version,
                              string path,
                              Recursion recursion);

        FolderMetaData GetChangedItems(string path,
                                       int versionFrom,
                                       int versionTo,
                                       UpdateReportData reportData);
    }
}