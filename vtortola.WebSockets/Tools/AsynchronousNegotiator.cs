using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace vtortola.WebSockets.Tools
{
    public sealed class NegotiationResult<TOut>
        where TOut : class
    {
        public TOut Result { get; internal set; }
        public ExceptionDispatchInfo Error { get; internal set; }
    }

    public abstract class AsynchronousNegotiator<TIn, TOut>:IDisposable
        where TIn : class
        where TOut : class
    {
        readonly BufferBlock<TIn> _in;
        readonly BufferBlock<NegotiationResult<TOut>> _out;
        readonly CancellationToken _cancellation;
        readonly SemaphoreSlim _semaphore;
        readonly TimeSpan _negotiationTimeout;

        public AsynchronousNegotiator(Int32 boundedCapacity, Int32 degreeOfParalellism, TimeSpan negotiationTimeout, CancellationToken cancellation)
        {
            _cancellation = cancellation;
            _negotiationTimeout = negotiationTimeout;
            _semaphore = new SemaphoreSlim(degreeOfParalellism);
            var options = new DataflowBlockOptions() { BoundedCapacity = boundedCapacity, CancellationToken = cancellation };
            _in = new BufferBlock<TIn>(options);
            _out = new BufferBlock<NegotiationResult<TOut>>(options);
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
        private async Task ProcessThroughGateAsync(TIn input)
        {
            _semaphore.Wait(_cancellation);
            Exception error = null;
            TOut result = null;
            try
            {
                Task timeout = Task.Delay(_negotiationTimeout);
                Task<TOut> processing = ProcessAsync(input);
                await Task.WhenAny(timeout, processing);
                if (processing.IsCompleted)
                    result = await processing;
                else
                    CancelNegotiation(input);
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                if (error != null)
                    _out.Post(new NegotiationResult<TOut>() { Error = ExceptionDispatchInfo.Capture(error)});
                else if(result !=null)
                    _out.Post(new NegotiationResult<TOut>() { Result = result });
                _semaphore.Release(1);
            }
        }

        protected abstract Task<TOut> ProcessAsync(TIn input);

        protected abstract void CancelNegotiation(TIn input);

        public void Post(TIn item)
        {
            _in.Post(item);
        }

        public Task<NegotiationResult<TOut>> ReceiveAsync(CancellationToken cancellation)
        {
            return _out.ReceiveAsync(cancellation);
        }
        protected virtual void Dispose(Boolean disposing)
        {
            if (disposing)
                GC.SuppressFinalize(this);
            _semaphore.Dispose();
        }
        public void Dispose()
        {
            Dispose(true);
        }

        ~AsynchronousNegotiator()
        {
            Dispose(false);
        }
    }
}
