/*
	Copyright (c) 2017 Denis Zykov
	License: https://opensource.org/licenses/MIT
*/
using System;

namespace vtortola.WebSockets.Async
{
    internal struct AsyncConditionVariable
    {
        private readonly AsyncConditionSource source;

        public bool IsSet => this.source != null && this.source.IsSet;

        public AsyncConditionVariable(AsyncConditionSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source), "source != null");

            this.source = source;
        }

        public AsyncConditionSource.Awaiter GetAwaiter()
        {
            if (this.source == null) throw new InvalidOperationException();

            return this.source.GetAwaiter();
        }

        public override string ToString()
        {
            return $"Condition: {this.IsSet}";
        }
    }
}