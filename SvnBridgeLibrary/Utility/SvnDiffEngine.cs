using System;
using System.IO;
using System.Collections.Generic;

namespace SvnBridge.Utility
{
    internal class BinaryReaderSvnDiffEOF : BinaryReaderEOF
    {
        public BinaryReaderSvnDiffEOF(Stream input)
            : base(input)
        {
        }
    }

    internal class BinaryReaderSvnDiffEOFViaPositionCheck : BinaryReader
    {
        public BinaryReaderSvnDiffEOFViaPositionCheck(Stream input)
            : base(input)
        {
        }

        public bool EOF
        {
            get
            {
                var stream = BaseStream;
                return (stream.Position >= stream.Length);
            }
        }
    }

    /// <summary>
    /// Alias class (~ typedef, "using").
    /// </summary>
    internal class BinaryReaderSvnDiff : BinaryReaderSvnDiffEOF
    {
        public BinaryReaderSvnDiff(Stream input)
            : base(input)
        {
        }
    }

    public sealed class SvnDiffEngine
    {
        private const int BUFFER_EXPAND_SIZE = Constants.AllocSize_AvoidLOHCatastrophy;

        public static byte[] ApplySvnDiff(SvnDiffWindow svnDiffWindow, byte[] source, int sourceDataStartIndex)
        {
            var instructionSection = svnDiffWindow.InstructionSectionBytes;
            MemoryStream instructionStream = new MemoryStream(instructionSection.Array, instructionSection.Offset, instructionSection.Count, false);
            var dataSection = svnDiffWindow.DataSectionBytes;
            MemoryStream dataStream = new MemoryStream(dataSection.Array, dataSection.Offset, dataSection.Count, false);
            BinaryReader dataReader = new BinaryReader(dataStream);

            //BinaryReaderSvnDiff instructionReader = new BinaryReaderSvnDiff(instructionStream);
            using (BinaryReaderSvnDiff instructionReader = new BinaryReaderSvnDiff(instructionStream))
            {
                return ApplySvnDiffInstructions(
                    instructionReader,
                    dataReader,
                    source,
                    sourceDataStartIndex);
            }
        }

        private static byte[] ApplySvnDiffInstructions(
            BinaryReaderSvnDiff instructionReader,
            BinaryReader dataReader,
            byte[] source,
            int sourceDataStartIndex)
        {
            byte[] buffer = new byte[BUFFER_EXPAND_SIZE];
            int targetIndex = 0;

            for (; ; )
            {
                SvnDiffInstruction instruction = ReadInstruction(instructionReader);

                bool haveInstruction = (null != instruction);
                if (!(haveInstruction))
                {
                    break;
                }

                EnsureRequiredLengthOfWorkBuffer(
                    ref buffer,
                    targetIndex + (int) instruction.Length);

                ApplySvnDiffInstruction(
                    instruction,
                    dataReader,
                    source,
                    sourceDataStartIndex,
                    buffer,
                    ref targetIndex);
            }

            Array.Resize(ref buffer, targetIndex);
            return buffer;
        }

        private static void EnsureRequiredLengthOfWorkBuffer(
            ref byte[] buffer,
            int requiredLength)
        {
            if (requiredLength > buffer.Length)
            {
                // Figure out new _exact_ multiple of request size (avoid LOH fragmentation!!):
                int oldLength = buffer.Length;
                int newLength = Helper.ValueAlignNext(
                    oldLength,
                    requiredLength,
                    BUFFER_EXPAND_SIZE);

                Array.Resize(ref buffer, newLength);
            }
        }

        private static void ApplySvnDiffInstruction(
            SvnDiffInstruction instruction,
            BinaryReader dataReader,
            byte[] source,
            int sourceDataStartIndex,
            byte[] buffer,
            ref int targetIndex)
        {
            int instructionLength = (int) instruction.Length;
            int instructionOffs = (int) instruction.Offset;
            switch (instruction.OpCode)
            {
                case SvnDiffInstructionOpCode.CopyFromSource:
                    Array.Copy(source,
                               instructionOffs + sourceDataStartIndex,
                               buffer,
                               targetIndex,
                               instructionLength);
                    break;

                case SvnDiffInstructionOpCode.CopyFromTarget:
                    // Cannot use Array.Copy because Offset + Length may be greater than starting targetIndex
                    for (int i = 0; i < instructionLength; i++)
                    {
                        buffer[targetIndex + i] = buffer[instructionOffs + i];
                    }
                    break;

                case SvnDiffInstructionOpCode.CopyFromNewData:
                    //byte[] newData = dataReader.ReadBytes(instructionLength);
                    //Array.Copy(newData, 0, buffer, targetIndex, newData.Length);
                    dataReader.BaseStream.Read(buffer, targetIndex, instructionLength);
                    break;

                default:
                    // http://stackoverflow.com/questions/1709894/c-sharp-switch-statement
                    throw new NotImplementedException();
                    //break;
            }
            targetIndex += instructionLength;
        }

        /// <summary>
        /// Deprecated API variant (prefer efficiently forwarding ArraySegment-based one).
        /// </summary>
        public static SvnDiffWindow CreateReplaceDiff(byte[] data, int index, int length)
        {
            return CreateReplaceDiff(new ArraySegment<byte>(data, index, length));
        }

        public static SvnDiffWindow CreateReplaceDiff(ArraySegment<byte> arrSeg)
        {
            SvnDiffWindow svnDiff = null;
            var length = arrSeg.Count;
            if (length > 0)
            {
                svnDiff = new SvnDiffWindow();

                svnDiff.SourceViewOffset = 0;
                svnDiff.SourceViewLength = 0;
                svnDiff.TargetViewLength = (ulong)length;

                SvnDiffInstruction instruction = new SvnDiffInstruction();
                instruction.OpCode = SvnDiffInstructionOpCode.CopyFromNewData;
                instruction.Length = (ulong)length;

                MemoryStream replaceDiffStream = new Utility.MemoryStreamLOHSanitized();
                using (BinaryWriter replaceDiffWriter = new BinaryWriter(replaceDiffStream))
                {
                    replaceDiffStream.Write(arrSeg.Array, arrSeg.Offset, arrSeg.Count);
                    svnDiff.DataSectionBytes = new ArraySegment<byte>(replaceDiffStream.GetBuffer(), 0, arrSeg.Count);

                    var idxPrev = svnDiff.DataSectionBytes.Count;
                    WriteInstruction(replaceDiffWriter, instruction);
                    svnDiff.InstructionSectionBytes = new ArraySegment<byte>(replaceDiffStream.GetBuffer(), idxPrev, (int)replaceDiffStream.Length - idxPrev);
                    // Flush() (and Close()) guaranteed by "using"
                }
            }
            return svnDiff;
        }

        public static SvnDiffWindow[] ParseSvnDiff(Stream inputStream)
        {
            BinaryReaderSvnDiff reader = new BinaryReaderSvnDiff(inputStream);

            CheckSvnDiffSignatureAndSupportedVersion(reader);

            List<SvnDiffWindow> diffs = new List<SvnDiffWindow>();
            while (HaveDataRemain(reader))
            {
                SvnDiffWindow diff = new SvnDiffWindow();

                diff.SourceViewOffset = ReadInt(reader);
                diff.SourceViewLength = ReadInt(reader);
                diff.TargetViewLength = ReadInt(reader);
                int instructionSectionLength = (int)ReadInt(reader);
                int dataSectionLength = (int)ReadInt(reader);

                var instructionPlusData = reader.ReadBytes(instructionSectionLength + dataSectionLength);
                diff.InstructionSectionBytes = new ArraySegment<byte>(instructionPlusData, 0, instructionSectionLength);
                diff.DataSectionBytes = new ArraySegment<byte>(instructionPlusData, instructionSectionLength, dataSectionLength);

                diffs.Add(diff);
            }
            return diffs.ToArray();
        }

        private static void CheckSvnDiffSignatureAndSupportedVersion(BinaryReaderSvnDiff reader)
        {
            byte[] signature = reader.ReadBytes(3);
            byte version = reader.ReadByte();

            // See Subversion files notes/svndiff and svndiff.c.
            bool isValidSignature = (signature[0] == 'S' && signature[1] == 'V' && signature[2] == 'N');
            if (!(isValidSignature))
            {
                throw new InvalidOperationException("The signature is invalid.");
            }
            bool isSupportedVersion = (version == 0);
            if (!(isSupportedVersion))
            {
                throw new NotSupportedException("Unsupported SVN diff version");
            }
        }

        public static void WriteSvnDiffSignature(BinaryWriter output)
        {
            byte[] signature = new byte[] { (byte)'S', (byte)'V', (byte)'N' };
            byte version = 0;
            output.Write(signature);
            output.Write(version);
        }

        public static void WriteSvnDiffWindow(SvnDiffWindow svnDiff, BinaryWriter output)
        {
            if (svnDiff != null)
            {
                var instructionSection = svnDiff.InstructionSectionBytes;
                var dataSection = svnDiff.DataSectionBytes;
                int bytesWritten;
                WriteInt(output, svnDiff.SourceViewOffset, out bytesWritten);
                WriteInt(output, svnDiff.SourceViewLength, out bytesWritten);
                WriteInt(output, svnDiff.TargetViewLength, out bytesWritten);
                WriteInt(output, (ulong)instructionSection.Count, out bytesWritten);
                WriteInt(output, (ulong)dataSection.Count, out bytesWritten);

                output.Write(instructionSection.Array, instructionSection.Offset, instructionSection.Count);
                output.Write(dataSection.Array, dataSection.Offset, dataSection.Count);

                output.Flush(); // Likely very important in case of huge-data memory pressure (frees internally amassed buffer data?)
            }
        }

        private static SvnDiffInstruction ReadInstruction(BinaryReaderSvnDiff reader)
        {
            if (!(HaveDataRemain(reader)))
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

        private static void WriteInstruction(BinaryWriter output, SvnDiffInstruction instruction)
        {
            byte opCodeAndLength = (byte) ((int) instruction.OpCode << 6);
            int bytesWritten = 0;

            if ((instruction.Length & 0x3F) == instruction.Length)
            {
                opCodeAndLength |= (byte) (instruction.Length & 0x3F);

                output.Write(opCodeAndLength);
            }
            else
            {
                output.Write(opCodeAndLength);
                WriteInt(output, instruction.Length, out bytesWritten);
            }

            if (instruction.OpCode == SvnDiffInstructionOpCode.CopyFromSource ||
                instruction.OpCode == SvnDiffInstructionOpCode.CopyFromTarget)
            {
                WriteInt(output, instruction.Offset, out bytesWritten);
            }
        }

        private static ulong ReadInt(BinaryReaderSvnDiff reader)
        {
            int bytesRead;
            return ReadInt(reader, out bytesRead);
        }

        private static ulong ReadInt(BinaryReaderSvnDiff reader, out int bytesRead)
        {
            ulong value = 0;

            bytesRead = 0;

            byte b = reader.ReadByte();
            ++bytesRead;

            while ((b & 0x80) != 0)
            {
                value |= (byte)(b & 0x7F);
                value <<= 7;

                b = reader.ReadByte();
                ++bytesRead;
            }

            value |= (ulong)b;

            return value;
        }

        private static void WriteInt(BinaryWriter output, ulong intValue, out int bytesWritten)
        {
            int count = 1;
            ulong temp = intValue >> 7;
            while (temp > 0)
            {
                temp = temp >> 7;
                ++count;
            }

            bytesWritten = count;
            while (--count >= 0)
            {
                byte b = (byte)((byte)(intValue >> ((byte)count * 7)) & 0x7F);
                if (count > 0)
                {
                    b |= 0x80;
                }

                output.Write(b);
            }
        }

        /// <summary>
        /// Determines whether the underlying stream of this reader
        /// has some data remaining to be read.
        /// </summary>
        /// <remarks>
        /// Condition determined by using .Position and .Length properties.
        /// Doing such checks will fail for those stream types
        /// which don't support seeking, though.
        /// See also
        /// http://stackoverflow.com/questions/3752968/endofstream-for-binaryreader
        /// </remarks>
        private static bool HaveDataRemain(BinaryReaderSvnDiff reader)
        {
            bool haveDataRemain;

            haveDataRemain = !(reader.EOF);

            return haveDataRemain;
        }
    }
}
