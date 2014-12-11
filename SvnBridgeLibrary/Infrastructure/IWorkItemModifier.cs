namespace SvnBridge.Infrastructure
{
    public interface IWorkItemModifier
    {
        void Associate(int workItemId, int changeSetId);
        void SetWorkItemFixed(int workItemId, int changeSetId);
    }
}