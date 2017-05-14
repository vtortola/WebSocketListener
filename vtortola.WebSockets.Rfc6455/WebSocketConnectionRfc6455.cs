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
        private readonly ArraySegment<byte> _headerBuffer, _pingBuffer, _pongBuffer, _controlBuffer, _keyBuffer, _maskBuffer, _closeBuffer;
        internal readonly ArraySegment<byte> SendBuffer;

        private readonly ILogger log;
        private readonly SemaphoreSlim _writeSemaphore;
        private readonly Stream _networkStream;
        private readonly WebSocketListenerOptions _options;
        private readonly PingStrategy _ping;
        private readonly bool _maskData;

        private int _ongoingMessageWrite, _ongoingMessageAwaiting, _isClosed;

        private bool _pingStarted;
        private TimeSpan _latency;

        internal bool IsConnected => _isClosed == 0;
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

        internal WebSocketConnectionRfc6455(Stream networkStream, bool maskData, WebSocketListenerOptions options)
        {
            if (networkStream == null) throw new ArgumentNullException(nameof(networkStream));
            if (options == null) throw new ArgumentNullException(nameof(options));

            const int HEADER_SEGMENT_SIZE = 16;
            const int CONTROL_SEGMENT_SIZE = 125;
            const int PONG_SEGMENT_SIZE = 125;
            const int PING_SEGMENT_SIZE = 8;
            const int SEND_HEADER_SEGMENT_SIZE = 16;
            const int KEY_SEGMENT_SIZE = 4;
            const int MASK_SEGMENT_SIZE = 4;
            const int CLOSE_SEGMENT_SIZE = 2;

            this.log = options.Logger;

            _writeSemaphore = new SemaphoreSlim(1);
            _options = options;

            _networkStream = networkStream;
            _maskData = maskData;

            var bufferSize = HEADER_SEGMENT_SIZE +
                CONTROL_SEGMENT_SIZE +
                PONG_SEGMENT_SIZE +
                PING_SEGMENT_SIZE +
                KEY_SEGMENT_SIZE +
                MASK_SEGMENT_SIZE +
                CLOSE_SEGMENT_SIZE;

            var smallBuffer = _options.BufferManager.TakeBuffer(bufferSize);
            _headerBuffer = new ArraySegment<byte>(smallBuffer, 0, HEADER_SEGMENT_SIZE);
            _controlBuffer = _headerBuffer.NextSegment(CONTROL_SEGMENT_SIZE);
            _pongBuffer = _controlBuffer.NextSegment(PONG_SEGMENT_SIZE);
            _pingBuffer = _pongBuffer.NextSegment(PING_SEGMENT_SIZE);
            _keyBuffer = _pingBuffer.NextSegment(KEY_SEGMENT_SIZE);
            _maskBuffer = _keyBuffer.NextSegment(MASK_SEGMENT_SIZE);
            _closeBuffer = _maskBuffer.NextSegment(CLOSE_SEGMENT_SIZE);


            var sendBuffer = this._options.BufferManager.TakeBuffer(this._options.SendBufferSize);
            SendBuffer = new ArraySegment<byte>(sendBuffer, 0, SEND_HEADER_SEGMENT_SIZE)
                .NextSegment(_options.SendBufferSize - SEND_HEADER_SEGMENT_SIZE);

            switch (options.PingMode)
            {
                case PingModes.BandwidthSaving:
                    _ping = new BandwidthSavingPing(this, _options.PingTimeout, _pingBuffer, this.log);
                    break;
                case PingModes.LatencyControl:
                    _ping = new LatencyControlPing(this, _options.PingTimeout, _pingBuffer, this.log);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown value '{options.PingMode}' for '{nameof(PingModes)}' enumeration.");
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
                    var buffered = 0;
                    var estimatedHeaderLength = 2;
                    // try read minimal frame first
                    while (buffered < estimatedHeaderLength)
                    {
                        var read = this._networkStream.Read(_headerBuffer.Array, _headerBuffer.Offset + buffered, estimatedHeaderLength - buffered);
                        if (read == 0)
                        {
                            buffered = 0;
                            break;
                        }

                        buffered += read;
                        if (buffered >= 2)
                            estimatedHeaderLength = WebSocketFrameHeader.GetHeaderLength(_headerBuffer.Array, _headerBuffer.Offset);
                    }

                    if (buffered == 0)
                    {
                        if (this.log.IsDebugEnabled)
                            this.log.Debug("Connection has been closed while async awaiting header.");
                        Close(WebSocketCloseReasons.ProtocolError);
                        return;
                    }

                    ParseHeader(buffered);
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
                    var buffered = 0;
                    var estimatedHeaderLength = 2;
                    // try read minimal frame first
                    while (buffered < estimatedHeaderLength && !cancellation.IsCancellationRequested)
                    {
                        var read = await this._networkStream.ReadAsync(_headerBuffer.Array, _headerBuffer.Offset + buffered, estimatedHeaderLength - buffered, cancellation).ConfigureAwait(false);
                        if (read == 0)
                        {
                            buffered = 0;
                            break;
                        }

                        buffered += read;
                        if (buffered >= 2)
                            estimatedHeaderLength = WebSocketFrameHeader.GetHeaderLength(_headerBuffer.Array, _headerBuffer.Offset);
                    }

                    if (buffered == 0 || cancellation.IsCancellationRequested)
                    {
                        if (buffered == 0)
                        {
                            if (this.log.IsDebugEnabled)
                                this.log.Debug("Connection has been closed while async awaiting header.");
                        }
                        Close(WebSocketCloseReasons.ProtocolError);
                        return;
                    }

                    ParseHeader(buffered);
                }
            }
            catch (Exception awaitHeaderError)
            {
                if (this.log.IsDebugEnabled)
                    this.log.Debug("An error occurred while async awaiting header.", awaitHeaderError);

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
        internal async Task<int> ReadInternalAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));

            var reg = cancellationToken.Register(this.Close, false);
            try
            {
                var read = await this._networkStream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
                CurrentHeader.DecodeBytes(buffer, offset, read);
                return read;
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
        internal int ReadInternal(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));

            try
            {
                var read = this._networkStream.Read(buffer, offset, count);
                CurrentHeader.DecodeBytes(buffer, offset, read);
                return read;
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
        internal void WriteInternal(ArraySegment<byte> payload, int length, bool isCompleted, bool headerSent, WebSocketMessageType type, WebSocketExtensionFlags extensionFlags)
        {
            try
            {
                var mask = default(ArraySegment<byte>);
                if (this._maskData)
                    ThreadStaticRandom.NextBytes(mask = _maskBuffer);

                var header = WebSocketFrameHeader.Create(length, isCompleted, headerSent, mask, (WebSocketFrameOption)type, extensionFlags);
                if (header.ToBytes(payload.Array, payload.Offset - header.HeaderLength) != header.HeaderLength)
                    throw new WebSocketException("Wrong frame header written.");

                if (!_writeSemaphore.Wait(_options.WebSocketSendTimeout))
                    throw new WebSocketException("Write timeout.");

                header.EncodeBytes(payload.Array, payload.Offset, length);

                this._networkStream.Write(payload.Array, payload.Offset - header.HeaderLength, length + header.HeaderLength);
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
        internal async Task WriteInternalAsync(ArraySegment<byte> payload, int length, bool isCompleted, bool headerSent, WebSocketMessageType type, WebSocketExtensionFlags extensionFlags, CancellationToken cancellation)
        {
            var reg = cancellation.Register(this.Close, false);
            try
            {
                var mask = default(ArraySegment<byte>);
                if (this._maskData)
                    ThreadStaticRandom.NextBytes(mask = _maskBuffer);

                var header = WebSocketFrameHeader.Create(length, isCompleted, headerSent, mask, (WebSocketFrameOption)type, extensionFlags);
                if (header.ToBytes(payload.Array, payload.Offset - header.HeaderLength) != header.HeaderLength)
                    throw new WebSocketException("Wrong frame header written.");

                if (!_writeSemaphore.Wait(_options.WebSocketSendTimeout))
                    throw new WebSocketException("Write timeout");

                header.EncodeBytes(payload.Array, payload.Offset, length);

                await this._networkStream.WriteAsync(payload.Array, payload.Offset - header.HeaderLength, length + header.HeaderLength, cancellation).ConfigureAwait(false);
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
        internal void Close()
        {
            this.Close(WebSocketCloseReasons.NormalClose);
        }
        private void ParseHeader(int read)
        {
            if (read < 2)
            {
                if (this.log.IsWarningEnabled)
                    this.log.Warning($"{nameof(this.ParseHeader)} is called with only {read} bytes buffer. Minimal is 2 bytes.");

                Close(WebSocketCloseReasons.ProtocolError);
                return;
            }

            int headerLength = WebSocketFrameHeader.GetHeaderLength(_headerBuffer.Array, _headerBuffer.Offset);

            if (read != headerLength)
            {
                if (this.log.IsWarningEnabled)
                    this.log.Warning($"{nameof(this.ParseHeader)} is called with {read} bytes buffer. While whole header is {headerLength} bytes length.");

                Close(WebSocketCloseReasons.ProtocolError);
                return;
            }

            WebSocketFrameHeader header;
            if (!WebSocketFrameHeader.TryParse(_headerBuffer.Array, _headerBuffer.Offset, headerLength, _keyBuffer, out header))
                throw new WebSocketException("Frame header is malformed.");

            CurrentHeader = header;

            if (!header.Flags.Option.IsData())
            {
                ProcessControlFrame(this._networkStream);
                CurrentHeader = null;
            }
            else
                _ongoingMessageAwaiting = 0;

            _ping.NotifyActivity();
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
                    int contentLength = _pongBuffer.Count;
                    if (CurrentHeader.ContentLength < 125)
                        contentLength = (int)CurrentHeader.ContentLength;
                    int readed = 0;
                    while (CurrentHeader.RemainingBytes > 0)
                    {
                        readed = clientStream.Read(_pongBuffer.Array, _pongBuffer.Offset + readed, contentLength - readed);
                        CurrentHeader.DecodeBytes(_pongBuffer.Array, _pongBuffer.Offset, readed);
                    }

                    if (CurrentHeader.Flags.Option == WebSocketFrameOption.Pong)
                        _ping.NotifyPong(_pongBuffer);
                    else // pong frames echo what was 'pinged'
                        this.WriteInternal(_pongBuffer, readed, true, false, (WebSocketMessageType)WebSocketFrameOption.Pong, WebSocketExtensionFlags.None);

                    break;
                default: throw new WebSocketException("Unexpected header option '" + CurrentHeader.Flags.Option.ToString() + "'");
            }
        }
        internal void Close(WebSocketCloseReasons reason)
        {
            try
            {
                if (Interlocked.CompareExchange(ref _isClosed, 1, 0) == 1)
                    return;

                ((ushort)reason).ToBytesBackwards(_closeBuffer.Array, _closeBuffer.Offset);
                WriteInternal(_closeBuffer, 2, true, false, (WebSocketMessageType)WebSocketFrameOption.ConnectionClose, WebSocketExtensionFlags.None);
#if (NET45 || NET451 || NET452 || NET46)
                this._networkStream.Close();
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
            SafeEnd.Dispose(_networkStream, this.log);
        }
    }

}
