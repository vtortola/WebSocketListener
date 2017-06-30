/*
	Copyright (c) 2017 Denis Zykov
	License: https://opensource.org/licenses/MIT
*/
using System;
using System.Collections.Generic;

namespace vtortola.WebSockets.Tools
{
    public static class HashSetExtensions
    {
        public static void AddRange<T>(this HashSet<T> hashSet, IEnumerable<T> values)
        {
            if (hashSet == null) throw new ArgumentNullException(nameof(hashSet), "hashSet != null");
            if (values == null) throw new ArgumentNullException(nameof(values), "values != null");

            foreach (var value in values)
            {
                hashSet.Add(value);
            }
        }
    }
}