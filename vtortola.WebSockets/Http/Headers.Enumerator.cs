/*
	Copyright (c) 2017 Denis Zykov
	License: https://opensource.org/licenses/MIT
*/
using System;
using System.Collections;
using System.Collections.Generic;

namespace vtortola.WebSockets.Http
{
	partial class Headers<KnownHeaderT>
    {
		public class Enumerator : IEnumerator<KeyValuePair<string, ValueCollection>>
		{
			private readonly Headers<KnownHeaderT> headers;
			private Dictionary<string, ValueCollection>.Enumerator customHeadersEnumerator;
			private int knownHeadersIndex;
			private bool useCustomHeadersEnumerator;
			private int version;

			public KeyValuePair<string, ValueCollection> Current { get; private set; }
			object IEnumerator.Current => this.Current;

			public Enumerator(Headers<KnownHeaderT> headers)
			{
				if (headers == null) throw new ArgumentNullException(nameof(headers), "headersDictionary != null");

				this.headers = headers;
				this.Reset();
			}

			public bool MoveNext()
			{
				if (this.version != this.headers.version) throw new InvalidOperationException("Collection was modified. Enumeration operation may not execute.");

				var knownHeaders = this.headers.knownHeaders;
				if (knownHeaders != null && this.knownHeadersIndex < knownHeaders.Length)
				{
					for (var i = this.knownHeadersIndex; i < knownHeaders.Length; i++)
					{
						this.knownHeadersIndex++;
						var value = knownHeaders[i];
						if (value.IsEmpty) continue;

						this.Current = new KeyValuePair<string, ValueCollection>(GetKnownHeaderName(i), value);
						return true;
					}
				}

				if (!this.useCustomHeadersEnumerator) return false;

				while (this.customHeadersEnumerator.MoveNext())
				{
					var value = this.customHeadersEnumerator.Current;
					if (value.Value.IsEmpty) continue;

					this.Current = value;
					return true;
				}

				return false;
			}
			public void Reset()
			{
				if (this.useCustomHeadersEnumerator) this.customHeadersEnumerator.Dispose();

				this.knownHeadersIndex = 0;
				this.useCustomHeadersEnumerator = this.headers.customHeaders != null;
				this.customHeadersEnumerator = this.headers.customHeaders?.GetEnumerator() ?? default(Dictionary<string, ValueCollection>.Enumerator);
				this.version = this.headers.version;
			}

			public void Dispose()
			{
				if (this.useCustomHeadersEnumerator) this.customHeadersEnumerator.Dispose();
			}
		}
	}
}