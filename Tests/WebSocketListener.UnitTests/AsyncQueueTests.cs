using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Threading;
using vtortola.WebSockets.Tools;
using Xunit;
using Xunit.Abstractions;

namespace WebSocketListener.UnitTests
{
    public sealed class AsyncQueueTests
    {
        private readonly TestLogger logger;

        public AsyncQueueTests(ITestOutputHelper output)
        {
            this.logger = new TestLogger(output);
        }

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
            var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, TaskScheduler = TaskScheduler.Default };
            var items = Enumerable.Range(0, count).ToArray();
            var expectedSum = items.Sum();
            Parallel.For(0, count, options, i => Assert.True(asyncQueue.TrySend(i), "fail to send"));

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
            var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
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
                    this.logger.Debug(value.ToString());
                    Interlocked.Add(ref actualSum, value);
                    if (Interlocked.Increment(ref ct) == count)
                        return;
                }
            })();

            for (var i = 0; i < count; i++)
                Assert.True(asyncQueue.TrySend(i), "fail to send");

            await receiveTask.ConfigureAwait(false);

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
            var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, TaskScheduler = TaskScheduler.Default };
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
                    Interlocked.Add(ref actualSum, value);
                    if (Interlocked.Increment(ref ct) == count)
                        return;
                }
            })();

            Parallel.For(0, count, options, i => Assert.True(asyncQueue.TrySend(i), "fail to send"));

            await receiveTask.ConfigureAwait(false);

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
        [InlineData(3)]
        [InlineData(4)]
        public async Task FastSendAndSlowReceiveAsync(int seconds)
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
                    await Task.Delay(2).ConfigureAwait(false);
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
        [InlineData(3)]
        [InlineData(4)]
        public async Task SlowSendAndFastReceiveAsync(int seconds)
        {
            var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
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
                    await Task.Delay(2).ConfigureAwait(false);
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

            var all = asyncQueue.CloseAndReceiveAll(closeError: new OperationCanceledException());

            if (await Task.WhenAny(timeout, recvTask).ConfigureAwait(false) == timeout)
                throw new TimeoutException();

            Assert.Empty(all);
        }

        [Theory]
        [InlineData(80000)]
        [InlineData(100000)]
        [InlineData(120000)]
        [InlineData(150000)]
        public async Task ParallelSendAndCloseReceiveAll(int count)
        {
            var cancellationSource = new CancellationTokenSource();
            var asyncQueue = new AsyncQueue<int>();
            var options = new ParallelOptions { CancellationToken = cancellationSource.Token, MaxDegreeOfParallelism = Environment.ProcessorCount / 2, TaskScheduler = TaskScheduler.Default };
            var items = new ConcurrentQueue<int>(Enumerable.Range(0, count));

            var sendTask = Task.Factory.StartNew(() => Parallel.For(0, count, options, i =>
            {
                var item = default(int);
                if (items.TryDequeue(out item))
                    if (asyncQueue.TrySend(item) == false)
                        items.Enqueue(item);
            }));

            await Task.Delay(1).ConfigureAwait(false);

            var itemsInAsyncQueue = asyncQueue.CloseAndReceiveAll(); // deny TrySend
            cancellationSource.Cancel(); // stop parallel for

            await sendTask.IgnoreFaultOrCancellation().ConfigureAwait(false);

            var actualCount = items.Count + itemsInAsyncQueue.Count;

            this.logger.Debug($"[TEST] en-queued: {itemsInAsyncQueue.Count}, total: {count}");

            Assert.Equal(count, actualCount);
        }
    }
}
