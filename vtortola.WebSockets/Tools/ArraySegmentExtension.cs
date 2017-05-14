using System;

namespace vtortola.WebSockets.Tools
{
    internal static class ArraySegmentExtension
    {
        public static ArraySegment<T> NextSegment<T>(this ArraySegment<T> segment, int segmentSize)
        {
            if (segment.Array == null) throw new ArgumentNullException(nameof(segment));
            if (segmentSize < 0 || segment.Offset + segment.Count + segmentSize > segment.Array.Length) throw new ArgumentOutOfRangeException(nameof(segmentSize));

            return new ArraySegment<T>(segment.Array, segment.Offset + segment.Count, segmentSize);
        }
    }
}
