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
        Boolean _headerSent = false;
        Int32 _finished, _internalUsedBufferLength;

        readonly WebSocketRfc6455 _webSocket;
        readonly WebSocketMessageType _messageType;
        public WebSocketMessageWriteRfc6455Stream(WebSocketRfc6455 webSocket, WebSocketMessageType messageType)
        {
            if (webSocket == null)
                throw new ArgumentNullException("webSocket");

            _internalUsedBufferLength = 0;
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
        private void BufferData(Byte[] buffer, ref Int32 offset, ref Int32 count)
        {
            var read = Math.Min(count, _webSocket.Connection.SendBuffer.Count - _internalUsedBufferLength);
            if (read == 0)
                return;
            Array.Copy(buffer, offset, _webSocket.Connection.SendBuffer.Array, _webSocket.Connection.SendBuffer.Offset + _internalUsedBufferLength, read);
            _internalUsedBufferLength += read;
            offset += read;
            count -= read;
        }
        public override void Write(Byte[] buffer, Int32 offset, Int32 count)
        {
            if (_finished == 1)
                throw new WebSocketException("The write stream has been already flushed or disposed.");

            RemoveUTF8BOM(buffer, ref offset, ref count);
            if (count == 0)
                return;

            while (count > 0)
            {
                BufferData(buffer, ref offset, ref count);

                if (_internalUsedBufferLength == _webSocket.Connection.SendBuffer.Count && count > 0)
                {
                    _webSocket.Connection.WriteInternal(_webSocket.Connection.SendBuffer, _internalUsedBufferLength, false, _headerSent, _messageType, ExtensionFlags);
                    _internalUsedBufferLength = 0;
                    _headerSent = true;
                }
            }
        }
        public override async Task WriteAsync(Byte[] buffer, Int32 offset, Int32 count, CancellationToken cancellationToken)
        {
            if (_finished == 1)
                throw new WebSocketException("The write stream has been already flushed or disposed.");

            RemoveUTF8BOM(buffer, ref offset, ref count);
            if (count == 0)
                return;

            while (count > 0)
            {
                BufferData(buffer, ref offset, ref count);

                if (_internalUsedBufferLength == _webSocket.Connection.SendBuffer.Count && count > 0)
                {
                    await _webSocket.Connection.WriteInternalAsync(_webSocket.Connection.SendBuffer, _internalUsedBufferLength, false, _headerSent, _messageType, ExtensionFlags, cancellationToken);
                    _internalUsedBufferLength = 0;
                    _headerSent = true;
                }
            }
        }
        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.CompareExchange(ref _finished, 1, 0) == 0)
            {
                await _webSocket.Connection.WriteInternalAsync(_webSocket.Connection.SendBuffer, _internalUsedBufferLength, true, _headerSent, _messageType, ExtensionFlags, cancellationToken);
                _webSocket.Connection.EndWritting();
            }
        }
        public override void Close()
        {
            if (Interlocked.CompareExchange(ref _finished, 1, 0) == 0)
            {
                _webSocket.Connection.WriteInternal(_webSocket.Connection.SendBuffer, _internalUsedBufferLength, true, _headerSent, _messageType, ExtensionFlags);
                _webSocket.Connection.EndWritting();
            }
        }
    }
}
