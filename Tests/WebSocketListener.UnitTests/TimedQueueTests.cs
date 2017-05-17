using System;
using System.Diagnostics;
using System.Threading;
using vtortola.WebSockets.Threading;
using Xunit;
using Xunit.Abstractions;

namespace vtortola.WebSockets.UnitTests
{
    public sealed class TimedQueueTests
    {
        private readonly TestLogger logger;

        public TimedQueueTests(ITestOutputHelper output)
        {
            this.logger = new TestLogger(output);
        }

        [Theory]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(2000)]
        [InlineData(3000)]
        public void SubscribeAndDispatch(int milliseconds)
        {
            var sw = Stopwatch.StartNew();
            var timedQueue = new CancellationQueue(TimeSpan.FromMilliseconds(milliseconds / 2.0));
            var subscriptions = 0;
            var hits = 0;
            while (sw.ElapsedMilliseconds < milliseconds)
            {
                timedQueue.GetSubscriptionList().Token.Register(() => Interlocked.Increment(ref hits));
                subscriptions++;
                Thread.Sleep(10);
            }

            sw.Reset();
            while (sw.ElapsedMilliseconds < milliseconds && subscriptions != hits)
                Thread.Sleep(10);

            this.logger.Debug($"[TEST] subscriptions: {subscriptions}, hits: {hits}.");

            Assert.Equal(subscriptions, hits);
        }
    }
}
