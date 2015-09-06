using NLog;
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
    public class Server: NemesisBase
    {
        int port;
        string host;

        private Thread serverThread;
        NetworkStream stream;

        ConcurrentQueue<QueuedCommand> commandQueue = new ConcurrentQueue<QueuedCommand>();

        public Server(Guid id, int port, string host)
        {
            _log = LogManager.GetLogger("NemesisServer");

            this.port = port;
            this.host = host;

            int sleepSeconds = 2;

            serverThread = new Thread(new ThreadStart(delegate
            {
                bool aborted = false;

                while (Thread.CurrentThread.ThreadState != ThreadState.Aborted && Thread.CurrentThread.ThreadState != ThreadState.AbortRequested)
                {

                    _log.Info("Connecting to client...");
                    TcpClient client = new TcpClient(host, port);

                    stream = client.GetStream();

                    var idBytes = id.ToByteArray();
                    stream.Write(idBytes, 0, idBytes.Length);

                    byte idResponse = 0xa0;

                    try
                    {
                        var response = stream.ReadByte();
                        idResponse = (byte)(response == -1 ? 0xa2 : response);

                    }
                    catch (Exception x)
                    {
                        _log.Error("Cannot get response from client: {0}", x.Message);
                        idResponse = 0xa1;
                    }

                    if (idResponse == 0)
                    {
                        _log.Info("Identity accepted!");
                        while (!aborted && client.Connected)
                        {
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
                        _log.Warn("Identity not accepted by client! Got response: 0x{0:x2} Closing connection.", idResponse);
                    }

                    _log.Info("Sleeping for {0} seconds...", sleepSeconds);
                    Thread.Sleep(TimeSpan.FromSeconds(sleepSeconds));
                }
            }));
            serverThread.Start();
        }

        public void Close()
        {
            serverThread.Abort();
        }

        ~Server()
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
    }
}
