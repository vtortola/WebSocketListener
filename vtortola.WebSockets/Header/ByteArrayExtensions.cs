using System;

namespace vtortola.WebSockets.Rfc6455
{
    internal static class ByteArrayExtensions
    {
        internal static void ToBytes(this ushort value, byte[] buffer, int offset)
        {
            for (int i = 0; i < 2; i++)
            {
                buffer[offset + i] = (byte)value;
                value >>= 8;
            }
        }

        internal static void ToBytes(this ulong value, byte[] buffer, int offset)
        {
            for (int i = 0; i < 8; i++)
            {
                buffer[offset + i] = (byte)value;
                value >>= 8;
            }
        }

        internal static void ToBytesBackwards(this ushort value, byte[] buffer, int offset)
        {
            for (int i = offset + 1; i >= offset; i--)
            {
                buffer[i] = (byte)value;
                value >>= 8;
            }
        }

        internal static void ToBytesBackwards(this ulong value, byte[] buffer, int offset)
        {
            for (int i = offset + 7; i >= offset; i--)
            {
                buffer[i] = (byte)value;
                value >>= 8;
            }
        }

        static bool GetBit(byte aByte, int pos)
        {
            //left-shift 1, then bitwise AND, then check for non-zero
            return ((aByte & (1 << pos)) != 0);
        }

        static ushort ToUInt16Left2Right(this byte[] array, int from)
        {
            checked
            {
                ushort value = 0;
                int pos = 0;
                for (int b = from; b < from + 2; b++)
                {
                    for (int p = 0; p < 8; p++)
                    {
                        if (GetBit(array[b], p))
                        {
                            value += (ushort)Math.Pow(2, pos);
                        }
                        pos++;
                    }
                }
                return value;
            }
        }

        static ushort ToUInt16RightToLeft(this byte[] array, int from)
        {
            checked
            {
                ushort value = 0;
                int pos = 0;
                for (int b = from + 1; b >= from; b--)
                {
                    for (int p = 0; p < 8; p++)
                    {
                        if (GetBit(array[b], p))
                        {
                            value += (ushort)Math.Pow(2, pos);
                        }
                        pos++;
                    }
                }
                return value;
            }
        }

        internal static ushort ToUInt16(this byte[] array, int from, bool isLittleEndian)
            => !isLittleEndian ? ToUInt16Left2Right(array, from) : ToUInt16RightToLeft(array, from);

        static ulong ToUInt64LeftToRight(this byte[] array, int from)
        {
            checked
            {
                ulong value = 0;
                int pos = 0;
                for (int b = from; b < from + 8; b++)
                {
                    for (int p = 0; p < 8; p++)
                    {
                        if (GetBit(array[b], p))
                        {
                            value += (ulong)Math.Pow(2, pos);
                        }
                        pos++;
                    }
                }
                return value;
            }
        }

        static ulong ToUInt64RightToLeft(this byte[] array, int from)
        {
            checked
            {
                ulong value = 0;
                int pos = 0;
                for (int b = from + 7; b >= from; b--)
                {
                    for (int p = 0; p < 8; p++)
                    {
                        if (GetBit(array[b], p))
                        {
                            value += (ulong)Math.Pow(2, pos);
                        }
                        pos++;
                    }
                }
                return value;
            }
        }

        internal static ulong ToUInt64(this byte[] array, int from, bool isLittleEndian)
             => !isLittleEndian ? ToUInt64LeftToRight(array, from) : ToUInt64RightToLeft(array, from);

    }
}
