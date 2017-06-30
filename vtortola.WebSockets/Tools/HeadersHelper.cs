﻿/*
	Copyright (c) 2017 Denis Zykov
	License: https://opensource.org/licenses/MIT
*/
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace vtortola.WebSockets.Tools
{
    internal static class HeadersHelper
    {
        public static IList<string> SplitAndTrimInList(string value, StringSplitOptions options = StringSplitOptions.None, char valuesSeparator = ';')
        {
            if (value == null)
                return (IList<string>)Enumerable.Empty<string>();

            var valuesCount = CountChars(value, valuesSeparator) + 1;

            var list = new List<string>(valuesCount);
            foreach (var v in SplitAndTrim(value, options, valuesSeparator))
                list.Add(v);

            return list;
        }

        public static IEnumerable<string> SplitAndTrim(string valueString, StringSplitOptions options = StringSplitOptions.None, char valuesSeparator = ';')
        {
            if (valueString == null)
                yield break;

            var startIndex = 0;
            do
            {
                var nextValueIndex = valueString.IndexOf(valuesSeparator, startIndex);
                if (nextValueIndex < 0)
                    nextValueIndex = valueString.Length;

                var valueStart = startIndex;
                var valueLength = Math.Max(0, nextValueIndex - startIndex);
                var value = string.Empty;

                TrimInPlace(valueString, ref valueStart, ref valueLength);

                if (valueLength > 0)
                    value = valueString.Substring(valueStart, valueLength);

                if (options != StringSplitOptions.RemoveEmptyEntries || !string.IsNullOrEmpty(value))
                    yield return value;

                startIndex = nextValueIndex + 1;
                continue;
            } while (startIndex < valueString.Length);
        }

        public static IEnumerable<KeyValuePair<string, string>> SplitAndTrimKeyValue(string valueString, char valuesSeparator = ';', char nameValueSeparator = '=', StringSplitOptions options = StringSplitOptions.None)
        {
            if (valueString == null)
                yield break;

            var startIndex = 0;
            do
            {
                var nextValueIndex = valueString.IndexOf(valuesSeparator, startIndex);
                if (nextValueIndex < 0)
                    nextValueIndex = valueString.Length;

                var equalsIndex = valueString.IndexOf(nameValueSeparator, startIndex, nextValueIndex - startIndex);
                if (equalsIndex < 0)
                    equalsIndex = startIndex - 1;

                var nameStart = startIndex;
                var nameLength = Math.Max(0, equalsIndex - startIndex);

                TrimInPlace(valueString, ref nameStart, ref nameLength);

                var name = string.Empty;
                if (nameLength > 0)
                    name = valueString.Substring(nameStart, nameLength);

                var valueStart = equalsIndex + 1;
                var valueLength = nextValueIndex - equalsIndex - 1;
                var value = string.Empty;

                TrimInPlace(valueString, ref valueStart, ref valueLength);

                if (valueLength > 0)
                    value = valueString.Substring(valueStart, valueLength);
                else
                    value = string.Empty;

                if (options == StringSplitOptions.None || (string.IsNullOrWhiteSpace(value) == false || string.IsNullOrWhiteSpace(name) == false))
                    yield return new KeyValuePair<string, string>(name, value);

                startIndex = nextValueIndex + 1;
            } while (startIndex < valueString.Length);
        }

        public static string Combine(IEnumerable<KeyValuePair<string, string>> keyValuePairs, char valuesSeparator = ';', char nameValueSeparator = '=')
        {
            if (keyValuePairs == null) throw new ArgumentNullException(nameof(keyValuePairs));

            var sb = new StringBuilder();

            foreach (var kv in keyValuePairs)
            {
                sb.Append(kv.Key)
                    .Append(nameValueSeparator)
                    .Append(kv.Value)
                    .Append(valuesSeparator);
            }

            if (sb.Length > 0) sb.Length--;

            return sb.ToString();
        }

        private static int CountChars(string value, char charToFind)
        {
            var count = 0;
            foreach (var ch in value)
            {
                if (ch == charToFind)
                    count++;
            }

            return count;
        }

        public static void TrimInPlace(string value, ref int startIndex, ref int length)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (startIndex < 0 || startIndex > value.Length) throw new ArgumentOutOfRangeException(nameof(startIndex));
            if (length < 0 || startIndex + length > value.Length) throw new ArgumentOutOfRangeException(nameof(length));

            if (length == 0)
                return;

            while (char.IsWhiteSpace(value[startIndex]) && length > 0)
            {
                startIndex++;
                length--;
            }

            if (length == 0) return;
            var end = startIndex + length - 1;
            while (char.IsWhiteSpace(value[end]) && length > 0)
            {
                end--;
                length--;
            }
        }
        public static void Skip(string value, ref int startIndex, UnicodeCategory category)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (startIndex < 0 || startIndex > value.Length) throw new ArgumentOutOfRangeException(nameof(startIndex));


            while (startIndex < value.Length && CharUnicodeInfo.GetUnicodeCategory(value[startIndex]) == category)
                startIndex++;
        }
    }
}