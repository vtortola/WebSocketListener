using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Tools
{
    public static class ByteArrayExtensions
    {
        public static void ShiftRight(this ArraySegment<Byte> segment, Int32 to, Int32 count)
        {
            if (count + to > segment.Count)
                throw new ArgumentException("The array is to small");

            if (count < 1)
                return;

            for (int i = (segment.Offset + count - 1) + to; i >= segment.Offset + to; i--)
                segment.Array[i] = segment.Array[i - to];
        }

        public static void ReversePortion(this Byte[] array, Int32 from, Int32 count)
        {
            if (count + from > array.Length)
                throw new ArgumentException("The array is to small");

            if (count < 1)
                return;

            Byte pivot;
            Int32 back = from + count - 1;
            Int32 half = (Int32)Math.Floor(count / 2f);
            for (int i = from; i < from + half; i++)
            {
                pivot = array[i];
                array[i] = array[back];
                array[back--] = pivot;
            }
        }

        public static void ToBytes(this UInt16 value, Byte[] buffer, Int32 offset)
        {
            for (int i = 0; i < 2; i++)
            {
                buffer[offset + i] = (byte)value;
                value >>= 8;
            }
        }

        public static void ToBytes(this UInt64 value, Byte[] buffer, Int32 offset)
        {
            for (int i = 0; i < 8; i++)
            {
                buffer[offset + i] = (byte)value;
                value >>= 8;
            }
        }

        public static void ToBytesBackwards(this UInt16 value, Byte[] buffer, Int32 offset)
        {
            for (int i = offset + 1; i >= offset; i--)
            {
                buffer[i] = (byte)value;
                value >>= 8;
            }
        }

        public static void ToBytesBackwards(this UInt64 value, Byte[] buffer, Int32 offset)
        {
            for (int i = offset + 7; i >= offset; i--)
            {
                buffer[i] = (byte)value;
                value >>= 8;
            }
        }
    }
}
