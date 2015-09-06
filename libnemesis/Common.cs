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
    public class QueuedCommand
    {
        public string CommandString { get; set; }
        public TaskCompletionSource<string> ResultSource { get; set; }
        public Guid ServerId { get; set; }
    }

    public class CommandRecievedEventArgs: EventArgs
    {
        public string Command;
        public TaskCompletionSource<string> ResultSource { get; set; }
        public Guid ServerId { get; set; }
    }

    public delegate void CommandRecievedEventHandler(object sender, CommandRecievedEventArgs e);

    public abstract class NemesisBase
    {
        protected ILogger _log {
            get; set;
        }

        public event CommandRecievedEventHandler CommandRecieved;

        protected void handleRemoteCommand(NetworkStream stream, QueuedCommand serverCommand)
        {
            using (var sw = new StreamWriter(stream, encoding: new UTF8Encoding(false, false), bufferSize: 32, leaveOpen: true))
            {
                sw.Write(serverCommand.CommandString);
            }
            stream.WriteByte(0);
            string response;
            using (var sr = new StreamReader(stream))
            {
                response = sr.ReadToEnd();
            }
            serverCommand.ResultSource.SetResult(response);
        }

        protected async void handleLocalCommand(NetworkStream stream)
        {

            string command;
            var cmdBuf = new byte[2048];
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
            command = Encoding.UTF8.GetString(cmdBuf, 0, cp);

            _log.Info("Got command: \"{0}\"", command);

            var crea = new CommandRecievedEventArgs()
            {
                Command = command,
                ResultSource = new TaskCompletionSource<string>()
            };

            CommandRecieved(this, crea);

            var response = await crea.ResultSource.Task;

            using (var sw = new StreamWriter(stream))
            {
                sw.Write("OK");
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
                ServerId = serverId
            };

            commandQueue.Enqueue(queuedCommand);

            return await queuedCommand.ResultSource.Task;
        }

        protected abstract ConcurrentQueue<QueuedCommand> getCommandQueue(Guid serverId);

    }

    public struct ServerConnection
    {
        public Thread Thread { get; set; }
        public TcpClient Client { get; set; }
        public ConcurrentQueue<QueuedCommand> CommandQueue { get; set; }
    }

}
