using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Nemesis = Piksel.Nemesis;

namespace NemesisTest
{
    class Program
    {
        static readonly int PORT = 8741;

        static NLog.Logger _log = NLog.LogManager.GetLogger("Test");

        static void Main(string[] args)
        {
            Console.Title = "Nemesis Test";
            _log.Info("Nemesis test started.");

            _log.Info("Creating client...");
            var client = new Nemesis.Client(new IPEndPoint(IPAddress.Loopback, PORT));
            client.CommandRecieved += Client_CommandRecieved;

            _log.Info("Generating GUIDs for server A and B...");
            var serverGuidA = Guid.NewGuid();
            _log.Info("Using GUID for server A: {0}", serverGuidA.ToString());
            var serverGuidB = Guid.NewGuid();
            _log.Info("Using GUID for server B: {0}", serverGuidB.ToString());

      

            _log.Info("Creating server A...");
            var serverA = new Nemesis.Server(serverGuidA, PORT, "localhost");
            serverA.CommandRecieved += ServerA_CommandRecieved;

            _log.Info("Waiting for connections to establish...");
            Thread.Sleep(2000);

            var cmdTest = "test";
            _log.Info("Sending command \"{0}\" to server A ({1})", cmdTest, serverGuidA);
            var response = client.SendCommand(cmdTest, serverGuidA);
            _log.Info("Got response: {0}", response.Result);

            _log.Info("Sending command \"{0}\" from server A to client", cmdTest);
            var response2 = serverA.SendCommand(cmdTest);
            _log.Info("Got response: {0}", response2.Result);


            _log.Info("Sending command \"{0}\" to server B ({1})", cmdTest, serverGuidB);
            var response3 = client.SendCommand(cmdTest, serverGuidB);

            _log.Info("Creating server B...");
            var serverB = new Nemesis.Server(serverGuidB, PORT, "localhost");
            serverB.CommandRecieved += ServerB_CommandRecieved;

            _log.Info("Got response: {0}", response3.Result);




            Console.ReadLine();
        }

        private static void Client_CommandRecieved(object sender, Nemesis.CommandRecievedEventArgs e)
        {
            e.ResultSource.SetResult("Client Result");
        }

        private static void ServerA_CommandRecieved(object sender, Nemesis.CommandRecievedEventArgs e)
        {
            e.ResultSource.SetResult("Server A Result");
        }

        private static void ServerB_CommandRecieved(object sender, Nemesis.CommandRecievedEventArgs e)
        {
            e.ResultSource.SetResult("Server B Result");
        }
    }
}
