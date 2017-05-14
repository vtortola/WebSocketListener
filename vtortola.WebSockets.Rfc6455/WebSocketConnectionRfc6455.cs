using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Tools;

namespace vtortola.WebSockets.Rfc6455
{

    internal partial class WebSocketConnectionRfc6455 : IDisposable
    {
        //    readonly Byte[] _buffer;
        private readonly ArraySegment<byte> _headerBuffer, _pingBuffer, _pongBuffer, _controlBuffer, _keyBuffer, _maskBuffer, _closeBuffer;
        internal readonly ArraySegment<byte> SendBuffer;

        private readonly ILogger log;
        private readonly SemaphoreSlim _writeSemaphore;
        private readonly Stream _networkStream;
        private readonly WebSocketListenerOptions _options;
        private readonly PingHandler _ping;
        private readonly bool _maskData;

        private int _ongoingMessageWrite, _ongoingMessageAwaiting, _isClosed;

        private TimeSpan _latency;

        public bool IsConnected => _isClosed == 0;
        public WebSocketFrameHeader CurrentHeader { get; private set; }
        public TimeSpan Latency
        {
            get
            {
                if (_options.PingMode != PingMode.LatencyControl)
                    throw new InvalidOperationException("PingMode has not been set to 'LatencyControl', so latency is not available");
                return _latency;
            }
            set
            {
                if (_options.PingMode != PingMode.LatencyControl)
                    throw new InvalidOperationException("PingMode has not been set to 'LatencyControl', so latency is not available");
                _latency = value;
            }
        }

        public WebSocketConnectionRfc6455(Stream networkStream, bool maskData, WebSocketListenerOptions options)
        {
            if (networkStream == null) throw new ArgumentNullException(nameof(networkStream));
            if (options == null) throw new ArgumentNullException(nameof(options));

            const int HEADER_SEGMENT_SIZE = 16;
            const int CONTROL_SEGMENT_SIZE = 128;
            const int PONG_SEGMENT_SIZE = 128;
            const int PING_HEADER_SEGMENT_SIZE = 16;
            const int PING_SEGMENT_SIZE = 128;
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
                PING_HEADER_SEGMENT_SIZE +
                PING_SEGMENT_SIZE +
                KEY_SEGMENT_SIZE +
                MASK_SEGMENT_SIZE +
                CLOSE_SEGMENT_SIZE;

            var smallBuffer = _options.BufferManager.TakeBuffer(bufferSize);
            _headerBuffer = new ArraySegment<byte>(smallBuffer, 0, HEADER_SEGMENT_SIZE);
            _controlBuffer = _headerBuffer.NextSegment(CONTROL_SEGMENT_SIZE);
            _pongBuffer = _controlBuffer.NextSegment(PONG_SEGMENT_SIZE);
            _pingBuffer = _pongBuffer.NextSegment(PING_HEADER_SEGMENT_SIZE).NextSegment(PING_SEGMENT_SIZE);
            _keyBuffer = _pingBuffer.NextSegment(KEY_SEGMENT_SIZE);
            _maskBuffer = _keyBuffer.NextSegment(MASK_SEGMENT_SIZE);
            _closeBuffer = _maskBuffer.NextSegment(CLOSE_SEGMENT_SIZE);


            var sendBuffer = this._options.BufferManager.TakeBuffer(this._options.SendBufferSize);
            SendBuffer = new ArraySegment<byte>(sendBuffer, 0, SEND_HEADER_SEGMENT_SIZE)
                .NextSegment(_options.SendBufferSize - SEND_HEADER_SEGMENT_SIZE);

            switch (options.PingMode)
            {
                case PingMode.BandwidthSaving:
                    _ping = new BandwidthSavingPing(this);
                    break;
                case PingMode.LatencyControl:
                    _ping = new LatencyControlPing(this);
                    break;
                case PingMode.Manual:
                    this._ping = new ManualPing(this);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown value '{options.PingMode}' for '{nameof(PingMode)}' enumeration.");
            }
        }

        public Task PingAsync(byte[] data, int offset, int count)
        {
            if (_ping is ManualPing)
            {
                if (data != null)
                {
                    if (offset < 0 || offset > data.Length) throw new ArgumentOutOfRangeException(nameof(offset));
                    if (count < 0 || count > 125 || offset + count > data.Length) throw new ArgumentOutOfRangeException(nameof(count));

                    _pingBuffer.Array[_pingBuffer.Offset] = (byte)count;
                    Buffer.BlockCopy(data, offset, _pingBuffer.Array, _pingBuffer.Offset + 1, count);
                }
                else
                {
                    _pingBuffer.Array[_pingBuffer.Offset] = 0;
                }
            }

            return _ping.PingAsync();
        }
        public void AwaitHeader()
        {
            CheckForDoubleRead();
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
        public async Task AwaitHeaderAsync(CancellationToken cancellation)
        {
            CheckForDoubleRead();
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
        public void DisposeCurrentHeaderIfFinished()
        {
            if (CurrentHeader != null && CurrentHeader.RemainingBytes == 0)
                CurrentHeader = null;
        }
        public async Task<int> ReadInternalAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
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
        public int ReadInternal(byte[] buffer, int offset, int count)
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
        public void EndWriting()
        {
            _ongoingMessageWrite = 0;
        }
        public void BeginWriting()
        {
            if (Interlocked.CompareExchange(ref _ongoingMessageWrite, 1, 0) == 1)
                throw new WebSocketException("There is an ongoing message that is being written from somewhere else. Only a single write is allowed at the time.");
        }

        public ArraySegment<byte> PrepareFrame(ArraySegment<byte> payload, int length, bool isCompleted, bool headerSent, WebSocketMessageType type, WebSocketExtensionFlags extensionFlags)
        {
            var mask = default(ArraySegment<byte>);
            if (this._maskData)
                ThreadStaticRandom.NextBytes(mask = _maskBuffer);

            var header = WebSocketFrameHeader.Create(length, isCompleted, headerSent, mask, (WebSocketFrameOption)type, extensionFlags);
            if (header.ToBytes(payload.Array, payload.Offset - header.HeaderLength) != header.HeaderLength)
                throw new WebSocketException("Wrong frame header written.");

            header.EncodeBytes(payload.Array, payload.Offset, length);

            return new ArraySegment<byte>(payload.Array, payload.Offset - header.HeaderLength, length + header.HeaderLength);
        }
        public async Task SendFrameAsync(ArraySegment<byte> frame, CancellationToken cancellation)
        {
            using (cancellation.CanBeCanceled ? cancellation.Register(this.Close, false) : default(CancellationTokenRegistration))
            {
                try
                {
                    var lockTaken = await _writeSemaphore.WaitAsync(_options.WebSocketSendTimeout, cancellation).ConfigureAwait(false);
                    try
                    {
                        if (!lockTaken)
                            throw new WebSocketException("Write timeout.");

                        await this._networkStream.WriteAsync(frame.Array, frame.Offset, frame.Count, cancellation).ConfigureAwait(false);
                    }
                    finally
                    {
                        if (_isClosed == 0 && lockTaken)
                            SafeEnd.ReleaseSemaphore(_writeSemaphore, this.log);
                    }
                }
                catch (Exception writeError)
                {
                    if (this.log.IsDebugEnabled)
                        this.log.Debug("An error occurred while async writing to WebSocket.", writeError);

                    Close(WebSocketCloseReasons.UnexpectedCondition);

                    if (writeError is IOException == false && writeError is InvalidOperationException == false)
                        throw new WebSocketException("Cannot write on WebSocket", writeError);
                }
            }
        }
        public async Task<bool> TrySendFrameAsync(ArraySegment<byte> frame, TimeSpan timeout, CancellationToken cancellation)
        {
            using (cancellation.CanBeCanceled ? cancellation.Register(this.Close, false) : default(CancellationTokenRegistration))
            {
                try
                {
                    var lockTaken = await _writeSemaphore.WaitAsync(timeout, cancellation).ConfigureAwait(false);
                    try
                    {
                        if (!lockTaken)
                            return false;

                        await this._networkStream.WriteAsync(frame.Array, frame.Offset, frame.Count, cancellation).ConfigureAwait(false);
                    }
                    finally
                    {
                        if (_isClosed == 0 && lockTaken)
                            SafeEnd.ReleaseSemaphore(_writeSemaphore, this.log);
                    }
                }
                catch (Exception writeError)
                {
                    if (this.log.IsDebugEnabled)
                        this.log.Debug("An error occurred while async writing to WebSocket.", writeError);

                    Close(WebSocketCloseReasons.UnexpectedCondition);

                    if (writeError is IOException == false && writeError is InvalidOperationException == false)
                        throw new WebSocketException("Cannot write on WebSocket", writeError);
                }
            }
            return true;
        }
        public void SendFrame(ArraySegment<byte> frame)
        {
            try
            {
                var lockTaken = _writeSemaphore.Wait(_options.WebSocketSendTimeout);
                try
                {
                    if (!lockTaken)
                        throw new WebSocketException("Write timeout.");

                    this._networkStream.Write(frame.Array, frame.Offset, frame.Count);
                }
                finally
                {
                    if (_isClosed == 0 && lockTaken)
                        SafeEnd.ReleaseSemaphore(_writeSemaphore, this.log);
                }
            }
            catch (Exception writeError)
            {
                if (this.log.IsDebugEnabled)
                    this.log.Debug("An error occurred while async writing to WebSocket.", writeError);

                Close(WebSocketCloseReasons.UnexpectedCondition);

                if (writeError is IOException == false && writeError is InvalidOperationException == false)
                    throw new WebSocketException("Cannot write on WebSocket", writeError);
            }
        }

        public void Close()
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
                    var contentLength = _pongBuffer.Count;
                    if (CurrentHeader.ContentLength < 125)
                        contentLength = (int)CurrentHeader.ContentLength;

                    int read = 0;
                    while (CurrentHeader.RemainingBytes > 0)
                    {
                        read = clientStream.Read(_pongBuffer.Array, _pongBuffer.Offset + read, contentLength - read);
                        CurrentHeader.DecodeBytes(_pongBuffer.Array, _pongBuffer.Offset, read);
                    }

                    if (CurrentHeader.Flags.Option == WebSocketFrameOption.Pong)
                        _ping.NotifyPong(_pongBuffer);
                    else // pong frames echo what was 'pinged'
                    {
                        var frame = this.PrepareFrame(_pongBuffer, read, true, false, (WebSocketMessageType)WebSocketFrameOption.Pong, WebSocketExtensionFlags.None);
                        this.SendFrame(frame);
                    }

                    break;
                default: throw new WebSocketException("Unexpected header option '" + CurrentHeader.Flags.Option.ToString() + "'");
            }
        }

        public async Task CloseAsync(WebSocketCloseReasons reason)
        {
            try
            {
                if (Interlocked.CompareExchange(ref _isClosed, 1, 0) == 1)
                    return;

                using (this._networkStream)
                {
                    ((ushort)reason).ToBytesBackwards(_closeBuffer.Array, _closeBuffer.Offset);
                    var messageType = (WebSocketMessageType)WebSocketFrameOption.ConnectionClose;
                    var closeFrame = this.PrepareFrame(_closeBuffer, 2, true, false, messageType, WebSocketExtensionFlags.None);
                    await this.SendFrameAsync(closeFrame, CancellationToken.None).ConfigureAwait(false);
#if (NET45 || NET451 || NET452 || NET46)
                    this._networkStream.Close();
#endif
                }
            }
            catch (Exception closeError)
            {
                if (this.log.IsDebugEnabled)
                    this.log.Debug("An error occurred while closing connection.", closeError);
            }
        }
        public void Close(WebSocketCloseReasons reason)
        {
            try
            {
                if (Interlocked.CompareExchange(ref _isClosed, 1, 0) == 1)
                    return;

                using (_networkStream)
                {

                    ((ushort)reason).ToBytesBackwards(_closeBuffer.Array, _closeBuffer.Offset);
                    var closeFrame = this.PrepareFrame(_closeBuffer, 2, true, false, (WebSocketMessageType)WebSocketFrameOption.ConnectionClose, WebSocketExtensionFlags.None);
                    this.SendFrame(closeFrame);
#if (NET45 || NET451 || NET452 || NET46)
                    this._networkStream.Close();
#endif
                }

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
