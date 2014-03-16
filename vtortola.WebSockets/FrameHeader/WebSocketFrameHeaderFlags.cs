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
        Boolean _rsv1, _rsv2, _rsv3;
        public Boolean Rsv1 { get { return _rsv1; } set { _rsv1 = value && !_none; } }
        public Boolean Rsv2 { get { return _rsv2; } set { _rsv2 = value && !_none; } }
        public Boolean Rsv3 { get { return _rsv3; } set { _rsv3 = value && !_none; } }

        readonly Boolean _none;
        public WebSocketExtensionFlags()
        {
            _none = false;
        }

        private WebSocketExtensionFlags(Boolean none)
        {
            _none = true;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly WebSocketExtensionFlags None = new WebSocketExtensionFlags(true);
    }

    public sealed class WebSocketFrameHeaderFlags
    {
        Byte _byte1, _byte2;

        public Boolean FIN { get { return GetBit(_byte1, 7); } private set { SetBit(ref _byte1, 7, value); } }
        public Boolean RSV1 { get { return GetBit(_byte1, 6); } private set { SetBit(ref _byte1, 6, value); } }
        public Boolean RSV2 { get { return GetBit(_byte1, 5); } private set { SetBit(ref _byte1, 5, value); } }
        public Boolean RSV3 { get { return GetBit(_byte1, 4); } private set { SetBit(ref _byte1, 4, value); } }
        public Boolean OPT4 { get { return GetBit(_byte1, 3); } private set { SetBit(ref _byte1, 3, value); } }
        public Boolean OPT3 { get { return GetBit(_byte1, 2); } private set { SetBit(ref _byte1, 2, value); } }
        public Boolean OPT2 { get { return GetBit(_byte1, 1); } private set { SetBit(ref _byte1, 1, value); } }
        public Boolean OPT1 { get { return GetBit(_byte1, 0); } private set { SetBit(ref _byte1, 0, value); } }
        public Boolean MASK { get { return GetBit(_byte2, 7); } private set { SetBit(ref _byte2, 7, value); } }
        public WebSocketFrameOption Option { get; private set; }

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

        public static Boolean TryParse(Byte[] buffer, Int32 offset, out WebSocketFrameHeaderFlags headerFlags)
        {
            headerFlags = null;
            if (buffer == null || buffer.Length - offset < 2)
                return false;

            var optionByte = buffer[0];
            SetBit(ref optionByte, 7, false);
            SetBit(ref optionByte, 6, false);
            SetBit(ref optionByte, 5, false);
            SetBit(ref optionByte, 4, false);
            Int32 value = optionByte;
            value = value > 128 ? value - 128 : value;

            if (!Enum.IsDefined(typeof(WebSocketFrameOption), value))
                return false;

            headerFlags = new WebSocketFrameHeaderFlags(buffer[0],buffer[1],(WebSocketFrameOption)value); 
            return true;
        }

        private WebSocketFrameHeaderFlags(Byte byte1, Byte byte2, WebSocketFrameOption option)
        {
            _byte1 = byte1;
            _byte2 = byte2;
            Option = option;
        }

        public WebSocketFrameHeaderFlags(bool isComplete, WebSocketFrameOption option, WebSocketExtensionFlags extensionFlags)
        {
            this.Option = option;
            _byte1 = new Byte();
            _byte2 = new Byte();

            SetBit(ref _byte1, 7, isComplete);

            this.RSV1 = extensionFlags.Rsv1;
            this.RSV2 = extensionFlags.Rsv2;
            this.RSV3 = extensionFlags.Rsv3;

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

        public void ToBytes(UInt64 length, Byte[] buffer)
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

            buffer[0] = _byte1;
            buffer[1] = (Byte)(_byte2 + headerLength);
        }
    }
}
