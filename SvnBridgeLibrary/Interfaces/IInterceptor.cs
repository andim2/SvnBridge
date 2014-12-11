namespace SvnBridge.Interfaces
{
    public interface IInterceptor
    {
        void Invoke(IInvocation invocation);
    }
}