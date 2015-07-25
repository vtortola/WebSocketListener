using System;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Rfc6455
{
    public class WebSocketMessageReadRfc6455Stream : WebSocketMessageReadStream
    {
        readonly WebSocketRfc6455 _webSocket;
        Boolean _hasPendingFrames;
        readonly WebSocketMessageType _messageType;
        readonly WebSocketExtensionFlags _flags;
        public override WebSocketMessageType MessageType { get { return _messageType; } }
        public override WebSocketExtensionFlags Flags { get { return _flags; } }

        public WebSocketMessageReadRfc6455Stream(WebSocketRfc6455 webSocket)
        {
            if(webSocket == null)
                throw new ArgumentNullException("webSocket");

            _webSocket = webSocket;
            _messageType = (WebSocketMessageType)_webSocket.Connection.CurrentHeader.Flags.Option;
            _flags = GetExtensionFlags(_webSocket.Connection.CurrentHeader.Flags);
            _hasPendingFrames = !_webSocket.Connection.CurrentHeader.Flags.FIN;
            if (_webSocket.Connection.CurrentHeader.Flags.Option != WebSocketFrameOption.Binary && _webSocket.Connection.CurrentHeader.Flags.Option != WebSocketFrameOption.Text)
                throw new WebSocketException("WebSocketMessageReadNetworkStream can only start with a Text or Binary frame, not " + _webSocket.Connection.CurrentHeader.Flags.Option.ToString());
        }

        private WebSocketExtensionFlags GetExtensionFlags(WebSocketFrameHeaderFlags webSocketFrameHeaderFlags)
        {
            var flags = new WebSocketExtensionFlags();
            flags.Rsv1 = webSocketFrameHeaderFlags.RSV1;
            flags.Rsv2 = webSocketFrameHeaderFlags.RSV2;
            flags.Rsv3 = webSocketFrameHeaderFlags.RSV3;
            return flags;
        }

        private Int32 CheckBoundaries(Byte[] buffer, Int32 offset, Int32 count)
        {
            if (count > buffer.Length - offset)
                throw new ArgumentException("There is not space in the array for that length considering that offset.");

            if (_webSocket.Connection.CurrentHeader == null)
                return 0;

            if (_webSocket.Connection.CurrentHeader.ContentLength < (Int64)count)
                count = (Int32)_webSocket.Connection.CurrentHeader.ContentLength;

            if (_webSocket.Connection.CurrentHeader.RemainingBytes < (Int64)count)
                count = (Int32)_webSocket.Connection.CurrentHeader.RemainingBytes;

            return count;
        }

        public override Int32 Read(Byte[] buffer, Int32 offset, Int32 count)
        {
            Int32 readed = 0;
            do
            {
                if (!_webSocket.IsConnected)
                    break;

                var checkedcount = CheckBoundaries(buffer, offset, count);

                if (checkedcount == 0 && !_hasPendingFrames)
                {
                    _webSocket.Connection.DisposeCurrentHeaderIfFinished();
                    break;
                }
                else if (checkedcount == 0 && _hasPendingFrames)
                    LoadNewHeader();
                else
                {
                    readed = _webSocket.Connection.ReadInternal(buffer, offset, checkedcount);
                    _webSocket.Connection.DisposeCurrentHeaderIfFinished();
                    if (_webSocket.Connection.CurrentHeader == null && _hasPendingFrames)
                        LoadNewHeader();
                }
            } while (readed == 0 && _webSocket.Connection.CurrentHeader.RemainingBytes != 0);

            return readed;
        }

        public override async Task<Int32> ReadAsync(Byte[] buffer, Int32 offset, Int32 count, CancellationToken cancellationToken)
        {
            Int32 readed = 0;
            do
            {
                if (!_webSocket.IsConnected || cancellationToken.IsCancellationRequested)
                    break;

                var checkedcount = CheckBoundaries(buffer, offset, count);

                if (checkedcount == 0 && !_hasPendingFrames)
                {
                    _webSocket.Connection.DisposeCurrentHeaderIfFinished();
                    break;
                }
                else if (checkedcount == 0 && _hasPendingFrames)
                    await LoadNewHeaderAsync(cancellationToken).ConfigureAwait(false);
                else
                {
                    readed = await _webSocket.Connection.ReadInternalAsync(buffer, offset, checkedcount, cancellationToken).ConfigureAwait(false);
                    _webSocket.Connection.DisposeCurrentHeaderIfFinished();
                    if (_webSocket.Connection.CurrentHeader == null && _hasPendingFrames)
                        await LoadNewHeaderAsync(cancellationToken).ConfigureAwait(false);
                }
            } while (readed == 0 && _webSocket.Connection.CurrentHeader.RemainingBytes != 0);

            return readed;
        }

        private void LoadNewHeader()
        {
            _webSocket.Connection.AwaitHeader();
            _hasPendingFrames = _webSocket.Connection.CurrentHeader != null && !_webSocket.Connection.CurrentHeader.Flags.FIN && _webSocket.Connection.CurrentHeader.Flags.Option == WebSocketFrameOption.Continuation;
        }
        private async Task LoadNewHeaderAsync(CancellationToken cancellationToken)
        {
            await _webSocket.Connection.AwaitHeaderAsync(cancellationToken).ConfigureAwait(false);
            _hasPendingFrames = _webSocket.Connection.CurrentHeader != null && !_webSocket.Connection.CurrentHeader.Flags.FIN && _webSocket.Connection.CurrentHeader.Flags.Option == WebSocketFrameOption.Continuation;
        }
    }

}
