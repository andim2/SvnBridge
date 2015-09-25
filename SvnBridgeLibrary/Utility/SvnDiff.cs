using System; // ArraySegment

namespace SvnBridge.Utility
{
    public sealed class SvnDiffWindow
    {
        public ulong SourceViewOffset;
        public ulong SourceViewLength;
        public ulong TargetViewLength;
        public ArraySegment<byte> InstructionSectionBytes;
        public ArraySegment<byte> DataSectionBytes;
    }
}
