using System.Collections.Generic;

namespace SvnBridge.SourceControl.Dto
{
    public class Activity
    {
		public readonly Dictionary<string, Properties> Properties = new Dictionary<string, Properties>();

        public readonly List<string> Collections = new List<string>();
        public readonly List<CopyAction> CopiedItems = new List<CopyAction>();
        public readonly List<string> DeletedItems = new List<string>();
        public readonly List<ActivityItem> MergeList = new List<ActivityItem>();
        public readonly List<string> PostCommitDeletedItems = new List<string>();
        public string Comment;
    }
}