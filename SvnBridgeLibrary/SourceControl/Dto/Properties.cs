namespace SvnBridge.SourceControl.Dto
{
	using System.Collections.Generic;

	public sealed class DAVPropertiesChanges
	{
		public IDictionary<string, string> Added = new Dictionary<string, string>();
		public IList<string> Removed = new List<string>();
	}
}