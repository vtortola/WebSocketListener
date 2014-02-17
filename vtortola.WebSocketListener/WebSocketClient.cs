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
            Byte[] buffer = new Byte[4096];
            Int32 readed = _rest.Length;
            if (readed != 0)
                Array.Copy(_rest, 0, buffer, 0, readed);

            while (readed == _rest.Length)
                readed += await _client.GetStream().ReadAsync(buffer, readed, 10 - readed);

            var header = new WebSocketFrameHeader(buffer);

            if (header.Option == WebSocketFrameOption.ConnectionClose)
            {
                _client.Close();
                return null;
            }

            if (header.Option != WebSocketFrameOption.Text)
                return await ReadAsync();

            if (!header.IsPartial)
            {
                WebSocketFrame frame = new WebSocketFrame(header);
                
                UInt64 frameLength = (UInt64)header.HeaderLength + header.ContentLength + 4;
                if (frameLength < (UInt64)readed)
                {
                    _rest = new Byte[readed - (Int32)frameLength];
                    Array.Copy(buffer, (Int32)frameLength, _rest, 0, readed - (Int32)frameLength);
                }
                else
                    _rest = new Byte[0];

                if (frameLength <= (UInt64)readed)
                {    
                    frame.Write(buffer, header.HeaderLength, (Int32)header.ContentLength + 4);
                }
                else
                {
                    if(header.HeaderLength < readed)
                        frame.Write(buffer, header.HeaderLength, readed - header.HeaderLength);
                    
                    UInt64 missingBytes = frameLength - (UInt64)readed;
                    Int32 bytesToRead = buffer.Length;

                    while (missingBytes > 0)
                    {
                        if (missingBytes < (UInt64)bytesToRead)
                            bytesToRead = (Int32)missingBytes;

                        readed = await _client.GetStream().ReadAsync(buffer, 0, bytesToRead);
                        frame.Write(buffer, 0, readed);
                        missingBytes = missingBytes - (UInt64)readed;
                    }
                }

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
            _client.Close();
            _client.Client.Dispose();
        }
    }

}
