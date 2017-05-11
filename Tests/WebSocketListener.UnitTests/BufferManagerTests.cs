using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vtortola.WebSockets;

namespace WebSocketListener.UnitTests
{
	[TestClass]
	public class BufferManagerTests
	{
		[TestMethod]
		public void Construct()
		{
			var bufferManager = BufferManager.CreateBufferManager(1, 1);

			Assert.IsNotNull(bufferManager);
		}

		[TestMethod]
		[ExpectedException(typeof(ArgumentOutOfRangeException))]
		public void ConstructWithInvalidFirstParameter()
		{

			BufferManager.CreateBufferManager(-1, 1);
			Assert.Fail("Should throw exception");
		}

		[TestMethod]
		[ExpectedException(typeof(ArgumentOutOfRangeException))]
		public void ConstructWithInvalidSecondParameters()
		{

			BufferManager.CreateBufferManager(1, -1);
			Assert.Fail("Should throw exception");
		}

		[TestMethod]
		public void TakeBuffer1024()
		{
			var maxBufferSize = 1024;
			var bufferManager = BufferManager.CreateBufferManager(100, maxBufferSize);
			var buffer = bufferManager.TakeBuffer(maxBufferSize);

			Assert.IsNotNull(buffer);
			Assert.IsTrue(buffer.Length >= maxBufferSize, "buffer.Length >= maxBufferSize");
		}


		[TestMethod]
		public void TakeBuffer812()
		{
			var maxBufferSize = 812;
			var bufferManager = BufferManager.CreateBufferManager(100, maxBufferSize);
			var buffer = bufferManager.TakeBuffer(maxBufferSize);

			Assert.IsNotNull(buffer);
			Assert.IsTrue(buffer.Length >= maxBufferSize, "buffer.Length >= maxBufferSize");
		}

		[TestMethod]
		public void TakeBuffer10()
		{
			var maxBufferSize = 10;
			var bufferManager = BufferManager.CreateBufferManager(100, maxBufferSize);
			var buffer = bufferManager.TakeBuffer(maxBufferSize);

			Assert.IsNotNull(buffer);
			Assert.IsTrue(buffer.Length >= maxBufferSize, "buffer.Length >= maxBufferSize");
		}

		[TestMethod]
		public void TakeSmallBuffer()
		{
			var maxBufferSize = 1024;
			var bufferSize = 8;
			var bufferManager = BufferManager.CreateBufferManager(100, maxBufferSize);
			var buffer = bufferManager.TakeBuffer(bufferSize);

			Assert.IsNotNull(buffer);
			Assert.IsTrue(buffer.Length >= bufferSize, "buffer.Length >= bufferSize");
		}

		[TestMethod]
		public void ReturnBuffer()
		{
			var maxBufferSize = 1024;
			var bufferManager = BufferManager.CreateBufferManager(100, maxBufferSize);
			var buffer = bufferManager.TakeBuffer(maxBufferSize);

			Assert.IsNotNull(buffer);

			bufferManager.ReturnBuffer(buffer);
		}

		[TestMethod]
		public void ReturnSmallBuffer()
		{
			var maxBufferSize = 1024;
			var bufferSize = 8;
			var bufferManager = BufferManager.CreateBufferManager(100, maxBufferSize);
			var buffer = bufferManager.TakeBuffer(bufferSize);

			Assert.IsNotNull(buffer);

			bufferManager.ReturnBuffer(buffer);
		}
	}
}
