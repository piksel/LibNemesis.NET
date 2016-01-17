using Piksel.Nemesis.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace libnemesis.tests
{
    public static class Shared
    {
        static readonly int[] PORTS = { 8741, 8742 };

        public static TestData GetTestSettings()
        {
            var nodeAId = Guid.NewGuid();
            var nodeBId = Guid.NewGuid();

            var testData = new TestData(
                nodeAId,
                RSA.CreateNewKeyPair(),
                nodeBId,
                RSA.CreateNewKeyPair(),
                RSA.CreateNewKeyPair(),
                PORTS,
                IPAddress.Loopback
            );

            return testData;
        }
    }



    public struct TestData
    {

        readonly Guid _nodeAId;
        public Guid NodeAId { get { return _nodeAId; } }
        readonly byte[] _nodeAKeys;
        public byte[] NodeAKeys { get { return _nodeAKeys; } }

        readonly Guid _nodeBId;
        public Guid NodeBId { get { return _nodeBId; } }
        readonly byte[] _nodeBKeys;
        public byte[] NodeBKeys { get { return _nodeBKeys; } }

        readonly byte[] _hubKeys;
        public byte[] HubKeys { get { return _hubKeys; } }

        readonly int[] _ports;
        public int[] Ports { get { return _ports; } }
        readonly IPAddress _host;
        public IPAddress Host { get { return _host; } }

        public ManualResetEvent[] ResetEvents;

        public TestData(Guid nodeAId, byte[] nodeAKeys, Guid nodeBId,
            byte[] nodeBKeys, byte[] hubKeys, int[] ports, IPAddress host)
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
            _hubKeys = hubKeys;
            _ports = ports;
            _host = host;
        }

    }
}
