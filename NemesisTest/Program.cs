using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Nemesis = Piksel.Nemesis;
using Piksel.Nemesis.Security;

namespace NemesisTest
{
    partial class Program
    {
        static readonly int[] PORTS = { 8741, 8742 };



        static void Main(string[] args)
        {
            NLog.Logger _log = NLog.LogManager.GetLogger("Test");

            Console.Title = "Nemesis Test";
            _log.Info("Nemesis test started.");

            _log.Info("Generating GUIDs for nodes A, B and C...");
            var nodeAId = Guid.NewGuid();
            _log.Info($"Using GUID for node A: {nodeAId.ToString()}");
            var nodeBId = Guid.NewGuid();
            _log.Info($"Using GUID for node B: {nodeBId.ToString()}");
            var nodeCId = Guid.NewGuid();
            _log.Info($"Using GUID for node C: {nodeCId.ToString()}");

            var kp = RSA.Default.CreateNewKeyPair();

            var testData = new TestData(
                nodeAId,
                kp,
                nodeBId,
                kp,
                nodeCId,
                kp,
                PORTS,
                IPAddress.Loopback
            );


            _log.Info("Starting Hub thread...");
            var threadHub = new Thread(new ParameterizedThreadStart(threadProcHub));
            threadHub.Start(testData);

            /*
            _log.Info("Starting Node A thread...");
            var threadNodeA = new Thread(new ParameterizedThreadStart(threadProcNodeA));
            threadNodeA.Start(testData);
            
            _log.Info("Starting Node B thread...");
            var threadNodeB = new Thread(new ParameterizedThreadStart(threadProcNodeB));
            threadNodeB.Start(testData);
            */
            _log.Info("Starting Node C thread...");
            var threadNodeC = new Thread(new ParameterizedThreadStart(threadProcNodeC));
            threadNodeC.Start(testData);






            _log.Info("Waiting for connections to establish...");
            WaitHandle.WaitAll(testData.ResetEvents);

            /*


            _log.Info("Sending command \"{0}\" to server B ({1})", cmdTest, serverGuidB);
            var response3 = client.SendCommand(cmdTest, serverGuidB);

            _log.Info("Creating server B...");
            var serverB = new Nemesis.NemesisNode(serverGuidB, PORT, "localhost");
            serverB.EnableEncryption(new MemoryKeyStore());
            serverB.CommandRecieved += NodeB_CommandRecieved;

            _log.Info("Letting the client try to send a message to the initialized server with no public key...");
            Thread.Sleep(2000);


            //serverB.ClientPublicKey = clientKey;

            _log.Info("Setting the public key for Server B...");
            //var serverKeyB = new RSAKey() { Key = serverB.KeyStore.PublicKey.Key };

            //var serverKeyB = new RSAKey() { Key = new byte[10] };
            //client.NodesPublicKeys.AddOrUpdate(serverGuidB, serverKeyB, (a, b) => serverKeyB);


            //_log.Info("Got response: {0}", response3.Result);
            */
            _log.Info("Waiting for transactions to complete...");
            Thread.Sleep(16000);

            Console.Write("\nPress any key to destroy instances...\n");
            Console.Read();

            threadHub.Abort();
            //threadNodeA.Abort();
            //threadNodeB.Abort();
            threadNodeC.Abort();

            _log.Info("Waiting for threads to exit...");
            

            Console.Write("\nPress any key to destroy instances...");
            Console.Read();
        }

    }

    public struct TestData {

        readonly Guid _nodeAId;
        public Guid NodeAId { get { return _nodeAId; } }
        readonly byte[] _nodeAKeys;
        public byte[] NodeAKeys { get { return _nodeAKeys; } }

        readonly Guid _nodeBId;
        public Guid NodeBId { get { return _nodeBId; } }
        readonly byte[] _nodeBKeys;
        public byte[] NodeBKeys { get { return _nodeBKeys; } }

        readonly Guid _nodeCId;
        public Guid NodeCId { get { return _nodeBId; } }

        readonly byte[] _hubKeys;
        public byte[] HubKeys { get { return _hubKeys; } }

        readonly int[] _ports;
        public int[] Ports { get { return _ports; } }
        readonly IPAddress _host;
        public IPAddress Host { get { return _host; } }

        public ManualResetEvent[] ResetEvents;

        public TestData(Guid nodeAId, byte[] nodeAKeys, Guid nodeBId,
            byte[] nodeBKeys, Guid nodeCId, byte[] hubKeys, int[] ports, IPAddress host)
        {
            ResetEvents = new ManualResetEvent[3] {
                new ManualResetEvent(false),
                new ManualResetEvent(false),
                new ManualResetEvent(false)
            };

            _nodeAId = nodeAId;
            _nodeAKeys = nodeAKeys;
            _nodeBId = nodeBId;
            _nodeBKeys = nodeBKeys;
            _nodeCId = nodeCId;
            _hubKeys = hubKeys;
            _ports = ports;
            _host = host;
        }

    }
}
