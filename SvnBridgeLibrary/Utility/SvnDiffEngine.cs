using System;
using System.IO;

namespace SvnBridge.Utility
{
    public class SvnDiffEngine
    {
        public static byte[] ApplySvnDiff(SvnDiff svnDiff,
                                          byte[] source,
                                          int sourceDataStartIndex)
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

        public static SvnDiff CreateReplaceDiff(byte[] bytes)
        {
            SvnDiff svnDiff = null;
            if (bytes.Length > 0)
            {
                svnDiff = new SvnDiff();

                svnDiff.SourceViewOffset = 0;
                svnDiff.SourceViewLength = 0;
                svnDiff.TargetViewLength = (ulong) bytes.Length;

                MemoryStream instructionStream = new MemoryStream();
                BinaryWriter instructionWriter = new BinaryWriter(instructionStream);
                MemoryStream dataStream = new MemoryStream();
                BinaryWriter dataWriter = new BinaryWriter(dataStream);

                dataWriter.Write(bytes);
                dataWriter.Flush();

                svnDiff.DataSectionBytes = dataStream.ToArray();

                SvnDiffInstruction instruction = new SvnDiffInstruction();
                instruction.OpCode = SvnDiffInstructionOpCode.CopyFromNewData;
                instruction.Length = (ulong) bytes.Length;

                WriteInstruction(instructionWriter, instruction);
                instructionWriter.Flush();

                svnDiff.InstructionSectionBytes = instructionStream.ToArray();
            }
            return svnDiff;
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
                instruction.Length = SvnDiffParser.ReadInt(reader);
            }
            else
            {
                instruction.Length = length;
            }

            if (instruction.OpCode == SvnDiffInstructionOpCode.CopyFromSource ||
                instruction.OpCode == SvnDiffInstructionOpCode.CopyFromTarget)
            {
                instruction.Offset = SvnDiffParser.ReadInt(reader);
            }

            return instruction;
        }

        private static void WriteInstruction(BinaryWriter writer,
                                             SvnDiffInstruction instruction)
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
                SvnDiffParser.WriteInt(writer, instruction.Length, out bytesWritten);
            }

            if (instruction.OpCode == SvnDiffInstructionOpCode.CopyFromSource ||
                instruction.OpCode == SvnDiffInstructionOpCode.CopyFromTarget)
            {
                SvnDiffParser.WriteInt(writer, instruction.Offset, out bytesWritten);
            }
        }
    }
}