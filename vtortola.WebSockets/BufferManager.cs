/*
	Copyright (c) 2017 Denis Zykov
	License: https://opensource.org/licenses/MIT
*/
using System;
using vtortola.WebSockets.Tools;

namespace vtortola.WebSockets
{
    public sealed class BufferManager
    {
        private readonly ObjectPool<byte[]> smallPool;
        private readonly ObjectPool<byte[]> largePool;

        public readonly int SmallBufferSize;
        public readonly int LargeBufferSize;

        private BufferManager(int smallBufferSize, int smallPoolSizeLimit, int largeBufferSize, int largePoolSizeLimit)
        {
            this.SmallBufferSize = smallBufferSize;
            this.LargeBufferSize = largeBufferSize;
            this.smallPool = new ObjectPool<byte[]>(() => new byte[smallBufferSize], smallPoolSizeLimit);
            this.largePool = new ObjectPool<byte[]>(() => new byte[largeBufferSize], largePoolSizeLimit);

        }

        /// <summary>
        /// Creates a new BufferManager with a specified maximum buffer pool size and a maximum size for each individual buffer in the pool.
        /// </summary>
        /// <param name="maxBufferPoolSize">The maximum size of the pool.</param>
        /// <param name="maxBufferSize">The maximum size of an individual buffer.</param>
        /// <returns>Returns a System.ServiceModel.Channels.BufferManager object with the specified parameters.</returns>
        /// <exception cref="System.InsufficientMemoryException">In the .NET for Windows Store apps or the Portable Class Library, catch the base class exception, System.OutOfMemoryException, instead.There was insufficient memory to create the requested buffer pool.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException"> maxBufferPoolSize or maxBufferSize was less than zero.</exception>
        public static BufferManager CreateBufferManager(long maxBufferPoolSize, int maxBufferSize)
        {
            if (maxBufferPoolSize < 0) throw new ArgumentOutOfRangeException(nameof(maxBufferPoolSize));
            if (maxBufferSize < 0) throw new ArgumentOutOfRangeException(nameof(maxBufferSize));
            if (maxBufferSize < 256) maxBufferSize = 256;
            if (maxBufferPoolSize < 10) maxBufferPoolSize = 10;

            var maxMemory = checked(maxBufferPoolSize * maxBufferSize);
            var largeBufferSize = (int)Math.Pow(2, Math.Ceiling(Math.Log(maxBufferSize) / Math.Log(2))); // find nearest bigger power of two
            var largePoolSizeLimit = (int)Math.Max(2, Math.Ceiling(maxMemory / 2.0f / largeBufferSize)); // take half of maxMemory as large buffers
            var smallBufferSize = Math.Max(32, largeBufferSize / 256); // small buffer is 256 times less than large buffer
            var smallPoolSizeLimit = (int)Math.Max(2, (maxMemory - largePoolSizeLimit * largeBufferSize) / smallBufferSize); // take another half of maxMemory as small buffers

            return new BufferManager(smallBufferSize, smallPoolSizeLimit, largeBufferSize, largePoolSizeLimit);
        }

        /// <summary>
        /// Releases the buffers currently cached in the manager.
        /// </summary>
        public void Clear()
        {
            this.smallPool.Clear();
            this.largePool.Clear();
        }
        /// <summary>
        /// Returns a buffer to the pool.
        /// </summary>
        /// <param name="buffer">A reference to the buffer being returned.</param>
        /// <exception cref="System.ArgumentNullException">buffer reference cannot be null.</exception>
        /// <exception cref="System.ArgumentException">Length of buffer does not match the pool's buffer length property.</exception>
        public void ReturnBuffer(byte[] buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));

            if (buffer.Length >= this.LargeBufferSize)
                this.largePool.Return(buffer);
            else if (buffer.Length >= this.SmallBufferSize)
                this.smallPool.Return(buffer);
            else
                throw new ArgumentException("Length of buffer does not match the pool's buffer length property.", nameof(buffer));
        }
        /// <summary>
        /// Gets a buffer of at least the specified size from the pool.
        /// </summary>
        /// <param name="bufferSize">The size, in bytes, of the requested buffer.</param>
        /// <returns>A byte array that is the requested size of the buffer.</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">bufferSize cannot be less than zero.</exception>
        public byte[] TakeBuffer(int bufferSize)
        {
            if (bufferSize < 0 || bufferSize > this.LargeBufferSize) throw new ArgumentOutOfRangeException(nameof(bufferSize));

            if (bufferSize >= this.SmallBufferSize)
                return this.largePool.Take();
            else
                return this.smallPool.Take();
        }
    }
}
