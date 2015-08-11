using System;
using vtortola.WebSockets.Tools;

namespace vtortola.WebSockets.Rfc6455
{
    public sealed class WebSocketFrameHeader
    {
        public Int64 ContentLength { get; private set; }
        public Int32 HeaderLength { get; private set; }
        public WebSocketFrameHeaderFlags Flags { get; private set; }
        public Int64 RemainingBytes { get; private set; }

        readonly ArraySegment<Byte> _key;
        Int32 cursor = 0;

        private WebSocketFrameHeader(ArraySegment<Byte> keySegment)
        {
            if (keySegment.Count != 4)
                throw new WebSocketException("The frame key must have a length of 4");
            _key = keySegment;
        }

        private WebSocketFrameHeader()
        {
        }

        public void DecodeBytes(Byte[] buffer, Int32 bufferOffset, Int32 readed)
        {
            if (Flags.MASK)
            {
                if (_key == null)
                    throw new WebSocketException("There is no key to decode the data");

                for (int i = bufferOffset; i < bufferOffset + readed; i++)
                {
                    buffer[i] = (Byte)(buffer[i] ^ _key.Array[_key.Offset + cursor++]);
                    if (cursor >= 4)
                        cursor = 0;
                }
            }

            RemainingBytes-= (Int64)readed;
        }

        public void ToBytes(Byte[] segment, Int32 offset)
        {
            this.Flags.ToBytes(this.ContentLength, segment, offset);
            if (this.ContentLength <= 125)
            { // header length is included in the 2b header
            }
            else if (this.ContentLength < UInt16.MaxValue)
                ((UInt16)this.ContentLength).ToBytesBackwards(segment, offset + 2);
            else if ((UInt64)this.ContentLength <= UInt64.MaxValue)
                ((UInt64)this.ContentLength).ToBytesBackwards(segment, offset + 2);
            else
                throw new WebSocketException("Invalid frame header length " + this.ContentLength);
        }

        public static Int32 GetHeaderLength(Byte[] frameStart, Int32 offset)
        {
            if (frameStart == null || frameStart.Length < offset + 2)
                throw new WebSocketException("The first two bytes of a header are required to understand the header length");

            Int32 value = frameStart[offset + 1];
            Boolean isMasked = value >= 128;
            Int32 contentLength = isMasked ? value - 128 : value;

            if (contentLength <= 125)
                return isMasked ? 6 : 2;
            else if (contentLength == 126)
                return (isMasked ? 6 : 2) + 2;
            else if (contentLength == 127)
                return (isMasked ? 6 : 2) + 8;
            else
                throw new WebSocketException("Cannot understand a length field of " + contentLength);
        }
        public static Boolean TryParse(Byte[] frameStart, Int32 offset, Int32 headerLength, ArraySegment<Byte> keySegment, out WebSocketFrameHeader header)
        {
            header = null;

            if (frameStart == null || frameStart.Length < 6 || frameStart.Length < (offset + headerLength))
                return false;

            Int32 value = frameStart[offset+1];
            Int64 contentLength = (Int64)(value>=128?value - 128:value);

            if (contentLength <= 125)
            {
                // small frame
            }
            else if (contentLength == 126)
            {
                if (frameStart.Length < headerLength)
                    return false;

                if(BitConverter.IsLittleEndian)
                    frameStart.ReversePortion(offset + 2, 2);
                contentLength = BitConverter.ToUInt16(frameStart, 2);
            }
            else if (contentLength == 127)
            {
                if (frameStart.Length < headerLength)
                    return false;

                if (BitConverter.IsLittleEndian)
                    frameStart.ReversePortion(offset + 2, 8);

                UInt64 length = BitConverter.ToUInt64(frameStart, 2);
                if(length > Int64.MaxValue)
                {
                    throw new WebSocketException("The maximum supported frame length is 9223372036854775807, current frame is " + length.ToString());
                }

                contentLength = (Int64)length;
            }
            else
                return false;

            WebSocketFrameHeaderFlags flags;
            if (WebSocketFrameHeaderFlags.TryParse(frameStart, offset, out flags))
            {
                header = new WebSocketFrameHeader(keySegment)
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
                        header._key.Array[header._key.Offset + i] = frameStart[offset + i + headerLength];
                }

                return true;
            }

            return false; 
        }
        public static WebSocketFrameHeader Create(Int64 count, Boolean isComplete, Boolean headerSent, WebSocketFrameOption option, WebSocketExtensionFlags extensionFlags)
        {
            var flags = new WebSocketFrameHeaderFlags(isComplete, headerSent ? WebSocketFrameOption.Continuation : option, extensionFlags);

            Int32 headerLength;
                        
            if (count <= 125)
                headerLength = 2;
            else if (count < UInt16.MaxValue)
                headerLength = 4;
            else if ((UInt64)count < UInt64.MaxValue)
                headerLength = 10;
            else
                throw new WebSocketException("Cannot create a header with a length of " + count);
            
            return new WebSocketFrameHeader()
            {
                HeaderLength = headerLength,
                ContentLength = (Int64)count,
                Flags = flags,
                RemainingBytes = (Int64)count
            };
        }
    }
}
