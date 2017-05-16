using System;

namespace vtortola.WebSockets.Rfc6455
{
    internal sealed class WebSocketFrameHeader
    {
        private readonly ArraySegment<byte> _key;
        private int cursor;

        public long ContentLength { get; private set; }
        public int HeaderLength { get; private set; }
        public WebSocketFrameHeaderFlags Flags { get; private set; }
        public long RemainingBytes { get; private set; }

        private WebSocketFrameHeader(ArraySegment<byte> keySegment)
        {
            _key = keySegment;
        }

        public void DecodeBytes(byte[] buffer, int bufferOffset, int count)
        {
            RemainingBytes -= count;

            if (!this.Flags.MASK) return;

            for (var i = bufferOffset; i < bufferOffset + count; i++)
            {
                buffer[i] = (byte)(buffer[i] ^ this._key.Array[this._key.Offset + this.cursor++]);
                if (this.cursor >= 4)
                    this.cursor = 0;
            }
        }
        public void EncodeBytes(byte[] buffer, int bufferOffset, int count)
        {
            RemainingBytes -= count;

            if (!this.Flags.MASK) return;

            for (var i = bufferOffset; i < bufferOffset + count; i++)
            {
                buffer[i] = (byte)(buffer[i] ^ this._key.Array[this._key.Offset + this.cursor++]);
                if (this.cursor >= 4)
                    this.cursor = 0;
            }
        }

        public int ToBytes(byte[] segment, int offset)
        {
            this.Flags.ToBytes(this.ContentLength, segment, offset);
            var written = 2;
            if (this.ContentLength <= 125)
            {
                // header length is included in the 2b header
            }
            else if (this.ContentLength <= ushort.MaxValue)
            {
                ((ushort)this.ContentLength).ToBytesBackwards(segment, offset + written);
                written += 2;
            }
            else
            {
                ((ulong)this.ContentLength).ToBytesBackwards(segment, offset + written);
                written += 8;
            }
            // write mask is it is masked message
            if (this.Flags.MASK)
            {
                Buffer.BlockCopy(_key.Array, _key.Offset, segment, offset + written, _key.Count);
                written += _key.Count;
            }
            return written;
        }

        public static int GetHeaderLength(byte[] frameStart, int offset)
        {
            if (frameStart == null || frameStart.Length < offset + 2)
                throw new WebSocketException("The first two bytes of a header are required to understand the header length");

            var value = frameStart[offset + 1];
            var isMasked = value >= 128;
            var contentLength = isMasked ? value - 128 : value;

            if (contentLength <= 125)
                return isMasked ? 6 : 2;
            else if (contentLength == 126)
                return (isMasked ? 6 : 2) + 2;
            else if (contentLength == 127)
                return (isMasked ? 6 : 2) + 8;
            else
                throw new WebSocketException("Cannot understand a length field of " + contentLength);
        }
        public static bool TryParse(byte[] frameStart, int offset, int headerLength, ArraySegment<byte> keySegment, out WebSocketFrameHeader header)
        {
            if (frameStart == null) throw new ArgumentNullException(nameof(frameStart));

            if (keySegment.Count != 4)
                throw new WebSocketException("The frame key must have a length of 4");

            header = null;

            if (frameStart == null || frameStart.Length < 6 || frameStart.Length < (offset + headerLength))
                return false;

            int value = frameStart[offset + 1];
            long contentLength = value >= 128 ? value - 128 : value;

            if (contentLength <= 125)
            {
                // small frame
            }
            else if (contentLength == 126)
            {
                if (frameStart.Length < headerLength)
                    return false;

                if (BitConverter.IsLittleEndian)
                    frameStart.ReversePortion(offset + 2, 2);
                contentLength = BitConverter.ToUInt16(frameStart, 2);
            }
            else if (contentLength == 127)
            {
                if (frameStart.Length < headerLength)
                    return false;

                if (BitConverter.IsLittleEndian)
                    frameStart.ReversePortion(offset + 2, 8);

                ulong length = BitConverter.ToUInt64(frameStart, 2);
                if (length > long.MaxValue)
                {
                    throw new WebSocketException("The maximum supported frame length is 9223372036854775807, current frame is " + length.ToString());
                }

                contentLength = (long)length;
            }
            else
                return false;

            WebSocketFrameHeaderFlags flags;
            if (!WebSocketFrameHeaderFlags.TryParse(frameStart, offset, out flags))
                return false;

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
                for (var i = 0; i < 4; i++)
                    header._key.Array[header._key.Offset + i] = frameStart[offset + i + headerLength];
            }

            return true;
        }
        public static WebSocketFrameHeader Create(long contentLength, bool isComplete, bool headerSent, ArraySegment<byte> keySegment, WebSocketFrameOption option, WebSocketExtensionFlags extensionFlags)
        {
            if (extensionFlags == null) throw new ArgumentNullException(nameof(extensionFlags));

            var isMasked = keySegment.Array != null;
            if (isMasked && keySegment.Count != 4)
                throw new WebSocketException("The frame key must have a length of 4");

            var flags = new WebSocketFrameHeaderFlags(isComplete, isMasked, headerSent ? WebSocketFrameOption.Continuation : option, extensionFlags);

            int headerLength;
            if (contentLength <= 125)
                headerLength = 2;
            else if (contentLength <= ushort.MaxValue)
                headerLength = 4;
            else 
                headerLength = 10;

            if (isMasked)
                headerLength += keySegment.Count;

            return new WebSocketFrameHeader(keySegment)
            {
                HeaderLength = headerLength,
                ContentLength = contentLength,
                Flags = flags,
                RemainingBytes = contentLength
            };
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{Flags.Option}, len: {this.ContentLength}, key: {(Flags.MASK ? BitConverter.ToUInt32(_key.Array, _key.Offset).ToString("X") : "0")}, flags: {Flags}";
        }
    }
}
