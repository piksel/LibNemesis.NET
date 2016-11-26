using NLog;
using Piksel.Nemesis.Security;
using Piksel.Nemesis.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Piksel.Nemesis
{
    public class NemesisNode: NemesisBase
    {
        int sendPort;
        int receivePort;

        Guid id;

        string host;
        IPAddress hostIp;

        int sleepSeconds = 2;

        public TimeSpan RetryDelay = TimeSpan.FromSeconds(10);

        private Thread sendThread;
        private Thread receiveThread;

        ConcurrentQueue<QueuedCommand> commandQueue = new ConcurrentQueue<QueuedCommand>();

        public bool PermitPublicKeyUpload { get; set; } = false;

        public NemesisNode(Guid id, int[] ports, string host, bool connectOnStart = true, int readTimeout = 30000, int writeTimeout = 30000)
        {
            _log = LogManager.GetLogger("NemesisNode");

            sendPort = ports[1];
            receivePort = ports[0];
            this.host = host;
            this.id = id;
            ReadTimeout = readTimeout;
            WriteTimeout = writeTimeout;

            hostIp = Dns.GetHostEntry(host).AddressList[0].MapToIPv4();

            sendThread = new Thread(new ParameterizedThreadStart(sendThreadProcedure));
            receiveThread = new Thread(new ParameterizedThreadStart(receiveThreadProcedure));
            if (connectOnStart)
            {
                sendThread.Start(new IPEndPoint(hostIp, sendPort));
                receiveThread.Start(new IPEndPoint(hostIp, receivePort));
            }
        }

        private bool HandleHandshake(NetworkStream stream)
        {

            HandshakeResult idResponse = HandshakeResult.NODE_UNKNOWN;

            try
            {
                bool couldRead;
                var sb = stream.ReadSbyte(out couldRead);
                idResponse = couldRead ? (HandshakeResult)sb : HandshakeResult.NODE_ERROR_CLOSED;

            }
            catch (Exception x)
            {
                _log.Error("Cannot get response from client: {0}", x.Message);
                idResponse = HandshakeResult.NODE_ERROR_READ;
            }

            if (idResponse < HandshakeResult.MIN_ERROR) // Anything lower than MIN_ERROR is a accepted result
            {
                _log.Info("Identity accepted!");

                if (idResponse == HandshakeResult.UNKNOWN_GUID_ACCEPTED)
                {
                    if (PermitPublicKeyUpload)
                    {
                        var uploadKeyCommand = new QueuedCommand()
                        {
                            CommandString = "__PUBKEY:" + KeyEncryption.GetPublicKey(KeyStore.PublicKey.Key),
                        };
                        commandQueue.Enqueue(uploadKeyCommand);
                    }
                    else
                    {
                        // Todo: Handle if the Hub wants the public key but the Node is configured not to send it.
                        return false;
                    }
                }

                return true;
            }
            else
            {
                _log.Warn("Identity not accepted by hub! Got response: {1} (0x{0:x2}). Closing connection.", (byte)idResponse, idResponse.ToString());
                return false;
            }
        }

        private void sendThreadProcedure(object remoteEndPoint_)
        {

            var remoteEndPoint = (IPEndPoint)remoteEndPoint_;

            bool aborted = false;

            while (Thread.CurrentThread.ThreadState != ThreadState.Aborted && Thread.CurrentThread.ThreadState != ThreadState.AbortRequested)
            {
                TcpClient client;
                _log.Info("Connecting to hub...");
                try
                {
                    //var localEndpoint = new IPEndPoint(IPAddress.Any, 0);

                    client = new TcpClient();
                    client.Connect(remoteEndPoint);
                    var lep = (IPEndPoint)client.Client.LocalEndPoint;
                    _log.Debug($"Local endpoint: {lep.Address}:{lep.Port}");
                }
                catch (Exception x)
                {
                    _log.Warn(x, "Could not connect to the hub: {0}", x.Message);
                    _log.Info("Waiting {0} seconds before trying again...", RetryDelay.TotalSeconds);
                    Thread.Sleep(RetryDelay);
                    continue;
                }

                var stream = client.GetStream();

                stream.ReadTimeout = 30000;

                var idBytes = id.ToByteArray();
                stream.Write(idBytes, 0, idBytes.Length);

                if (HandleHandshake(stream)) { 

                    while (!aborted && client.Connected)
                    {
                        if (commandQueue.Count > 0) // Sending mode
                        {
                            _log.Info("Processing command from queue...");
                            QueuedCommand serverCommand;
                            if (commandQueue.TryDequeue(out serverCommand))
                            {
                                try
                                {
                                    handleRemoteCommand(stream, serverCommand);
                                }
                                catch (System.Security.Cryptography.CryptographicException cx)
                                {
                                    _log.Warn(cx, $"Cryptographic communication error with hub. Check encryption keys. Details: {cx.Message}");
                                    serverCommand.ResultSource.SetException(cx);
                                }
                                catch (Exception x)
                                {
                                    var ix = x.InnerException;
                                    _log.Warn(x, $"Communication error with hub. Requeueing command. Details: {x.Message}{(ix != null ? "; " + ix.Message : "")}");
                                    commandQueue.Enqueue(serverCommand);
                                }
                            }
                            else
                            {
                                _log.Info("Could not dequeue command!");
                            }
                        }
                        else
                        {
                            Thread.Sleep(500);
                        }
                    }
                }
                else
                {
                    client.Close();
                }

                _log.Info("Sleeping for {0} seconds...", sleepSeconds);
                Thread.Sleep(TimeSpan.FromSeconds(sleepSeconds));
            }
            
        }

        private void receiveThreadProcedure(object remoteEndPoint_)
        {

            var remoteEndPoint = (IPEndPoint)remoteEndPoint_;

            bool aborted = false;

            while (Thread.CurrentThread.ThreadState != ThreadState.Aborted && Thread.CurrentThread.ThreadState != ThreadState.AbortRequested)
            {
                TcpClient client;
                _log.Info("Connecting to hub...");
                try
                {
                    //var localEndpoint = new IPEndPoint(IPAddress.Any, 0);

                    client = new TcpClient();
                    client.Connect(remoteEndPoint);
                    var lep = (IPEndPoint)client.Client.LocalEndPoint;
                    _log.Debug($"Local endpoint: {lep.Address}:{lep.Port}");
                }
                catch (Exception x)
                {
                    _log.Warn(x, "Could not connect to the hub: {0}", x.Message);
                    _log.Info("Waiting {0} seconds before trying again...", RetryDelay.TotalSeconds);
                    Thread.Sleep(RetryDelay);
                    continue;
                }

                var stream = client.GetStream();

                var idBytes = id.ToByteArray();
                stream.Write(idBytes, 0, idBytes.Length);

                if (HandleHandshake(stream))
                {

                    while (!aborted && client.Connected)
                    {
                        if (stream.DataAvailable) // Receiving mode
                        {
                            _log.Info("Waiting for command...");
                            try
                            {
                                handleLocalCommand(stream, Guid.Empty).Wait();
                            }
                            catch (System.Security.Cryptography.CryptographicException cx)
                            {
                                _log.Warn(cx, $"Cryptographic communication error with hub. Check encryption keys. Details: {cx.Message}");

                            }
                            catch (Exception x)
                            {
                                var ix = x.InnerException;
                                _log.Warn(x, $"Communication error with hub. Details: {x.Message}{(ix!=null?"; "+ix.Message:"")}");
                            }
                        }
                        
                        else
                        {
                            Thread.Sleep(500);
                        }
                    }
                }
                else
                {
                    client.Close();
                }

                _log.Info("Sleeping for {0} seconds...", sleepSeconds);
                Thread.Sleep(TimeSpan.FromSeconds(sleepSeconds));
            }

        }

        public void Connect()
        {
            if (!sendThread.IsAlive)
                sendThread.Start(new IPEndPoint(hostIp, sendPort));

            if (!receiveThread.IsAlive)
                receiveThread.Start(new IPEndPoint(hostIp, receivePort));
        }

        public void Close()
        {
            sendThread.Abort();
            receiveThread.Abort();
        }

        ~NemesisNode()
        {
            Close();
        }


        public async Task<string> SendCommand(string command)
        {
            return await sendCommand(command, Guid.Empty);
        }

        protected override ConcurrentQueue<QueuedCommand> getCommandQueue(Guid serverId)
        {
            return commandQueue;
        }

        protected override void encryptKey(ref EncryptedMessage em, Guid remoteId)
        {
            if (KeyEncryption is NoKey) return;

            em.Key = KeyEncryption.EncryptData(em.Key, HubPublicKey?.Key);
            em.KeyType = KeyEncryption.Type;
        }

        public override void EnableEncryption(IKeyStore keyStore)
        {
            if (HubPublicKey == null) throw new Exception("Hub public key not set, cannot enable encryption!");
            base.EnableEncryption(keyStore);
        }

        public RSAKey HubPublicKey { get; set; }
    }

}
