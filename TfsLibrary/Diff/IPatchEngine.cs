using CodePlex.TfsLibrary.ObjectModel;

namespace CodePlex.TfsLibrary
{
    public interface IPatchEngine
    {
        string CreatePatch(string directory,
                           SourceItemCallback callback);

        void ApplyPatch(string directory,
                        string patchXml,
                        SourceItemCallback callback);
    }
}