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
        readonly TcpClient _client;
        public IPEndPoint RemoteEndpoint { get; private set; }
        public IPEndPoint LocalEndpoint { get; private set; }
        public Boolean IsConnected { get { return _client.Client.Connected; } }

        Byte[] _rest;

        public WebSocketClient(TcpClient client)
        {
            if (client == null)
                throw new ArgumentNullException("client");
            _client = client;
            RemoteEndpoint = (IPEndPoint)_client.Client.RemoteEndPoint;
            LocalEndpoint = (IPEndPoint)_client.Client.LocalEndPoint;
            _rest = new Byte[0];
        }

        public async Task<String> ReadAsync()
        {
            Byte[] headerBuffer = new Byte[10];
            Int32 readed = _rest.Length;
            if (readed != 0)
                Array.Copy(_rest, 0, headerBuffer, 0, readed);

            while (readed == _rest.Length)
                readed += await _client.GetStream().ReadAsync(headerBuffer, readed, headerBuffer.Length - readed);

            var header = new WebSocketFrameHeader(headerBuffer);

            if (header.Option == WebSocketFrameOption.ConnectionClose)
            {
                _client.Close();
                return null;
            }

            if (header.Option != WebSocketFrameOption.Text)
                return await ReadAsync();

            if (!header.IsPartial)
            {
                Int32 contentBytesInHeader = 0;
                Byte[] contentBuffer = new Byte[header.ContentLength + 4];
                if (header.HeaderLength < headerBuffer.Length)
                {
                    contentBytesInHeader = Math.Min(header.ContentLength + 4, readed - header.HeaderLength);
                    Array.Copy(headerBuffer, header.HeaderLength, contentBuffer, 0, contentBytesInHeader);
                }

                var frameLength = header.HeaderLength + header.ContentLength + 4;
                if (frameLength < readed)
                {
                    _rest = new Byte[readed - frameLength];
                    Array.Copy(headerBuffer, frameLength, _rest, 0, readed - frameLength);
                }
                else
                    _rest = new Byte[0];

                WebSocketFrame frame = null;

                if (frameLength <= readed)
                {
                    frame = new WebSocketFrame(header, contentBuffer);
                }
                else
                {
                    while (contentBytesInHeader < header.ContentLength + 4)
                        contentBytesInHeader += await _client.GetStream().ReadAsync(contentBuffer, contentBytesInHeader, contentBuffer.Length - contentBytesInHeader);

                    frame = new WebSocketFrame(header, contentBuffer);
                }

                if (frame.Header.Option == WebSocketFrameOption.Text)
                    return Encoding.UTF8.GetString(frame.Data);
                else if (frame.Header.Option == WebSocketFrameOption.Ping)
                {
                    var pongFrame = new WebSocketFrame(frame.Data, WebSocketFrameOption.Pong);
                    await _client.GetStream().WriteAsync(pongFrame.Data, 0, pongFrame.Data.Length);
                    return await ReadAsync();
                }
                else
                    throw new WebSocketException("WebSocket option not supported " + frame.Header.Option.ToString());
            }
            else
                throw new WebSocketException("Partial frames not supported yet.");
        }

        public async Task WriteAsync(String data)
        {
            var frame = new WebSocketFrame(Encoding.UTF8.GetBytes(data), WebSocketFrameOption.Text);
            await _client.GetStream().WriteAsync(frame.Data, 0, frame.Data.Length);
        }

        public void Dispose()
        {
            _client.Close();
            _client.Client.Dispose();
        }
    }

}
