using System;
using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using SvnBridge.SourceControl;

namespace Tests
{
    public static class TestHelper
    {
        public static SourceItemChange MakeChange(ChangeType changeType,
                                                  string serverPath)
        {
            SourceItemChange result = new SourceItemChange();
            result.Item = SourceItem.FromRemoteItem(0, ItemType.Folder, serverPath, 0, 0, DateTime.Now, null);
            result.ChangeType = changeType;
            return result;
        }

        public static SourceItemChange MakeChange(ChangeType changeType,
                                                  string serverPath,
                                                  string originalPath,
                                                  int originalRevision)
        {
            SourceItemChange result = MakeChange(changeType, serverPath);
            result.Item = new RenamedSourceItem(result.Item, originalPath, originalRevision);
            return result;
        }

        /// <summary>
        /// Very convenient (if dirty) helper.
        /// Definitely make sure to provide both strings, for convenient direct comparison.
        /// </summary>
        /// <param name="expected">Expected content</param>
        /// <param name="actual">Actual content</param>
        public static void AnalyzeStringContentAsHex(string expected, string actual)
        {
            System.Text.UTF8Encoding encoding = new System.Text.UTF8Encoding();
            //System.Diagnostics.Debugger.Launch();
            string expected_hex = BitConverter.ToString(encoding.GetBytes(expected));
            string actual_hex = BitConverter.ToString(encoding.GetBytes(actual));
        }
    }
}
