using Piksel.Nemesis.Security;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace Piksel.Nemesis
{
    public static class StreamUtils
    {
        public static byte[] PackUint16(UInt16 input)
        {
            byte[] output = new byte[2];

            output[0] = (byte)(input & 0xff);
            output[1] = (byte)(input >> 8 & 0xff);

            return output;
        }

        public static UInt16 UnpackUint16(byte[] input)
        {
            int msb = input[0];
            int lsb = input[1] << 8;

            return (ushort) (msb + lsb);
        }


    }
}
