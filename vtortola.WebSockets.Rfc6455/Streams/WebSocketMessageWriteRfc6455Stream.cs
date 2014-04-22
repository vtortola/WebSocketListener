using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Rfc6455
{
    public class WebSocketMessageWriteRfc6455Stream : WebSocketMessageWriteStream
    {
        Boolean _headerSent;
        Int32 _finished;

        readonly WebSocketRfc6455 _webSocket;
        readonly WebSocketMessageType _messageType;
        public WebSocketMessageWriteRfc6455Stream(WebSocketRfc6455 webSocket, WebSocketMessageType messageType)
        {
            if (webSocket == null)
                throw new ArgumentNullException("webSocket");

            _messageType = messageType;
            _webSocket = webSocket;
        }
        public WebSocketMessageWriteRfc6455Stream(WebSocketRfc6455 client, WebSocketMessageType messageType, WebSocketExtensionFlags extensionFlags)
            :this(client,messageType)
        {
            ExtensionFlags.Rsv1 = extensionFlags.Rsv1;
            ExtensionFlags.Rsv2 = extensionFlags.Rsv2;
            ExtensionFlags.Rsv3 = extensionFlags.Rsv3;
        }

        public override void Write(Byte[] buffer, Int32 offset, Int32 count)
        {
            if (_finished == 1)
                throw new WebSocketException("The write stream has been already flushed or disposed.");

            RemoveUTF8BOM(buffer, ref offset, ref count);
            if (count == 0)
                return;

            _webSocket.Connection.WriteInternal(buffer,offset, count, false, _headerSent, _messageType, ExtensionFlags);
            _headerSent = true;
        }
        public override async Task WriteAsync(Byte[] buffer, Int32 offset, Int32 count, CancellationToken cancellationToken)
        {
            if (_finished == 1)
                throw new WebSocketException("The write stream has been already flushed or disposed.");

            RemoveUTF8BOM(buffer, ref offset, ref count);
            if (count == 0)
                return;

            await _webSocket.Connection.WriteInternalAsync(buffer, offset, count, false, _headerSent, _messageType, ExtensionFlags, cancellationToken);
            _headerSent = true;
        }
        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.CompareExchange(ref _finished, 1, 0) == 0)
                await _webSocket.Connection.EndWrittingAsync();
        }
        public override void Close()
        {
            if (Interlocked.CompareExchange(ref _finished, 1, 0) == 0)
                _webSocket.Connection.EndWritting();
        }
    }
}
