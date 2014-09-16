using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CodePlex.TfsLibrary.Utility;

namespace CodePlex.TfsLibrary.ObjectModel
{
    public class WebTransferFormData
    {
        readonly string boundary = "--------------------------8e5m2D6l5Q4h6";
        readonly IFileSystem fileSystem;
        readonly List<IFormPart> formParts = new List<IFormPart>();

        public WebTransferFormData() {}

        public WebTransferFormData(IFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public string Boundary
        {
            get { return boundary; }
        }

        public void Add(string name,
                        string value)
        {
            formParts.Add(new StringFormPart(name, value));
        }

        public void AddFile(string name,
                            string filename)
        {
            if (fileSystem == null)
                throw new InvalidOperationException("Cannot add a file from the file system without the IFileSystem object");

            byte[] bytes;

            using (Stream stream = fileSystem.OpenFile(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                bytes = new byte[fileSystem.GetFileSize(filename)];
                stream.Read(bytes, 0, bytes.Length);
            }

            formParts.Add(new BinaryFormPart(name, bytes));
        }

        public void AddFile(string name,
                            byte[] bytes)
        {
            formParts.Add(new BinaryFormPart(name, bytes));
        }

        public void Render(Stream stream)
        {
            foreach (IFormPart formPart in formParts)
            {
                WriteString(stream, "--{0}\r\n", boundary);
                formPart.Render(stream);
                WriteString(stream, "\r\n");
            }

            WriteString(stream, "--{0}--\r\n", boundary);
        }

        internal static void WriteString(Stream stream,
                                         string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
        }

        internal static void WriteString(Stream stream,
                                         string format,
                                         params object[] args)
        {
            WriteString(stream, string.Format(format, args));
        }

        class BinaryFormPart : IFormPart
        {
            readonly byte[] content;
            readonly string filename;

            public BinaryFormPart(string filename,
                                  byte[] content)
            {
                this.filename = filename;
                this.content = content;
            }

            public void Render(Stream stream)
            {
                WriteString(stream, "Content-Disposition: form-data; name=\"content\"; filename=\"{0}\"\r\nContent-Type: application/octet-stream\r\n\r\n", filename);
                stream.Write(content, 0, content.Length);
            }
        }

        interface IFormPart
        {
            void Render(Stream stream);
        }

        class StringFormPart : IFormPart
        {
            readonly string content;
            readonly string name;

            public StringFormPart(string name,
                                  string content)
            {
                this.name = name;
                this.content = content;
            }

            public void Render(Stream stream)
            {
                WriteString(stream, "Content-Disposition: form-data; name=\"{0}\"\r\n\r\n{1}", name, content);
            }
        }
    }
}