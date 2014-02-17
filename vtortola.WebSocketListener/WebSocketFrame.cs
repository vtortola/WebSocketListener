using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public sealed class WebSocketFrame
    {
        public WebSocketFrameHeader Header { get; private set; }

        public MemoryStream StreamData { get; private set; }

        private Int32 _keyCount, _keyCursor;
        private Byte[] _key;

        public WebSocketFrame(WebSocketFrameHeader header)
        {
            Header = header;
            Int32 capacity = header.ContentLength > Int32.MaxValue ? Int32.MaxValue : (Int32)header.ContentLength;
            StreamData = new MemoryStream(capacity);
            _key = new Byte[4];
            _keyCount = _keyCursor = 0;
        }

        public void Write(Byte[] buffer, Int32 offset, Int32 length)
        {
            length += offset;
            if (_keyCount < 4)
            {
                var limit = Math.Min(length, 4 - _keyCount) + offset;
                for (offset = offset; offset < limit; offset++)
                    _key[_keyCount++] = buffer[offset];
            }

            if (_keyCount != 4 && offset < length)
                throw new ArgumentException("The key is not set but there is still data in the buffer.");

            if (_keyCount == 4 && offset < length)
            {
                Decode(buffer, offset, length - offset);
                StreamData.Write(buffer, offset, length - offset);
            }
        }

        private void Decode(Byte[] array, Int32 offset, Int32 count)
        {
            for (int i = offset; i < count + offset; i++)
                array[i] = (Byte)(array[i] ^ _key[_keyCursor++ % 4]);
        }

        public WebSocketFrame(Byte[] data, WebSocketFrameOption option)
        {
            Header = new WebSocketFrameHeader((UInt64)data.Length, false, option);
            StreamData = new MemoryStream(data);
        }
    }

}
