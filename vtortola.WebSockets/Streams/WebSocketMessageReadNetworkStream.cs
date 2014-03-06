using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public sealed class WebSocketMessageReadNetworkStream : WebSocketMessageReadStream
    {
        readonly WebSocketClient _client;
        readonly WebSocketFrameHeader _header;
        Boolean _hasPendingFrames;
        public override WebSocketMessageType MessageType 
        { 
            get { return (WebSocketMessageType)_header.Flags.Option; } 
        }
        public override WebSocketFrameHeaderFlags Flags
        {
            get { return _header.Flags; }
        }

        public WebSocketMessageReadNetworkStream(WebSocketClient client, WebSocketFrameHeader header)
        {
            if(client == null)
                throw new ArgumentNullException("client");
            if(header == null)
                throw new ArgumentNullException("header");

            _client = client;
            _header = header;
            _hasPendingFrames = !_header.Flags.FIN;
            if (header.Flags.Option != WebSocketFrameOption.Binary && header.Flags.Option != WebSocketFrameOption.Text)
                throw new WebSocketException("WebSocketMessageReadNetworkStream can only start with a Text or Binary frame, not " + header.Flags.Option.ToString());
        }

        private Int32 CheckBoundaries(Byte[] buffer, Int32 offset, Int32 count)
        {
            if (count < buffer.Length - offset)
                throw new ArgumentException("There is not space in the array for that length considering that offset.");

            if (_client.Header == null)
                return 0;

            if (_client.Header.ContentLength < (UInt64)count)
                count = (Int32)_client.Header.ContentLength;

            if (_client.Header.RemainingBytes < (UInt64)count)
                count = (Int32)_client.Header.RemainingBytes;

            return count;
        }

        public override Int32 Read(Byte[] buffer, Int32 offset, Int32 count)
        {
            Int32 readed = 0;
            do
            {
                if (!_client.IsConnected)
                    return 0;

                var checkedcount = CheckBoundaries(buffer, offset, count);

                if (checkedcount == 0 && !_hasPendingFrames)
                    return 0;
                else if (checkedcount == 0 && _hasPendingFrames)
                    LoadNewHeader();
                else
                {
                    readed = _client.ReadInternal(buffer, offset, checkedcount);
                    if (_client.Header.RemainingBytes == 0)
                        LoadNewHeader();
                }
            } while (readed == 0 && _client.Header.RemainingBytes != 0);

            return readed;
        }

        public override async Task<Int32> ReadAsync(Byte[] buffer, Int32 offset, Int32 count, CancellationToken cancellationToken)
        {
            Int32 readed = 0;
            do
            {
                if (!_client.IsConnected || cancellationToken.IsCancellationRequested)
                    return 0;

                var checkedcount = CheckBoundaries(buffer, offset, count);

                if(checkedcount == 0 && !_hasPendingFrames)
                    return 0;
                else if (checkedcount == 0 && _hasPendingFrames)
                    await LoadNewHeaderAsync(cancellationToken);
                else
                {
                    readed = await _client.ReadInternalAsync(buffer, offset, checkedcount, cancellationToken);
                    if (_client.Header.RemainingBytes == 0)
                        await LoadNewHeaderAsync(cancellationToken);
                }
            } while (readed ==0 && _client.Header.RemainingBytes != 0);

            return readed;
        }

        private void LoadNewHeader()
        {
            _client.CleanHeader();
            if (_hasPendingFrames)
            {
                _client.AwaitHeader();
                _hasPendingFrames = _client.Header != null && !_client.Header.Flags.FIN && _client.Header.Flags.Option == WebSocketFrameOption.Continuation;
            }
        }
        private async Task LoadNewHeaderAsync(CancellationToken cancellationToken)
        {
            _client.CleanHeader();
            if (_hasPendingFrames)
            {
                await _client.AwaitHeaderAsync(cancellationToken);
                _hasPendingFrames = _client.Header != null && !_client.Header.Flags.FIN && _client.Header.Flags.Option == WebSocketFrameOption.Continuation;
            }
        }
    }

}
