/*
	Copyright (c) 2017 Denis Zykov
	License: https://opensource.org/licenses/MIT
*/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace vtortola.WebSockets.Http
{
    partial class Headers<KnownHeaderT>
    {
        public struct ValueCollection : ICollection<string>, ICollection
        {
            private readonly string firstValue;
            private readonly List<string> restValues;

            public int Count { get; }
            object ICollection.SyncRoot => this.firstValue ?? (object)this.restValues;
            bool ICollection<string>.IsReadOnly => false;
            bool ICollection.IsSynchronized => false;
            public bool IsEmpty => this.firstValue == null;

            public string this[int index]
            {
                get
                {
                    if (index < 0 || index >= this.Count) throw new ArgumentOutOfRangeException(nameof(index));

                    return index == 0 ? this.firstValue : this.restValues[index - 1];
                }
            }

            public ValueCollection(string firstValue)
            {
                if (firstValue == null) throw new ArgumentNullException(nameof(firstValue), "firstValue != null");

                this.firstValue = firstValue;
                this.restValues = null;
                this.Count = 1;
            }
            public ValueCollection(IEnumerable<string> values)
            {
                if (values == null) throw new ArgumentNullException(nameof(values), "values != null");

                var list = values as List<string>;
                var array = values as string[];
                var abstractList = values as IList<string>;
                if (list != null) InitializeFromList(out this.firstValue, out this.restValues, list);
                else if (array != null) InitializeFromList(out this.firstValue, out this.restValues, array);
                else if (abstractList != null) InitializeFromList(out this.firstValue, out this.restValues, abstractList);
                else InitializeFromEnumerable(out this.firstValue, out this.restValues, values);

                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                // ReSharper disable once ConstantConditionalAccessQualifier
                this.Count = (this.firstValue != null ? 1 : 0) + (this.restValues?.Count ?? 0);
            }
            private ValueCollection(string firstValue, List<string> restValues)
            {
                if (firstValue == null) throw new ArgumentNullException(nameof(firstValue), "firstValue != null");
                if (restValues == null) throw new ArgumentNullException(nameof(restValues), "restValues != null");

                this.firstValue = firstValue;
                this.restValues = restValues;
                this.Count = 1 + restValues.Count;
            }

            private static void InitializeFromList<T>(out string firstValue, out List<string> restValues, T list) where T : IList<string>
            {
                if (list.Count > 0) firstValue = list[0];
                else firstValue = null;

                if (list.Count > 1)
                {
                    restValues = new List<string>(list.Count - 1);
                    for (var i = 1; i < list.Count; i++) restValues.Add(list[i]);
                }
                else restValues = null;
            }
            private static void InitializeFromEnumerable<T>(out string firstValue, out List<string> restValues, T enumerable) where T : IEnumerable<string>
            {
                firstValue = null;
                restValues = null;

                var first = true;
                foreach (var value in enumerable)
                {
                    if (first)
                    {
                        firstValue = value;
                        first = false;
                    }

                    if (restValues == null) restValues = new List<string>();

                    restValues.Add(value);
                }
            }

            public bool Contains(string value)
            {
                return this.Contains(value, StringComparison.Ordinal);
            }
            public bool Contains(string value, StringComparison comparison)
            {
                if (value == null) return false;

                var comparer = default(StringComparer);
                switch (comparison)
                {
                    case StringComparison.CurrentCulture: comparer = StringComparer.CurrentCulture; break;
                    case StringComparison.CurrentCultureIgnoreCase: comparer = StringComparer.CurrentCultureIgnoreCase; break;
                    case StringComparison.InvariantCulture: comparer = StringComparer.InvariantCulture; break;
                    case StringComparison.InvariantCultureIgnoreCase: comparer = StringComparer.InvariantCultureIgnoreCase; break;
                    case StringComparison.Ordinal: comparer = StringComparer.Ordinal; break;
                    case StringComparison.OrdinalIgnoreCase: comparer = StringComparer.OrdinalIgnoreCase; break;
                    default: comparer = StringComparer.Ordinal; break;
                }

                return comparer.Equals(this.firstValue, value) || (this.restValues?.Contains(value, comparer) ?? false);
            }
            public void CopyTo(Array array, int arrayIndex)
            {
                if (array == null || array.Length - arrayIndex < this.Count) throw new ArgumentException("Passed array is too small.", nameof(array));

                array.SetValue(this.firstValue, arrayIndex);
                arrayIndex++;

                if (this.restValues != null) (this.restValues as ICollection).CopyTo(array, arrayIndex);
            }

            void ICollection<string>.Add(string value)
            {
                throw new NotSupportedException();
            }
            void ICollection<string>.CopyTo(string[] array, int arrayIndex)
            {
                if (array == null || array.Length - arrayIndex < this.Count) throw new ArgumentException("Passed array is too small.", nameof(array));

                array[arrayIndex] = this.firstValue;
                arrayIndex++;

                this.restValues?.CopyTo(array, arrayIndex);
            }
            void ICollection<string>.Clear()
            {
                throw new NotSupportedException();
            }
            bool ICollection<string>.Remove(string item)
            {
                throw new NotSupportedException();
            }

            IEnumerator<string> IEnumerable<string>.GetEnumerator()
            {
                return this.GetEnumerator();
            }
            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
            public ValueCollectionEnumerator GetEnumerator()
            {
                return this.restValues != null
                    ? new ValueCollectionEnumerator(this.firstValue, this.restValues.GetEnumerator())
                    : new ValueCollectionEnumerator(this.firstValue);
            }

            public static ValueCollection operator +(ValueCollection list1, ValueCollection list2)
            {
                if (!list1.IsEmpty && list2.IsEmpty) return list1;
                if (list1.IsEmpty && !list2.IsEmpty) return list2;

                var otherValues = new List<string>();
                if (list1.restValues != null) otherValues.AddRange(list1.restValues);

                otherValues.Add(list2.firstValue);
                if (list2.restValues != null) otherValues.AddRange(list2.restValues);

                return new ValueCollection(list1.firstValue, otherValues);
            }
            public static ValueCollection operator -(ValueCollection list1, ValueCollection list2)
            {
                if (list1.IsEmpty || list2.IsEmpty) return list1;

                var values = new List<string>(list1.Count)
                {
                    list1.firstValue
                };

                if (list1.restValues != null) values.AddRange(list1.restValues);

                foreach (var value in list2)
                {
                    values.Remove(value);
                }

                return new ValueCollection(values);
            }

            public override string ToString()
            {
                if (this.restValues == null) return this.firstValue ?? string.Empty;

                var len = this.firstValue.Length + this.restValues.Sum(v => v.Length) + 2 * this.restValues.Count;
                var sb = new StringBuilder(len);
                sb.Append(this.firstValue);
                foreach (var headerValue in this.restValues)
                {
                    sb.Append(", ");
                    sb.Append(headerValue);
                }

                return sb.ToString();
            }

            public struct ValueCollectionEnumerator : IEnumerator<string>
            {
                private bool iterateRest;
                private List<string>.Enumerator restEnumerator;
                private readonly bool restEnumeratorIsSet;
                private readonly string firstValue;

                public ValueCollectionEnumerator(string firstValue, List<string>.Enumerator restEnumerator) : this()
                {
                    this.firstValue = firstValue;
                    this.restEnumerator = restEnumerator;
                    this.restEnumeratorIsSet = true;
                }
                public ValueCollectionEnumerator(string firstValue) : this()
                {
                    this.firstValue = firstValue;
                    this.restEnumerator = default(List<string>.Enumerator);
                    this.restEnumeratorIsSet = false;
                }

                public string Current { get; private set; }
                object IEnumerator.Current => this.Current;

                public bool MoveNext()
                {
                    if (!this.iterateRest)
                    {
                        this.iterateRest = true;
                        this.Current = this.firstValue;
                        return this.Current != null;
                    }

                    if (!this.restEnumeratorIsSet || !this.restEnumerator.MoveNext()) return false;

                    this.Current = this.restEnumerator.Current;
                    return true;
                }
                public void Reset()
                {
                    throw new NotSupportedException();
                }
                public void Dispose()
                {
                    if (this.restEnumeratorIsSet) this.restEnumerator.Dispose();
                }
            }
        }
    }
}