using System;
using System.Collections.Generic;

namespace CodePlex.TfsLibrary.ObjectModel
{
    public class SourceItemHistory
    {
        public List<SourceItemChange> Changes = new List<SourceItemChange>();
        public int ChangeSetID;
        public string Comment;
        public DateTime CommitDateTime;
        public string Username;

        public SourceItemHistory() {}

        public SourceItemHistory(int changeSetID,
                                 string username,
                                 DateTime commitDateTime,
                                 string comment)
        {
            ChangeSetID = changeSetID;
            Username = username;
            CommitDateTime = commitDateTime;
            Comment = comment;
        }
    }
}