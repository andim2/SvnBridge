using System;

namespace CodePlex.TfsLibrary.ClientEngine
{
    public enum TfsStateError
    {
        LocalPathNotFound,
        NotAWorkingFolder,
        NotInAWorkingFolder,
        NotUnderSourceControl,
    }

    public class TfsStateException : Exception
    {
        readonly TfsStateError error;
        readonly string localPath;

        public TfsStateException(TfsStateError error,
                                 string localPath)
            : base(string.Format("{0}: {1}", ErrorToText(error), localPath))
        {
            this.error = error;
            this.localPath = localPath;
        }

        public TfsStateError Error
        {
            get { return error; }
        }

        public string LocalPath
        {
            get { return localPath; }
        }

        static string ErrorToText(TfsStateError error)
        {
            switch (error)
            {
                case TfsStateError.LocalPathNotFound:
                    return "Path not found";
                case TfsStateError.NotAWorkingFolder:
                    return "Not a working folder";
                case TfsStateError.NotInAWorkingFolder:
                    return "Not in a working folder";
                case TfsStateError.NotUnderSourceControl:
                    return "Not under source control";
                default:
                    return error.ToString();
            }
        }
    }
}