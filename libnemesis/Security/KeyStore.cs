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
        public byte[] Key { get; set; }
    }

    public class MemoryKeyStore : IKeyStore
    {
        public RSAKey PrivateKey { get; set; }
        public RSAKey PublicKey { get; set; }
        public int KeySize { get; set; }

        public MemoryKeyStore()
        {
            PrivateKey = new RSAKey();
            PublicKey = new RSAKey();
        }

        public void Load()
        {
            RSA.CreateNewKeyPair(this);
        }

        public void Save()
        {
            // Nothing to do here
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

        public XMLFileKeyStore(string filename, bool createIfNotExist = false)
        {
            PrivateKey = new RSAKey();
            PublicKey = new RSAKey();
            FileName = filename;
            CreateIfNotExist = createIfNotExist;

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
                    RSA.ImportFromXml(this, sr.ReadToEnd());
                }
            }
            else if(CreateIfNotExist)
            {
                RSA.CreateNewKeyPair(this);
            }
        }

        public void Save()
        {
            using(var sw = File.CreateText(FileName))
            {
                sw.Write(RSA.ExportToXml(PrivateKey.Key, true));
            }
        }
    }
}
