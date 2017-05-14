using System;

namespace vtortola.WebSockets.Rfc6455
{
    internal static class ByteArrayExtensions
    {
        internal static void ReversePortion(this byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));

            if (count + offset > buffer.Length)
                throw new ArgumentException("The array is to small");

            if (count < 1)
                return;

            byte pivot;
            var back = offset + count - 1;
            var half = (int)Math.Floor(count / 2f);
            for (var i = offset; i < offset + half; i++)
            {
                pivot = buffer[i];
                buffer[i] = buffer[back];
                buffer[back--] = pivot;
            }
        }

        internal static void ToBytes(this ushort value, byte[] buffer, int offset)
        {
            for (var i = 0; i < 2; i++)
            {
                buffer[offset + i] = (byte)value;
                value >>= 8;
            }
        }

        internal static void ToBytes(this ulong value, byte[] buffer, int offset)
        {
            for (var i = 0; i < 8; i++)
            {
                buffer[offset + i] = (byte)value;
                value >>= 8;
            }
        }

        internal static void ToBytesBackwards(this ushort value, byte[] buffer, int offset)
        {
            for (var i = offset + 1; i >= offset; i--)
            {
                buffer[i] = (byte)value;
                value >>= 8;
            }
        }

        internal static void ToBytesBackwards(this ulong value, byte[] buffer, int offset)
        {
            for (var i = offset + 7; i >= offset; i--)
            {
                buffer[i] = (byte)value;
                value >>= 8;
            }
        }
    }
}
