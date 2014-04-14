using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Rfc6455
{
    internal static class WebSocketFrameOptionExtensions
    {
        internal static Boolean IsData(this WebSocketFrameOption option)
        {
            return option == WebSocketFrameOption.Binary || option == WebSocketFrameOption.Text || option == WebSocketFrameOption.Continuation;
        }
    }

    internal static class ByteArrayExtensions
    {
        internal static void ShiftRight(this ArraySegment<Byte> segment, Int32 to, Int32 count)
        {
            if (count + to > segment.Count)
                throw new ArgumentException("The array is to small");

            if (count < 1)
                return;

            for (int i = (segment.Offset + count - 1) + to; i >= segment.Offset + to; i--)
                segment.Array[i] = segment.Array[i - to];
        }

        internal static void ReversePortion(this Byte[] array, Int32 from, Int32 count)
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
    }
}
