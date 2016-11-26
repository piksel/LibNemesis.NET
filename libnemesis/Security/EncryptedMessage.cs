using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Piksel.Nemesis.Security
{
    public class EncryptedMessage
    {
        public byte[] CipherBytes { get; set; }
        public byte[] Key { get; set; }
        public byte[] IV { get; set; }
        public MessageEncryptionType EncryptionType { get; set; }
        public KeyEncryptionType KeyType { get; set; }

        private static readonly int _headerSize = 6;

        public void WriteToStream(Stream stream)
        {

            byte encryptionType = (byte)EncryptionType;
            byte keyType = (byte)KeyType;
            byte keySize = PoSizeFromByteLength(Key.Length);
            byte ivSize = (byte)IV.Length;
            byte[] dataSize = BitConverter.GetBytes( (ushort)CipherBytes.Length );

            stream.WriteByte(encryptionType);
            stream.WriteByte(keyType);
            stream.WriteByte(keySize);
            stream.WriteByte(ivSize);
            stream.Write(dataSize, 0, dataSize.Length);

            stream.Write(IV, 0, IV.Length);
            stream.Write(Key, 0, Key.Length);
            stream.Write(CipherBytes, 0, CipherBytes.Length);
        }

        public static EncryptedMessage FromStream(Stream stream)
        {
            var em = new EncryptedMessage();
            byte[] header = new byte[_headerSize];
            stream.Read(header, 0, _headerSize);

            MessageEncryptionType encryptionType = (MessageEncryptionType)header[0];
            KeyEncryptionType keyType = (KeyEncryptionType)header[1];

            int keySize = ByteLengthFromPoSize(header[2]);
            int ivSize = header[3];

            // read dataSize from header using a offset of 2
            ushort dataSize = BitConverter.ToUInt16(header, 4);

            byte[] iv = new byte[ivSize];
            stream.Read(iv, 0, ivSize);

            byte[] key = new byte[keySize];
            stream.Read(key, 0, keySize);

            byte[] data = new byte[dataSize];
            stream.Read(data, 0, dataSize);

            em.IV = iv;
            em.Key = key;
            em.CipherBytes = data;
            em.KeyType = keyType;
            em.EncryptionType = encryptionType;

            return em;
        }

        private static byte PoSizeFromByteLength(int length)
        {
            return (byte)(Math.Log((length * 8), 2));
        }

        private static int ByteLengthFromPoSize(byte posize)
        {
            return (int)(Math.Pow(2, posize) / 8);
        }
    }

    public enum MessageEncryptionType : byte
    {
        Unknown = 0xFF,
            Aes = 0x01,
           None = 0x0
    }

    public enum KeyEncryptionType : byte
    {
        Unknown = 0xFF,
            Rsa = 0x01,
           None = 0x0
    }
}
