using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Threading;
using vtortola.WebSockets.Tools;
using Xunit;

namespace WebSocketListener.UnitTests
{
    public sealed class AsyncQueueTests
    {
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(10)]
        [InlineData(50)]
        [InlineData(100)]
        [InlineData(1000)]
        public void TrySendAndTryReceive(int count)
        {
            var asyncQueue = new AsyncQueue<int>();
            for (var i = 0; i < count; i++)
                Assert.True(asyncQueue.TrySend(i), "fail to send");

            for (var i = 0; i < count; i++)
            {
                var value = default(int);
                Assert.True(asyncQueue.TryReceive(out value), "fail to receive");
                Assert.Equal(i, value);
            }
        }

        [Theory]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(10)]
        [InlineData(50)]
        [InlineData(100)]
        [InlineData(1000)]
        public void ParallelSendAndTryReceive(int count)
        {
            var asyncQueue = new AsyncQueue<int>();
            var items = Enumerable.Range(0, count).ToArray();
            var expectedSum = items.Sum();
            var result = Parallel.For(0, count, i => Assert.True(asyncQueue.TrySend(i), "fail to send"));
            while (result.IsCompleted == false)
                Thread.Sleep(10);

            var actualSum = 0;
            var value = default(int);
            while (asyncQueue.TryReceive(out value))
                actualSum += value;

            Assert.Equal(expectedSum, actualSum);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(10)]
        [InlineData(50)]
        [InlineData(100)]
        [InlineData(1000)]
        public async Task TrySendAndReceiveAsync(int count)
        {
            var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var asyncQueue = new AsyncQueue<int>();
            var items = Enumerable.Range(0, count).ToArray();
            var expectedSum = items.Sum();

            var actualSum = 0;
            var ct = 0;
            var receiveTask = new Func<Task>(async () =>
            {
                await Task.Yield();

                while (cancellation.IsCancellationRequested == false)
                {
                    var value = await asyncQueue.ReceiveAsync(cancellation.Token).ConfigureAwait(false);
                    actualSum += value;
                    ct++;
                    if (ct == count)
                        return;
                }
            })().IgnoreFaultOrCancellation().ConfigureAwait(false);

            for (var i = 0; i < count; i++)
                Assert.True(asyncQueue.TrySend(i), "fail to send");

            await receiveTask;

            Assert.Equal(expectedSum, actualSum);
        }

        [Theory]
        [InlineData(2)]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(10)]
        [InlineData(50)]
        [InlineData(100)]
        [InlineData(1000)]
        public async Task ParallelSendAndReceiveAsync(int count)
        {
            var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var asyncQueue = new AsyncQueue<int>();
            var items = Enumerable.Range(0, count).ToArray();
            var expectedSum = items.Sum();

            var actualSum = 0;
            var ct = 0;
            var receiveTask = new Func<Task>(async () =>
            {
                await Task.Yield();

                while (cancellation.IsCancellationRequested == false)
                {
                    var value = await asyncQueue.ReceiveAsync(cancellation.Token).ConfigureAwait(false);
                    actualSum += value;
                    ct++;
                    if (ct == count)
                        return;
                }
            })().IgnoreFaultOrCancellation().ConfigureAwait(false);

            var result = Parallel.For(0, count, i => Assert.True(asyncQueue.TrySend(i), "fail to send"));
            while (result.IsCompleted == false)
                Thread.Sleep(10);

            await receiveTask;

            Assert.Equal(expectedSum, actualSum);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(4)]
        public async Task BoundedInfiniteSendAndReceiveAsync(int seconds)
        {
            var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
            var asyncQueue = new AsyncQueue<int>(10);
            var expectedValue = (int)(DateTime.Now.Ticks % int.MaxValue);

            var ct = 0;
            var receiveTask = new Func<Task>(async () =>
            {
                await Task.Yield();

                while (cancellation.IsCancellationRequested == false)
                {
                    var actual = await asyncQueue.ReceiveAsync(cancellation.Token).ConfigureAwait(false);
                    ct++;
                    Assert.Equal(expectedValue, actual);
                }
            })().IgnoreFaultOrCancellation().ConfigureAwait(false);

            var sendTask = new Func<Task>(async () =>
            {
                await Task.Yield();
                while (cancellation.IsCancellationRequested == false)
                {
                    asyncQueue.TrySend(expectedValue);
                    await Task.Delay(10).ConfigureAwait(false);
                }
            })().IgnoreFaultOrCancellation().ConfigureAwait(false);

            while (cancellation.IsCancellationRequested == false)
                await Task.Delay(10).ConfigureAwait(false);

            await receiveTask;
            await sendTask;

            Assert.NotEqual(ct, 0);
        }


        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(10)]
        [InlineData(50)]
        [InlineData(100)]
        [InlineData(1000)]
        public async Task FastSendAndSlowReceiveAsync(int count)
        {
            var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var asyncQueue = new AsyncQueue<int>(10);
            var expectedValue = (int)(DateTime.Now.Ticks % int.MaxValue);

            var ct = 0;
            var receiveTask = new Func<Task>(async () =>
            {
                await Task.Yield();

                while (cancellation.IsCancellationRequested == false)
                {
                    await Task.Delay(5).ConfigureAwait(false);
                    var actual = await asyncQueue.ReceiveAsync(cancellation.Token).ConfigureAwait(false);
                    ct++;
                    Assert.Equal(expectedValue, actual);
                }
            })().IgnoreFaultOrCancellation().ConfigureAwait(false);

            var sendTask = new Func<Task>(async () =>
            {
                await Task.Yield();
                while (cancellation.IsCancellationRequested == false)
                {
                    asyncQueue.TrySend(expectedValue);
                    await Task.Delay(1).ConfigureAwait(false);
                }
            })().IgnoreFaultOrCancellation().ConfigureAwait(false);

            while (cancellation.IsCancellationRequested == false)
                await Task.Delay(10).ConfigureAwait(false);

            await receiveTask;
            await sendTask;

            Assert.NotEqual(ct, 0);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(10)]
        [InlineData(50)]
        [InlineData(100)]
        [InlineData(1000)]
        public async Task SlowSendAndFastReceiveAsync(int count)
        {
            var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var asyncQueue = new AsyncQueue<int>(10);
            var expectedValue = (int)(DateTime.Now.Ticks % int.MaxValue);

            var ct = 0;
            var receiveTask = new Func<Task>(async () =>
            {
                await Task.Yield();

                while (cancellation.IsCancellationRequested == false)
                {
                    await Task.Delay(1).ConfigureAwait(false);
                    var actual = await asyncQueue.ReceiveAsync(cancellation.Token).ConfigureAwait(false);
                    ct++;
                    Assert.Equal(expectedValue, actual);
                }
            })().IgnoreFaultOrCancellation().ConfigureAwait(false);

            var sendTask = new Func<Task>(async () =>
            {
                await Task.Yield();
                while (cancellation.IsCancellationRequested == false)
                {
                    asyncQueue.TrySend(expectedValue);
                    await Task.Delay(10).ConfigureAwait(false);
                }
            })().IgnoreFaultOrCancellation().ConfigureAwait(false);

            while (cancellation.IsCancellationRequested == false)
                await Task.Delay(10).ConfigureAwait(false);

            await receiveTask;
            await sendTask;

            Assert.NotEqual(ct, 0);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(10)]
        [InlineData(50)]
        [InlineData(100)]
        [InlineData(1000)]
        public async Task AsyncSendAndClose(int count)
        {
            var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var asyncQueue = new AsyncQueue<int>();

            var sendValue = 0;
            var sendTask = new Func<Task>(async () =>
            {

                await Task.Yield();
                while (cancellation.IsCancellationRequested == false)
                {
                    if (asyncQueue.TrySend(sendValue++) == false)
                        return;
                    await Task.Delay(10).ConfigureAwait(false);
                }
            })().IgnoreFaultOrCancellation().ConfigureAwait(false);

            await Task.Delay(count);
            asyncQueue.Close();

            await sendTask;

            var value = default(int);
            var actualSum = 0;
            while (asyncQueue.TryReceive(out value))
                actualSum++;
            var expectedSum = Enumerable.Range(0, sendValue).Sum();

            Assert.NotEqual(expectedSum, actualSum);
            cancellation.Cancel();
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(10)]
        [InlineData(50)]
        [InlineData(100)]
        [InlineData(1000)]
        public async Task AsyncSendAndCloseAndReceiveAll(int count)
        {
            var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var asyncQueue = new AsyncQueue<int>();

            var sendValue = 0;
            var sendTask = new Func<Task>(async () =>
            {

                await Task.Yield();
                while (cancellation.IsCancellationRequested == false)
                {
                    if (asyncQueue.TrySend(sendValue++) == false)
                        return;
                    await Task.Delay(10).ConfigureAwait(false);
                }
            })().IgnoreFaultOrCancellation().ConfigureAwait(false);

            await Task.Delay(count);
            var actualSum = asyncQueue.CloseAndReceiveAll().Sum();

            await sendTask;

            var expectedSum = Enumerable.Range(0, sendValue).Sum();

            Assert.NotEqual(expectedSum, actualSum);
            cancellation.Cancel();
        }

        [Fact]
        public async Task ReceiveAsyncCancellation()
        {
            var asyncQueue = new AsyncQueue<int>();
            var cancellation = new CancellationTokenSource();

            var receiveAsync = asyncQueue.ReceiveAsync(cancellation.Token);
            cancellation.CancelAfter(10);

            var timeout = Task.Delay(1000);
            var recvTask = Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await receiveAsync.ConfigureAwait(false);
            });

            if (await Task.WhenAny(timeout, recvTask).ConfigureAwait(false) == timeout)
                throw new TimeoutException();
        }

        [Fact]
        public async Task ReceiveAsyncCloseCancellation()
        {
            var asyncQueue = new AsyncQueue<int>();
            var cancellation = new CancellationTokenSource(2000);
            var receiveAsync = asyncQueue.ReceiveAsync(cancellation.Token);

            var timeout = Task.Delay(1000);
            var recvTask = Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await receiveAsync.ConfigureAwait(false);
            });

            asyncQueue.Close(new OperationCanceledException());

            if (await Task.WhenAny(timeout, recvTask).ConfigureAwait(false) == timeout)
                throw new TimeoutException();
        }

        [Fact]
        public async Task ReceiveAsyncCloseError()
        {
            var asyncQueue = new AsyncQueue<int>();
            var cancellation = new CancellationTokenSource(2000);
            var receiveAsync = asyncQueue.ReceiveAsync(cancellation.Token);

            var timeout = Task.Delay(1000);
            var recvTask = Assert.ThrowsAsync<IOException>(async () =>
            {
                await receiveAsync.ConfigureAwait(false);
            });

            asyncQueue.Close(new IOException());

            if (await Task.WhenAny(timeout, recvTask).ConfigureAwait(false) == timeout)
                throw new TimeoutException();
        }

        [Fact]
        public async Task ReceiveAsyncCloseReceiveAllCancellation()
        {
            var asyncQueue = new AsyncQueue<int>();
            var cancellation = new CancellationTokenSource(2000);
            var receiveAsync = asyncQueue.ReceiveAsync(cancellation.Token);

            var timeout = Task.Delay(1000);
            var recvTask = Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await receiveAsync.ConfigureAwait(false);
            });

            var all = asyncQueue.CloseAndReceiveAll(closeException: new OperationCanceledException());

            if (await Task.WhenAny(timeout, recvTask).ConfigureAwait(false) == timeout)
                throw new TimeoutException();

            Assert.Empty(all);
        }

    }
}
