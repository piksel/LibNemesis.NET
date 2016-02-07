using Piksel.Nemesis.Security;
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
    partial class Program
    {
        private static void threadProcHub(object testData_)
        {
            var _log = NLog.LogManager.GetLogger("HubThread");

            var testData = (TestData)testData_;

            _log.Info("Creating keystore for Hub...");
            var hubKeystore = new MemoryKeyStore();
            hubKeystore.Load(testData.HubKeys);

            _log.Info("Waiting for node A to fail the connection...");
            Thread.Sleep(4000);

            _log.Info("Creating Hub...");
            var hub = new Nemesis.NemesisHub(
                new IPEndPoint(testData.Host, testData.Ports[0]),
                new IPEndPoint(testData.Host, testData.Ports[1])
            );
            hub.EnableEncryption(hubKeystore);
            hub.CommandReceived += Hub_CommandReceived;

            var nodeAKeyStore = new MemoryKeyStore();
            nodeAKeyStore.Load(testData.NodeAKeys);

            hub.NodesPublicKeys.AddOrUpdate(testData.NodeAId, nodeAKeyStore.PublicKey,
                (a, b) => { return nodeAKeyStore.PublicKey; });

            var nodeBKeyStore = new MemoryKeyStore();
            nodeBKeyStore.Load(testData.NodeBKeys);

            hub.NodesPublicKeys.AddOrUpdate(testData.NodeBId, nodeBKeyStore.PublicKey,
                (a, b) => { return nodeBKeyStore.PublicKey; });

            _log.Info("Waiting for connections to establish...");

            var cmdTest = "test";
            _log.Info($"Sending command \"{cmdTest}\" (1/2) to Node A ({testData.NodeAId})");
            Task.Run(async () => {
                var response2 = await hub.SendCommand(cmdTest, testData.NodeAId);
                _log.Info("Got response: {0}", response2);
            });

            _log.Info($"Sending command \"{cmdTest}\" (2/2) to Node A ({testData.NodeAId})");
            Task.Run(async () => {
                var response2 = await hub.SendCommand(cmdTest, testData.NodeAId);
                _log.Info("Got response: {0}", response2);
            });

        }

        private static void Hub_CommandReceived(object sender, Nemesis.CommandReceivedEventArgs e)
        {
            e.ResultSource.SetResult(String.Format("Client Result from {0}", e.NodeId));
        }
    }
}
