using System;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Rfc6455
{
    public class WebSocketMessageWriteRfc6455Stream : WebSocketMessageWriteStream
    {
        Boolean _isHeaderSent, _isFinished;
        Int32 _internalUsedBufferLength;

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
            if (_isFinished)
                throw new WebSocketException("The write stream has been already flushed or disposed.");

            RemoveUTF8BOM(buffer, ref offset, ref count);

            while (count > 0)
            {
                BufferData(buffer, ref offset, ref count);

                if (_internalUsedBufferLength == _webSocket.Connection.SendBuffer.Count && count > 0)
                {
                    _webSocket.Connection.WriteInternal(_webSocket.Connection.SendBuffer, _internalUsedBufferLength, false, _isHeaderSent, _messageType, ExtensionFlags);
                    _internalUsedBufferLength = 0;
                    _isHeaderSent = true;
                }
            }
        }
        public override async Task WriteAsync(Byte[] buffer, Int32 offset, Int32 count, CancellationToken cancellationToken)
        {
            if (_isFinished)
                throw new WebSocketException("The write stream has been already flushed or disposed.");

            RemoveUTF8BOM(buffer, ref offset, ref count);

            while (count > 0)
            {
                BufferData(buffer, ref offset, ref count);

                if (_internalUsedBufferLength == _webSocket.Connection.SendBuffer.Count && count > 0)
                {
                    await _webSocket.Connection.WriteInternalAsync(_webSocket.Connection.SendBuffer, _internalUsedBufferLength, false, _isHeaderSent, _messageType, ExtensionFlags, cancellationToken).ConfigureAwait(false);
                    _internalUsedBufferLength = 0;
                    _isHeaderSent = true;                
                }
            }
        }
        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            await _webSocket.Connection.WriteInternalAsync(_webSocket.Connection.SendBuffer, _internalUsedBufferLength, false, _isHeaderSent, _messageType, ExtensionFlags, cancellationToken).ConfigureAwait(false);
            _internalUsedBufferLength = 0;
            _isHeaderSent = true;
        }
        public override void Close()
        {
            if (!_isFinished)
            {
                _isFinished = true;
                _webSocket.Connection.WriteInternal(_webSocket.Connection.SendBuffer, _internalUsedBufferLength, true, _isHeaderSent, _messageType, ExtensionFlags);
                _webSocket.Connection.EndWritting();
            }
        }

        public override async Task CloseAsync(CancellationToken cancellation)
        {
            if (!_isFinished)
            {
                _isFinished = true;
                await _webSocket.Connection.WriteInternalAsync(_webSocket.Connection.SendBuffer, _internalUsedBufferLength, true, _isHeaderSent, _messageType, ExtensionFlags, cancellation).ConfigureAwait(false);
                _webSocket.Connection.EndWritting();
            }
        }
    }
}
