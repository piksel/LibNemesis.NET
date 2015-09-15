using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Piksel.Nemesis.Security
{
    public static class RSA
    {

        public static void CreateNewKeyPair(IKeyStore keyStore, int keySize = 1024)
        {

            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(keySize))
            {
                rsa.PersistKeyInCsp = false;

                keyStore.PublicKey.Key = rsa.ExportCspBlob(false);
                keyStore.PrivateKey.Key = rsa.ExportCspBlob(true);
                keyStore.KeySize = keySize;
            }
        }

        public static string ExportToXml(byte[] key, bool includePrivate = true, int keySize = 1024)
        {
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(keySize))
            {
                rsa.PersistKeyInCsp = false;
                rsa.ImportCspBlob(key);
                return rsa.ToXmlString(includePrivate);
            }
        }

        public static void ImportFromXml(IKeyStore keyStore, string xml, int keySize = 1024)
        {
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(keySize))
            {
                rsa.PersistKeyInCsp = false;
                rsa.FromXmlString(xml);
                keyStore.PublicKey.Key = rsa.ExportCspBlob(false);
                keyStore.PrivateKey.Key = rsa.ExportCspBlob(true);
            }
        }

        public static byte[] GetPublicKey(byte[] privateKey, int keySize = 1024)
        {
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(keySize))
            {
                rsa.PersistKeyInCsp = false;
                rsa.ImportCspBlob(privateKey);
                return rsa.ExportCspBlob(false);
            }
        }

        // plain -> public key -> encrypted bytes
        public static byte[] EncryptData(string data, byte[] key)
        {
            return EncryptData(Encoding.UTF8.GetBytes(data), key);
        }

        public static byte[] EncryptData(byte[] data, byte[] key, int keySize = 1024)
        {
            byte[] cipherbytes;

            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(keySize))
            {
                rsa.PersistKeyInCsp = false;
                rsa.ImportCspBlob(key);
                cipherbytes = rsa.Encrypt(data, false);
            }

            return cipherbytes;
        }

        // encrypted bytes -> private key -> plain
        public static byte[] DecryptData(byte[] data, byte[] key, int keySize = 1024)
        {
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(keySize))
            {
                rsa.PersistKeyInCsp = false;

                //byte[] encodedCipherText = Convert.FromBase64String(data);
                rsa.ImportCspBlob(key);
                return rsa.Decrypt(data, false);
            }
        }

        public static string DecryptDataString(byte[] data, byte[] key)
        {
            return Encoding.UTF8.GetString(DecryptData(data, key));
        }

    }
}
