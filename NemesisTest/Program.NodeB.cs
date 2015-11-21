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

        }

        private static void NodeB_CommandRecieved(object sender, Nemesis.CommandRecievedEventArgs e)
        {
            e.ResultSource.SetResult("Node B Result");
        }
    }
}
