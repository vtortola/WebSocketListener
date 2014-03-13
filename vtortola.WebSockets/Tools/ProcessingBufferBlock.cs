using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace vtortola.WebSockets.Tools
{
    public sealed class ProcessingResult<TOut>
        where TOut : class
    {
        public TOut Result { get; internal set; }
        public Exception Error { get; internal set; }
    }

    public abstract class ProcessingBufferBlock<TIn, TOut>
        where TIn : class
        where TOut : class
    {
        readonly BufferBlock<TIn> _in;
        readonly BufferBlock<ProcessingResult<TOut>> _out;
        readonly CancellationToken _cancellation;
        readonly SemaphoreSlim _semaphore;

        public ProcessingBufferBlock(Int32 boundedCapacity, Int32 degreeOfParalellism, CancellationToken cancellation)
        {
            _cancellation = cancellation;
            _semaphore = new SemaphoreSlim(degreeOfParalellism);
            var options = new DataflowBlockOptions() { BoundedCapacity = boundedCapacity, CancellationToken = cancellation };
            _in = new BufferBlock<TIn>(options);
            _out = new BufferBlock<ProcessingResult<TOut>>(options);
            StartReadingAsync();
        }

        private async Task StartReadingAsync()
        {
            await Task.Yield();
            while (!_cancellation.IsCancellationRequested)
            {
                var incoming = await _in.ReceiveAsync(_cancellation);
                ProcessThroughGateAsync(incoming);
            }
        }

        Int32 counter = 0;
        private async Task ProcessThroughGateAsync(TIn input)
        {
            _semaphore.Wait(_cancellation);
            Interlocked.Increment(ref counter);
            Exception error = null;
            TOut result = null;
            try
            {
                result = await ProcessAsync(input);
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                Interlocked.Decrement(ref counter);
                if (result != null || error != null)
                    _out.Post(new ProcessingResult<TOut>() { Error = error, Result = result });
                _semaphore.Release(1);
            }
        }

        protected abstract Task<TOut> ProcessAsync(TIn input);

        public void Post(TIn item)
        {
            _in.Post(item);
        }

        public Task<ProcessingResult<TOut>> ReceiveAsync(CancellationToken cancellation)
        {
            return _out.ReceiveAsync(cancellation);
        }
    }
}
