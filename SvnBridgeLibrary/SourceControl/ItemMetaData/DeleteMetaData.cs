namespace SvnBridge.SourceControl
{
    public sealed class DeleteMetaData : ItemMetaData
    {
		public override string ToString()
		{
			return "Delete: " + base.ToString();
		}
    }
}