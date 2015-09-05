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

namespace Piksel.Nemesis
{
    public class Client: NemesisBase
    {
        IPEndPoint endpoint;
        private TcpListener tcpListener;
        private AutoResetEvent connectionWaitHandle = new AutoResetEvent(false);
        private Thread listenerThread;

        private bool aborted = false;

        ConcurrentDictionary<Guid, ServerConnection> serverConnections = new ConcurrentDictionary<Guid, ServerConnection>();

        public Client(IPEndPoint endpoint)
        {
            _log = LogManager.GetLogger("NemesisClient");

            this.endpoint = endpoint;
            _log.Info("Starting communication thread...");

            listenerThread = new Thread(new ThreadStart(delegate
            {
                tcpListener = new TcpListener(endpoint);
                tcpListener.Start();

                while (true)
                {
                    IAsyncResult result = tcpListener.BeginAcceptTcpClient(HandleAsyncConnection, tcpListener);
                    connectionWaitHandle.WaitOne(); // Wait until a client has begun handling an event
                    connectionWaitHandle.Reset(); // Reset wait handle or the loop goes as fast as it can (after first request)
                }
            }));

            listenerThread.Start();
        }

        public async Task<string> SendCommand(string command, Guid serverId)
        {
            return await sendCommand(command, serverId);
        }

        private void HandleAsyncConnection(IAsyncResult result)
        {
            TcpListener listener = (TcpListener)result.AsyncState;
            TcpClient client = listener.EndAcceptTcpClient(result);
            connectionWaitHandle.Set(); //Inform the main thread this connection is now handled

            var clientEndpoint = (IPEndPoint)client.Client.RemoteEndPoint;

            _log.Info("Got connection from {0}:{1}", clientEndpoint.Address, clientEndpoint.Port);

            var stream = client.GetStream();

            var idBytes = new byte[16]; // GUID is 16 bytes

            stream.Read(idBytes, 0, 16);

            var serverId = new Guid(idBytes);

            _log.Info("Server identified as: {0}", serverId.ToString());

            bool serverIdAccepted = true; // dummy for testing

            if (serverIdAccepted)
            {
                stream.WriteByte(0);
                var serverConnection = new ServerConnection()
                {
                    Client = client,
                    Thread = Thread.CurrentThread,
                    CommandQueue = new ConcurrentQueue<QueuedCommand>()
                };
                serverConnections.AddOrUpdate(serverId, serverConnection, (g, sc) => {
                    serverConnection.CommandQueue = sc.CommandQueue; // Preserve the queue if it already exists
                    return serverConnection;
                });


                while (!aborted && client.Connected)
                {
                    var commandQueue = serverConnection.CommandQueue;
                    if (commandQueue.Count > 0) // Sending mode
                    {
                        _log.Info("Processing command from queue...");
                        QueuedCommand serverCommand;
                        if (commandQueue.TryDequeue(out serverCommand))
                        {
                            handleRemoteCommand(stream, serverCommand);
                        }
                        else
                        {
                            _log.Info("Could not dequeue command!");
                        }
                    }
                    else if (stream.DataAvailable) // Recieving mode
                    {
                        _log.Info("Waiting for command...");
                        handleLocalCommand(stream);
                    }
                    else
                    {
                        Thread.Sleep(1000);
                    }
                }
            }
            else
            {
                stream.WriteByte(1);
            }
        }

        protected override ConcurrentQueue<QueuedCommand> getCommandQueue(Guid serverId)
        {
            var serverConnection = serverConnections.GetOrAdd(serverId, new ServerConnection()
            {
                CommandQueue = new ConcurrentQueue<QueuedCommand>()
            });
            return serverConnection.CommandQueue;
        }
    }


}
