using System;
using System.Runtime.Serialization;

namespace SvnBridge.Exceptions
{
	[Serializable]
	public class CacheMissException : Exception
	{
		//
		// For guidelines regarding the creation of new exception types, see
		//    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/cpgenref/html/cpconerrorraisinghandlingguidelines.asp
		// and
		//    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/dncscol/html/csharp07192001.asp
		//

		public CacheMissException()
		{
		}

		public CacheMissException(string message) : base(message)
		{
		}

		public CacheMissException(string message, Exception inner) : base(message, inner)
		{
		}

		protected CacheMissException(
			SerializationInfo info,
			StreamingContext context) : base(info, context)
		{
		}
	}
}
