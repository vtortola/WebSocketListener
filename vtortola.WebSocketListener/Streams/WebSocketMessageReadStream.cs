using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public class WebSocketMessageReadStream : WebSocketMessageStream
    {
        public override bool CanRead { get { return true; } }
        public WebSocketMessageReadStream(WebSocketClient client)
            : base(client)
        { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Int32 readed = 0;
            if (_client.Header == null || _client.Header.RemainingBytes != 0)
            {
                readed = _client.ReadInternal(buffer, offset, count);

                if (readed == 0)
                    return 0;

                this.MessageType = (WebSocketMessageType)_client.Header.Flags.Option;
            }
            if (readed == 0 && _client.Header.RemainingBytes == 0)
                _client.CleanHeader();

            return readed;
        }
    }

}
