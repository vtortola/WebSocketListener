using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public class WebSocketClient : IDisposable
    {
        readonly TcpClient _client;
        public IPEndPoint RemoteEndpoint { get; private set; }
        public IPEndPoint LocalEndpoint { get; private set; }
        volatile Boolean _closed;
        public Boolean IsConnected { get { return !_closed &&_client.Client.Connected; } }
        public WebSocketHttpRequest HttpRequest { get; private set; }

        WebSocketFrameHeader _header;
        internal WebSocketFrameHeader Header { get { return _header; } }

        readonly TimeSpan _pingInterval;
        readonly Timer _ping;
        public WebSocketClient(TcpClient client, WebSocketHttpRequest httpRequest, TimeSpan pingInterval)
        {
            if (client == null)
                throw new ArgumentNullException("client");
            _client = client;
            RemoteEndpoint = (IPEndPoint)_client.Client.RemoteEndPoint;
            LocalEndpoint = (IPEndPoint)_client.Client.LocalEndPoint;
            HttpRequest = httpRequest;
            _pingInterval = pingInterval;
            _ping = new Timer(Ping, null, _pingInterval, _pingInterval);
        }
                
        public async Task<WebSocketMessageReadStream> ReadMessageAsync(CancellationToken token)
        {
            var message = new WebSocketMessageReadStream(this);
            await AwaitHeaderAsync(token);
            if (this.IsConnected && _header != null)
            {
                message.MessageType = (WebSocketMessageType)_header.Flags.Option;
                return message;
            }
            return null;
        }

        public WebSocketMessageWriteStream CreateMessageWriter(WebSocketMessageType messageType)
        {
            return new WebSocketMessageWriteStream(this,messageType);
        }

        readonly Byte[] _headerBuffer = new Byte[14];
        private async Task AwaitHeaderAsync(CancellationToken token)
        {
            try
            {
                Int32 readed = 0, headerLength;
                UInt64 contentLength;
                NetworkStream clientStream = _client.GetStream();

                while (this.IsConnected && _header == null)
                {
                    do
                    { // Checking for small frame
                        readed += await clientStream.ReadAsync(_headerBuffer, 0, 6, token); // 6 = 2 minimal header + 4 key
                        if (readed == 0 || token.IsCancellationRequested)
                        {
                            Close();
                            return;
                        }
                    }
                    while (readed < 6);

                    if (!WebSocketFrameHeader.TryParseLengths(_headerBuffer, 0, readed, out headerLength, out contentLength))
                    { // Checking for medium frame
                        if (!clientStream.ReadSynchronouslyUntilCount(ref readed, _headerBuffer, readed, 2, 8, token)) // 8 = 2 header + 2 size + 4 key
                        {
                            Close();
                            return;
                        }
                    }

                    if (!WebSocketFrameHeader.TryParseLengths(_headerBuffer, 0, readed, out headerLength, out contentLength))
                    { // Checking for large frame
                        if (!clientStream.ReadSynchronouslyUntilCount(ref readed, _headerBuffer, readed, 6, 14, token)) // 14 = 2 header + 8 size + 4 key
                        { 
                            Close();
                            return;
                        }
                    }

                    if (token.IsCancellationRequested)
                    {
                        Close();
                        return;
                    }

                    if (!WebSocketFrameHeader.TryParse(_headerBuffer, 0, readed, out _header))
                        throw new WebSocketException("Cannot understand header");

                    if (!_header.Flags.Option.IsData())
                    {
                        ProcessControlFrame(clientStream);
                        readed = 0;
                        _header = null;
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                Close();
            }
            catch(IOException)
            {
                Close();
            }
        }

        internal void CleanHeader()
        {
            _header = null;
        }
        
        internal Int32 ReadInternal(Byte[] buffer, Int32 bufferOffset, Int32 bufferCount)
        {
            try
            {
                if (bufferCount < buffer.Length - bufferOffset)
                    throw new ArgumentException("There is not space in the array for that length considering that offset.");

                if (_header.ContentLength < (UInt64)bufferCount)
                    bufferCount = (Int32)_header.ContentLength;

                if (_header.RemainingBytes < (UInt64)bufferCount)
                    bufferCount = (Int32)_header.RemainingBytes;

                if (!this.IsConnected)
                    return ReturnAndClose();

                var readed = _client.GetStream().Read(buffer, bufferOffset, bufferCount);

                if (_header.Flags.MASK)
                    for (int i = 0; i < readed; i++)
                        buffer[i + bufferOffset] = _header.DecodeByte(buffer[i + bufferOffset]);

                return readed;
            }
            catch (ObjectDisposedException)
            {
                return ReturnAndClose();
            }
            catch (IOException)
            {
                return ReturnAndClose();
            }
        }

        readonly Byte[] _controlFrameBuffer = new Byte[125];
        private void ProcessControlFrame(NetworkStream clientStream)
        {
            switch (_header.Flags.Option)
            {
                case WebSocketFrameOption.Continuation:
                    break;

                case WebSocketFrameOption.Text:
                case WebSocketFrameOption.Binary:
                    throw new ArgumentException("Text or Binary are not protocol frames");
                    break;

                case WebSocketFrameOption.ConnectionClose:
                    this.Close();
                    break;

                case WebSocketFrameOption.Ping:
                    break;

                case WebSocketFrameOption.Pong: // removing the pong frame from the stream, TODO: parse and control timeout
                    Int32 contentLength =  _controlFrameBuffer.Length;
                    if(_header.ContentLength < 125)
                        contentLength = (Int32)_header.ContentLength;
                    var readed = clientStream.Read(_controlFrameBuffer, 0, contentLength);

                    if(_header.Flags.MASK)
                        for (int i = 0; i < readed; i++)
                            _controlFrameBuffer[i] = _header.DecodeByte(_controlFrameBuffer[i]);
                    var timestamp = DateTime.FromBinary(BitConverter.ToInt64(_controlFrameBuffer, 0));
                    break;
                default: throw new WebSocketException("Unexpected header option '" + _header.Flags.Option.ToString() + "'");
            }
        }

        volatile Boolean _messageInCourse = false; 
        readonly Object _locker = new Object();
        internal void WriteInternal(Byte[] buffer, Int32 offset, Int32 count, Boolean isCompleted, Boolean headerSent, WebSocketFrameOption option)
        {
            try
            {
                if(!option.IsData() && _messageInCourse)
                    return;
                
                try
                {
                    Monitor.Enter(_locker);

                    if (!option.IsData() && _messageInCourse)
                        return;

                    _messageInCourse = true;

                    if (this.IsConnected)
                    {
                        Stream s = _client.GetStream();
                        var header = WebSocketFrameHeader.Create(count, isCompleted, headerSent, option);
                        s.Write(header.Raw, 0, header.Raw.Length);
                        s.Write(buffer, offset, count);
                    }
                }
                finally
                {
                    _messageInCourse = !isCompleted;
                    Monitor.Exit(_locker);
                }

            }
            catch (ObjectDisposedException)
            {
                Close();
            }
            catch (IOException)
            {
                Close();
            }
        }

        private void Ping(Object state)
        {
            var array = BitConverter.GetBytes(DateTime.UtcNow.ToBinary());
            this.WriteInternal(array, 0, array.Length, true, false, WebSocketFrameOption.Ping);
        }
        public void Close()
        {
            try
            {
                _closed = true;
                _client.Close();
            }
            catch { }
        }

        private Int32 ReturnAndClose()
        {
            this.Close();
            return 0;
        }

        public void Dispose()
        {
            try
            {
                this.Close();
                _client.Client.Dispose();
            }
            catch { }
        }
    }

}
