namespace SvnBridge.Interfaces
{
    /// <summary>
    /// We need this helper class so we can store a null cache value
    /// </summary>
    public class CachedResult
    {
        public object Value;

        public CachedResult(object value)
        {
            Value = value;
        }
    }
}