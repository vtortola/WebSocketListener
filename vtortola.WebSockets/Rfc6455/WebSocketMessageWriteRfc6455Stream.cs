using System;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Rfc6455
{
    internal sealed class WebSocketMessageWriteRfc6455Stream : WebSocketMessageWriteStream
    {
        bool _isHeaderSent, _isFinished;
        int _internalUsedBufferLength;

        readonly WebSocketConnectionRfc6455 _webSocket;
        readonly WebSocketMessageType _messageType;

        public WebSocketMessageWriteRfc6455Stream(WebSocketConnectionRfc6455 webSocket, WebSocketMessageType messageType)
        {
            Guard.ParameterCannotBeNull(webSocket, nameof(webSocket));

            _internalUsedBufferLength = 0;
            _messageType = messageType;
            _webSocket = webSocket;
        }

        public WebSocketMessageWriteRfc6455Stream(WebSocketConnectionRfc6455 client, WebSocketMessageType messageType, WebSocketExtensionFlags extensionFlags)
            :this(client,messageType)
        {
            ExtensionFlags.Rsv1 = extensionFlags.Rsv1;
            ExtensionFlags.Rsv2 = extensionFlags.Rsv2;
            ExtensionFlags.Rsv3 = extensionFlags.Rsv3;
        }

        private void BufferData(byte[] buffer, ref int offset, ref int count)
        {
            var read = Math.Min(count, _webSocket.SendBuffer.Count - _internalUsedBufferLength);
            if (read == 0)
                return;

            Array.Copy(buffer, offset, _webSocket.SendBuffer.Array, _webSocket.SendBuffer.Offset + _internalUsedBufferLength, read);
            _internalUsedBufferLength += read;
            offset += read;
            count -= read;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_isFinished)
                throw new WebSocketException("The write stream has been already flushed or disposed.");

            RemoveUTF8BOM(buffer, ref offset, ref count);

            while (count > 0)
            {
                BufferData(buffer, ref offset, ref count);

                if (_internalUsedBufferLength == _webSocket.SendBuffer.Count && count > 0)
                {
                    _webSocket.WriteInternal(_webSocket.SendBuffer, _internalUsedBufferLength, false, _isHeaderSent, _messageType, ExtensionFlags);
                    _internalUsedBufferLength = 0;
                    _isHeaderSent = true;
                }
            }
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancel)
        {
            if (_isFinished)
                throw new WebSocketException("The write stream has been already flushed or disposed.");

            RemoveUTF8BOM(buffer, ref offset, ref count);

            while (count > 0)
            {
                BufferData(buffer, ref offset, ref count);

                if (_internalUsedBufferLength == _webSocket.SendBuffer.Count && count > 0)
                {
                    await _webSocket.WriteInternalAsync(_webSocket.SendBuffer, _internalUsedBufferLength, false, _isHeaderSent, _messageType, ExtensionFlags, cancel).ConfigureAwait(false);
                    _internalUsedBufferLength = 0;
                    _isHeaderSent = true;                
                }
            }
        }

        public override async Task FlushAsync(CancellationToken cancel)
        {
            await _webSocket.WriteInternalAsync(_webSocket.SendBuffer, _internalUsedBufferLength, false, _isHeaderSent, _messageType, ExtensionFlags, cancel).ConfigureAwait(false);
            _internalUsedBufferLength = 0;
            _isHeaderSent = true;
        }

        public override void Close()
        {
            if (!_isFinished)
            {
                _isFinished = true;
                _webSocket.WriteInternal(_webSocket.SendBuffer, _internalUsedBufferLength, true, _isHeaderSent, _messageType, ExtensionFlags);
                _webSocket.EndWritting();
            }
        }

        public override async Task CloseAsync(CancellationToken cancel)
        {
            if (!_isFinished)
            {
                _isFinished = true;
                await _webSocket.WriteInternalAsync(_webSocket.SendBuffer, _internalUsedBufferLength, true, _isHeaderSent, _messageType, ExtensionFlags, cancel).ConfigureAwait(false);
                _webSocket.EndWritting();
            }
        }
    }
}
