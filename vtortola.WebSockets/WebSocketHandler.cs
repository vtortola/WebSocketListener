using System;
using System.Threading;
using System.Threading.Tasks;
namespace vtortola.WebSockets
{
    public abstract class WebSocketHandler:IDisposable
    {
        public abstract bool IsConnected { get; }
        public abstract void AwaitHeader();
        public abstract Task AwaitHeaderAsync(CancellationToken cancellation);
        public abstract int ReadInternal(byte[] buffer, int offset, int count);
        public abstract Task<int> ReadInternalAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);
        public abstract void BeginWritting();
        public abstract void EndWritting();
        public abstract void WriteInternal(byte[] buffer, int offset, int count, bool isCompleted, bool headerSent, WebSocketMessageType type, WebSocketExtensionFlags extensionFlags);
        public abstract Task WriteInternalAsync(byte[] buffer, int offset, int count, bool isCompleted, bool headerSent, WebSocketMessageType type, WebSocketExtensionFlags extensionFlags, CancellationToken cancellation);
        public abstract void Close();
        public abstract void Dispose();
    }
}
