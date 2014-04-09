using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public static class WebSocketStringExtensions
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times")]
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times")]
        public static void WriteString(this WebSocket ws, String data)
        {
            using (var msg = ws.CreateMessageWriter(WebSocketMessageType.Text))
            using (var writer = new StreamWriter(msg, Encoding.UTF8))
                writer.Write(data);
        }
        public static async Task<String> ReadStringAsync(this WebSocket ws, CancellationToken cancel)
        {
            using (var msg = await ws.ReadMessageAsync(cancel))
            {
                if (msg == null)
                    return null;

                using (var reader = new StreamReader(msg, Encoding.UTF8))
                    return await reader.ReadToEndAsync();
            }
        }
        public static async Task WriteStringAsync(this WebSocket ws, String data, CancellationToken cancel)
        {
            using (var msg = ws.CreateMessageWriter(WebSocketMessageType.Text))
            using (var writer = new StreamWriter(msg, Encoding.UTF8))
            {
                await writer.WriteAsync(data);
                await writer.FlushAsync();
            }
        }
    }
}
