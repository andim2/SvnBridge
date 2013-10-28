using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml.Serialization;
using SvnBridge.Infrastructure;
using SvnBridge.Interfaces;

namespace SvnBridge.Proxies
{
	[DebuggerStepThrough]
	public class TracingInterceptor : IInterceptor
	{
		private static IDictionary<Type, XmlSerializer> typeToXmlSerializers = new Dictionary<Type, XmlSerializer>();

        private readonly DefaultLogger logger;

        public TracingInterceptor(DefaultLogger logger)
		{
			this.logger = logger;
		}

		public void Invoke(IInvocation invocation)
		{
			if (Logging.MethodTraceEnabled)
				TraceCallAndParamters(invocation);

			invocation.Proceed();
		}

		private void TraceCallAndParamters(IInvocation invocation)
		{
			List<string> args= new List<string>();
			foreach (object arg in invocation.Arguments)
			{
				if(arg==null)
				{
					args.Add("null");
					continue;
				}
				XmlSerializer serializer = TryGetSerializer(arg);
				if(serializer==null)
				{
					args.Add(arg.ToString());
					continue;
				}
				StringWriter sw = new StringWriter();
				serializer.Serialize(sw, arg);
				args.Add(sw.GetStringBuilder().ToString());
			}

			logger.Trace("{0}({1});", invocation.Method.Name, string.Join(", ", args.ToArray()));
		}

		private static XmlSerializer TryGetSerializer(object arg)
		{
			XmlSerializer value = null;
			Type type = arg.GetType();
			if (typeToXmlSerializers.TryGetValue(type, out value))
				return value;
			lock (typeToXmlSerializers)
			{
				if(typeToXmlSerializers.TryGetValue(type, out value))
					return value;
				object[] attributes = type.GetCustomAttributes(true);
				bool xmlSerializable = Array.Exists(attributes, delegate(object o)
				{
					return o is XmlRootAttribute || o is XmlElementAttribute;
				});
				if (xmlSerializable)
					typeToXmlSerializers[type] = value = new XmlSerializer(type);
				else
					typeToXmlSerializers[type] = value = null;
			}
			return value;
		}
	}
}