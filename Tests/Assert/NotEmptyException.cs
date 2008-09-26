using System;
using System.Runtime.Serialization;

namespace CodePlex.NUnitExtensions
{
    [Serializable]
    public class NotEmptyException : AssertException
    {
        // Lifetime

        protected NotEmptyException(SerializationInfo info,
                                    StreamingContext context)
            : base(info, context)
        {
        }

        public NotEmptyException()
            : base(string.Format("Assert.NotEmpty() failure"))
        {
        }
    }
}