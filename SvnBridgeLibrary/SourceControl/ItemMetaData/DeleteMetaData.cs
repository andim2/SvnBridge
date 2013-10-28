namespace SvnBridge.SourceControl
{
    public class DeleteMetaData : ItemMetaData
    {
		public override string ToString()
		{
			return "Delete: " + base.ToString();
		}
    }
}