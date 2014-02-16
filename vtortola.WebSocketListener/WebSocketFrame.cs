using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public sealed class WebSocketFrame
    {
        public Byte[] Data { get; private set; }

        public WebSocketFrameHeader Header { get; private set; }

        public WebSocketFrame(WebSocketFrameHeader header, Byte[] data)
        {
            Header = header;
            var key = new Byte[4];
            Data = new Byte[header.ContentLength];
            Array.Copy(data, 0, key, 0, 4);
            Array.Copy(data, 4, Data, 0, header.ContentLength);
            Decode(Data, key, 0, header.ContentLength);
        }

        private void Decode(Byte[] array, Byte[] key, Int32 start, Int32 count)
        {
            for (int i = start; i < array.Length; i++)
                array[i] = (Byte)(array[i] ^ key[(i - start) % 4]);
        }

        public WebSocketFrame(Byte[] data, WebSocketFrameOption option)
        {
            Header = new WebSocketFrameHeader(data.Length, false, option);

            List<Byte> arrayMaker = new List<Byte>();
            arrayMaker.AddRange(Header.Raw);
            arrayMaker.AddRange(data);
            Data = arrayMaker.ToArray();
        }
    }

}
