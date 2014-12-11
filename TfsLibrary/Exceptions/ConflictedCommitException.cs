using System;

namespace CodePlex.TfsLibrary
{
    public class ConflictedCommitException : Exception
    {
        public ConflictedCommitException(params string[] conflictedItems)
            : base("Cannot commit because of outstanding conflicts in the following items:\r\n" + string.Join("\r\n", conflictedItems)) {}
    }
}