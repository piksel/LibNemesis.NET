using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Piksel.Nemesis.Security
{
    public class Rijndael
    {
        public static EncryptedMessage Encrypt(byte[] input)
        {
            var em = new EncryptedMessage();
            using (var aes = new AesManaged())
            {
                aes.KeySize = 256;

                aes.GenerateIV();
                aes.GenerateKey();

                em.IV = aes.IV;
                em.Key = aes.Key;
                em.EncryptionType = MessageEncryptionType.Aes;

                using (var encryptor = aes.CreateEncryptor())
                {
                    using (var msOutput = new MemoryStream())
                    {
                        using (var cryptoStream = new CryptoStream(msOutput, encryptor, CryptoStreamMode.Write)) {
                            using (var msInput = new MemoryStream(input)) {
                                msInput.CopyTo(cryptoStream);
                            }
                        }
                        em.CipherBytes = msOutput.ToArray();
                    }
                }

                return em;
            }
        }

        public static byte[] Decrypt(EncryptedMessage em)
        {
            using (var aes = new AesManaged())
            {
                aes.Key = em.Key;
                aes.IV = em.IV;

                using (var decryptor = aes.CreateDecryptor())
                {
                    using (var msInput = new MemoryStream(em.CipherBytes))
                    {
                        using (var cryptoStream = new CryptoStream(msInput, decryptor, CryptoStreamMode.Read))
                        {
                            using (var msOutput = new MemoryStream())
                            {
                                cryptoStream.CopyTo(msOutput);
                                return msOutput.ToArray();
                            }
                        }
                    }
                }
            }
        }
        
    }
}
