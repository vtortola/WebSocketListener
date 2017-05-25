using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Tools;
using vtortola.WebSockets.Transports;

namespace vtortola.WebSockets.Rfc6455
{

    internal partial class WebSocketConnectionRfc6455 : IDisposable
    {
        [Flags]
        internal enum SendOptions { None, NoLock = 0x1, NoErrors = 0x2, IgnoreClose = 0x4 }

        private const int CLOSE_STATE_OPEN = 0;
        private const int CLOSE_STATE_CLOSED = 1;
        private const int CLOSE_STATE_DISPOSED = 2;

        //    readonly Byte[] _buffer;
        private readonly ArraySegment<byte> headerBuffer, pingBuffer, pongBuffer, controlBuffer, keyBuffer, maskBuffer, closeBuffer;
        internal readonly ArraySegment<byte> SendBuffer;

        private readonly ILogger log;
        private readonly SemaphoreSlim writeSemaphore;
        private readonly NetworkConnection networkConnection;
        private readonly WebSocketListenerOptions options;
        private readonly PingHandler pingHandler;
        private readonly bool maskData;
        public volatile WebSocketFrameHeader CurrentHeader;
        private volatile int ongoingMessageWrite, ongoingMessageAwaiting, closeState;
        private TimeSpan latency;

        public ILogger Log => this.log;
        public bool IsConnected => this.closeState == CLOSE_STATE_OPEN;
        public bool IsClosed => this.closeState >= CLOSE_STATE_CLOSED;
        public TimeSpan Latency
        {
            get
            {
                if (this.options.PingMode != PingMode.LatencyControl)
                    throw new InvalidOperationException("PingMode has not been set to 'LatencyControl', so latency is not available");
                return this.latency;
            }
        }

        public WebSocketConnectionRfc6455(NetworkConnection networkConnection, bool maskData, WebSocketListenerOptions options)
        {
            if (networkConnection == null) throw new ArgumentNullException(nameof(networkConnection));
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

            this.writeSemaphore = new SemaphoreSlim(1);
            this.options = options;

            this.networkConnection = networkConnection;
            this.maskData = maskData;

            var bufferSize = HEADER_SEGMENT_SIZE +
                CONTROL_SEGMENT_SIZE +
                PONG_SEGMENT_SIZE +
                PING_HEADER_SEGMENT_SIZE +
                PING_SEGMENT_SIZE +
                KEY_SEGMENT_SIZE +
                MASK_SEGMENT_SIZE +
                CLOSE_SEGMENT_SIZE;

            var smallBuffer = this.options.BufferManager.TakeBuffer(bufferSize);
            this.headerBuffer = new ArraySegment<byte>(smallBuffer, 0, HEADER_SEGMENT_SIZE);
            this.controlBuffer = this.headerBuffer.NextSegment(CONTROL_SEGMENT_SIZE);
            this.pongBuffer = this.controlBuffer.NextSegment(PONG_SEGMENT_SIZE);
            this.pingBuffer = this.pongBuffer.NextSegment(PING_HEADER_SEGMENT_SIZE).NextSegment(PING_SEGMENT_SIZE);
            this.keyBuffer = this.pingBuffer.NextSegment(KEY_SEGMENT_SIZE);
            this.maskBuffer = this.keyBuffer.NextSegment(MASK_SEGMENT_SIZE);
            this.closeBuffer = this.maskBuffer.NextSegment(CLOSE_SEGMENT_SIZE);


            var sendBuffer = this.options.BufferManager.TakeBuffer(this.options.SendBufferSize);
            SendBuffer = new ArraySegment<byte>(sendBuffer, 0, SEND_HEADER_SEGMENT_SIZE)
                .NextSegment(this.options.SendBufferSize - SEND_HEADER_SEGMENT_SIZE);

            switch (options.PingMode)
            {
                case PingMode.BandwidthSaving:
                    this.pingHandler = new BandwidthSavingPing(this);
                    break;
                case PingMode.LatencyControl:
                    this.pingHandler = new LatencyControlPing(this);
                    break;
                case PingMode.Manual:
                    this.pingHandler = new ManualPing(this);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown value '{options.PingMode}' for '{nameof(PingMode)}' enumeration.");
            }
        }

        private void CheckForDoubleRead()
        {
            if (Interlocked.CompareExchange(ref this.ongoingMessageAwaiting, 1, 0) == 1)
                throw new WebSocketException("There is an ongoing message await from somewhere else. Only a single write is allowed at the time.");

            if (CurrentHeader != null)
                throw new WebSocketException("There is an ongoing message that is being readed from somewhere else");
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
                        var read = await this.networkConnection.ReadAsync(this.headerBuffer.Array, this.headerBuffer.Offset + buffered, estimatedHeaderLength - buffered, cancellation).ConfigureAwait(false);
                        if (read == 0)
                        {
                            buffered = 0;
                            break;
                        }

                        buffered += read;
                        if (buffered >= 2)
                            estimatedHeaderLength = WebSocketFrameHeader.GetHeaderLength(this.headerBuffer.Array, this.headerBuffer.Offset);
                    }

                    if (buffered == 0 || cancellation.IsCancellationRequested)
                    {
                        if (buffered == 0)
                        {
                            if (this.log.IsDebugEnabled)
                                this.log.Debug($"({this.GetHashCode():X}) Connection has been closed while async awaiting header.");
                        }
                        await this.CloseAsync(WebSocketCloseReasons.ProtocolError).ConfigureAwait(false);
                        return;
                    }

                    await this.ParseHeaderAsync(buffered).ConfigureAwait(false);
                }
            }
            catch (Exception awaitHeaderError) when (awaitHeaderError.Unwrap() is ThreadAbortException == false)
            {
                if (!this.IsConnected)
                    return;

                var awaitHeaderErrorUnwrap = awaitHeaderError.Unwrap();
                if (this.log.IsDebugEnabled && awaitHeaderErrorUnwrap is OperationCanceledException == false && this.IsConnected)
                    this.log.Debug($"({this.GetHashCode():X}) An error occurred while async awaiting header.", awaitHeaderErrorUnwrap);

                if (this.IsConnected)
                    await this.CloseAsync(WebSocketCloseReasons.ProtocolError).ConfigureAwait(false);

                if (awaitHeaderErrorUnwrap is WebSocketException == false && awaitHeaderErrorUnwrap is OperationCanceledException == false)
                    throw new WebSocketException("Read operation on WebSocket stream is failed. More detailed information in inner exception.", awaitHeaderErrorUnwrap);
                else
                    throw;
            }
        }
        public void DisposeCurrentHeaderIfFinished()
        {
            if (CurrentHeader != null && CurrentHeader.RemainingBytes == 0)
                CurrentHeader = null;
        }
        public async Task<int> ReceiveAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));

            try
            {
                var read = await this.networkConnection.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
                CurrentHeader.DecodeBytes(buffer, offset, read);
                return read;
            }
            catch (Exception readError) when (readError.Unwrap() is ThreadAbortException == false)
            {
                var readErrorUnwrap = readError.Unwrap();
                if (this.log.IsDebugEnabled && readErrorUnwrap is OperationCanceledException == false && this.IsConnected)
                    this.log.Debug($"({this.GetHashCode():X}) An error occurred while async reading from WebSocket.", readErrorUnwrap);

                if (this.IsConnected)
                    await this.CloseAsync(WebSocketCloseReasons.UnexpectedCondition).ConfigureAwait(false);

                if (readErrorUnwrap is WebSocketException == false && readErrorUnwrap is OperationCanceledException == false)
                    throw new WebSocketException("Read operation on WebSocket stream is failed: " + readErrorUnwrap.Message, readErrorUnwrap);
                else
                    throw;
            }
        }

        public void EndWriting()
        {
            this.ongoingMessageWrite = 0;
        }
        public void BeginWriting()
        {
            if (Interlocked.CompareExchange(ref this.ongoingMessageWrite, 1, 0) == 1)
                throw new WebSocketException("There is an ongoing message that is being written from somewhere else. Only a single write is allowed at the time.");
        }

        public ArraySegment<byte> PrepareFrame(ArraySegment<byte> payload, int length, bool isCompleted, bool headerSent, WebSocketMessageType type, WebSocketExtensionFlags extensionFlags)
        {
            var mask = default(ArraySegment<byte>);
            if (this.maskData)
                ThreadStaticRandom.NextBytes(mask = this.maskBuffer);

            var header = WebSocketFrameHeader.Create(length, isCompleted, headerSent, mask, (WebSocketFrameOption)type, extensionFlags);
            if (header.ToBytes(payload.Array, payload.Offset - header.HeaderLength) != header.HeaderLength)
                throw new WebSocketException("Wrong frame header written.");

            if (this.log.IsDebugEnabled)
                this.log.Debug($"({this.GetHashCode():X}) [FRAME->] {header}");

            header.EncodeBytes(payload.Array, payload.Offset, length);

            return new ArraySegment<byte>(payload.Array, payload.Offset - header.HeaderLength, length + header.HeaderLength);
        }
        public Task<bool> SendFrameAsync(ArraySegment<byte> frame, CancellationToken cancellation)
        {
            return this.SendFrameAsync(frame, Timeout.InfiniteTimeSpan, SendOptions.None, cancellation);
        }
        public async Task<bool> SendFrameAsync(ArraySegment<byte> frame, TimeSpan timeout, SendOptions sendOptions, CancellationToken cancellation)
        {
            try
            {
                var noLock = (sendOptions & SendOptions.NoLock) == SendOptions.NoLock;
                var noError = (sendOptions & SendOptions.NoErrors) == SendOptions.NoErrors;
                var ignoreClose = (sendOptions & SendOptions.IgnoreClose) == SendOptions.IgnoreClose;

                if (!ignoreClose && this.IsClosed)
                {
                    if (noError)
                        return false;
                    else
                        throw new WebSocketException("WebSocket connection is closed.");
                }

                var lockTaken = noLock || await this.writeSemaphore.WaitAsync(timeout, cancellation).ConfigureAwait(false);
                try
                {
                    if (!lockTaken)
                    {
                        if (noError)
                            return false;
                        else
                            throw new WebSocketException($"Write operation lock timeout ({timeout.TotalMilliseconds:F2}ms).");
                    }

                    await this.networkConnection.WriteAsync(frame.Array, frame.Offset, frame.Count, cancellation).ConfigureAwait(false);

                    return true;
                }
                finally
                {
                    if (lockTaken && !noLock)
                        SafeEnd.ReleaseSemaphore(this.writeSemaphore, this.log);
                }
            }
            catch (Exception writeError) when (writeError.Unwrap() is ThreadAbortException == false)
            {
                var writeErrorUnwrap = writeError.Unwrap();
                if (this.log.IsDebugEnabled && writeErrorUnwrap is OperationCanceledException == false && this.IsConnected)
                    this.log.Debug($"({this.GetHashCode():X}) Write operation on WebSocket stream is failed.", writeErrorUnwrap);

                if (this.IsConnected)
                    await this.CloseAsync(WebSocketCloseReasons.UnexpectedCondition).ConfigureAwait(false);

                if (writeErrorUnwrap is WebSocketException == false && writeErrorUnwrap is OperationCanceledException == false)
                    throw new WebSocketException("Write operation on WebSocket stream is failed: " + writeErrorUnwrap.Message, writeErrorUnwrap);
                else
                    throw;
            }
        }

        private async Task ParseHeaderAsync(int read)
        {
            if (read < 2)
            {
                if (this.log.IsWarningEnabled)
                    this.log.Warning($"{nameof(this.ParseHeaderAsync)} is called with only {read} bytes buffer. Minimal is 2 bytes.");

                await this.CloseAsync(WebSocketCloseReasons.ProtocolError).ConfigureAwait(false);
                return;
            }

            int headerLength = WebSocketFrameHeader.GetHeaderLength(this.headerBuffer.Array, this.headerBuffer.Offset);

            if (read != headerLength)
            {
                if (this.log.IsWarningEnabled)
                    this.log.Warning($"{nameof(this.ParseHeaderAsync)} is called with {read} bytes buffer. While whole header is {headerLength} bytes length.");

                await this.CloseAsync(WebSocketCloseReasons.ProtocolError).ConfigureAwait(false);
                return;
            }

            WebSocketFrameHeader header;
            if (!WebSocketFrameHeader.TryParse(this.headerBuffer.Array, this.headerBuffer.Offset, headerLength, this.keyBuffer, out header))
                throw new WebSocketException("Frame header is malformed.");

            if (this.log.IsDebugEnabled)
                this.log.Debug($"({this.GetHashCode():X}) [FRAME<-] {header}");


            CurrentHeader = header;

            if (!header.Flags.Option.IsData())
            {
                await ProcessControlFrameAsync().ConfigureAwait(false);
                CurrentHeader = null;
            }
            else
                this.ongoingMessageAwaiting = 0;

            try
            {
                this.pingHandler.NotifyActivity();
            }
            catch (Exception notifyPingError)
            {
                if (this.log.IsWarningEnabled)
                    this.log.Warning($"({this.GetHashCode():X}) An error occurred while trying to call {this.pingHandler.GetType().Name}.{nameof(this.pingHandler.NotifyActivity)}() method.", notifyPingError);
            }
        }
        private async Task ProcessControlFrameAsync()
        {
            switch (CurrentHeader.Flags.Option)
            {
                case WebSocketFrameOption.Continuation:
                case WebSocketFrameOption.Text:
                case WebSocketFrameOption.Binary:
                    throw new WebSocketException("Text, Continuation or Binary are not protocol frames");

                case WebSocketFrameOption.ConnectionClose:
                    Interlocked.CompareExchange(ref this.closeState, CLOSE_STATE_CLOSED, CLOSE_STATE_OPEN);
                    this.Dispose();
                    break;

                case WebSocketFrameOption.Ping:
                case WebSocketFrameOption.Pong:
                    var contentLength = this.pongBuffer.Count;
                    if (CurrentHeader.ContentLength < 125)
                        contentLength = (int)CurrentHeader.ContentLength;

                    var read = 0;
                    while (CurrentHeader.RemainingBytes > 0)
                    {
                        read = await this.networkConnection.ReadAsync(this.pongBuffer.Array, this.pongBuffer.Offset + read, contentLength - read, CancellationToken.None).ConfigureAwait(false);
                        CurrentHeader.DecodeBytes(this.pongBuffer.Array, this.pongBuffer.Offset, read);
                    }

                    if (CurrentHeader.Flags.Option == WebSocketFrameOption.Pong)
                    {
                        try
                        {
                            this.pingHandler.NotifyPong(this.pongBuffer);
                        }
                        catch (Exception notifyPong)
                        {
                            if (this.log.IsWarningEnabled)
                                this.log.Warning($"({this.GetHashCode():X}) An error occurred while trying to call {this.pingHandler.GetType().Name}.{nameof(this.pingHandler.NotifyPong)}() method.", notifyPong);
                        }
                    }
                    else // pong frames echo what was 'pinged'
                    {
                        var frame = this.PrepareFrame(this.pongBuffer, read, true, false, (WebSocketMessageType)WebSocketFrameOption.Pong, WebSocketExtensionFlags.None);
                        await this.SendFrameAsync(frame, CancellationToken.None).ConfigureAwait(false);
                    }

                    break;
                default:
                    throw new WebSocketException("Unexpected header option '" + CurrentHeader.Flags.Option.ToString() + "'");
            }
        }

        public async Task CloseAsync(WebSocketCloseReasons reason)
        {
            if (Interlocked.CompareExchange(ref this.closeState, CLOSE_STATE_CLOSED, CLOSE_STATE_OPEN) != CLOSE_STATE_OPEN)
                return;

            await this.writeSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {

                ((ushort)reason).ToBytesBackwards(this.closeBuffer.Array, this.closeBuffer.Offset);
                var messageType = (WebSocketMessageType)WebSocketFrameOption.ConnectionClose;
                var closeFrame = this.PrepareFrame(this.closeBuffer, 2, true, false, messageType, WebSocketExtensionFlags.None);

                await this.SendFrameAsync(closeFrame, Timeout.InfiniteTimeSpan, SendOptions.NoLock | SendOptions.IgnoreClose, CancellationToken.None)
                    .ConfigureAwait(false);
                await this.networkConnection.FlushAsync(CancellationToken.None).ConfigureAwait(false);
                await this.networkConnection.CloseAsync().ConfigureAwait(false);
            }
            catch (Exception closeError) when (closeError.Unwrap() is ThreadAbortException == false)
            {
                var closeErrorUnwrap = closeError.Unwrap();
                if (closeErrorUnwrap is IOException || closeErrorUnwrap is OperationCanceledException || closeErrorUnwrap is InvalidOperationException)
                    return; // ignore common IO exceptions while closing connection

                if (this.log.IsDebugEnabled)
                    this.log.Debug($"({this.GetHashCode():X}) An error occurred while closing connection.", closeError.Unwrap());
            }
            finally
            {
                SafeEnd.ReleaseSemaphore(this.writeSemaphore, this.log);
            }
        }
        public Task PingAsync(byte[] data, int offset, int count)
        {
            if (this.pingHandler is ManualPing)
            {
                if (data != null)
                {
                    if (offset < 0 || offset > data.Length) throw new ArgumentOutOfRangeException(nameof(offset));
                    if (count < 0 || count > 125 || offset + count > data.Length) throw new ArgumentOutOfRangeException(nameof(count));

                    this.pingBuffer.Array[this.pingBuffer.Offset] = (byte)count;
                    Buffer.BlockCopy(data, offset, this.pingBuffer.Array, this.pingBuffer.Offset + 1, count);
                }
                else
                {
                    this.pingBuffer.Array[this.pingBuffer.Offset] = 0;
                }
            }

            return this.pingHandler.PingAsync();
        }

        public void Dispose()
        {
            this.CloseAsync(WebSocketCloseReasons.NormalClose).Wait();

            if (Interlocked.Exchange(ref this.closeState, CLOSE_STATE_DISPOSED) == CLOSE_STATE_DISPOSED)
                return;

            this.options.BufferManager.ReturnBuffer(this.SendBuffer.Array);
            this.options.BufferManager.ReturnBuffer(this.closeBuffer.Array);

            SafeEnd.Dispose(this.writeSemaphore, this.log);
            SafeEnd.Dispose(this.networkConnection, this.log);
        }
    }

}
