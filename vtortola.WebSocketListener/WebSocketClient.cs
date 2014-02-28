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
        Int32 _gracefullyClosed;
        public Boolean IsConnected { get { return !_closed &&_client.Client.Connected; } }
        public WebSocketHttpRequest HttpRequest { get; private set; }

        internal WebSocketFrameHeader Header { get; private set; }

        readonly TimeSpan _pingInterval,_pingTimeout;
        DateTime _lastPong;
        public WebSocketClient(TcpClient client, WebSocketHttpRequest httpRequest, TimeSpan pingTimeOut)
        {
            if (client == null)
                throw new ArgumentNullException("client");
            _client = client;
            RemoteEndpoint = (IPEndPoint)_client.Client.RemoteEndPoint;
            LocalEndpoint = (IPEndPoint)_client.Client.LocalEndPoint;
            HttpRequest = httpRequest;
            _pingTimeout = pingTimeOut;
            _lastPong = DateTime.Now.Add(_pingTimeout);
            _pingInterval = TimeSpan.FromMilliseconds( Math.Min(5000, pingTimeOut.TotalMilliseconds / 4));
            PingAsync();
        }
                
        public async Task<WebSocketMessageReadStream> ReadMessageAsync(CancellationToken token)
        {
            var message = new WebSocketMessageReadStream(this);
            await AwaitHeaderAsync(token);
            if (this.IsConnected && Header != null)
            {
                message.MessageType = (WebSocketMessageType)Header.Flags.Option;
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

                while (this.IsConnected && Header == null)
                {
                    do
                    { 
                        // read small frame
                        readed += await clientStream.ReadAsync(_headerBuffer, 0, 6, token); // 6 = 2 minimal header + 4 key
                        if (readed == 0 || token.IsCancellationRequested)
                        {
                            Close();
                            return;
                        }
                    }
                    while (readed < 6);

                    // Checking for small frame
                    if (!WebSocketFrameHeader.TryParseLengths(_headerBuffer, 0, readed, out headerLength, out contentLength))
                    {   // Read for medium frame
                        if (!clientStream.ReadSynchronouslyUntilCount(ref readed, _headerBuffer, readed, 2, 8, token)) // 8 = 2 header + 2 size + 4 key
                        {
                            Close();
                            return;
                        }

                        // check for medium frame
                        if (!WebSocketFrameHeader.TryParseLengths(_headerBuffer, 0, readed, out headerLength, out contentLength))
                        { 
                            // read for large frame
                            if (!clientStream.ReadSynchronouslyUntilCount(ref readed, _headerBuffer, readed, 6, 14, token)) // 14 = 2 header + 8 size + 4 key
                            {
                                Close();
                                return;
                            }
                        }
                    }

                    if (token.IsCancellationRequested)
                    {
                        Close();
                        return;
                    }

                    WebSocketFrameHeader header;
                    if (!WebSocketFrameHeader.TryParse(_headerBuffer, 0, readed, out header))
                        throw new WebSocketException("Cannot understand header");

                    Header = header;

                    if (!Header.Flags.Option.IsData())
                    {
                        ProcessControlFrame(clientStream);
                        readed = 0;
                        Header = null;
                    }
                }
            }
            catch (InvalidOperationException)
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
            Header = null;
        }
        internal async Task<Int32> ReadInternalAsync(Byte[] buffer, Int32 offset, Int32 count, CancellationToken cancellationToken)
        {
            try
            {
                var readed = await _client.GetStream().ReadAsync(buffer, offset, count, cancellationToken);

                if (Header.Flags.MASK)
                    DecodeMaskedData(buffer, offset, readed);

                return readed;
            }
            catch (InvalidOperationException)
            {
                return ReturnAndClose();
            }
            catch (IOException)
            {
                return ReturnAndClose();
            }
        }     
        internal Int32 ReadInternal(Byte[] buffer, Int32 offset, Int32 count)
        {
            try
            {
                var readed = _client.GetStream().Read(buffer, offset, count);

                if (Header.Flags.MASK)
                    DecodeMaskedData(buffer, offset, readed);

                return readed;
            }
            catch (InvalidOperationException)
            {
                return ReturnAndClose();
            }
            catch (IOException)
            {
                return ReturnAndClose();
            }
        }
        private void DecodeMaskedData(Byte[] buffer, Int32 bufferOffset, int readed)
        {
            for (int i = 0; i < readed; i++)
                buffer[i + bufferOffset] = Header.DecodeByte(buffer[i + bufferOffset]);
        }
        readonly Byte[] _controlFrameBuffer = new Byte[125];
        private void ProcessControlFrame(NetworkStream clientStream)
        {
            switch (Header.Flags.Option)
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
                    if (Header.ContentLength < 125)
                        contentLength = (Int32)Header.ContentLength;
                    Int32 readed = 0;
                    while (Header.RemainingBytes > 0)
                    {
                        readed = clientStream.Read(_controlFrameBuffer, readed, contentLength - readed);
                        if (Header.Flags.MASK)
                            for (int i = 0; i < readed; i++)
                                _controlFrameBuffer[i] = Header.DecodeByte(_controlFrameBuffer[i]);
                    }
                    var ticks = BitConverter.ToInt64(_controlFrameBuffer, 0);
                    if (ticks >= DateTime.MinValue.Ticks && ticks <= DateTime.MaxValue.Ticks)
                        _lastPong = new DateTime(ticks);
                    break;
                default: throw new WebSocketException("Unexpected header option '" + Header.Flags.Option.ToString() + "'");
            }
        }

        private async Task PingAsync()
        {
            await Task.Yield();
            while (this.IsConnected)
            {
                await Task.Delay(_pingInterval);

                var now = DateTime.Now;

                if (_lastPong.Add(_pingTimeout).Add(_pingInterval) < now)
                    Close();
                else
                    this.WriteInternal(BitConverter.GetBytes(now.Ticks), 0, 8, true, false, WebSocketFrameOption.Ping);
            }
        }

        readonly SemaphoreSlim _writeSemaphore = new SemaphoreSlim(1);
        internal void WriteInternal(Byte[] buffer, Int32 offset, Int32 count, Boolean isCompleted, Boolean headerSent, WebSocketFrameOption option)
        {
            try
            {
                _writeSemaphore.Wait(_client.SendTimeout);
                Stream s = _client.GetStream();
                var header = WebSocketFrameHeader.Create(count, isCompleted, headerSent, option);
                s.Write(header.Raw, 0, header.Raw.Length);
                if (count > 0)
                    s.Write(buffer, offset, count);
            }
            catch (InvalidOperationException)
            {
                Close();
            }
            catch (IOException)
            {
                Close();
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }

        internal async Task WriteInternalAsync(Byte[] buffer, Int32 offset, Int32 count, Boolean isCompleted, Boolean headerSent, WebSocketFrameOption option, CancellationToken cancellation)
        {
            try
            {
                await _writeSemaphore.WaitAsync(_client.SendTimeout, cancellation);
                Stream s = _client.GetStream();
                var header = WebSocketFrameHeader.Create(count, isCompleted, headerSent, option);
                await s.WriteAsync(header.Raw, 0, header.Raw.Length);
                if(count>0)
                    await s.WriteAsync(buffer, offset, count);
            }
            catch (InvalidOperationException)
            {
                Close();
            }
            catch (IOException)
            {
                Close();
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }

        static readonly Byte[] _emptyFrame = new Byte[0];
        public void Close()
        {
            try
            {
                if (Interlocked.CompareExchange(ref _gracefullyClosed, 1,0) == 0)
                    WriteInternal(_emptyFrame, 0, 0, true, false, WebSocketFrameOption.ConnectionClose);
                
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
