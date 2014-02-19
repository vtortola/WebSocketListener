using System;
using System.Collections.Generic;
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

        readonly Byte[] _tail, _buffer;
        Int32 _tailLength;
        public WebSocketClient(TcpClient client, Uri uri, Version version, CookieContainer cookies, HttpHeadersCollection headers)
        {
            if (client == null)
                throw new ArgumentNullException("client");
            _client = client;
            RemoteEndpoint = (IPEndPoint)_client.Client.RemoteEndPoint;
            LocalEndpoint = (IPEndPoint)_client.Client.LocalEndPoint;
            _tail = new Byte[BufferLength];
            _buffer = new Byte[BufferLength];
            _tailLength = 0;
            RequestUri = uri;
            HttpVersion = version;
            Cookies = cookies;
            Headers = headers;
        }

        private async Task<String> ProcessFrameAsync(WebSocketFrame frame)
        {
            if (frame.Header.Option == WebSocketFrameOption.Text)
                return Encoding.UTF8.GetString(frame.StreamData.ToArray());
            else if (frame.Header.Option == WebSocketFrameOption.Ping)
            {
                var pongFrame = new WebSocketFrame(frame.StreamData.ToArray(), WebSocketFrameOption.Pong);
                await _client.GetStream().WriteAsync(pongFrame.StreamData.ToArray(), 0, pongFrame.StreamData.ToArray().Length);
                return await ReadAsync();
            }
            else
                throw new WebSocketException("WebSocket option not supported " + frame.Header.Option.ToString());
        }

        public void Close()
        {
            _client.Close();
            _client.Client.Dispose(); 
        }

        public async Task<String> ReadAsync()
        {
            WebSocketFrameHeader header;
            Int32 readed = 0;

            if (_tailLength != 0 && WebSocketFrameHeader.TryParse(_tail, _tailLength, out header))
            {
                UInt64 frameLength = (UInt64)header.HeaderLength + header.ContentLength + 4;
                if (frameLength <= (UInt64)_tailLength)
                { // frame is already in tail
                    readed = (Int32)frameLength;
                    _tailLength = _tailLength - readed;
                    Array.Copy(_tail, 0, _buffer, 0, readed);
                    for (int i = 0; i < _tailLength; i++) // shift
                        _tail[i] = _tail[i + readed];

                    WebSocketFrame frame = new WebSocketFrame(header);
                    frame.Write(_buffer, header.HeaderLength, readed - header.HeaderLength);
                    return await ProcessFrameAsync(frame);
                }
                else
                {  // set tail to buffer
                    readed = _tailLength;
                    Array.Copy(_tail, 0, _buffer, 0, _tailLength);
                    _tailLength = 0;
                }
            }

            while(!WebSocketFrameHeader.TryParse(_buffer, readed, out header))
            {
                Int32 r = await _client.GetStream().ReadAsync(_buffer, readed, BufferLength - readed);
                if (r == 0)
                {
                    this.Close();
                    return null;
                }
                readed += r;
            }          

            if (header.Option == WebSocketFrameOption.ConnectionClose)
            {
                _client.Close();
                return null;
            }

            if (!header.IsPartial)
            {
                WebSocketFrame frame = new WebSocketFrame(header);
                
                UInt64 frameLength = (UInt64)header.HeaderLength + header.ContentLength + 4;
                if (frameLength < (UInt64)readed)
                {
                    _tailLength = readed - (Int32)frameLength;
                    Array.Copy(_buffer, (Int32)frameLength, _tail, 0, _tailLength);
                }
                else
                    _tailLength=0;

                if (frameLength <= (UInt64)readed)
                {    
                    frame.Write(_buffer, header.HeaderLength, (Int32)header.ContentLength + 4);
                }
                else
                {
                    if(header.HeaderLength < readed)
                        frame.Write(_buffer, header.HeaderLength, readed - header.HeaderLength);
                    
                    UInt64 missingBytes = frameLength - (UInt64)readed;
                    Int32 bytesToRead = _buffer.Length;

                    while (missingBytes > 0)
                    {
                        if (missingBytes < (UInt64)bytesToRead)
                            bytesToRead = (Int32)missingBytes;

                        readed = await _client.GetStream().ReadAsync(_buffer, 0, bytesToRead);
                        frame.Write(_buffer, 0, readed);
                        missingBytes = missingBytes - (UInt64)readed;
                    }
                }

                return await ProcessFrameAsync(frame);
            }
            else 
                throw new WebSocketException("Partial frames not supported yet.");
        }

        public async Task WriteAsync(String data)
        {
            var frame = new WebSocketFrame(Encoding.UTF8.GetBytes(data), WebSocketFrameOption.Text);
            await _client.GetStream().WriteAsync(frame.Header.Raw, 0, frame.Header.HeaderLength);
            await frame.StreamData.CopyToAsync(_client.GetStream());
        }

        public void Dispose()
        {
            this.Close();
        }
    }

}
