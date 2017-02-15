using System;
using System.Threading;
using System.Net;
using vtortola.WebSockets;
using System.Threading.Tasks;

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
			CancellationTokenSource cancellation = new CancellationTokenSource();

			var endpoint = new IPEndPoint(IPAddress.Any, 8005);
			WebSocketListener server = new WebSocketListener(endpoint, new WebSocketListenerOptions(){ SubProtocols = new []{"text"}});
			var rfc6455 = new vtortola.WebSockets.Rfc6455.WebSocketFactoryRfc6455(server);
			server.Standards.RegisterStandard(rfc6455);
			server.Start();

			Log("Mono Echo Server started at " + endpoint.ToString());

			var task = Task.Run(() => AcceptWebSocketClientsAsync(server, cancellation.Token));

			Console.ReadKey(true);
			Log("Server stoping");
			cancellation.Cancel();
			task.Wait();
			Console.ReadKey(true);
		}

		static async Task AcceptWebSocketClientsAsync(WebSocketListener server, CancellationToken token)
		{
			while (!token.IsCancellationRequested)
			{
				try
				{
					var ws = await server.AcceptWebSocketAsync(token);
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
					String msg = await ws.ReadStringAsync(cancellation).ConfigureAwait(false);
					Log("Message: " + msg);
					ws.WriteString(msg);
				}
			}
			catch (Exception aex)
			{
				Log("Error Handling connection: " + aex.GetBaseException().Message);
				try { ws.Close(); }
				catch { }
			}
			finally
			{
				ws.Dispose();
			}
		}
	}
}
