using System.Xml.Serialization;

namespace CodePlex.TfsLibrary.Utility
{
    [XmlType(TypeName = "entries")]
    public class CredentialsCacheEntries : XmlSerializedDictionary<CredentialsCacheEntries, CredentialsCacheEntry> {}
}