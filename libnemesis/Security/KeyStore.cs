using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Piksel.Nemesis.Security
{
    public interface IKeyStore
    {
        RSAKey PrivateKey { get; set; }
        RSAKey PublicKey { get; set; }
        int KeySize { get; set; }

        bool Available { get; set; }
        void Save();
        void Load();
    }

    public class RSAKey
    {
        public byte[] Key;// { get; set; }
        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach(byte b in Key)
            {
                sb.Append(b.ToString("x"));
            }
            return sb.ToString();
        }
    }

    public class MemoryKeyStore : IKeyStore
    {
        public RSAKey PrivateKey { get; set; }
        public RSAKey PublicKey { get; set; }
        public int KeySize { get; set; }

        public bool Initialized { get { return PublicKey.Key != null && PublicKey.Key.Length > 0; } }

        private IKeyEncryption keyEncryption;

        public MemoryKeyStore(IKeyEncryption keyEncryption)
        {
            PrivateKey = new RSAKey();
            PublicKey = new RSAKey();
            this.keyEncryption = keyEncryption;
        }

        public void Load()
        {
            if(!Initialized)
                keyEncryption.CreateNewKeyPair(this);
        }

        public void Load(byte[] cspBytes)
        {
            keyEncryption.ImportFromBytes(this, cspBytes);
        }

        public void Save()
        {
            // Nothing to do here
        }

        public void Save(out byte[] bytes, bool onlyPublic = false)
        {
            bytes = onlyPublic ? PublicKey.Key : PrivateKey.Key;
        }

        public bool Available
        {
            get { return true; }
            set { return; }
        }
    }

    public class XMLFileKeyStore: IKeyStore
    {
        public RSAKey PrivateKey { get; set; }
        public RSAKey PublicKey { get; set; }
        public int KeySize { get; set; }

        public string FileName { get; set; }

        public bool CreateIfNotExist;
        private IKeyEncryption keyEncryption;

        public XMLFileKeyStore(string filename, IKeyEncryption keyEncryption, bool createIfNotExist = false)
        {
            PrivateKey = new RSAKey();
            PublicKey = new RSAKey();
            FileName = filename;
            CreateIfNotExist = createIfNotExist;
            this.keyEncryption = keyEncryption;

            Load();
        }

        public bool Available
        {
            get
            {
                return File.Exists(FileName);
            }
            set { return; }
        }

        public void Load()
        {
            if (Available)
            {
                using (var sr = File.OpenText(FileName))
                {
                    keyEncryption.ImportFromXml(this, sr.ReadToEnd());
                }
            }
            else if(CreateIfNotExist)
            {
                keyEncryption.CreateNewKeyPair(this);
            }
        }

        public void Save()
        {
            using(var sw = File.CreateText(FileName))
            {
                sw.Write(keyEncryption.ExportToXml(PrivateKey.Key, true));
            }
        }
    }
}
