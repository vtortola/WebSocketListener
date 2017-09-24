using System;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Rfc6455
{
    internal sealed class WebSocketMessageReadRfc6455Stream : WebSocketMessageReadStream
    {
        readonly WebSocketConnectionRfc6455 _webSocket;
        readonly WebSocketMessageType _messageType;
        readonly WebSocketExtensionFlags _flags;

        bool _hasPendingFrames;

        public override WebSocketMessageType MessageType { get { return _messageType; } }
        public override WebSocketExtensionFlags Flags { get { return _flags; } }

        public WebSocketMessageReadRfc6455Stream(WebSocketConnectionRfc6455 webSocket)
        {
            Guard.ParameterCannotBeNull(webSocket, nameof(webSocket));

            _webSocket = webSocket;
            _messageType = (WebSocketMessageType)_webSocket.CurrentHeader.Flags.Option;
            _flags = GetExtensionFlags(_webSocket.CurrentHeader.Flags);
            _hasPendingFrames = !_webSocket.CurrentHeader.Flags.FIN;
            if (_webSocket.CurrentHeader.Flags.Option != WebSocketFrameOption.Binary && _webSocket.CurrentHeader.Flags.Option != WebSocketFrameOption.Text)
                throw new WebSocketException("WebSocketMessageReadNetworkStream can only start with a Text or Binary frame, not " + _webSocket.CurrentHeader.Flags.Option.ToString());
        }

        private WebSocketExtensionFlags GetExtensionFlags(WebSocketFrameHeaderFlags webSocketFrameHeaderFlags)
        {
            var flags = new WebSocketExtensionFlags();
            flags.Rsv1 = webSocketFrameHeaderFlags.RSV1;
            flags.Rsv2 = webSocketFrameHeaderFlags.RSV2;
            flags.Rsv3 = webSocketFrameHeaderFlags.RSV3;
            return flags;
        }

        private int CheckBoundaries(byte[] buffer, int offset, int count)
        {
            if (count > buffer.Length - offset)
                throw new ArgumentException("There is not space in the array for that length considering that offset.");

            if (_webSocket.CurrentHeader == null)
                return 0;

            if (_webSocket.CurrentHeader.ContentLength < count)
                count = (int)_webSocket.CurrentHeader.ContentLength;

            if (_webSocket.CurrentHeader.RemainingBytes < count)
                count = (int)_webSocket.CurrentHeader.RemainingBytes;

            return count;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var readed = 0;
            do
            {
                if (!_webSocket.IsConnected)
                    break;

                var checkedcount = CheckBoundaries(buffer, offset, count);

                if (checkedcount == 0 && !_hasPendingFrames)
                {
                    _webSocket.DisposeCurrentHeaderIfFinished();
                    break;
                }
                else if (checkedcount == 0 && _hasPendingFrames)
                {
                    LoadNewHeader();
                }
                else
                {
                    readed = _webSocket.ReadInternal(buffer, offset, checkedcount);
                    _webSocket.DisposeCurrentHeaderIfFinished();
                    if (_webSocket.CurrentHeader == null && _hasPendingFrames)
                        LoadNewHeader();
                }
            } while (readed == 0 && _webSocket.CurrentHeader.RemainingBytes != 0);

            return readed;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancel)
        {
            Int32 readed = 0;
            do
            {
                if (!_webSocket.IsConnected || cancel.IsCancellationRequested)
                    break;

                var checkedcount = CheckBoundaries(buffer, offset, count);

                if (checkedcount == 0 && !_hasPendingFrames)
                {
                    _webSocket.DisposeCurrentHeaderIfFinished();
                    break;
                }
                else if (checkedcount == 0 && _hasPendingFrames)
                {
                    await LoadNewHeaderAsync(cancel).ConfigureAwait(false);
                }
                else
                {
                    readed = await _webSocket.ReadInternalAsync(buffer, offset, checkedcount, cancel).ConfigureAwait(false);
                    _webSocket.DisposeCurrentHeaderIfFinished();
                    if (_webSocket.CurrentHeader == null && _hasPendingFrames)
                        await LoadNewHeaderAsync(cancel).ConfigureAwait(false);
                }
            } while (readed == 0 && _webSocket.CurrentHeader.RemainingBytes != 0);

            return readed;
        }

        private void LoadNewHeader()
        {
            _webSocket.AwaitHeader();
            _hasPendingFrames = HasPendingFrames();
        }

        private async Task LoadNewHeaderAsync(CancellationToken cancel)
        {
            await _webSocket.AwaitHeaderAsync(cancel).ConfigureAwait(false);
            _hasPendingFrames = HasPendingFrames();
        }

        private bool HasPendingFrames()
            => _webSocket.CurrentHeader != null && !_webSocket.CurrentHeader.Flags.FIN && _webSocket.CurrentHeader.Flags.Option == WebSocketFrameOption.Continuation;

    }
}
