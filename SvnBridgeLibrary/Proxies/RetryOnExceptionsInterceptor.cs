using System;
using System.Diagnostics;
using System.Threading;
using SvnBridge.Infrastructure;
using SvnBridge.Interfaces;

namespace SvnBridge.Proxies
{
	[DebuggerStepThrough]
	public class RetryOnExceptionsInterceptor<TException> : IInterceptor
	   where TException : Exception
	{
        private readonly DefaultLogger logger;

        public RetryOnExceptionsInterceptor(DefaultLogger logger)
		{
			this.logger = logger;
		}

		public void Invoke(IInvocation invocation)
		{
			Exception exception = null;
			for (int i = 0; i < 3; i++)
			{
				try
				{
					invocation.Proceed();
					return;
				}
				catch (TException we)
				{
					exception = we;
					// we will retry here, since we assume that the failure is trasient
					logger.Info("Exception occured, attempt #" + (i + 1) + ", retrying...", we);
				}

				// if we are here we got an exception, we will assume this is a
				// trasient situation and wait a bit, hopefully it will clear up
				Thread.Sleep(100);
			}
			if (exception == null)
				return;
			logger.Error("All retries failed", exception);
			ExceptionHelper.PreserveStackTrace(exception);
			throw exception;
		}
	}
}
