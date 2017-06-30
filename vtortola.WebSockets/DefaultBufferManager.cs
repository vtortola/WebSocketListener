using System;
using vtortola.WebSockets.Tools;

namespace vtortola.WebSockets
{
    internal sealed class DefaultBufferManager : BufferManager
    {
        private readonly ObjectPool<byte[]> smallPool;
        private readonly ObjectPool<byte[]> largePool;

        public override int SmallBufferSize { get; }
        public override int LargeBufferSize { get; }

        public DefaultBufferManager(int smallBufferSize, int smallPoolSizeLimit, int largeBufferSize, int largePoolSizeLimit)
        {
            this.SmallBufferSize = smallBufferSize;
            this.LargeBufferSize = largeBufferSize;
            this.smallPool = new ObjectPool<byte[]>(() => new byte[smallBufferSize], smallPoolSizeLimit);
            this.largePool = new ObjectPool<byte[]>(() => new byte[largeBufferSize], largePoolSizeLimit);

        }

        /// <inheritdoc/>
        public override void Clear()
        {
            this.smallPool.Clear();
            this.largePool.Clear();
        }
        /// <inheritdoc/>
        public override void ReturnBuffer(byte[] buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));

            if (buffer.Length >= this.LargeBufferSize)
                this.largePool.Return(buffer);
            else if (buffer.Length >= this.SmallBufferSize)
                this.smallPool.Return(buffer);
            else
                throw new ArgumentException("Length of buffer does not match the pool's buffer length property.", nameof(buffer));
        }
        /// <inheritdoc/>
        public override byte[] TakeBuffer(int bufferSize)
        {
            if (bufferSize < 0 || bufferSize > this.LargeBufferSize) throw new ArgumentOutOfRangeException(nameof(bufferSize));

            if (bufferSize >= this.SmallBufferSize)
                return this.largePool.Take();
            else
                return this.smallPool.Take();
        }
    }
}