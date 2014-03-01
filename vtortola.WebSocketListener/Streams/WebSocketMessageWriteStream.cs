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
        public override sealed Boolean CanWrite { get { return true; } }

        Boolean _headerSent = false;
        Boolean _finished = false;
        readonly Byte[] _internalBuffer;
        Int32 _internalUsedBufferLength;
        readonly WebSocketExtensionFlags _extensionFlags;

        public WebSocketMessageWriteStream(WebSocketClient client, WebSocketMessageType messageType)
            : base(client)
        {
            MessageType = messageType;
            _internalUsedBufferLength = 0;
            _internalBuffer = new Byte[8192];
            _extensionFlags = WebSocketExtensionFlags.None;
        }

        public WebSocketMessageWriteStream(WebSocketClient client, WebSocketMessageType messageType, WebSocketExtensionFlags extensionFlags)
            :this(client,messageType)
        {
            _extensionFlags = extensionFlags;
        }

        private void RemoveUTF8BOM(Byte[] buffer, ref Int32 offset, ref Int32 count)
        {
            // http://www.rgagnon.com/javadetails/java-handle-utf8-file-with-bom.html
            if (buffer.Length >= 3 &&
                buffer[0] == 239 && buffer[1] == 187 && buffer[2] == 191)
            {
                count -= 3;

                if (count <= 0)
                    return;

                offset += 3;
            }
        }

        public override sealed void Write(Byte[] buffer, Int32 offset, Int32 count)
        {
            RemoveUTF8BOM(buffer, ref offset, ref count);
            if (count == 0)
                return;

            if (_internalUsedBufferLength != 0)
            {
                if (_internalUsedBufferLength == _internalBuffer.Length)
                {
                    _client.WriteInternal(_internalBuffer, 0, _internalUsedBufferLength, false, _headerSent, (WebSocketFrameOption)MessageType, _extensionFlags);
                    _internalUsedBufferLength = 0;
                    _headerSent = true;
                }
                else if (_internalUsedBufferLength < _internalBuffer.Length)
                {
                    var read = Math.Min(count, _internalBuffer.Length - _internalUsedBufferLength);
                    Array.Copy(buffer, offset, _internalBuffer, _internalUsedBufferLength, read);
                    _internalUsedBufferLength += read;
                    offset += read;
                    count -= read;
                }
            }

            if (count == 0)
                return;

            _internalUsedBufferLength = Math.Min(count, _internalBuffer.Length);
            count -= _internalUsedBufferLength;
            Array.Copy(buffer, offset + count, _internalBuffer, 0, _internalUsedBufferLength);
            offset += _internalUsedBufferLength;

            if (count != 0)
            {
                _client.WriteInternal(buffer, offset, count, false, _headerSent, (WebSocketFrameOption)MessageType, _extensionFlags);
                _headerSent = true;
            }
        }

        public override sealed async Task WriteAsync(Byte[] buffer, Int32 offset, Int32 count, CancellationToken cancellationToken)
        {
            RemoveUTF8BOM(buffer, ref offset, ref count);
            if (count == 0)
                return;

            if (_internalUsedBufferLength != 0)
            {
                if(_internalUsedBufferLength == _internalBuffer.Length)
                {
                    await _client.WriteInternalAsync(_internalBuffer, 0, _internalUsedBufferLength, false, _headerSent, (WebSocketFrameOption)MessageType,_extensionFlags, cancellationToken);
                    _internalUsedBufferLength = 0;
                    _headerSent = true;
                }
                else if (_internalUsedBufferLength < _internalBuffer.Length)
                {
                    var read = Math.Min(count, _internalBuffer.Length - _internalUsedBufferLength);
                    Array.Copy(buffer, offset, _internalBuffer, _internalUsedBufferLength, read);
                    _internalUsedBufferLength += read;
                    offset += read;
                    count -= read;
                }
            }

            if (count == 0)
                return;

            _internalUsedBufferLength = Math.Min(count, _internalBuffer.Length);
            count -= _internalUsedBufferLength;
            Array.Copy(buffer, offset + count, _internalBuffer, 0, _internalUsedBufferLength);
            offset += _internalUsedBufferLength;

            if (count != 0)
            {
                await _client.WriteInternalAsync(buffer, offset, count, false, _headerSent, (WebSocketFrameOption)MessageType,_extensionFlags, cancellationToken);
                _headerSent = true;
            }
        }

        public override sealed void Close()
        {
            if (!_finished)
            {
                _finished = true;
                _client.WriteInternal(_internalBuffer, 0, _internalUsedBufferLength, true, _headerSent, (WebSocketFrameOption)MessageType, _extensionFlags);
                base.Close();
            }
        }
    }
}
