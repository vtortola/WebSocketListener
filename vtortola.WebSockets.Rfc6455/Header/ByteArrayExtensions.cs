using System;

namespace vtortola.WebSockets.Rfc6455
{
    internal static class ByteArrayExtensions
    {
        internal static void ReversePortion(this byte[] array, int from, int count)
        {
            if (count + from > array.Length)
                throw new ArgumentException("The array is to small");

            if (count < 1)
                return;

            byte pivot;
            int back = from + count - 1;
            int half = (int)Math.Floor(count / 2f);
            for (int i = from; i < from + half; i++)
            {
                pivot = array[i];
                array[i] = array[back];
                array[back--] = pivot;
            }
        }

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
    }
}
