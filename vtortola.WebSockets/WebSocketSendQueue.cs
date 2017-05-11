using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using NegotiatedRequest = System.Tuple<vtortola.WebSockets.WebSocket, System.Exception>;

namespace vtortola.WebSockets
{
	internal sealed class WebSocketSendQueue
	{
		private static readonly string WebSocketHttpVersion = "HTTP/1.1";

		private readonly WebSocketFactoryCollection standards;
		private readonly WebSocketListenerOptions options;
		private readonly CancellationToken cancellation;
		private readonly TransformBlock<Uri, NegotiatedRequest> negotiationBlock;
		private readonly ActionBlock<NegotiatedRequest> sendBlock;

		/// <inheritdoc />
		public Task Completion { get; }

		public WebSocketSendQueue(WebSocketFactoryCollection standards, WebSocketListenerOptions options, CancellationToken cancellation)
		{
			if (standards == null) throw new ArgumentNullException(nameof(standards));
			if (options == null) throw new ArgumentNullException(nameof(options));

			this.standards = standards;
			this.options = options;
			this.cancellation = cancellation;
			this.negotiationBlock = new TransformBlock<Uri, NegotiatedRequest>((Func<Uri, Task<NegotiatedRequest>>)this.NegotiateRequestAsync, new ExecutionDataflowBlockOptions
			{
				CancellationToken = cancellation,
				MaxDegreeOfParallelism = Environment.ProcessorCount,
				TaskScheduler = TaskScheduler.Default
			});
			this.sendBlock = new ActionBlock<NegotiatedRequest>((Action<NegotiatedRequest>)this.SendNegotiatedMessage, new ExecutionDataflowBlockOptions
			{
				CancellationToken = cancellation,
				MaxDegreeOfParallelism = Environment.ProcessorCount,
				TaskScheduler = TaskScheduler.Default
			});
			this.negotiationBlock.LinkTo(this.sendBlock, new DataflowLinkOptions
			{
				PropagateCompletion = true
			});
			this.Completion = Task.WhenAll(this.sendBlock.Completion, this.negotiationBlock.Completion);
		}

		private Task<NegotiatedRequest> NegotiateRequestAsync(Uri url)
		{
			throw new NotImplementedException();
			/*
			var isValidSchema = string.Equals(url?.Scheme, "ws", StringComparison.OrdinalIgnoreCase) ||
								string.Equals(url?.Scheme, "wss", StringComparison.OrdinalIgnoreCase);
			if (isValidSchema == false || url == null)
				throw new InvalidOperationException($"Invalid request url '{url}' or schema '{url?.Scheme}'.");

			var isSecure = string.Equals(url.Scheme, "wss", StringComparison.OrdinalIgnoreCase);
			var remoteEndpoint = default(EndPoint);
			var ipAddress = default(IPAddress);
			var port = url.Port;
			if (port == 0) port = isSecure ? 443 : 80;
			if (IPAddress.TryParse(url.Host, out ipAddress))
				remoteEndpoint = new IPEndPoint(ipAddress, port);
			else
				remoteEndpoint = new DnsEndPoint(url.DnsSafeHost, port);
			var localEndpoint = new IPEndPoint(remoteEndpoint.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0);

			var client = new Socket(remoteEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			client.NoDelay = !(options.UseNagleAlgorithm ?? true);
			client.SendTimeout = (int)options.WebSocketSendTimeout.TotalMilliseconds;
			client.ReceiveTimeout = (int)options.WebSocketReceiveTimeout.TotalMilliseconds;
			client.Bind(localEndpoint);
			var socketConnectedCondition = new AsyncConditionSource { ContinueOnCapturedContext = false };
			var socketAsyncEventArgs = new SocketAsyncEventArgs
			{
				RemoteEndPoint = remoteEndpoint,
				UserToken = socketConnectedCondition
			};
			socketAsyncEventArgs.Completed += (_, e) => ((AsyncConditionSource)e.UserToken).Set();
			if (client.ConnectAsync(socketAsyncEventArgs) == false)
				socketConnectedCondition.Set();

			await socketConnectedCondition;

			if (socketAsyncEventArgs.ConnectByNameError != null)
				throw socketAsyncEventArgs.ConnectByNameError;

			if (socketAsyncEventArgs.SocketError != SocketError.Success)
				throw new SocketException((int)socketAsyncEventArgs.SocketError);

			var stream = new NetworkStream(client, ownsSocket: true);
			using (var writer = new StreamWriter(stream, Encoding.ASCII, options.BufferManager, leaveOpen: true))
			{
				writer.NewLine = "\r\n";
				await writer.WriteAsync("GET ").ConfigureAwait(false);
				await writer.WriteAsync(url.PathAndQuery).ConfigureAwait(false);
				await writer.WriteLineAsync($" {WebSocketHttpVersion}").ConfigureAwait(false);
				await writer.WriteLineAsync("Upgrade: WebSocket").ConfigureAwait(false);
				await writer.WriteLineAsync("Connection: Upgrade").ConfigureAwait(false);
				await writer.WriteAsync("Host: ").ConfigureAwait(false);
				await writer.WriteLineAsync(url.DnsSafeHost).ConfigureAwait(false);
				foreach (var header in webSocketRequest.Headers)
				{
					if (string.Equals(header.Key, "Host", StringComparison.OrdinalIgnoreCase))
						continue;
					if (string.Equals(header.Key, "Upgrade", StringComparison.OrdinalIgnoreCase))
						continue;
					if (string.Equals(header.Key, "Connection", StringComparison.OrdinalIgnoreCase))
						continue;

					foreach (var value in header.Value)
					{
						await writer.WriteAsync(header.Key).ConfigureAwait(false);
						await writer.WriteAsync(": ").ConfigureAwait(false);
						await writer.WriteLineAsync(value).ConfigureAwait(false);
					}
				}

				await writer.WriteLineAsync().ConfigureAwait(false);
			}
			using (var reader = new StreamReader(stream, Encoding.ASCII, false, this.BufferSize, leaveOpen: true))
			{
				var responseLine = await reader.ReadLineAsync().ConfigureAwait(false);
				if (!responseLine.Equals($"{protocolVersion} 101 Web Socket Protocol Handshake"))
					throw new IOException($"Invalid handshake response: {responseLine}.");

				responseLine = await reader.ReadLineAsync().ConfigureAwait(false);
				if (!responseLine.Equals("Upgrade: WebSocket"))
					throw new IOException($"Invalid handshake response: {responseLine}.");

				responseLine = await reader.ReadLineAsync().ConfigureAwait(false);
				if (!responseLine.Equals("Connection: Upgrade"))
					throw new IOException($"Invalid handshake response: {responseLine}.");
				
			}
			*/
		}
		private void SendNegotiatedMessage(NegotiatedRequest negotiatedMessage)
		{
			throw new NotImplementedException();
		}
	}
}
