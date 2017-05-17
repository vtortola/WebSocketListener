using System;
using System.Threading;
using System.Net;
using vtortola.WebSockets;
using System.Threading.Tasks;
using vtortola.WebSockets.Rfc6455;

namespace MonoEchoServer
{
	class MainClass
	{
		static void Log(String line)
		{
			Console.WriteLine (line);
		}

		public static void Main (string[] args)
		{
			var cancellation = new CancellationTokenSource();

			var endpoint = new IPEndPoint(IPAddress.Any, 8005);
		    var options = new WebSocketListenerOptions() {SubProtocols = new[] {"text"}};
		    options.Standards.RegisterRfc6455();
            var server = new WebSocketListener(endpoint, options);

		    server.StartAsync().Wait();
			Log("Mono Echo Server started at " + endpoint.ToString());

			var task = Task.Run(() => AcceptWebSocketClientsAsync(server, cancellation.Token));

			Console.ReadKey(true);
			Log("Server stoping");
		    server.StopAsync().Wait();

			task.Wait();
			Console.ReadKey(true);
		}

		static async Task AcceptWebSocketClientsAsync(WebSocketListener server, CancellationToken token)
		{
			while (!token.IsCancellationRequested)
			{
				try
				{
					var ws = await server.AcceptWebSocketAsync(token).ConfigureAwait(false);
					Log("Connected " + ws??"Null");
					if (ws != null)
						Task.Run(()=>HandleConnectionAsync(ws, token));
				}
				catch(Exception aex)
				{
					Log("Error Accepting clients: " + aex.GetBaseException().Message);
				}
			}
			Log("Server Stop accepting clients");
		}

		static async Task HandleConnectionAsync(WebSocket ws, CancellationToken cancellation)
		{
			try
			{
				while (ws.IsConnected && !cancellation.IsCancellationRequested)
				{
					var msg = await ws.ReadStringAsync(cancellation).ConfigureAwait(false);
					Log("Message: " + msg);
					await ws.WriteStringAsync(msg, cancellation).ConfigureAwait(false);
				}
			}
			catch (Exception aex)
			{
				Log("Error Handling connection: " + aex.GetBaseException().Message);
				try { await ws.CloseAsync().ConfigureAwait(false); }
				catch { }
			}
			finally
			{
				ws.Dispose();
			}
		}
	}
}
