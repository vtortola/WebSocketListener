using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public sealed class WebSocketExtensionFlags
    {
        public Boolean Rsv1 { get; set; }
        public Boolean Rsv2 { get; set; }
        public Boolean Rsv3 { get; set; }

        public static readonly WebSocketExtensionFlags None = new WebSocketExtensionFlags();
    }
    public sealed class WebSocketFrameHeaderFlags
    {
        readonly Boolean[] _byte1, _byte2;

        public Boolean FIN { get { return _byte1[7]; } private set { _byte1[7] = value; } }
        public Boolean RSV1 { get { return _byte1[6]; } private set { _byte1[6] = value; } }
        public Boolean RSV2 { get { return _byte1[5]; } private set { _byte1[5] = value; } }
        public Boolean RSV3 { get { return _byte1[4]; } private set { _byte1[4] = value; } }
        public Boolean OPT4 { get { return _byte1[3]; } private set { _byte1[3] = value; } }
        public Boolean OPT3 { get { return _byte1[2]; } private set { _byte1[2] = value; } }
        public Boolean OPT2 { get { return _byte1[1]; } private set { _byte1[1] = value; } }
        public Boolean OPT1 { get { return _byte1[0]; } private set { _byte1[0] = value; } }
        public Boolean MASK { get { return _byte2[7]; } private set { _byte2[7] = value; } }
        public WebSocketFrameOption Option { get; private set; }

        public static Boolean TryParse(Byte[] buffer, Int32 offset, out WebSocketFrameHeaderFlags headerFlags)
        {
            headerFlags = null;
            if (buffer == null || buffer.Length - offset < 2)
                return false;

            Boolean[] byte1Flags = new Boolean[8];
            Boolean[] byte2Flags = new Boolean[8];

            Byte[] byte1 = new Byte[] { buffer[0] };
            Byte[] byte2 = new Byte[] { buffer[1] };

            BitArray bitArray = new BitArray(byte1);
            bitArray.CopyTo(byte1Flags, 0);

            bitArray = new BitArray(byte2);
            bitArray.CopyTo(byte2Flags, 0);

            Int32 value = buffer[0];
            value = value > 128 ? value - 128 : value;

            if (!Enum.IsDefined(typeof(WebSocketFrameOption), value))
                return false;

            headerFlags = new WebSocketFrameHeaderFlags(byte1Flags, byte2Flags ,(WebSocketFrameOption)value); 
            return true;
        }

        private WebSocketFrameHeaderFlags(Boolean[] byte1Flags, Boolean[] byte2Flags, WebSocketFrameOption option)
        {
            _byte1 = byte1Flags;
            _byte2 = byte2Flags;
            Option = option;
        }

        public WebSocketFrameHeaderFlags(bool isComplete, WebSocketFrameOption option, WebSocketExtensionFlags extensionFlags)
        {
            this.Option = option;
            _byte1 = new Boolean[8];
            _byte2 = new Boolean[8];

            _byte1[7] = isComplete;
            switch (option)
            {
                case WebSocketFrameOption.Text:
                    this.OPT1 = true;
                    break;
                case WebSocketFrameOption.Binary:
                    this.OPT2 = true;
                    break;
                case WebSocketFrameOption.ConnectionClose:
                    this.OPT4 = true;
                    break;
                case WebSocketFrameOption.Ping:
                    this.OPT1 = this.OPT4 = true;
                    break;
                case WebSocketFrameOption.Pong:
                    this.OPT4 = this.OPT2 = true;
                    break;
            }

            this.RSV1 = extensionFlags.Rsv1;
            this.RSV2 = extensionFlags.Rsv2;
            this.RSV3 = extensionFlags.Rsv3;
        }

        readonly Byte[] _byteHead = new Byte[2];
        public Byte[] ToBytes(UInt64 length)
        {
            Int32 headerLength;
            if (length <= 125)
                headerLength = (Int32)length;
            else if (length < UInt16.MaxValue)
                headerLength = 126;
            else if ((UInt64)length < UInt64.MaxValue)
                headerLength = 127;
            else
                throw new WebSocketException("Cannot create a header with a length of " + length);

            BitArray bitArray1 = new BitArray(_byte1);

            bitArray1.CopyTo(_byteHead, 0);

            BitArray bitArray2 = new BitArray(_byte2);
            bitArray2.CopyTo(_byteHead, 1);
            _byteHead[1] += (Byte)headerLength;

            return _byteHead;
        }
    }
}
