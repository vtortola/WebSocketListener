﻿using System.Text;
using vtortola.WebSockets.Tools;

namespace vtortola.WebSockets.Rfc6455
{
    internal sealed class WebSocketFrameHeaderFlags
    {
        private byte _byte1, _byte2;

        public bool FIN { get { return GetBit(_byte1, 7); } private set { SetBit(ref _byte1, 7, value); } }
        public bool RSV1 { get { return GetBit(_byte1, 6); } private set { SetBit(ref _byte1, 6, value); } }
        public bool RSV2 { get { return GetBit(_byte1, 5); } private set { SetBit(ref _byte1, 5, value); } }
        public bool RSV3 { get { return GetBit(_byte1, 4); } private set { SetBit(ref _byte1, 4, value); } }
        public bool OPT4 { get { return GetBit(_byte1, 3); } private set { SetBit(ref _byte1, 3, value); } }
        public bool OPT3 { get { return GetBit(_byte1, 2); } private set { SetBit(ref _byte1, 2, value); } }
        public bool OPT2 { get { return GetBit(_byte1, 1); } private set { SetBit(ref _byte1, 1, value); } }
        public bool OPT1 { get { return GetBit(_byte1, 0); } private set { SetBit(ref _byte1, 0, value); } }
        public bool MASK { get { return GetBit(_byte2, 7); } private set { SetBit(ref _byte2, 7, value); } }

        public WebSocketFrameOption Option { get; }

        public static void SetBit(ref byte aByte, int pos, bool value)
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

        public static bool GetBit(byte aByte, int pos)
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
            var options = (WebSocketFrameOption)optionByte;
            options = options > (WebSocketFrameOption)128 ? options - 128 : options;

            if (EnumHelper<WebSocketFrameOption>.IsDefined(options) == false)
                return false;

            headerFlags = new WebSocketFrameHeaderFlags(buffer[0], buffer[1], options);

            if (options > WebSocketFrameOption.Binary)
                headerFlags.FIN = true; // control frames is always final

            return true;
        }
        private WebSocketFrameHeaderFlags(byte byte1, byte byte2, WebSocketFrameOption option)
        {
            _byte1 = byte1;
            _byte2 = byte2;
            Option = option;
        }
        public WebSocketFrameHeaderFlags(bool isComplete, bool isMasked, WebSocketFrameOption option, WebSocketExtensionFlags extensionFlags)
        {
            this.Option = option;
            _byte1 = new byte();
            _byte2 = new byte();

            SetBit(ref _byte1, 7, isComplete);

            this.RSV1 = extensionFlags.Rsv1;
            this.RSV2 = extensionFlags.Rsv2;
            this.RSV3 = extensionFlags.Rsv3;
            this.MASK = isMasked;

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
                case WebSocketFrameOption.Continuation:
                    this.RSV1 = this.RSV2 = this.RSV3 = false;
                    break;
            }
        }
        public void ToBytes(long length, byte[] buffer, int offset)
        {
            int headerLength;
            if (length <= 125)
                headerLength = (int)length;
            else if (length <= ushort.MaxValue)
                headerLength = 126;
            else if ((ulong)length < ulong.MaxValue)
                headerLength = 127;
            else
                throw new WebSocketException("Cannot create a header with a length of " + length);

            buffer[offset] = _byte1;
            buffer[offset + 1] = (byte)(_byte2 + headerLength);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var sb = new StringBuilder();
            if (FIN) sb.Append("FIN,");
            if (RSV1) sb.Append("RSV1,");
            if (RSV2) sb.Append("RSV2,");
            if (RSV3) sb.Append("RSV3,");
            if (MASK) sb.Append("MASK,");
            if (sb.Length > 0)
                sb.Length--;
            return sb.ToString();
        }
    }
}
