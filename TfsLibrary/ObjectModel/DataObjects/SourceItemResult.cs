namespace CodePlex.TfsLibrary.ObjectModel
{
    public enum SourceItemResult
    {
        S_Ok,
        S_ForcedDelete,
        E_PathNotFound,
        E_AlreadyUnderSourceControl,
        E_HasLocalModifications,
        E_NotInAWorkingFolder,
        E_NotUnderSourceControl,
        E_WontClobberLocalItem,
        E_WontDeleteFileWithModifications,
        E_AlreadyConflicted,
        E_ChildDeleteFailure,
        E_AccessDenied,
        E_FileNotFound,
        E_DirectoryNotFound,
        E_PatchFilesOutsideLocalDirectory
    }
}