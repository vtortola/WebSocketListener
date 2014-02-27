using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
            _internalBuffer = new Byte[8192];
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            // http://www.rgagnon.com/javadetails/java-handle-utf8-file-with-bom.html
            if (buffer.Length >= 3 &&
                buffer[0] == 239 && buffer[1] == 187 && buffer[2] == 191)
            {
                count -= 3;
                
                if(count<=0)
                    return;

                offset += 3;
            }

            if (_internalBufferLength != 0)
            {
                _client.WriteInternal(_internalBuffer, 0, _internalBufferLength, false, _headerSent, (WebSocketFrameOption)MessageType);
                _headerSent = true;
            }

            _internalBufferLength = Math.Min(count, _internalBuffer.Length);
            count -= _internalBufferLength;
            Array.Copy(buffer, offset + count, _internalBuffer, 0, _internalBufferLength);

            if (count != 0)
            {
                _client.WriteInternal(buffer, offset, count, false, _headerSent, (WebSocketFrameOption)MessageType);
                _headerSent = true;
            }
        }

        public override void Close()
        {
            if (!_finished)
            {
                _finished = true;
                _client.WriteInternal(_internalBuffer, 0, _internalBufferLength, true, _headerSent, (WebSocketFrameOption)MessageType);
            }
            base.Close();
        }
    }
}
