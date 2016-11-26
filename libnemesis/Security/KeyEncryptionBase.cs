using System;
using System.Text;

namespace Piksel.Nemesis.Security
{
    public abstract class KeyEncryptionBase : IKeyEncryption
    {
        public abstract KeyEncryptionType Type { get; }

        // helpers
        public byte[] EncryptData(string data, byte[] key)
        {
            return EncryptData(Encoding.UTF8.GetBytes(data), key);
        }

        public string DecryptDataString(byte[] data, byte[] key)
        {
            return Encoding.UTF8.GetString(DecryptData(data, key));
        }

        // base implementation

        public virtual byte[] CreateNewKeyPair(int keySize = 1024)
        {
            return new byte[0];
        }

        public virtual void CreateNewKeyPair(IKeyStore keyStore, int keySize = 1024) { }



        public virtual byte[] EncryptData(byte[] data, byte[] key, int keySize = 1024)
        {
            return data;
        }

        public virtual string ExportToXml(byte[] key, bool includePrivate = true, int keySize = 1024)
        {
            return string.Empty;
        }

        public virtual byte[] GetPublicKey(byte[] privateKey, int keySize = 1024)
        {
            return new byte[0];
        }

        public virtual void ImportFromBytes(IKeyStore keyStore, byte[] bytes, int keySize = 1024)
        {
        }

        public virtual void ImportFromXml(IKeyStore keyStore, string xml, int keySize = 1024)
        {
        }

        public virtual byte[] DecryptData(byte[] data, byte[] key, int keySize = 1024)
        {
            return data;
        }
    }
}