/*
	Copyright (c) 2017 Denis Zykov
	License: https://opensource.org/licenses/MIT
*/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using vtortola.WebSockets.Tools;

namespace vtortola.WebSockets.Http
{
    [SuppressMessage("ReSharper", "StaticMemberInGenericType")]
    public sealed partial class Headers<KnownHeaderT> : IDictionary<string, string>
    {
        private static readonly KnownHeaderT FirstKnownHeader;
        private static readonly KnownHeaderT LastKnownHeader;
        private static readonly int KnownHeadersCapacity;
        private static readonly string[] KnownHeaderNames;
        private static readonly bool[] KnownHeaderFlags;
        private static readonly string[] KnownHeaderSortedNames;
        private static readonly int[] KnownHeaderSortedValues;
        private static readonly StringComparer KeyComparer;
        private static readonly Func<KnownHeaderT, int> ToInt;
        private static readonly Func<int, KnownHeaderT> FromInt;

        private Dictionary<string, ValueCollection> customHeaders; // by default is null
        private bool isReadOnly;

        private ValueCollection[] knownHeaders; // by default is null
        private int knownHeadersCount;
        private int version;

        public int Count => (this.customHeaders?.Count ?? 0) + this.knownHeadersCount;
        public int FlatCount => (this.customHeaders?.Values.Sum(v => v.Count) ?? 0) + (this.knownHeaders?.Sum(v => v.Count) ?? 0);
        public bool IsReadOnly => this.isReadOnly;

        public string this[KnownHeaderT knownHeader]
        {
            get { return this.Get(knownHeader); }
            set { this.Set(knownHeader, value); }
        }
        public string this[string headerName]
        {
            get { return this.Get(headerName); }
            set { this.Set(headerName, value); }
        }
        string IDictionary<string, string>.this[string key]
        {
            get { return this.Get(key); }
            set
            {
                this.ThrowIfReadOnly();

                this.Set(key, value);
            }
        }
        ICollection<string> IDictionary<string, string>.Keys { get { return this.Select(kv => kv.Key).ToList(); } }
        ICollection<string> IDictionary<string, string>.Values { get { return this.Select(kv => kv.Value).ToList(); } }
        bool ICollection<KeyValuePair<string, string>>.IsReadOnly => this.isReadOnly;

        static Headers()
        {
            const int MAX_ENUM_VALUE = 256;

            if (typeof(KnownHeaderT).IsEnum == false)
                throw new InvalidOperationException("TKnownHeader should be enum type.");
            if (Enum.GetUnderlyingType(typeof(KnownHeaderT)) != typeof(int))
                throw new InvalidOperationException("TKnownHeader should be enum with System.Int32 underlying type.");

            FromInt = (Func<int, KnownHeaderT>)EnumHelper<KnownHeaderT>.FromNumber;
            ToInt = (Func<KnownHeaderT, int>)EnumHelper<KnownHeaderT>.ToNumber;

            KeyComparer = StringComparer.OrdinalIgnoreCase;

            var fields = typeof(KnownHeaderT).GetFields(BindingFlags.Public | BindingFlags.Static);
            var names = new string[fields.Length];
            var values = new int[fields.Length];
            var atomic = new bool[fields.Length];
            for (var i = 0; i < fields.Length; i++)
            {
                var headerAttribute = fields[i].GetCustomAttributes(typeof(HeaderAttribute), false).Cast<HeaderAttribute>().FirstOrDefault();
                var name = headerAttribute?.Name ?? fields[i].Name;
                var value = Convert.ToInt32(fields[i].GetRawConstantValue());
                var isAtomic = headerAttribute?.IsAtomic ?? false;

                names[i] = name;
                values[i] = value;
                atomic[i] = isAtomic;
            }

            Array.Sort(values, names); // sort by order

            KnownHeadersCapacity = names.Length;
            FirstKnownHeader = FromInt(values.Min());
            LastKnownHeader = FromInt(values.Max());

            if (ToInt(LastKnownHeader) > MAX_ENUM_VALUE)
                throw new InvalidOperationException($"Max acceptable value for enum TKnownHeader is {MAX_ENUM_VALUE}.");

            KnownHeaderNames = names;
            KnownHeaderFlags = atomic;

            var sortedNames = new string[names.Length];
            var sortedValues = new int[values.Length];
            names.CopyTo(sortedNames, 0);
            values.CopyTo(sortedValues, 0);
            Array.Sort(sortedNames, sortedValues, KeyComparer);

            KnownHeaderSortedNames = sortedNames;
            KnownHeaderSortedValues = sortedValues;
        }
        public Headers() { }
        public Headers(NameValueCollection nameValueCollection)
        {
            if (nameValueCollection == null) throw new ArgumentNullException(nameof(nameValueCollection), "nameValueCollection != null");

            this.AddMany(nameValueCollection);
        }
        public Headers(StringDictionary stringDictionary)
        {
            if (stringDictionary == null) throw new ArgumentNullException(nameof(stringDictionary), "stringDictionary != null");

            foreach (string key in stringDictionary.Keys)
                this.Add(key, stringDictionary[key]);
        }
        public Headers(IDictionary<string, string> dictionary)
        {
            if (dictionary == null) throw new ArgumentNullException(nameof(dictionary), "dictionary != null");

            this.AddMany(dictionary);
        }
        public Headers(IDictionary<string, IEnumerable<string>> dictionary)
        {
            if (dictionary == null) throw new ArgumentNullException(nameof(dictionary), "dictionary != null");

            this.AddMany(dictionary);
        }

        public void Add(string headerName, string value)
        {
            this.ThrowIfReadOnly();

            var knownHeaderIndex = GetKnownHeaderValue(headerName);
            if (knownHeaderIndex >= 0 && knownHeaderIndex <= ToInt(LastKnownHeader))
            {
                this.Add(FromInt(knownHeaderIndex), value);
            }
            else
            {
                var values = this.GetCustomHeader(headerName);
                values += new ValueCollection(value);
                this.SetCustomHeader(headerName, values);
            }
        }
        public void Add(KnownHeaderT knownHeader, string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value), "value != null");

            this.ThrowIfReadOnly();

            var values = this.GetKnownHeader(ToInt(knownHeader));
            values += new ValueCollection(value);
            this.SetKnownHeader(ToInt(knownHeader), values);
        }
        public void AddMany(NameValueCollection nameValueCollection)
        {
            if (nameValueCollection == null) throw new ArgumentNullException(nameof(nameValueCollection));

            for (var i = 0; i < nameValueCollection.Count; i++)
                foreach (var value in nameValueCollection.GetValues(i) ?? new string[0])
                    this.Add(nameValueCollection.GetKey(i), value);
        }
        public void AddMany(IEnumerable<KeyValuePair<string, string>> pairs)
        {
            if (pairs == null) throw new ArgumentNullException(nameof(pairs));

            foreach (var kv in pairs)
                this.Add(kv.Key, kv.Value);
        }
        public void AddMany(IEnumerable<KeyValuePair<string, IEnumerable<string>>> pairs)
        {
            if (pairs == null) throw new ArgumentNullException(nameof(pairs));

            foreach (var kv in pairs)
                foreach (var value in kv.Value)
                    this.Add(kv.Key, value);
        }
        public void Set(string headerName, string value)
        {
            if (headerName == null) throw new ArgumentNullException(nameof(headerName), "headerName != null");
            if (value == null) throw new ArgumentNullException(nameof(value), "value != null");

            this.Set(headerName, new ValueCollection(value));
        }
        public void Set(KnownHeaderT knownHeader, string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value), "value != null");

            this.Set(knownHeader, new ValueCollection(value));
        }
        public void Set(string headerName, IEnumerable<string> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values), "values != null");
            if (headerName == null) throw new ArgumentNullException(nameof(headerName), "headerName != null");

            this.ThrowIfReadOnly();

            var knownHeaderIndex = GetKnownHeaderValue(headerName);
            if (knownHeaderIndex >= 0 && knownHeaderIndex <= ToInt(LastKnownHeader)) this.SetKnownHeader(knownHeaderIndex, new ValueCollection(values));
            else this.SetCustomHeader(headerName, new ValueCollection(values));
        }
        public void Set(string headerName, ValueCollection values)
        {
            if (headerName == null) throw new ArgumentNullException(nameof(headerName), "headerName != null");

            this.ThrowIfReadOnly();

            var knownHeaderIndex = GetKnownHeaderValue(headerName);
            if (knownHeaderIndex >= 0 && knownHeaderIndex <= ToInt(LastKnownHeader)) this.SetKnownHeader(knownHeaderIndex, values);
            else this.SetCustomHeader(headerName, values);
        }
        public void Set(KnownHeaderT knownHeader, IEnumerable<string> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values), "values != null");

            this.ThrowIfReadOnly();

            this.SetKnownHeader(ToInt(knownHeader), new ValueCollection(values));
        }
        public void Set(KnownHeaderT knownHeader, ValueCollection values)
        {
            this.ThrowIfReadOnly();

            this.SetKnownHeader(ToInt(knownHeader), values);
        }
        public bool TryParseAndAdd(string header)
        {
            if (header == null) throw new ArgumentNullException(nameof(header));

            var separatorIndex = header.IndexOf(':', 0);
            if (separatorIndex == -1 || separatorIndex == 0)
                return false; // header is invalid

            var headerStartIndex = 0;
            var headerLength = separatorIndex - headerStartIndex;
            HeadersHelper.TrimInPlace(header, ref headerStartIndex, ref headerLength);

            var valueStartIndex = separatorIndex + 1;
            var valueLength = header.Length - valueStartIndex;
            HeadersHelper.TrimInPlace(header, ref valueStartIndex, ref valueLength);

            if (headerLength == 0 || valueLength == 0)
                return false; // header name or value is empty

            var headerName = header.Substring(headerStartIndex, headerLength);
            var knownHeaderValue = GetKnownHeaderValue(headerName);
            if (knownHeaderValue >= 0)
            {
                var value = header.Substring(valueStartIndex, valueLength);
                if (KnownHeaderFlags[knownHeaderValue]) // it is atomic header, so don't split it
                {
                    this.Add(FromInt(knownHeaderValue), value);
                }
                else
                {
                    foreach (var headerValue in TrimAndSplit(header, valueStartIndex, valueLength))
                        this.Add(headerName, headerValue);
                }
            }
            else
            {
                foreach (var headerValue in TrimAndSplit(header, valueStartIndex, valueLength))
                    this.Add(headerName, headerValue);
            }
            return true;

        }

        private static ValueCollection TrimAndSplit(string valueString, int startIndex, int count)
        {
            if (valueString == null) throw new ArgumentNullException(nameof(valueString));
            if (startIndex < 0 || startIndex >= valueString.Length) throw new ArgumentOutOfRangeException(nameof(startIndex));
            if (count < 0 || startIndex + count > valueString.Length) throw new ArgumentOutOfRangeException(nameof(count));

            const char VALUE_SEPARATOR = ',';

            var values = new ValueCollection();

            var valueStartIndex = startIndex;
            var valueLength = count;
            var valueSeparatorIndex = valueString.IndexOf(VALUE_SEPARATOR, valueStartIndex, count);
            while (valueSeparatorIndex >= 0)
            {
                valueLength = valueSeparatorIndex - valueStartIndex;
                HeadersHelper.TrimInPlace(valueString, ref valueStartIndex, ref valueLength);
                if (valueLength > 0)
                    values += new ValueCollection(valueString.Substring(valueStartIndex, valueLength));
                valueStartIndex = valueSeparatorIndex + 1;
                valueLength = startIndex + count - valueStartIndex;
                valueSeparatorIndex = valueString.IndexOf(VALUE_SEPARATOR, valueStartIndex, valueLength);
            }
            HeadersHelper.TrimInPlace(valueString, ref valueStartIndex, ref valueLength);
            if (valueLength > 0)
                values += new ValueCollection(valueString.Substring(valueStartIndex, valueLength));

            return values;
        }


        public string Get(string headerName)
        {
            if (headerName == null) throw new ArgumentNullException(nameof(headerName), "headerName != null");

            var values = this.GetHeader(headerName);
            return values.ToString();
        }
        public string Get(KnownHeaderT knownHeader)
        {
            var values = this.GetKnownHeader(ToInt(knownHeader));
            return values.ToString();
        }
        public ValueCollection GetValues(string headerName)
        {
            if (headerName == null) throw new ArgumentNullException(nameof(headerName), "headerName != null");

            var values = this.GetHeader(headerName);
            return values;
        }
        public ValueCollection GetValues(KnownHeaderT knownHeader)
        {
            var values = this.GetKnownHeader(ToInt(knownHeader));
            return values;
        }

        public void SetReadOnly()
        {
            this.isReadOnly = true;
        }
        public void Clear()
        {
            this.ThrowIfReadOnly();

            if (this.knownHeaders != null) Array.Clear(this.knownHeaders, 0, this.knownHeaders.Length);
            this.knownHeadersCount = 0;

            this.customHeaders?.Clear();
        }
        public void Remove(string headerName)
        {
            if (headerName == null) throw new ArgumentNullException(nameof(headerName), "headerName != null");

            this.ThrowIfReadOnly();

            var knownHeaderIndex = GetKnownHeaderValue(headerName);
            if (knownHeaderIndex >= 0 && knownHeaderIndex <= ToInt(LastKnownHeader)) this.SetKnownHeader(knownHeaderIndex, default(ValueCollection));
            else this.SetCustomHeader(headerName, default(ValueCollection));
        }
        public void Remove(KnownHeaderT knownHeader)
        {
            this.ThrowIfReadOnly();

            this.SetKnownHeader(ToInt(knownHeader), default(ValueCollection));
        }

        public bool Contains(string headerName)
        {
            var values = this.GetHeader(headerName);
            return values.IsEmpty == false;
        }
        public bool Contains(KnownHeaderT knownHeader)
        {
            var values = this.GetKnownHeader(ToInt(knownHeader));
            return values.IsEmpty == false;
        }
        public bool TryGetValue(string key, out string value)
        {
            var values = this.GetHeader(key);
            value = values.ToString();
            return values.IsEmpty;
        }

        bool IDictionary<string, string>.ContainsKey(string key)
        {
            return this.Contains(key);
        }
        bool IDictionary<string, string>.Remove(string key)
        {
            this.ThrowIfReadOnly();

            var contains = this.Contains(key);
            this.Remove(key);
            return contains;
        }

        void ICollection<KeyValuePair<string, string>>.Add(KeyValuePair<string, string> item)
        {
            this.ThrowIfReadOnly();

            this.Add(item.Key, item.Value);
        }
        bool ICollection<KeyValuePair<string, string>>.Contains(KeyValuePair<string, string> item)
        {
            var headerName = item.Key;
            var values = this.GetHeader(headerName);

            return values.Contains(item.Value);
        }
        void ICollection<KeyValuePair<string, string>>.CopyTo(KeyValuePair<string, string>[] array, int arrayIndex)
        {
            foreach (var kv in this as IEnumerable<KeyValuePair<string, string>>)
            {
                if (arrayIndex >= array.Length) return;

                array[arrayIndex++] = kv;
            }
        }
        bool ICollection<KeyValuePair<string, string>>.Remove(KeyValuePair<string, string> item)
        {
            this.ThrowIfReadOnly();

            var value = this.Get(item.Key);
            if (item.Value != value) return false;

            this.Remove(item.Key);
            return true;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            var result = new DictionaryEntry[this.FlatCount];
            var i = 0;
            foreach (var kv in this)
            {
                foreach (var value in kv.Value)
                    result[i++] = new DictionaryEntry(kv.Key, value);
            }

            return result.GetEnumerator();
        }
        IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator()
        {
            var result = new KeyValuePair<string, string>[this.FlatCount];
            var i = 0;
            foreach (var kv in this)
            {
                foreach (var value in kv.Value)
                    result[i++] = new KeyValuePair<string, string>(kv.Key, value);
            }

            return ((IEnumerable<KeyValuePair<string, string>>)result).GetEnumerator();
        }
        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        private void SetKnownHeader(int knownHeaderValue, ValueCollection valueCollection)
        {
            if (knownHeaderValue < ToInt(FirstKnownHeader) || knownHeaderValue > ToInt(LastKnownHeader)) throw new ArgumentOutOfRangeException(nameof(knownHeaderValue));

            if (this.knownHeaders == null) this.knownHeaders = new ValueCollection[KnownHeadersCapacity];

            var oldValue = this.knownHeaders[knownHeaderValue];
            this.knownHeaders[knownHeaderValue] = valueCollection;

            if (oldValue.IsEmpty && !valueCollection.IsEmpty) this.knownHeadersCount++;
            else if (!oldValue.IsEmpty && valueCollection.IsEmpty) this.knownHeadersCount--;
            this.version++;
        }
        private void SetCustomHeader(string headerName, ValueCollection valueCollection)
        {
            if (headerName == null) throw new ArgumentNullException(nameof(headerName), "headerName != null");

            if (this.customHeaders == null) this.customHeaders = new Dictionary<string, ValueCollection>(KeyComparer);

            if (!valueCollection.IsEmpty) this.customHeaders[headerName] = valueCollection;
            else this.customHeaders.Remove(headerName);
            this.version++;
        }
        private ValueCollection GetHeader(string headerName)
        {
            var values = default(ValueCollection);
            var knownHeaderIndex = GetKnownHeaderValue(headerName);
            if (knownHeaderIndex >= 0 && knownHeaderIndex <= ToInt(LastKnownHeader)) values = this.GetKnownHeader(knownHeaderIndex);
            else if (this.customHeaders != null) values = this.GetCustomHeader(headerName);

            return values;
        }
        private ValueCollection GetKnownHeader(int knownHeaderValue)
        {
            if (knownHeaderValue < ToInt(FirstKnownHeader) || knownHeaderValue > ToInt(LastKnownHeader))
                throw new ArgumentOutOfRangeException(nameof(knownHeaderValue));

            var values = this.knownHeaders?[knownHeaderValue] ?? default(ValueCollection);
            return values;
        }
        private ValueCollection GetCustomHeader(string customHeaderName)
        {
            if (customHeaderName == null) throw new ArgumentNullException(nameof(customHeaderName), "customHeaderName != null");

            var values = default(ValueCollection);
            this.customHeaders?.TryGetValue(customHeaderName, out values);

            return values;
        }

        private static int GetKnownHeaderValue(string headerName)
        {
            if (headerName == null) throw new ArgumentNullException(nameof(headerName), "key != null");

            var binIndex = Array.BinarySearch(KnownHeaderSortedNames, headerName, KeyComparer);
            if (binIndex >= 0 && binIndex <= KnownHeaderSortedValues.Length)
                return KnownHeaderSortedValues[binIndex];

            return -1;
        }
        private static string GetKnownHeaderName(int knownHeaderValue)
        {
            if (!(knownHeaderValue >= 0 && knownHeaderValue < KnownHeadersCapacity)) throw new ArgumentOutOfRangeException(nameof(knownHeaderValue), "index >= 0 && index < KnownHeadersCapacity");

            if (knownHeaderValue > KnownHeaderNames.Length) throw new ArgumentOutOfRangeException();

            return KnownHeaderNames[knownHeaderValue];
        }

        public static bool TryGetKnownHeaderByName(string knownHeaderName, out KnownHeaderT knownHeader)
        {
            if (knownHeaderName == null) throw new ArgumentNullException(nameof(knownHeaderName), "knownHeaderName != null");

            knownHeader = default(KnownHeaderT);
            var knownHeaderIndex = GetKnownHeaderValue(knownHeaderName);
            if (knownHeaderIndex < 0) return false;

            knownHeader = FromInt(knownHeaderIndex);
            return true;
        }
        public static string GetHeaderName(KnownHeaderT knownHeader)
        {
            return KnownHeaderNames[ToInt(knownHeader)];
        }

        private void ThrowIfReadOnly()
        {
            if (this.isReadOnly) throw new InvalidOperationException("Headers collection is read-only.");
        }

        public override string ToString()
        {
            return string.Join(", ", this);
        }
    }
}