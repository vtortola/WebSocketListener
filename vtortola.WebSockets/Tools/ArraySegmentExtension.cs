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
        public static ArraySegment<T> Skip<T>(this ArraySegment<T> segment, int offset)
        {
            if (segment.Array == null) throw new ArgumentNullException(nameof(segment));
            if (offset < 0 || offset > segment.Count) throw new ArgumentOutOfRangeException(nameof(offset));

            return new ArraySegment<T>(segment.Array, segment.Offset + offset, segment.Count - offset);
        }
        public static ArraySegment<T> Limit<T>(this ArraySegment<T> segment, int size)
        {
            if (segment.Array == null) throw new ArgumentNullException(nameof(segment));
            if (size < 0 || size > segment.Count) throw new ArgumentOutOfRangeException(nameof(size));

            return new ArraySegment<T>(segment.Array, segment.Offset, size);
        }
    }
}
