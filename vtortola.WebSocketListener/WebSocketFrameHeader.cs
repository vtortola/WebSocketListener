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

        public WebSocketFrameHeader(Byte[] header)
        {
            IdentifyHeader(header[0]);

            ContentLength = (UInt64)(header[1] - 128);
            if (ContentLength <= 125)
            {
                HeaderLength = 2;
            }
            else if (ContentLength == 126)
            {
                Byte b = header[2];
                header[2] = header[3];
                header[3] = b;

                ContentLength = BitConverter.ToUInt16(header, 2);
                HeaderLength = 4;
            }
            else if (ContentLength == 127)
            {
                Byte[] ui64 = new Byte[8];
                for (int i = 0; i < 8; i++)
                    ui64[i] = header[9-i];

                // if it is bigger... well, fuck
                ContentLength = BitConverter.ToUInt64(ui64,0);
                HeaderLength = 10;
            }
            else
                throw new WebSocketException("Protocol error");

            Raw = new Byte[HeaderLength];
            Array.Copy(header, 0, Raw, 0, HeaderLength);
        }
        public WebSocketFrameHeader(UInt64 contentLength, Boolean isPartial, WebSocketFrameOption option)
        {
            List<Byte> arrayMaker = new List<Byte>();
            Int32 value = isPartial ? 0 : 128;
            arrayMaker.Add((Byte)(value + (Int32)option));

            if (contentLength <= 125)
            {
                arrayMaker.Add((Byte)(contentLength));
                HeaderLength = 2;
            }
            else if (contentLength < UInt16.MaxValue)
            {
                arrayMaker.Add(126);
                Byte[] i16 = BitConverter.GetBytes((UInt16)contentLength).Reverse().ToArray();
                arrayMaker.AddRange(i16);
                HeaderLength = 4;
            }
            else if ((UInt64)contentLength < UInt64.MaxValue)
            {
                arrayMaker.Add(127);
                arrayMaker.AddRange(BitConverter.GetBytes((UInt64)contentLength).Reverse().ToArray());
                HeaderLength = 10;
            }

            ContentLength = contentLength;
            Raw = arrayMaker.ToArray();
            Option = option;
        }

        private void IdentifyHeader(Int32 value)
        {
            IsPartial = value <= 128;
            value = value > 128 ? value - 128 : value;

            switch (value)
            {
                case 0: Option = WebSocketFrameOption.Continuation;
                    break;
                case 1: Option = WebSocketFrameOption.Text;
                    break;
                case 2: Option = WebSocketFrameOption.Binary;
                    break;
                case 8: Option = WebSocketFrameOption.ConnectionClose;
                    break;
                case 9: Option = WebSocketFrameOption.Ping;
                    break;
                case 10: Option = WebSocketFrameOption.Ping;
                    break;
                default: throw new WebSocketException("Unrecognized opt code");
            }
        }
    }
}
