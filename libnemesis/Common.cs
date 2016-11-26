using NLog;
using Piksel.Nemesis.Security;
using Piksel.Nemesis.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Piksel.Nemesis
{
    public class QueuedCommand
    {
        public string CommandString { get; set; }
        public TaskCompletionSource<string> ResultSource { get; set; }
        public Guid NodeId { get; set; }
    }

    public class CommandReceivedEventArgs: EventArgs
    {
        public string Command;
        public TaskCompletionSource<string> ResultSource { get; set; }
        public Guid NodeId { get; set; }
    }

    public delegate void CommandReceivedEventHandler(object sender, CommandReceivedEventArgs e);

    public abstract class NemesisBase
    {
        protected ILogger _log {
            get; set;
        }

        public void SetLogName(string name)
        {
            _log = LogManager.GetLogger(name);
        }

        public event CommandReceivedEventHandler CommandReceived;

        public int ReadTimeout { get; set; }
        public int WriteTimeout { get; set; }

        protected abstract void encryptKey(ref EncryptedMessage em, Guid remoteId);

        protected EncryptedMessage encryptMessage(QueuedCommand qc)
        {
            return encryptMessage(qc.CommandString, qc.NodeId);
        }

        protected EncryptedMessage encryptMessage(string message, Guid remoteId)
        {
            // encrypt message 
            var em = MessageEncryption.Encrypt(Encoding.UTF8.GetBytes(message));

            // encrypt key 
            encryptKey(ref em, remoteId);

            return em;
        }

        protected string decryptMessage(EncryptedMessage em)
        {
            var me = getMessageEncryption(em);
            var ke = getKeyEncryption(em);
            em.Key = ke.DecryptData(em.Key, KeyStore?.PrivateKey?.Key);

            var bytes = me.Decrypt(em);

            return Encoding.UTF8.GetString(bytes);
        }

        private IKeyEncryption getKeyEncryption(EncryptedMessage em)
        {
            switch (em.KeyType)
            {
                case KeyEncryptionType.Rsa:
                    return RSA.Default;
                default:
                case KeyEncryptionType.None:
                    return NoKey.Default;
            }
        }

        private IMessageEncryption getMessageEncryption(EncryptedMessage em)
        {
            switch(em.EncryptionType)
            {
                case MessageEncryptionType.Aes:
                    return Rijndael.Default;
                default:
                case MessageEncryptionType.None:
                    return PlainText.Default;
            }
        }

        protected void handleRemoteCommand(NetworkStream stream, QueuedCommand serverCommand)
        {
            var emc = encryptMessage(serverCommand);
            emc.WriteToStream(stream);

            string response;

            using (MemoryStream ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                ms.Position = 0;
                var emr = EncryptedMessage.FromStream(ms);
                response = decryptMessage(emr);
            }

            serverCommand.ResultSource.SetResult(response);
            stream.Close();
        }

        protected async Task handleLocalCommand(NetworkStream stream, Guid remoteId)
        {

            string command;

            var emc = EncryptedMessage.FromStream(stream);
            command = decryptMessage(emc);

            _log.Debug($"Got command \"{command.Truncate(10)}\".");

            var crea = new CommandReceivedEventArgs()
            {
                Command = command,
                NodeId = remoteId,
                ResultSource = new TaskCompletionSource<string>()
            };

            CommandReceived(this, crea);

            var response = await crea.ResultSource.Task;

            try {
                var emr = encryptMessage(response, remoteId);
                emr.WriteToStream(stream);
            }
            catch (Exception x)
            {
                _log.Warn($"Could not send response: {x.Message}");
                throw x;
            }

            stream.Close();
        }

        protected async Task<string> sendCommand(string command, Guid serverId)
        {
            var commandQueue = getCommandQueue(serverId);

            var queuedCommand = new QueuedCommand()
            {
                CommandString = command,
                ResultSource = new TaskCompletionSource<string>(),
                NodeId = serverId
            };

            commandQueue.Enqueue(queuedCommand);

            return await queuedCommand.ResultSource.Task;
        }

        protected abstract ConcurrentQueue<QueuedCommand> getCommandQueue(Guid serverId);

        public IKeyStore KeyStore { get; set; }
        public bool EncryptionEnabled { get { return KeyStore != null; } }

        public virtual void EnableEncryption(IKeyStore keyStore)
        {
            KeyStore = keyStore;
            KeyStore.Load();

            // TODO: Fix hard coded encryption -NM 2016-11-24
            KeyEncryption = new RSA();
            MessageEncryption = new Rijndael();
        }

        protected IMessageEncryption MessageEncryption { get; set; } = new PlainText();
        protected IKeyEncryption KeyEncryption { get; set; } = new NoKey();

    }

    public struct NodeConnection
    {
        public Thread Thread { get; set; }
        public TcpClient Client { get; set; }
        public ConcurrentQueue<QueuedCommand> CommandQueue { get; set; }
    }

    public enum HandshakeResult: sbyte
    {
        // Accepted results:
        ACCEPTED = 0x00,

        UNKNOWN_GUID_ACCEPTED = -0x12,



        // Non-accepted results:
        MIN_ERROR = 0x01, // Do not use! Only for success comparission

        BAD_GUID = 0x41, // GUID is not in a valid format
        BLOCKED_GUID = 0x42, // GUID is blacklisted
        UNKNOWN_GUID_NOT_ALLOWED = 0x4a, // Client does not have the public key


        POOL_FULL = 0x51, // Client cannot accept more connections

        // Node-side results:
        NODE_UNKNOWN = 0x60,
        NODE_ERROR_READ = 0x61,
        NODE_ERROR_CLOSED = 0x62,

    }

}
