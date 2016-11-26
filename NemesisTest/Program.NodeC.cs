using Piksel.Nemesis.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Nemesis = Piksel.Nemesis;

namespace NemesisTest
{
    partial class Program
    {
        private static void threadProcNodeC(object testData_)
        {
            var testData = (TestData)testData_;
            var _log = NLog.LogManager.GetLogger("NodeCThread");

            _log.Info("Creating node C...");

            var nodeC = new Nemesis.NemesisNode(testData.NodeAId, testData.Ports, testData.Host.ToString(), false);

            nodeC.SetLogName("nNodeC");

            nodeC.CommandReceived += NodeC_CommandReceived;

            nodeC.Connect();


            var cmdTest = "n2h";
            _log.Info($"Sending command \"{cmdTest}\" (1/2) from Node C to Hub");

            var response = nodeC.SendCommand(cmdTest);
            _log.Info("Got response: {0}", response.Result);



            _log.Info($"Sending command \"{cmdTest}\" (2/2) from Node C to Hub");
            response = nodeC.SendCommand(cmdTest);
            _log.Info("Got response: {0}", response.Result);



        }

        private static void NodeC_CommandReceived(object sender, Piksel.Nemesis.CommandReceivedEventArgs e)
        {
            e.ResultSource.SetResult("Node C Result");
        }
    }
}
