using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public class WebSocketMessageReadStream : WebSocketMessageStream
    {
        public override bool CanRead { get { return true; } }
        public WebSocketMessageReadStream(WebSocketClient client)
            : base(client)
        { }

        Boolean _headerAwaited;
        public async Task AwaitHeaderAsync()
        {
            _headerAwaited = true;
            await _client.AwaitHeaderAsync();
            if(_client.Header != null)
                this.MessageType = (WebSocketMessageType)_client.Header.Option;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
        {
            if (!_headerAwaited)
                await _client.AwaitHeaderAsync();

            Int32 readed = 0;
            if (_client.Header == null || _client.Header.RemainingBytes != 0)
            {
                readed = await _client.ReadInternalAsync(buffer, offset, count);

                if (_client.CancellationToken.IsCancellationRequested || readed == 0)
                    return 0;

                this.MessageType = (WebSocketMessageType)_client.Header.Option;
            }
            if (readed == 0 && _client.Header.RemainingBytes == 0)
                _client.CleanHeader();

            return readed;
        }
    }

}
