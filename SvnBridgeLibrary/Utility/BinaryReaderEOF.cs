using System;
using System.IO;

namespace SvnBridge.Utility
{
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