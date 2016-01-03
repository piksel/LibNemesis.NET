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
        int recievePort;

        Guid id;

        string host;
        IPAddress hostIp;

        int sleepSeconds = 2;

        public TimeSpan RetryDelay = TimeSpan.FromSeconds(10);

        private Thread sendThread;
        private Thread recieveThread;

        ConcurrentQueue<QueuedCommand> commandQueue = new ConcurrentQueue<QueuedCommand>();

        public bool PermitPublicKeyUpload { get; set; } = false;

        public NemesisNode(Guid id, int[] ports, string host, bool connectOnStart = true)
        {
            _log = LogManager.GetLogger("NemesisNode");

            sendPort = ports[1];
            recievePort = ports[0];
            this.host = host;
            this.id = id;

            hostIp = Dns.GetHostEntry(host).AddressList[0];

            sendThread = new Thread(new ParameterizedThreadStart(sendThreadProcedure));
            recieveThread = new Thread(new ParameterizedThreadStart(recieveThreadProcedure));
            if (connectOnStart)
            {
                sendThread.Start(new IPEndPoint(hostIp, sendPort));
                recieveThread.Start(new IPEndPoint(hostIp, recievePort));
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
                            CommandString = "__PUBKEY:" + RSA.GetPublicKey(KeyStore.PublicKey.Key),
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
                    var localEndpoint = new IPEndPoint(IPAddress.Any, 0);

                    client = new TcpClient(localEndpoint);
                    client.Connect(remoteEndPoint);
                    var lep = (IPEndPoint)client.Client.LocalEndPoint;
                    _log.Debug($"Local endpoint: {lep.Address}:{lep.Port}");
                }
                catch (Exception x)
                {
                    _log.Warn("Could not connect to the hub: {0}", x.Message);
                    _log.Info("Waiting {0} seconds before trying again...", RetryDelay.TotalSeconds);
                    Thread.Sleep(RetryDelay);
                    continue;
                }

                var stream = client.GetStream();

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
                                catch (Exception x)
                                {
                                    _log.Warn($"Communication error with hub. Requeueing command. Details: {x.Message}");
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

        private void recieveThreadProcedure(object remoteEndPoint_)
        {

            var remoteEndPoint = (IPEndPoint)remoteEndPoint_;

            bool aborted = false;

            while (Thread.CurrentThread.ThreadState != ThreadState.Aborted && Thread.CurrentThread.ThreadState != ThreadState.AbortRequested)
            {
                TcpClient client;
                _log.Info("Connecting to hub...");
                try
                {
                    var localEndpoint = new IPEndPoint(IPAddress.Any, 0);

                    client = new TcpClient(localEndpoint);
                    client.Connect(remoteEndPoint);
                    var lep = (IPEndPoint)client.Client.LocalEndPoint;
                    _log.Debug($"Local endpoint: {lep.Address}:{lep.Port}");
                }
                catch (Exception x)
                {
                    _log.Warn("Could not connect to the hub: {0}", x.Message);
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
                        if (stream.DataAvailable) // Recieving mode
                        {
                            _log.Info("Waiting for command...");
                            try
                            {
                                handleLocalCommand(stream, Guid.Empty).Wait();
                            }
                            catch (Exception x)
                            {
                                _log.Warn($"Communication error with hub. Details: {x.Message}");
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

            if (!recieveThread.IsAlive)
                recieveThread.Start(new IPEndPoint(hostIp, recievePort));
        }

        public void Close()
        {
            sendThread.Abort();
            recieveThread.Abort();
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

        protected override byte[] encryptKey(byte[] key, Guid remoteId)
        {
            return RSA.EncryptData(key, HubPublicKey.Key);

        }

        public RSAKey HubPublicKey { get; set; }
    }

}
