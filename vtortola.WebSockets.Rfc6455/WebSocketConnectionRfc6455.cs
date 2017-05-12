using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Tools;

namespace vtortola.WebSockets.Rfc6455
{
    internal class WebSocketConnectionRfc6455 : IDisposable
    {
        //    readonly Byte[] _buffer;
        readonly ArraySegment<Byte> _headerBuffer, _pingBuffer, _pongBuffer, _controlBuffer, _keyBuffer, _closeBuffer;
        internal readonly ArraySegment<Byte> SendBuffer;

        private readonly ILogger log;
        readonly SemaphoreSlim _writeSemaphore;
        readonly Stream _clientStream;
        readonly WebSocketListenerOptions _options;
        readonly PingStrategy _ping;

        Int32 _ongoingMessageWrite, _ongoingMessageAwaiting, _isClosed;

        Boolean _pingStarted;
        TimeSpan _latency;

        internal Boolean IsConnected { get { return _isClosed == 0; } }
        internal WebSocketFrameHeader CurrentHeader { get; private set; }
        internal TimeSpan Latency
        {
            get
            {
                if (_options.PingMode != PingModes.LatencyControl)
                    throw new InvalidOperationException("PingMode has not been set to 'LatencyControl', so latency is not available");
                return _latency;
            }
            set
            {
                if (_options.PingMode != PingModes.LatencyControl)
                    throw new InvalidOperationException("PingMode has not been set to 'LatencyControl', so latency is not available");
                _latency = value;
            }
        }

        internal WebSocketConnectionRfc6455(Stream clientStream, WebSocketListenerOptions options)
        {
            if (clientStream == null) throw new ArgumentNullException(nameof(clientStream));
            if (options == null) throw new ArgumentNullException(nameof(options));

            const int HEADER_SEGMENT_SIZE = 14;
            const int CONTROL_SEGMENT_SIZE = 125;
            const int PONG_SEGMENT_SIZE = 125;
            const int PING_SEGMENT_SIZE = 8;
            const int KEY_SEGMENT_SIZE = 4;
            const int CLOSE_SEGMENT_SIZE = 2;

            this.log = options.Logger;

            _writeSemaphore = new SemaphoreSlim(1);
            _options = options;

            _clientStream = clientStream;

            var smallBufferSize = HEADER_SEGMENT_SIZE +
                CONTROL_SEGMENT_SIZE +
                PONG_SEGMENT_SIZE +
                PING_SEGMENT_SIZE +
                KEY_SEGMENT_SIZE +
                CLOSE_SEGMENT_SIZE;

            var smallBuffer = _options.BufferManager.TakeBuffer(smallBufferSize);
            _headerBuffer = new ArraySegment<Byte>(smallBuffer, 0, HEADER_SEGMENT_SIZE);
            _controlBuffer = _headerBuffer.NextSegment(CONTROL_SEGMENT_SIZE);
            _pongBuffer = _controlBuffer.NextSegment(PONG_SEGMENT_SIZE);
            _pingBuffer = _pongBuffer.NextSegment(PING_SEGMENT_SIZE);
            _keyBuffer = _pingBuffer.NextSegment(KEY_SEGMENT_SIZE);
            _closeBuffer = _keyBuffer.NextSegment(CLOSE_SEGMENT_SIZE);

            var sendBuffer = _options.BufferManager.TakeBuffer(this._options.SendBufferSize);
            SendBuffer = new ArraySegment<Byte>(sendBuffer, 10, _options.SendBufferSize - 10);


            switch (options.PingMode)
            {
                case PingModes.BandwidthSaving:
                    _ping = new BandwidthSavingPing(this, _options.PingTimeout, _pingBuffer, this.log);
                    break;
                case PingModes.LatencyControl:
                default:
                    _ping = new LatencyControlPing(this, _options.PingTimeout, _pingBuffer, this.log);
                    break;
            }
        }
        private void StartPing()
        {
            if (!_pingStarted)
            {
                _pingStarted = true;
                if (_options.PingTimeout != Timeout.InfiniteTimeSpan)
                {
                    Task.Run((Func<Task>)_ping.StartPing);
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
                    Int32 readed = _clientStream.Read(_headerBuffer.Array, _headerBuffer.Offset, 6);
                    if (readed == 0)
                    {
                        Close(WebSocketCloseReasons.ProtocolError);
                        return;
                    }

                    ParseHeader(readed);
                }
            }
            catch (Exception awaitHeaderError)
            {
                if (this.log.IsDebugEnabled)
                    this.log.Debug("An error occurred while sync awaiting header from WebSocket.", awaitHeaderError);

                Close(WebSocketCloseReasons.ProtocolError);

                if (awaitHeaderError is IOException == false && awaitHeaderError is InvalidOperationException == false)
                    throw;
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
                    Int32 readed = await _clientStream.ReadAsync(_headerBuffer.Array, _headerBuffer.Offset, 6, cancellation).ConfigureAwait(false);
                    if (readed == 0 || cancellation.IsCancellationRequested)
                    {
                        Close(WebSocketCloseReasons.ProtocolError);
                        return;
                    }

                    ParseHeader(readed);
                }
            }
            catch (Exception awaitHeaderError)
            {
                if (this.log.IsDebugEnabled)
                    this.log.Debug("An error occurred while async awaiting header from WebSocket.", awaitHeaderError);

                Close(WebSocketCloseReasons.ProtocolError);

                if (awaitHeaderError is IOException == false && awaitHeaderError is InvalidOperationException == false)
                    throw;
            }
        }
        internal void DisposeCurrentHeaderIfFinished()
        {
            if (CurrentHeader != null && CurrentHeader.RemainingBytes == 0)
                CurrentHeader = null;
        }
        internal async Task<Int32> ReadInternalAsync(Byte[] buffer, Int32 offset, Int32 count, CancellationToken cancellationToken)
        {
            var reg = cancellationToken.Register(this.Close, false);
            try
            {
                var readed = await _clientStream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
                CurrentHeader.DecodeBytes(buffer, offset, readed);
                return readed;
            }
            catch (Exception readError) when (readError is IOException || readError is InvalidOperationException)
            {
                if (this.log.IsDebugEnabled)
                    this.log.Debug("An error occurred while async reading from WebSocket.", readError);

                this.Close(WebSocketCloseReasons.UnexpectedCondition);
                return 0;
            }
            finally
            {
                reg.Dispose();
            }
        }
        internal Int32 ReadInternal(Byte[] buffer, Int32 offset, Int32 count)
        {
            try
            {
                var readed = _clientStream.Read(buffer, offset, count);
                CurrentHeader.DecodeBytes(buffer, offset, readed);
                return readed;
            }
            catch (Exception readError) when (readError is IOException || readError is InvalidOperationException)
            {
                if (this.log.IsDebugEnabled)
                    this.log.Debug("An error occurred while sync reading from WebSocket.", readError);

                this.Close(WebSocketCloseReasons.UnexpectedCondition);
                return 0;
            }
        }
        internal void EndWriting()
        {
            _ongoingMessageWrite = 0;
        }
        internal void BeginWriting()
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

            _ping.NotifyActivity();
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
                        _ping.NotifyPong(_pongBuffer);
                    else // pong frames echo what was 'pinged'
                        this.WriteInternal(_pongBuffer, readed, true, false, WebSocketFrameOption.Pong, WebSocketExtensionFlags.None);

                    break;
                default: throw new WebSocketException("Unexpected header option '" + CurrentHeader.Flags.Option.ToString() + "'");
            }
        }
        internal void WriteInternal(ArraySegment<Byte> buffer, Int32 count, Boolean isCompleted, Boolean headerSent, WebSocketFrameOption option, WebSocketExtensionFlags extensionFlags)
        {
            try
            {
                var header = WebSocketFrameHeader.Create(count, isCompleted, headerSent, option, extensionFlags);
                header.ToBytes(buffer.Array, buffer.Offset - header.HeaderLength);

                if (!_writeSemaphore.Wait(_options.WebSocketSendTimeout))
                    throw new WebSocketException("Write timeout");
                _clientStream.Write(buffer.Array, buffer.Offset - header.HeaderLength, count + header.HeaderLength);
            }
            catch (Exception writeError)
            {
                if (this.log.IsDebugEnabled)
                    this.log.Debug("An error occurred while sync writing to WebSocket.", writeError);

                Close(WebSocketCloseReasons.UnexpectedCondition);

                if (writeError is IOException == false && writeError is InvalidOperationException == false)
                    throw new WebSocketException("Cannot write on WebSocket", writeError);
            }
            finally
            {
                if (_isClosed == 0)
                    SafeEnd.ReleaseSemaphore(_writeSemaphore, this.log);
            }
        }
        private async Task WriteInternalAsync(ArraySegment<Byte> buffer, Int32 count, Boolean isCompleted, Boolean headerSent, WebSocketFrameOption option, WebSocketExtensionFlags extensionFlags, CancellationToken cancellation)
        {
            CancellationTokenRegistration reg = cancellation.Register(this.Close, false);
            try
            {
                var header = WebSocketFrameHeader.Create(count, isCompleted, headerSent, option, extensionFlags);
                header.ToBytes(buffer.Array, buffer.Offset - header.HeaderLength);

                if (!_writeSemaphore.Wait(_options.WebSocketSendTimeout))
                    throw new WebSocketException("Write timeout");
                await _clientStream.WriteAsync(buffer.Array, buffer.Offset - header.HeaderLength, count + header.HeaderLength, cancellation).ConfigureAwait(false);
            }
            catch (Exception writeError)
            {
                if (this.log.IsDebugEnabled)
                    this.log.Debug("An error occurred while async writing to WebSocket.", writeError);

                Close(WebSocketCloseReasons.UnexpectedCondition);

                if (writeError is IOException == false && writeError is InvalidOperationException == false)
                    throw new WebSocketException("Cannot write on WebSocket", writeError);
            }
            finally
            {
                reg.Dispose();
                if (_isClosed == 0)
                    SafeEnd.ReleaseSemaphore(_writeSemaphore, this.log);
            }
        }
        internal void Close(WebSocketCloseReasons reason)
        {
            try
            {
                if (Interlocked.CompareExchange(ref _isClosed, 1, 0) == 1)
                    return;

                ((UInt16)reason).ToBytesBackwards(_closeBuffer.Array, _closeBuffer.Offset);
                WriteInternal(_closeBuffer, 2, true, false, WebSocketFrameOption.ConnectionClose, WebSocketExtensionFlags.None);
#if (NET45 || NET451 || NET452 || NET46)
                _clientStream.Close();
#endif
            }
            catch (Exception closeError)
            {
                if (this.log.IsDebugEnabled)
                    this.log.Debug("An error occurred while closing connection.", closeError);
            }
        }

        public void Dispose()
        {
            try
            {
                this.Close();
            }
            catch (Exception closeError)
            {
                if (this.log.IsDebugEnabled)
                    this.log.Debug("An error occurred while closing connection.", closeError);
            }
            finally
            {
                _options.BufferManager.ReturnBuffer(this.SendBuffer.Array);
                _options.BufferManager.ReturnBuffer(this._closeBuffer.Array);
            }
            SafeEnd.Dispose(_writeSemaphore, this.log);
            SafeEnd.Dispose(_clientStream, this.log);
        }
    }

}
