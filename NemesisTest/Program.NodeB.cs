using Piksel.Nemesis.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Nemesis = Piksel.Nemesis;

namespace NemesisTest
{
    partial class Program
    {
        private static void threadProcNodeB(object testData_)
        {
            var testData = (TestData)testData_;
            var _log = NLog.LogManager.GetLogger("NodeBThread");

            _log.Info("Creating node B...");

            var badKeyStore = new MemoryKeyStore();
            badKeyStore.Load(testData.NodeAKeys); // Note: The WRONG keystore

            var nodeBKeyStore = new MemoryKeyStore();
            nodeBKeyStore.Load(testData.NodeAKeys);

            var nodeB = new Nemesis.NemesisNode(testData.NodeBId, testData.Ports, testData.Host.ToString(), false);
            nodeB.SetLogName("nNodeB");

            nodeB.CommandReceived += NodeB_CommandReceived;

            //var hubKeyStore = new MemoryKeyStore();
            //hubKeyStore.Load(testData.HubKeys);
            //nodeB.HubPublicKey = hubKeyStore.PublicKey;
            nodeB.HubPublicKey = badKeyStore.PublicKey; // Note: Again; the wrong key

            nodeB.EnableEncryption(badKeyStore);

            nodeB.Connect();

            var cmdTest = "n2h";
            _log.Info($"Sending command \"{cmdTest}\" with WRONG ENCODING from Node B to Hub");

            try {
                var response = nodeB.SendCommand(cmdTest);
                var r = response.Result; // Note: Will fail
            }
            catch (Exception x)
            {
                _log.Warn("Oh no! " + x.Message);
            }

            nodeB.EnableEncryption(nodeBKeyStore);

            _log.Info($"Sending command \"{cmdTest}\" with the RIGHT ENCODING from Node B to Hub");

            try {
                var response = nodeB.SendCommand(cmdTest);
                _log.Info("Got result: " + response.Result); // Note: Will fail
            }
            catch (Exception x)
            {
                _log.Warn("Oh no! " + x.Message);
            }

            //var hubKeyStore = new MemoryKeyStore();
            //hubKeyStore.Load(testData.HubKeys);
            //nodeB.HubPublicKey = hubKeyStore.PublicKey;

        }

        private static void NodeB_CommandReceived(object sender, Nemesis.CommandReceivedEventArgs e)
        {
            e.ResultSource.SetResult("Node B Result");
        }
    }
}
