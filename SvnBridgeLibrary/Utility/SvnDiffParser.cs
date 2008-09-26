using System;
using System.Collections.Generic;
using System.IO;

namespace SvnBridge.Utility
{
    public class SvnDiffParser
    {
        public static string GetSvnDiffData(byte[] data)
        {
            SvnDiff svnDiff = SvnDiffEngine.CreateReplaceDiff(data);
            using (MemoryStream svnDiffStream = new MemoryStream())
            {
                WriteSvnDiff(svnDiff, svnDiffStream);
                byte[] diff = svnDiffStream.ToArray();
                return Convert.ToBase64String(diff);
            }
        }

        public static SvnDiff[] ParseSvnDiff(Stream inputStream)
        {
            BinaryReaderEOF reader = new BinaryReaderEOF(inputStream);

            byte[] signature = reader.ReadBytes(3);
            byte version = reader.ReadByte();

            if (signature[0] != 'S' || signature[1] != 'V' || signature[2] != 'N')
            {
                throw new InvalidOperationException("The signature is invalid.");
            }
            if (version != 0)
            {
                throw new Exception("Unsupported SVN diff version");
            }

            List<SvnDiff> diffs = new List<SvnDiff>();
            while (!reader.EOF)
            {
                SvnDiff diff = new SvnDiff();

                diff.SourceViewOffset = ReadInt(reader);
                diff.SourceViewLength = ReadInt(reader);
                diff.TargetViewLength = ReadInt(reader);
                int instructionSectionLength = (int) ReadInt(reader);
                int dataSectionLength = (int) ReadInt(reader);

                diff.InstructionSectionBytes = reader.ReadBytes(instructionSectionLength);
                diff.DataSectionBytes = reader.ReadBytes(dataSectionLength);

                diffs.Add(diff);
            }
            return diffs.ToArray();
        }

        public static SvnDiff[] ParseSvnDiff(byte[] bytes)
        {
            MemoryStream stream = new MemoryStream(bytes);
            return ParseSvnDiff(stream);
        }

        public static void WriteSvnDiff(SvnDiff svnDiff,
                                        Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream);

            byte[] signature = new byte[] {(byte) 'S', (byte) 'V', (byte) 'N'};
            byte version = 0;

            writer.Write(signature);
            writer.Write(version);

            if (svnDiff != null)
            {
                int bytesWritten;
                WriteInt(writer, svnDiff.SourceViewOffset, out bytesWritten);
                WriteInt(writer, svnDiff.SourceViewLength, out bytesWritten);
                WriteInt(writer, svnDiff.TargetViewLength, out bytesWritten);
                WriteInt(writer, (ulong) svnDiff.InstructionSectionBytes.Length, out bytesWritten);
                WriteInt(writer, (ulong) svnDiff.DataSectionBytes.Length, out bytesWritten);

                writer.Write(svnDiff.InstructionSectionBytes);
                writer.Write(svnDiff.DataSectionBytes);
            }
            writer.Flush();
        }

        public static ulong ReadInt(BinaryReaderEOF reader)
        {
            int bytesRead;
            return ReadInt(reader, out bytesRead);
        }

        public static ulong ReadInt(BinaryReaderEOF reader,
                                    out int bytesRead)
        {
            ulong value = 0;

            bytesRead = 0;

            byte b = reader.ReadByte();
            bytesRead++;

            while ((b & 0x80) != 0)
            {
                value |= (byte) (b & 0x7F);
                value <<= 7;

                b = reader.ReadByte();
                bytesRead++;
            }

            value |= (ulong) b;

            return value;
        }

        public static void WriteInt(BinaryWriter writer,
                                    ulong value,
                                    out int bytesWritten)
        {
            int count = 1;
            ulong temp = value >> 7;
            while (temp > 0)
            {
                temp = temp >> 7;
                count++;
            }

            bytesWritten = count;
            while (--count >= 0)
            {
                byte b = (byte) ((byte) (value >> ((byte) count*7)) & 0x7F);
                if (count > 0)
                {
                    b |= 0x80;
                }

                writer.Write(b);
            }
        }
    }
}