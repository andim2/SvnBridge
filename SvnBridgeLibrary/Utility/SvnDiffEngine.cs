using System;
using System.IO;
using System.Collections.Generic;

namespace SvnBridge.Utility
{
    public class SvnDiffEngine
    {
        public static byte[] ApplySvnDiff(SvnDiff svnDiff, byte[] source, int sourceDataStartIndex)
        {
            const int BUFFER_EXPAND_SIZE = 5000;
            byte[] buffer = new byte[BUFFER_EXPAND_SIZE];
            int targetIndex = 0;

            MemoryStream instructionStream = new MemoryStream(svnDiff.InstructionSectionBytes);
            BinaryReaderEOF instructionReader = new BinaryReaderEOF(instructionStream);
            MemoryStream dataStream = new MemoryStream(svnDiff.DataSectionBytes);
            BinaryReader dataReader = new BinaryReader(dataStream);

            SvnDiffInstruction instruction = ReadInstruction(instructionReader);
            while (instruction != null)
            {
                if (targetIndex + (int) instruction.Length > buffer.Length)
                {
                    Array.Resize(ref buffer, buffer.Length + (int) instruction.Length + BUFFER_EXPAND_SIZE);
                }

                switch (instruction.OpCode)
                {
                    case SvnDiffInstructionOpCode.CopyFromSource:
                        Array.Copy(source,
                                   (int) instruction.Offset + sourceDataStartIndex,
                                   buffer,
                                   targetIndex,
                                   (int) instruction.Length);
                        targetIndex += (int) instruction.Length;
                        break;

                    case SvnDiffInstructionOpCode.CopyFromTarget:
                        // Cannot use Array.Copy because Offset + Length may be greater then starting targetIndex
                        for (int i = 0; i < (int) instruction.Length; i++)
                        {
                            buffer[targetIndex] = buffer[(int) instruction.Offset + i];
                            targetIndex++;
                        }
                        break;

                    case SvnDiffInstructionOpCode.CopyFromNewData:
                        byte[] newData = dataReader.ReadBytes((int) instruction.Length);
                        Array.Copy(newData, 0, buffer, targetIndex, newData.Length);
                        targetIndex += newData.Length;
                        break;
                }

                instruction = ReadInstruction(instructionReader);
            }

            Array.Resize(ref buffer, targetIndex);
            return buffer;
        }

        public static SvnDiff CreateReplaceDiff(byte[] bytes, int index, int length)
        {
            SvnDiff svnDiff = null;
            if (length > 0)
            {
                svnDiff = new SvnDiff();

                svnDiff.SourceViewOffset = 0;
                svnDiff.SourceViewLength = 0;
                svnDiff.TargetViewLength = (ulong)length;

                MemoryStream instructionStream = new MemoryStream();
                BinaryWriter instructionWriter = new BinaryWriter(instructionStream);
                MemoryStream dataStream = new MemoryStream();
                BinaryWriter dataWriter = new BinaryWriter(dataStream);

                dataWriter.Write(bytes, index, length);
                dataWriter.Flush();

                svnDiff.DataSectionBytes = dataStream.ToArray();

                SvnDiffInstruction instruction = new SvnDiffInstruction();
                instruction.OpCode = SvnDiffInstructionOpCode.CopyFromNewData;
                instruction.Length = (ulong)length;

                WriteInstruction(instructionWriter, instruction);
                instructionWriter.Flush();

                svnDiff.InstructionSectionBytes = instructionStream.ToArray();
            }
            return svnDiff;
        }

        public static SvnDiff[] ParseSvnDiff(byte[] bytes)
        {
            MemoryStream stream = new MemoryStream(bytes);
            return ParseSvnDiff(stream);
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
                int instructionSectionLength = (int)ReadInt(reader);
                int dataSectionLength = (int)ReadInt(reader);

                diff.InstructionSectionBytes = reader.ReadBytes(instructionSectionLength);
                diff.DataSectionBytes = reader.ReadBytes(dataSectionLength);

                diffs.Add(diff);
            }
            return diffs.ToArray();
        }

        public static void WriteSvnDiffSignature(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream);

            byte[] signature = new byte[] { (byte)'S', (byte)'V', (byte)'N' };
            byte version = 0;
            writer.Write(signature);
            writer.Write(version);

            writer.Flush();
        }

        public static void WriteSvnDiff(SvnDiff svnDiff, Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream);

            if (svnDiff != null)
            {
                int bytesWritten;
                WriteInt(writer, svnDiff.SourceViewOffset, out bytesWritten);
                WriteInt(writer, svnDiff.SourceViewLength, out bytesWritten);
                WriteInt(writer, svnDiff.TargetViewLength, out bytesWritten);
                WriteInt(writer, (ulong)svnDiff.InstructionSectionBytes.Length, out bytesWritten);
                WriteInt(writer, (ulong)svnDiff.DataSectionBytes.Length, out bytesWritten);

                writer.Write(svnDiff.InstructionSectionBytes);
                writer.Write(svnDiff.DataSectionBytes);
            }
            writer.Flush();
        }

        private static SvnDiffInstruction ReadInstruction(BinaryReaderEOF reader)
        {
            if (reader.EOF)
            {
                return null;
            }

            SvnDiffInstruction instruction = new SvnDiffInstruction();

            byte opCodeAndLength = reader.ReadByte();

            instruction.OpCode = (SvnDiffInstructionOpCode) ((opCodeAndLength & 0xC0) >> 6);

            byte length = (byte) (opCodeAndLength & 0x3F);
            if (length == 0)
            {
                instruction.Length = ReadInt(reader);
            }
            else
            {
                instruction.Length = length;
            }

            if (instruction.OpCode == SvnDiffInstructionOpCode.CopyFromSource ||
                instruction.OpCode == SvnDiffInstructionOpCode.CopyFromTarget)
            {
                instruction.Offset = ReadInt(reader);
            }

            return instruction;
        }

        private static void WriteInstruction(BinaryWriter writer, SvnDiffInstruction instruction)
        {
            byte opCodeAndLength = (byte) ((int) instruction.OpCode << 6);
            int bytesWritten = 0;

            if ((instruction.Length & 0x3F) == instruction.Length)
            {
                opCodeAndLength |= (byte) (instruction.Length & 0x3F);

                writer.Write(opCodeAndLength);
            }
            else
            {
                writer.Write(opCodeAndLength);
                WriteInt(writer, instruction.Length, out bytesWritten);
            }

            if (instruction.OpCode == SvnDiffInstructionOpCode.CopyFromSource ||
                instruction.OpCode == SvnDiffInstructionOpCode.CopyFromTarget)
            {
                WriteInt(writer, instruction.Offset, out bytesWritten);
            }
        }

        private static ulong ReadInt(BinaryReaderEOF reader)
        {
            int bytesRead;
            return ReadInt(reader, out bytesRead);
        }

        private static ulong ReadInt(BinaryReaderEOF reader, out int bytesRead)
        {
            ulong value = 0;

            bytesRead = 0;

            byte b = reader.ReadByte();
            bytesRead++;

            while ((b & 0x80) != 0)
            {
                value |= (byte)(b & 0x7F);
                value <<= 7;

                b = reader.ReadByte();
                bytesRead++;
            }

            value |= (ulong)b;

            return value;
        }

        private static void WriteInt(BinaryWriter writer, ulong value, out int bytesWritten)
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
                byte b = (byte)((byte)(value >> ((byte)count * 7)) & 0x7F);
                if (count > 0)
                {
                    b |= 0x80;
                }

                writer.Write(b);
            }
        }
    }
}