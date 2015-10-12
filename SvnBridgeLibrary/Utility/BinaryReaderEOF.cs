using System;
using System.IO;

namespace SvnBridge.Utility
{
    /// <summary>
    /// This class appears to be a helper
    /// to be able to support an EOF query
    /// even with non-.CanSeek stream types
    /// (e.g. CryptoStream rather than MemoryStream),
    /// by doing read-ahead into a local buffer.
    /// </summary>
    /// <remarks>
    /// WARNING: possibly still buggy class
    /// (see ReadBytes() comment)
    /// </remarks>
    public class BinaryReaderEOF : IDisposable
    {
        public const int BUF_SIZE = Constants.AllocSize_AvoidLOHCatastrophy;
        private byte[] _buffer = new byte[BUF_SIZE];
        private int _count;
        private int _position;
        private BinaryReader _reader;

        public BinaryReaderEOF(Stream input)
        {
            _reader = new BinaryReader(input);
            TryFillReadahead();
        }

        public bool EOF
        {
            get
            {
                TryFillReadaheadIfNeeded();

                if (_count == 0)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public Stream BaseStream
        {
            get
            {
                return _reader.BaseStream;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(
            bool disposing)
        {
            //_reader.Dispose(disposing);
        }

        private void TryFillReadaheadIfNeeded()
        {
            if (_position >= _count)
            {
                TryFillReadahead();
            }
        }

        private void TryFillReadahead()
        {
            _count = _reader.Read(_buffer, 0, _buffer.Length);
            _position = 0;
        }

        public byte ReadByte()
        {
            TryFillReadaheadIfNeeded();

            return _buffer[_position++];
        }

        /// <remarks>
        /// WARNING: ReadBytes() is terribly buggy
        /// (UPDATE: hopefully now fully corrected...)
        /// since it does not handle
        /// the "read more bytes than buffer member size" case
        /// (no _loop_ implemented!).
        /// This is why BinaryReaderEOF is now completely removed from
        /// all user code (with no harmful effects observed on a
        /// somewhat larger repo, I might add).
        /// Reasons:
        /// - original bug report had a very different discussion
        /// - this class got committed out of the blue, it was not
        ///   obvious at all that things had to be fixed like that
        ///   (no documentation / reasoning on the bug report)
        /// - almost no Changeset comment (merely the bug tracker link
        ///   was mentioned, fortunately)
        /// </remarks>
        public byte[] ReadBytes(int count)
        {
            byte[] result = new byte[count];
            int bytesToBeReadTotal = count;
            int index = 0;

            for (; ; )
            {
                bool needMoreData = (0 < bytesToBeReadTotal);
                if (!(needMoreData))
                {
                    break;
                }

                TryFillReadaheadIfNeeded();

                int availableBytes = _count - _position;

                bool haveData = (0 < availableBytes);

                if (!(haveData))
                {
                    throw new CouldNotReadRequiredAmountException();
                }

                var bytesToBeCopiedThisTime = Math.Min(availableBytes, bytesToBeReadTotal);
                Array.Copy(_buffer, _position, result, index, bytesToBeCopiedThisTime);
                _position += bytesToBeCopiedThisTime;

                bytesToBeReadTotal -= bytesToBeCopiedThisTime;
                index += bytesToBeCopiedThisTime;
            }

            return result;
        }

        public sealed class CouldNotReadRequiredAmountException : IOException
        {
            public CouldNotReadRequiredAmountException()
                : base(
                    "Could not read required amount of data")
            {
            }
        }
    }
}
