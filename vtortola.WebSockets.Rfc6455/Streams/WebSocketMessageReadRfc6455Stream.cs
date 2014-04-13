using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Rfc6455
{
    public sealed class WebSocketMessageReadRfc6455Stream : WebSocketMessageReadStream
    {
        readonly WebSocketHandlerRfc6455 _handler;
        Boolean _hasPendingFrames;
        readonly WebSocketMessageType _messageType;
        readonly WebSocketExtensionFlags _flags;
        public override WebSocketMessageType MessageType { get { return _messageType; } }
        public override WebSocketExtensionFlags Flags { get { return _flags; } }

        public WebSocketMessageReadRfc6455Stream(WebSocketHandlerRfc6455 handler)
        {
            if(handler == null)
                throw new ArgumentNullException("client");

            _handler = handler;
            _messageType = (WebSocketMessageType)_handler.CurrentHeader.Flags.Option;
            _flags = GetExtensionFlags(_handler.CurrentHeader.Flags);
            _hasPendingFrames = !_handler.CurrentHeader.Flags.FIN;
            if (_handler.CurrentHeader.Flags.Option != WebSocketFrameOption.Binary && _handler.CurrentHeader.Flags.Option != WebSocketFrameOption.Text)
                throw new WebSocketException("WebSocketMessageReadNetworkStream can only start with a Text or Binary frame, not " + _handler.CurrentHeader.Flags.Option.ToString());
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
            if (count < buffer.Length - offset)
                throw new ArgumentException("There is not space in the array for that length considering that offset.");

            if (_handler.CurrentHeader == null)
                return 0;

            if (_handler.CurrentHeader.ContentLength < (UInt64)count)
                count = (Int32)_handler.CurrentHeader.ContentLength;

            if (_handler.CurrentHeader.RemainingBytes < (UInt64)count)
                count = (Int32)_handler.CurrentHeader.RemainingBytes;

            return count;
        }

        public override Int32 Read(Byte[] buffer, Int32 offset, Int32 count)
        {
            Int32 readed = 0;
            do
            {
                if (!_handler.IsConnected)
                    return 0;

                var checkedcount = CheckBoundaries(buffer, offset, count);

                if (checkedcount == 0 && !_hasPendingFrames)
                    return 0;
                else if (checkedcount == 0 && _hasPendingFrames)
                    LoadNewHeader();
                else
                {
                    readed = _handler.ReadInternal(buffer, offset, checkedcount);
                    if (_handler.CurrentHeader == null)
                        LoadNewHeader();
                }
            } while (readed == 0 && _handler.CurrentHeader.RemainingBytes != 0);

            return readed;
        }

        public override async Task<Int32> ReadAsync(Byte[] buffer, Int32 offset, Int32 count, CancellationToken cancellationToken)
        {
            Int32 readed = 0;
            do
            {
                if (!_handler.IsConnected || cancellationToken.IsCancellationRequested)
                    return 0;

                var checkedcount = CheckBoundaries(buffer, offset, count);

                if(checkedcount == 0 && !_hasPendingFrames)
                    return 0;
                else if (checkedcount == 0 && _hasPendingFrames)
                    await LoadNewHeaderAsync(cancellationToken);
                else
                {
                    readed = await _handler.ReadInternalAsync(buffer, offset, checkedcount, cancellationToken);
                    if (_handler.CurrentHeader == null)
                        await LoadNewHeaderAsync(cancellationToken);
                }
            } while (readed ==0 && _handler.CurrentHeader.RemainingBytes != 0);

            return readed;
        }

        private void LoadNewHeader()
        {
            if (_hasPendingFrames)
            {
                _handler.AwaitHeader();
                _hasPendingFrames = _handler.CurrentHeader != null && !_handler.CurrentHeader.Flags.FIN && _handler.CurrentHeader.Flags.Option == WebSocketFrameOption.Continuation;
            }
        }
        private async Task LoadNewHeaderAsync(CancellationToken cancellationToken)
        {
            if (_hasPendingFrames)
            {
                await _handler.AwaitHeaderAsync(cancellationToken);
                _hasPendingFrames = _handler.CurrentHeader != null && !_handler.CurrentHeader.Flags.FIN && _handler.CurrentHeader.Flags.Option == WebSocketFrameOption.Continuation;
            }
        }
    }

}
