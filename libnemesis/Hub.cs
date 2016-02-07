using NLog;
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
using Piksel.Nemesis.Security;
using Piksel.Nemesis.Utilities;

namespace Piksel.Nemesis
{
    public class NemesisHub : NemesisBase
    {

        private AutoResetEvent sendConnectionWaitHandle = new AutoResetEvent(false);
        private AutoResetEvent receiveConnectionWaitHandle = new AutoResetEvent(false);

        IPEndPoint sendEndpoint;
        IPEndPoint receiveEndpoint;

        private TcpListener sendListener;
        private TcpListener receiveListener;

        private Thread sendListenerThread;
        private Thread receiveListenerThread;

        private bool aborted = false;

        ConcurrentDictionary<Guid, NodeConnection> nodeConnections = new ConcurrentDictionary<Guid, NodeConnection>();

        // Accept the connection even if we do not have the node public key
        public bool AllowUnknownGuid { get; set; } = false;

        public NemesisHub(IPEndPoint sendEndpoint, IPEndPoint receiveEndpoint, int readTimeout = 30000, int writeTimeout = 30000)
        {
            _log = LogManager.GetLogger("NemesisHub");

            this.sendEndpoint = sendEndpoint;
            this.receiveEndpoint = receiveEndpoint;
            WriteTimeout = writeTimeout;
            ReadTimeout = readTimeout;

            _log.Info("Starting sending communication thread...");

            sendListenerThread = new Thread(new ThreadStart(delegate
            {
                sendListener = new TcpListener(sendEndpoint);
                sendListener.Start();

                while (Thread.CurrentThread.ThreadState != ThreadState.AbortRequested)
                {
                    IAsyncResult result = sendListener.BeginAcceptTcpClient(HandleAsyncSendConnection, sendListener);
                    sendConnectionWaitHandle.WaitOne(TimeSpan.FromSeconds(3));  // Wait until a client has begun handling an event
                    sendConnectionWaitHandle.Reset(); // Reset wait handle or the loop goes as fast as it can (after first request)
                }
            }));

            sendListenerThread.Start();

            _log.Info("Starting receiving communication thread...");

            receiveListenerThread = new Thread(new ThreadStart(delegate
            {
                receiveListener = new TcpListener(receiveEndpoint);
                receiveListener.Start();

                while (Thread.CurrentThread.ThreadState != ThreadState.AbortRequested)
                {
                    IAsyncResult result = receiveListener.BeginAcceptTcpClient(HandleAsyncReceiveConnection, receiveListener);
                    receiveConnectionWaitHandle.WaitOne(TimeSpan.FromSeconds(3));  // Wait until a client has begun handling an event
                    receiveConnectionWaitHandle.Reset(); // Reset wait handle or the loop goes as fast as it can (after first request)
                }
            }));

            receiveListenerThread.Start();
        }

        public async Task<string> SendCommand(string command, Guid nodeId)
        {
            return await sendCommand(command, nodeId);
        }

        private bool HandleHandshake(TcpClient client, out NetworkStream stream, out Guid nodeId)
        {


            var clientEndpoint = (IPEndPoint)client.Client.RemoteEndPoint;

            _log.Info("Got connection from {0}:{1}", clientEndpoint.Address, clientEndpoint.Port);

            stream = client.GetStream();

            var idBytes = new byte[16]; // GUID is 16 bytes

            stream.Read(idBytes, 0, 16);

            nodeId = new Guid(idBytes);

            _log.Info("Node identified as: {0}", nodeId.ToString());

            bool knownGuid = NodesPublicKeys.ContainsKey(nodeId);

            if (!knownGuid && !AllowUnknownGuid)
            {
                _log.Warn("Node GUID not recognized, closing connection.");
                stream.WriteSbyte((sbyte)HandshakeResult.UNKNOWN_GUID_NOT_ALLOWED);
                stream.Close();
                return false;
            }
            else
            {
                if (knownGuid)
                {
                    stream.WriteSbyte((sbyte)HandshakeResult.ACCEPTED);
                }
                else
                {
                    stream.WriteSbyte((sbyte)HandshakeResult.UNKNOWN_GUID_ACCEPTED);
                }



                return true;
            }
        }

        private void HandleAsyncSendConnection(IAsyncResult result)
        {
            TcpListener listener = (TcpListener)result.AsyncState;
            TcpClient client = listener.EndAcceptTcpClient(result);
            NetworkStream stream;
            Guid nodeId;
            sendConnectionWaitHandle.Set(); //Inform the main thread this connection is now handled

            if (HandleHandshake(client, out stream, out nodeId))
            {
                var nodeConnection = new NodeConnection()
                {
                    Client = client,
                    Thread = Thread.CurrentThread,
                    CommandQueue = new ConcurrentQueue<QueuedCommand>()
                };
                nodeConnections.AddOrUpdate(nodeId, nodeConnection, (g, sc) =>
                {
                    nodeConnection.CommandQueue = sc.CommandQueue; // Preserve the queue if it already exists
                    return nodeConnection;
                });

                while (!aborted && client.Connected)
                {
                    var commandQueue = nodeConnection.CommandQueue;
                    if (commandQueue.Count > 0) // Sending mode
                    {
                        if (!NodesPublicKeys.ContainsKey(nodeId))
                        {
                            _log.Info(String.Format("Cannot process queue. No public key for server {0}.", nodeId));
                            Thread.Sleep(1000);
                        }
                        else
                        {

                            _log.Debug("Processing command from queue...");
                            QueuedCommand serverCommand;

                            if (commandQueue.TryDequeue(out serverCommand))
                            {
                                try
                                {
                                    handleRemoteCommand(stream, serverCommand);
                                }
                                catch (AggregateException ax)
                                {
                                    ax.Handle((x) =>
                                    {
                                        if (x is System.Security.Cryptography.CryptographicException)
                                        {
                                            _log.Warn($"Cryptographic communication error with node. Check encryption keys.  Details: {x.Message}");
                                        }
                                        else
                                        {
                                            var ix = x.InnerException;
                                            _log.Warn($"Communication error with node. Requeueing command. Details: {x.Message}{(ix != null ? "; " + ix.Message : "")}");
                                            commandQueue.Enqueue(serverCommand);
                                        }
                                        return true;
                                    });
                                }
                                stream.Close();
                            }
                            else
                            {
                                _log.Warn("Could not dequeue command!");
                            }
                        }
                    }
                    else
                    {
                        Thread.Sleep(1000);
                    }
                }
            }
        }

        private void HandleAsyncReceiveConnection(IAsyncResult result)
        {
            TcpListener listener = (TcpListener)result.AsyncState;
            TcpClient client = listener.EndAcceptTcpClient(result);
            NetworkStream stream;
            Guid nodeId;
            receiveConnectionWaitHandle.Set(); //Inform the main thread this connection is now handled

            if (HandleHandshake(client, out stream, out nodeId))
            {
                while (!aborted && client.Connected)
                {
                    if (stream.DataAvailable) // Receiving mode
                    {
                        _log.Debug("Waiting for command...");
                        try
                        {
                            var task = handleLocalCommand(stream, nodeId);
                            task.Wait();
                            if (task.Exception != null)
                            {
                                throw task.Exception;
                            }
                        }
                        catch (AggregateException ax)
                        {
                            ax.Handle((x) =>
                            {
                                if (x is System.Security.Cryptography.CryptographicException)
                                {
                                    _log.Warn($"Cryptographic communication error with node. Check encryption keys.  Details: {x.Message}");
                                }
                                else
                                {
                                    var ix = x.InnerException;
                                    _log.Warn($"Communication error with node. Details: {x.Message}{(ix != null ? "; " + ix.Message : "")}");
                                }
                                return true;
                            });
                        }
                        stream.Close();
                    }

                    else
                    {
                        Thread.Sleep(1000);
                    }
                }
            }
        }

        protected override ConcurrentQueue<QueuedCommand> getCommandQueue(Guid nodeId)
        {
            var nodeConnection = nodeConnections.GetOrAdd(nodeId, new NodeConnection()
            {
                CommandQueue = new ConcurrentQueue<QueuedCommand>()
            });
            return nodeConnection.CommandQueue;
        }

        public void Close()
        {
            foreach (var scVlk in nodeConnections)
            {
                var nodeConnection = scVlk.Value;
                if (nodeConnection.Client != null) nodeConnection.Client.Close();
                if (nodeConnection.Thread != null) nodeConnection.Thread.Abort();
            }

            if (sendListenerThread != null)
                sendListenerThread.Abort();

            if (receiveListenerThread != null)
                receiveListenerThread.Abort();
        }

        ~NemesisHub()
        {
            Close();
        }

        public ConcurrentDictionary<Guid, RSAKey> NodesPublicKeys = new ConcurrentDictionary<Guid, RSAKey>();

        protected override byte[] encryptKey(byte[] key, Guid remoteId)
        {
            if (!NodesPublicKeys.ContainsKey(remoteId))
                throw new Exception(String.Format("No RSA public key for node \"{0}\" found!", remoteId.ToString()));

            return RSA.EncryptData(key, NodesPublicKeys[remoteId].Key);

        }
    }


}
