using System;

namespace vtortola.WebSockets.Rfc6455
{
    internal sealed class WebSocketFrameHeader
    {
        public long ContentLength { get; private set; }
        public int HeaderLength { get; private set; }
        public WebSocketFrameHeaderFlags Flags { get; private set; }
        public long RemainingBytes { get; private set; }

        readonly ArraySegment<byte> _key;
        int cursor = 0;

        private WebSocketFrameHeader(ArraySegment<byte> keySegment)
        {
            if (keySegment.Count != 4)
                throw new WebSocketException("The frame key must have a length of 4");
            _key = keySegment;
        }

        private WebSocketFrameHeader()
        {
        }

        public void DecodeBytes(byte[] buffer, int bufferOffset, int readed)
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

            RemainingBytes-= readed;
        }

        public void ToBytes(byte[] segment, int offset)
        {
            Flags.ToBytes(ContentLength, segment, offset);
            if (ContentLength <= 125)
            { // header length is included in the 2b header
            }
            else if (ContentLength <= ushort.MaxValue)
            {
                ((ushort)ContentLength).ToBytesBackwards(segment, offset + 2);
            }
            else if ((ulong)this.ContentLength <= ulong.MaxValue)
            {
                ((ulong)ContentLength).ToBytesBackwards(segment, offset + 2);
            }
            else
            {
                throw new WebSocketException("Invalid frame header length " + ContentLength);
            }
        }

        public static int GetHeaderLength(byte[] frameStart, int offset)
        {
            if (frameStart == null || frameStart.Length < offset + 2)
                throw new WebSocketException("The first two bytes of a header are required to understand the header length");

            var value = frameStart[offset + 1];
            var isMasked = value >= 128;
            var contentLength = isMasked ? value - 128 : value;

            if (contentLength <= 125)
            {
                return isMasked ? 6 : 2;
            }
            else if (contentLength == 126)
            {
                return (isMasked ? 6 : 2) + 2;
            }
            else if (contentLength == 127)
            {
                return (isMasked ? 6 : 2) + 8;
            }
            else
            {
                throw new WebSocketException("Cannot understand a length field of " + contentLength);
            }
        }
        public static Boolean TryParse(byte[] frameStart, int offset, int headerLength, ArraySegment<byte> keySegment, out WebSocketFrameHeader header)
        {
            header = null;

            if (frameStart == null || frameStart.Length < 6 || frameStart.Length < (offset + headerLength))
                return false;

            var value = frameStart[offset+1];
            long contentLength = value >= 128 ? value - 128 : value;

            if (contentLength <= 125)
            {
                // small frame
            }
            else if (contentLength == 126)
            {
                if (frameStart.Length < headerLength)
                    return false;
                
                contentLength = frameStart.ToUInt16(2, BitConverter.IsLittleEndian);
            }
            else if (contentLength == 127)
            {
                if (frameStart.Length < headerLength)
                    return false;

                var length = frameStart.ToUInt64(2, BitConverter.IsLittleEndian);
                if (length > long.MaxValue)
                {
                    throw new WebSocketException("The maximum supported frame length is 9223372036854775807, current frame is " + length.ToString());
                }

                contentLength = (long)length;
            }
            else
            {
                return false;
            }

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
        public static WebSocketFrameHeader Create(long count, bool isComplete, bool headerSent, WebSocketFrameOption option, WebSocketExtensionFlags extensionFlags)
        {
            var flags = new WebSocketFrameHeaderFlags(isComplete, headerSent ? WebSocketFrameOption.Continuation : option, extensionFlags);

            int headerLength;

            if (count <= 125)
            {
                headerLength = 2;
            }
            else if (count <= ushort.MaxValue)
            {
                headerLength = 4;
            }
            else if ((ulong)count < ulong.MaxValue)
            {
                headerLength = 10;
            }
            else
            {
                throw new WebSocketException("Cannot create a header with a length of " + count);
            }
            
            return new WebSocketFrameHeader()
            {
                HeaderLength = headerLength,
                ContentLength = count,
                Flags = flags,
                RemainingBytes = count
            };
        }
    }
}
