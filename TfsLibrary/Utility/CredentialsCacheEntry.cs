using System;
using System.Net;
using System.Xml.Serialization;

namespace CodePlex.TfsLibrary.Utility
{
    [Serializable]
    [XmlType(TypeName = "entry")]
    public class CredentialsCacheEntry : INamedEntry
    {
        string domain;
        string password;
        string url;
        string username;

        public CredentialsCacheEntry() {}

        CredentialsCacheEntry(string url,
                              string username,
                              string password,
                              string domain)
        {
            this.url = url;
            this.username = username;
            this.password = password;
            this.domain = domain;
        }

        [XmlIgnore]
        public string Domain
        {
            get { return domain; }
            set { domain = value; }
        }

        [XmlElement("domain")]
        public string Domain_Serialized
        {
            get { return EncryptionUtil.EncryptString(domain); }
            set { domain = EncryptionUtil.DecryptString(value); }
        }

        [XmlIgnore]
        string INamedEntry.Name
        {
            get { return Url; }
        }

        [XmlIgnore]
        public string Password
        {
            get { return password; }
            set { password = value; }
        }

        [XmlElement("password")]
        public string Password_Serialized
        {
            get { return EncryptionUtil.EncryptString(password); }
            set { password = EncryptionUtil.DecryptString(value); }
        }

        [XmlAttribute("url")]
        public string Url
        {
            get { return url; }
            set { url = value; }
        }

        [XmlIgnore]
        public string Username
        {
            get { return username; }
            set { username = value; }
        }

        [XmlElement("username")]
        public string Username_Serialized
        {
            get { return EncryptionUtil.EncryptString(username); }
            set { username = EncryptionUtil.DecryptString(value); }
        }

        public static CredentialsCacheEntry FromNetworkCredential(string url,
                                                                  NetworkCredential credential)
        {
            return new CredentialsCacheEntry(url, credential.UserName, credential.Password, credential.Domain);
        }

        public NetworkCredential ToNetworkCredential()
        {
            return new NetworkCredential(username, password, domain);
        }
    }
}