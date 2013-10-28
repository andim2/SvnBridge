using System;

namespace CodePlex.TfsLibrary
{
    public class InconsistentTfsStateException : Exception
    {
        public InconsistentTfsStateException(params string[] directories)
            : base(string.Format(@"Cannot commit because of the following directories:
{0}

Some files were updated since the delete was requested. Please review the
directory status using the status command, then re-issue the delete command
before attempting to re-commit.", string.Join("\r\n  ", directories))) {}
    }
}