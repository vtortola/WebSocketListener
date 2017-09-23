using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public static class WebSocketStringExtensions
    {
        public static String ReadString(this WebSocket ws)
        {
            using (var msg = ws.ReadMessage())
            {
                if (msg == null)
                    return null;

                using (var reader = new StreamReader(msg, Encoding.UTF8))
                    return reader.ReadToEnd();
            }
        }
        public static void WriteString(this WebSocket ws, string data)
        {
            using (var msg = ws.CreateMessageWriter(WebSocketMessageType.Text))
            using (var writer = new StreamWriter(msg, Encoding.UTF8))
                writer.Write(data);
        }

        public static async Task<string> ReadStringAsync(this WebSocket ws, CancellationToken cancel)
        {
            using (var msg = await ws.ReadMessageAsync(cancel).ConfigureAwait(false))
            {
                if (msg == null)
                    return null;

                using (var reader = new StreamReader(msg, Encoding.UTF8))
                    return await reader.ReadToEndAsync().ConfigureAwait(false);
            }
        }
        public static async Task WriteStringAsync(this WebSocket ws, string data, CancellationToken cancel)
        {
            using (var msg = ws.CreateMessageWriter(WebSocketMessageType.Text))
            using (var writer = new StreamWriter(msg, Encoding.UTF8))
            {
                writer.Write(data);
                await writer.FlushAsync().ConfigureAwait(false);
                await msg.CloseAsync(cancel).ConfigureAwait(false);
            }
        }
    }
}
