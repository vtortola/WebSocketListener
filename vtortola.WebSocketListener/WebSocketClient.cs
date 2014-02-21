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

        readonly Byte[] _tail;
        Int32 _tailLength;
        public WebSocketClient(TcpClient client, Uri uri, Version version, CookieContainer cookies, HttpHeadersCollection headers)
        {
            if (client == null)
                throw new ArgumentNullException("client");
            _client = client;
            RemoteEndpoint = (IPEndPoint)_client.Client.RemoteEndPoint;
            LocalEndpoint = (IPEndPoint)_client.Client.LocalEndPoint;
            _tail = new Byte[BufferLength];
            _tailLength = 0;
            RequestUri = uri;
            HttpVersion = version;
            Cookies = cookies;
            Headers = headers;
        }

        //private async Task<String> ProcessFrameAsync(WebSocketFrame frame)
        //{
        //    if (frame.Header.Option == WebSocketFrameOption.Text)
        //        return Encoding.UTF8.GetString(frame.StreamData.ToArray());
        //    else if (frame.Header.Option == WebSocketFrameOption.Ping)
        //    {
        //        var pongFrame = new WebSocketFrame(frame.StreamData.ToArray(), WebSocketFrameOption.Pong);
        //        await _client.GetStream().WriteAsync(pongFrame.StreamData.ToArray(), 0, pongFrame.StreamData.ToArray().Length);
        //        return await ReadAsync();
        //    }
        //    else
        //        throw new WebSocketException("WebSocket option not supported " + frame.Header.Option.ToString());
        //}

        public void Close()
        {
            _client.Close();
            _client.Client.Dispose(); 
        }

        WebSocketFrameHeader _header;
        public async Task<WebSocketReadState> ReadAsync(Byte[] buffer, Int32 bufferOffset, Int32 bufferLength)
        {
            if (bufferLength < buffer.Length - bufferOffset)
                throw new ArgumentException("There is not space in the array for that length considering that offset.");

            WebSocketReadState state = new WebSocketReadState();

            if (_tailLength == 0 )
            {
                if (_header == null)
                {
                    do
                    {
                        Int32 offset = bufferOffset;
                        Int32 length = bufferLength;
                        Int32 readed = await _client.GetStream().ReadAsync(buffer, offset, length);
                        if (readed == 0)
                        {
                            this.Close();
                            return WebSocketReadState.Empty;
                        }
                        offset += readed;
                        length -= readed;
                        state.BytesReaded += readed;
                    }
                    while (!WebSocketFrameHeader.TryParse(buffer, bufferOffset, state.BytesReaded, out _header));
                }
                else
                {
                    state.BytesReaded += await _client.GetStream().ReadAsync(buffer, bufferOffset, bufferLength);
                }

                var frameLength = _header.ContentLength + (UInt64)_header.HeaderLength;

                if ((UInt64)state.BytesReaded + _header.Cursor > frameLength)
                { // tail
                    _tailLength = state.BytesReaded + (Int32)_header.Cursor - (Int32)frameLength;
                    Array.Copy(buffer, bufferOffset + (Int32)frameLength, _tail, 0, _tailLength);
                    state.BytesReaded -= _tailLength;
                }
            }
            else
            {
                if (_tailLength > 0 && WebSocketFrameHeader.TryParse(_tail, 0, _tailLength, out _header))
                {// there is a full header
                    var frameLength = _header.ContentLength + (UInt64)_header.HeaderLength;
                    state.BytesReaded = Math.Min(bufferLength,_tailLength);
                    if ((UInt64)state.BytesReaded > frameLength)
                        state.BytesReaded = (Int32)frameLength;
                    Array.Copy(_tail, 0, buffer, bufferOffset, state.BytesReaded);
                    _tailLength -= state.BytesReaded;
                    _tail.ShiftLeft(state.BytesReaded, _tailLength);
                }
            }

            if(_header.Option == WebSocketFrameOption.ConnectionClose)
            {
                this.Close();
                return WebSocketReadState.Empty;
            }

            if (_header.Cursor == 0)
            {
                state.BytesReaded -= _header.HeaderLength;
                buffer.ShiftLeft(_header.HeaderLength, state.BytesReaded);
            }
            
            state.BytesRemaining = _header.ContentLength - (_header.Cursor + (UInt64)state.BytesReaded);
            state.MessageType = (WebSocketMessageType)_header.Option;
                        
            for (int i = bufferOffset; i < bufferOffset + state.BytesReaded; i++)
            {
                buffer[i] = _header.DecodeByte(buffer[i], _header.Cursor);
                _header.Cursor++;
            }

            if (state.BytesRemaining == 0)
                _header = null;

            return state;
        }

        public async Task WriteAsync(Byte[] buffer, Int32 offset, Int32 count, Boolean isCompleted, WebSocketMessageType messageType)
        {
            Stream s = _client.GetStream();
            var header = WebSocketFrameHeader.Create(count,!isCompleted,(WebSocketFrameOption)messageType);
            await s.WriteAsync(header.Raw, 0, header.Raw.Length);
            await s.WriteAsync(buffer, offset, count);
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

            Int32 capacity = (UInt64)Int32.MaxValue < messageLength ? Int32.MaxValue : (Int32)messageLength;

            using (MemoryStream ms = new MemoryStream(capacity))
            {
                ms.Write(buffer, 0, state.BytesReaded);
                while (state.BytesRemaining != 0 && state.BytesReaded != 0)
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
