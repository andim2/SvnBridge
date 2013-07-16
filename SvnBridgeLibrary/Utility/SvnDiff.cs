namespace SvnBridge.Utility
{
    public sealed class SvnDiffWindow
    {
        public ulong SourceViewOffset;
        public ulong SourceViewLength;
        public ulong TargetViewLength;
        public byte[] InstructionSectionBytes;
        public byte[] DataSectionBytes;
    }
}
