using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Piksel.Nemesis.Utilities
{
    public static class NetworkStreamExtensions
    {
        public static void WriteSbyte(this NetworkStream stream, sbyte sb)
        {
            unchecked
            {
                stream.WriteByte((byte)sb);
            }
        }

        public static void WriteHandshakeResult(this NetworkStream stream, HandshakeResult hr)
        {
            stream.WriteSbyte((sbyte)hr);
        }

        public static sbyte ReadSbyte(this NetworkStream stream, out bool success)
        {
            var ib = stream.ReadByte();
            success = ib >= 0;
            if (!success)
                return sbyte.MaxValue;
            byte b = (byte)ib;
            unchecked
            {
                return (sbyte)b;
            }
        }

        public static HandshakeResult ReadHandshakeResult(this NetworkStream stream)
        {
            bool success;
            var sb = stream.ReadSbyte(out success);
            return success ? (HandshakeResult)sb : HandshakeResult.NODE_ERROR_CLOSED;
        }
    }

    public static class StringExtensions
    {
        public static string Truncate(this string s, int length)
        {
            if (string.IsNullOrEmpty(s) || s.Length < length)
                return s;
            return s.Substring(0, length) + "...";
        }
    }
}
