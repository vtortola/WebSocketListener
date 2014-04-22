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
    internal class WebSocketConnectionRfc6455 
    {
        readonly Byte[] _buffer, _endMessageEmptyBuffer;
        readonly ArraySegment<Byte> _headerReadSegment, _headerWriteSegment, _pingSegment, _pongSegment, _controlSegment, _keySegment, _closeSegment;

        readonly SemaphoreSlim _writeSemaphore;
        readonly Stream _clientStream;
        readonly WebSocketListenerOptions _options;
        
        Int32 _closed,_ongoingMessageWrite,_ongoingMessageAwaiting, _disposed;
        readonly TimeSpan _pingInterval, _pingTimeout;
        DateTime _lastPong;
        Boolean _pingFail, _pingStarted;

        internal Boolean IsConnected { get { return _closed != 1; } }
        internal WebSocketFrameHeader CurrentHeader { get; set; }
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
                _buffer = options.BufferManager.TakeBuffer(14 + 10 + 125 + 125 + 2 + 4 + 2);
            else
                _buffer = new Byte[14 + 10 + 125 + 125 + 2 + 10 + 4 + 2];

            _endMessageEmptyBuffer = new Byte[0];
            _headerReadSegment = new ArraySegment<Byte>(_buffer, 0, 14);
            _headerWriteSegment = new ArraySegment<Byte>(_buffer, 14, 10);
            _controlSegment = new ArraySegment<Byte>(_buffer, 14 + 10, 125);
            _pongSegment = new ArraySegment<Byte>(_buffer, 14 + 10 + 125, 125);
            _pingSegment = new ArraySegment<Byte>(_buffer, 14 + 10 + 125 + 125, 2);
            _keySegment = new ArraySegment<Byte>(_buffer, 14 + 10 + 125 + 125 + 2, 4);
            _closeSegment = new ArraySegment<Byte>(_buffer, 14 + 10 + 125 + 125 + 2 + 4, 2);

            _pingTimeout = _options.PingTimeout;
            _pingInterval = TimeSpan.FromMilliseconds(Math.Min(5000, _options.PingTimeout.TotalMilliseconds / 2));
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
                    Int32 readed =  _clientStream.Read(_headerReadSegment.Array,_headerReadSegment.Offset, 6);
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
                    Int32 readed = await _clientStream.ReadAsync(_headerReadSegment.Array, _headerReadSegment.Offset, 6, cancellation);
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
            WriteInternal(_endMessageEmptyBuffer, 0, 0, true, true, WebSocketMessageType.Binary, WebSocketExtensionFlags.None);
        }
        internal Task EndWrittingAsync()
        {
            _ongoingMessageWrite = 0;
            return WriteInternalAsync(_endMessageEmptyBuffer, 0, 0, true, true, WebSocketMessageType.Binary, WebSocketExtensionFlags.None, CancellationToken.None);
        }
        internal void BeginWritting()
        {
            if (Interlocked.CompareExchange(ref _ongoingMessageWrite, 1, 0) == 1)
                throw new WebSocketException("There is an ongoing message that is being written from somewhere else. Only a single write is allowed at the time.");
        }
        internal void WriteInternal(Byte[] buffer, Int32 offset, Int32 count, Boolean isCompleted, Boolean headerSent, WebSocketMessageType type, WebSocketExtensionFlags extensionFlags)
        {
            WriteInternal(buffer, offset,count, isCompleted, headerSent, (WebSocketFrameOption)type, extensionFlags);
        }
        internal Task WriteInternalAsync(Byte[] buffer, Int32 offset, Int32 count, Boolean isCompleted, Boolean headerSent, WebSocketMessageType type, WebSocketExtensionFlags extensionFlags, CancellationToken cancellation)
        {
            return WriteInternalAsync(buffer, offset, count, isCompleted, headerSent, (WebSocketFrameOption)type, extensionFlags, cancellation);
        }
        internal void Close()
        {
            this.Close(WebSocketCloseReasons.NormalClose);
        }
        internal void Dispose()
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

            Int32 headerlength = WebSocketFrameHeader.GetHeaderLength(_headerReadSegment.Array, _headerReadSegment.Offset);

            if (!TryReadHeaderUntil(ref readed, headerlength))
            {
                Close(WebSocketCloseReasons.ProtocolError);
                return;
            }

            WebSocketFrameHeader header;
            if (!WebSocketFrameHeader.TryParse(_headerReadSegment.Array, _headerReadSegment.Offset, headerlength, _keySegment, out header))
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
                r = _clientStream.Read(_headerReadSegment.Array, _headerReadSegment.Offset + readed, until - readed);
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

                case WebSocketFrameOption.Ping: // read the buffer and echo
                case WebSocketFrameOption.Pong: // read the buffer, remember timestamp
                    Int32 contentLength = _pongSegment.Count;
                    if (CurrentHeader.ContentLength < 125)
                        contentLength = (Int32)CurrentHeader.ContentLength;
                    Int32 readed = 0;
                    while (CurrentHeader.RemainingBytes > 0)
                    {
                        readed = clientStream.Read(_pongSegment.Array, _pongSegment.Offset + readed, contentLength - readed);
                        CurrentHeader.DecodeBytes(_pongSegment.Array, _pongSegment.Offset, readed);
                    }

                    if(CurrentHeader.Flags.Option == WebSocketFrameOption.Pong)
                        _lastPong = DateTime.Now;
                    else // pong frames echo what was 'pinged'
                        this.WriteInternal(_pongSegment.Array, _pongSegment.Offset, readed, true, false, WebSocketFrameOption.Pong, WebSocketExtensionFlags.None);
                    
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
                        WriteInternal(_pingSegment.Array, _pingSegment.Offset, 0, true, false, WebSocketFrameOption.Ping, WebSocketExtensionFlags.None);
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
        private void WriteInternal(Byte[] buffer, Int32 offset, Int32 count, Boolean isCompleted, Boolean headerSent, WebSocketFrameOption option, WebSocketExtensionFlags extensionFlags)
        {
            try
            {
                var header = WebSocketFrameHeader.Create(count, isCompleted, headerSent, option, extensionFlags);
                header.ToBytes(_headerWriteSegment.Array, _headerWriteSegment.Offset);

                if (!_writeSemaphore.Wait(_options.WebSocketSendTimeout))
                    throw new WebSocketException("Write timeout");

                _clientStream.Write(_headerWriteSegment.Array, _headerWriteSegment.Offset, header.HeaderLength);
                _clientStream.Write(buffer, offset, count);

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
        private async Task WriteInternalAsync(Byte[] buffer, Int32 offset, Int32 count, Boolean isCompleted, Boolean headerSent, WebSocketFrameOption option, WebSocketExtensionFlags extensionFlags, CancellationToken cancellation)
        {
            try
            {
                var header = WebSocketFrameHeader.Create(count, isCompleted, headerSent, option, extensionFlags);
                header.ToBytes(_headerWriteSegment.Array, _headerWriteSegment.Offset);

                if (!_writeSemaphore.Wait(_options.WebSocketSendTimeout))
                    throw new WebSocketException("Write timeout");
                await _clientStream.WriteAsync(_headerWriteSegment.Array, _headerWriteSegment.Offset, header.HeaderLength, cancellation);
                await _clientStream.WriteAsync(buffer, offset, count, cancellation);

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

                ((UInt16)reason).ToBytes(_closeSegment.Array, _controlSegment.Offset);
                WriteInternal(_closeSegment.Array, _closeSegment.Offset, 2, true, false, WebSocketFrameOption.ConnectionClose, WebSocketExtensionFlags.None);

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
