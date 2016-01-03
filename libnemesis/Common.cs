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

    public class CommandRecievedEventArgs: EventArgs
    {
        public string Command;
        public TaskCompletionSource<string> ResultSource { get; set; }
        public Guid NodeId { get; set; }
    }

    public delegate void CommandRecievedEventHandler(object sender, CommandRecievedEventArgs e);

    public abstract class NemesisBase
    {
        protected ILogger _log {
            get; set;
        }

        public void SetLogName(string name)
        {
            _log = LogManager.GetLogger(name);
        }

        public event CommandRecievedEventHandler CommandRecieved;


        protected abstract byte[] encryptKey(byte[] key, Guid remoteId);

        protected EncryptedMessage encryptMessage(QueuedCommand qc)
        {
            return encryptMessage(qc.CommandString, qc.NodeId);
        }

        protected EncryptedMessage encryptMessage(string message, Guid remoteId)
        {
            // encrypt message with Rijndael
            var em = Rijndael.Encrypt(Encoding.UTF8.GetBytes(message));

            // encrypt key with RSA
            em.Key = encryptKey(em.Key, remoteId);

            return em;
        }


        protected string decryptMessage(EncryptedMessage em)
        {
            em.Key = RSA.DecryptData(em.Key, KeyStore.PrivateKey.Key);

            var bytes = Rijndael.Decrypt(em);

            return Encoding.UTF8.GetString(bytes);
        }

        protected void handleRemoteCommand(NetworkStream stream, QueuedCommand serverCommand)
        {
            if (EncryptionEnabled)
            {
                var em = encryptMessage(serverCommand);
                em.WriteToStream(stream);
            }
            else
            {
                using (var sw = new StreamWriter(stream, encoding: new UTF8Encoding(false, false), bufferSize: 32, leaveOpen: true))
                {
                    sw.Write(serverCommand.CommandString);
                }
                stream.WriteByte(0);
            }

            string response;

            if (EncryptionEnabled)
            {

                using (MemoryStream ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    ms.Position = 0;
                    var em = EncryptedMessage.FromStream(ms);
                    response = decryptMessage(em);
                }
            }
            else
            {
                using (var sr = new StreamReader(stream))
                {
                    response = sr.ReadToEnd();
                }
            }
            serverCommand.ResultSource.SetResult(response);
            stream.Close();
        }

        protected async Task handleLocalCommand(NetworkStream stream, Guid remoteId)
        {

            string command;
            byte[] cmdBuf;

            if (EncryptionEnabled)
            {
                var em = EncryptedMessage.FromStream(stream);
                command = decryptMessage(em);
            }
            else
            {
                cmdBuf = new byte[2048];
                int cp = 0;
                for (; cp < cmdBuf.Length; ++cp)
                {
                    var ib = stream.ReadByte();
                    if (ib > 0)
                    {
                        cmdBuf[cp] = (byte)ib;
                    }
                    else
                    {
                        break;
                    }
                }
                command = Encoding.UTF8.GetString(cmdBuf, 0, cmdBuf.Length);
            }

            _log.Debug($"Got command \"{command.Truncate(10)}\".");

            var crea = new CommandRecievedEventArgs()
            {
                Command = command,
                NodeId = remoteId,
                ResultSource = new TaskCompletionSource<string>()
            };

            CommandRecieved(this, crea);

            var response = await crea.ResultSource.Task;

            if (EncryptionEnabled)
            {
                try {
                    var em = encryptMessage(response, remoteId);
                    em.WriteToStream(stream);
                }
                catch (Exception x)
                {
                    _log.Warn($"Could not send response: {x.Message}");
                    throw x;
                }
            }
            else
            {
                using (var sw = new StreamWriter(stream))
                {
                    sw.Write(response);
                }
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

        public void EnableEncryption(IKeyStore keyStore)
        {
            KeyStore = keyStore;
            KeyStore.Load();
        }

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
