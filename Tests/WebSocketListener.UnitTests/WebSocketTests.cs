﻿using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets;
using vtortola.WebSockets.Rfc6455;
using Xunit;
using Xunit.Abstractions;

namespace vtortola.WebSockets.UnitTests
{
    public class WebSocketTests
    {
        private readonly WebSocketFactoryCollection factories;
        private readonly WebSocketListenerOptions options;

        public WebSocketTests(ITestOutputHelper output)
        {
            var logger = new TestLogger(output);
            this.factories = new WebSocketFactoryCollection();
            this.factories.Add(new WebSocketFactoryRfc6455());
            this.options = new WebSocketListenerOptions
            {
                Logger = logger,
                PingTimeout = Timeout.InfiniteTimeSpan,
                BufferManager = BufferManager.CreateBufferManager(10, 1024),
                SendBufferSize = 512
            };
        }

        private WebSocketHandshake GenerateSimpleHandshake()
        {
            var handshaker = new WebSocketHandshaker(this.factories, this.options);

            using (var connectionInput = new MemoryStream())
            using (var connectionOutput = new MemoryStream())
            using (var connection = new DummyNetworkConnection(connectionInput, connectionOutput))
            {
                using (var sw = new StreamWriter(connectionInput, Encoding.ASCII, 1024, true))
                {
                    sw.WriteLine(@"GET /chat HTTP/1.1");
                    sw.WriteLine(@"Host: server.example.com");
                    sw.WriteLine(@"Upgrade: websocket");
                    sw.WriteLine(@"Connection: Upgrade");
                    sw.WriteLine(@"Sec-WebSocket-Key: x3JJHMbDL1EzLkh9GBhXDw==");
                    sw.WriteLine(@"Sec-WebSocket-Version: 13");
                    sw.WriteLine(@"Origin: http://example.com");
                }

                connectionInput.Seek(0, SeekOrigin.Begin);

                return handshaker.HandshakeAsync(connection).Result;
            }
        }

        [Fact]
        public void DetectHalfOpenConnection()
        {
            var handshake = this.GenerateSimpleHandshake();
            var options = this.options.Clone();
            options.PingTimeout = TimeSpan.FromMilliseconds(100);

            using (var connectionInput = new MemoryStream())
            using (var connectionOutput = new MemoryStream())
            using (var connection = new DummyNetworkConnection(connectionInput, connectionOutput))
            using (var ws = new WebSocketRfc6455(connection, options, handshake.Request,
                handshake.Response, handshake.NegotiatedMessageExtensions))
            {
                ws.ReadMessageAsync(CancellationToken.None);

                // DateTime has no millisecond precision. 
                Thread.Sleep(500);
                Assert.False(ws.IsConnected);
            }
        }

        [Fact]
        public void ReadEmptyMessage()
        {
            var handshake = this.GenerateSimpleHandshake();
            using (var connectionInput = new MemoryStream())
            using (var connectionOutput = new MemoryStream())
            using (var connection = new DummyNetworkConnection(connectionInput, connectionOutput))
            using (var ws = new WebSocketRfc6455(connection, this.options, handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions))
            {
                connectionInput.Write(new byte[]
                {
                    129, 128, 166, 124, 106, 65
                }, 0, 6);
                connectionInput.Flush();
                connectionInput.Seek(0, SeekOrigin.Begin);

                var reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.NotNull(reader);
                using (var sr = new StreamReader(reader, Encoding.UTF8, true, 1024, true))
                {
                    var s = sr.ReadToEnd();
                    Assert.Equal(string.Empty, s);
                }
            }
        }

        [Fact]
        public void ReadEmptyMessagesFollowedWithNonEmptyMessage()
        {
            var handshake = this.GenerateSimpleHandshake();
            using (var connectionInput = new MemoryStream())
            using (var connectionOutput = new MemoryStream())
            using (var connection = new DummyNetworkConnection(connectionInput, connectionOutput))
            using (var ws = new WebSocketRfc6455(connection, this.options, handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions))
            {
                connectionInput.Write(new byte[] { 129, 128, 166, 124, 106, 65 }, 0, 6);
                connectionInput.Write(new byte[] { 129, 128, 166, 124, 106, 65 }, 0, 6);
                connectionInput.Write(new byte[] { 129, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                connectionInput.Flush();
                connectionInput.Seek(0, SeekOrigin.Begin);

                var reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.NotNull(reader);
                using (var sr = new StreamReader(reader, Encoding.UTF8, true, 1024, true))
                {
                    var s = sr.ReadToEnd();
                    Assert.Equal(string.Empty, s);
                }

                reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.NotNull(reader);
                using (var sr = new StreamReader(reader, Encoding.UTF8, true, 1024, true))
                {
                    var s = sr.ReadToEnd();
                    Assert.Equal(string.Empty, s);
                }

                reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.NotNull(reader);
                using (var sr = new StreamReader(reader, Encoding.UTF8, true, 1024, true))
                {
                    var s = sr.ReadToEnd();
                    Assert.Equal("Hi", s);
                }
            }
        }

        [Fact]
        public void ReadSmallFrame()
        {
            var handshake = this.GenerateSimpleHandshake();
            using (var connectionInput = new MemoryStream())
            using (var connectionOutput = new MemoryStream())
            using (var connection = new DummyNetworkConnection(connectionInput, connectionOutput))
            using (var ws = new WebSocketRfc6455(connection, this.options, handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions))
            {
                connectionInput.Write(new byte[] { 129, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                connectionInput.Flush();
                connectionInput.Seek(0, SeekOrigin.Begin);

                var reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.NotNull(reader);
                using (var sr = new StreamReader(reader, Encoding.UTF8, true, 1024, true))
                {
                    var s = sr.ReadToEnd();
                    Assert.Equal("Hi", s);
                }

                connectionInput.Seek(0, SeekOrigin.Begin);
                connectionInput.Write(new byte[] { 129, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                connectionInput.Flush();
                connectionInput.Seek(0, SeekOrigin.Begin);

                reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.NotNull(reader);
                using (var sr = new StreamReader(reader, Encoding.UTF8, true, 1024, true))
                {
                    var s = sr.ReadToEndAsync().Result;
                    Assert.Equal("Hi", s);
                }
            }
        }

        [Fact]
        public void ReadThreeSmallPartialFrames()
        {
            var handshake = this.GenerateSimpleHandshake();
            using (var connectionInput = new MemoryStream())
            using (var connectionOutput = new MemoryStream())
            using (var connection = new DummyNetworkConnection(connectionInput, connectionOutput))
            using (var ws = new WebSocketRfc6455(connection, this.options, handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions))
            {
                connectionInput.Write(new byte[] { 1, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                connectionInput.Write(new byte[] { 0, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                connectionInput.Write(new byte[] { 128, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                connectionInput.Flush();
                connectionInput.Seek(0, SeekOrigin.Begin);

                var reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.NotNull(reader);
                using (var sr = new StreamReader(reader, Encoding.UTF8, true, 1024, true))
                {
                    var s = sr.ReadToEnd();
                    Assert.Equal("HiHiHi", s);
                }
            }
        }

        [Fact]
        public void ReadTwoBufferedSmallFrames()
        {
            var handshake = this.GenerateSimpleHandshake();
            using (var connectionInput = new MemoryStream())
            using (var connectionOutput = new MemoryStream())
            using (var connection = new DummyNetworkConnection(connectionInput, connectionOutput))
            using (var ws = new WebSocketRfc6455(connection, this.options, handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions))
            {
                connectionInput.Write(new byte[] { 129, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                connectionInput.Write(new byte[] { 129, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                connectionInput.Flush();
                connectionInput.Seek(0, SeekOrigin.Begin);

                var reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.NotNull(reader);
                using (var sr = new StreamReader(reader, Encoding.UTF8, true, 1024, true))
                {
                    var s = sr.ReadToEnd();
                    Assert.Equal("Hi", s);
                }

                reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.NotNull(reader);
                using (var sr = new StreamReader(reader, Encoding.UTF8, true, 1024, true))
                {
                    var s = sr.ReadToEndAsync().Result;
                    Assert.Equal("Hi", s);
                }

                reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.Null(reader);
            }
        }

        [Fact]
        public void ReadTwoSmallPartialFrames()
        {
            var handshake = this.GenerateSimpleHandshake();
            using (var connectionInput = new MemoryStream())
            using (var connectionOutput = new MemoryStream())
            using (var connection = new DummyNetworkConnection(connectionInput, connectionOutput))
            using (var ws = new WebSocketRfc6455(connection, this.options, handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions))
            {
                connectionInput.Write(new byte[] { 1, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                connectionInput.Write(new byte[] { 128, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                connectionInput.Flush();
                connectionInput.Seek(0, SeekOrigin.Begin);

                var reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.NotNull(reader);
                using (var sr = new StreamReader(reader, Encoding.UTF8, true, 1024, true))
                {
                    var s = sr.ReadToEnd();
                    Assert.Equal("HiHi", s);
                }
            }
        }

        [Fact]
        public void WriteTwoSequentialMessages()
        {
            var handshake = this.GenerateSimpleHandshake();
            using (var connectionInput = new MemoryStream())
            using (var connectionOutput = new MemoryStream())
            using (var connection = new DummyNetworkConnection(connectionInput, connectionOutput))
            using (var ws = new WebSocketRfc6455(connection, this.options, handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions))
            {
                using (var writer = ws.CreateMessageWriter(WebSocketMessageType.Text)) { }
                using (var writer = ws.CreateMessageWriter(WebSocketMessageType.Text)) { }
            }
        }

        [Fact]
        public async Task FailDoubleMessageAwait()
        {
            var handshake = this.GenerateSimpleHandshake();
            await Assert.ThrowsAsync<WebSocketException>(async () =>
            {
                using (var connectionInput = new BufferedStream(new MemoryStream()))
                using (var connectionOutput = new MemoryStream())
                using (var connection = new DummyNetworkConnection(connectionInput, connectionOutput))
                using (var ws = new WebSocketRfc6455(connection, this.options, handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions))
                {
                    connectionInput.Write(new byte[] { 129, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                    connectionInput.Write(new byte[] { 129, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                    connectionInput.Flush();
                    connectionInput.Seek(0, SeekOrigin.Begin);

                    await ws.ReadMessageAsync(CancellationToken.None).ConfigureAwait(false);
                    await ws.ReadMessageAsync(CancellationToken.None).ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
        }

        [Fact]
        public async Task FailDoubleMessageRead()
        {
            var handshake = this.GenerateSimpleHandshake();
            await Assert.ThrowsAsync<WebSocketException>(async () =>
            {
                using (var connectionInput = new MemoryStream())
                using (var connectionOutput = new MemoryStream())
                using (var connection = new DummyNetworkConnection(connectionInput, connectionOutput))
                using (var ws = new WebSocketRfc6455(connection, this.options, handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions))
                {
                    connectionInput.Write(new byte[] { 129, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                    connectionInput.Write(new byte[] { 129, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                    connectionInput.Flush();
                    connectionInput.Seek(0, SeekOrigin.Begin);

                    var reader = await ws.ReadMessageAsync(CancellationToken.None).ConfigureAwait(false);
                    Assert.NotNull(reader);
                    reader = await ws.ReadMessageAsync(CancellationToken.None).ConfigureAwait(false);
                    Assert.NotNull(reader);
                }
            }).ConfigureAwait(false);
        }

        [Fact]
        public void FailDoubleMessageWrite()
        {
            var handshake = this.GenerateSimpleHandshake();
            Assert.Throws<WebSocketException>(() =>
            {
                using (var connectionInput = new MemoryStream())
                using (var connectionOutput = new MemoryStream())
                using (var connection = new DummyNetworkConnection(connectionInput, connectionOutput))
                using (var ws = new WebSocketRfc6455(connection, this.options, handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions))
                {
                    var writer = ws.CreateMessageWriter(WebSocketMessageType.Text);
                    writer = ws.CreateMessageWriter(WebSocketMessageType.Text);
                }
            });
        }

    }
}
