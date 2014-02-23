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
        public static readonly Int32 BufferLength = 4096;

        readonly TcpClient _client;
        public IPEndPoint RemoteEndpoint { get; private set; }
        public IPEndPoint LocalEndpoint { get; private set; }
        public Boolean IsConnected { get { return _client.Client.Connected; } }
        public WebSocketHttpRequest HttpRequest { get; private set; }

        WebSocketFrameHeader _header;
        internal WebSocketFrameHeader Header { get { return _header; } }

        readonly TimeSpan _pingInterval;

        public WebSocketClient(TcpClient client, WebSocketHttpRequest httpRequest, TimeSpan pingInterval)
        {
            if (client == null)
                throw new ArgumentNullException("client");
            _client = client;
            RemoteEndpoint = (IPEndPoint)_client.Client.RemoteEndPoint;
            LocalEndpoint = (IPEndPoint)_client.Client.LocalEndPoint;
            HttpRequest = httpRequest;
            _pingInterval = pingInterval;
            PingAsync();
        }
                
        public async Task<WebSocketMessageReadStream> ReadMessageAsync(CancellationToken token)
        {
            var message = new WebSocketMessageReadStream(this);
            await AwaitHeaderAsync(token);
            if (_header != null)
            {
                message.MessageType = (WebSocketMessageType)_header.Option;
                return message;
            }
            return null;
        }

        public WebSocketMessageWriteStream CreateMessageWriter(WebSocketMessageType messageType)
        {
            return new WebSocketMessageWriteStream(this,messageType);
        }

        private async Task AwaitHeaderAsync(CancellationToken token)
        {
            try
            {
                Int32 readed = 0, headerLength;
                UInt64 contentLength;
                NetworkStream clientStream = _client.GetStream();

                while (_header == null && _client.Connected)
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

                    if (!_header.Option.IsData())
                    {
                        ProcessControlFrame(clientStream);
                        readed = 0;
                        _header = null;
                    }
                }
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
        readonly Byte[] _headerBuffer = new Byte[14];
        internal async Task<Int32> ReadInternalAsync(Byte[] buffer, Int32 bufferOffset, Int32 bufferCount, CancellationToken token)
        {
            try
            {
                if (bufferCount < buffer.Length - bufferOffset)
                    throw new ArgumentException("There is not space in the array for that length considering that offset.");

                if (_header.ContentLength < (UInt64)bufferCount)
                    bufferCount = (Int32)_header.ContentLength;

                if (_header.RemainingBytes < (UInt64)bufferCount)
                    bufferCount = (Int32)_header.RemainingBytes;

                var readed = await _client.GetStream().ReadAsync(buffer, bufferOffset, bufferCount, token);

                for (int i = 0; i < readed; i++)
                    buffer[i + bufferOffset] = _header.DecodeByte(buffer[i + bufferOffset]);

                return readed;
            }
            catch (IOException)
            {
                return ReturnAndClose();
            }
        }

        readonly Byte[] _controlFrameBuffer = new Byte[125];
        private void ProcessControlFrame(NetworkStream clientStream)
        {
            switch (_header.Option)
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
                    for (int i = 0; i < readed; i++)
                        _controlFrameBuffer[i] = _header.DecodeByte(_controlFrameBuffer[i]);
                    var timestamp = DateTime.FromBinary(BitConverter.ToInt64(_controlFrameBuffer, 0));
                    break;
                default: throw new WebSocketException("Unexpected header option '" + _header.Option.ToString() + "'");
            }
        }

        ManualResetEventSlim _controlFrameGate = new ManualResetEventSlim(true);
        SemaphoreSlim _writeSemaphore = new SemaphoreSlim(1);
        internal async Task WriteInternalAsync(Byte[] buffer, Int32 offset, Int32 count, Boolean isCompleted, Boolean headerSent, WebSocketFrameOption option, CancellationToken token)
        {
            try
            {
                if(!option.IsData()) 
                    _controlFrameGate.Wait(token);

                await _writeSemaphore.WaitAsync(token);

                if (!isCompleted)
                    _controlFrameGate.Reset();

                if (!option.IsData() && !isCompleted)
                    return;

                if (_client.Connected)
                {
                    Stream s = _client.GetStream();
                    var header = WebSocketFrameHeader.Create(count, isCompleted, headerSent, option);
                    await s.WriteAsync(header.Raw, 0, header.Raw.Length);
                    await s.WriteAsync(buffer, offset, count);
                }
                
            }
            catch(IOException)
            {
                Close();
            }
            finally
            {
                if (isCompleted)
                    _controlFrameGate.Set();

                _writeSemaphore.Release();
                  
            }
        }
        private async Task PingAsync()
        {
            try
            {
                while (_client.Connected)
                {
                    await Task.Delay(_pingInterval);

                    if (!_client.Connected)
                        return;

                    var array = BitConverter.GetBytes(DateTime.UtcNow.ToBinary());
                    await WriteInternalAsync(array, 0, array.Length, true, false, WebSocketFrameOption.Ping, CancellationToken.None);
                }
            }
            catch(IOException)
            {
                Close();
            }
        }
        public async Task Close()
        {
            try
            {
                _client.Close();
                _client.Client.Dispose();
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
            this.Close();
        }
    }

}
