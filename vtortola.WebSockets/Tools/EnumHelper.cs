/*
	Copyright (c) 2017 Denis Zykov
	License: https://opensource.org/licenses/MIT
*/
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace vtortola.WebSockets.Tools
{
    internal static class EnumHelper
    {
        public static readonly bool PlatformSupportEnumInterchange;

        static EnumHelper()
        {
            try
            {
                // test if platform support of enum interchange in delegates
                // ReSharper disable ReturnValueOfPureMethodIsNotUsed
                Delegate.CreateDelegate(typeof(Action<int>), typeof(Console), "set_BackgroundColor");
                Delegate.CreateDelegate(typeof(Func<ConsoleColor>), typeof(Console), "get_CursorLeft");
                // ReSharper restore ReturnValueOfPureMethodIsNotUsed

                PlatformSupportEnumInterchange = true;
            }
            catch
            {
                PlatformSupportEnumInterchange = false;
            }
        }

        public static byte FromOrToUInt8(byte value) => value;
        public static sbyte FromOrToInt8(sbyte value) => value;
        public static short FromOrToInt16(short value) => value;
        public static int FromOrToInt32(int value) => value;
        public static long FromOrToInt64(long value) => value;
        public static ushort FromOrToUInt16(ushort value) => value;
        public static uint FromOrToUInt32(uint value) => value;
        public static ulong FromOrToUInt64(ulong value) => value;
    }

    internal static class EnumHelper<EnumT>
    {
        private static readonly SortedDictionary<EnumT, string> NamesByNumber;

        public static readonly Delegate ToNumber;
        public static readonly Delegate FromNumber;
        private static readonly Comparer<EnumT> Comparer;

        static EnumHelper()
        {
            var enumType = typeof(EnumT);
            if (enumType.IsEnum == false)
                throw new InvalidOperationException("TKnownHeader should be enum type.");

            var underlyingType = Enum.GetUnderlyingType(enumType);

            if (EnumHelper.PlatformSupportEnumInterchange)
            {
                switch (Type.GetTypeCode(underlyingType))
                {
                    case TypeCode.SByte:
                        ToNumber = Delegate.CreateDelegate(typeof(Func<EnumT, sbyte>), typeof(EnumHelper), nameof(EnumHelper.FromOrToInt8), throwOnBindFailure: true, ignoreCase: false);
                        FromNumber = Delegate.CreateDelegate(typeof(Func<sbyte, EnumT>), typeof(EnumHelper), nameof(EnumHelper.FromOrToInt8), throwOnBindFailure: true, ignoreCase: false);
                        Comparer = Comparer<EnumT>.Create((x, y) => ((Func<EnumT, sbyte>)ToNumber).Invoke(x).CompareTo(((Func<EnumT, sbyte>)ToNumber).Invoke(y)));
                        break;
                    case TypeCode.Byte:
                        ToNumber = Delegate.CreateDelegate(typeof(Func<EnumT, byte>), typeof(EnumHelper), nameof(EnumHelper.FromOrToUInt8), throwOnBindFailure: true, ignoreCase: false);
                        FromNumber = Delegate.CreateDelegate(typeof(Func<byte, EnumT>), typeof(EnumHelper), nameof(EnumHelper.FromOrToUInt8), throwOnBindFailure: true, ignoreCase: false);
                        Comparer = Comparer<EnumT>.Create((x, y) => ((Func<EnumT, byte>)ToNumber).Invoke(x).CompareTo(((Func<EnumT, byte>)ToNumber).Invoke(y)));
                        break;
                    case TypeCode.Int16:
                        ToNumber = Delegate.CreateDelegate(typeof(Func<EnumT, short>), typeof(EnumHelper), nameof(EnumHelper.FromOrToInt16), throwOnBindFailure: true, ignoreCase: false);
                        FromNumber = Delegate.CreateDelegate(typeof(Func<short, EnumT>), typeof(EnumHelper), nameof(EnumHelper.FromOrToInt16), throwOnBindFailure: true, ignoreCase: false);
                        Comparer = Comparer<EnumT>.Create((x, y) => ((Func<EnumT, short>)ToNumber).Invoke(x).CompareTo(((Func<EnumT, short>)ToNumber).Invoke(y)));
                        break;
                    case TypeCode.UInt16:
                        ToNumber = Delegate.CreateDelegate(typeof(Func<EnumT, ushort>), typeof(EnumHelper), nameof(EnumHelper.FromOrToUInt16), throwOnBindFailure: true, ignoreCase: false);
                        FromNumber = Delegate.CreateDelegate(typeof(Func<ushort, EnumT>), typeof(EnumHelper), nameof(EnumHelper.FromOrToUInt16), throwOnBindFailure: true, ignoreCase: false);
                        Comparer = Comparer<EnumT>.Create((x, y) => ((Func<EnumT, ushort>)ToNumber).Invoke(x).CompareTo(((Func<EnumT, ushort>)ToNumber).Invoke(y)));
                        break;
                    case TypeCode.Int32:
                        ToNumber = Delegate.CreateDelegate(typeof(Func<EnumT, int>), typeof(EnumHelper), nameof(EnumHelper.FromOrToInt32), throwOnBindFailure: true, ignoreCase: false);
                        FromNumber = Delegate.CreateDelegate(typeof(Func<int, EnumT>), typeof(EnumHelper), nameof(EnumHelper.FromOrToInt32), throwOnBindFailure: true, ignoreCase: false);
                        Comparer = Comparer<EnumT>.Create((x, y) => ((Func<EnumT, int>)ToNumber).Invoke(x).CompareTo(((Func<EnumT, int>)ToNumber).Invoke(y)));
                        break;
                    case TypeCode.UInt32:
                        ToNumber = Delegate.CreateDelegate(typeof(Func<EnumT, uint>), typeof(EnumHelper), nameof(EnumHelper.FromOrToUInt32), throwOnBindFailure: true, ignoreCase: false);
                        FromNumber = Delegate.CreateDelegate(typeof(Func<uint, EnumT>), typeof(EnumHelper), nameof(EnumHelper.FromOrToUInt32), throwOnBindFailure: true, ignoreCase: false);
                        Comparer = Comparer<EnumT>.Create((x, y) => ((Func<EnumT, uint>)ToNumber).Invoke(x).CompareTo(((Func<EnumT, uint>)ToNumber).Invoke(y)));
                        break;
                    case TypeCode.Int64:
                        ToNumber = Delegate.CreateDelegate(typeof(Func<EnumT, long>), typeof(EnumHelper), nameof(EnumHelper.FromOrToInt64), throwOnBindFailure: true, ignoreCase: false);
                        FromNumber = Delegate.CreateDelegate(typeof(Func<long, EnumT>), typeof(EnumHelper), nameof(EnumHelper.FromOrToInt64), throwOnBindFailure: true, ignoreCase: false);
                        Comparer = Comparer<EnumT>.Create((x, y) => ((Func<EnumT, long>)ToNumber).Invoke(x).CompareTo(((Func<EnumT, long>)ToNumber).Invoke(y)));
                        break;
                    case TypeCode.UInt64:
                        ToNumber = Delegate.CreateDelegate(typeof(Func<EnumT, ulong>), typeof(EnumHelper), nameof(EnumHelper.FromOrToUInt64), throwOnBindFailure: true, ignoreCase: false);
                        FromNumber = Delegate.CreateDelegate(typeof(Func<ulong, EnumT>), typeof(EnumHelper), nameof(EnumHelper.FromOrToUInt64), throwOnBindFailure: true, ignoreCase: false);
                        Comparer = Comparer<EnumT>.Create((x, y) => ((Func<EnumT, ulong>)ToNumber).Invoke(x).CompareTo(((Func<EnumT, ulong>)ToNumber).Invoke(y)));
                        break;
                    default: throw new ArgumentOutOfRangeException($"Unexpected underlying type '{underlyingType}' of enum '{enumType}'.");
                }
            }
            else
            {
                var valueParameter = Expression.Parameter(underlyingType, "value");
                var enumParameter = Expression.Parameter(enumType, "value");
                var xParameter = Expression.Parameter(enumType, "value");
                var yParameter = Expression.Parameter(enumType, "value");

                FromNumber = Expression.Lambda(Expression.ConvertChecked(valueParameter, enumType), valueParameter).Compile();
                ToNumber = Expression.Lambda(Expression.ConvertChecked(enumParameter, underlyingType), enumParameter).Compile();
                Comparer = Comparer<EnumT>.Create(Expression.Lambda<Comparison<EnumT>>(
                    Expression.Call
                    (
                        Expression.ConvertChecked(xParameter, underlyingType),
                        nameof(int.CompareTo),
                        Type.EmptyTypes,
                        Expression.ConvertChecked(yParameter, underlyingType)
                    ),
                    xParameter,
                    yParameter
                ).Compile());
            }

            NamesByNumber = new SortedDictionary<EnumT, string>(Comparer);
            foreach (EnumT value in Enum.GetValues(typeof(EnumT)))
                NamesByNumber[value] = value.ToString();
        }

        public static string ToName(EnumT value)
        {
            var name = default(string);
            if (NamesByNumber.TryGetValue(value, out name))
                return name;
            return value.ToString();
        }

        public static bool IsDefined(EnumT value)
        {
            return NamesByNumber.ContainsKey(value);
        }
    }
}
