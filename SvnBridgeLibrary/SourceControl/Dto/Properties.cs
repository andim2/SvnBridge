namespace SvnBridge.SourceControl.Dto
{
	using System.Collections.Generic;

	public class Properties
	{
		public IDictionary<string, string> Added = new Dictionary<string, string>();
		public IList<string> Removed = new List<string>();
	}
}