using System;
using System.Runtime.Serialization;

namespace SvnBridge.Exceptions
{
	[Serializable]
	public class EnvironmentValidationException : Exception
	{

		public EnvironmentValidationException()
		{
		}

		public EnvironmentValidationException(string message) : base(message)
		{
		}

		public EnvironmentValidationException(string message, Exception inner) : base(message, inner)
		{
		}

		protected EnvironmentValidationException(
			SerializationInfo info,
			StreamingContext context) : base(info, context)
		{
		}
	}
}