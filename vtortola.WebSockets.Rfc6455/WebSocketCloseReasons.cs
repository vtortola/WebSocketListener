using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Rfc6455
{
    public enum WebSocketCloseReasons : ushort
    {
        NormalClose=1000,
        GoingAway=1001,
        ProtocolError=1002,
        UnacceptableDataType=1003,
        InvalidData=1007,
        MessageViolatesPolicy=1008,
        MessageToLarge=1009,
        ExtensionRequired=1010,
        UnexpectedCondition=1011,
        TLSFailure=105,
    }

    internal static class WebSocketCloseReasonsExtensions
    {
        static Dictionary<WebSocketCloseReasons, Byte[]> _bytes;

        static WebSocketCloseReasonsExtensions()
        {
            _bytes = Enum.GetValues(typeof(WebSocketCloseReasons))
                         .Cast<WebSocketCloseReasons>()
                         .ToDictionary(v => v, v => BitConverter.GetBytes((UInt16)v));
        }

        internal static Byte[] GetBytes(this WebSocketCloseReasons reason)
        {
            return _bytes[reason];
        }
    }
}
