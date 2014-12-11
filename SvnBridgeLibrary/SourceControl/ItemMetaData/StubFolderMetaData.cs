using System.Collections.Generic;

namespace SvnBridge.SourceControl
{
	public class StubFolderMetaData : FolderMetaData
	{
		FolderMetaData realFolder;

		public FolderMetaData RealFolder
		{
			get { return realFolder; }
			set
			{
				Id = value != null ? value.Id : 0;
				realFolder = value;
			}
		}

		public StubFolderMetaData()
		{

		}

		public StubFolderMetaData(string name)
			: base(name)
		{
		}


		public override IList<ItemMetaData> Items
		{
			get { return RealFolder.Items; }
		}
	}
}