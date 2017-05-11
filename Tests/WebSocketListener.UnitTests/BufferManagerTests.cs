using System;
using vtortola.WebSockets;
using Xunit;

namespace WebSocketListener.UnitTests
{
    public class BufferManagerTests
    {
        [Theory, 
        InlineData(1), 
        InlineData(2), 
        InlineData(8), 
        InlineData(64), 
        InlineData(256), 
        InlineData(333), 
        InlineData(800),
        InlineData(1024), 
        InlineData(2047), 
        InlineData(2048), 
        InlineData(2049)]
        public void TakeBuffer(int maxBufferSize)
        {
            var bufferManager = BufferManager.CreateBufferManager(100, maxBufferSize);
            var buffer = bufferManager.TakeBuffer(maxBufferSize);

            Assert.NotNull(buffer);
            Assert.True(buffer.Length >= maxBufferSize, "buffer.Length >= maxBufferSize");
        }

        [Theory, 
        InlineData(1024, 1),
        InlineData(1024, 2),
        InlineData(1024, 8),
        InlineData(1024, 64),
        InlineData(1024, 256),
        InlineData(1024, 333),
        InlineData(1024, 800),
        InlineData(4096, 1),
        InlineData(4096, 2),
        InlineData(4096, 8),
        InlineData(4096, 64),
        InlineData(4096, 256),
        InlineData(4096, 333),
        InlineData(4096, 800),
        InlineData(4096, 1024),
        InlineData(4096, 2047),
        InlineData(4096, 2048),
        InlineData(4096, 2049)]
        public void TakeSmallBuffer(int maxBufferSize, int takeBufferSize)
        {
            var bufferManager = BufferManager.CreateBufferManager(100, maxBufferSize);
            var buffer = bufferManager.TakeBuffer(takeBufferSize);

            Assert.NotNull(buffer);
            Assert.True(buffer.Length >= takeBufferSize, "buffer.Length >= maxBufferSize");
        }

        [Theory, 
        InlineData(1), 
        InlineData(2), 
        InlineData(8), 
        InlineData(64), 
        InlineData(256), 
        InlineData(333), 
        InlineData(800),
        InlineData(1024), 
        InlineData(2047), 
        InlineData(2048), 
        InlineData(2049)]
        public void ReturnBuffer(int maxBufferSize)
        {
            var bufferManager = BufferManager.CreateBufferManager(100, maxBufferSize);
            var buffer = bufferManager.TakeBuffer(maxBufferSize);

            Assert.NotNull(buffer);

            bufferManager.ReturnBuffer(buffer);
        }

        [Theory, 
        InlineData(1024, 1), 
        InlineData(1024, 2),
        InlineData(1024, 8),
        InlineData(1024, 64),
        InlineData(1024, 256),
        InlineData(1024, 333), 
        InlineData(1024, 800),
        InlineData(4096, 1),
        InlineData(4096, 2),
        InlineData(4096, 8),
        InlineData(4096, 64),
        InlineData(4096, 256),
        InlineData(4096, 333),
        InlineData(4096, 800),
        InlineData(4096, 1024),
        InlineData(4096, 2047),
        InlineData(4096, 2048),
        InlineData(4096, 2049)]
        public void ReturnSmallBuffer(int maxBufferSize, int takeBufferSize)
        {
            var bufferManager = BufferManager.CreateBufferManager(100, maxBufferSize);
            var buffer = bufferManager.TakeBuffer(takeBufferSize);

            Assert.NotNull(buffer);

            bufferManager.ReturnBuffer(buffer);
        }
        [Fact]
        public void Construct()
        {
            var bufferManager = BufferManager.CreateBufferManager(1, 1);

            Assert.NotNull(bufferManager);
        }

        [Fact]
        public void ConstructWithInvalidFirstParameter()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BufferManager.CreateBufferManager(-1, 1));
        }

        [Fact]
        public void ConstructWithInvalidSecondParameters()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BufferManager.CreateBufferManager(1, -1));
        }
    }
}
