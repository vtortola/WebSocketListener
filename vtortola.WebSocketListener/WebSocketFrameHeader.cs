using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public sealed class WebSocketFrameHeader
    {
        public UInt64 ContentLength { get; private set; }
        public Boolean IsPartial { get; private set; }
        public Int32 HeaderLength { get; private set; }
        public WebSocketFrameOption Option { get; private set; }
        public Byte[] Raw { get; set; }

        Byte[] _key;

        public static Boolean TryParse(Byte[] frameStart, Int32 offset, Int32 count, out WebSocketFrameHeader header)
        {
            header = null;

            if (frameStart == null || frameStart.Length < 6 || count < 6 || frameStart.Length - (offset + count) < 0)
                return false;

            Boolean isPartial = frameStart[offset] <= 128;

            Int32 value = frameStart[offset];
            value = value > 128 ? value - 128 : value;

            WebSocketFrameOption option = (WebSocketFrameOption)value;

            UInt64 contentLength = (UInt64)(frameStart[offset+1] - 128);
            Int32 headerLength = 0;

            if (contentLength <= 125)
            {
                headerLength = 6;
            }
            else if (contentLength == 126)
            {
                if (frameStart.Length < 8 || count < 8)
                    return false;

                Byte b = frameStart[offset+2];
                frameStart[offset + 2] = frameStart[offset + 3];
                frameStart[offset + 3] = b;

                contentLength = BitConverter.ToUInt16(frameStart, 2);
                headerLength = 8;
            }
            else if (contentLength == 127)
            {
                if (frameStart.Length < 14 || count < 14)
                    return false;

                Byte[] ui64 = new Byte[8];
                for (int i = 0; i < 8; i++)
                    ui64[i] = frameStart[offset + (9 - i)];

                contentLength = (UInt64)BitConverter.ToUInt64(ui64, 0);
                headerLength = 14;
            }
            else
                throw new WebSocketException("Protocol error");

            header = new WebSocketFrameHeader()
            {
                ContentLength = contentLength,
                HeaderLength = headerLength,
                IsPartial = isPartial,
                Option = option
            };

            headerLength -= 4;
            for (int i = 0; i < 4; i++)
                header._key[i] = frameStart[offset + i + headerLength];

            return true;
        }

        public static WebSocketFrameHeader Create(Int32 count, Boolean isPartial, WebSocketFrameOption option)
        {
            List<Byte> arrayMaker = new List<Byte>();
            Int32 value = isPartial ? 0 : 128;
            arrayMaker.Add((Byte)(value + (Int32)option));
            Int32 headerLength = 2;

            if (count <= 125)
            {
                arrayMaker.Add((Byte)(count));
                headerLength = 2;
            }
            else if (count < UInt16.MaxValue)
            {
                arrayMaker.Add(126);
                Byte[] i16 = BitConverter.GetBytes((UInt16)count).Reverse().ToArray();
                arrayMaker.AddRange(i16);
                headerLength = 4;
            }
            else if ((UInt64)count < UInt64.MaxValue)
            {
                arrayMaker.Add(127);
                arrayMaker.AddRange(BitConverter.GetBytes((UInt64)count).Reverse().ToArray());
                headerLength = 10;
            }

            return new WebSocketFrameHeader()
            {
                HeaderLength = headerLength,
                ContentLength = (UInt64)count,
                Option = option,
                IsPartial = isPartial,
                Raw = arrayMaker.ToArray()
            };
        }

        public Byte DecodeByte(Byte b, UInt64 position)
        {
            return (Byte)(b ^ _key[position++ % 4]);
        }

        public WebSocketFrameHeader()
        {
            _key = new Byte[4];
        }


        public ulong Cursor { get; set; }
    }

    internal static class ByteArrayExtensions
    {
        internal static void ShiftLeft(this Byte[] array, Int32 from, Int32 count)
        {
            if (count + from > array.Length)
                throw new ArgumentException("The array is to small");

            if (count < 1)
                return;
            
            for (int i = 0; i < count; i++)
                array[i] = array[i+from];
        }
    }
}
