using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

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
        readonly PingStrategy _ping;

        Int32 _ongoingMessageWrite, _ongoingMessageAwaiting, _isClosed;
        
        int _pingStarted;
        TimeSpan _latency;

        internal Boolean IsConnected { get { return _isClosed==0; } }
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
            Guard.ParameterCannotBeNull(clientStream, "clientStream");
            Guard.ParameterCannotBeNull(options, "options");

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

            switch (options.PingMode)
            {
                case PingModes.BandwidthSaving:
                    _ping = new BandwidthSavingPing(this, _options.PingTimeout, _pingBuffer);
                    break;

                case PingModes.LatencyControl:
                default:
                    _ping = new LatencyControlPing(this, _options.PingTimeout, _pingBuffer);
                    break;
            }
        }

        private void StartPing()
        {
            if (Interlocked.CompareExchange(ref _pingStarted, 1, 0) == 0)
            {
                if (_options.PingTimeout != Timeout.InfiniteTimeSpan)
                {
                    _ping.StartPing();
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
                while (IsConnected && CurrentHeader == null)
                {
                    // try read minimal frame first
                    Int32 readed = await _clientStream.ReadAsync(_headerBuffer.Array, _headerBuffer.Offset, 6, cancellation).ConfigureAwait(false);
                    if (readed == 0 || cancellation.IsCancellationRequested)
                    {
                        Close(WebSocketCloseReasons.ProtocolError);
                        return;
                    }

                    await ParseHeaderAsync(readed).ConfigureAwait(false);
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
            catch
            {
                Close(WebSocketCloseReasons.ProtocolError);
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
            CancellationTokenRegistration reg = cancellationToken.Register(this.Close, false);
            try
            {
                 var readed = await _clientStream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
                CurrentHeader.DecodeBytes(buffer, offset, readed);
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
            catch (InvalidOperationException)
            {
                Close(WebSocketCloseReasons.UnexpectedCondition);
                return 0;
            }
            catch (IOException)
            {
                Close(WebSocketCloseReasons.UnexpectedCondition);
                return 0;
            }
        }

        internal void EndWritting()
        {
            _ongoingMessageWrite = 0;
        }

        internal void BeginWritting()
        {
            if(Interlocked.CompareExchange(ref _ongoingMessageWrite, 1 , 0) == 1)
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

            ProcessHeaderFrame(headerlength);
        }

        private async Task ParseHeaderAsync(Int32 readed)
        {
            if (await TryReadHeaderUntilAsync(readed, 6).ConfigureAwait(false) == -1)
            {
                Close(WebSocketCloseReasons.ProtocolError);
                return;
            }

            Int32 headerlength = WebSocketFrameHeader.GetHeaderLength(_headerBuffer.Array, _headerBuffer.Offset);

            if (await TryReadHeaderUntilAsync(readed, headerlength).ConfigureAwait(false) == -1)
            {
                Close(WebSocketCloseReasons.ProtocolError);
                return;
            }

            await ProcessHeaderFrameAsync(headerlength);
        }

        private void ProcessHeaderFrame(int headerlength)
        {
            WebSocketFrameHeader header;
            if (!WebSocketFrameHeader.TryParse(_headerBuffer.Array, _headerBuffer.Offset, headerlength, _keyBuffer, out header))
                throw new WebSocketException("Cannot understand frame header");

            CurrentHeader = header;

            if (!header.Flags.Option.IsData())
            {
                ProcessControlFrame();
                CurrentHeader = null;
            }
            else
                _ongoingMessageAwaiting = 0;

            _ping.NotifyActivity();
        }

        private async Task ProcessHeaderFrameAsync(int headerlength)
        {
            WebSocketFrameHeader header;
            if (!WebSocketFrameHeader.TryParse(_headerBuffer.Array, _headerBuffer.Offset, headerlength, _keyBuffer, out header))
                throw new WebSocketException("Cannot understand frame header");

            CurrentHeader = header;

            if (!header.Flags.Option.IsData())
            {
                await ProcessControlFrameAsync().ConfigureAwait(false);
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
        private async Task<int> TryReadHeaderUntilAsync(Int32 readed, Int32 until)
        {
            Int32 r = 0;
            while (readed < until)
            {
                r = await _clientStream.ReadAsync(_headerBuffer.Array, _headerBuffer.Offset + readed, until - readed).ConfigureAwait(false);
                if (r == 0)
                    return -1;

                readed += r;
            }

            return readed;
        }

        private void CheckForDoubleRead()
        {
            if (Interlocked.CompareExchange(ref _ongoingMessageAwaiting,1,0) == 1)
                throw new WebSocketException("There is an ongoing message await from somewhere else. Only a single write is allowed at the time.");
                       
            if (CurrentHeader != null)
                throw new WebSocketException("There is an ongoing message that is being readed from somewhere else");
        }

        private void ProcessControlFrame()
        {
            switch (CurrentHeader.Flags.Option)
            {
                case WebSocketFrameOption.Continuation:
                case WebSocketFrameOption.Text:
                case WebSocketFrameOption.Binary:
                    throw new WebSocketException("Text, Continuation or Binary are not protocol frames");

                case WebSocketFrameOption.ConnectionClose:
                    Close(WebSocketCloseReasons.NormalClose);
                    break;

                case WebSocketFrameOption.Ping:
                case WebSocketFrameOption.Pong:
                    ProcessPingPong();
                    break;
                default: throw new WebSocketException("Unexpected header option '" + CurrentHeader.Flags.Option.ToString() + "'");
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
                    Close(WebSocketCloseReasons.NormalClose);
                    break;

                case WebSocketFrameOption.Ping:
                case WebSocketFrameOption.Pong:
                    await ProcessPingPongAsync().ConfigureAwait(false);
                    break;
                default: throw new WebSocketException("Unexpected header option '" + CurrentHeader.Flags.Option.ToString() + "'");
            }
        }

        private void ProcessPingPong()
        {
            Int32 contentLength = _pongBuffer.Count;
            if (CurrentHeader.ContentLength < 125)
                contentLength = (Int32)CurrentHeader.ContentLength;
            Int32 readed = 0;
            while (CurrentHeader.RemainingBytes > 0)
            {
                readed = _clientStream.Read(_pongBuffer.Array, _pongBuffer.Offset + readed, contentLength - readed);
                CurrentHeader.DecodeBytes(_pongBuffer.Array, _pongBuffer.Offset, readed);
            }

            if (CurrentHeader.Flags.Option == WebSocketFrameOption.Pong)
                _ping.NotifyPong(_pongBuffer);
            else // pong frames echo what was 'pinged'
                this.WriteInternal(_pongBuffer, readed, true, false, WebSocketFrameOption.Pong, WebSocketExtensionFlags.None);
        }

        private async Task ProcessPingPongAsync()
        {
            Int32 contentLength = _pongBuffer.Count;
            if (CurrentHeader.ContentLength < 125)
                contentLength = (Int32)CurrentHeader.ContentLength;
            Int32 readed = 0;
            while (CurrentHeader.RemainingBytes > 0)
            {
                readed = await _clientStream.ReadAsync(_pongBuffer.Array, _pongBuffer.Offset + readed, contentLength - readed).ConfigureAwait(false);
                CurrentHeader.DecodeBytes(_pongBuffer.Array, _pongBuffer.Offset, readed);
            }

            if (CurrentHeader.Flags.Option == WebSocketFrameOption.Pong)
                _ping.NotifyPong(_pongBuffer);
            else // pong frames echo what was 'pinged'
                await WriteInternalAsync(_pongBuffer, readed, true, false, WebSocketFrameOption.Pong, WebSocketExtensionFlags.None, CancellationToken.None).ConfigureAwait(false);
        }

        internal void WriteInternal(ArraySegment<Byte> buffer, Int32 count, Boolean isCompleted, Boolean headerSent, WebSocketFrameOption option, WebSocketExtensionFlags extensionFlags)
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
                SafeEnd.ReleaseSemaphore(_writeSemaphore);
            }
        }

        internal async Task WriteInternalAsync(ArraySegment<Byte> buffer, Int32 count, Boolean isCompleted, Boolean headerSent, WebSocketFrameOption option, WebSocketExtensionFlags extensionFlags, CancellationToken cancellation)
        {
            try
            {
                var header = WebSocketFrameHeader.Create(count, isCompleted, headerSent, option, extensionFlags);
                header.ToBytes(buffer.Array, buffer.Offset - header.HeaderLength);

                if (!await _writeSemaphore.WaitAsync(_options.WebSocketSendTimeout).ConfigureAwait(false))
                    throw new WebSocketException("Write timeout");
                await _clientStream.WriteAsync(buffer.Array, buffer.Offset - header.HeaderLength, count + header.HeaderLength, cancellation).ConfigureAwait(false);
            }
            catch(OperationCanceledException)
            {
                Close(WebSocketCloseReasons.GoingAway);
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
                SafeEnd.ReleaseSemaphore(_writeSemaphore);
            }
        }

        internal void Close(WebSocketCloseReasons reason)
        {
            try
            {
                if (Interlocked.CompareExchange(ref _isClosed,1,0) == 1)
                    return;

                ((UInt16)reason).ToBytesBackwards(_closeBuffer.Array, _closeBuffer.Offset);
                WriteInternal(_closeBuffer, 2, true, false, WebSocketFrameOption.ConnectionClose, WebSocketExtensionFlags.None);
                _clientStream.Close();
            }
            catch (Exception ex)
            {
                DebugLog.Fail("WebSocketConnectionRfc6455.Close", ex);
            }
        }

        public void Dispose()
        {
            try
            {
                this.Close();
            }
            catch { }
            finally
            {
                if (_options != null && _options.BufferManager != null)
                    _options.BufferManager.ReturnBuffer(_buffer);
            }
            SafeEnd.Dispose(_writeSemaphore);
            SafeEnd.Dispose(_clientStream);
        }
    }

}
