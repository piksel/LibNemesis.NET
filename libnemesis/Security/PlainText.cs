using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Piksel.Nemesis.Security
{
    public class PlainText : IMessageEncryption
    {
        private static PlainText _default;
        public static PlainText Default
        {
            get
            {
                if (_default == null)
                    _default = new PlainText();
                return _default;
            }
        }

        public byte[] Decrypt(EncryptedMessage em)
        {
            return em.CipherBytes;
        }

        public EncryptedMessage Encrypt(byte[] input)
        {
            var em = new EncryptedMessage();
            em.EncryptionType = MessageEncryptionType.None;
            em.IV = new byte[0];
            em.Key = new byte[0];
            em.CipherBytes = input;
            return em;
        }
    }
}
