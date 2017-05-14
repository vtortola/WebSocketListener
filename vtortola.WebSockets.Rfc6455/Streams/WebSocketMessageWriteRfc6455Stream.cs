using System;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Rfc6455
{
    public class WebSocketMessageWriteRfc6455Stream : WebSocketMessageWriteStream
    {
        private bool _isHeaderSent, _isFinished;
        private int _internalUsedBufferLength;

        private readonly WebSocketRfc6455 _webSocket;
        private readonly WebSocketMessageType _messageType;

        public WebSocketMessageWriteRfc6455Stream(WebSocketRfc6455 webSocket, WebSocketMessageType messageType)
        {
            if (webSocket == null) throw new ArgumentNullException(nameof(webSocket));

            _internalUsedBufferLength = 0;
            _messageType = messageType;
            _webSocket = webSocket;
        }
        public WebSocketMessageWriteRfc6455Stream(WebSocketRfc6455 client, WebSocketMessageType messageType, WebSocketExtensionFlags extensionFlags)
            : this(client, messageType)
        {
            ExtensionFlags.Rsv1 = extensionFlags.Rsv1;
            ExtensionFlags.Rsv2 = extensionFlags.Rsv2;
            ExtensionFlags.Rsv3 = extensionFlags.Rsv3;
        }

        private void BufferData(byte[] buffer, ref int offset, ref int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));

            var read = Math.Min(count, _webSocket.Connection.SendBuffer.Count - _internalUsedBufferLength);
            if (read == 0)
                return;
            Array.Copy(buffer, offset, _webSocket.Connection.SendBuffer.Array, _webSocket.Connection.SendBuffer.Offset + _internalUsedBufferLength, read);
            _internalUsedBufferLength += read;
            offset += read;
            count -= read;
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));

            if (_isFinished)
                throw new WebSocketException("The write stream has been already flushed or disposed.");

            while (count > 0)
            {
                BufferData(buffer, ref offset, ref count);

                if (_internalUsedBufferLength == _webSocket.Connection.SendBuffer.Count && count > 0)
                {
                    var dataFrame = _webSocket.Connection.PrepareFrame(_webSocket.Connection.SendBuffer, _internalUsedBufferLength, false, _isHeaderSent, _messageType, ExtensionFlags);
                    _webSocket.Connection.SendFrame(dataFrame);
                    _internalUsedBufferLength = 0;
                    _isHeaderSent = true;
                }
            }
        }
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));

            if (_isFinished)
                throw new WebSocketException("The write stream has been already flushed or disposed.");

            while (count > 0)
            {
                BufferData(buffer, ref offset, ref count);

                if (_internalUsedBufferLength == _webSocket.Connection.SendBuffer.Count && count > 0)
                {
                    var dataFrame = _webSocket.Connection.PrepareFrame(_webSocket.Connection.SendBuffer, _internalUsedBufferLength, false, _isHeaderSent, _messageType, ExtensionFlags);
                    await _webSocket.Connection.SendFrameAsync(dataFrame, cancellationToken).ConfigureAwait(false);
                    _internalUsedBufferLength = 0;
                    _isHeaderSent = true;
                }
            }
        }
        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            var dataFrame = _webSocket.Connection.PrepareFrame(_webSocket.Connection.SendBuffer, _internalUsedBufferLength, false, _isHeaderSent, _messageType, ExtensionFlags);
            await this._webSocket.Connection.SendFrameAsync(dataFrame, cancellationToken).ConfigureAwait(false);
            _internalUsedBufferLength = 0;
            _isHeaderSent = true;
        }
#if (NET45 || NET451 || NET452 || NET46 || DNX451 || DNX452 || DNX46)
        public override void Close()
#else
        // NETSTANDARD, UAP10_0 and DOTNET5_4 don't support Close, so just override Dispose(bool disposing).
        protected override void Dispose(bool disposing)
#endif
        {
            if (!_isFinished)
            {
                _isFinished = true;
                var dataFrame = _webSocket.Connection.PrepareFrame(_webSocket.Connection.SendBuffer, _internalUsedBufferLength, true, _isHeaderSent, _messageType, ExtensionFlags);
                _webSocket.Connection.SendFrame(dataFrame);
                _webSocket.Connection.EndWriting();
            }
        }

        public override async Task CloseAsync(CancellationToken cancellation)
        {
            if (!_isFinished)
            {
                _isFinished = true;
                var dataFrame = _webSocket.Connection.PrepareFrame(_webSocket.Connection.SendBuffer, _internalUsedBufferLength, true, _isHeaderSent, _messageType, ExtensionFlags);
                await _webSocket.Connection.SendFrameAsync(dataFrame, cancellation).ConfigureAwait(false);
                _webSocket.Connection.EndWriting();
            }
        }
    }
}
