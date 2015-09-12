using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;

using Nemesis = Piksel.Nemesis;
using Piksel.Nemesis.Security;

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

            _log.Info("Testing packing...");
            UInt16 start = 36282;
            _log.Info(String.Format("Start: {0}, 0x{0:x4}", start));
            byte[] packed = Nemesis.StreamUtils.PackUint16(start);
            _log.Info(String.Format("Packed: 0 => {0:x2}, 1 => {1:x2}", packed[0], packed[1]));
            UInt16 end = Nemesis.StreamUtils.UnpackUint16(packed);
            _log.Info(String.Format("End: {0}, 0x{0:x4}", end));

            _log.Info("Creating client...");
            var client = new Nemesis.Client(new IPEndPoint(IPAddress.Loopback, PORT));
            client.EnableEncryption(new MemoryKeyStore());
            client.CommandRecieved += Client_CommandRecieved;

            var clientKey = client.KeyStore.PublicKey;

            _log.Info("Generating GUIDs for server A and B...");
            var serverGuidA = Guid.NewGuid();
            _log.Info("Using GUID for server A: {0}", serverGuidA.ToString());
            var serverGuidB = Guid.NewGuid();
            _log.Info("Using GUID for server B: {0}", serverGuidB.ToString());

      

            _log.Info("Creating server A...");
            var serverA = new Nemesis.Server(serverGuidA, PORT, "localhost");
            serverA.EnableEncryption(new MemoryKeyStore());
            serverA.CommandRecieved += ServerA_CommandRecieved;
            serverA.ClientPublicKey = clientKey;

            var serverKeyA = serverA.KeyStore.PublicKey;
            client.ServerPublicKeys.Add(serverGuidA, serverKeyA);

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
            serverB.EnableEncryption(new MemoryKeyStore());
            serverB.CommandRecieved += ServerB_CommandRecieved;

            _log.Info("Letting the client try to send a message to the initialized server with no public key...");
            Thread.Sleep(2000);


            serverB.ClientPublicKey = clientKey;

            _log.Info("Setting the public key for Server B...");
            var serverKeyB = serverB.KeyStore.PublicKey;
            client.ServerPublicKeys.Add(serverGuidB, serverKeyB);

            _log.Info("Got response: {0}", response3.Result);

            _log.Info("Waiting for transactions to complete...");
            Thread.Sleep(2000);

            Console.Write("\nPress any key to destroy instances...");
            Console.Read();

            _log.Info("Waiting for threads to exit...");
            serverB.Close();
            serverA.Close();
            client.Close();
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
