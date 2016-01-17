using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Piksel.Nemesis.Security;
using Piksel.Nemesis;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;

namespace libnemesis.tests
{
    [TestClass]
    public class BasicFunctionalityTests
    {
        [TestMethod]
        public void SimpleNodeAndHub()
        {
            var answerTimeout = TimeSpan.FromSeconds(10);

            var NodeMessageRecieved = new TaskCompletionSource<string>();

            var testData = Shared.GetTestSettings();

            var nodeAkeyStore = new MemoryKeyStore();
            nodeAkeyStore.Load(testData.NodeAKeys);

            var hubKeystore = new MemoryKeyStore();
            hubKeystore.Load(testData.HubKeys);

            var hubThread = new Thread(() =>
            {

                var hub = new NemesisHub(
                    new IPEndPoint(testData.Host, testData.Ports[0]),
                    new IPEndPoint(testData.Host, testData.Ports[1])
                );
                hub.EnableEncryption(hubKeystore);


                hub.NodesPublicKeys.AddOrUpdate(testData.NodeAId, nodeAkeyStore.PublicKey,
                    (a, b) => { return nodeAkeyStore.PublicKey; });

                hub.CommandRecieved += (sender, e) =>
                {
                    Trace.WriteLine("Hub command recieved: " + e.Command);

                    e.ResultSource.SetResult("ok");
                };

                var nodeResponse = hub.SendCommand("h2n", testData.NodeAId);

                NodeMessageRecieved.SetResult(nodeResponse.Result);


            });

            hubThread.Start();

            var nodeA = new NemesisNode(testData.NodeAId, testData.Ports, testData.Host.ToString(), false);
            nodeA.HubPublicKey = hubKeystore.PublicKey;
            nodeA.EnableEncryption(nodeAkeyStore);

            nodeA.CommandRecieved += (sender, e) =>
            {
                Trace.WriteLine("nodeA command recieved: " + e.Command);

                e.ResultSource.SetResult("ok");
            };

            nodeA.Connect();

            var hubResponse = nodeA.SendCommand("n2h");

            hubResponse.Wait(answerTimeout);
            var hubResult = hubResponse.IsCompleted ? hubResponse.Result : "timeout";
            Assert.AreEqual("ok", hubResult, "Hub -> Node command error");

            NodeMessageRecieved.Task.Wait(answerTimeout);
            var nodeResult = NodeMessageRecieved.Task.IsCompleted ? NodeMessageRecieved.Task.Result : "timeout";
            Assert.AreEqual("ok", nodeResult, "Node -> Hub command error");

        }

    }
}
