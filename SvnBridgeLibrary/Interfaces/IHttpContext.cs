namespace SvnBridge.Interfaces
{
    public interface IHttpContext
    {
        IHttpRequest Request { get; }
        IHttpResponse Response { get; }
    }
}
