using System.Xml.Serialization;
using CodePlex.TfsLibrary.Utility;

namespace CodePlex.TfsLibrary.ClientEngine
{
    [XmlType(TypeName = "entries")]
    public class TfsStateEntryList : XmlSerializedDictionary<TfsStateEntryList, TfsStateEntry> {}
}