using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public sealed class WebSocketFrameHeader
    {
        public UInt64 ContentLength { get; private set; }
        public Int32 HeaderLength { get; private set; }
        public WebSocketFrameHeaderFlags Flags { get; private set; }
        public Byte[] Raw { get; set; }
        public UInt64 RemainingBytes { get; private set; }

        readonly Byte[] _key;

        public WebSocketFrameHeader()
        {
            _key = new Byte[4];
        }

        UInt64 cursor = 0;
        public Byte DecodeByte(Byte b)
        {
            RemainingBytes--;
            if (Flags.MASK)
                return (Byte)(b ^ _key[cursor++ % 4]);
            else
                return b;
        }

        public static Boolean TryParse(Byte[] frameStart, Int32 offset, Int32 count, out WebSocketFrameHeader header)
        {
            header = null;

            if (frameStart == null || frameStart.Length < 6 || count < 6 || frameStart.Length - (offset + count) < 0)
                return false;

            // First Byte
            Int32 value = frameStart[offset];
            value = value >= 128 ? value - 128 : value; // completed or not

            // Second Byte
            value = frameStart[offset+1];
            Boolean isMasked = value >= 128;

            UInt64 contentLength = (UInt64)(isMasked?value - 128:value);
            Int32 headerLength = isMasked?6:2; // if masked the key is 4 bytes

            if (contentLength <= 125)
            {
                // small frame
            }
            else if (contentLength == 126)
            {
                if (frameStart.Length < (headerLength + 2) || count < (headerLength + 2))
                    return false;

                frameStart.ReversePortion(offset + 2, 2);
                contentLength = BitConverter.ToUInt16(frameStart, 2);
                frameStart.ReversePortion(offset + 2, 2);

                headerLength += 2;
            }
            else if (contentLength == 127)
            {
                if (frameStart.Length < (headerLength + 8) || count < (headerLength + 8))
                    return false;

                frameStart.ReversePortion(offset + 2, 8);
                contentLength = (UInt64)BitConverter.ToUInt64(frameStart, 2);
                frameStart.ReversePortion(offset + 2, 8);

                headerLength += 8;
            }
            else
                return false;

            WebSocketFrameHeaderFlags flags;
            if (WebSocketFrameHeaderFlags.TryParse(frameStart,offset,out flags))
            {
                header = new WebSocketFrameHeader()
                {
                    ContentLength = contentLength,
                    HeaderLength = headerLength,
                    Flags = flags,
                    RemainingBytes = contentLength
                };

                if (flags.MASK)
                {
                    headerLength -= 4;
                    for (int i = 0; i < 4; i++)
                        header._key[i] = frameStart[offset + i + headerLength];
                }

                return true;
            }

            return false; 
        }

        public static WebSocketFrameHeader Create(Int32 count, Boolean isComplete, Boolean headerSent, WebSocketFrameOption option, WebSocketExtensionFlags extensionFlags)
        {
            var flags = new WebSocketFrameHeaderFlags(isComplete, headerSent ? WebSocketFrameOption.Continuation : option, extensionFlags);

            Int32 headerLength;
            var raw = flags.ToBytes((UInt64)count);

            List<Byte> arrayMaker = new List<Byte>(raw);
            
            if (count <= 125)
            {
                headerLength = 2;
            }
            else if (count < UInt16.MaxValue)
            {
                Byte[] i16 = BitConverter.GetBytes((UInt16)count).Reverse().ToArray();
                arrayMaker.AddRange(i16);
                headerLength = 4;
            }
            else if ((UInt64)count < UInt64.MaxValue)
            {
                arrayMaker.AddRange(BitConverter.GetBytes((UInt64)count).Reverse().ToArray());
                headerLength = 10;
            }
            else
                throw new WebSocketException("Cannot create a header with a length of " + count);
            
            return new WebSocketFrameHeader()
            {
                HeaderLength = headerLength,
                ContentLength = (UInt64)count,
                Flags = flags,
                Raw = arrayMaker.ToArray(),
                RemainingBytes = (UInt64)count
            };
        }
    }
}
