                namespace SvnBridge.SourceControl.Dto
{
    public sealed class Property
    {
        public string Name /* = null */;
        public string Value /* = null */;

        public Property()
        {
        }

        public Property(string name,
                        string value)
        {
            Name = name;
            Value = value;
        }
    }
}
