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
        public static void WriteString(this WebSocket ws, String data)
        {
            using (var msg = ws.CreateMessageWriter(WebSocketMessageType.Text))
            using (var writer = new StreamWriter(msg, Encoding.UTF8))
                writer.Write(data);
        }
        /*
         * Async methods are not enterely asynchronous. There are an asynchronous part and a synchronous one.
         * 
         * ReadStringAsync: Awaiting a message is async (since it is unpredictable), but reading is sync since usually
         * messages are small,  probably the data is already in the buffer.
         * 
         * WriteStringAsync: Write is sync, since most of messages will be smaller than buffer size and they will be probably 
         * cached inside the message writer or StreamWriter, but Flush is async since it is when the actual buffered message
         * will be written.
         * 
         * These are the best settings for most of uses cases in my experience. If you have particulary large
         * messages and/or very slow connections, you can create your own extension like this one to fit your particular 
         * needs. That is the reason why these methods are extensions and not part of the component.
         */
        public static async Task<String> ReadStringAsync(this WebSocket ws, CancellationToken cancel)
        {
            using (var msg = await ws.ReadMessageAsync(cancel).ConfigureAwait(false))
            {
                if (msg == null)
                    return null;

                using (var reader = new StreamReader(msg, Encoding.UTF8))
                    return reader.ReadToEnd();
            }
        }
        public static async Task WriteStringAsync(this WebSocket ws, String data, CancellationToken cancel)
        {
            using (var msg = ws.CreateMessageWriter(WebSocketMessageType.Text))
            using (var writer = new StreamWriter(msg, Encoding.UTF8))
            {
                writer.Write(data);
                await writer.FlushAsync().ConfigureAwait(false);
            }
        }
    }
}
