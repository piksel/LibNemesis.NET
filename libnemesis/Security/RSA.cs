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

        // plain -> public key -> encrypted bytes
        public static byte[] EncryptData(string data, byte[] key)
        {
            return EncryptData(Encoding.UTF8.GetBytes(data), key);
        }

        public static byte[] EncryptData(byte[] data, byte[] key)
        {
            byte[] cipherbytes;

            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(1024))
            {
                rsa.PersistKeyInCsp = false;
                rsa.ImportCspBlob(key);
                cipherbytes = rsa.Encrypt(data, false);
            }

            return cipherbytes;
        }

        // encrypted bytes -> private key -> plain
        public static byte[] DecryptData(byte[] data, byte[] key)
        {
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(1024))
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
