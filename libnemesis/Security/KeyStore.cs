using System;
using System.Collections.Generic;
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
    }
}
