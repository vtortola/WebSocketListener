using System;

namespace vtortola.WebSockets.Rfc6455
{
    internal sealed class WebSocketFrameHeaderFlags
    {
        byte _byte1, _byte2;

        public bool FIN { get { return GetBit(_byte1, 7); } private set { SetBit(ref _byte1, 7, value); } }
        public bool RSV1 { get { return GetBit(_byte1, 6); } private set { SetBit(ref _byte1, 6, value); } }
        public bool RSV2 { get { return GetBit(_byte1, 5); } private set { SetBit(ref _byte1, 5, value); } }
        public bool RSV3 { get { return GetBit(_byte1, 4); } private set { SetBit(ref _byte1, 4, value); } }
        public bool OPT4 { get { return GetBit(_byte1, 3); } private set { SetBit(ref _byte1, 3, value); } }
        public bool OPT3 { get { return GetBit(_byte1, 2); } private set { SetBit(ref _byte1, 2, value); } }
        public bool OPT2 { get { return GetBit(_byte1, 1); } private set { SetBit(ref _byte1, 1, value); } }
        public bool OPT1 { get { return GetBit(_byte1, 0); } private set { SetBit(ref _byte1, 0, value); } }
        public bool MASK { get { return GetBit(_byte2, 7); } private set { SetBit(ref _byte2, 7, value); } }
        public WebSocketFrameOption Option { get; private set; }

        static void SetBit(ref byte aByte, int pos, bool value)
        {
            if (value)
            {
                //left-shift 1, then bitwise OR
                aByte = (byte)(aByte | (1 << pos));
            }
            else
            {
                //left-shift 1, then take complement, then bitwise AND
                aByte = (byte)(aByte & ~(1 << pos));
            }
        }

        static bool GetBit(byte aByte, int pos)
        {
            //left-shift 1, then bitwise AND, then check for non-zero
            return ((aByte & (1 << pos)) != 0);
        }

        public static bool TryParse(byte[] buffer, int offset, out WebSocketFrameHeaderFlags headerFlags)
        {
            headerFlags = null;
            if (buffer == null || buffer.Length - offset < 2)
                return false;

            var optionByte = buffer[0];
            SetBit(ref optionByte, 7, false);
            SetBit(ref optionByte, 6, false);
            SetBit(ref optionByte, 5, false);
            SetBit(ref optionByte, 4, false);
            int value = optionByte;
            value = value > 128 ? value - 128 : value;

            if (!Enum.IsDefined(typeof(WebSocketFrameOption), value))
                return false;

            headerFlags = new WebSocketFrameHeaderFlags(buffer[0],buffer[1],(WebSocketFrameOption)value); 
            return true;
        }

        private WebSocketFrameHeaderFlags(byte byte1, byte byte2, WebSocketFrameOption option)
        {
            _byte1 = byte1;
            _byte2 = byte2;
            Option = option;
        }

        public WebSocketFrameHeaderFlags(bool isComplete, WebSocketFrameOption option, WebSocketExtensionFlags extensionFlags)
        {
            this.Option = option;
            _byte1 = new byte();
            _byte2 = new byte();

            SetBit(ref _byte1, 7, isComplete);

            RSV1 = extensionFlags.Rsv1;
            RSV2 = extensionFlags.Rsv2;
            RSV3 = extensionFlags.Rsv3;

            switch (option)
            {
                case WebSocketFrameOption.Text:
                    OPT1 = true;
                    break;
                case WebSocketFrameOption.Binary:
                    OPT2 = true;
                    break;
                case WebSocketFrameOption.ConnectionClose:
                    OPT4 = true;
                    break;
                case WebSocketFrameOption.Ping:
                    OPT1 = OPT4 = true;
                    break;
                case WebSocketFrameOption.Pong:
                    OPT4 = OPT2 = true;
                    break;
                case WebSocketFrameOption.Continuation:
                    RSV1 = RSV2 = RSV3 = false;
                    break;
            }
        }
        public void ToBytes(long length, byte[] buffer, int offset)
        {
            int headerLength;
            if (length <= 125)
            {
                headerLength = (int)length;
            }
            else if (length <= ushort.MaxValue)
            {
                headerLength = 126;
            }
            else if ((ulong)length < ulong.MaxValue)
            {
                headerLength = 127;
            }
            else
            {
                throw new WebSocketException("Cannot create a header with a length of " + length);
            }

            buffer[offset] = _byte1;
            buffer[offset+1] = (byte)(_byte2 + headerLength);
        }
    }
}
