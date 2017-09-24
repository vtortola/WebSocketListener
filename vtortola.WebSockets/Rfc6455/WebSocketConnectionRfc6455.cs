using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Rfc6455
{
    internal class WebSocketConnectionRfc6455 :IDisposable
    {
        readonly byte[] _buffer;
        readonly ArraySegment<byte> _headerBuffer, _pingBuffer, _pongBuffer, _controlBuffer, _keyBuffer, _closeBuffer;
        internal readonly ArraySegment<byte> SendBuffer;

        readonly SemaphoreSlim _writeSemaphore;
        readonly Stream _clientStream;
        readonly WebSocketListenerOptions _options;
        readonly PingStrategy _ping;

        int _ongoingMessageWrite, _ongoingMessageAwaiting, _isClosed;
        
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
            Guard.ParameterCannotBeNull(clientStream, nameof(clientStream));
            Guard.ParameterCannotBeNull(options, nameof(options));

            _writeSemaphore = new SemaphoreSlim(1);
            _options = options;
            
            _clientStream = clientStream;

            if (options.BufferManager != null)
                _buffer = options.BufferManager.TakeBuffer(14 + 125 + 125 + 8 + 10 + _options.SendBufferSize + 4 + 2);
            else
                _buffer = new byte[14 + 125 + 125 + 8 + 10 + _options.SendBufferSize  + 4 + 2];

            _headerBuffer = new ArraySegment<byte>(_buffer, 0, 14);
            _controlBuffer = new ArraySegment<byte>(_buffer, 14, 125);
            _pongBuffer = new ArraySegment<byte>(_buffer, 14 + 125, 125);
            _pingBuffer = new ArraySegment<byte>(_buffer, 14 + 125 + 125, 8);
            SendBuffer = new ArraySegment<byte>(_buffer, 14 + 125 + 125 + 8 + 10, _options.SendBufferSize);
            _keyBuffer = new ArraySegment<byte>(_buffer, 14 + 125 + 125 + 8 + 10 + _options.SendBufferSize, 4);
            _closeBuffer = new ArraySegment<byte>(_buffer, 14 + 125 + 125 + 8 + 10 + _options.SendBufferSize + 4, 2);

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
                while (IsConnected && CurrentHeader == null)
                {
                    // try read minimal frame first
                    var readed =  _clientStream.Read(_headerBuffer.Array,_headerBuffer.Offset, 6);
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

        internal async Task AwaitHeaderAsync(CancellationToken cancel)
        {
            CheckForDoubleRead();
            StartPing();
            try
            {
                while (IsConnected && CurrentHeader == null)
                {
                    // try read minimal frame first
                    var readed = await _clientStream.ReadAsync(_headerBuffer.Array, _headerBuffer.Offset, 6, cancel).ConfigureAwait(false);
                    if (readed == 0 || cancel.IsCancellationRequested)
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

        internal async Task<int> ReadInternalAsync(byte[] buffer, int offset, int count, CancellationToken cancel)
        {
            var reg = cancel.Register(Close, false);
            try
            {
                 var readed = await _clientStream.ReadAsync(buffer, offset, count, cancel).ConfigureAwait(false);
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
            finally
            {
                reg.Dispose();
            }
        }

        internal int ReadInternal(byte[] buffer, int offset, int count)
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

        internal void WriteInternal(ArraySegment<byte> buffer, int count, bool isCompleted, bool headerSent, WebSocketMessageType type, WebSocketExtensionFlags extensionFlags)
        {
            WriteInternal(buffer, count, isCompleted, headerSent, (WebSocketFrameOption)type, extensionFlags);
        }

        internal Task WriteInternalAsync(ArraySegment<byte> buffer, int count, bool isCompleted, bool headerSent, WebSocketMessageType type, WebSocketExtensionFlags extensionFlags, CancellationToken cancel)
        {
            return WriteInternalAsync(buffer, count, isCompleted, headerSent, (WebSocketFrameOption)type, extensionFlags, cancel);
        }

        internal void Close()
        {
            Close(WebSocketCloseReasons.NormalClose);
        }

        private void ParseHeader(int readed)
        {
            if (!TryReadHeaderUntil(ref readed, 6))
            {
                Close(WebSocketCloseReasons.ProtocolError);
                return;
            }

            var headerlength = WebSocketFrameHeader.GetHeaderLength(_headerBuffer.Array, _headerBuffer.Offset);

            if (!TryReadHeaderUntil(ref readed, headerlength))
            {
                Close(WebSocketCloseReasons.ProtocolError);
                return;
            }

            ProcessHeaderFrame(headerlength);
        }

        private async Task ParseHeaderAsync(int readed)
        {
            if (await TryReadHeaderUntilAsync(readed, 6).ConfigureAwait(false) == -1)
            {
                Close(WebSocketCloseReasons.ProtocolError);
                return;
            }

            var headerlength = WebSocketFrameHeader.GetHeaderLength(_headerBuffer.Array, _headerBuffer.Offset);

            if (await TryReadHeaderUntilAsync(readed, headerlength).ConfigureAwait(false) == -1)
            {
                Close(WebSocketCloseReasons.ProtocolError);
                return;
            }

            await ProcessHeaderFrameAsync(headerlength).ConfigureAwait(false);
        }

        private void ProcessHeaderFrame(int headerlength)
        {
            if (!WebSocketFrameHeader.TryParse(_headerBuffer.Array, _headerBuffer.Offset, headerlength, _keyBuffer, out WebSocketFrameHeader header))
                throw new WebSocketException("Cannot understand frame header");

            CurrentHeader = header;

            if (!header.Flags.Option.IsData())
            {
                ProcessControlFrame();
                CurrentHeader = null;
            }
            else
            {
                _ongoingMessageAwaiting = 0;
            }

            _ping.NotifyActivity();
        }

        private async Task ProcessHeaderFrameAsync(int headerlength)
        {
            if (!WebSocketFrameHeader.TryParse(_headerBuffer.Array, _headerBuffer.Offset, headerlength, _keyBuffer, out WebSocketFrameHeader header))
                throw new WebSocketException("Cannot understand frame header");

            CurrentHeader = header;

            if (!header.Flags.Option.IsData())
            {
                await ProcessControlFrameAsync().ConfigureAwait(false);
                CurrentHeader = null;
            }
            else
            {
                _ongoingMessageAwaiting = 0;
            }

            _ping.NotifyActivity();
        }

        private bool TryReadHeaderUntil(ref int readed, int until)
        {
            var r = 0;
            while (readed < until)
            {
                r = _clientStream.Read(_headerBuffer.Array, _headerBuffer.Offset + readed, until - readed);
                if (r == 0)
                    return false;

                readed += r;
            }

            return true;
        }
        private async Task<int> TryReadHeaderUntilAsync(int readed, int until)
        {
            var r = 0;
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
            if (Interlocked.CompareExchange(ref _ongoingMessageAwaiting, 1, 0) == 1)
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

        private Task ProcessControlFrameAsync()
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
                    return ProcessPingPongAsync();

                default: throw new WebSocketException("Unexpected header option '" + CurrentHeader.Flags.Option.ToString() + "'");
            }

            return Task.FromResult<object>(null);
        }

        private void ProcessPingPong()
        {
            var contentLength = _pongBuffer.Count;
            if (CurrentHeader.ContentLength < 125)
                contentLength = (int)CurrentHeader.ContentLength;

            var readed = 0;
            while (CurrentHeader.RemainingBytes > 0)
            {
                readed = _clientStream.Read(_pongBuffer.Array, _pongBuffer.Offset + readed, contentLength - readed);
                CurrentHeader.DecodeBytes(_pongBuffer.Array, _pongBuffer.Offset, readed);
            }

            if (CurrentHeader.Flags.Option == WebSocketFrameOption.Pong)
            {
                _ping.NotifyPong(_pongBuffer);
            }
            else
            {// pong frames echo what was 'pinged'
                WriteInternal(_pongBuffer, readed, true, false, WebSocketFrameOption.Pong, WebSocketExtensionFlags.None);
            }
        }

        private async Task ProcessPingPongAsync()
        {
            var contentLength = _pongBuffer.Count;
            if (CurrentHeader.ContentLength < 125)
                contentLength = (int)CurrentHeader.ContentLength;
            var readed = 0;
            while (CurrentHeader.RemainingBytes > 0)
            {
                readed = await _clientStream.ReadAsync(_pongBuffer.Array, _pongBuffer.Offset + readed, contentLength - readed).ConfigureAwait(false);
                CurrentHeader.DecodeBytes(_pongBuffer.Array, _pongBuffer.Offset, readed);
            }

            if (CurrentHeader.Flags.Option == WebSocketFrameOption.Pong)
            {
                _ping.NotifyPong(_pongBuffer);
            }
            else
            {// pong frames echo what was 'pinged'
                await WriteInternalAsync(_pongBuffer, readed, true, false, WebSocketFrameOption.Pong, WebSocketExtensionFlags.None, CancellationToken.None).ConfigureAwait(false);
            }
        }

        internal void WriteInternal(ArraySegment<byte> buffer, int count, bool isCompleted, bool headerSent, WebSocketFrameOption option, WebSocketExtensionFlags extensionFlags)
        {
            try
            {
                var header = WebSocketFrameHeader.Create(count, isCompleted, headerSent, option, extensionFlags);
                header.ToBytes(buffer.Array,buffer.Offset - header.HeaderLength);

                if (!_writeSemaphore.Wait(_options.WebSocketSendTimeout))
                {
                    throw new WebSocketException("Write timeout");
                }
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

        internal async Task WriteInternalAsync(ArraySegment<byte> buffer, int count, bool isCompleted, bool headerSent, WebSocketFrameOption option, WebSocketExtensionFlags extensionFlags, CancellationToken cancel)
        {
            try
            {
                var header = WebSocketFrameHeader.Create(count, isCompleted, headerSent, option, extensionFlags);
                header.ToBytes(buffer.Array, buffer.Offset - header.HeaderLength);

                if (!await _writeSemaphore.WaitAsync(_options.WebSocketSendTimeout, cancel).ConfigureAwait(false))
                {
                    throw new WebSocketException("Write timeout");
                }
                await _clientStream.WriteAsync(buffer.Array, buffer.Offset - header.HeaderLength, count + header.HeaderLength, cancel).ConfigureAwait(false);
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
                if (Interlocked.CompareExchange(ref _isClosed, 1, 0) == 1)
                    return;

                ((ushort)reason).ToBytesBackwards(_closeBuffer.Array, _closeBuffer.Offset);
                WriteInternal(_closeBuffer, 2, true, false, WebSocketFrameOption.ConnectionClose, WebSocketExtensionFlags.None);
                _clientStream.Close();
            }
            catch (Exception ex)
            {
                Debug.Fail("WebSocketConnectionRfc6455.Close: " + ex.Message);
            }
        }

        public void Dispose()
        {
            try
            {
                Close();
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
