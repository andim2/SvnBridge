using System.IO;
using System.Text;

namespace SvnBridge.Interfaces
{
    public interface IHttpResponse
    {
        Encoding ContentEncoding { get; set; }
        string ContentType { get; set; }
        // HONKING HUGE BIG FAT WARNING: properly "using" StreamWriter objects
        // will immediately (and very annoyingly!!
        // Since one might want to keep re-using a stream
        // via several unrelated writer objects serially...)
        // Close() their .BaseStream once leaving scope!
        // http://www.hightechtalks.com/csharp/streamwriter-closes-memorystream-255006.html
        // One could "try" to prevent this
        // by writing "stream wrapper" helper classes
        // which dirtily avoid Close() on Dispose() -
        // but in fact the response class
        // supports creating a "Filter" *chain* of *several* streams
        // (e.g. for gzip compression),
        // thus one absolutely cannot rely
        // on all chained particular stream class(es) used
        // actually providing such "hacks".
        // Thus user code should *NEVER* directly do "using" of OutputStream,
        // but rather use a sufficiently globally scoped StreamWriter object
        // (which is to be established
        // *after* actual setup of Filter chain has become ultimately stable)
        // which it has been passed
        // in order to avoid exactly these issues,
        // and in order to provide proper dependency separation
        // of output generator parts
        // from network-specific stream stuff!!
        // An alternative could be
        // creating a separate/owned local MemoryStream
        // whose data would then eventually (finally)
        // be flushed into OutputStream,
        // but better don't even think about that overhead...
        Stream OutputStream { get; }
        /// <summary>
        /// MSDN: "Gets or sets a wrapping filter object that is used to modify the HTTP entity body before transmission."
        /// </summary>
        Stream Filter { get; set; }
        bool SendChunked { get; set; }
        int StatusCode { get; set; }

        bool BufferOutput { get; set; }

        void AppendHeader(string name,
                          string value);

        void ClearHeaders();
        void Close();
    }
}
