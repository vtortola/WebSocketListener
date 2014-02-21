using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
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
        public Uri RequestUri { get; private set; }
        public Version HttpVersion { get; private set; }
        public CookieContainer Cookies { get; private set; }
        public HttpHeadersCollection Headers { get; private set; }

        public WebSocketClient(TcpClient client, Uri uri, Version version, CookieContainer cookies, HttpHeadersCollection headers)
        {
            if (client == null)
                throw new ArgumentNullException("client");
            _client = client;
            RemoteEndpoint = (IPEndPoint)_client.Client.RemoteEndPoint;
            LocalEndpoint = (IPEndPoint)_client.Client.LocalEndPoint;
            RequestUri = uri;
            HttpVersion = version;
            Cookies = cookies;
            Headers = headers;
        }

        public void Close()
        {
            _client.Close();
            _client.Client.Dispose(); 
        }

        public WebSocketReadState ReturnAndClose()
        {
            this.Close();
            return WebSocketReadState.Empty;
        }
        
        WebSocketFrameHeader _header;
        readonly Byte[] _headerBuffer = new Byte[14];
        public async Task<WebSocketReadState> ReadAsync(Byte[] buffer, Int32 bufferOffset, Int32 bufferCount)
        {
            if (bufferCount < buffer.Length - bufferOffset)
                throw new ArgumentException("There is not space in the array for that length considering that offset.");
                                   
            Int32 readed =0, headerLength;
            UInt64 contentLength;
            NetworkStream clientStream = _client.GetStream();
            if(_header == null)
            {
                do
                { // Checking for small frame
                    readed += await clientStream.ReadAsync(_headerBuffer, 0, 6); // 6 = 2 minimal header + 4 key
                    if (readed == 0)
                        return ReturnAndClose();
                }
                while (readed < 6);

                
                if (!WebSocketFrameHeader.TryParseLengths(_headerBuffer, 0, readed, out headerLength, out contentLength))
                { // Checking for medium frame
                    if (!clientStream.ReadUntilCount(ref readed, _headerBuffer, readed, 2, 8)) // 8 = 2 header + 2 size + 4 key
                        return ReturnAndClose();
                }

                if (!WebSocketFrameHeader.TryParseLengths(_headerBuffer, 0, readed, out headerLength, out contentLength))
                { // Checking for large frame
                    if (!clientStream.ReadUntilCount(ref readed, _headerBuffer, readed, 6, 14)) // 14 = 2 header + 8 size + 4 key
                        return ReturnAndClose();
                }
                
                if (!WebSocketFrameHeader.TryParse(_headerBuffer, 0, readed, out _header))
                    throw new WebSocketException("Cannot understand header");
            }

            if (!_header.Option.IsData())
            {
                ProcessProtocolFrame(clientStream);
                if (!_client.Connected)
                    return null;

                return await ReadAsync(buffer,bufferOffset, bufferCount);
            }

            if ((UInt64)bufferCount > _header.RemainingBytes)
                bufferCount = (Int32)_header.RemainingBytes;

            readed = await clientStream.ReadAsync(buffer, bufferOffset, bufferCount);
            for (int i = 0; i < readed; i++)
                buffer[i + bufferOffset] = _header.DecodeByte(buffer[i + bufferOffset]);

            WebSocketReadState state = new WebSocketReadState();
            state.BytesRemaining = _header.RemainingBytes;
            state.BytesReaded = readed;
            state.MessageType = (WebSocketMessageType)_header.Option;
            state.IsCompleted = !_header.IsPartial && state.BytesRemaining == 0;

            if (_header.RemainingBytes == 0)
                _header = null;

            return state;
        }

        readonly Byte[] _controlFrame = new Byte[125];
        private void ProcessProtocolFrame(NetworkStream clientStream)
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
                    Int32 frameLength =  _controlFrame.Length;
                    if(_header.ContentLength < 125)
                        frameLength = (Int32)_header.ContentLength;
                    var readed = clientStream.Read(_controlFrame, 0, frameLength);            
                    break;
                default: throw new WebSocketException("Unexpected header option '" + _header.Option.ToString() + "'");
            }
        }

        public async Task WriteAsync(Byte[] buffer, Int32 offset, Int32 count, Boolean isCompleted, WebSocketMessageType messageType)
        {
            if (_client.Connected)
            {
                Stream s = _client.GetStream();
                var header = WebSocketFrameHeader.Create(count, !isCompleted, (WebSocketFrameOption)messageType);
                await s.WriteAsync(header.Raw, 0, header.Raw.Length);
                await s.WriteAsync(buffer, offset, count);
            }
        }

        public Task WriteAsync(String data)
        {
            var dataArray = Encoding.UTF8.GetBytes(data);
            return WriteAsync(dataArray, 0, dataArray.Length, true, WebSocketMessageType.Text);
        }

        public async Task<String> ReadAsync()
        {
            Byte[] buffer = new Byte[BufferLength];
            var state = await ReadAsync(buffer, 0, BufferLength);

            if (state == null || state.MessageType == WebSocketMessageType.Closing)
                return null;

            UInt64 messageLength = state.BytesRemaining + (UInt64)state.BytesReaded;

            if (state.IsCompleted && state.BytesRemaining == 0 && state.BytesReaded != 0)
                return Encoding.UTF8.GetString(buffer, 0, state.BytesReaded);

            Int32 capacity = (UInt64)Int32.MaxValue < messageLength ? Int32.MaxValue : (Int32)messageLength;
            using (MemoryStream ms = new MemoryStream(capacity))
            {
                ms.Write(buffer, 0, state.BytesReaded);
                while (!state.IsCompleted)
                {
                    state = await ReadAsync(buffer, 0, BufferLength);
                    if (state.MessageType == WebSocketMessageType.Closing)
                        return null;
                    ms.Write(buffer, 0, state.BytesReaded);
                }
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        public void Dispose()
        {
            this.Close();
        }
    }

}
