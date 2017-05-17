#if (NET45 || NET451 || NET452 || NET46 || DNX451 || DNX452 || DNX46)
using System;
using System.IO;
using System.IO.Pipes;
using System.Net;

namespace vtortola.WebSockets.Transports.NamedPipes
{
    public class NamedPipeConnection : Connection
    {
        private readonly PipeStream pipeStream;

        public string PipeName { get; }
        /// <inheritdoc />
        public override EndPoint LocalEndPoint { get; }
        /// <inheritdoc />
        public override EndPoint RemoteEndPoint { get; }
        /// <inheritdoc />
        public override bool ShouldBeSecure => false;

        public NamedPipeConnection(PipeStream pipeStream, string pipeName)
        {
            if (pipeStream == null) throw new ArgumentNullException(nameof(pipeStream));
            if (pipeName == null) throw new ArgumentNullException(nameof(pipeName));

            this.pipeStream = pipeStream;
            this.PipeName = pipeName;
            this.RemoteEndPoint = this.LocalEndPoint = new NamedPipeEndPoint(pipeName);
        }

        /// <inheritdoc />
        public override Stream GetDataStream()
        {
            return this.pipeStream;
        }
        /// <inheritdoc />
        public override void Dispose(bool disposed)
        {
            SafeEnd.Dispose(this.pipeStream);
        }
        /// <inheritdoc />
        public override string ToString()
        {
            return $"{nameof(NamedPipeConnection)}, pipe: {this.PipeName}";
        }
    }
}
#endif