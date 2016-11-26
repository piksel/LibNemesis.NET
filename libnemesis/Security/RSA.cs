using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Piksel.Nemesis.Security
{
    public class RSA : KeyEncryptionBase
    {

        public override KeyEncryptionType Type
        {
            get { return KeyEncryptionType.Rsa; }
        }

        private static RSA _default;
        public static RSA Default
        {
            get
            {
                if (_default == null)
                    _default = new RSA();
                return _default;
            }
        }

        public override void CreateNewKeyPair(IKeyStore keyStore, int keySize = 1024)
        {

            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(keySize))
            {
                rsa.PersistKeyInCsp = false;

                keyStore.PublicKey.Key = rsa.ExportCspBlob(false);
                keyStore.PrivateKey.Key = rsa.ExportCspBlob(true);
                keyStore.KeySize = keySize;
            }
        }

        public override byte[] CreateNewKeyPair(int keySize = 1024)
        {
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(keySize))
            {
                rsa.PersistKeyInCsp = false;
                return  rsa.ExportCspBlob(true);
            }
        }

        public override string ExportToXml(byte[] key, bool includePrivate = true, int keySize = 1024)
        {
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(keySize))
            {
                rsa.PersistKeyInCsp = false;
                rsa.ImportCspBlob(key);
                return rsa.ToXmlString(includePrivate);
            }
        }

        public override void ImportFromXml(IKeyStore keyStore, string xml, int keySize = 1024)
        {
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(keySize))
            {
                rsa.PersistKeyInCsp = false;
                rsa.FromXmlString(xml);
                keyStore.PublicKey.Key = rsa.ExportCspBlob(false);
                if(!rsa.PublicOnly)
                    keyStore.PrivateKey.Key = rsa.ExportCspBlob(true);
            }
        }

        public override void ImportFromBytes(IKeyStore keyStore, byte[] bytes, int keySize = 1024)
        {
            
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(keySize))
            {
                
                rsa.PersistKeyInCsp = false;
                rsa.ImportCspBlob(bytes);
                keyStore.PublicKey.Key = rsa.ExportCspBlob(false);
                if (!rsa.PublicOnly)
                
                    keyStore.PrivateKey.Key = bytes;
            }

        }

        public override byte[] GetPublicKey(byte[] privateKey, int keySize = 1024)
        {
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(keySize))
            {
                rsa.PersistKeyInCsp = false;
                rsa.ImportCspBlob(privateKey);
                return rsa.ExportCspBlob(false);
            }
        }



        public override byte[] EncryptData(byte[] data, byte[] key, int keySize = 1024)
        {
            byte[] cipherbytes;

            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(keySize))
            {
                rsa.PersistKeyInCsp = false;
                rsa.ImportCspBlob(key);
                cipherbytes = rsa.Encrypt(data, true);
            }

            return cipherbytes;
        }

        // encrypted bytes -> private key -> plain
        public override byte[] DecryptData(byte[] data, byte[] key, int keySize = 1024)
        {
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(keySize))
            {
                try
                {
                    rsa.PersistKeyInCsp = false;
                    rsa.ImportCspBlob(key);
                    return rsa.Decrypt(data, true);
                }
                catch (Exception x)
                {
                    var a = x;
                    return new byte[0];
                }
            }
        }

    }
}
