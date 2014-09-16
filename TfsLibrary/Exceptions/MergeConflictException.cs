using System;

namespace CodePlex.TfsLibrary
{
    public class MergeConflictException : Exception
    {
        public MergeConflictException()
            : base("A merge conflict was detected") {}
    }
}