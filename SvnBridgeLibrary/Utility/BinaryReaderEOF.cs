using System;
using System.IO;

namespace SvnBridge.Utility
{
    /// <remarks>
    /// WARNING: buggy class
    /// (see ReadBytes() comment)
    /// </remarks>
    public class BinaryReaderEOF
    {
        public const int BUF_SIZE = 1024;
        private byte[] _buffer = new byte[BUF_SIZE];
        private int _count;
        private int _position;
        private BinaryReader _reader;

        public BinaryReaderEOF(Stream input)
        {
            _reader = new BinaryReader(input);
            FillBuffer(BUF_SIZE);
        }

        public bool EOF
        {
            get
            {
                if (_position == _count)
                {
                    FillBuffer(BUF_SIZE);
                }

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

        private void FillBuffer(int size)
        {
            if (_buffer.Length < size)
            {
                _buffer = new byte[size];
            }

            _count = _reader.Read(_buffer, 0, _buffer.Length);
            _position = 0;
        }

        public byte ReadByte()
        {
            if (_position >= _count)
            {
                FillBuffer(BUF_SIZE);
            }

            return _buffer[_position++];
        }

        /// <remarks>
        /// WARNING: ReadBytes() is terribly buggy
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
            int bytesToRead = count;
            int index = 0;

            if (_position + bytesToRead > _count)
            {
                int availableBytes = _count - _position;
                Array.Copy(_buffer, _position, result, index, availableBytes);
                bytesToRead -= availableBytes;
                index += availableBytes;
                FillBuffer(bytesToRead);
            }

            Array.Copy(_buffer, _position, result, index, bytesToRead);
            _position += bytesToRead;

            return result;
        }
    }
}
