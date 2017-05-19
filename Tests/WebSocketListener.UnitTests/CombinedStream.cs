using System.IO;

namespace vtortola.WebSockets.UnitTests
{
    public sealed class CombinedStream : Stream
    {
        private readonly Stream readStream;
        private readonly Stream writeStream;
        /// <inheritdoc />
        public override bool CanRead => this.readStream.CanRead;
        /// <inheritdoc />
        public override bool CanSeek => this.readStream.CanSeek;
        /// <inheritdoc />
        public override bool CanWrite => this.writeStream.CanWrite;
        /// <inheritdoc />
        public override long Length => this.readStream.Length;
        /// <inheritdoc />
        public override long Position { get => this.readStream.Position; set => this.readStream.Position = value; }

        public CombinedStream(Stream readStream, Stream writeStream)
        {
            this.readStream = readStream;
            this.writeStream = writeStream;
        }

        /// <inheritdoc />
        public override void Flush()
        {
            this.writeStream.Flush();
        }
        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin)
        {
            return this.readStream.Seek(offset, origin);
        }
        /// <inheritdoc />
        public override void SetLength(long value)
        {
            this.readStream.SetLength(value);
        }
        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            return this.readStream.Read(buffer, offset, count);
        }
        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            this.writeStream.Write(buffer, offset, count);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            this.readStream.Dispose();
            this.writeStream.Dispose();
            base.Dispose(disposing);
        }
    }
}