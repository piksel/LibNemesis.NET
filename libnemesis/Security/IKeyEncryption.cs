namespace Piksel.Nemesis.Security
{
    public interface IKeyEncryption
    {
        void CreateNewKeyPair(IKeyStore keyStore, int keySize = 1024);
        byte[] CreateNewKeyPair(int keySize = 1024);

        string ExportToXml(byte[] key, bool includePrivate = true, int keySize = 1024);
        void ImportFromXml(IKeyStore keyStore, string xml, int keySize = 1024);
        void ImportFromBytes(IKeyStore keyStore, byte[] bytes, int keySize = 1024);

        byte[] GetPublicKey(byte[] privateKey, int keySize = 1024);

        byte[] EncryptData(byte[] data, byte[] key, int keySize = 1024);
        byte[] DecryptData(byte[] data, byte[] key, int keySize = 1024);

        KeyEncryptionType Type { get; }
    }
}