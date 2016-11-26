namespace Piksel.Nemesis.Security
{
    public interface IMessageEncryption
    {
        EncryptedMessage Encrypt(byte[] input);
        byte[] Decrypt(EncryptedMessage em);
    }
}