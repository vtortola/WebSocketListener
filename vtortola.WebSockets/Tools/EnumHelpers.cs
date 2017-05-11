using System;
using System.Linq.Expressions;

namespace vtortola.WebSockets.Tools
{
    internal static class EnumHelpers
    {
        public static readonly bool PlatformSupportEnumInterchange;

        static EnumHelpers()
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

    internal static class EnumHelpers<EnumT>
    {
        public static readonly Delegate ToNumber;
        public static readonly Delegate FromNumber;

        static EnumHelpers()
        {
            var enumType = typeof(EnumT);
            if (enumType.IsEnum == false)
                throw new InvalidOperationException("TKnownHeader should be enum type.");

            var underlyingType = Enum.GetUnderlyingType(enumType);

            if (EnumHelpers.PlatformSupportEnumInterchange)
            {
                switch (Type.GetTypeCode(underlyingType))
                {
                    case TypeCode.SByte:
                        ToNumber = Delegate.CreateDelegate(typeof(Func<EnumT, sbyte>), typeof(EnumHelpers), nameof(EnumHelpers.FromOrToInt8), throwOnBindFailure: true, ignoreCase: false);
                        FromNumber = Delegate.CreateDelegate(typeof(Func<sbyte, EnumT>), typeof(EnumHelpers), nameof(EnumHelpers.FromOrToInt8), throwOnBindFailure: true, ignoreCase: false);
                        break;
                    case TypeCode.Byte:
                        ToNumber = Delegate.CreateDelegate(typeof(Func<EnumT, byte>), typeof(EnumHelpers), nameof(EnumHelpers.FromOrToUInt8), throwOnBindFailure: true, ignoreCase: false);
                        FromNumber = Delegate.CreateDelegate(typeof(Func<byte, EnumT>), typeof(EnumHelpers), nameof(EnumHelpers.FromOrToUInt8), throwOnBindFailure: true, ignoreCase: false);
                        break;
                    case TypeCode.Int16:
                        ToNumber = Delegate.CreateDelegate(typeof(Func<EnumT, short>), typeof(EnumHelpers), nameof(EnumHelpers.FromOrToInt16), throwOnBindFailure: true, ignoreCase: false);
                        FromNumber = Delegate.CreateDelegate(typeof(Func<short, EnumT>), typeof(EnumHelpers), nameof(EnumHelpers.FromOrToInt16), throwOnBindFailure: true, ignoreCase: false);
                        break;
                    case TypeCode.UInt16:
                        ToNumber = Delegate.CreateDelegate(typeof(Func<EnumT, ushort>), typeof(EnumHelpers), nameof(EnumHelpers.FromOrToUInt16), throwOnBindFailure: true, ignoreCase: false);
                        FromNumber = Delegate.CreateDelegate(typeof(Func<ushort, EnumT>), typeof(EnumHelpers), nameof(EnumHelpers.FromOrToUInt16), throwOnBindFailure: true, ignoreCase: false);
                        break;
                    case TypeCode.Int32:
                        ToNumber = Delegate.CreateDelegate(typeof(Func<EnumT, int>), typeof(EnumHelpers), nameof(EnumHelpers.FromOrToInt32), throwOnBindFailure: true, ignoreCase: false);
                        FromNumber = Delegate.CreateDelegate(typeof(Func<int, EnumT>), typeof(EnumHelpers), nameof(EnumHelpers.FromOrToInt32), throwOnBindFailure: true, ignoreCase: false);
                        break;
                    case TypeCode.UInt32:
                        ToNumber = Delegate.CreateDelegate(typeof(Func<EnumT, uint>), typeof(EnumHelpers), nameof(EnumHelpers.FromOrToUInt32), throwOnBindFailure: true, ignoreCase: false);
                        FromNumber = Delegate.CreateDelegate(typeof(Func<uint, EnumT>), typeof(EnumHelpers), nameof(EnumHelpers.FromOrToUInt32), throwOnBindFailure: true, ignoreCase: false);
                        break;
                    case TypeCode.Int64:
                        ToNumber = Delegate.CreateDelegate(typeof(Func<EnumT, long>), typeof(EnumHelpers), nameof(EnumHelpers.FromOrToInt64), throwOnBindFailure: true, ignoreCase: false);
                        FromNumber = Delegate.CreateDelegate(typeof(Func<long, EnumT>), typeof(EnumHelpers), nameof(EnumHelpers.FromOrToInt64), throwOnBindFailure: true, ignoreCase: false);
                        break;
                    case TypeCode.UInt64:
                        ToNumber = Delegate.CreateDelegate(typeof(Func<EnumT, ulong>), typeof(EnumHelpers), nameof(EnumHelpers.FromOrToUInt64), throwOnBindFailure: true, ignoreCase: false);
                        FromNumber = Delegate.CreateDelegate(typeof(Func<ulong, EnumT>), typeof(EnumHelpers), nameof(EnumHelpers.FromOrToUInt64), throwOnBindFailure: true, ignoreCase: false);
                        break;
                    default: throw new ArgumentOutOfRangeException($"Unexpected underlying type '{underlyingType}' of enum '{enumType}'.");
                }
            }
            else
            {
                var valueParameter = Expression.Parameter(underlyingType, "value");
                var enumParameter = Expression.Parameter(enumType, "value");

                FromNumber = Expression.Lambda(Expression.ConvertChecked(valueParameter, enumType), valueParameter).Compile();
                ToNumber = Expression.Lambda(Expression.ConvertChecked(enumParameter, underlyingType), enumParameter).Compile();
            }
        }
    }
}
