using System;
using System.Collections.Generic;

namespace CodePlex.TfsLibrary
{
    public class InvalidTfsDirectoryStructureException : Exception
    {
        readonly List<Error> errors = new List<Error>();

        public InvalidTfsDirectoryStructureException(params Error[] errors)
        {
            this.errors.AddRange(errors);
        }

        public InvalidTfsDirectoryStructureException(IEnumerable<Error> errors)
        {
            this.errors.AddRange(errors);
        }

        public IList<Error> Errors
        {
            get { return errors.AsReadOnly(); }
        }

        public override string Message
        {
            get
            {
                string message = "Your source directories contain the following problems that prevent this\r\noperation from completing:\r\n";

                foreach (Error error in errors)
                {
                    message += "\r\nIn path " + error.LocalPath + ":\r\n";

                    if (string.Compare(error.ExpectedServerUrl, error.ActualServerUrl, true) != 0)
                        message += string.Format("    Expected server URL: {0}\r\n    Actual server URL:   {1}\r\n",
                                                 error.ExpectedServerUrl, error.ActualServerUrl);

                    if (string.Compare(error.ExpectedServerPath, error.ActualServerPath, true) != 0)
                        message += string.Format("    Expected server path: {0}\r\n    Actual server path:   {1}\r\n",
                                                 error.ExpectedServerPath, error.ActualServerPath);
                }

                return message;
            }
        }

        public class Error
        {
            public readonly string ActualServerPath;
            public readonly string ActualServerUrl;
            public readonly string ExpectedServerPath;
            public readonly string ExpectedServerUrl;
            public readonly string LocalPath;

            public Error(string localPath,
                         string expectedServerUrl,
                         string expectedServerPath,
                         string actualServerUrl,
                         string actualServerPath)
            {
                LocalPath = localPath;
                ExpectedServerUrl = expectedServerUrl;
                ExpectedServerPath = expectedServerPath;
                ActualServerUrl = actualServerUrl;
                ActualServerPath = actualServerPath;
            }
        }
    }
}