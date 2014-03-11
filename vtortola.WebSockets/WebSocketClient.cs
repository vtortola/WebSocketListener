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
    public sealed class WebSocketClient : IDisposable
    {
        readonly TcpClient _client;
        readonly Stream _clientStream;
        Int32 _gracefullyClosed, _closed, _disposed;
        readonly TimeSpan _pingInterval, _pingTimeout;
        DateTime _lastPong;
        readonly IReadOnlyList<IWebSocketMessageExtensionContext> _extensions;

        public IPEndPoint RemoteEndpoint { get; private set; }
        public IPEndPoint LocalEndpoint { get; private set; }
        public Boolean IsConnected { get { return _closed != 1 && _client.Client.Connected; } }
        public WebSocketHttpRequest HttpRequest { get; private set; }
        internal WebSocketFrameHeader Header { get; private set; }
        internal readonly Byte[] WriteBufferTail;
                
        public WebSocketClient(TcpClient client, Stream clientStream, WebSocketHttpRequest httpRequest, TimeSpan pingTimeOut, IReadOnlyList<IWebSocketMessageExtensionContext> extensions)
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
            _extensions = extensions;
            _clientStream = clientStream;
            WriteBufferTail = new Byte[_client.SendBufferSize];

            PingAsync();
        }
                
        public async Task<WebSocketMessageReadStream> ReadMessageAsync(CancellationToken token)
        {
            if(Header==null)
                await AwaitHeaderAsync(token);

            if (this.IsConnected && Header != null)
            {
                WebSocketMessageReadStream reader = new WebSocketMessageReadNetworkStream(this, Header);
                foreach (var extension in _extensions)
                    reader = extension.ExtendReader(reader);
                return reader;
            }

            return null;
        }

        public WebSocketMessageWriteStream CreateMessageWriter(WebSocketMessageType messageType)
        {
            WebSocketMessageWriteStream writer = new WebSocketMessageWriteNetworkStream(this, messageType);

            foreach (var extension in _extensions)
                writer = extension.ExtendWriter(writer);

            return writer;
        }

        readonly Byte[] _headerBuffer = new Byte[14];
        private WebSocketFrameHeader ParseHeader(ref Int32 readed, CancellationToken token)
        {
            WebSocketFrameHeader header;

            if(readed<6)
            {
                if (!_clientStream.ReadSynchronouslyUntilCount(ref readed, _headerBuffer, readed, 6, token)) // 6 = 2 header + 4 key
                {
                    Close();
                    return null;
                }
            }

            // Checking for small frame
            if (!WebSocketFrameHeader.TryParse(_headerBuffer, 0, readed, out header))
            {   // Read for medium frame
                if (!_clientStream.ReadSynchronouslyUntilCount(ref readed, _headerBuffer, readed, 8, token)) // 8 = 2 header + 2 size + 4 key
                {
                    Close();
                    return null;
                }

                // check for medium frame
                if (!WebSocketFrameHeader.TryParse(_headerBuffer, 0, readed, out header))
                {
                    // read for large frame
                    if (!_clientStream.ReadSynchronouslyUntilCount(ref readed, _headerBuffer, readed, 14, token)) // 14 = 2 header + 8 size + 4 key
                    {
                        Close();
                        return null;
                    }

                    // check for large frame
                    if (!WebSocketFrameHeader.TryParse(_headerBuffer, 0, readed, out header))
                        throw new WebSocketException("Cannot understand frame header");
                }
            }
            return header;
        }
        internal void AwaitHeader()
        {
            try
            {
                while (this.IsConnected && Header == null)
                {
                    // read small frame
                    Int32 readed =  _clientStream.Read(_headerBuffer, 0, 6); // 6 = 2 minimal header + 4 key
                    if (readed == 0 )
                    {
                        Close();
                        return;
                    }

                    Header = ParseHeader(ref readed, CancellationToken.None);
                    if (Header == null)
                    {
                        Close();
                        return;
                    }

                    if (!Header.Flags.Option.IsData())
                    {
                        ProcessControlFrame(_clientStream);
                        readed = 0;
                        Header = null;
                    }
                }
            }
            catch (InvalidOperationException)
            {
                Close();
            }
            catch (IOException)
            {
                Close();
            }
        }  
        internal async Task AwaitHeaderAsync(CancellationToken token)
        {
            try
            {
                while (this.IsConnected && Header == null)
                {
                        // read small frame
                    Int32 readed = await _clientStream.ReadAsync(_headerBuffer, 0, 6, token); // 6 = 2 minimal header + 4 key
                    if (readed == 0 || token.IsCancellationRequested)
                    {
                        Close();
                        return;
                    }

                    Header = ParseHeader(ref readed, token);
                    if(Header == null || token.IsCancellationRequested)
                    {
                        Close();
                        return;
                    }

                    if (!Header.Flags.Option.IsData())
                    {
                        ProcessControlFrame(_clientStream);
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
                var readed = await _clientStream.ReadAsync(buffer, offset, count, cancellationToken);

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
                var readed = _clientStream.Read(buffer, offset, count);

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
            Header.DecodeBytes(buffer, bufferOffset, readed);
        }

        readonly Byte[] _controlFrameBuffer = new Byte[125];
        private void ProcessControlFrame(Stream clientStream)
        {
            switch (Header.Flags.Option)
            {
                case WebSocketFrameOption.Continuation:
                case WebSocketFrameOption.Text:
                case WebSocketFrameOption.Binary:
                    throw new ArgumentException("Text, Continuation or Binary are not protocol frames");
                    break;

                case WebSocketFrameOption.ConnectionClose:
                    this.Close();
                    break;

                case WebSocketFrameOption.Ping:
                    break;

                case WebSocketFrameOption.Pong:
                    Int32 contentLength =  _controlFrameBuffer.Length;
                    if (Header.ContentLength < 125)
                        contentLength = (Int32)Header.ContentLength;
                    Int32 readed = 0;
                    while (Header.RemainingBytes > 0)
                    {
                        readed = clientStream.Read(_controlFrameBuffer, readed, contentLength - readed);
                        Header.DecodeBytes(_controlFrameBuffer, 0, readed);
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
                    this.WriteInternal(BitConverter.GetBytes(now.Ticks), 0, 8, true, false, WebSocketFrameOption.Ping, WebSocketExtensionFlags.None);
            }
        }

        readonly SemaphoreSlim _writeSemaphore = new SemaphoreSlim(1);
        internal void WriteInternal(Byte[] buffer, Int32 offset, Int32 count, Boolean isCompleted, Boolean headerSent, WebSocketFrameOption option, WebSocketExtensionFlags extensionFlags)
        {
            try
            {
                var header = WebSocketFrameHeader.Create(count, isCompleted, headerSent, option, extensionFlags);

                if (buffer.Length >= offset + count + header.HeaderLength)
                {
                    buffer.ShiftRight(header.HeaderLength + offset, count);
                    Array.Copy(header.Raw, 0, buffer, offset, header.HeaderLength);

                    if (!_writeSemaphore.Wait(_client.SendTimeout))
                        throw new WebSocketException("Write timeout");
                    _clientStream.Write(buffer, offset, count + header.HeaderLength);
                }
                else
                {
                    if (!_writeSemaphore.Wait(_client.SendTimeout))
                        throw new WebSocketException("Write timeout");
                    _clientStream.Write(header.Raw, 0, header.HeaderLength);
                    _clientStream.Write(buffer, offset, count);
                }
            }
            catch (InvalidOperationException)
            {
                Close();
            }
            catch (IOException)
            {
                Close();
            }
            catch(Exception)
            {
                Close();
                throw;
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }
        internal async Task WriteInternalAsync(Byte[] buffer, Int32 offset, Int32 count, Boolean isCompleted, Boolean headerSent, WebSocketFrameOption option, WebSocketExtensionFlags extensionFlags, CancellationToken cancellation)
        {
            try
            {
               var header = WebSocketFrameHeader.Create(count, isCompleted, headerSent, option, extensionFlags);

                if (buffer.Length >= offset + count + header.HeaderLength)
                {
                    buffer.ShiftRight(header.HeaderLength + offset, count);
                    Array.Copy(header.Raw, 0, buffer, offset, header.HeaderLength);

                    if (!await _writeSemaphore.WaitAsync(_client.SendTimeout, cancellation))
                        throw new WebSocketException("Write timeout");
                    await _clientStream.WriteAsync(buffer, offset, count + header.HeaderLength, cancellation);
                }
                else
                {
                    if (!await _writeSemaphore.WaitAsync(_client.SendTimeout, cancellation))
                        throw new WebSocketException("Write timeout");
                    await _clientStream.WriteAsync(header.Raw, 0, header.HeaderLength);
                    await _clientStream.WriteAsync(buffer, offset, count);
                }
            }
            catch (InvalidOperationException)
            {
                Close();
            }
            catch (IOException)
            {
                Close();
            }
            catch (Exception)
            {
                Close();
                throw;
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }
        private Int32 ReturnAndClose()
        {
            this.Close();
            return 0;
        }

        static readonly Byte[] _emptyFrame = new Byte[0];
        public void Close()
        {
            try
            {
                if (Interlocked.CompareExchange(ref _closed, 1, 0) == 1)
                    return;

                if (Interlocked.CompareExchange(ref _gracefullyClosed, 1,0) == 0)
                    WriteInternal(_emptyFrame, 0, 0, true, false, WebSocketFrameOption.ConnectionClose, WebSocketExtensionFlags.None);

                _clientStream.Close();
                _client.Close();
            }
            catch { }
        }

        private void Dispose(Boolean disposing)
        {
            if (Interlocked.CompareExchange(ref _disposed, 1,0) == 0)
            {
                if (disposing)
                    GC.SuppressFinalize(this);

                try
                {
                    _writeSemaphore.Dispose();
                    this.Close();
                    _client.Client.Dispose();
                }
                catch { }
            }
        }

        public void Dispose()
        {
            Dispose(true);   
        }

        ~WebSocketClient()
        {
            Dispose(false);
        }
    }

}
