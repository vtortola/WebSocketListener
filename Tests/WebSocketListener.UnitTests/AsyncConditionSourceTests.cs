using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Threading;
using Xunit;
using Xunit.Abstractions;

namespace vtortola.WebSockets.UnitTests
{
    public sealed class AsyncConditionSourceTests
    {
        private readonly TestLogger logger;

        public AsyncConditionSourceTests(ITestOutputHelper output)
        {
            this.logger = new TestLogger(output);
        }

        [Theory]
        [InlineData(2)]
        [InlineData(5)]
        [InlineData(10)]
        [InlineData(40)]
        [InlineData(80)]
        [InlineData(100)]
        [InlineData(200)]
        [InlineData(500)]
        [InlineData(1000)]
        public async Task ParallelSetContinuationTest(int subscribers)
        {
            var condition = new AsyncConditionSource();

            var hits = 0;
            var parallelLoopResult = Parallel.For(0, subscribers, i =>
            {
                if (i == subscribers / 2)
                    condition.Set();
                condition.GetAwaiter().OnCompleted(() => Interlocked.Increment(ref hits));
            });

            while (parallelLoopResult.IsCompleted == false)
                await Task.Delay(10).ConfigureAwait(false);

            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 1000 && subscribers != hits)
                Thread.Sleep(10);

            this.logger.Debug($"[TEST] subscribers: {subscribers}, hits: {hits}.");

            Assert.Equal(subscribers, hits);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(5)]
        [InlineData(10)]
        [InlineData(40)]
        [InlineData(80)]
        [InlineData(100)]
        [InlineData(200)]
        [InlineData(500)]
        [InlineData(1000)]
        public async Task BeforeSetContinuationTest(int subscribers)
        {
            var condition = new AsyncConditionSource(isSet: true);

            var hits = 0;
            var parallelLoopResult = Parallel.For(0, subscribers, i =>
            {
                condition.GetAwaiter().OnCompleted(() => Interlocked.Increment(ref hits));
            });

            while (parallelLoopResult.IsCompleted == false)
                await Task.Delay(10).ConfigureAwait(false);

            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 1000 && subscribers != hits)
                Thread.Sleep(10);

            this.logger.Debug($"[TEST] subscribers: {subscribers}, hits: {hits}.");

            Assert.Equal(subscribers, hits);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(5)]
        [InlineData(10)]
        [InlineData(40)]
        [InlineData(80)]
        [InlineData(100)]
        [InlineData(200)]
        [InlineData(500)]
        [InlineData(1000)]
        public async Task AfterSetContinuationTest(int subscribers)
        {
            var condition = new AsyncConditionSource(isSet: false);

            var hits = 0;
            var parallelLoopResult = Parallel.For(0, subscribers, i =>
            {
                condition.GetAwaiter().OnCompleted(() => Interlocked.Increment(ref hits));
            });

            while (parallelLoopResult.IsCompleted == false)
                await Task.Delay(10).ConfigureAwait(false);

            condition.Set();

            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 1000 && subscribers != hits)
                Thread.Sleep(10);

            this.logger.Debug($"[TEST] subscribers: {subscribers}, hits: {hits}.");
            
            Assert.Equal(subscribers, hits);
        }
    }
}
