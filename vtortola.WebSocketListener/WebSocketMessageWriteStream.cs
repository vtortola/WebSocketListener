using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public class WebSocketMessageWriteStream : WebSocketMessageStream
    {
        public override bool CanWrite { get { return true; } }

        volatile Boolean _headerSent = false;
        volatile Boolean _finished = false;
        readonly Byte[] _internalBuffer;
        Int32 _internalBufferLength;
        public WebSocketMessageWriteStream(WebSocketClient client, WebSocketMessageType messageType)
            : base(client)
        {
            MessageType = messageType;
            _internalBufferLength = 0;
            _internalBuffer = new Byte[64];
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this.WriteAsync(buffer, offset, count).Wait();
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
        {
            // http://www.rgagnon.com/javadetails/java-handle-utf8-file-with-bom.html
            if (count == 3 && buffer.Length >= 3 &&
                buffer[0] == 239 && buffer[1] == 187 && buffer[2] == 191)
            {
                return;
            }

            if (_internalBufferLength != 0)
            {
                await _client.WriteInternalAsync(_internalBuffer, 0, _internalBufferLength, false, _headerSent, (WebSocketFrameOption)MessageType);
            }

            _internalBufferLength = Math.Min(count, _internalBuffer.Length);
            count -= _internalBufferLength;
            Array.Copy(buffer, offset + count, _internalBuffer, 0, _internalBufferLength);

            if (count != 0)
            {
                await _client.WriteInternalAsync(buffer, offset, count, false, _headerSent, (WebSocketFrameOption)MessageType);
                _headerSent = true;
            }
        }

        public override void Close()
        {
            if (!_finished)
            {
                _finished = true;
                _client.WriteInternalAsync(_internalBuffer, 0, _internalBufferLength, true, _headerSent, (WebSocketFrameOption)MessageType).Wait();
            }
            base.Close();
        }

    }

}
