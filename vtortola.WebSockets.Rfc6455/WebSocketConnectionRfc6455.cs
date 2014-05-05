using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Tools;

namespace vtortola.WebSockets.Rfc6455
{
    internal class WebSocketConnectionRfc6455 :IDisposable
    {
        readonly Byte[] _buffer;
        readonly ArraySegment<Byte> _headerBuffer, _pingBuffer, _pongBuffer, _controlBuffer, _keyBuffer, _closeBuffer;
        internal readonly ArraySegment<Byte> SendBuffer;

        readonly SemaphoreSlim _writeSemaphore;
        readonly Stream _clientStream;
        readonly WebSocketListenerOptions _options;
        
        Int32 _closed,_ongoingMessageWrite,_ongoingMessageAwaiting, _disposed;
        readonly TimeSpan _pingInterval, _pingTimeout;
        DateTime _lastPong;
        Boolean _pingFail, _pingStarted;

        internal Boolean IsConnected { get { return _closed != 1; } }
        internal WebSocketFrameHeader CurrentHeader { get; set; }
        public TimeSpan Latency { get; private set; }
        internal WebSocketConnectionRfc6455(Stream clientStream, WebSocketListenerOptions options)
        {
            if (clientStream == null)
                throw new ArgumentNullException("clientStream");

            if (options == null)
                throw new ArgumentNullException("options");

            _writeSemaphore = new SemaphoreSlim(1);
            _options = options;
            
            _clientStream = clientStream;

            if (options.BufferManager != null)
                _buffer = options.BufferManager.TakeBuffer(14 + 125 + 125 + 8 + 10 + _options.SendBufferSize + 4 + 2);
            else
                _buffer = new Byte[14 + 125 + 125 + 8 + 10 + _options.SendBufferSize  + 4 + 2];

            _headerBuffer = new ArraySegment<Byte>(_buffer, 0, 14);
            _controlBuffer = new ArraySegment<Byte>(_buffer, 14, 125);
            _pongBuffer = new ArraySegment<Byte>(_buffer, 14 + 125, 125);
            _pingBuffer = new ArraySegment<Byte>(_buffer, 14 + 125 + 125, 8);
            SendBuffer = new ArraySegment<Byte>(_buffer, 14 + 125 + 125 + 8 + 10, _options.SendBufferSize);
            _keyBuffer = new ArraySegment<Byte>(_buffer, 14 + 125 + 125 + 8 + 10 + _options.SendBufferSize, 4);
            _closeBuffer = new ArraySegment<Byte>(_buffer, 14 + 125 + 125 + 8 + 10 + _options.SendBufferSize + 4, 2);

            _pingTimeout = _options.PingTimeout;
            _pingInterval = TimeSpan.FromMilliseconds(Math.Min(1000, _options.PingTimeout.TotalMilliseconds / 3));
        }
        private void StartPing()
        {
            if (!_pingStarted)
            {
                _pingStarted = true;
                if (_options.PingTimeout != Timeout.InfiniteTimeSpan)
                {
                    _lastPong = DateTime.Now.Add(_pingTimeout);
                    Task.Run((Func<Task>)PingAsync);
                }
            }
        }
        internal void AwaitHeader()
        {
            CheckForDoubleRead();
            StartPing();
            try
            {
                while (this.IsConnected && CurrentHeader == null)
                {
                    // try read minimal frame first
                    Int32 readed =  _clientStream.Read(_headerBuffer.Array,_headerBuffer.Offset, 6);
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
        internal async Task AwaitHeaderAsync(CancellationToken cancellation)
        {
            CheckForDoubleRead();
            StartPing();
            try
            {
                while (this.IsConnected && CurrentHeader == null)
                {
                    // try read minimal frame first
                    Int32 readed = await _clientStream.ReadAsync(_headerBuffer.Array, _headerBuffer.Offset, 6, cancellation);
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
        internal async Task<Int32> ReadInternalAsync(Byte[] buffer, Int32 offset, Int32 count, CancellationToken cancellationToken)
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
        internal Int32 ReadInternal(Byte[] buffer, Int32 offset, Int32 count)
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
        internal void EndWritting()
        {
            _ongoingMessageWrite = 0;
        }
        internal void BeginWritting()
        {
            if (Interlocked.CompareExchange(ref _ongoingMessageWrite, 1, 0) == 1)
                throw new WebSocketException("There is an ongoing message that is being written from somewhere else. Only a single write is allowed at the time.");
        }
        internal void WriteInternal(ArraySegment<Byte> buffer, Int32 count, Boolean isCompleted, Boolean headerSent, WebSocketMessageType type, WebSocketExtensionFlags extensionFlags)
        {
            WriteInternal(buffer, count, isCompleted, headerSent, (WebSocketFrameOption)type, extensionFlags);
        }
        internal Task WriteInternalAsync(ArraySegment<Byte> buffer, Int32 count, Boolean isCompleted, Boolean headerSent, WebSocketMessageType type, WebSocketExtensionFlags extensionFlags, CancellationToken cancellation)
        {
            return WriteInternalAsync(buffer, count, isCompleted, headerSent, (WebSocketFrameOption)type, extensionFlags, cancellation);
        }
        internal void Close()
        {
            this.Close(WebSocketCloseReasons.NormalClose);
        }
        public void Dispose()
        {
            Dispose(true);
        }
        ~WebSocketConnectionRfc6455()
        {
            Dispose(false);
        }
        private void ParseHeader(Int32 readed)
        {
            if (!TryReadHeaderUntil(ref readed, 6))
            {
                Close(WebSocketCloseReasons.ProtocolError);
                return;
            }

            Int32 headerlength = WebSocketFrameHeader.GetHeaderLength(_headerBuffer.Array, _headerBuffer.Offset);

            if (!TryReadHeaderUntil(ref readed, headerlength))
            {
                Close(WebSocketCloseReasons.ProtocolError);
                return;
            }

            WebSocketFrameHeader header;
            if (!WebSocketFrameHeader.TryParse(_headerBuffer.Array, _headerBuffer.Offset, headerlength, _keyBuffer, out header))
                throw new WebSocketException("Cannot understand frame header");

            CurrentHeader = header;

            if (!header.Flags.Option.IsData())
            {
                ProcessControlFrame(_clientStream);
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
                r = _clientStream.Read(_headerBuffer.Array, _headerBuffer.Offset + readed, until - readed);
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

                case WebSocketFrameOption.Ping: 
                case WebSocketFrameOption.Pong: 
                    Int32 contentLength = _pongBuffer.Count;
                    if (CurrentHeader.ContentLength < 125)
                        contentLength = (Int32)CurrentHeader.ContentLength;
                    Int32 readed = 0;
                    while (CurrentHeader.RemainingBytes > 0)
                    {
                        readed = clientStream.Read(_pongBuffer.Array, _pongBuffer.Offset + readed, contentLength - readed);
                        CurrentHeader.DecodeBytes(_pongBuffer.Array, _pongBuffer.Offset, readed);
                    }

                    if (CurrentHeader.Flags.Option == WebSocketFrameOption.Pong)
                    {
                        var now = DateTime.Now;
                        _lastPong = now;
                        var timestamp = BitConverter.ToInt64(_pongBuffer.Array, _pongBuffer.Offset);
                        Latency = TimeSpan.FromTicks(now.Ticks - timestamp);
                    }
                    else // pong frames echo what was 'pinged'
                        this.WriteInternal(_pongBuffer, readed, true, false, WebSocketFrameOption.Pong, WebSocketExtensionFlags.None);
                    
                    break;
                default: throw new WebSocketException("Unexpected header option '" + CurrentHeader.Flags.Option.ToString() + "'");
            }
        }    
        private async Task PingAsync()
        {
            while (this.IsConnected)
            {
                await Task.Delay(_pingInterval).ConfigureAwait(false);

                try
                {
                    var now = DateTime.Now;

                    if (_lastPong.Add(_pingTimeout) < now)
                        Close(WebSocketCloseReasons.NormalClose);
                    else
                    {
                        ((UInt64)now.Ticks).ToBytes(_pingBuffer.Array, _pingBuffer.Offset);
                        WriteInternal(_pingBuffer, 8, true, false, WebSocketFrameOption.Ping, WebSocketExtensionFlags.None);
                    }
                }
                catch
                {
                    if (_pingFail)
                        return;
                    else
                        _pingFail = true;
                }
            }
        }
        private void WriteInternal(ArraySegment<Byte> buffer, Int32 count, Boolean isCompleted, Boolean headerSent, WebSocketFrameOption option, WebSocketExtensionFlags extensionFlags)
        {
            try
            {
                var header = WebSocketFrameHeader.Create(count, isCompleted, headerSent, option, extensionFlags);
                header.ToBytes(buffer.Array,buffer.Offset - header.HeaderLength);

                if (!_writeSemaphore.Wait(_options.WebSocketSendTimeout))
                    throw new WebSocketException("Write timeout");
                _clientStream.Write(buffer.Array, buffer.Offset - header.HeaderLength, count + header.HeaderLength);
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
        private async Task WriteInternalAsync(ArraySegment<Byte> buffer, Int32 count, Boolean isCompleted, Boolean headerSent, WebSocketFrameOption option, WebSocketExtensionFlags extensionFlags, CancellationToken cancellation)
        {
            try
            {
                var header = WebSocketFrameHeader.Create(count, isCompleted, headerSent, option, extensionFlags);
                header.ToBytes(buffer.Array, buffer.Offset - header.HeaderLength);

                if (!_writeSemaphore.Wait(_options.WebSocketSendTimeout))
                    throw new WebSocketException("Write timeout");
                await _clientStream.WriteAsync(buffer.Array, buffer.Offset - header.HeaderLength, count + header.HeaderLength, cancellation);
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
        private void Close(WebSocketCloseReasons reason)
        {
            try
            {
                if (Interlocked.CompareExchange(ref _closed, 1, 0) == 1)
                    return;

                ((UInt16)reason).ToBytes(_closeBuffer.Array, _controlBuffer.Offset);
                WriteInternal(_closeBuffer, 2, true, false, WebSocketFrameOption.ConnectionClose, WebSocketExtensionFlags.None);

                _clientStream.Close();
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
                    this.Close();
                    _writeSemaphore.Dispose();
                    _clientStream.Dispose();
                }
                catch { }
                finally
                {
                    if (_options.BufferManager != null)
                        _options.BufferManager.ReturnBuffer(_buffer);
                }
            }
        }
    }

}
