using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Rfc6455
{
    public class WebSocketHandlerRfc6455 : WebSocketHandler
    {
        readonly Byte[] _headerBuffer = new Byte[14];
        readonly Byte[] _controlFrameBuffer = new Byte[125];
        readonly Byte[] _pingBuffer = new Byte[2];
        readonly SemaphoreSlim _writeSemaphore;
        readonly Stream _clientStream;
        readonly WebSocketListenerOptions _options;
        public readonly Byte[] WriteTailBuffer;
        Int32 _gracefullyClosed, _closed,_ongoingMessageWrite,_ongoingMessageAwaiting, _disposed;
        readonly TimeSpan _pingInterval, _pingTimeout;
        DateTime _lastPong;
        
        public override Boolean IsConnected { get { return _closed != 1; } }

        internal WebSocketFrameHeader CurrentHeader { get; set; }
              
        public WebSocketHandlerRfc6455(Stream clientStream, WebSocketListenerOptions options)
        {
            if (clientStream == null)
                throw new ArgumentNullException("clientStream");

            if (options == null)
                throw new ArgumentNullException("options");

            _writeSemaphore = new SemaphoreSlim(1);
            _options = options;
            
            _clientStream = clientStream;
            WriteTailBuffer = new Byte[_options.SendBufferSize];

            if (options.PingTimeout != Timeout.InfiniteTimeSpan)
            {
                _pingTimeout = options.PingTimeout;
                _lastPong = DateTime.Now.Add(_pingTimeout);
                _pingInterval = TimeSpan.FromMilliseconds(Math.Min(5000, options.PingTimeout.TotalMilliseconds / 2));

                Task.Run((Func<Task>)PingAsync);
            }
        }
        public override void EndWritting()
        {
            _ongoingMessageWrite = 0;
        }
        public override void BeginWritting()
        {
            if (Interlocked.CompareExchange(ref _ongoingMessageWrite, 1, 0) == 1)
                throw new WebSocketException("There is an ongoing message that is being written from somewhere else. Only a single write is allowed at the time.");
        }
        public override void AwaitHeader()
        {
            CheckForDoubleRead();
            try
            {
                while (this.IsConnected && CurrentHeader == null)
                {
                    // try read minimal frame
                    Int32 readed =  _clientStream.Read(_headerBuffer, 0, 6);
                    if (readed == 0 )
                    {
                        Close(WebSocketCloseReasons.ProtocolError);
                        return;
                    }

                    ParseHeader(readed);
                }
            }
            catch (InvalidOperationException)
            {
                Close(WebSocketCloseReasons.ProtocolError);
            }
            catch (IOException)
            {
                Close(WebSocketCloseReasons.ProtocolError);
            }
        }
        public override async Task AwaitHeaderAsync(CancellationToken cancellation)
        {
            CheckForDoubleRead();
            try
            {
                while (this.IsConnected && CurrentHeader == null)
                {
                    // try read minimal frame
                    Int32 readed = await _clientStream.ReadAsync(_headerBuffer, 0, 6, cancellation);
                    if (readed == 0 || cancellation.IsCancellationRequested)
                    {
                        Close(WebSocketCloseReasons.ProtocolError);
                        return;
                    }

                    ParseHeader(readed);
                }
            }
            catch (InvalidOperationException)
            {
                Close(WebSocketCloseReasons.ProtocolError);
            }
            catch(IOException)
            {
                Close(WebSocketCloseReasons.ProtocolError);
            }
        }
        private void ParseHeader(Int32 readed)
        {
            if (!TryReadHeaderUntil(ref readed, 6))
            {
                Close(WebSocketCloseReasons.ProtocolError);
                return;
            }

            Int32 headerlength = WebSocketFrameHeader.GetHeaderLength(_headerBuffer, 0);

            if (!TryReadHeaderUntil(ref readed, headerlength))
            {
                Close(WebSocketCloseReasons.ProtocolError);
                return;
            }

            WebSocketFrameHeader header;
            if (!WebSocketFrameHeader.TryParse(_headerBuffer, 0, headerlength, out header))
                throw new WebSocketException("Cannot understand frame header");

            CurrentHeader = header;

            if (!header.Flags.Option.IsData())
            {
                ProcessControlFrame(_clientStream);
                readed = 0;
                CurrentHeader = null;
            }
            else
                _ongoingMessageAwaiting = 0;
        }
        private Boolean TryReadHeaderUntil(ref Int32 readed, Int32 until)
        {
            Int32 r = 0;
            while (readed < until)
            {
                r = _clientStream.Read(_headerBuffer, readed, until - readed);
                if (r == 0)
                    return false;

                readed += r;
            }

            return true;
        }
        private void CheckForDoubleRead()
        {
            if (Interlocked.CompareExchange(ref _ongoingMessageAwaiting, 1, 0) == 1)
                throw new WebSocketException("There is an ongoing message await from somewhere else. Only a single write is allowed at the time.");

            if (CurrentHeader != null)
                throw new WebSocketException("There is an ongoing message that is being readed from somewhere else");
        }
        public override async Task<Int32> ReadInternalAsync(Byte[] buffer, Int32 offset, Int32 count, CancellationToken cancellationToken)
        {
            try
            {
                var readed = await _clientStream.ReadAsync(buffer, offset, count, cancellationToken);

                CurrentHeader.DecodeBytes(buffer, offset, readed);
                if (CurrentHeader.RemainingBytes == 0)
                    CurrentHeader = null;
                return readed;
            }
            catch (InvalidOperationException)
            {
                this.Close(WebSocketCloseReasons.UnexpectedCondition);
                return 0;
            }
            catch (IOException)
            {
                this.Close(WebSocketCloseReasons.UnexpectedCondition);
                return 0;
            }
        }
        public override Int32 ReadInternal(Byte[] buffer, Int32 offset, Int32 count)
        {
            try
            {
                var readed = _clientStream.Read(buffer, offset, count);

                CurrentHeader.DecodeBytes(buffer, offset, readed);
                if (CurrentHeader.RemainingBytes == 0)
                    CurrentHeader = null;
                return readed;
            }
            catch (InvalidOperationException)
            {
                this.Close(WebSocketCloseReasons.UnexpectedCondition);
                return 0;
            }
            catch (IOException)
            {
                this.Close(WebSocketCloseReasons.UnexpectedCondition);
                return 0;
            }
        }       
        private void ProcessControlFrame(Stream clientStream)
        {
            switch (CurrentHeader.Flags.Option)
            {
                case WebSocketFrameOption.Continuation:
                case WebSocketFrameOption.Text:
                case WebSocketFrameOption.Binary:
                    throw new WebSocketException("Text, Continuation or Binary are not protocol frames");

                case WebSocketFrameOption.ConnectionClose:
                    this.Close(WebSocketCloseReasons.NormalClose);
                    break;

                case WebSocketFrameOption.Ping: // read the buffer and echo
                case WebSocketFrameOption.Pong: // read the buffer, remember timestamp
                    Int32 contentLength =  _controlFrameBuffer.Length;
                    if (CurrentHeader.ContentLength < 125)
                        contentLength = (Int32)CurrentHeader.ContentLength;
                    Int32 readed = 0;
                    while (CurrentHeader.RemainingBytes > 0)
                    {
                        readed = clientStream.Read(_controlFrameBuffer, readed, contentLength - readed);
                        CurrentHeader.DecodeBytes(_controlFrameBuffer, 0, readed);
                    }

                    if(CurrentHeader.Flags.Option == WebSocketFrameOption.Pong)
                        _lastPong = DateTime.Now;
                    else // pong frames echo what was 'pinged'
                        this.WriteInternal(_controlFrameBuffer, 0, readed, true, false, WebSocketFrameOption.Pong, WebSocketExtensionFlags.None);
                    
                    break;
                default: throw new WebSocketException("Unexpected header option '" + CurrentHeader.Flags.Option.ToString() + "'");
            }
        }    
        private async Task PingAsync()
        {
            while (this.IsConnected)
            {
                await Task.Delay(_pingInterval);

                try
                {
                    var now = DateTime.Now;

                    if (_lastPong.Add(_pingTimeout).Add(_pingInterval) < now)
                        Close(WebSocketCloseReasons.NormalClose);
                    else
                        await this.WriteInternalAsync(_pingBuffer, 0, 0, true, false, WebSocketFrameOption.Ping, WebSocketExtensionFlags.None, CancellationToken.None);
                }
                catch{}
            }
        }
        public override void WriteInternal(Byte[] buffer, Int32 offset, Int32 count, Boolean isCompleted, Boolean headerSent, WebSocketMessageType type, WebSocketExtensionFlags extensionFlags)
        {
            WriteInternal(buffer, offset, count, isCompleted, headerSent, (WebSocketFrameOption)type, extensionFlags);
        }
        private void WriteInternal(Byte[] buffer, Int32 offset, Int32 count, Boolean isCompleted, Boolean headerSent, WebSocketFrameOption option, WebSocketExtensionFlags extensionFlags)
        {
            try
            {
                var header = WebSocketFrameHeader.Create(count, isCompleted, headerSent, option, extensionFlags);

                if (buffer.Length >= offset + count + header.HeaderLength)
                {
                    buffer.ShiftRight(header.HeaderLength + offset, count);
                    Array.Copy(header.Raw, 0, buffer, offset, header.HeaderLength);

                    if (!_writeSemaphore.Wait(_options.WebSocketSendTimeout))
                        throw new WebSocketException("Write timeout");
                    _clientStream.Write(buffer, offset, count + header.HeaderLength);
                }
                else
                {
                    if (!_writeSemaphore.Wait(_options.WebSocketSendTimeout))
                        throw new WebSocketException("Write timeout");
                    _clientStream.Write(header.Raw, 0, header.HeaderLength);
                    _clientStream.Write(buffer, offset, count);
                }
            }
            catch (InvalidOperationException)
            {
                Close(WebSocketCloseReasons.UnexpectedCondition);
            }
            catch (IOException)
            {
                Close(WebSocketCloseReasons.UnexpectedCondition);
            }
            catch(Exception ex)
            {
                Close(WebSocketCloseReasons.UnexpectedCondition);
                throw new WebSocketException("Cannot write on WebSocket", ex);
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }
        public override Task WriteInternalAsync(Byte[] buffer, Int32 offset, Int32 count, Boolean isCompleted, Boolean headerSent, WebSocketMessageType type, WebSocketExtensionFlags extensionFlags, CancellationToken cancellation)
        {
            return WriteInternalAsync(buffer, offset, count, isCompleted, headerSent, (WebSocketFrameOption)type, extensionFlags, cancellation);
        }
        private async Task WriteInternalAsync(Byte[] buffer, Int32 offset, Int32 count, Boolean isCompleted, Boolean headerSent, WebSocketFrameOption option, WebSocketExtensionFlags extensionFlags, CancellationToken cancellation)
        {
            try
            {
               var header = WebSocketFrameHeader.Create(count, isCompleted, headerSent, option, extensionFlags);

                if (buffer.Length >= offset + count + header.HeaderLength)
                {
                    buffer.ShiftRight(header.HeaderLength + offset, count);
                    Array.Copy(header.Raw, 0, buffer, offset, header.HeaderLength);

                    if (!await _writeSemaphore.WaitAsync(_options.WebSocketSendTimeout, cancellation))
                        throw new WebSocketException("Write timeout");
                    await _clientStream.WriteAsync(buffer, offset, count + header.HeaderLength, cancellation);
                }
                else
                {
                    if (!await _writeSemaphore.WaitAsync(_options.WebSocketSendTimeout, cancellation))
                        throw new WebSocketException("Write timeout");
                    await _clientStream.WriteAsync(header.Raw, 0, header.HeaderLength);
                    await _clientStream.WriteAsync(buffer, offset, count);
                }
            }
            catch (InvalidOperationException)
            {
                Close(WebSocketCloseReasons.UnexpectedCondition);
            }
            catch (IOException)
            {
                Close(WebSocketCloseReasons.UnexpectedCondition);
            }
            catch (Exception ex)
            {
                Close(WebSocketCloseReasons.UnexpectedCondition);
                throw new WebSocketException("Cannot write on WebSocket",ex);
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }
        public void Close(WebSocketCloseReasons reason)
        {
            try
            {
                if (Interlocked.CompareExchange(ref _closed, 1, 0) == 1)
                    return;

                if (Interlocked.CompareExchange(ref _gracefullyClosed, 1,0) == 0)
                    WriteInternal(reason.GetBytes(), 0, 2, true, false, WebSocketFrameOption.ConnectionClose, WebSocketExtensionFlags.None);

                _clientStream.Close();
            }
            catch { }
        }
        public override void Close()
        {
            this.Close(WebSocketCloseReasons.NormalClose);
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
                    this.Close(WebSocketCloseReasons.NormalClose);
                    _clientStream.Dispose();
                }
                catch { }
            }
        }
        public override void Dispose()
        {
            Dispose(true);   
        }
        ~WebSocketHandlerRfc6455()
        {
            Dispose(false);
        }
    }

}
