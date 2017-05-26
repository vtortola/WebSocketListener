/*
	Copyright (c) 2017 Denis Zykov
	License: https://opensource.org/licenses/MIT
*/
using System;
using System.Collections.Concurrent;
using System.Threading;

#pragma warning disable 420

namespace vtortola.WebSockets.Tools
{
    internal sealed class ObjectPool<T> where T : class
    {
        public readonly Func<T> ConstructFn;

        private readonly ConcurrentQueue<T> items;

        private readonly int sizeLimit;
        private volatile int count;
        public readonly Action<T> ReturnFunction;
        public readonly Func<T> TakeFunction;

        public ObjectPool(Func<T> constructor, int poolSizeLimit = 1000)
        {
            if (constructor == null) throw new ArgumentNullException(nameof(constructor), "constructor != null");
            if (poolSizeLimit <= 0) throw new ArgumentOutOfRangeException(nameof(poolSizeLimit), "poolLimit > 0");

            this.sizeLimit = poolSizeLimit;
            this.items = new ConcurrentQueue<T>();
            this.ConstructFn = constructor;
            this.TakeFunction = this.Take;
            this.ReturnFunction = this.Return;
        }

        public void Clear()
        {
            var item = default(T);
            while (this.items.TryDequeue(out item))
            {
                /* no body */
            }
        }

        public T Take()
        {
            var item = default(T);
            if (!this.TryTake(out item)) item = this.ConstructFn();

            return item;
        }
        public bool TryTake(out T item)
        {
            while (this.items.TryDequeue(out item))
            {
                Interlocked.Decrement(ref this.count);
                return true;
            }

            item = default(T);
            return false;
        }
        public void Return(T item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            this.items.Enqueue(item);
        }
        public bool TryReturn(T item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            if (this.count >= this.sizeLimit) return false;

            this.items.Enqueue(item);
            Interlocked.Increment(ref this.count);
            return true;
        }

    }
}
