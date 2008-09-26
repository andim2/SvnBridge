using System;
using System.Runtime.Serialization;

namespace CodePlex.NUnitExtensions
{
    [Serializable]
    public class InRangeException : AssertException
    {
        // Fields

        private string actual;
        private string high;
        private string low;

        // Lifetime

        public InRangeException(object actual,
                                object low,
                                object high,
                                string userMessage)
            : base(userMessage ?? "Assert.InRange() Failure")
        {
            this.low = low == null ? null : low.ToString();
            this.high = high == null ? null : high.ToString();
            this.actual = actual == null ? null : actual.ToString();
        }

        protected InRangeException(SerializationInfo info,
                                   StreamingContext context)
            : base(info, context)
        {
            low = info.GetString("Low");
            high = info.GetString("High");
            actual = info.GetString("Actual");
        }

        // Properties

        public string Actual
        {
            get { return actual; }
        }

        public string High
        {
            get { return high; }
        }

        public string Low
        {
            get { return low; }
        }

        public override string Message
        {
            get
            {
                return string.Format("{0}\r\nRange:  ({1} - {2})\r\nActual: {3}",
                                     base.Message,
                                     Low,
                                     High,
                                     Actual ?? "(null)");
            }
        }

        // Methods

        public override void GetObjectData(SerializationInfo info,
                                           StreamingContext context)
        {
            base.GetObjectData(info, context);

            info.AddValue("Low", low);
            info.AddValue("High", high);
            info.AddValue("Actual", actual);
        }
    }
}