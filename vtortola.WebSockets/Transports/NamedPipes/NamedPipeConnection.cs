/*
	Copyright (c) 2017 Denis Zykov
	License: https://opensource.org/licenses/MIT
*/
#if !NAMED_PIPES_DISABLE
using System;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Tools;

namespace vtortola.WebSockets.Transports.NamedPipes
{
    public class NamedPipeConnection : NetworkConnection
    {
        private readonly PipeStream pipeStream;

        public string PipeName { get; }

        public virtual int OutBufferSize => this.pipeStream.OutBufferSize;
        public virtual int InBufferSize => this.pipeStream.InBufferSize;
        public bool IsConnected => this.pipeStream.IsConnected;
        public bool CanWrite => this.pipeStream.CanWrite;
        public bool CanRead => this.pipeStream.CanRead;
        public bool IsAsync => this.pipeStream.IsAsync;

        /// <inheritdoc />
        public override EndPoint LocalEndPoint { get; }
        /// <inheritdoc />
        public override EndPoint RemoteEndPoint { get; }

        public NamedPipeConnection(PipeStream pipeStream, string pipeName)
        {
            if (pipeStream == null) throw new ArgumentNullException(nameof(pipeStream));
            if (pipeName == null) throw new ArgumentNullException(nameof(pipeName));

            this.pipeStream = pipeStream;
            this.PipeName = pipeName;
            this.RemoteEndPoint = this.LocalEndPoint = new NamedPipeEndPoint(pipeName);
        }

        /// <inheritdoc />
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return this.pipeStream.ReadAsync(buffer, offset, count, cancellationToken);
        }
        /// <inheritdoc />
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return this.pipeStream.WriteAsync(buffer, offset, count, cancellationToken);
        }
        /// <inheritdoc />
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return TaskHelper.CanceledTask;

            return this.pipeStream.FlushAsync(cancellationToken);
        }

        /// <inheritdoc />
        public override Stream AsStream()
        {
            return this.pipeStream;
        }

        /// <inheritdoc />
        public override Task CloseAsync()
        {
            try
            {
                this.pipeStream.Close();
                return TaskHelper.CompletedTask;
            }
            catch (Exception closeError) when (closeError.Unwrap() is ThreadAbortException == false)
            {
                if (closeError is ObjectDisposedException)
                    return TaskHelper.CompletedTask;

                return TaskHelper.FailedTask(closeError);
            }

        }

        /// <inheritdoc />
        public override void Dispose(bool disposed)
        {
            SafeEnd.Dispose(this.pipeStream);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            // ReSharper disable HeapView.BoxingAllocation
            return $"{nameof(NamedPipeConnection)}, pipe: {this.PipeName}, connected: {this.IsConnected}, " +
                $"can write: {this.CanWrite}, can read: {this.CanRead} in buffer: {this.InBufferSize}, out buffer: {this.OutBufferSize}";
            // ReSharper restore HeapView.BoxingAllocation
        }
    }
}
#endif