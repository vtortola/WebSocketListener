using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public sealed class WebSocketMessageWriteNetworkStream : WebSocketMessageWriteStream
    {
        readonly WebSocket _client;

        Boolean _headerSent = false;
        Int32 _finished = 0;
        readonly Byte[] _internalBuffer;
        Int32 _internalUsedBufferLength;
        
        readonly WebSocketMessageType _messageType;

        public WebSocketMessageWriteNetworkStream(WebSocket client, WebSocketMessageType messageType)
        {
            _internalUsedBufferLength = 0;
            _internalBuffer = client.WriteBufferTail;
            _messageType = messageType;
            _client = client;
        }

        public WebSocketMessageWriteNetworkStream(WebSocket client, WebSocketMessageType messageType, WebSocketExtensionFlags extensionFlags)
            :this(client,messageType)
        {
            ExtensionFlags.Rsv1 = extensionFlags.Rsv1;
            ExtensionFlags.Rsv2 = extensionFlags.Rsv2;
            ExtensionFlags.Rsv3 = extensionFlags.Rsv3;
        }

        public override void Write(Byte[] buffer, Int32 offset, Int32 count)
        {
            if (Interlocked.CompareExchange(ref _finished, 1, 1) == 1)
                throw new WebSocketException("The write stream has been already flushed or disposed.");

            RemoveUTF8BOM(buffer, ref offset, ref count);
            if (count == 0)
                return;

            if (_internalUsedBufferLength != 0)
            {
                if (_internalUsedBufferLength < _internalBuffer.Length)
                {
                    var read = Math.Min(count, _internalBuffer.Length - _internalUsedBufferLength);
                    Array.Copy(buffer, offset, _internalBuffer, _internalUsedBufferLength, read);
                    _internalUsedBufferLength += read;
                    offset += read;
                    count -= read;
                }

                if (_internalUsedBufferLength == _internalBuffer.Length)
                {
                    _client.WriteInternal(_internalBuffer, 0, _internalUsedBufferLength, false, _headerSent, (WebSocketFrameOption)_messageType, ExtensionFlags);
                    _internalUsedBufferLength = 0;
                    _headerSent = true;
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
                _client.WriteInternal(buffer, offset, count, false, _headerSent, (WebSocketFrameOption)_messageType, ExtensionFlags);
                _headerSent = true;
            }
        }

        public override async Task WriteAsync(Byte[] buffer, Int32 offset, Int32 count, CancellationToken cancellationToken)
        {
            if (Interlocked.CompareExchange(ref _finished, 1, 1) == 1)
                throw new WebSocketException("The write stream has been already flushed or disposed.");

            RemoveUTF8BOM(buffer, ref offset, ref count);
            if (count == 0)
                return;

            if (_internalUsedBufferLength != 0)
            {
                if (_internalUsedBufferLength < _internalBuffer.Length)
                {
                    var read = Math.Min(count, _internalBuffer.Length - _internalUsedBufferLength);
                    Array.Copy(buffer, offset, _internalBuffer, _internalUsedBufferLength, read);
                    _internalUsedBufferLength += read;
                    offset += read;
                    count -= read;
                }

                if(_internalUsedBufferLength == _internalBuffer.Length)
                {
                    await _client.WriteInternalAsync(_internalBuffer, 0, _internalUsedBufferLength, false, _headerSent, (WebSocketFrameOption)_messageType, ExtensionFlags, cancellationToken);
                    _internalUsedBufferLength = 0;
                    _headerSent = true;
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
                await _client.WriteInternalAsync(buffer, offset, count, false, _headerSent, (WebSocketFrameOption)_messageType, ExtensionFlags, cancellationToken);
                _headerSent = true;
            }
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.CompareExchange(ref _finished, 1, 1) == 0)
            {
                await _client.WriteInternalAsync(_internalBuffer, 0, _internalUsedBufferLength, true, _headerSent, (WebSocketFrameOption)_messageType, ExtensionFlags, cancellationToken);
                base.Close();
            }
        }

        public override void Close()
        {
            if (Interlocked.CompareExchange(ref _finished,1,0) ==0)
            {
                _client.WriteInternal(_internalBuffer, 0, _internalUsedBufferLength, true, _headerSent, (WebSocketFrameOption)_messageType, ExtensionFlags);
                base.Close();
            }
        }
    }
}
