using System;
using System.Collections.Generic;

namespace SvnBridge.SourceControl
{
    public class MergeActivityResponse
    {
        public DateTime CreationDate;
        public string Creator;
        public List<MergeActivityResponseItem> Items = new List<MergeActivityResponseItem>();
        public int Version;

        public MergeActivityResponse(int version,
                                     DateTime creationDate,
                                     string creator)
        {
            Version = version;
            CreationDate = creationDate;
            Creator = creator;
        }
    }
}