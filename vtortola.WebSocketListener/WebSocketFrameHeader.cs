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

        public static Boolean TryParse(Byte[] frameStart, Int32 count, out WebSocketFrameHeader header)
        {
            header = null;

            if (frameStart == null || frameStart.Length < 2 || count < 2)
                return false;

            Boolean isPartial = frameStart[0] <= 128;

            Int32 value = frameStart[0];
            value = value > 128 ? value - 128 : value;

            WebSocketFrameOption option = (WebSocketFrameOption)value;

            UInt64 contentLength= (UInt64)(frameStart[1] - 128);
            Int32 headerLength = 0;

            if (contentLength <= 125)
            {
                headerLength = 2;
            }
            else if (contentLength == 126)
            {
                if (frameStart.Length < 4 || count < 4)
                    return false;

                Byte b = frameStart[2];
                frameStart[2] = frameStart[3];
                frameStart[3] = b;

                contentLength = BitConverter.ToUInt16(frameStart, 2);
                headerLength = 4;
            }
            else if (contentLength == 127)
            {
                if (frameStart.Length < 10 || count < 10)
                    return false;

                Byte[] ui64 = new Byte[8];
                for (int i = 0; i < 8; i++)
                    ui64[i] = frameStart[9 - i];

                contentLength = (UInt64)BitConverter.ToUInt64(ui64, 0);
                headerLength = 10;
            }
            else
                throw new WebSocketException("Protocol error");

            header = new WebSocketFrameHeader(contentLength, isPartial, option);
            return true;
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
    }
}
