using System.IO;
using System.Xml.Serialization;

namespace CodePlex.TfsLibrary.Utility
{
    public class XmlSerializationRoot<TEntries>
        where TEntries : new()
    {
        public static TEntries Deserialize(IFileSystem fileSystem,
                                           string path)
        {
            if (!fileSystem.FileExists(path))
                return new TEntries();

            XmlSerializer ser = new XmlSerializer(typeof(TEntries));

            using (Stream stream = fileSystem.OpenFile(path, FileMode.Open, FileAccess.Read, FileShare.None))
                return (TEntries)ser.Deserialize(stream);
        }

        public void Serialize(IFileSystem fileSystem,
                              string path)
        {
            XmlSerializer ser = new XmlSerializer(typeof(TEntries));

            FileMode fileMode = FileMode.Create;

            using (Stream stream = fileSystem.OpenFile(path, fileMode, FileAccess.Write, FileShare.None))
                ser.Serialize(stream, this);
        }
    }
}